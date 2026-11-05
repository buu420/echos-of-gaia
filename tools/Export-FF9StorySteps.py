"""Compile current native Story steps and typed transit from the decoded installed event inventory.

Read-only. The input is the same decoded inventory as Export-FF9StoryCatalog.py (field event
scripts decoded with Memoria's own opcode tables); no game file is modified and no walkthrough
text supplies a position, an outcome or a trigger.

A step is a native player action that the field's own scripts wait for:
  * direct: a Confirm/touch handler (tag 3/2) of an actor or region, or a function it requests,
    writes the scenario counter or the field's phase variable;
  * flag:   an automatic (loop/init) function waits on a variable that only a player handler sets;
  * position: an automatic function waits on the controlled player's own x/y/z.
Every step carries the native guard expressions (raw expression bytes, per owning entry) that the
runtime re-evaluates on the live state, and a completion expression where one exists. Nothing is
emitted for waits whose variables have no player-rooted setter; those are recorded as unresolved.

Transit edges are typed by the reverse request provenance of their MAPJUMP site:
  player   - reached from a touch/Confirm handler (doors, walk-offs, confirm transitions);
  position - an automatic function's MAPJUMP behind a player position/flag wait;
  scripted - only automatic code reaches it (scenes). A scripted edge keeps its proven counter band.
"""
import argparse, json, hashlib, re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
p = argparse.ArgumentParser()
p.add_argument('--inventory', required=True)
p.add_argument('--catalog', default=str(ROOT / 'docs/accessibility/story-navigation.json'))
p.add_argument('--review', default=str(ROOT / 'docs/accessibility/story-main-steps.json'))
p.add_argument('--output', default=str(ROOT / 'docs/accessibility/story-steps.json'))
p.add_argument('--ledger', default=str(ROOT / 'docs/accessibility/story-step-coverage.json'))
a = p.parse_args()

inventory_bytes = Path(a.inventory).read_bytes()
fields = json.loads(inventory_bytes)
catalog = json.loads(Path(a.catalog).read_text(encoding='utf-8'))
review = json.loads(Path(a.review).read_text(encoding='utf-8'))
if hashlib.sha256(inventory_bytes).hexdigest() != catalog['inventorySHA256']:
    raise SystemExit('Inventory does not match the Story catalog inventory')

# EBin.op_binary values (Assembly-CSharp/Global/EBin.cs).
B = {'NOT': 14, 'LT': 24, 'GT': 25, 'LE': 26, 'GE': 27, 'LT_E': 28, 'GT_E': 29, 'LE_E': 30, 'GE_E': 31,
     'EQ': 32, 'NE': 33, 'EQ_E': 34, 'NE_E': 35, 'ANDAND': 39, 'OROR': 40, 'LET': 44}
COMPARE = {24: '<', 25: '>', 26: '<=', 27: '>=', 28: '<', 29: '>', 30: '<=', 31: '>=', 32: '==', 33: '!=', 34: '==', 35: '!='}
NEGATE = {'<': '>=', '>': '<=', '<=': '>', '>=': '<', '==': '!=', '!=': '=='}
REQUESTS = ('REQ', 'REQSW', 'REQEW')
JUMPS = ('MAPJUMP',)

def gameplay(fid): return fields[str(fid)]['event'].split('_')[1] != 'ENDING'

def expr_bytes(ops):
    out = bytearray()
    for op, arg in ops:
        out.append(op)
        if arg: out.extend(bytes.fromhex(arg))
    return bytes(out)

def parse(ops):
    """Small expression tree: ('var',scope,type,index) ('const',v) ('obj',uid,prop) ('sys',c)
    ('un',op,x) ('bin',op,l,r) ('other',op). None when the shape is not understood."""
    stack = []
    try:
        for op, arg in ops:
            b = bytes.fromhex(arg) if arg else b''
            if op == 0x7F: break
            if op >= 0xC0: stack.append(('var', op & 3, (op & 0x1C) >> 2, int.from_bytes(b, 'little')))
            elif op in (0x7D, 0x7E): stack.append(('const', int.from_bytes(b, 'little', signed=True)))
            elif op == 0x78: stack.append(('obj', b[0], b[1]))
            elif op == 0x7A: stack.append(('sys', b[0]))
            elif op == 0x5F: stack.append(('ptr', b[0]))
            elif op in (0x60, 0x61): stack.append(('dist' if op == 0x61 else 'angle', stack.pop()))  # B_ANGLEA/B_DISTANCEA from the executing object
            elif op in (12, 13, 14, 15, 16): stack.append(('un', op, stack.pop()))
            elif 17 <= op <= 40 or op == 44:
                r = stack.pop(); l = stack.pop(); stack.append(('bin', op, l, r))
            else: return None
        return stack[0] if len(stack) == 1 else None
    except (IndexError, ValueError):
        return None

