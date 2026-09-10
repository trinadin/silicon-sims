#!/usr/bin/env python3
"""Reproduce the native CAS editor geometry evidence from the owner's binary."""
from pathlib import Path
import hashlib
import capstone

repo = Path(__file__).resolve().parents[3]
raw = (repo / 'game-data/The Sims/The Sims Complete').read_bytes()
assert hashlib.sha256(raw).hexdigest() == '33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f'
decoder = capstone.Cs(capstone.CS_ARCH_PPC, capstone.CS_MODE_32 | capstone.CS_MODE_BIG_ENDIAN)
regions = [
    ('SetArea text rectangle', 0x5330e0, 0x533114),
    ('Init rectangle, visible rows, wrap width and y centering', 0x534f48, 0x535098),
    ('Rebuild empty-row origin', 0x530774, 0x5307a0),
    ('Rebuild row origin', 0x530a60, 0x530a90),
    ('Scroll row offset', 0x5317b8, 0x5317d4),
    ('Paint row placement', 0x5346f8, 0x53472c),
    ('Typed Return and control-character rules', 0x5336e8, 0x533764),
]
for label, start, end in regions:
    print('\n' + label)
    for instruction in decoder.disasm(raw[start:end], start):
        print(f'{instruction.address:08x}: {instruction.mnemonic:8} {instruction.op_str}')
