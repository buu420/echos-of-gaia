"""Export the physical identity of FFIX field scenes for stable repeated-area numbering.

Read-only. A field's native background id (EventEngineUtils.eventIDToFBGID) is a scene such as
FBG_N01_ALXT_MAP016_AT_MSA_0. Story/disc variants of one physical place use a new scene id
(MAP820_AT_MSA_A at night, MAP830_AT_MSA_W in disc 3), while distinct rooms of one named place
can differ only by the final suffix (Cleyra GT_DUN_0 .. GT_DUN_F). Neither the name nor the scene
alone identifies the place.

Two scenes are one physical area when they share the scene's area and room token
(ALXT / AT_MSA) AND either
  * the installed walkmesh (.bgi) is byte-identical, or
  * a background camera pose (.bgs rotation and translation) is shared AND the walkable floor
    covers the same ground: intersection-over-union of the rasterised XZ floor (32-unit cells)
    of at least MIN_FLOOR_IOU.
A camera pose alone is not enough: pre-rendered rooms reuse poses (Lindblum SG_TOP 342 and 343
share one pose with floor IoU 0.10; the Red Rose and Hilda Garde III bridges share one too).
The relation is closed transitively. Nothing comes from walkthroughs; a scene without installed
assets stays separate and is listed with its exact missing asset keys.

Outputs:
  Assembly-CSharp/Memoria/Accessibility/Navigation/Core/FieldPhysicalAreas.Generated.cs
    scene -> canonical scene, only for scenes merged with another scene;
  docs/accessibility/field-physical-areas.json
    the evidence (per scene hashes, the merge reason) for review.
"""
import argparse, hashlib, json, re, struct, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
p = argparse.ArgumentParser()
p.add_argument('--inventory', required=True, help='decoded native field inventory (field -> scene)')
p.add_argument('--game', default=r'D:\SteamLibrary\steamapps\common\FINAL FANTASY IX')
p.add_argument('--unitypy', help='optional directory containing the UnityPy package')
p.add_argument('--output', default=str(ROOT / 'Assembly-CSharp/Memoria/Accessibility/Navigation/Core/FieldPhysicalAreas.Generated.cs'))
p.add_argument('--ledger', default=str(ROOT / 'docs/accessibility/field-physical-areas.json'))
p.add_argument('--min-floor-iou', type=float, default=0.6)
a = p.parse_args()

if a.unitypy:
    sys.path.insert(0, a.unitypy)
import UnityPy

fields = json.loads(Path(a.inventory).read_bytes())
scene_of = {int(k): v['scene'] for k, v in fields.items()}
scenes = sorted(set(scene_of.values()))

bundles = sorted((Path(a.game) / 'StreamingAssets').glob('p0data1*.bin'))
index = {}
envs = {}
for bundle in bundles:
    env = UnityPy.load(str(bundle))
    envs[bundle.name] = env
    for key in env.container.keys():
        lower = key.lower()
        if lower.startswith('assets/resources/fieldmaps/') and (lower.endswith('.bgs.bytes') or lower.endswith('.bgi.bytes')):
            index[lower] = bundle.name


def asset(scene, ext):
    key = 'assets/resources/fieldmaps/%s/%s.%s.bytes' % (scene.lower(), scene.lower(), ext)
    bundle = index.get(key)
    if bundle is None:
        return None
    data = envs[bundle].container[key].read().m_Script
    return data.encode('utf-8', 'surrogateescape') if isinstance(data, str) else bytes(data)


def cameras(raw):
    # BGSCENE_DEF header (Memoria BGSCENE_DEF.ReadData): six UInt16 counts, then four UInt32 offsets.
    if raw is None or len(raw) < 28:
        return None
    count = struct.unpack_from('<H', raw, 10)[0]
    offset = struct.unpack_from('<I', raw, 24)[0]
    result = []
    for i in range(count):
        o = offset + 52 * i
        if o + 32 > len(raw):
            return None
        # A camera pose: rotation matrix and translation (projection distance may be retuned per variant).
        result.append(hashlib.sha256(json.dumps([list(struct.unpack_from('<9h', raw, o + 2)), list(struct.unpack_from('<3i', raw, o + 20))]).encode()).hexdigest()[:16])
    return sorted(set(result)) if result else None


GRID = 32


