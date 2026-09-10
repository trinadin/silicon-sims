#!/usr/bin/env python3
"""Independent scalar/hash fixtures from owner FFNs; no font pixels exported."""
from pathlib import Path
import hashlib
import runpy
import struct

ROOT = Path(__file__).resolve().parents[3]
far = runpy.run_path(str(ROOT / "tools/iff-dump/r142/far_manifest.py"))
raw, entries = far["parse_far"](far["FAR"])
print("// C# fixtures: member, char code, runtime top offset, destination y, SHA256 of 64x80 alpha bytes")
for entry in sorted(entries, key=lambda item: item["name"].lower()):
    name = entry["name"]
    if not name.lower().endswith(".ffn") or name.count("\\") != 1:
        continue
    data = raw[entry["dataOffset"]:entry["dataOffset"] + entry["dataLen"]]
    count = struct.unpack_from("<H", data, 10)[0]
    offset = struct.unpack_from("<I", data, 20)[0]
    shape = struct.unpack_from("<I", data, 28)[0]
    atlas_width = struct.unpack_from("<H", data, shape + 4)[0]
    glyphs = [struct.unpack_from("<HBBHHbbb", data, offset + 11 * i) for i in range(count)]
    eligible = [g for g in glyphs if g[0] != 160 and g[1] > 1 and g[2]]
    top = min(0, min(g[-1] for g in eligible))
    for glyph in [next(g for g in eligible if g[0] == 65), min(eligible, key=lambda g: g[-1])]:
        char, w, h, u, v, _, _, t2 = glyph
        raster = bytearray(64 * 80)
        for y in range(h):
            for x in range(w):
                source = (v + y) * atlas_width + u + x
                value = data[shape + 16 + source // 2]
                nibble = (value >> 4) if source % 2 == 0 else (value & 15)
                raster[(4 + t2 - top + y) * 64 + 4 + x] = nibble * 17
        print(f'(@"{name}", {char}, {-top}, {4+t2-top}, "{hashlib.sha256(raster).hexdigest()}"),')
