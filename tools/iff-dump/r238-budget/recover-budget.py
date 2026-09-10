#!/usr/bin/env python3
"""Read the owner's original engine/resources; emit Budget layout metadata only."""

from pathlib import Path
import hashlib
import runpy
import struct
import capstone

ROOT = Path(__file__).resolve().parents[3]
pef = runpy.run_path(str(ROOT / "tools/iff-dump/r176/r176-expansion-category-decode.py"))
engine = pef["ENGINE"].read_bytes()
assert hashlib.sha256(engine).hexdigest() == pef["ENGINE_SHA256"]
section = pef["unpack_pef_data"](engine[pef["DATA_CONTAINER_OFFSET"]:
    pef["DATA_CONTAINER_OFFSET"] + pef["DATA_CONTAINER_LENGTH"]])
word = pef["word"]

font_table = word(section, 0x8000 - 0x6AE4)
indent_table = word(section, 0x8000 - 0x6AE0)
fonts = [(word(section, font_table + i * 4) - 0x92900) // 4 for i in range(20)]
indents = [word(section, indent_table + i * 4) for i in range(20)]
assert fonts == [16, 10, 12, 10, 10, 10, 12, 10, 10, 10, 10, 9, 9, 10, 10, 12, 12, 10, 10, 10]
assert indents == [0, 0, 0, 1, 1, 0, 0, 1, 1, 1, 1, 2, 2, 1, 0, 0, 0, 0, 0, 0]
far = runpy.run_path(str(ROOT / "tools/iff-dump/r142/far_manifest.py"))
raw, entries = far["parse_far"](far["FAR"])
heights = {}
print("Owned engine SHA-256:", pef["ENGINE_SHA256"])
print(f"Font table DATA {font_table:#x}; indent table DATA {indent_table:#x}")
for index in sorted(set(fonts) | {20}):
    name = f"fonts\\variablesans_{index:02}.ffn"
    entry = next(entry for entry in entries if entry["name"].lower() == name)
    data = raw[entry["dataOffset"]:entry["dataOffset"] + entry["dataLen"]]
    count = struct.unpack_from("<H", data, 10)[0]
    offset = struct.unpack_from("<I", data, 20)[0]
    glyphs = [struct.unpack_from("<HBBHHbbb", data, offset + 11 * i) for i in range(count)]
    glyphs = [g for g in glyphs if g[0] != 160 and g[1] and g[2]]
    bottom = max(g[2] + g[-1] for g in glyphs)
    top = min(0, min(g[-1] for g in glyphs))
    heights[index] = bottom - top
    print(f"font[{index}]: TSCharHeight={bottom}-{top}={bottom-top}; sha256={hashlib.sha256(data).hexdigest()}")

labels = ["Budget", "Today / 3 Days", "Income", "Job", "Miscellaneous", "spacer",
          "Expenses", "Bills Paid", "Food", "Repair/Cleaning/Gardening", "New Purchases",
          "Household Items", "Architecture/Landscaping", "Miscellaneous Expenses", "spacer",
          "Cash Flow", "spacer", "Household Account Total", "Household Net Worth", "Days Since Move-In"]
y = 25
for i in range(20):
    h = heights[fonts[i]]
    print(f"row {i:02}: y={y}, h={h}, font={fonts[i]}, indent={12*indents[i]}, centered={i in (0,17,18,19)}; {labels[i]}")
    y += h
print(f"Final window height={y + 100}; OK y={y + (100-52)//2}")

decoder = capstone.Cs(capstone.CS_ARCH_PPC, capstone.CS_MODE_32 | capstone.CS_MODE_BIG_ENDIAN)
print("\nIndependent Budget row-method decode (raw file offsets):")
for start, end in [(0x264EA0, 0x264EF4), (0x264F30, 0x265020),
                   (0x265320, 0x265330), (0x265360, 0x2653D0), (0x265410, 0x265848)]:
    for instruction in decoder.disasm(engine[start:end], start):
        print(f"{instruction.address:08x}: {instruction.bytes.hex()} {instruction.mnemonic:10} {instruction.op_str}")
