"""Validate reviewed Story actions against decoded, hash-identified native sites.

This checks provenance and structural binding, not gameplay success. Current guards,
trigger selection and geometry still have to pass the native readers at runtime.
"""
from story_native_graph import NativeEventGraph
from story_step_analysis import Function


def _expression_ops(expression):
    for item in expression or []:
        if isinstance(item, dict):
            for op, _ in item.get('expr', []):
                yield op


def _global_guard(text,allow_map=False):
    """Only constant and persistent-global reads in cross-room predicates."""
    try:
        data = bytes.fromhex(text)
    except (ValueError, TypeError):
        return False
    p, depth = 0, 0
    while p < len(data):
        op = data[p]
        p += 1
        if op == 0x7f:
            return p == len(data) and depth == 1
        if op >= 0xc0:
            if (op & 3) not in ((0,1) if allow_map else (0,)):
                return False
            p += 2 if op >= 0xe0 else 1
            depth += 1
        elif op in (0x7d, 0x7e):
            p += 2 if op == 0x7d else 4
            depth += 1
        elif op in (12, 13, 14, 15, 16, 70, 71, 72, 73):
            if depth < 1:
                return False
        elif 17 <= op <= 40:
            if depth < 2:
                return False
            depth -= 1
        else:
            return False
    return False