def floor_cells(raw):
    # BGI_DEF.ReadData / BGI_TRI_DEF / BGI_FLOOR_DEF layouts: absolute vertex = raw + floor org + bgi org.
    if raw is None or len(raw) < 64:
        return None
    r = lambda f, o: struct.unpack_from('<' + f, raw, o)
    org = r('3h', 6)
    (_, _, tri_count, tri_off, _, _, _, _, floor_count, floor_off, _, _, vert_count, vert_off) = r('2h12H', 36)
    verts = [r('3h', 4 + vert_off + 6 * i) for i in range(vert_count)]
    floors = [r('3h', 4 + floor_off + 32 * i + 4) for i in range(floor_count)]
    cells = set()
    for i in range(tri_count):
        o = 4 + tri_off + 40 * i
        flags, _, floor, _ = r('HHhh', o)
        if not flags & 1:
            continue
        fo = floors[floor] if 0 <= floor < floor_count else (0, 0, 0)
        (ax, _, az), (bx, _, bz), (cx, _, cz) = [tuple(verts[k][j] + fo[j] + org[j] for j in range(3)) for k in r('3h', o + 12)]
        d = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz)
        if d == 0:
            continue
        for gx in range(int(min(ax, bx, cx) // GRID), int(max(ax, bx, cx) // GRID) + 1):
            for gz in range(int(min(az, bz, cz) // GRID), int(max(az, bz, cz) // GRID) + 1):
                px, pz = (gx + .5) * GRID, (gz + .5) * GRID
                l1 = ((bz - cz) * (px - cx) + (cx - bx) * (pz - cz)) / d
                l2 = ((cz - az) * (px - cx) + (ax - cx) * (pz - cz)) / d
                if l1 >= -0.05 and l2 >= -0.05 and l1 + l2 <= 1.05:
                    cells.add((gx, gz))
    return cells


TOKEN = re.compile(r'^FBG_N\d+_([A-Z0-9]+)_MAPV?\d+[A-Z]?_([A-Z0-9]+)_([A-Z0-9]+)_[A-Z0-9]+$')
evidence = {}
floors = {}
missing = {}
for scene in scenes:
    bgi = asset(scene, 'bgi')
    bgs = asset(scene, 'bgs')
    match = TOKEN.match(scene)
    floors[scene] = floor_cells(bgi)
    evidence[scene] = {'room': '/'.join(match.groups()) if match else None,
                       'walkmesh': hashlib.sha256(bgi).hexdigest() if bgi else None,
                       'cameras': cameras(bgs)}
    if bgi is None or bgs is None:
        missing[scene] = ['assets/resources/fieldmaps/%s/%s.%s.bytes' % (scene.lower(), scene.lower(), ext)
                          for ext, raw in (('bgi', bgi), ('bgs', bgs)) if raw is None]


def floor_iou(x, y):
    a, b = floors.get(x), floors.get(y)
    if not a or not b:
        return None
    return len(a & b) / len(a | b)

parent = {s: s for s in scenes}
first_field = {}
for field, scene in sorted(scene_of.items()):
    first_field.setdefault(scene, field)


def find(s):
    while parent[s] != s:
        parent[s] = parent[parent[s]]
        s = parent[s]
    return s


def union(x, y):
    rx, ry = find(x), find(y)
    if rx == ry:
        return
    # The canonical scene is the one the game uses first (lowest native field id).
    if (first_field[rx], rx) > (first_field[ry], ry):
        rx, ry = ry, rx
    parent[ry] = rx


reasons = {}
rejected = []
by_room = {}
for scene, item in evidence.items():
    if item['room']:
        by_room.setdefault(item['room'], []).append(scene)
for room, members in by_room.items():
    for i, x in enumerate(members):
        for y in members[i + 1:]:
            ex, ey = evidence[x], evidence[y]
            why = None
            pose = bool(ex['cameras'] and ey['cameras'] and set(ex['cameras']) & set(ey['cameras']))
            iou = floor_iou(x, y)
            if ex['walkmesh'] and ex['walkmesh'] == ey['walkmesh']:
                why = 'identical walkmesh'
            elif pose and iou is not None and iou >= a.min_floor_iou:
                why = 'camera pose and floor IoU %.2f' % iou
            elif pose:
                rejected.append({'a': x, 'b': y, 'floorIoU': None if iou is None else round(iou, 3)})
            if why:
                union(x, y)
                reasons.setdefault(x, {})[y] = why

aliases = {s: find(s) for s in scenes if find(s) != s}
groups = {}
for scene, canonical in aliases.items():
    groups.setdefault(canonical, []).append(scene)

lines = ['// <auto-generated> tools/Export-FF9PhysicalAreas.py from the installed field assets. Do not edit.',
         'using System.Collections.Generic;',
         'namespace Memoria.Accessibility.Navigation',
         '{',
         '    // Scene -> canonical scene of the same physical place: same room token and an identical installed',
         '    // walkmesh, or a shared camera pose with the same walkable floor. Scenes absent here are their own place.',
         '    public static partial class FieldPhysicalAreas',
         '    {',
         '        private static readonly Dictionary<string,string> _aliases=new Dictionary<string,string>{']
for scene in sorted(aliases):
    lines.append('            {"%s","%s"},' % (scene, aliases[scene]))
lines += ['        };', '    }', '}', '']
Path(a.output).write_text('\n'.join(lines), encoding='utf-8')
ledger = {'schema': 1, 'inventorySHA256': hashlib.sha256(Path(a.inventory).read_bytes()).hexdigest(),
          'scenes': len(scenes), 'mergedScenes': len(aliases), 'physicalAreas': len(scenes) - len(aliases),
          'minFloorIoU': a.min_floor_iou,
          'missingAssets': {s: {'fields': sorted(f for f, sc in scene_of.items() if sc == s), 'keys': keys} for s, keys in sorted(missing.items())},
          'rejectedSamePose': rejected,
          'groups': {k: sorted(v) for k, v in sorted(groups.items())},
          'reasons': reasons, 'evidence': evidence}
Path(a.ledger).write_text(json.dumps(ledger, indent=1, sort_keys=True), encoding='utf-8')
print('scenes', len(scenes), 'merged', len(aliases), 'physical', len(scenes) - len(aliases), 'missing assets', len(missing), 'pose-only rejected', len(rejected))
