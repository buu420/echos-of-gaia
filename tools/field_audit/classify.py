"""Turn census.json into honest per-transition classifications and per-field quantities.

Classes (static evidence only; nothing here is live proof of visibility):
  static-eligible          region touch exit passes every gate the runtime can check offline (G1-G5); live
                           rendering/permission/approach still decide presentation.
  candidate-offscreen-edge plain walk-out (MJPOS+MOVQ / requested walk) whose permitted outer boundary is never
                           inside the viewport, even from the best camera position (screen-edge walk-off; Forest).
  candidate-interior       plain walk-out whose polygon has no permitted outer boundary edge (walkmesh continues).
  candidate-requested      touch region whose jump is only in a requested function (runtime ContainsJump false),
                           requested path barrier-free.
  candidate-nowalk         touch jump reachable without dialogue/choice/input but without MJPOS/MOVQ walk.
  animated-door            walk-out with BGA door animation (needs live door tile; static only partial).
  confirm-transition       Confirm (tag 3) region transition; object while icon shown; not a walking exit.
  dialogue-transition      jump only after dialogue/choice/key input (teleport/menu); not a walking exit.
  unsupported              dynamic polygon / missing geometry / blocked-attribute-only edges / unresolved.
Other jumps (director loops, actor/player functions) are counted as story/scripted transitions.
"""
import csv
import json
from collections import Counter, defaultdict
from pathlib import Path

from ebscript import Script, load_field_scripts
from census import door_paths, features

from audit_paths import OUTPUT_ROOT as HERE, SOURCE_ROOT, GAME_ROOT
R = json.loads((HERE / 'census.json').read_text(encoding='utf-8'))
EV = json.loads((SOURCE_ROOT / 'docs/accessibility/field-exit-evidence.json').read_text(encoding='utf-8'))
reviewed = {(int(r['sceneKey'].split(':')[1]), r['scriptId']) for r in EV['boundaryReviewed']}
scripts, _ = load_field_scripts()


def best(x, key):
    g = x.get('geometry') or {}
    return max([c[l][key] for c in g.get('cameras', []) for l in ('native4x3', 'wide16x9')] or [0])


def classify(f, x, s):
    gcls = x.get('geometryClass', '')
    rc = x['runtimeClass']
    if x['trigger'] == 'confirm':
        return 'confirm-transition'
    if rc.startswith('EXCLUDED: jump only after'):
        return 'dialogue-transition'
    if rc.startswith('IGNORED'):
        if 'dialogue/choice' in x['features'] or 'key-input' in x['features']:
            return 'dialogue-transition'
        # analyse requested functions: barrier-free walk-out?
        free = walk = False
        for owner in x['indirectVia']:
            sid, tag = int(owner.split()[1]), int(owner.split()[3])
            code = s.entries[sid].funcs[tag][2]
            w, _, fr = door_paths(code, None)
            free |= fr
            feats = features(code)
            walk |= ('MJPOS' in feats and 'MOVQ' in feats) or w
        x['requestedWalkOut'] = walk
        x['requestedBarrierFree'] = free
        if not free:
            return 'dialogue-transition'
        return 'candidate-requested'
    if x['dynamicPolygon'] or x.get('geometry') is None:
        return 'unsupported'
    if rc.startswith('EXCLUDED: touch jump without'):
        if (f['field'], x['entry']) in reviewed:
            return 'reviewed-nowalk' if best(x, 'inViewAtExit') else 'reviewed-nowalk-dead'
        return 'candidate-nowalk'
    if rc.startswith('G1-G2'):
        return 'animated-door'
    if gcls.startswith('outer edge on screen at exit'):
        return 'static-eligible'
    if gcls.startswith('outer edge on screen only from another'):
        return 'static-eligible-other-camera-position'
    if gcls.startswith('outer edge always off-screen'):
        return 'candidate-offscreen-edge'
    if gcls.startswith('no permitted outer edge'):
        if 'player-blocked' in x.get('noEdgeReason', ''):
            return 'unsupported'
        return 'candidate-interior'
    return 'unsupported'


