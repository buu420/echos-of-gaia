"""All-field static navigation census (read-only).

For every installed US field script (p0data7.bin) and its BGS/BGI (p0data11-19.bin) this reports:
  * every function containing MAPJUMP/WMAPJUMP, its owner (region/actor/code), tag, path features;
  * a static replica of the NativeFieldTargets exit gates for region touch exits
      G1 ContainsJump(own touch)  G2 FieldDoorEvidence.HasWalkOut  G3 HasPlainWalkOut (no BGA)
      G4 permitted outer boundary sample inside polygon (static BGI)
      G5 EntranceOnScreen: sample inside viewport with the player standing at it (camera follow model)
  * indirect region transitions (touch/talk -> REQ* -> function with a jump) that the runtime never sees;
  * actors (NEW3) with model, talk/push/talkable state and the runtime category they would receive.
Static only: runtime branch state, scripted camera changes, BGI triangle toggles and CFLAG/visibility changes
are NOT evaluated; those are reported as conditions, never as proof of visibility.
"""
import csv
import json
import re
import sys
from collections import defaultdict
from pathlib import Path

from ebscript import Script, load_field_scripts, OP
from fieldgeo import field_asset, parse_bgi, parse_bgs_cameras, outer_edges, bgpix, camera_center, in_view, sha

from audit_paths import OUTPUT_ROOT as HERE, SOURCE_ROOT
SRC = SOURCE_ROOT / "Assembly-CSharp"
MAPJUMP, WMAPJUMP, MJPOS, MOVQ, MOVE, MOVJ = OP['MAPJUMP'], OP['WMAPJUMP'], OP['MJPOS'], OP['MOVQ'], OP['MOVE'], OP['MOVJ']
REQS = {OP['REQ'], OP['REQSW'], OP['REQEW']}
BGA = {OP[n] for n in ('BGAANIME', 'BGAACTIVE', 'BGARATE', 'BGAWAIT', 'BGAFLAG', 'BGARANGE', 'BGAVISIBLE', 'BGAFRAME')}
BARRIERS = {OP[n] for n in ('MES', 'MESN', 'MESA', 'MESAN', 'MESB', 'WAITMES', 'CHOOSEPARAM')}
NEW, NEW2, NEW3, CC, MODEL, QUAD = OP['NEW'], OP['NEW2'], OP['NEW3'], OP['CC'], OP['MODEL'], OP['QUAD']
INPUT_OPS = {79, 88, 89, 90, 91, 92}
POLL_OPS = {94, 97, 112, 113}  # DISTANCE, DISTANCEA, BGIID, BGIFLOOR


def tables():
    ev = (SRC / 'Global/ff9/FF9DBAll.Events.cs').read_text(encoding='utf-8-sig')
    evdb = {int(a): b for a, b in re.findall(r'\{(\d+),\s*"(EVT_[A-Z0-9_]+)"\}', ev)}
    fb = (SRC / 'Global/Event/Engine/EventEngineUtils.cs').read_text(encoding='utf-8-sig')
    block = re.search(r'eventIDToFBGID = new Dictionary<Int32, String>\s*\{(.*?)\};', fb, re.S).group(1)
    fbg = {int(a): b for a, b in re.findall(r'\{(\d+),\s*"([A-Z0-9_]+)"\}', block)}
    geo_src = (SRC / 'Global/ff9/Battle/FF9BattleDB.GEO.cs').read_text(encoding='utf-8-sig')
    geo = {int(a): b for a, b in re.findall(r'\{\s*(\d+)\s*,\s*"(GEO_[A-Z0-9_]+)"\s*\}', geo_src)}
    return evdb, fbg, geo


def s16(v):
    return v - 65536 if v >= 32768 else v


