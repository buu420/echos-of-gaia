"""Bind reviewed background objects to their own native passive FICON loop.

No world position is inferred from an actor or from walkthrough directions. A
reviewed object anchor is the centre of the native prompt's position predicate.
The runtime still evaluates that predicate on the actual current floor and state.
"""
import argparse
import hashlib
import json
from pathlib import Path
from story_native_graph import NativeEventGraph

ROOT = Path(__file__).resolve().parents[1]
# Original descriptions of the actual objects; no optional reward contents.
OBJECTS = {
    2213: {4: 'Entrance candelabrum', 5: 'Entrance bloodstone pedestal', 6: 'Balcony candelabrum'},
    2214: {8: 'Central candelabrum', 9: 'Side candelabrum 1', 10: 'Side candelabrum 2'},
    2215: {n: 'Candelabrum ' + str(n) for n in range(1, 9)},
    2216: {3: 'Passage candelabrum', 4: 'Passage bloodstone pedestal'},
    2217: {11: 'Landing candelabrum 1', 12: 'Upper landing candelabrum',
           13: 'Landing candelabrum 2', 14: 'Upper landing bloodstone pedestal'},
    2220: {1: 'Upper shelf candelabrum', 2: 'Middle shelf candelabrum',
           3: 'Lower shelf candelabrum', 4: 'Candelabrum by the stairs', 5: 'Library inscription'},
    2221: {2: 'Candelabrum 1', 3: 'Candelabrum 2', 4: 'Candelabrum 3', 5: 'Candelabrum 4',
           6: 'Stone inscription', 7: 'Dark statue', 8: 'Light statue',
           9: 'Bloodstone pedestal 1', 10: 'Bloodstone pedestal 2'},
    2222: {6: 'Final candelabrum'},
}
# Three visible save spheres have persistent sprite effects. Four other save
# services in Memoria are discoverable only through the native field icon.
SAVE_SPHERES = {2901: (9, 644), 2913: (4, 646), 2925: (4, 988)}
HIDDEN_SAVE_POINTS = {(2905, 6), (2909, 4), (2916, 4), (2919, 7)}


def expressions(code):
    return [arg['expr'] for row in code for arg in row['args'] if isinstance(arg, dict) and 'expr' in arg]


def const(value):
    op, raw = value
    if op not in (0x7d, 0x7e):
        return None
    return int.from_bytes(bytes.fromhex(raw), 'little', signed=True)


def coordinate(code, prop, opcodes):
    values = set()
    for expr in expressions(code):
        for first, second, third in zip(expr, expr[1:], expr[2:]):
            if first == [0x78, 'fa%02x' % prop] and third[0] in opcodes and const(second) is not None:
                values.add(const(second))
    return sorted(values)


