#!/usr/bin/env python3
"""R156: generate the NATIVE app icon (the plumbob) for the macOS bundle.

v3 (2026-10-06, user directive "as original as possible"): the icon is the
REAL in-game plumbob — the xskin-head-arrow-ROOT-ARROW mesh and its `arrow`
texture read from the installed game data at PACK time, rasterized with the
native Animator lighting laws decoded in PLUMB-01 (MoodColor at full mood,
the camera-following directional light, per-facet ambient 0.05+0.25*ch).
Zero proprietary bytes are read or committed by the REPO; the game data is
the packer's own installation. If the mesh/texture cannot be found, falls
back to the v2 hand-shaded octahedron (itself the fallback from the flat
v1), so packing never fails.

Usage: python3 tools/make_native_icon.py [--out build/NativeIcon.icns]
                                         [--gamedata game-data/The Sims]
"""
import glob
import math
import os
import struct
import subprocess
import sys
import zlib

SIZES = [16, 32, 64, 128, 256, 512, 1024]

# ---------------------------------------------------------------------------
# v2 fallback: the hand-shaded octahedron (classic green look)
# ---------------------------------------------------------------------------

# vertical gradient stops (top vertex -> waist -> bottom vertex)
GRAD_TOP = (0xDA, 0xF2, 0x78)   # bright yellow-green
GRAD_MID = (0x53, 0xC2, 0x3C)   # vivid green at the waist
GRAD_BOT = (0x17, 0x5E, 0x12)   # deep green

# per-facet light multipliers (light from upper-left, classic 4-face look)
LIGHT_TL = 1.00
LIGHT_TR = 0.72
LIGHT_BL = 0.86
LIGHT_BR = 0.48

SEAM_DARK = 0.62     # facet seam darkening
SPEC_ADD = 95        # specular streak white addition (TL facet)


def grad(u):
    """Base gradient color at vertical position u (0 top, 1 bottom)."""
    if u < 0.52:
        t = u / 0.52
        a, b = GRAD_TOP, GRAD_MID
    else:
        t = (u - 0.52) / 0.48
        a, b = GRAD_MID, GRAD_BOT
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def shade_pixel(S, x, y, facet):
    """Color for one supersample pixel of the crystal."""
    u = y / S
    r, g, b = grad(u)
    light = {0: LIGHT_TL, 1: LIGHT_TR, 2: LIGHT_BL, 3: LIGHT_BR}[facet]
    r, g, b = r * light, g * light, b * light
    # specular highlight on the upper-left facet: one compact soft-edged
    # spot (gaussian in x and y) near the upper third — reads as a glint,
    # not a stripe
    if facet == 0:
        gx = x / S - 0.355
        gy = u - 0.14
        s = SPEC_ADD * pow(2.718281828, -((gx * gx) / 0.0016 + (gy * gy) / 0.0022))
        r, g, b = r + s, g + s, b + s
    # facet seams: darken a thin band along the vertical centerline and the
    # horizontal waist so the four faces read as cut crystal
    if abs(x / S - 0.5) < 1.5 / S:
        r, g, b = r * SEAM_DARK, g * SEAM_DARK, b * SEAM_DARK
    if abs(u - 0.52) < 1.5 / S and x / S > 0.28 and x / S < 0.72:
        r, g, b = r * SEAM_DARK, g * SEAM_DARK, b * SEAM_DARK
    return (min(255, r), min(255, g), min(255, b))