def branch_targets(ins):
    """Successor offsets (static, all branches) following NativeFieldTargets/ScriptBranches semantics."""
    op = ins.op
    if op in (1, 2, 3):
        a = ins.c(0)
        if a is None:
            return []
        tgt = ins.next + (a if op == 2 else s16(a))
        return [tgt] if op == 1 else [tgt, ins.next]
    if op == 6:
        n = len(ins.args)
        return [ins.off + 4 + ins.args[i] for i in range(0, n, 2) if isinstance(ins.args[i], int)] if n >= 1 and n % 2 == 1 else []
    if op in (11, 13):
        origin = ins.off + (1 if op == 11 else 2)
        return [origin + a for a in ins.args[1:] if isinstance(a, int)]
    if op == 4:
        return []
    return [ins.next]


def reads_input(ins):
    for a in ins.args:
        if not isinstance(a, int) and (a.opset() & INPUT_OPS):
            return True
    return False


def expr_ops(ins):
    s = set()
    for a in ins.args:
        if not isinstance(a, int):
            s |= a.opset()
    return s


def reads_player_xz(ins):
    for a in ins.args:
        if isinstance(a, int):
            continue
        for op, arg in a.ops:
            if op == 0x78 and arg and arg[0] in (250, 251, 252, 253, 254) and arg[1] in (0, 2):
                return True
    return False


def player_walks(code, ):
    """FieldDoorEvidence.PlayerWalks: MOVE(sysvar10, sysvar11) reachable without barrier/return/input."""
    if not code:
        return False
    offs = {i.off: i for i in code}
    pend = [code[0].off]
    seen = set()
    while pend and len(seen) < 1024:
        o = pend.pop()
        if o in seen:
            continue
        seen.add(o)
        i = offs.get(o)
        if i is None:
            continue
        if i.op == MOVE and len(i.args) >= 2 and not isinstance(i.args[0], int) and not isinstance(i.args[1], int) \
                and i.args[0].is_sysvar(10) and i.args[1].is_sysvar(11):
            return True
        if i.op == 4 or i.op in BARRIERS or reads_input(i):
            continue
        pend.extend(branch_targets(i))
    return False


def door_paths(code, player_funcs):
    """FieldDoorEvidence.ReadPaths: (walk, animations, barrier_free_jump, any_jump_path_features)."""
    walk = False
    anims = set()
    free_jump = False
    if not code:
        return walk, anims, free_jump
    offs = {i.off: i for i in code}
    pend = [(code[0].off, False, False, -1)]
    seen = set()
    while pend:
        st = pend.pop()
        if st in seen:
            continue
        seen.add(st)
        if len(seen) > 8192:
            return False, set(), free_jump
        o, pos, wk, an = st
        i = offs.get(o)
        if i is None:
            continue
        if i.op in (MAPJUMP, WMAPJUMP):
            free_jump = True
            if pos and wk:
                walk = True
                if an >= 0:
                    anims.add(an)
            continue
        if i.op == 4 or i.op in BARRIERS or reads_input(i):
            continue
        if i.op == MJPOS:
            pos = True
        if i.op == MOVQ:
            wk = True
        if i.op in REQS and i.c(0) is not None and i.c(1) == 250 and i.c(2) is not None and player_funcs is not None:
            wk = wk or any(player_walks(f.get(i.c(2), (0, 0, []))[2]) for f in player_funcs)
        if i.op in BGA:
            an = i.c(0) if i.c(0) is not None else -1
        for t in branch_targets(i):
            pend.append((t, pos, wk, an))
    return walk, anims, free_jump


def jumps_in(code):
    out = []
    for i in code:
        if i.op in (MAPJUMP, WMAPJUMP):
            d = i.c(0)
            out.append({'op': 'MAPJUMP' if i.op == MAPJUMP else 'WMAPJUMP', 'offset': i.off, 'destination': d if d is not None else 'expr'})
    return out


