"""Pure control-flow predicates for decoded native FFIX event functions."""
B={"NOT":14,"EQ":32,"OROR":40}

def expr_bytes(ops):
    out = bytearray()
    for op, arg in ops:
        out.append(op)
        if arg: out.extend(bytes.fromhex(arg))
    return bytes(out)
def negate_bytes(raw):
    """Logical NOT of an expression (raw ends with 0x7F)."""
    return raw[:-1] + bytes([B['NOT'], 0x7F])

def equals_bytes(raw, value):
    return raw[:-1] + bytes([0x7D]) + (value & 0xFFFF).to_bytes(2, 'little') + bytes([B['EQ'], 0x7F])

def s16(v): return v - 65536 if v & 0x8000 else v
class Function:
    def __init__(self, fid, sid, tag, ops, uid=-1):
        self.fid, self.sid, self.tag, self.ops, self.uid = fid, sid, tag, ops, uid
        self.index = {o['off']: i for i, o in enumerate(ops)}
        self.jumps = []  # (source offset, target offset)
        for i, o in enumerate(ops):
            n = o['op']
            if n == 'rsv01': self.jumps.append((o['off'], o['next'] + s16(o['args'][0])))
            elif n == 'rsv02': self.jumps.append((o['off'], o['next'] + o['args'][0]))
            elif n == 'rsv03': self.jumps.append((o['off'], o['next'] + s16(o['args'][0])))
            elif n == 'rsv0b' and len(o['args']) >= 2:
                for t in o['args'][1:]: self.jumps.append((o['off'], o['off'] + 1 + t))
            elif n == 'rsv06' and len(o['args']) >= 1:
                self.jumps.append((o['off'], o['off'] + 4 + o['args'][0]))
                for t in o['args'][2::2]: self.jumps.append((o['off'], o['off'] + 4 + t))
        self.end = ops[-1]['next'] if ops else 0
    def entered_from_outside(self, start, end, owner):
        # Any jump from outside [owner, end) landing strictly inside (start, end) or at start.
        for src, dst in self.jumps:
            if start <= dst < end and not (owner <= src < end): return True
        return False
    def _flow(self):
        if hasattr(self, '_idom'): return
        self._edges = {}
        self._switches = {}
        self._conditions = {}
        for i, o in enumerate(self.ops):
            op, off, nxt, args = o['op'], o['off'], o['next'], o['args']
            targets = [nxt]
            if op == 'rsv04': targets = []
            elif op == 'rsv01': targets = [nxt + s16(args[0])]
            elif op in ('rsv02', 'rsv03'):
                target = nxt + (args[0] if op == 'rsv02' else s16(args[0]))
                targets = [nxt, target]
                if i and self.ops[i-1]['op'] == 'EXPR':
                    raw = expr_bytes(self.ops[i-1]['args'][0]['expr'])
                    self._conditions[off] = (raw, nxt if op == 'rsv02' else target, target if op == 'rsv02' else nxt)
            elif op in ('rsv0b', 'rsv0d', 'rsv06'):
                if op == 'rsv06':
                    default = off + 4 + args[0]
                    cases = [(args[j], off + 4 + args[j+1]) for j in range(1, len(args)-1, 2)]
                else:
                    origin = off + (1 if op == 'rsv0b' else 2)
                    default = origin + args[1]
                    cases = [(s16(args[0])+j, origin+t) for j,t in enumerate(args[2:])]
                targets = [default] + [t for v,t in cases]
                if i and self.ops[i-1]['op'] == 'EXPR':
                    self._switches[off] = (expr_bytes(self.ops[i-1]['args'][0]['expr']), default, cases)
            self._edges[off] = list(dict.fromkeys(t for t in targets if t in self.index))
        entry = self.ops[0]['off'] if self.ops else None
        post, seen = [], set()
        stack = [(entry, False)] if entry is not None else []
        while stack:
            node, done = stack.pop()
            if done: post.append(node); continue
            if node in seen: continue
            seen.add(node); stack.append((node, True))
            stack.extend((n,False) for n in self._edges[node] if n not in seen)
        order = {n:i for i,n in enumerate(reversed(post))}
        predecessors = {n:[] for n in seen}
        for node in seen:
            for child in self._edges[node]: predecessors[child].append(node)
        idom = {entry:entry} if entry is not None else {}
        def intersect(a,b):
            while a != b:
                while order[a] > order[b]: a = idom[a]
                while order[b] > order[a]: b = idom[b]
            return a
        changed = True
        while changed:
            changed = False
            for node in reversed(post):
                if node == entry: continue
                parents = [n for n in predecessors[node] if n in idom]
                if not parents: continue
                common = parents[0]
                for parent in parents[1:]: common = intersect(common,parent)
                if idom.get(node) != common: idom[node] = common; changed = True
        self._idom, self._reach, self._guard_cache = idom, {}, {}

    def _without_branch(self, start, branch):
        key = (start,branch)
        if key not in self._reach:
            seen, pending = set(), [start]
            while pending:
                node = pending.pop()
                if node == branch or node in seen or node not in self._edges: continue
                seen.add(node); pending.extend(self._edges[node])
            self._reach[key] = seen
        return self._reach[key]

    def prefix_sites(self):
        """Sites the current native invocation can reach without yielding.

        A yield site itself is evidence that the action can start. Its later
        result, or the scheduled child of a request, is not prefix evidence.
        """
        self._flow()
        barriers={'WAIT','WAITANIM','WAITTURN','WAITMES','REQSW','REQEW',
                  'MES','MESN','MESA','MESAN','MESB','CHOOSEPARAM','MAPJUMP','WMAPJUMP'}
        seen=set()
        pending=[self.ops[0]['off']] if self.ops else []
        while pending:
            off=pending.pop()
            if off in seen:continue
            seen.add(off)
            if self.ops[self.index[off]]['op'] not in barriers:
                pending.extend(self._edges[off])
        return seen

    def guards(self, site):
        """Necessary predicates of dominating native branches, including early skips/returns.

        These static predicates are NOT a complete action proof. The runtime must also
        reach the bound assignment on its detached native trigger/request path.
        """
        self._flow()
        if site in self._guard_cache: return self._guard_cache[site]
        result, node, seen = [], site, set()
        if site not in self._idom: return [bytes([0x7d,0,0,0x7f])]
        def either(expressions):
            if not expressions: return bytes([0x7d,0,0,0x7f])
            raw = expressions[0][:-1]
            for value in expressions[1:]: raw += value[:-1] + bytes([40])
            return raw + bytes([0x7f])
        while node not in seen:
            seen.add(node)
            if node in self._conditions:
                raw, yes, no = self._conditions[node]
                a = site in self._without_branch(yes,node)
                b = site in self._without_branch(no,node)
                if a and not b: result.append(raw)
                elif b and not a: result.append(negate_bytes(raw))
            if node in self._switches:
                selector, default, cases = self._switches[node]
                allowed = {target for value,target in cases if site in self._without_branch(target,node)}
                default_ok = site in self._without_branch(default,node)
                if default_ok:
                    rejected = [equals_bytes(selector,value) for value,target in cases if target not in allowed]
                    if rejected: result.append(negate_bytes(either(rejected)))
                else:
                    accepted = [equals_bytes(selector,value) for value,target in cases if target in allowed]
                    if accepted: result.append(either(accepted))
            parent = self._idom[node]
            if parent == node: break
            node = parent
        result.reverse(); self._guard_cache[site] = result
        return result
