#!/usr/bin/env python3
"""Scan code section for lwz rD, imm16(r2) TOC loads with a given offset.
usage: scan_toc.py 0xAF6C   (imm16 as hex, signed offsets given as e.g. -0x5094 -> pass 0xAF6C)
Prints file address + register.
"""
import struct, sys
BIN = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete'
raw = open(BIN,'rb').read()
imm = int(sys.argv[1],16) & 0xFFFF
start, end = 0x8E90, 0x5C22E8
hits=[]
for off in range(start, end, 4):
    w = struct.unpack('>I', raw[off:off+4])[0]
    if (w>>26)==32 and ((w>>16)&0x1f)==2 and (w&0xFFFF)==imm:
        rD=(w>>21)&0x1f
        hits.append((off,rD))
for a,r in hits:
    print(f'{a:#x}  lwz r{r}, {imm:#04x}(r2)')
print(f'{len(hits)} hits')