def features(code):
    f = set()
    for i in code:
        if i.op in BARRIERS:
            f.add('dialogue/choice')
        if reads_input(i):
            f.add('key-input')
        if i.op == MJPOS:
            f.add('MJPOS')
        if i.op == MOVQ:
            f.add('MOVQ')
        if i.op == MOVJ:
            f.add('MOVJ')
        if i.op in BGA:
            f.add('BGA')
        if i.op in REQS:
            f.add('REQ')
        if i.op == MOVE:
            f.add('MOVE')
        if i.op in (OP['ENCOUNT'], OP['ENCOUNT2'], OP['BTLSET'] if 'BTLSET' in OP else -1):
            f.add('battle')
        if i.op == OP['FMV']:
            f.add('FMV')
        if expr_ops(i) & POLL_OPS:
            f.add('distance/triangle-read')
        if reads_player_xz(i):
            f.add('player-xz-read')
        if i.op == 5 and i.args and not isinstance(i.args[0], int) and i.args[0].is_sysvar(2):
            f.add('usercontrol-guard')
    return sorted(f)


def talkable(entry):
    """EventEngine.IsActuallyTalkable on the static entry bytes."""
    if 3 not in entry.funcs:
        return False
    ip = entry.funcs[3][0]
    d = entry.data
    if ip + 8 >= len(d):
        return True
    return not (d[ip + 7] == 0x04 and d[ip + 8] == 0x04)


def polygon(entry):
    polys = []
    dynamic = False
    if 0 in entry.funcs:
        for i in entry.funcs[0][2]:
            if i.op == QUAD:
                if all(isinstance(a, int) for a in i.args[1:]) and i.args:
                    vals = i.args[1:] if len(i.args) == i.args[0] + 1 else i.args
                    pts = [(s16(v & 0xffff), s16((v >> 16) & 0xffff)) for v in vals]
                    polys.append(pts)
                else:
                    dynamic = True
    return polys, dynamic


def in_quad(q, x, z):
    def sign(p1, p2, p3):
        return (p1[0] - p3[0]) * (p2[1] - p3[1]) - (p2[0] - p3[0]) * (p1[1] - p3[1])
    n = len(q)
    for k in range(n):
        a, b, c = q[k % n], q[(k + 1) % n], q[(k + 2) % n]
        d1, d2, d3 = sign((x, z), a, b), sign((x, z), b, c), sign((x, z), c, a)
        if not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0)):
            return True
    return False


def no_edge_reason(poly, bgi):
    """Why a polygon has no runtime-permitted outer boundary sample."""
    disabled = blocked = interior = 0
    for t in bgi['tris']:
        for e in range(3):
            a, b = t['v'][e], t['v'][(e + 1) % 3]
            hit = any(in_quad(poly, a[0] + (b[0] - a[0]) * s / 8.0, a[2] + (b[2] - a[2]) * s / 8.0) for s in range(9))
            if not hit:
                continue
            if t['n'][(e + 1) % 3] >= 0:
                interior += 1
            elif not t['enabled']:
                disabled += 1
            elif t['attr'] & 0x80:
                blocked += 1
    if disabled:
        return 'outer edge only on statically disabled triangles (%d)' % disabled
    if blocked:
        return 'outer edge only on player-blocked (attr 0x80) triangles (%d)' % blocked
    if interior:
        return 'interior region: only shared walkmesh edges inside polygon (%d)' % interior
    return 'polygon does not overlap any walkmesh edge'


def geometry_gates(poly, bgi, cams):
    samples = []
    for tid, e, a, b in outer_edges(bgi):
        for s in range(9):
            p = tuple(a[i] + (b[i] - a[i]) * s / 8.0 for i in range(3))
            if in_quad(poly, p[0], p[2]):
                samples.append((tid, e, p))
    res = {'outerSamples': len(samples), 'outerEdges': len(set((t, e) for t, e, _ in samples)), 'cameras': []}
    for c in cams:
        info = {'camera': c['index']}
        for label, hw, ws in (('native4x3', 160, False), ('wide16x9', 199, True)):
            at_exit = 0
            anywhere = 0
            miss = None
            for tid, e, p in samples:
                px = bgpix(c, p, hw)
                aim = bgpix(c, (p[0], p[1] - 324, p[2]), hw)
                cen = camera_center(c, aim, hw, ws)
                if in_view(px, cen, hw):
                    at_exit += 1
                vminx, vmaxx = c['vrpMinX'], c['vrpMaxX']
                if ws:
                    dlt = min(2 * hw - 320, vmaxx - vminx) // 2
                    vminx, vmaxx = vminx + dlt, vmaxx - dlt
                best = (min(max(px[0], vminx), vmaxx), min(max(px[1], c['vrpMinY']), c['vrpMaxY']))
                if in_view(px, best, hw):
                    anywhere += 1
                dx = max(0.0, abs(px[0] - best[0]) - hw)
                dy = max(0.0, abs(px[1] - best[1]) - 112)
                m = max(dx, dy)
                if miss is None or m < miss:
                    miss = m
            info[label] = {'inViewAtExit': at_exit, 'inViewBestCamera': anywhere, 'minOffscreenPx': None if miss is None else round(miss, 2)}
        res['cameras'].append(info)
    return res


