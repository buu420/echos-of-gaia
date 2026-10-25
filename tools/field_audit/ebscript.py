"""Read-only FF9 field event-script decoder (mirrors EventEngineUtils.BinaryScript / Code / ArgumentExpression).

Entry bytes == runtime Obj.ebData (EventEngine.cs:565-571: 128 + sObjTable[i].ofs, size), so
sha256(entry bytes) matches NativeFieldTargets.Fingerprint(obj.ebData).
"""
import hashlib
import sys
from pathlib import Path

from opcodes import COUNTS, SIZES, NAMES, BYNUM

from audit_paths import UNITYPY, GAME_ROOT as GAME

OP = NAMES  # name -> number


def u16(b, p):
    return b[p] | (b[p + 1] << 8)


class Expr:
    __slots__ = ('ops',)  # list of (op, argbytes)

    def __init__(self, ops):
        self.ops = ops

    def raw(self):
        out = bytearray()
        for op, a in self.ops:
            out.append(op)
            if a:
                out += a
        return bytes(out)

    def is_sysvar(self, n):
        return len(self.ops) == 2 and self.ops[0][0] == 0x7a and self.ops[0][1][0] == n and self.ops[1][0] == 0x7f

    def opset(self):
        return set(op for op, _ in self.ops)

    def text(self):
        parts = []
        for op, a in self.ops:
            if op == 0x7f:
                break
            if op >= 0xc0:
                src = 'GLOB' if (op & 3) == 0 else 'MAP' if (op & 3) == 1 else 'LOC' if (op & 3) == 2 else '?'
                typ = (op & 0x1c) >> 2
                idx = a[0] | ((a[1] << 8) if len(a) > 1 else 0)
                parts.append('%s%d[%d]' % (src, typ, idx))
            elif op == 0x7d:
                v = a[0] | (a[1] << 8)
                parts.append(str(v - 65536 if v >= 32768 else v))
            elif op == 0x7e:
                parts.append(str(int.from_bytes(a, 'little', signed=True)))
            elif op == 0x7a:
                parts.append('SYS%d' % a[0])
            elif op == 0x79:
                parts.append('SYSLIST%d' % a[0])
            elif op == 0x78:
                parts.append('OBJA(%d,%d)' % (a[0], a[1]))
            elif op in (0x29, 0x5f):
                parts.append('op%02x(%d)' % (op, a[0]))
            else:
                parts.append(OPNAME.get(op, 'o%d' % op))
        return ' '.join(parts)


OPNAME = {4: '++post', 5: '--post', 6: '++pre', 7: '--pre', 12: '+u', 13: '-u', 14: '!', 15: '!e', 16: '~',
          17: '*', 18: '/', 19: '%', 20: '+', 21: '-', 22: '<<', 23: '>>', 24: '<', 25: '>', 26: '<=', 27: '>=',
          28: '<e', 29: '>e', 30: '<=e', 31: '>=e', 32: '==', 33: '!=', 34: '==e', 35: '!=e', 36: '&', 37: '^', 38: '|',
          39: '&&', 40: '||', 41: 'MEMBER', 42: 'COUNT', 43: 'PICK', 44: '=', 70: 'c8', 71: 'c8u', 72: 'c16', 73: 'c16u',
          78: 'OBJSPEC', 79: 'KEYON', 88: 'KEYOFF', 89: 'KEY', 90: 'KEYON2', 91: 'KEYOFF2', 92: 'KEY2', 93: 'ANGLE',
          94: 'DISTANCE', 96: 'ANGLEA', 97: 'DISTANCEA', 100: 'HAVE_ITEM', 106: 'FRAME', 107: 'PARTYCHK', 112: 'BGIID',
          113: 'BGIFLOOR', 102: 'ANGLE2', 82: 'CURHP', 83: 'MAXHP', 109: 'PARTYADD'}


def read_expr(b, p):
    ops = []
    for _ in range(2048):
        op = b[p]
        p += 1
        isvar = op >= 0xc0 or op in (0x29, 0x5f, 0x78, 0x79, 0x7a)
        isconst = op in (0x7d, 0x7e)
        if isvar or isconst:
            n = 4 if op == 0x7e else 2 if (op >= 0xe0 or op in (0x78, 0x7d)) else 1
            ops.append((op, bytes(b[p:p + n])))
            p += n
        else:
            ops.append((op, None))
        if op == 0x7f:
            return Expr(ops), p
        if p > len(b):
            raise ValueError('truncated expression')
    raise ValueError('unbounded expression')


