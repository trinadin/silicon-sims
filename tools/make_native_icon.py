#!/usr/bin/env python3
"""R156: generate the NATIVE app icon (the plumbob) for the macOS bundle.

The original game ships no standalone plumbob asset (the discs' boot art is
wordmark-only; the in-game crystal is a 3D mesh), so the icon is drawn
programmatically as the game's own symbol. v2 (2026-10-06, user directive
"restore the original app icon"): the flat-facet v1 read as synthetic — this
version shades the octahedron the way the classic artwork did: a vertical
green gradient, four facet lights from the upper left, a specular streak on
the upper-left face, and darkened facet seams. Still no proprietary bytes
are read or committed; the .icns is generated at PACK time by packmac.sh
into build/NativeIcon.icns (gitignored).

Usage: python3 tools/make_native_icon.py [--out build/NativeIcon.icns]
"""
import os
import struct
import subprocess
import sys
import zlib

SIZES = [16, 32, 64, 128, 256, 512, 1024]

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


def render(size):
    """Render the plumbob at `size` via 4x supersampling."""
    SS = 4
    big = size * SS
    buf = bytearray(4 * big * big)
    # geometry in unit space (elongated octahedron)
    top = (0.5, 0.025)
    left = (0.06, 0.52)
    right = (0.94, 0.52)
    bottom = (0.5, 0.985)
    def P(u):
        return (u[0] * big, u[1] * big)
    fill_tri(buf, big, P(top), P(left), P((0.5, 0.52)), 0)   # TL
    fill_tri(buf, big, P(top), P((0.5, 0.52)), P(right), 1)  # TR
    fill_tri(buf, big, P(left), P(bottom), P((0.5, 0.52)), 2)  # BL
    fill_tri(buf, big, P((0.5, 0.52)), P(bottom), P(right), 3)  # BR
    # downsample (box) with alpha-weighted color
    out = bytearray(4 * size * size)
    for y in range(size):
        for x in range(size):
            r = g = b = a = 0
            for sy in range(SS):
                for sx in range(SS):
                    o = ((y * SS + sy) * big + (x * SS + sx)) * 4
                    pa = buf[o + 3]
                    r += buf[o] * pa
                    g += buf[o + 1] * pa
                    b += buf[o + 2] * pa
                    a += pa
            n = SS * SS
            oo = (y * size + x) * 4
            if a > 0:
                out[oo] = r // a
                out[oo + 1] = g // a
                out[oo + 2] = b // a
            out[oo + 3] = a // n
    return out


def main():
    out_icns = sys.argv[sys.argv.index('--out') + 1] if '--out' in sys.argv else 'build/NativeIcon.icns'
    iconset = out_icns + '.iconset'
    os.makedirs(iconset, exist_ok=True)
    cache = {}
    for s in SIZES:
        cache[s] = render(s)
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
    print('wrote', out_icns, os.path.getsize(out_icns), 'bytes')


if __name__ == '__main__':
    main()