def atoms(tree, out):
    """Flatten an ANDAND/OROR tree of comparisons: returns 'and'/'or'/'atom'."""
    if tree is None: return None
    if tree[0]=='un' and tree[1]==14:
        inner=[];kind=atoms(tree[2],inner)
        if kind is None:return None
        out.extend((x,NEGATE[c],v) for x,c,v in inner)
        return 'or' if kind=='and' else 'and' if kind=='or' else kind
    if tree[0] == 'bin' and tree[1] in (39, 40):
        kind = 'and' if tree[1] == 39 else 'or'
        k1 = atoms(tree[2], out); k2 = atoms(tree[3], out)
        if k1 is None or k2 is None: return None
        if (k1 not in ('atom', kind)) or (k2 not in ('atom', kind)): return None
        return kind
    if tree[0] == 'bin' and tree[1] in COMPARE:
        l, r = tree[2], tree[3]
        if r[0] == 'const': out.append((l, COMPARE[tree[1]], r[1])); return 'atom'
        if l[0] == 'const': out.append((r, {'<': '>', '>': '<', '<=': '>=', '>=': '<=', '==': '==', '!=': '!='}[COMPARE[tree[1]]], l[1])); return 'atom'
        return None
    if tree[0] == 'var': out.append((tree, '!=', 0)); return 'atom'
    if tree[0] == 'un' and tree[1] in (14, 15) and tree[2][0] == 'var': out.append((tree[2], '==', 0)); return 'atom'
    return None

def negate_bytes(raw):
    """Logical NOT of an expression (raw ends with 0x7F)."""
    return raw[:-1] + bytes([B['NOT'], 0x7F])

def equals_bytes(raw, value):
    return raw[:-1] + bytes([0x7D]) + (value & 0xFFFF).to_bytes(2, 'little') + bytes([B['EQ'], 0x7F])

def s16(v): return v - 65536 if v & 0x8000 else v

def var_bytes(var):
    _, scope, typ, index = var
    if index < 256: return bytes([0xC0 | (typ << 2) | scope, index])
    return bytes([0xE0 | (typ << 2) | scope]) + index.to_bytes(2, 'little')

def refs(tree, out):
    if tree is None: return
    if tree[0] == 'var': out.add(tree[1:])
    elif tree[0] == 'obj': out.add(('obj',))
    elif tree[0] == 'sys': out.add(('sys', tree[1]))
    elif tree[0] == 'un': refs(tree[2], out)
    elif tree[0] == 'bin': refs(tree[2], out); refs(tree[3], out)

def local_guard(raw):
    used = set(); tree = parse_raw(raw)
    if tree is None: return True
    refs(tree, used)
    return any(u != (0, 7, 0) and u != ('sys', 2) for u in used)

def dependency_scope(guards):
    used=set()
    for raw in guards:
        tree=parse_raw(raw)
        if tree is None:return 'room'
        refs(tree,used)
        if any(op==44 or 4<=op<=7 for op,arg in parse_ops(raw)):return 'room'
    if any(u!=('sys',2) and (len(u)!=3 or u[0]!=0) for u in used):return 'room'
    # General_FieldEntrance and the carriage's stored return room change as
    # part of source-room transit; they are not persistent quest flags.
    if any(len(u)==3 and u[0]==0 and u[2] in (2,239) for u in used):return 'room'
    return 'global' if any(u not in ((0,7,0),('sys',2)) for u in used) else 'counter'

from story_step_analysis import Function
from story_native_graph import NativeEventGraph

class OwnedGuard:
    def __init__(self,sid,expression,uid=-1):self.sid,self.expression,self.uid=sid,expression,uid
    def __iter__(self):return iter((self.sid,self.expression))


def counter_band(guards):
    """[floor, ceiling) of G7[0] proved by guards built only from G7[0] comparisons (ANDed)."""
    lo, hi = None, None
    for raw in guards:
        ops = []; i = 0
        tree = parse_raw(raw)
        if tree is None: continue
        flat = []; kind = atoms(tree, flat)
        if kind not in ('atom', 'and'): continue
        if not all(x[0] == ('var', 0, 7, 0) for x in flat): continue
        for _, cmp, v in flat:
            if cmp == '==': lo = max(lo, v) if lo is not None else v; hi = min(hi, v + 1) if hi is not None else v + 1
            elif cmp == '>=': lo = max(lo, v) if lo is not None else v
            elif cmp == '>': lo = max(lo, v + 1) if lo is not None else v + 1
            elif cmp == '<': hi = min(hi, v) if hi is not None else v
            elif cmp == '<=': hi = min(hi, v + 1) if hi is not None else v + 1
    return lo, hi

