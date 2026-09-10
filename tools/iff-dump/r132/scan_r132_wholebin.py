#!/usr/bin/env python3
"""R132: whole-binary scan for the ratings-bar RT ids (kHouseBars 4800 etc.)
in every immediate encoding (addi/li with rA=0, lis+ori pairs), to find which
code region builds those gadgets."""
import struct, os

BIN = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                   '../../../game-data/The Sims/The Sims Complete')
data = open(BIN, 'rb').read()

IDS = {4800: 'kHouseBars', 4801: 'kJobBars', 4802: 'kRelBars',
       4822: 'kFameSubpanelBars', 4823: 'kFameSubBarsOff', 4900: 'kHouseSubpanelBars',
       4901: 'kJobSubpanelBars', 4919: 'kHouseSubBarsOff', 4920: 'kJobSubBarsOff'}

hits = []
for off in range(0x1000, len(data) - 4, 4):
    w = int.from_bytes(data[off:off+4], 'big')
    imm = w & 0xFFFF
    # addi rD, r0, imm (also li)
    if (w >> 26) == 14 and ((w >> 16) & 31) == 0 and imm in IDS:
        hits.append((off, 'li r%d,%d (%s)' % ((w >> 21) & 31, imm, IDS[imm])))
    # ori rD, rA, imm (often after lis)
    elif (w >> 26) == 24 and imm in IDS and ((w >> 16) & 31) == 0 and ((w >> 21) & 31) != 0:
        hits.append((off, 'ori r%d,r0,%d (%s)' % ((w >> 21) & 31, imm, IDS[imm])))

print('total', len(hits))
for off, what in hits:
    print('0x%06x  %s' % (off, what))