def fill_tri(buf, S, p0, p1, p2, facet):
    """Scanline-fill a triangle with per-pixel shading."""
    ys = sorted((p0[1], p1[1], p2[1]))
    y_start, y_end = max(0, int(ys[0])), min(S - 1, int(ys[-1]))
    for y in range(y_start, y_end + 1):
        xs = []
        for a, b in ((p0, p1), (p1, p2), (p2, p0)):
            ay, by = a[1], b[1]
            if ay == by:
                continue
            lo, hi = min(ay, by), max(ay, by)
            if lo <= y < hi or y == ys[-1]:
                t = (y - ay) / (by - ay)
                xs.append(a[0] + t * (b[0] - a[0]))
        if len(xs) < 2:
            continue
        x_start, x_end = int(round(min(xs))), int(round(max(xs)))
        x_start, x_end = max(0, x_start), min(S - 1, x_end)
        row = y * S
        for x in range(x_start, x_end + 1):
            o = (row + x) * 4
            r, g, b = shade_pixel(S, x, y, facet)
            buf[o] = int(r)
            buf[o + 1] = int(g)
            buf[o + 2] = int(b)
            buf[o + 3] = 255


def write_png(path, S, buf):
    raw = b''
    for y in range(S):
        raw += b'\x00' + bytes(buf[y * S * 4:(y + 1) * S * 4])
    def chunk(t, data):
        c = t + data
        return struct.pack('>I', len(data)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)
    png = b'\x89PNG\r\n\x1a\n'
    png += chunk(b'IHDR', struct.pack('>IIBBBBB', S, S, 8, 6, 0, 0, 0))
    png += chunk(b'IDAT', zlib.compress(raw, 9))
    png += chunk(b'IEND', b'')
    open(path, 'wb').write(png)


def box_downsample(buf, S, size):
    """Average an SS=S/size supersampled buffer down to `size`."""
    ss = S // size
    out = bytearray(4 * size * size)
    for y in range(size):
        for x in range(size):
            r = g = b = a = 0
            for sy in range(ss):
                for sx in range(ss):
                    o = ((y * ss + sy) * S + x * ss + sx) * 4
                    pa = buf[o + 3]
                    r += buf[o] * pa
                    g += buf[o + 1] * pa
                    b += buf[o + 2] * pa
                    a += pa
            n = ss * ss
            oo = (y * size + x) * 4
            if a > 0:
                out[oo] = r // a
                out[oo + 1] = g // a
                out[oo + 2] = b // a
            out[oo + 3] = a // n
    return out


def render_drawn(size):
    """v2 fallback: the hand-shaded octahedron at `size` (4x supersampled)."""
    SS = 4
    big = size * SS
    buf = bytearray(4 * big * big)
    top = (0.5, 0.025)
    left = (0.06, 0.52)
    right = (0.94, 0.52)
    bottom = (0.5, 0.985)
    def P(u):
        return (u[0] * big, u[1] * big)
    fill_tri(buf, big, P(top), P(left), P((0.5, 0.52)), 0)     # TL
    fill_tri(buf, big, P(top), P((0.5, 0.52)), P(right), 1)    # TR
    fill_tri(buf, big, P(left), P(bottom), P((0.5, 0.52)), 2)  # BL
    fill_tri(buf, big, P((0.5, 0.52)), P(bottom), P(right), 3) # BR
    return box_downsample(buf, big, size)


# ---------------------------------------------------------------------------
# v3: the AUTHENTIC plumbob — the real mesh + texture from game data,
# rasterized with the PLUMB-01-decoded native lighting laws.
# ---------------------------------------------------------------------------

def far_entry(far_path, want_lower):
    """Return one file's bytes from a FAR1 archive by exact name (lowered)."""
    data = open(far_path, "rb").read()
    if data[:8] != b"FAR!byAZ":
        raise ValueError("not a FAR1 archive: " + far_path)
    man = struct.unpack("<I", data[12:16])[0]
    num = struct.unpack("<I", data[man:man + 4])[0]
    off = man + 4
    for _ in range(num):
        dlen, dlen2, doff = struct.unpack("<III", data[off:off + 12])
        nlen = struct.unpack("<I", data[off + 12:off + 16])[0]
        name = data[off + 16:off + 16 + nlen].decode("latin-1").rstrip("\x00")
        if name.lower().replace("\\", "/") == want_lower:
            return data[doff:doff + dlen]
        off += 16 + nlen
    raise KeyError(want_lower + " not in " + far_path)