def parse_raw(raw):
    return parse(parse_ops(raw))

def parse_ops(raw):
    ops = []; i = 0
    while i < len(raw):
        op = raw[i]; i += 1; k = 0
        if op >= 0xC0: k = 2 if op >= 0xE0 else 1
        elif op == 0x7D: k = 2
        elif op == 0x7E: k = 4
        elif op in (0x78,): k = 2
        elif op in (0x7A, 0x79, 0x29, 0x5F): k = 1
        ops.append((op, raw[i:i + k].hex())); i += k
        if op == 0x7F: break
    return ops

def is_counter_write(o):
    if o['op'] != 'EXPR': return None
    e = o['args'][0]['expr']
    if len(e) == 4 and e[0][0] == 0xDC and e[0][1] == '00' and e[1][0] == 0x7D and e[2][0] == B['LET']:
        return int.from_bytes(bytes.fromhex(e[1][1]), 'little', signed=True)
    return None

def assignment(o):
    """('var',...) := const for a plain constant LET."""
    if o['op'] != 'EXPR': return None
    e = o['args'][0]['expr']
    if len(e) == 4 and e[0][0] >= 0xC0 and e[1][0] == 0x7D and e[2][0] == B['LET']:
        op, arg = e[0]; return (('var', op & 3, (op & 0x1C) >> 2, int.from_bytes(bytes.fromhex(arg), 'little')), int.from_bytes(bytes.fromhex(e[1][1]), 'little', signed=True))
    return None

stages = sorted(s['milestone'] for s in catalog['stages'])
steps, transit, unresolved = [], [], []
stats = {'fields': 0, 'counterWrites': 0}

