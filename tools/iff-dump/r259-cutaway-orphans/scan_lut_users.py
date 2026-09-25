#!/usr/bin/env python3
"""UI-25: find every reference to TOC slot -0x73E4 (BSS 0x7F0B8) and every
direct bl caller of BuildMaxAltsTable (0x1c6b70) / BuildRotationLookup (0x60f20).

PEF/PPC container laws reused from r143/r144 (see ../r244-cutaway-state/decode.md):
  code section starts at file 0x8e90; `lwz rX, -0x73e4(r2)` encodes d=0x8C1C, rA=2.
  bl: opcode 18, LI = sign-extended 24-bit target offset (addr + LI*4 ... actual:
  target = (addr of insn) + sign24(li)*4 when AA=0, LK=1).
Writes only scan output to stdout; read-only on the binary.
"""
import struct, sys

BIN = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete'
SYMS = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r141/symbol-index.txt'

data = open(BIN, 'rb').read()
syms = {}
for line in open(SYMS):
    p = line.split()
    if len(p) >= 3:
        try:
            syms[int(p[0], 16) - 2] = p[2]   # r145 law: index addr == file offset + 2
        except ValueError:
            pass

CODE_START = 0x8e90
CODE_END   = 0x5c22f0          # data section begins (PEF-packed) here per r244 geom decode

def sym(a):
    return syms.get(a, '')

# 1) TOC -0x73e4 users: d-field 0x8C1C with rA=2. Report opcode+rD by name for
#    common opcodes (lwz=32, lbz=34, stw=36, stb=38, lfs/stfd etc. resolved below).
OPS = {32:'lwz',33:'lwzu',34:'lbz',35:'lbzu',36:'stw',37:'stwu',38:'stb',39:'stbu',
       40:'lhz',41:'lhzu',42:'lha',44:'sth',48:'lfs',50:'lfd',52:'stfs',54:'stfd',
       20:'lwarx',21:'ldx',23:'lwzx',24:'slw',26:'cntlzw',28:'and',7:'mullw',
       10:'cmplw',11:'cmpw',6:'rlwimi',5:'rlwinm',23:'lwzx',55:'lwzux',39:'stbu',
       31:'?31-form',27:'xor',4:'tw'}
hits = []
for off in range(CODE_START, CODE_END, 4):
    w = struct.unpack_from('>I', data, off)[0]
    if (w & 0x3E000000) and False:
        pass
    op = w >> 26
    rA = (w >> 16) & 31
    d  = w & 0xFFFF
    if rA == 2 and d == 0x8C1C and op in (32,34,36,38,40,42,44,50,48,52,54):
        rD = (w >> 21) & 31
        hits.append((off, w, op, rD))
print(f'== TOC -0x73E4 (BSS 0x7F0B8) users: {len(hits)} ==')
for off, w, op, rD in hits:
    print(f'  {off:08x}: {w:08x}  {OPS.get(op,f"op{op}")} r{rD}, -0x73e4(r2)   {sym(off)}')

# 2) bl callers of the two builders
def bl_callers(target):
    out = []
    for off in range(CODE_START, CODE_END, 4):
        w = struct.unpack_from('>I', data, off)[0]
        if (w >> 26) == 18 and (w & 3) == 1:          # bl, AA=0
            li = w & 0x03FFFFFC
            if li & 0x02000000:
                li -= 0x04000000
            tgt = off + li
            if tgt == target:
                out.append((off, sym(off)))
    return out

for name, addr in (('BuildMaxAltsTable__11HouseViewerFv', 0x1c6b70),
                   ('BuildRotationLookup__7CTilePtFv', 0x60f20)):
    callers = bl_callers(addr)
    print(f'== bl callers of {name} (0x{addr:x}): {len(callers)} ==')
    for off, s in callers:
        print(f'  {off:08x}   {s}')
