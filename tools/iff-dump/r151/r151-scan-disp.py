#!/usr/bin/env python3
"""R151: scan the engine code region for D-form loads/stores of the interest
halfword offsets (expansion topics +0x5a4..+0x5c2, base array +0x5e8..+0x5fa).
Usage: r151-scan-disp.py <binary>"""
import struct, sys

p = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete"
b = open(p, 'rb').read()
START, END = 0x1000, 0x5c22f0

# opcode -> mnemonic (D-form, disp = word & 0xffff)
OPS = {32:"lwz",33:"lwzu",34:"lbz",36:"stw",37:"stwu",38:"stb",40:"lhz",42:"lha",44:"sth",45:"sthu"}

# struct context: all halfwords in the two regions, plus a little margin
expansion = list(range(0x5a4, 0x5c4, 2))     # +0x5a4..+0x5c2
base      = list(range(0x5e0, 0x600, 2))     # +0x5e0..+0x5fe (margin around 5e8..5fa)
TARGETS = set(expansion) | set(base)

hits = {}
for off in range(START, END-4, 4):
    w = struct.unpack_from('>I', b, off)[0]
    op = w >> 26
    if op not in OPS: continue
    disp = w & 0xFFFF
    if disp in TARGETS:
        rD = (w>>21)&31; rA = (w>>16)&31
        hits.setdefault(disp, []).append((off, w, OPS[op], rD, rA))

for disp in sorted(hits):
    tag = "EXP" if disp in expansion else "BASE"
    print(f"== {tag} +{disp:#06x} : {len(hits[disp])} sites")
    for (off, w, m, rD, rA) in hits[disp]:
        print(f"  {off:#08x}: {w:08x}  {m} r{rD}, {disp:#x}(r{rA})")