for fid_s, f in sorted(fields.items(), key=lambda x: int(x[0])):
    fid = int(fid_s)
    if not gameplay(fid): continue
    stats['fields'] += 1
    E = f['entries']
    native=NativeEventGraph(E)
    def graph_node(key):return key[0],key[2],key[1]
    def function_key(node):return node[0],node[2],node[1]
    funcs={function_key(node):Function(fid,node[0],node[2],ops,node[1]) for node,ops in native.functions.items()}
    hashes={int(sid):entry['sha256'] for sid,entry in E.items()}
    types={int(sid):entry['type'] for sid,entry in E.items()}
    def owned(key,expression):return OwnedGuard(key[0],expression,key[2])
    def guard_row(guard):
        sid,expression=guard;row={'sid':sid,'hash':hashes[sid],'expr':expression.hex()}
        if guard.uid>=0:row['uid']=guard.uid
        return row
    def roots(key):
        result=[]
        for path in native.player_paths(graph_node(key)):
            guards=[]
            for edge in path.edges:
                caller=function_key(edge.source)
                guards.extend(owned(caller,g) for g in funcs[caller].guards(edge.offset))
            result.append((function_key(path.root),guards))
        return result
    # Constant assignments and their sites.
    setters = {}
    for key, fn in funcs.items():
        for o in fn.ops:
            asg = assignment(o)
            if asg: setters.setdefault(asg[0], []).append((key, o['off'], asg[1]))
    # Phase graph: phase var -> value -> counter writes / next values (from switch cases).
    def case_value(fn, site, var):
        for raw in fn.guards(site):
            t = parse_raw(raw); flat = []
            if t is not None and atoms(t, flat) == 'atom' and flat[0][0] == var and flat[0][1] == '==': return flat[0][2]
        return None
    writes_in = {}; edges_in = {}
    phase_vars = set()
    for key, fn in funcs.items():
        for i, o in enumerate(fn.ops):
            if o['op'] == 'rsv0b' and i and fn.ops[i - 1]['op'] == 'EXPR':
                t = parse(fn.ops[i - 1]['args'][0]['expr'])
                if t and t[0] == 'var' and t[1] == 1: phase_vars.add(t)
    for var in phase_vars:
        for key, fn in funcs.items():
            for o in fn.ops:
                v = is_counter_write(o)
                if v is not None:
                    c = case_value(fn, o['off'], var)
                    if c is not None: writes_in.setdefault((var, c), set()).add(v)
                asg = assignment(o)
                if asg and asg[0] == var:
                    c = case_value(fn, o['off'], var)
                    if c is not None and c != asg[1]: edges_in.setdefault((var, c), set()).add(asg[1])
    def phase_milestone(var, value):
        seen, frontier = {value}, [value]
        while frontier:
            found = sorted(v for x in frontier for v in writes_in.get((var, x), ()))
            if found: return found[0]
            nxt = []
            for x in frontier:
                for y in edges_in.get((var, x), ()):
                    if y not in seen: seen.add(y); nxt.append(y)
            frontier = nxt
        return -1
    def anchored(guards):
        # A phase or counter guard proves which scene state the step belongs to. Without one the
        # same handler/wait could be offered in an unrelated visit of this field.
        for s_, g in guards:
            used = set(); refs(parse_raw(g), used)
            if (0, 7, 0) in used or any(('var',) + u in phase_vars for u in used if len(u) == 3): return True
        return False
    def add_step(root, kind_source, guards, milestone, extra=None, completion=None):
        sid, tag, uid = root
        if kind_source != 'direct-counter' and not anchored(guards):
            unresolved.append({'field': fid, 'sid': sid, 'tag': tag, 'reason': kind_source + ' without a phase or counter guard'}); return
        kind = ('region-confirm' if tag == 3 else 'region-walk') if types.get(sid) == 1 else ('talk' if tag == 3 else 'touch')
        band = counter_band([g for s, g in guards])
        if band[0] is not None and band[1] is not None and band[0] >= band[1]: return
        if kind_source == 'direct-counter' and band[0] is not None and band[0] >= milestone: return
        row = {'field': fid, 'sid': sid, 'uid':uid if uid>=0 else None, 'hash': hashes[sid], 'kind': kind, 'source': kind_source,
               'guards': [guard_row(guard) for guard in guards],
               'milestone': milestone, 'floor': band[0], 'ceiling': band[1]}
        if completion: row['completion'] = [guard_row(guard) for guard in completion]
        if extra: row.update(extra)
        steps.append(row)

    def effect(key, offset):
        return dict({'sid':key[0],'tag':key[1],'offset':offset,'hash':hashes[key[0]]},**({'uid':key[2]} if key[2]>=0 else {}))

    def wait_details(fn, index):
        """Native exit polarity and actual post-WAIT instruction pointers."""
        if index+1>=len(fn.ops):return None
        o,n=fn.ops[index:index+2]
        if n['op']=='rsv03' and s16(n['args'][0])<0:
            start=n['next']+s16(n['args'][0]);end=n['next'];exit=end;negated=True
        elif n['op']=='rsv02' and n['args'][0]==3 and index+2<len(fn.ops) and fn.ops[index+2]['op']=='rsv01' and s16(fn.ops[index+2]['args'][0])>0:
            # A false condition enters the progress body; true goes to the
            # shared phase-loop tail. That tail can be after all other cases.
            jump=fn.ops[index+2];tail=jump['next']+s16(jump['args'][0]);exit=jump['next'];start=tail;end=fn.end;negated=True
        else:return None
        fn._flow();ips=set();pending=[start];seen=set()
        while pending and len(seen)<64:
            offset=pending.pop()
            if offset in seen or offset not in fn.index:continue
            seen.add(offset);row=fn.ops[fn.index[offset]]
            if row['op']=='WAIT':ips.add(row['next']);continue
            if row['op'] in ('rsv04','rsv02','rsv03','rsv0b','rsv06','rsv0d'):continue
            pending.extend(fn._edges.get(offset,()))
        ips=sorted(ips)
        if not ips:return None
        return {'exit':exit,'negated':negated,'binding':{'sid':fn.sid,'tag':fn.tag,'hash':hashes[fn.sid],'offset':o['off'],'ips':ips,**({'uid':fn.uid} if fn.uid>=0 else {})}}

    # Handshake variables: single-variable guards on the director's own phase assignments
    # (e.g. MAP1[231]==1 before MAP5[24]=next). Setting one is how actors end a phase.
    handshakes = set()
    transitions = {}  # (phase var, from value, handshake var, handshake value) -> next phase value
    for var in phase_vars:
        for key, fn in funcs.items():
            for o in fn.ops:
                asg = assignment(o)
                if not asg or asg[0] != var: continue
                for raw in fn.guards(o['off']):
                    t = parse_raw(raw); flat = []
                    if t is not None and atoms(t, flat) == 'atom' and flat[0][0][0] == 'var' and flat[0][0] != var and flat[0][0][1] == 1 and flat[0][1] == '==':
                        handshakes.add(flat[0][0])
                        before = case_value(fn, o['off'], var)
                        if before is not None: transitions[(var, before, flat[0][0], flat[0][2])] = asg[1]
    exits = {key for key, fn in funcs.items() if any(o['op'] in ('MAPJUMP', 'WMAPJUMP') for o in fn.ops)}
    def requests_story(key):
        pending=[key];seen=set()
        while pending and len(seen)<64:
            current=pending.pop()
            if current in seen or current not in funcs:continue
            seen.add(current)
            for row in funcs[current].ops:
                if is_counter_write(row) in stages:return True
                if row['op'] in REQUESTS and len(row['args'])>=3 and isinstance(row['args'][1],int) and isinstance(row['args'][2],int):
                    pending.extend(function_key(edge.target) for edge in native.calls(graph_node(current)) if edge.offset==row['off'])
        return False

    director_waits=set()
    for key,fn in funcs.items():
        if key[1] not in (0,1) or not requests_story(key):continue
        for index,row in enumerate(fn.ops):
            if row['op']!='EXPR' or wait_details(fn,index) is None:continue
            used=set();refs(parse(row['args'][0]['expr']),used)
            director_waits.update(('var',)+ref for ref in used if len(ref)==3 and ref[0]==1)

    def progresses(fn, start):
        """True when the code after a wait ends a control window or advances the scene."""
        fn._flow();pending=[start];seen=set()
        while pending and len(seen)<512:
            offset=pending.pop()
            if offset in seen or offset not in fn.index:continue
            seen.add(offset);i=fn.index[offset];o=fn.ops[i]
            # Moving an actor after the player's position wait is also a shown
            # scene response (the alley boy hops ahead). It does not establish
            # a later counter milestone; the actual wait IP remains mandatory.
            if o['op'] in ('UCOFF', 'MAPJUMP', 'WMAPJUMP','MOVE','MOVE3','MOVQ','POS','POS3') or o['op'] in REQUESTS or is_counter_write(o) is not None: return True
            asg = assignment(o)
            if asg and (asg[0] in phase_vars or asg[0] in handshakes or asg[0] in director_waits): return True
            if o['op']=='EXPR' and wait_details(fn,i) is not None:return True
            # Reused code blocks may be behind this instruction. Do not wander
            # back through the phase dispatcher into a different future case.
            if o['op'] in ('rsv0b','rsv06','rsv0d') and i and fn.ops[i-1]['op']=='EXPR' and parse(fn.ops[i-1]['args'][0]['expr']) in phase_vars:continue
            pending.extend(fn._edges.get(offset,()))
        return False

    def scene_guard(raw):
        used=set();refs(parse_raw(raw),used)
        return used and all(u==(0,7,0) or len(u)==3 and ('var',)+u in phase_vars for u in used)

    def wait_guards(fn,detail,guards):
        if not hasattr(fn,'_wait_groups'):
            groups={}
            for index,row in enumerate(fn.ops):
                if row['op']!='EXPR':continue
                other=wait_details(fn,index)
                if other:groups.setdefault(tuple(other['binding']['ips']),set()).add(row['off'])
            fn._wait_groups=groups
        shared=len(fn._wait_groups.get(tuple(detail['binding']['ips']),()))>1
        selected=guards if shared else [guard for guard in guards if scene_guard(guard.expression)]
        detail['binding']['guards']=[guard_row(guard) for guard in selected]
    pending_phase = []
    pending_handshake = []
    wait_phases = set()
    # 1. Direct writers and phase setters reached from player handlers.
    for key, fn in funcs.items():
        for o in fn.ops:
            v = is_counter_write(o)
            asg = assignment(o)
            if asg and asg[0] in handshakes and asg[0] not in phase_vars and v is None:
                for root, chain_guards in roots(key):
                    pending_handshake.append((root, chain_guards + [owned(key,g) for g in fn.guards(o['off'])], key, o['off'], asg))
                continue
            if v is None and not (asg and asg[0] in phase_vars): continue
            if v is not None: stats['counterWrites'] += 1
            chains = roots(key)
            for root, chain_guards in chains:
                own = [owned(key,g) for g in fn.guards(o['off'])]
                if v is not None:
                    add_step(root, 'direct-counter', chain_guards + own, v,extra={'effects':[effect(key,o['off'])]})
                else:
                    done = [owned(key,equals_bytes(var_bytes(asg[0]) + b'\x7f', asg[1]))]
                    pending_phase.append((root, chain_guards + own, asg, done, case_value(fn, o['off'], asg[0]),key,o['off']))
    # 2. Waits in automatic functions.
    for key, fn in funcs.items():
        if roots(key): continue
        for i, o in enumerate(fn.ops):
            if o['op'] != 'EXPR' or i + 1 >= len(fn.ops): continue
            detail=wait_details(fn,i)
            if detail is None or not progresses(fn,detail['exit']):continue
            cond = o['args'][0]['expr']; tree = parse(cond); flat = []
            kind = atoms(tree, flat)
            if kind is None or not flat: continue
            # Progress condition = NOT(cond): an OR of atoms becomes an AND of negated atoms.
            if (detail['negated'] and kind=='and' or not detail['negated'] and kind=='or') and len(flat)>1:continue
            progress=[(x,NEGATE[c] if detail['negated'] else c,v) for x,c,v in flat]
            context_guards = [owned(key,g) for g in fn.guards(o['off'])]
            wait_guards(fn,detail,context_guards)
            phase = None
            for var in phase_vars:
                c = case_value(fn, o['off'], var)
                if c is not None: phase = (var, c)
            milestone = phase_milestone(*phase) if phase else -1
            if phase: wait_phases.add(phase)
            # A later textual write may belong to another switch case entirely.
            position=[x for x in progress if x[0][0]=='obj' and x[0][2] in (0,1,2)]
            if position and len(position) == len(progress):
                axes = {x[0][2] for x in position}
                if len(axes) != 1: unresolved.append({'field': fid, 'sid': key[0], 'tag': key[1], 'offset': o['off'], 'reason': 'multi-axis position wait'}); continue
                prop = axes.pop(); lo = hi = None
                for _, c, v in position:
                    if prop == 1:  # OBJ.1 = -round(pos.y): convert to a bound on round(Y)
                        c = {'<': '>', '>': '<', '<=': '>=', '>=': '<=', '==': '==', '!=': '!='}[c]; v = -v
                    if c == '>=': lo = v if lo is None else max(lo, v)
                    elif c == '>': lo = v + 1 if lo is None else max(lo, v + 1)
                    elif c == '<=': hi = v if hi is None else min(hi, v)
                    elif c == '<': hi = v - 1 if hi is None else min(hi, v - 1)
                    else: lo = None; hi = None; break
                if lo is None and hi is None: unresolved.append({'field': fid, 'sid': key[0], 'tag': key[1], 'offset': o['off'], 'reason': 'position equality wait'}); continue
                operands={x[0][1] for x in position};literals=operands-{250,255}
                if len(literals)>1 or 255 in operands and types.get(key[0])!=2:
                    unresolved.append({'field':fid,'sid':key[0],'tag':key[1],'offset':o['off'],'reason':'position wait watches multiple or non-actor instances'});continue
                player=key[0] if 255 in operands else None
                player_uid=next(iter(literals)) if literals else key[2] if 255 in operands and key[2]>=0 else None
                if not anchored(context_guards):
                    unresolved.append({'field': fid, 'sid': key[0], 'tag': key[1], 'offset': o['off'], 'reason': 'position wait without a phase or counter guard'}); continue
                band = counter_band([g for s, g in context_guards])
                steps.append({'field': fid, 'sid': key[0], 'uid':key[2] if key[2]>=0 else None, 'hash': hashes[key[0]], 'kind': 'position', 'source': 'position-wait',
                              'guards':[guard_row(guard) for guard in context_guards if scene_guard(guard.expression)],
                              'milestone': milestone, 'floor': band[0], 'ceiling': band[1],
                              'axis': 'XYZ'[prop], 'minimum': lo, 'maximum': hi, 'playerSid': player,'playerUid':player_uid,'waiting':detail['binding'],'offset':o['off']})
                continue
            if any(x[0][0] == 'obj' for x in progress): continue  # other actors' positions: automatic choreography
            variables = [x for x in progress if x[0][0] == 'var' and x[0][1] == 1]
            if len(variables) != len(progress) or len(variables) != 1:
                continue
            var, c, v = variables[0]
            cands = [(sk, so, sv) for sk, so, sv in setters.get(var, ()) if (c == '==' and sv == v) or (c == '!=' and sv != v) or
                     (c == '>=' and sv >= v) or (c == '>' and sv > v) or (c == '<=' and sv <= v) or (c == '<' and sv < v)]
            rooted = [(sk, so, r, rg) for sk, so, sv in cands for r, rg in roots(sk) if r not in exits and sk not in exits]
            if len({r for sk, so, r, rg in rooted}) > 3: rooted = []  # a shared bookkeeping flag, not one action
            if not rooted:
                if var[1] == 1 and not cands and not any(r for sk, so, sv in setters.get(var, ()) for r in roots(sk)):
                    unresolved.append({'field': fid, 'sid': key[0], 'tag': key[1], 'offset': o['off'], 'reason': 'wait on a variable with no constant setter in this field'})
                continue
            done=[owned(key,negate_bytes(expr_bytes(cond)) if detail['negated'] else expr_bytes(cond))]
            for sk, so, root, rg in rooted:
                add_step(root,'flag-wait',rg+[owned(sk,g) for g in funcs[sk].guards(so)]+context_guards,milestone,extra={'effects':[effect(sk,so)],'waiting':detail['binding']},completion=done)
    # Phase setters: a player handler that advances the phase to a state which writes the counter
    # or which itself waits for a player action.
    def returns_to(var, start, target):
        seen, frontier = {start}, [start]
        while frontier:
            nxt = []
            for x in frontier:
                for y in edges_in.get((var, x), ()):
                    if y == target: return True
                    if y not in seen: seen.add(y); nxt.append(y)
            frontier = nxt
        return False
    for root, guards, asg, done, before, source_key, source_offset in pending_phase:
        m = phase_milestone(asg[0], asg[1])
        # A detour that only plays a scene and returns to the setter's own phase is not progress
        # (e.g. the castle hall's "Look out!" doorways, 153 regions 26/27: phase 6 -> 7..16 -> 6).
        if m < 0 and before is not None and returns_to(asg[0], asg[1], before): continue
        if m>=0 or (asg[0],asg[1]) in wait_phases:add_step(root,'direct-phase',guards,m,extra={'effects':[effect(source_key,source_offset)]},completion=done)
    # Handshakes: a player handler that ends the current phase (e.g. MAP1[231]=1 inside phase Q's
    # Confirm handler), letting the director advance to a phase that writes the counter or waits.
    for root, guards, key, off, asg in pending_handshake:
        for var in phase_vars:
            before = case_value(funcs[key], off, var)
            nxt = transitions.get((var, before, asg[0], asg[1])) if before is not None else None
            if nxt is None: continue
            m = phase_milestone(var, nxt)
            if m < 0 and returns_to(var, nxt, before): continue
            if m >= 0 or (var, nxt) in wait_phases:
                add_step(root, 'direct-handshake', guards, m,extra={'effects':[effect(key,off)]},completion=[owned(key,equals_bytes(var_bytes(var) + b'', nxt))])
    unresolved.extend(dict(row,field=fid) for row in native.unresolved)
    # 3. Typed transit.
    def position_gate_before(key, fn, site):
        """An automatic function that waits on the controlled player's own x/y/z before this jump:
        a walk-off implemented by polling (the player's walking is the trigger)."""
        for i, o in enumerate(fn.ops):
            if o['off'] >= site: break
            if o['op'] != 'EXPR' or i + 1 >= len(fn.ops): continue
            n = fn.ops[i + 1]
            looped = (n['op'] == 'rsv03' and s16(n['args'][0]) < 0) or (n['op'] == 'rsv02' and n['args'][0] == 3 and i + 2 < len(fn.ops) and fn.ops[i + 2]['op'] == 'rsv01')
            branch = n['op'] in ('rsv02', 'rsv03')
            if not (looped or branch): continue
            used = set(); refs(parse(o['args'][0]['expr']), used)
            flat = []; tree = parse(o['args'][0]['expr'])
            if tree is None or atoms(tree, flat) is None: continue
            def player_related(x):
                if x[0] == 'obj': return x[2] in (0,1,2) and x[1]==250
                if x[0] == 'dist': return x[1][0]=='ptr' and x[1][1]==250
                return False
            if not any(player_related(x[0]) for x in flat): continue
            control=[]
            for raw in fn.guards(o['off']):
                tests=[]
                if atoms(parse_raw(raw),tests) is not None:control.extend(tests)
            if not any(x==('sys',2) and ((c in ('==','>=') and v==1) or (c in ('!=','>') and v==0)) for x,c,v in control):continue
            # The wait and the jump must belong to the same native phase case (or neither to one):
            # an earlier phase's wait does not make a later scene jump walkable.
            if all(case_value(fn, o['off'], var) == case_value(fn, site, var) for var in phase_vars): return True
        return False
    for key, fn in funcs.items():
        rs = roots(key)
        for o in fn.ops:
            if o['op'] not in JUMPS or not o['args'] or not isinstance(o['args'][0], int): continue
            dest = o['args'][0]
            if str(dest) not in fields or not gameplay(dest): continue
            own = fn.guards(o['off'])
            if rs:
                for root, rg in rs:
                    gs = [g for s, g in rg] + own
                    band = counter_band(gs)
                    local = any(local_guard(g) for g in gs);scope=dependency_scope(gs)
                    transit.append({'source': fid, 'destination': dest, 'kind': 'player', 'sid': root[0], 'tag': root[1], 'floor': band[0], 'ceiling': band[1], 'local':local,'scope':scope,'guards':[g.hex() for g in gs]})
            else:
                band = counter_band(own)
                gated = position_gate_before(key, fn, o['off'])
                transit.append({'source': fid, 'destination': dest, 'kind': 'position' if gated else 'scripted', 'sid': key[0], 'tag': key[1], 'floor': band[0], 'ceiling': band[1], 'local':any(local_guard(g) for g in own),'scope':'room' if gated else dependency_scope(own),'guards':[g.hex() for g in own]})