class Ins:
    __slots__ = ('off', 'next', 'op', 'args')  # args: list of int | Expr

    def __init__(self, off, nxt, op, args):
        self.off, self.next, self.op, self.args = off, nxt, op, args

    @property
    def name(self):
        return BYNUM.get(self.op, 'op%x' % self.op)

    def c(self, i):
        if i < len(self.args) and isinstance(self.args[i], int):
            return self.args[i]
        return None

    def __repr__(self):
        a = []
        for x in self.args:
            a.append(str(x) if isinstance(x, int) else '{' + x.text() + '}')
        return '%04x %s(%s)' % (self.off, self.name, ', '.join(a))


def decode_function(b, start, end):
    p = start
    out = []
    while p < end:
        off = p
        op = b[p]
        p += 1
        if op == 0xff:
            op = 0x100 | b[p]
            p += 1
        count = COUNTS[op]
        flags = 0
        if op >= 0x10 and count != 0:
            flags = b[p]
            p += 1
        if op == 0x05:
            flags = 1
        if count < 0:
            count = b[p]
            p += 1
            if op == 0x0d:
                count |= b[p] << 8
                p += 1
            if op == 0x06:
                count = 1 + 2 * count
            elif op in (0x0b, 0x0d):
                count = 2 + count
        args = []
        for i in range(count):
            if flags & (1 << i):
                e, p = read_expr(b, p)
                args.append(e)
            else:
                size = 4 if op == 0x29 else 2 if op in (0x06, 0x0b, 0x0d) else (SIZES[op][i] if SIZES[op] else 0)
                v = int.from_bytes(b[p:p + size], 'little') if size else 0
                args.append(v)
                p += size
        if p > end:
            raise ValueError('instruction crosses function end at %x' % off)
        out.append(Ins(off, p, op, args))
    return out


class Entry:
    def __init__(self, sid, data, lvc, flags):
        self.sid = sid
        self.data = data
        self.lvc = lvc
        self.flags = flags
        self.sha = hashlib.sha256(data).hexdigest()
        self.type = data[0] if data else None
        self.funcs = {}  # tag -> (start, end, [Ins])
        self.errors = []
        if len(data) < 2:
            return
        n = data[1]
        table = [(u16(data, 2 + 4 * i), 2 + u16(data, 4 + 4 * i)) for i in range(n)]
        for i, (tag, start) in enumerate(table):
            end = table[i + 1][1] if i + 1 < n else len(data)
            try:
                self.funcs[tag] = (start, end, decode_function(data, start, end))
            except Exception as ex:  # keep going; report
                self.errors.append('tag %d: %s' % (tag, ex))
                self.funcs[tag] = (start, end, [])


class Script:
    def __init__(self, raw):
        assert raw[:2] == b'EV'
        self.raw = raw
        self.sha = hashlib.sha256(raw).hexdigest()
        count = raw[3]
        base = 128
        self.entries = []
        for i in range(count):
            p = base + 8 * i
            off, size, lvc, fl = u16(raw, p), u16(raw, p + 2), raw[p + 4], raw[p + 5]
            data = raw[base + off: base + off + size]
            self.entries.append(Entry(i, data, lvc, fl))


def load_field_scripts(lang='us'):
    sys.path.insert(0, UNITYPY)
    import UnityPy
    bundle = GAME / 'StreamingAssets' / 'p0data7.bin'
    env = UnityPy.load(str(bundle))
    out = {}
    marker = '/eventbinary/field/%s/' % lang
    for path, obj in env.container.items():
        if marker not in path.lower():
            continue
        asset = obj.read()
        raw = asset.m_Script
        if isinstance(raw, str):
            raw = raw.encode('utf-8', 'surrogateescape')
        name = path.rsplit('/', 1)[-1]
        if name.lower().endswith('.bytes'):
            name = name[:-6]
        if name.lower().endswith('.eb'):
            name = name[:-3]
        if name.upper() in out:
            raise ValueError('Duplicate installed field event asset: ' + name)
        out[name.upper()] = (path, bytes(raw))
    return out, hashlib.sha256(bundle.read_bytes()).hexdigest()