def main():
    evdb, fbg, geo = tables()
    scripts, bundle_sha = load_field_scripts()
    by_name = defaultdict(list)
    for fid, name in evdb.items():
        by_name[name].append(fid)
    field_ids = sorted(fid for fid, name in evdb.items() if name in scripts)
    outside_field_assets = sorted(fid for fid, name in evdb.items() if name not in scripts)
    unmapped_assets = sorted(set(scripts) - set(evdb.values()))
    if unmapped_assets:
        raise ValueError('Installed field assets without an event mapping: ' + ', '.join(unmapped_assets))
    report = {'schema': 2, 'p0data7Sha256': bundle_sha, 'installedEnglishFieldAssets': len(scripts),
              'unmappedInstalledAssets': unmapped_assets, 'fields': [],
              'eventMappingsOutsideInstalledFieldAssets': outside_field_assets,
              'mappingNote': 'The event database also maps battle/world/non-installed events; these are not missing field assets.'}
    rows = []
    geo_cache = {}
    for fid in field_ids:
        name = evdb[fid]
        path, raw = scripts[name]
        s = Script(raw)
        scene = fbg.get(fid)
        F = {'field': fid, 'event': name, 'scene': scene, 'scriptSha256': s.sha, 'entries': len(s.entries), 'decodeErrors': [],
             'exits': [], 'indirect': [], 'otherTransitions': [], 'actors': [], 'regionsWithoutJump': 0, 'geometry': None}
        for e in s.entries:
            for err in e.errors:
                F['decodeErrors'].append('entry %d %s' % (e.sid, err))
        # creation and uid map
        created = defaultdict(set)
        uid_to_sid = {}
        players = set()
        for e in s.entries:
            for tag, (_, _, code) in e.funcs.items():
                for i in code:
                    if i.op in (NEW, NEW2, NEW3) and i.c(0) is not None:
                        created[i.c(0)].add({NEW: 'code', NEW2: 'region', NEW3: 'actor'}[i.op])
                        uid_to_sid[i.c(1) or i.c(0)] = i.c(0)
                    if i.op == CC:
                        players.add(e.sid)
        player_funcs = [s.entries[p].funcs for p in sorted(players) if p < len(s.entries)]

        def callees(sid, tag, depth=0, seen=None):
            seen = seen if seen is not None else set()
            if (sid, tag) in seen or depth > 6 or sid >= len(s.entries) or tag not in s.entries[sid].funcs:
                return seen
            seen.add((sid, tag))
            for i in s.entries[sid].funcs[tag][2]:
                if i.op in REQS and i.c(1) is not None and i.c(2) is not None:
                    u = i.c(1)
                    tgts = sorted(players) if u == 250 else [uid_to_sid.get(u, u)] if u < 250 else []
                    for t in tgts:
                        callees(t, i.c(2), depth + 1, seen)
            return seen

        jumpfuncs = {}
        for e in s.entries:
            for tag, (_, _, code) in e.funcs.items():
                j = jumps_in(code)
                if j:
                    jumpfuncs[(e.sid, tag)] = j
        # geometry (lazy)
        bgi = cams = None
        if scene:
            if scene not in geo_cache:
                try:
                    bb, braw = field_asset(scene, 'bgs')
                    ib, iraw = field_asset(scene, 'bgi')
                    geo_cache[scene] = (parse_bgi(iraw), parse_bgs_cameras(braw), sha(iraw), sha(braw)) if braw and iraw else None
                except Exception as ex:
                    geo_cache[scene] = None
                    F['decodeErrors'].append('geometry: %s' % ex)
            g = geo_cache[scene]
            if g:
                bgi, cams = g[0], g[1]
                F['geometry'] = {'bgiSha256': g[2], 'bgsSha256': g[3], 'cameras': len(cams), 'triangles': len(bgi['tris'])}
        handled = set()
        for e in s.entries:
            kinds = created.get(e.sid, set())
            is_region = e.type == 1 or 'region' in kinds
            is_actor = e.type == 2 or 'actor' in kinds
            if is_region:
                polys, dynamic = polygon(e)
                if fid in (1608, 1707):  # EventEngine.DoEventCode QUAD runtime hotfix
                    polys = [[(x, -157 if z == -257 else z) for x, z in pl] for pl in polys]
                touch = e.funcs.get(2, (0, 0, []))[2]
                talk = e.funcs.get(3, (0, 0, []))[2]
                any_region_jump = False
                for tag, code in ((2, touch), (3, talk)):
                    if not code:
                        continue
                    direct = jumpfuncs.get((e.sid, tag))
                    reach = callees(e.sid, tag) - {(e.sid, tag)}
                    indirect = sorted(k for k in reach if k in jumpfuncs)
                    if not direct and not indirect:
                        continue
                    any_region_jump = True
                    if direct:
                        handled.add((e.sid, tag))
                    for k in indirect:
                        handled.add(k)
                    walk, anims, free = door_paths(code, player_funcs) if direct else (False, set(), False)
                    rec = {'field': fid, 'entry': e.sid, 'tag': tag, 'trigger': 'touch' if tag == 2 else 'confirm',
                           'entrySha256': e.sha, 'created': sorted(kinds) or ['not-created'], 'directJump': bool(direct),
                           'jumps': direct or [], 'indirectVia': ['entry %d tag %d' % k for k in indirect],
                           'indirectJumps': [dict(j, owner='entry %d tag %d' % k) for k in indirect for j in jumpfuncs[k]],
                           'features': features(code), 'walkOut': walk, 'plainWalkOut': walk and not any(i.op in BGA for i in code),
                           'animations': sorted(anims), 'jumpReachableWithoutBarrier': free, 'polygon': polys[0] if polys else None,
                           'dynamicPolygon': dynamic or not polys}
                    # runtime gate replica
                    if tag == 2 and direct:
                        if walk and not rec['animations'] and rec['plainWalkOut']:
                            rec['runtimeClass'] = 'G1-G3 pass: plain walk-out (needs on-screen outer edge)'
                        elif walk:
                            rec['runtimeClass'] = 'G1-G2 pass: animated door (needs live door tile)'
                        elif free:
                            rec['runtimeClass'] = 'EXCLUDED: touch jump without MJPOS/MOVQ walk (unless reviewed)'
                        else:
                            rec['runtimeClass'] = 'EXCLUDED: jump only after dialogue/choice/input'
                    elif tag == 2:
                        rec['runtimeClass'] = 'IGNORED: jump only in requested function (ContainsJump false)'
                    else:
                        rec['runtimeClass'] = 'confirm region: object only while native icon is shown'
                    if polys and bgi is not None and tag == 2:
                        rec['geometry'] = geometry_gates(polys[0], bgi, cams)
                        g = rec['geometry']
                        best_exit = max([c[l]['inViewAtExit'] for c in g['cameras'] for l in ('native4x3', 'wide16x9')] or [0])
                        best_any = max([c[l]['inViewBestCamera'] for c in g['cameras'] for l in ('native4x3', 'wide16x9')] or [0])
                        miss = min([c[l]['minOffscreenPx'] for c in g['cameras'] for l in ('native4x3', 'wide16x9') if c[l]['minOffscreenPx'] is not None] or [None]) \
                            if g['outerSamples'] else None
                        if not g['outerSamples']:
                            rec['noEdgeReason'] = no_edge_reason(polys[0], bgi)
                        rec['geometryClass'] = ('no permitted outer edge inside polygon' if not g['outerSamples'] else
                                                'outer edge on screen at exit' if best_exit else
                                                'outer edge on screen only from another camera position' if best_any else
                                                'outer edge always off-screen (min %.1f px beyond viewport)' % miss)
                    F['exits'].append(rec)
                if not any_region_jump:
                    F['regionsWithoutJump'] += 1
            if is_actor:
                model = None
                if 0 in e.funcs:
                    for i in e.funcs[0][2]:
                        if i.op == MODEL and i.c(0) is not None:
                            model = i.c(0)
                            break
                gname = geo.get(model)
                tk = talkable(e)
                push = 2 in e.funcs
                cat = ('Objects' if gname and gname.startswith('GEO_ACC_') else
                       'People' if gname and gname.startswith(('GEO_NPC_', 'GEO_MAIN_', 'GEO_SUB_')) else
                       'People' if 3 in e.funcs else 'Objects')
                trans = []
                for tag in (2, 3):
                    if tag in e.funcs:
                        reach = callees(e.sid, tag)
                        for k in sorted(reach):
                            if k in jumpfuncs:
                                trans.append('tag %d -> entry %d tag %d' % (tag, k[0], k[1]))
                                handled.add(k)
                F['actors'].append({'entry': e.sid, 'sha256': e.sha, 'model': model, 'geo': gname, 'created': sorted(kinds) or ['not-created'],
                                    'talkHandler': 3 in e.funcs, 'talkable': tk, 'pushHandler': push, 'runtimeCategory': cat,
                                    'transitionsFromInteraction': trans, 'isPlayerEntry': e.sid in players})
        for (sid, tag), j in sorted(jumpfuncs.items()):
            if (sid, tag) in handled:
                continue
            e = s.entries[sid]
            code = e.funcs[tag][2]
            F['otherTransitions'].append({'entry': sid, 'tag': tag, 'ownerType': {0: 'code', 1: 'region', 2: 'actor'}.get(e.type, str(e.type)),
                                          'isPlayerEntry': sid in players, 'jumps': j, 'features': features(code)})
        report['fields'].append(F)
        for rec in F['exits']:
            g = rec.get('geometry') or {}
            rows.append([fid, name, scene, rec['entry'], rec['tag'], rec['trigger'], rec['entrySha256'], rec['directJump'],
                         ';'.join(rec['indirectVia']), ';'.join('%s(%s)' % (j['op'], j['destination']) for j in rec['jumps'] + rec['indirectJumps']),
                         rec['walkOut'], rec['plainWalkOut'], ' '.join(map(str, rec['animations'])), rec['jumpReachableWithoutBarrier'],
                         ' '.join(rec['features']), rec['runtimeClass'], g.get('outerSamples', ''), rec.get('geometryClass', '')])
    (HERE / 'census.json').write_text(json.dumps(report, indent=1), encoding='utf-8')
    with open(HERE / 'region-transitions.csv', 'w', newline='', encoding='utf-8') as f:
        w = csv.writer(f)
        w.writerow(['field', 'event', 'scene', 'entry', 'tag', 'trigger', 'entrySha256', 'directJump', 'indirectVia', 'jumps', 'walkOut',
                    'plainWalkOut', 'animations', 'jumpWithoutBarrier', 'features', 'runtimeClass', 'outerSamples', 'geometryClass'])
        w.writerows(rows)
    assert {f['event'] for f in report['fields']} == set(scripts), 'Some installed fields were not audited'
    print('installed English field assets', len(scripts), 'mapped fields', len(report['fields']),
          'unmapped installed assets', len(unmapped_assets), 'region transition rows', len(rows))


if __name__ == '__main__':
    main()