def find_far(gamedata, rel):
    """Resolve gamedata/<rel> case-insensitively (the install is French/upper-mixed)."""
    p = os.path.join(gamedata, *rel.split("/"))
    if os.path.isfile(p):
        return p
    cur = [gamedata]
    for part in rel.split("/"):
        nxt = []
        for d in cur:
            try:
                for entry in os.listdir(d):
                    if entry.lower() == part.lower():
                        nxt.append(os.path.join(d, entry))
            except OSError:
                pass
        cur = nxt
        if not cur:
            return None
    return cur[0] if cur and os.path.isfile(cur[0]) else None


def parse_bmf(blob):
    """Parse a TS1 .bmf mesh (Mesh.Read, bcf=true path; X/nX negated)."""
    pos = 0
    def pascal():
        nonlocal pos
        n = blob[pos]; pos += 1
        s = blob[pos:pos + n].decode("latin-1"); pos += n
        return s
    def i32():
        nonlocal pos
        v = struct.unpack_from("<i", blob, pos)[0]; pos += 4; return v
    def f32():
        nonlocal pos
        v = struct.unpack_from("<f", blob, pos)[0]; pos += 4; return v
    pascal(); pascal()  # skin name, texture name
    bone_count = i32()
    for _ in range(bone_count):
        pascal()
    face_count = i32()
    idx = [i32() for _ in range(face_count * 3)]
    binding_count = i32()
    for _ in range(binding_count):
        for _ in range(5):
            i32()
    rvc = i32()
    uvs = [(f32(), f32()) for _ in range(rvc)]
    bvc = i32()
    for _ in range(bvc):
        i32(); i32()
    rvc2 = i32()
    verts, norms = [], []
    for _ in range(rvc2):
        verts.append((-f32(), f32(), f32()))
        norms.append((-f32(), f32(), f32()))
    return {"idx": idx, "uvs": uvs, "verts": verts, "norms": norms}


