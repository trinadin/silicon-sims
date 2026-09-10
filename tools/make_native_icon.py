#!/usr/bin/env python3
"""R156: generate the NATIVE app icon (the plumbob) for the macOS bundle.

The original game ships no standalone high-res plumbob asset (the Complete
Collection boot art is the Makin' Magic purple variant; the 70x52 logo
stamps are RLE8 wordmarks), so the icon is drawn programmatically as the
game's own symbol — the green mood-crystal octahedron — at every iconset
size with 4x supersampling (crisp at 16px through 1024px). No proprietary
bytes are read or committed; the .icns is generated at PACK time by
packmac.sh and lands in build/NativeIcon.icns (gitignored).

Usage: python3 tools/make_native_icon.py [--out build/NativeIcon.icns]
"""
import os
import struct
import subprocess
import sys
import zlib

# classic plumbob facet greens (light source upper-left)
FACET_TL = (0x8B, 0xE0, 0x4F)   # light
FACET_TR = (0x4E, 0xBE, 0x3B)   # mid
FACET_BL = (0x3E, 0xA1, 0x2F)   # dark-mid
FACET_BR = (0x2A, 0x7E, 0x22)   # dark

SIZES = [16, 32, 64, 128, 256, 512, 1024]


def fill_tri(buf, S, p0, p1, p2, color):
    """Scanline-fill a triangle into an RGBA byte buffer (4*S*S)."""
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
            buf[o] = color[0]
            buf[o + 1] = color[1]
            buf[o + 2] = color[2]
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
    fill_tri(buf, big, P(top), P(left), P((0.5, 0.52)), FACET_TL)
    fill_tri(buf, big, P(top), P((0.5, 0.52)), P(right), FACET_TR)
    fill_tri(buf, big, P(left), P(bottom), P((0.5, 0.52)), FACET_BL)
    fill_tri(buf, big, P((0.5, 0.52)), P(bottom), P(right), FACET_BR)
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