fields_out = []
trows = []
frows = []
totals = Counter()
for f in R['fields']:
    s = Script(scripts[f['event']][1])
    cls = Counter()
    for x in f['exits']:
        c = classify(f, x, s)
        x['class'] = c
        cls[c] += 1
        totals[c] += 1
        g = x.get('geometry') or {}
        trows.append([f['field'], f['event'], f['scene'], x['entry'], x['tag'], x['trigger'], x['entrySha256'], c, x['runtimeClass'],
                      x.get('geometryClass', ''), x.get('noEdgeReason', ''), g.get('outerSamples', ''), best(x, 'inViewAtExit') if g else '',
                      ';'.join('%s(%s)' % (j['op'], j['destination']) for j in x['jumps'] + x['indirectJumps']), ';'.join(x['indirectVia']),
                      ' '.join(x['features'])])
    story = len(f['otherTransitions'])
    actors = [a for a in f['actors']]
    people = sum(1 for a in actors if a['runtimeCategory'] == 'People' and not a['isPlayerEntry'])
    objects = sum(1 for a in actors if a['runtimeCategory'] == 'Objects' and not a['isPlayerEntry'])
    touch = sum(v for k, v in cls.items() if k not in ('confirm-transition',))
    eligible = cls['static-eligible'] + cls['static-eligible-other-camera-position'] + cls['reviewed-nowalk'] + cls['animated-door']
    blocked = cls['candidate-offscreen-edge'] + cls['candidate-interior'] + cls['candidate-requested'] + cls['candidate-nowalk'] + cls['reviewed-nowalk-dead']
    flag = 'no-presentable-exit' if touch and not eligible and blocked else 'some-exits-blocked' if blocked else ''
    frows.append([f['field'], f['event'], f['scene'], touch, cls['static-eligible'] + cls['static-eligible-other-camera-position'], cls['animated-door'],
                  cls['reviewed-nowalk'], cls['candidate-offscreen-edge'], cls['candidate-interior'], cls['candidate-requested'], cls['candidate-nowalk'],
                  cls['reviewed-nowalk-dead'], cls['dialogue-transition'], cls['confirm-transition'], cls['unsupported'], story, people, objects,
                  f['regionsWithoutJump'], flag])
    f['classCounts'] = dict(cls)
    f['flag'] = flag
    fields_out.append(f)

(HERE / 'field-navigation-audit.json').write_text(json.dumps({
    'schema': 1, 'source': str(SOURCE_ROOT) + ' (read-only; baseline runtime policy comparison)' ,
    'game': str(GAME_ROOT) + ' (read-only)', 'p0data7Sha256': R['p0data7Sha256'],
    'classTotals': dict(totals), 'fields': fields_out,
    'note': 'Static census. Classes are evidence categories, not live visibility proof. Live state (branches, CFLAG, BGI toggles, scripted cameras) is not evaluated.'},
    indent=1), encoding='utf-8')
with open(HERE / 'field-transitions.csv', 'w', newline='', encoding='utf-8') as fh:
    w = csv.writer(fh)
    w.writerow(['field', 'event', 'scene', 'entry', 'tag', 'trigger', 'entrySha256', 'class', 'runtimeGate', 'geometryClass', 'noEdgeReason',
                'outerSamples', 'samplesOnScreenAtExit', 'jumps', 'requestedVia', 'features'])
    w.writerows(trows)
with open(HERE / 'field-summary.csv', 'w', newline='', encoding='utf-8') as fh:
    w = csv.writer(fh)
    w.writerow(['field', 'event', 'scene', 'touchTransitions', 'staticEligible', 'animatedDoor', 'reviewedNoWalk', 'candOffscreenEdge',
                'candInterior', 'candRequested', 'candNoWalk', 'reviewedDead', 'dialogueTransitions', 'confirmTransitions', 'unsupported',
                'storyOrScriptedJumps', 'actorsPeople', 'actorsObjects', 'regionsWithoutJump', 'flag'])
    w.writerows(frows)
print('class totals', dict(totals))
print('fields flagged', Counter(r[-1] for r in frows))
