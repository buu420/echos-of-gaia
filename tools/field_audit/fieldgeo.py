"""Read-only BGS camera + BGI walkmesh loader mirroring Memoria runtime conventions.

BGI_DEF.ReadData / BGI_TRI_DEF.ReadData / BGI_FLOOR_DEF.ReadData layouts; WalkMesh.ProcessBGI absolute
vertices (raw + floor.orgPos + bgi.orgPos, PSX y kept here); edge convention from
WalkMeshTriangle.GetVertexIdxForEdge: native neighbour n[k] spans (v[(k+2)%3], v[k]).  The Memoria runtime
FieldTriangle (NativeFieldGeometry.Normalize) uses Neighbors[e]=n[(e+1)%3] across (v[e], v[e+1]) -- identical.
Enabled = triFlags & 1, Attributes = triFlags >> 8 (FieldNativeGeometryContract.CreateTriangle).
"""
import struct
import sys
import hashlib
from functools import lru_cache
from pathlib import Path

from audit_paths import UNITYPY, GAME_ROOT
SA = GAME_ROOT / "StreamingAssets"
BUNDLES = ['p0data11.bin', 'p0data12.bin', 'p0data13.bin', 'p0data14.bin', 'p0data15.bin', 'p0data16.bin',
           'p0data17.bin', 'p0data18.bin', 'p0data19.bin']
HALF_H = 112


@lru_cache(maxsize=None)
def _env(name):
    sys.path.insert(0, UNITYPY)
    import UnityPy
    return UnityPy.load(str(SA / name))


_index = None


def _build_index():
    global _index
    if _index is not None:
        return _index
    _index = {}
    for b in BUNDLES:
        e = _env(b)
        for k in e.container.keys():
            kl = k.lower()
            if kl.startswith('assets/resources/fieldmaps/') and (kl.endswith('.bgs.bytes') or kl.endswith('.bgi.bytes')):
                _index[kl] = b
    return _index


def field_asset(scene, ext):
    s = scene.lower()
    key = 'assets/resources/fieldmaps/%s/%s.%s.bytes' % (s, s, ext)
    idx = _build_index()
    b = idx.get(key)
    if b is None:
        return None, None
    d = _env(b).container[key].read().m_Script
    raw = d.encode('utf-8', 'surrogateescape') if isinstance(d, str) else bytes(d)
    return b, raw


def parse_bgs_cameras(raw):
    r = lambda f, o: struct.unpack_from('<' + f, raw, o)
    sceneLength, depthShift, animCount, overlayCount, lightCount, cameraCount = r('6H', 0)
    animOff, overlayOff, lightOff, cameraOff = r('4I', 12)
    cams = []
    for i in range(cameraCount):
        o = cameraOff + 52 * i
        proj, = r('H', o)
        rr = r('9h', o + 2)
        t = r('3i', o + 20)
        cxy = r('2h', o + 32)
        w, h = r('2h', o + 36)
        vrp = r('4h', o + 40)
        cams.append({'index': i, 'proj': proj, 'r': rr, 't': t, 'centerOffset': cxy, 'w': w, 'h': h,
                     'vrpMinX': vrp[0], 'vrpMaxX': vrp[1], 'vrpMinY': vrp[2], 'vrpMaxY': vrp[3]})
    return cams


def parse_bgi(raw):
    r = lambda f, o: struct.unpack_from('<' + f, raw, o)
    org = r('3h', 6)
    cur = r('3h', 12)
    (activeFloor, activeTri, triCount, triOff, edgeCount, edgeOff, anmCount, anmOff, floorCount, floorOff,
     normalCount, normalOff, vertCount, vertOff) = r('2h12H', 36)
    verts = [r('3h', 4 + vertOff + 6 * i) for i in range(vertCount)]
    floors = []
    for i in range(floorCount):
        o = 4 + floorOff + 32 * i
        flags, ndx = r('2H', o)
        forg = r('3h', o + 4)
        fcur = r('3h', o + 10)
        floors.append({'flags': flags, 'org': forg, 'cur': fcur})
    tris = []
    for i in range(triCount):
        o = 4 + triOff + 40 * i
        fl, data, floor, normal = r('HHhh', o)
        vi = r('3h', o + 12)
        ei = r('3h', o + 18)
        ni = r('3h', o + 24)
        fo = floors[floor]['org'] if 0 <= floor < floorCount else (0, 0, 0)
        v = [tuple(verts[k][j] + fo[j] + org[j] for j in range(3)) for k in vi]  # PSX space (y not negated)
        tris.append({'id': i, 'flags': fl, 'enabled': bool(fl & 1), 'attr': fl >> 8, 'floor': floor, 'v': v, 'n': ni})
    return {'org': org, 'cur': cur, 'tris': tris, 'floors': floors}


def outer_edges(bgi, blocked=0x80, mask=0xff):
    """Runtime-equivalent permitted outer boundary edges: list of (tri, e, a, b) with edge e = (v[e], v[e+1])
    whose neighbour n[(e+1)%3] < 0 (NativeFieldTargets/FieldGeometry.IsNativeOuterBoundary)."""
    out = []
    for t in bgi['tris']:
        permitted = t['enabled'] and (t['attr'] & mask & blocked) == 0
        if not permitted:
            continue
        for e in range(3):
            if t['n'][(e + 1) % 3] < 0:
                out.append((t['id'], e, t['v'][e], t['v'][(e + 1) % 3]))
    return out


def project(cam, p, half_w=160):
    """PSX.CalculateGTE_RTPT_POS with FieldMap offset; p in PSX space. Returns Project-space (X, Y) (y up)."""
    R = [[cam['r'][3 * i + j] / 4096.0 for j in range(3)] for i in range(3)]
    res = [sum(R[i][j] * p[j] for j in range(3)) + cam['t'][i] for i in range(3)]
    res[1] = -res[1]
    z = abs(res[2]) or 1e-6
    x = res[0] * cam['proj'] / z
    y = res[1] * cam['proj'] / z
    offx = cam['centerOffset'][0] + cam['w'] / 2.0 - half_w
    offy = -cam['centerOffset'][1] - cam['h'] / 2.0 + HALF_H
    return (x + offx, y + offy, res[2])


def bgpix(cam, p, half_w=160):
    """Background pixel coordinates (x right, y down) of a PSX-space point; independent of half_w."""
    X, Y, z = project(cam, p, half_w)
    return (X + half_w, HALF_H - Y, z)


def camera_center(cam, aim_px, half_w=160, widescreen=False):
    """FieldMap.SceneService3DScroll: camera centre (bg px) = aim clamped to vrp ranges."""
    vminx, vmaxx = cam['vrpMinX'], cam['vrpMaxX']
    if widescreen:
        desired = 2 * half_w - 320
        delta = min(desired, vmaxx - vminx) // 2
        vminx, vmaxx = vminx + delta, vmaxx - delta
    cx = min(max(aim_px[0], vminx), vmaxx)
    cy = min(max(aim_px[1], cam['vrpMinY']), cam['vrpMaxY'])
    return cx, cy


def in_view(pt_px, center, half_w=160):
    return abs(pt_px[0] - center[0]) <= half_w and abs(pt_px[1] - center[1]) <= HALF_H and pt_px[2] > 0


def sha(raw):
    return hashlib.sha256(raw).hexdigest()