# Deduplicate.
def dedupe(rows, keys):
    seen, out = set(), []
    for r in rows:
        k = json.dumps({x: r.get(x) for x in keys}, sort_keys=True)
        if k not in seen: seen.add(k); out.append(r)
    return out
steps = dedupe(steps, ['field', 'sid', 'uid', 'kind', 'source', 'guards', 'milestone', 'completion', 'axis', 'minimum', 'maximum','playerSid','playerUid','effects','waiting'])
transit = dedupe(transit, ['source', 'destination', 'kind', 'floor', 'ceiling','scope','guards'])
# Absent bounds are omitted (the runtime JSON reader has no typed null).
steps = [{k: v for k, v in r.items() if v is not None} for r in steps]
transit = [{k: v for k, v in r.items() if v is not None} for r in transit]
result = {'schema':2, 'source':'Installed native event inventory; necessary CFG guards plus executed trigger effects and exact active wait sites',
          'inventorySHA256': catalog['inventorySHA256'], 'steps': steps, 'transit': transit}
Path(a.output).write_text(json.dumps(result, separators=(',', ':')), encoding='utf-8')

# Coverage ledger: chapter-level native action bindings, not guide rows.
chapters = review['chapters']
by_milestone = {}
for s in steps:
    if s['milestone'] is not None and s['milestone'] >= 0: by_milestone.setdefault(s['milestone'], []).append(s)
