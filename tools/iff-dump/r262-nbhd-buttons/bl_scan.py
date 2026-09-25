#!/usr/bin/env python3
"""R262: scan the code section for bl/b targets landing on a given address."""
import sys
BIN = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete'
CODE_LO, CODE_HI = 0x8E90, 0x5C22E8
data = open(BIN, 'rb').read()
targets = [int(a, 16) for a in sys.argv[1:]]
hits = {t: [] for t in targets}
for pc in range(CODE_LO, CODE_HI, 4):
    w = int.from_bytes(data[pc:pc+4], 'big')
    if (w >> 26) == 18 and (w & 1) == 1:  # bl (lk=1, aa=0)
        li = w & 0x03FFFFFC
        if li & 0x02000000: li -= 0x04000000
        tgt = pc + li
        if tgt in hits:
            hits[tgt].append(pc)
for t in targets:
    print(f'target {t:08x}: ' + ' '.join(f'{p:08x}' for p in hits[t]))
