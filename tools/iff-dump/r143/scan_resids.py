#!/usr/bin/env python3
"""R143: scan code section for `addi rD, r0, imm` / `addis`-free immediate loads of
Res_Nbhd resource IDs, plus lis/addi pairs, to find layout-init sites."""
import struct, sys

BIN = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/../../game-data/The Sims/The Sims Complete'
data = open(BIN.replace('tools/iff-dump/../../', ''), 'rb').read()

IDS = set(range(5000, 5050)) | set(range(5100, 5110)) | set(range(5200, 5225)) | \
      set(range(5300, 5320)) | set(range(5350, 5351)) | set(range(5400, 5406)) | \
      set(range(5420, 5437)) | set(range(6000, 6001)) | set(range(6302, 6312)) | set(range(6330, 6339))

CODE_START, CODE_END = 0x8E90, 0x5C22E8

hits = []
for off in range(CODE_START & ~3, CODE_END, 4):
    w = struct.unpack_from('>I', data, off)[0]
    op = w >> 26
    if op == 14:  # addi
        rA = (w >> 16) & 0x1F
        imm = w & 0xFFFF
        if rA == 0 and imm in IDS:
            rD = (w >> 21) & 0x1F
            hits.append((off, 'addi', rD, rA, imm))
    elif op == 15:  # addis
        rA = (w >> 16) & 0x1F
        imm = w & 0xFFFF
        # addis rD,0,1 => rD = 0x10000 (unlikely res); skip unless combined later
        if rA == 0 and imm == 1:
            pass

for off, mn, rD, rA, imm in hits:
    print(f'{off:08x}: {mn} r{rD}, r{rA}, {imm}')
print(f'total {len(hits)}')
