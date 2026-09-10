#!/usr/bin/env python3
"""Read-only owned FFN metrics and original runtime normalization evidence."""
from pathlib import Path
import hashlib
import runpy
import struct
import capstone

ROOT = Path(__file__).resolve().parents[3]
far = runpy.run_path(str(ROOT / "tools/iff-dump/r142/far_manifest.py"))
raw, entries = far["parse_far"](far["FAR"])
print("FFN name | eligible cells | minT2 | maxH | max(H+T2) | TSCharHeight | A H/rawT2/runtimeT2")
for entry in sorted(entries, key=lambda item: item["name"].lower()):
    if not entry["name"].lower().endswith(".ffn"):
        continue
    data = raw[entry["dataOffset"]:entry["dataOffset"] + entry["dataLen"]]
    count = struct.unpack_from("<H", data, 10)[0]
    offset = struct.unpack_from("<I", data, 20)[0]
    glyphs = [struct.unpack_from("<HBBHHbbb", data, offset + 11 * i) for i in range(count)]
    eligible = [g for g in glyphs if g[0] != 160 and g[1] and g[2]]
    top = min(0, min(g[-1] for g in eligible))
    bottom = max(0, max(g[2] + g[-1] for g in eligible))
    a = next((g for g in glyphs if g[0] == 65), None)
    am = f"{a[2]}/{a[-1]}/{a[-1]-top}" if a else "absent"
    print(f"{entry['name']} | {len(eligible)} | {top} | {max(g[2] for g in eligible)} | {bottom} | {bottom-top} | {am}")

pef = runpy.run_path(str(ROOT / "tools/iff-dump/r176/r176-expansion-category-decode.py"))
engine = pef["ENGINE"].read_bytes()
assert hashlib.sha256(engine).hexdigest() == pef["ENGINE_SHA256"]
print("\nOwned engine SHA-256:", pef["ENGINE_SHA256"])
decoder = capstone.Cs(capstone.CS_ARCH_PPC, capstone.CS_MODE_32 | capstone.CS_MODE_BIG_ENDIAN)
for name, start, end in [
    ("InitBitmapped: compute minT2 and maximum bottom, normalize runtime offsets", 0x4b4530, 0x4b47e0),
    ("TSDrawChar: runtime destination offset versus atlas source address", 0x4b3580, 0x4b36a0),
    ("GetCharDims: runtime offset applied to ink bounds", 0x4acc10, 0x4acc74),
    ("CalcRowCol: four/six-column state branches", 0x50d270, 0x50d3e0),
]:
    print("\n" + name)
    for ins in decoder.disasm(engine[start:end], start):
        print(f"{ins.address:08x}: {ins.bytes.hex()} {ins.mnemonic:10} {ins.op_str}")
