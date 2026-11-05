"""Export real decoded exit regions (installed field scripts) as a test fixture for FieldExitPaths.

Read-only. For each listed field/region the fixture keeps the region's touch function (tag 2) and every
function it requests (transitively), re-encoded as Memoria ScriptInstruction data: opcode numbers from
EBin.event_code_binary, constant arguments, and expression bytes exactly as NativeFieldTargets.Decode
concatenates them (operation byte followed by its argument bytes).
"""
import argparse, json, re
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
p = argparse.ArgumentParser()
p.add_argument('--inventory', required=True)
p.add_argument('--output', default=str(ROOT / 'tests/fixtures/exit-native-cases.json'))
# field:region sid[:controlled player sid[:trigger tag]].
p.add_argument('--case', action='append')
a = p.parse_args()
a.case = a.case or ['551:13:5', '601:5:1', '251:12:1', '756:5:6',
                    '905:10:16','2750:1:-1:3','2751:2:-1:3','2901:7:11:3']
src = (ROOT / 'Assembly-CSharp/Global/EBin.cs').read_text(encoding='utf-8')
i = src.index('public enum event_code_binary')
body = src[src.index('{', i) + 1:src.index('}', i)]
opcodes, value = {}, -1
for raw in body.split(','):
    item = re.sub(r'//.*', '', raw).strip()
    if not item:
        continue
    m = re.match(r'^(\w+)(?:\s*=\s*(0x[0-9A-Fa-f]+|\d+))?$', item)
    if not m:
        continue
    value = int(m.group(2), 0) if m.group(2) else value + 1
    opcodes[m.group(1)] = value
fields = json.loads(Path(a.inventory).read_bytes())


def encode(item):
    args, exprs = [], []
    for arg in item['args']:
        if isinstance(arg, dict):
            data = []
            for op, h in arg['expr']:
                data.append(op)
                data.extend(bytes.fromhex(h))
            args.append(None)
            exprs.append(bytes(data).hex())
        else:
            args.append(arg)
            exprs.append(None)
    return {'offset': item['off'], 'next': item['next'], 'opcode': opcodes[item['op']], 'args': args, 'exprs': exprs}


cases = []
for case in a.case:
    parts = [int(x) for x in case.split(':')]
    field, sid, player = parts[0], parts[1], parts[2] if len(parts) > 2 else -1
    trigger = parts[3] if len(parts)>3 else 2
    entries = fields[str(field)]['entries']
    functions, pending = {}, [(sid, trigger)]
    while pending:
        s, t = pending.pop()
        key = '%d:%d' % (s, t)
        entry = entries.get(str(player if s == 250 else s))
        if key in functions or not entry or str(t) not in entry['functions']:
            continue
        code = entry['functions'][str(t)]
        functions[key] = [encode(x) for x in code]
        for x in code:
            if x['op'] in ('REQ', 'REQSW', 'REQEW') and len(x['args']) >= 3 and all(isinstance(v, int) for v in x['args'][1:3]):
                pending.append((x['args'][1], x['args'][2]))
    cases.append({'field': field, 'sid': sid, 'player': player, 'tag':trigger, 'hash': entries[str(sid)]['sha256'], 'functions': functions})
Path(a.output).write_text(json.dumps({'opcodes': {k: opcodes[k] for k in ('MAPJUMP', 'WMAPJUMP', 'REQ', 'REQSW', 'REQEW', 'MES', 'MESN', 'MESA', 'MESAN', 'MESB', 'UCOFF', 'UCON', 'MOVE', 'MOVQ', 'MJPOS')},
                                      'cases': cases}, indent=0), encoding='utf-8')
print({c['field']: len(c['functions']) for c in cases}, opcodes['MAPJUMP'], opcodes['REQEW'])
