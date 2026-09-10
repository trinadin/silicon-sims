#!/usr/bin/env python3
"""find_li.py IMM16 [IMM16...] - find `li rD, imm` instructions in code"""
import struct, sys
raw=open('/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete','rb').read()
imms={int(x,16) for x in sys.argv[1:]}
start,end=0x8E90,0x5C22E8
for off in range(start,end,4):
    w=struct.unpack('>I',raw[off:off+4])[0]
    if (w>>26)==14:  # addi rD,rA,imm  (li rD,imm == addi rD,r0,imm)
        imm=w&0xFFFF
        if imm>=0x8000: imm-=0x10000
        if imm in imms and ((w>>16)&0x1f)==0 and ((w>>21)&0x1f)==3:
            print(f'{off:#x}: li r3, {imm} (0x{imm&0xFFFF:x})')
