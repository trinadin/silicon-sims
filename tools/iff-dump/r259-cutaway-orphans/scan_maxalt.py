#!/usr/bin/env python3
"""UI-25 follow-ups: (1) find readers/writers of viewer+0x1EC / +0x1F0
(the two 128-byte max-alt arrays built by BuildMaxAltsTable);
(2) verify BuildRotationLookup's output is a pure function of the loop
constants by simulating it against the port's RotMap/InvRotMap laws."""
import struct

BIN = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete'
SYMS = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r141/symbol-index.txt'
data = open(BIN, 'rb').read()
syms = {}
for line in open(SYMS):
    p = line.split()
    if len(p) >= 3:
        try: syms[int(p[0], 16) - 2] = p[2]
        except ValueError: pass
CODE_START, CODE_END = 0x8e90, 0x5c22f0
OPS = {32:'lwz',33:'lwzu',34:'lbz',35:'lbzu',36:'stw',37:'stwu',38:'stb',39:'stbu'}
hits = []
for off in range(CODE_START, CODE_END, 4):
    w = struct.unpack_from('>I', data, off)[0]
    op, rA, d = w >> 26, (w >> 16) & 31, w & 0xFFFF
    if op in (32, 34, 36, 38) and d in (0x1EC, 0x1F0):
        hits.append((off, w, OPS[op], (w >> 21) & 31, d))
print(f'== viewer+0x1EC/0x1F0 accesses: {len(hits)} ==')
for off, w, mn, rD, d in hits:
    print(f'  {off:08x}: {w:08x}  {mn} r{rD}, 0x{d:x}(rX)   {syms.get(off, "")}')