def parse_bmp(blob):
    """Minimal BMP reader (8-bit paletted or 24-bit) -> (w, h, {(x,y):(r,g,b)})."""
    off = struct.unpack_from("<I", blob, 10)[0]
    w = struct.unpack_from("<i", blob, 18)[0]
    h = struct.unpack_from("<i", blob, 22)[0]
    bpp = struct.unpack_from("<H", blob, 28)[0]
    px = {}
    if bpp == 8:
        pal_off = 54
        pal = []
        for i in range(256):
            b, g, r = blob[pal_off + i * 4:pal_off + i * 4 + 3]
            pal.append((r, g, b))
        row = ((w + 3) // 4) * 4
        for y in range(abs(h)):
            yy = y if h > 0 else abs(h) - 1 - y
            for x in range(w):
                px[(x, yy)] = pal[blob[off + y * row + x]]
    elif bpp == 24:
        row = ((w * 3 + 3) // 4) * 4
        for y in range(abs(h)):
            yy = y if h > 0 else abs(h) - 1 - y
            for x in range(w):
                b, g, r = blob[off + y * row + x * 3:off + y * row + x * 3 + 3]
                px[(x, yy)] = (r, g, b)
    else:
        raise ValueError("unsupported bmp depth %d" % bpp)
    return w, h, px


# the decoded native laws (TS1PlumbBob.cs, PLUMB-01)
def mood_color(mood):
    value = max(-1.0, min(1.0, 1.2 * mood / 100.0))
    if value < 0:
        remaining = 1 - value * value
        return (1.0, remaining, remaining - (0.4 + value) if value > -0.4 else remaining)
    # AUD-18-E: native subtraction form (1−v)−(0.4−v), not a literal 0.6.
    return (1 - value, 1.0, (1 - value) - (0.4 - value) if value < 0.4 else 1 - value)


def _rot_x(a):
    c, s = math.cos(a), math.sin(a)
    return ((1, 0, 0), (0, c, -s), (0, s, c))
def _rot_y(a):
    c, s = math.cos(a), math.sin(a)
    return ((c, 0, s), (0, 1, 0), (-s, 0, c))
def _mat_mul(a, b):
    return tuple(tuple(sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)) for i in range(3))
def _mat_vec(m, v):
    return tuple(sum(m[i][k] * v[k] for k in range(3)) for i in range(3))
def _v_dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def _v_norm(v):
    l = math.sqrt(_v_dot(v, v)) or 1.0
    return (v[0] / l, v[1] / l, v[2] / l)


def _light_direction():
    # front camera: Invert(view)=I, Forward=(0,0,-1); native light follows the
    # camera ray through RotationTf(Y,80deg)*RotationTf(X,30deg) — in XNA's
    # row-vector form that is rotY(-80) applied FIRST, then rotX(30)
    # (TS1PlumbBob.LightDirection; AUD-18-E numeric mirror: 0.0 error).
    m = _mat_mul(_rot_x(math.radians(30)), _rot_y(math.radians(-80)))
    return _v_norm(_mat_vec(m, (0.0, 0.0, -1.0)))


def _facet_light(color, world_normal, light):
    diffuse = max(0.0, _v_dot(_v_norm(world_normal), (-light[0], -light[1], -light[2])))
    return tuple(min(1.0, 0.05 + color[i] * (0.25 + diffuse)) for i in range(3))


def render_authentic(mesh, tw, th, tpx, size, ss=2, angle=0.0, mood=100):
    """Software-rasterize the real mesh under the native lighting laws."""
    combined = _mat_mul(_rot_y(angle), _rot_x(-math.pi / 2))  # LocalTransform minus translation
    light = _light_direction()
    color = mood_color(mood)
    tv = [_mat_vec(combined, v) for v in mesh["verts"]]
    tn = [_mat_vec(combined, n) for n in mesh["norms"]]

    xs = [v[0] for v in tv]
    ys = [v[1] for v in tv]
    minx, maxx, miny, maxy = min(xs), max(xs), min(ys), max(ys)
    span = max(maxx - minx, maxy - miny) * 1.06
    cx, cy = (minx + maxx) / 2, (miny + maxy) / 2
    S = size * ss
    scale = S / span

    def to_screen(p):
        return ((p[0] - cx) * scale + S / 2, S / 2 - (p[1] - cy) * scale)

    facets = []
    for i in range(0, len(mesh["idx"]), 3):
        a, b, c = mesh["idx"][i], mesh["idx"][i + 1], mesh["idx"][i + 2]
        n = tn[a]
        if n[2] <= 0:
            continue  # backface (viewer at +Z)
        pa, pb, pc = to_screen(tv[a]), to_screen(tv[b]), to_screen(tv[c])
        z = (tv[a][2] + tv[b][2] + tv[c][2]) / 3
        facets.append((z, pa, pb, pc, _facet_light(color, n, light), (a, b, c)))
    facets.sort(key=lambda f: f[0])

    buf = bytearray(4 * S * S)
    zbuf = [-1e9] * (S * S)
    for z, pa, pb, pc, lit, tri in facets:
        ys_l = sorted((pa[1], pb[1], pc[1]))
        y0, y1 = max(0, int(ys_l[0])), min(S - 1, int(ys_l[-1]) + 1)
        ua = (mesh["uvs"][tri[0]][0] + mesh["uvs"][tri[1]][0] + mesh["uvs"][tri[2]][0]) / 3
        va = (mesh["uvs"][tri[0]][1] + mesh["uvs"][tri[1]][1] + mesh["uvs"][tri[2]][1]) / 3
        # AUD-18-E: parenthesize BEFORE int() — the old `int(ua % 1.0) * tw`
        # truncated to 0 first and sampled pixel (0,0) for every facet.
        tx = min(tw - 1, max(0, int((ua % 1.0) * tw)))
        ty = min(th - 1, max(0, int((va % 1.0) * th)))
        tr, tg, tb = tpx.get((tx, ty), (255, 255, 255))
        for y in range(y0, y1):
            xs_ = []
            for p, q in ((pa, pb), (pb, pc), (pc, pa)):
                if p[1] == q[1]:
                    continue
                lo, hi = min(p[1], q[1]), max(p[1], q[1])
                if lo <= y < hi or y == ys_l[-1]:
                    t = (y - p[1]) / (q[1] - p[1])
                    xs_.append(p[0] + t * (q[0] - p[0]))
            if len(xs_) < 2:
                continue
            xa, xb = int(math.ceil(min(xs_))), int(math.floor(max(xs_)))
            for x in range(max(0, xa), min(S - 1, xb) + 1):
                o = (y * S + x) * 4
                if z > zbuf[y * S + x]:
                    zbuf[y * S + x] = z
                    buf[o] = min(255, int(255 * lit[0] * tr / 255))
                    buf[o + 1] = min(255, int(255 * lit[1] * tg / 255))
                    buf[o + 2] = min(255, int(255 * lit[2] * tb / 255))
                    buf[o + 3] = 255
    return buf, S


def authentic_cache(gamedata):
    """All icon sizes from ONE authentic 1024 render, cascade-downsampled."""
    anim_far = find_far(gamedata, "GameData/Animation/Animation.far")
    tex_far = find_far(gamedata, "GameData/Textures/Textures.far")
    if not anim_far or not tex_far:
        raise FileNotFoundError("plumbob mesh/texture FARs not found under " + gamedata)
    mesh_blob = None
    for name in ("accessories/xskin-head-arrow-root-arrow.bmf",
                 "xskin-head-arrow-root-arrow.bmf"):
        try:
            mesh_blob = far_entry(anim_far, name)
            break
        except KeyError:
            continue
    if mesh_blob is None:
        raise KeyError("xskin-head-arrow-ROOT-ARROW.bmf not in " + anim_far)
    tex_blob = far_entry(tex_far, "arrow.bmp")
    mesh = parse_bmf(mesh_blob)
    tw, th, tpx = parse_bmp(tex_blob)
    big, S = render_authentic(mesh, tw, th, tpx, 1024, ss=2)
    cache = {}
    cur, curS = big, S
    for s in (1024, 512, 256, 128, 64, 32, 16):
        cur = box_downsample(cur, curS, s)
        curS = s
        cache[s] = cur
    return cache


def main():
    out_icns = sys.argv[sys.argv.index('--out') + 1] if '--out' in sys.argv else 'build/NativeIcon.icns'
    gamedata = sys.argv[sys.argv.index('--gamedata') + 1] if '--gamedata' in sys.argv else None
    iconset = out_icns + '.iconset'
    os.makedirs(iconset, exist_ok=True)

    cache = None
    source = "drawn-v2"
    if gamedata and os.path.isdir(gamedata):
        try:
            cache = authentic_cache(gamedata)
            source = "authentic-mesh (PLUMB-01 laws)"
        except Exception as ex:
            print('authentic plumbob unavailable (%s); falling back to drawn v2' % ex)
    if cache is None:
        cache = {s: render_drawn(s) for s in SIZES}

    entries = [(16, 1), (16, 2), (32, 1), (32, 2), (128, 1), (128, 2),
               (256, 1), (256, 2), (512, 1), (512, 2)]
    for base, scale in entries:
        s = base * scale
        name = f'icon_{base}x{base}' + ('@2x' if scale == 2 else '') + '.png'
        write_png(os.path.join(iconset, name), s, cache[s])
    r = subprocess.run(['iconutil', '-c', 'icns', iconset, '-o', out_icns],
                       capture_output=True, text=True)
    if r.returncode != 0:
        print('iconutil failed:', r.stderr)
        sys.exit(1)
    print('wrote', out_icns, os.path.getsize(out_icns), 'bytes —', source)


if __name__ == '__main__':
    main()
