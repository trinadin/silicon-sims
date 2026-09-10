#!/usr/bin/env python3
"""find_bl.py TARGET_HEX [TARGET_HEX...] - find direct bl calls to target(s) in code section"""
import struct, sys
BIN = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete'
raw = open(BIN,'rb').read()
tgts = [int(t,16) for t in sys.argv[1:]]
start, end = 0x8E90, 0x5C22E8
for off in range(start, end, 4):
    w = struct.unpack('>I', raw[off:off+4])[0]
    if (w >> 26) == 18:  # b/bl
        li = w & 0x03FFFFFC
        if li & 0x02000000: li -= 0x04000000
        tgt = (off + li) & 0xFFFFFFFF
        if tgt in tgts:
            aa = (w>>1)&1; lk=(w)&1
            print(f'{off:#x}: b{"l" if lk else ""}{"a" if aa else ""} {tgt:#x}')