writers = {}
for fid_s, f in fields.items():
    if not gameplay(int(fid_s)): continue
    for sid, e in f['entries'].items():
        for tag, ops in e['functions'].items():
            for o in ops:
                v = is_counter_write(o)
                if v is not None: writers.setdefault(v, set()).add((int(fid_s), int(sid), int(tag)))
ledger = []
for c in chapters:
    lo, hi, name = c[0], c[1], c[2]
    rows = [m for m in stages if lo < m <= hi]
    bound = [m for m in rows if any(x['kind'] != 'position' for x in by_milestone.get(m, ()))]
    position = [m for m in rows if any(x['kind'] == 'position' for x in by_milestone.get(m, ()))]
    automatic = [m for m in rows if m not in bound and m not in position and writers.get(m)]
    missing = [m for m in rows if not writers.get(m)]
    ledger.append({'chapter': name, 'counter': [lo, hi], 'stageMilestones': len(rows), 'playerActionBound': len(bound),
                   'positionWaitBound': len(position), 'arrivalOrAutomaticOnly': automatic, 'noNativeWriter': missing})
summary = {'fields': stats['fields'], 'steps': len(steps), 'stepsByKind': {}, 'stepsBySource': {}, 'transit': len(transit), 'transitByKind': {}, 'unresolved': len(unresolved)}
for s in steps:
    summary['stepsByKind'][s['kind']] = summary['stepsByKind'].get(s['kind'], 0) + 1
    summary['stepsBySource'][s['source']] = summary['stepsBySource'].get(s['source'], 0) + 1
for t in transit: summary['transitByKind'][t['kind']] = summary['transitByKind'].get(t['kind'], 0) + 1
Path(a.ledger).write_text(json.dumps({'schema': 1, 'proofBoundary': 'Static native bindings only. Runtime guards, presentation and live play decide whether a step is offered; this is not a live playthrough or complete semantic coverage.',
    'summary': summary, 'chapters': ledger, 'unresolved': unresolved}, indent=1), encoding='utf-8')
print('PASS native story steps:', json.dumps(summary))