def validate(chains, fields, inventory_sha256, loop_prompts=None):
    errors = []
    if chains.get('schema') != 1:
        errors.append('Unsupported reviewed action schema')
    if chains.get('inventorySHA256') != inventory_sha256:
        errors.append('Reviewed action inventory identity mismatch')
    seen = set()
    graphs = {}
    def native_graph(field):
        if field not in graphs:
            graphs[field] = NativeEventGraph(fields[str(field)]['entries'])
        return graphs[field]
    for row in chains.get('steps', []):
        label = row.get('id', '<missing id>')

        def fail(reason):
            errors.append(label + ': ' + reason)

        if not row.get('id'):
            fail('missing stable action id')
        if label in seen:
            fail('duplicate action id')
        seen.add(label)
        field = fields.get(str(row.get('field')))
        if field is None:
            fail('unknown native field')
            continue
        entries = field.get('entries', {})
        graph = native_graph(row['field'])

        def owner(value, what):
            entry = entries.get(str(value.get('sid')))
            if entry is None:
                fail(what + ' owner does not exist')
                return None
            if value.get('hash') != entry.get('sha256'):
                fail(what + ' hash differs from native entry')
            sid, uid = value.get('sid'), value.get('uid')
            if not isinstance(uid, int) or uid not in graph.instances.get(sid, ()):
                fail(what + ' instance is not a native literal creation')
            return entry

        target = owner(row, 'target')
        floor, ceiling = row.get('floor'), row.get('ceiling')
        if not isinstance(floor, int) or not isinstance(ceiling, int) or not 0 <= floor < ceiling <= 12001:
            fail('invalid bounded counter window')
        if not isinstance(row.get('action'), str) or not row['action'].strip():
            fail('missing player action wording')
        if row.get('source') != 'reviewed-prefix':
            fail('unknown reviewed action proof mode')
        for what, values in [('guard', row.get('guards', [])), ('completion', row.get('completion', [])),
                             ('wait guard', row.get('waiting', {}).get('guards', []))]:
            for value in values:
                owner(value, what)
                text = value.get('expr', '')
                try:
                    raw = bytes.fromhex(text)
                    if not raw or raw[-1] != 0x7f:
                        raise ValueError()
                except (ValueError, TypeError):
                    fail(what + ' is not a complete native expression')
        player = row.get('playerSid')
        if player is not None and row.get('playerUid') not in graph.instances.get(player, ()):
            fail('controlled player instance is not a native creation')
        waiting = row.get('waiting')
        if waiting:
            waiter = owner(waiting, 'wait')
            if waiter:
                code = waiter.get('functions', {}).get(str(waiting.get('tag')), [])
                # WAIT stores the following IP; other native waits can retain their
                # own IP while retrying. Keep both only when a real yield owns it.
                resumes = {i['next'] for i in code if i['op'] == 'WAIT'}
                resumes.update(i['off'] for i in code if i['op'].startswith('WAIT') and i['op'] != 'WAIT')
                if not waiting.get('ips') or any(ip not in resumes for ip in waiting['ips']):
                    fail('wait IP is not a native yield resume address')
        kind = row.get('kind')
        if kind not in ('talk', 'touch', 'region-confirm', 'region-walk', 'position'):
            fail('unknown action kind')
        if kind == 'position':
            if row.get('axis') not in ('X', 'Y', 'Z') or row.get('minimum') is None and row.get('maximum') is None:
                fail('position action has no native position predicate')
            if not waiting:
                fail('position action has no current wait proof')
            if row.get('supportFloor') is not None:
                waiter=entries.get(str((waiting or {}).get('sid')), {})
                code=waiter.get('functions',{}).get(str((waiting or {}).get('tag')),[])
                predicate=next((i for i in code if i['off']==row.get('evidence',{}).get('nativeCondition')),None)
                expressions=list(_expression_ops(predicate['args'])) if predicate and predicate['op']=='EXPR' else []
                if not isinstance(row['supportFloor'],int) or row['supportFloor']<0 or 113 not in expressions:
                    fail('support floor is not bound to a native floor predicate')
            if row.get('minimumHeight') is not None:
                waiter=entries.get(str((waiting or {}).get('sid')), {})
                code=waiter.get('functions',{}).get(str((waiting or {}).get('tag')),[])
                predicate=next((i for i in code if i['off']==row.get('evidence',{}).get('nativeCondition') and i['op']=='EXPR'),None)
                expr=predicate['args'][0]['expr'] if predicate else []
                # The native wait repeats while Z is too small OR native Y
                # is below the upper path. Unity Y has the opposite sign.
                expected=[120,125,24,120,125,25,40,127]
                if (row.get('axis')!='Z' or type(row.get('minimum')) is not int or type(row['minimumHeight']) is not int or
                        [x[0] for x in expr]!=expected or expr[0][1]!='fa02' or expr[3][1]!='fa01' or
                        int.from_bytes(bytes.fromhex(expr[1][1]),'little',signed=True)!=row['minimum'] or
                        -int.from_bytes(bytes.fromhex(expr[4][1]),'little',signed=True)!=row['minimumHeight']):
                    fail('secondary height is not part of this native position wait')
            continue
        root_tag = 3 if kind in ('talk', 'region-confirm') else 2
        root = (row.get('sid'), row.get('uid'), root_tag)
        facing = row.get('approachFacing')
        if facing is not None:
            offset = row.get('evidence', {}).get('nativeFacing')
            predicate = next((i for i in graph.functions.get(root, []) if i['off'] == offset and i['op'] == 'EXPR'), None)
            expr = predicate['args'][0]['expr'] if predicate else []
            # A reviewed possible turn must be inside the native player's
            # strict greater-than / less-than facing arc in this same trigger.
            inside = (len(expr) == 8 and expr[0] == expr[3] == [120, 'fa03'] and
                      expr[1][0] == expr[4][0] == 125 and [expr[i][0] for i in (2,5,6,7)] == [25,24,39,127] and
                      int.from_bytes(bytes.fromhex(expr[1][1]),'little',signed=True) < facing <
                      int.from_bytes(bytes.fromhex(expr[4][1]),'little',signed=True)) if type(facing) is int else False
            if kind != 'region-confirm' or not inside or not 0 <= facing <= 255:
                fail('reviewed approach facing is outside the native Confirm arc')
        reachable, pending = set(), [root]
        while pending:
            node = pending.pop()
            if node in reachable:
                continue
            reachable.add(node)
            pending.extend(edge.target for edge in graph.calls(node))
        effects = row.get('effects', [])
        if not effects:
            fail('interaction has no current effect/prefix evidence')
        for effect in effects:
            entry = owner(effect, 'effect')
            if entry is None:
                continue
            node = (effect.get('sid'), effect.get('uid'), effect.get('tag'))
            if node not in reachable:
                fail('effect cannot be reached by this native trigger/request chain')
            code = entry.get('functions', {}).get(str(effect.get('tag')), [])
            site = next((i for i in code if i['off'] == effect.get('offset')), None)
            if site is None:
                fail('effect offset is not an instruction in its native tag')
            elif effect.get('proof', 'assigned') == 'assigned':
                if site['op'] != 'EXPR' or not any(op in (4, 5, 6, 7, 44) or 45 <= op <= 55 for op in _expression_ops(site['args'])):
                    fail('assignment proof identifies no native assignment')
            elif effect['proof'] != 'reached':
                fail('unknown effect proof mode')
            elif node!=root or site['off'] not in Function(row['field'],node[0],node[2],code,node[1]).prefix_sites():
                fail('reviewed prefix reaches its effect only after a native yield or in another invocation')
        if target is not None and not target.get('functions', {}).get(str(root_tag)):
            fail('action has no native player trigger')
    valid_loops = []
    loop_bindings = {(r['field'],r['uid']):r for r in (loop_prompts or {}).get('objects',[]) if not r.get('promptOnly')}
    for row in chains.get('loopActions', []):
        label = row.get('id', '<missing loop id>')
        field, sid, uid = (row.get(k) for k in ('field','sid','uid'))
        binding = loop_bindings.get((field,uid),{})
        lo, hi = row.get('floor'), row.get('ceiling')
        window=type(lo) is int and type(hi) is int and 0<=lo<hi<=12001
        if (not binding or (loop_prompts or {}).get('inventorySHA256')!=inventory_sha256 or not window or
                not binding['floor']<=lo<hi<=binding['ceiling'] or not row.get('action') or
                any(binding.get(k)!=row.get(k) for k in ('sid','uid','hash','entry','prompt','resume'))):
            errors.append(label+': no matching reviewed loop prompt in this counter window');continue
        entry = fields.get(str(field),{}).get('entries',{}).get(str(sid),{})
        code = native_graph(field).functions.get((sid,uid,1),[])
        if (not entry or entry.get('sha256')!=row.get('hash') or
                not any(i['off']==row['prompt'] and i['op']=='FICON' for i in code) or
                not any(i['next']==row['resume'] and i['op']=='WAIT' for i in code) or
                not any(i['off']==row['entry'] for i in code)):
            errors.append(label+': native loop entry, prompt, or wait changed');continue
        valid_loops.append(row)
    phase_ids = set()
    for phase in chains.get('phases', []):
        label = phase.get('id', '<missing phase id>')
        def fail(reason):
            errors.append(label + ': ' + reason)
        if not phase.get('id') or label in phase_ids:
            fail('missing or duplicate phase id')
        phase_ids.add(label)
        floor, ceiling = phase.get('floor'), phase.get('ceiling')
        if not isinstance(floor, int) or not isinstance(ceiling, int) or not 0 <= floor < ceiling <= 12001:
            fail('invalid phase counter window')
        rooms = {}
        for room in phase.get('rooms', []):
            field = room.get('field')
            if field in rooms:
                fail('duplicate room in phase')
            rooms[field] = room
            director = fields.get(str(field), {}).get('entries', {}).get('0', {})
            if not director or room.get('directorHash') != director.get('sha256'):
                fail('director hash differs from native room %s' % field)
        if phase.get('field') not in rooms:
            fail('phase goal is outside its verified rooms')
        if any(field not in rooms for field in phase.get('scope', [])):
            fail('phase scope contains an unverified room')
        local=phase.get('local')
        if local:
            if local.get('field') not in rooms or any(not _global_guard(raw,True) for raw in local.get('guards',[])):
                fail('local phase predicate does not belong to this native room')
            triangles=local.get('triangles',[])
            if triangles and (len(local.get('walkmesh',''))!=64 or any(not isinstance(t,int) or t<0 for t in triangles)):
                fail('local phase floor has no native walkmesh identity')
        if any(not isinstance(i.get('id'),int) or not 0<=i['id']<768 or type(i.get('owned')) is not bool
               for i in phase.get('items', [])):
            fail('phase item requirement is not a native inventory read')
        if any(not _global_guard(raw) for raw in phase.get('guards', [])):
            fail('phase guard is not a read-only persistent expression')
        for uid in phase.get('uids', []):
            if not any(step.get('field') == phase.get('field') and step.get('uid') == uid and
                       step.get('milestone') == phase.get('milestone') and step.get('floor', 0) <= floor and
                       step.get('ceiling', 12001) >= ceiling for step in list(chains.get('steps', []))+valid_loops):
                fail('phase action %s has no reviewed native binding for this window' % uid)
        for leg in phase.get('links', []):
            source, destination = leg.get('from'), leg.get('to')
            if source not in rooms or destination not in rooms:
                fail('reviewed route leaves its scoped rooms')
            native = fields.get(str(source), {}).get('entries', {}).get(str(leg.get('sid')), {})
            if not native or native.get('sha256') != leg.get('hash'):
                fail('reviewed route has a changed source entry')
                continue
            node = (leg.get('sid'), leg.get('uid'), leg.get('tag'))
            graph=native_graph(source)
            jump=leg.get('via')
            binding=leg
            jump_owner=node
            ok=leg.get('tag') in (2,3)
            if jump is not None:
                binding=jump
                jump_owner=(jump.get('sid'),jump.get('uid'),jump.get('tag'))
                native_helper=fields[str(source)]['entries'].get(str(jump.get('sid')), {})
                if not native_helper or native_helper.get('sha256')!=jump.get('hash'):
                    fail('reviewed route has a changed native helper')
                    continue
                seen=set()
                pending=[e.target for e in graph.calls(node) if e.offset==leg.get('offset')]
                while pending:
                    call=pending.pop()
                    if call in seen:continue
                    seen.add(call)
                    pending.extend(e.target for e in graph.calls(call))
                ok=ok and jump_owner in seen
            code=graph.functions.get(jump_owner, [])
            if not ok or not any(row['off'] == binding.get('offset') and row['op'] == 'MAPJUMP' and
                                 row['args'] and row['args'][0] == destination for row in code):
                fail(f'reviewed route {source}#{node[0]}.{node[2]}@{leg.get("offset")} to {destination} is not that native player-triggered map jump')
            if any(not _global_guard(raw) for raw in leg.get('guards', [])):
                fail('reviewed route guard is not a read-only persistent expression')
    return errors