def bind(field, entry, label, native, assets, floor=9825):
    entries = native['entries']
    owner = entries[str(entry)]
    if owner['type'] != 0 or NativeEventGraph(entries).instances[entry] != {entry}:
        raise ValueError('Not one native passive loop instance: %s:%s' % (field, entry))
    code = owner['functions']['1']
    cue, = [row for row in code if row['op'] == 'FICON']
    prefix = [row for row in code if row['off'] < cue['off']]
    if any(row['op'] not in ('EXPR', 'rsv01', 'rsv02', 'rsv03', 'rsv06', 'rsv0B', 'rsv0D') for row in prefix):
        raise ValueError('Passive prompt prefix contains an unreviewed operation')
    x, = coordinate(prefix, 0, {21})
    z, = coordinate(prefix, 2, {21})
    y_min, y_max = coordinate(prefix, 1, {24, 25})
    entry_ip = code[0]['off']
    resume, = [nxt for current, nxt in zip(code, code[1:]) if current['op'] == 'WAIT' and
               nxt['op'] == 'rsv01' and nxt['next'] + (nxt['args'][0] - 65536 if nxt['args'][0] >= 32768 else nxt['args'][0]) == entry_ip]
    button = next(row for row in code if row['off'] == cue['next'])
    if not any(op == 79 for expr in expressions([button]) for op, _ in expr):
        raise ValueError('No native button check follows the reviewed prompt')
    return dict(field=field, scene=native['scene'], sid=entry, uid=entry, hash=owner['sha256'],
                background=assets['bgs'], walkmesh=assets['bgi'], floor=floor, ceiling=12001,
                entry=entry_ip, prompt=cue['off'], resume=resume['off'], button=button['off'],
                anchor=[x, -(y_min + y_max) // 2, z], halfWidth=600, label=label)

def bind_gondola(native, assets):
    field, sid = 1866, 17
    entries = native['entries']
    owner = entries[str(sid)]
    code = {row['off']:row for row in owner['functions']['1']}
    if (owner['type'] != 0 or NativeEventGraph(entries).instances[sid] != {sid} or
            code[15]['op'] != 'EXPR' or coordinate([code[15]],2,{25}) != [-1450] or
            code[38]['op'] != 'rsv02' or code[41]['op'] != 'FICON' or
            not any(op==79 for expr in expressions([code[44]]) for op,_ in expr) or
            code[104]['op'] != 'WAIT' or code[104]['next'] != 107):
        raise ValueError('Alexandria gondola prompt changed')
    # Center of the installed BGI dock triangle 2, within the native
    # Z > -1450 boarding strip. Runtime still replays the complete guard.
    return dict(field=field,scene=native['scene'],sid=sid,uid=sid,hash=owner['sha256'],
                background=assets['bgs'],walkmesh=assets['bgi'],floor=7200,ceiling=7300,
                entry=15,prompt=41,resume=107,button=44,anchor=[53,0,-1165],
                halfWidth=420,label='Gondola boarding')


def save_prompt(owner, menu):
    body = owner['functions']['1']
    cue, = [i for i in body if i['op'] == 'FICON' and i['args'] == [0]]
    button = next(i for i in body if i['off'] == cue['next'])
    if (not any(op == 79 for code in expressions([button]) for op, _ in code)
            or not any(i['op'] == 'MESN' and i['args'] == [7, 0, menu] for i in body)
            or not any(i['op'] == 'MENU' and i['args'] == [4, 0] for i in body)):
        raise ValueError('Memoria service no longer shows its native save menu')
    return cue['off']


def bind_save_sphere(field, sid, effect, native, assets):
    entries = native['entries']
    cue = save_prompt(entries[str(sid)], 455 if field == 2925 else 454)
    sprite = 10
    setup = [i['args'] for i in entries['0']['functions']['0'] if i['op'] == 'SPS']
    if [sprite, 130, effect, 0, 0] not in setup or [sprite, 156, 1, 0, 0] not in setup:
        raise ValueError('Memoria save sphere no longer has its native sprite')
    row = bind(field, sid, 'Save sphere', native, assets, floor=0)
    if row['prompt'] != cue:
        raise ValueError('Memoria save sphere prompt changed')
    return dict(row, sprite=[sprite, effect])


parser = argparse.ArgumentParser()
parser.add_argument('--inventory', required=True)
parser.add_argument('--asset-hashes', required=True, help='field-indexed BGS and BGI SHA-256 from installed assets')
parser.add_argument('--background-review', default=str(ROOT / 'docs/accessibility/reviewed-background-objects.json'))
parser.add_argument('--output', default=str(ROOT / 'docs/accessibility/field-loop-prompts.json'))
args = parser.parse_args()
inventory = Path(args.inventory).read_bytes()
fields = json.loads(inventory)
hashes = json.loads(Path(args.asset_hashes).read_text(encoding='utf-8-sig'))
rows = [bind(field, sid, label, fields[str(field)], hashes[str(field)])
        for field, objects in OBJECTS.items() for sid, label in objects.items()]
rows.append(bind_gondola(fields['1866'],hashes['1866']))
rows.extend(bind_save_sphere(field, sid, effect, fields[str(field)], hashes[str(field)])
            for field, (sid, effect) in SAVE_SPHERES.items())
review = json.loads(Path(args.background_review).read_text(encoding='utf-8-sig'))
bound = {(r['field'], r['sid']) for r in rows}
prompts = []
for witness in review['loopWitnesses']:
    f, s = witness['field'], witness['sid']
    if (f, s) in bound:
        continue
    native = fields[str(f)]
    owner = native['entries'][str(s)]
    if owner['type'] != 0 or owner['sha256'] != witness['hash'] or native['scene'] != witness['scene'] or not any(
            i['off'] == witness['prompt'] and i['op'] == 'FICON' for i in owner['functions'].get(str(witness['tag']), [])):
        raise ValueError('Unverified prompt-only native binding: %s:%s' % (f, s))
    # A cue-only binding uses the actual currently presented UID at runtime.
    # No placement or default UID is inferred for a reused/uncreated script.
    label = 'Interaction point'
    if (f, s) in HIDDEN_SAVE_POINTS:
        if witness['tag'] != 1 or witness['prompt'] != save_prompt(owner, 454):
            raise ValueError('Hidden save point has a different native prompt')
        label = 'Save point'
    prompts.append(dict(field=f, scene=native['scene'], sid=s, uid=-1, hash=owner['sha256'], floor=0, ceiling=12001,
                        prompt=witness['prompt'], tag=witness['tag'], promptOnly=True, label=label))
    bound.add((f,s))
result = dict(schema=1, inventorySHA256=hashlib.sha256(inventory).hexdigest(),
              proofBoundary='Native loop prompt and geometry. Prompt-only targets require the actual displayed bubble. Never supplies Confirm or exposes hidden reward contents.', objects=rows+prompts)
Path(args.output).write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print('PASS reviewed field loop prompts:', len(rows), 'objects in', len({r['field'] for r in rows}), 'fields;', len(prompts), 'prompt-only native bindings')
