#!/usr/bin/env python3
"""Dump a cTSWin-family vtable: array of 4-byte data-relative pointers -> TVector{code,TOC}.
usage: vt.py DATA_OFF [COUNT]"""
import struct, sys
sec1 = open('/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r142/tsui/data-sec1-unpacked.bin','rb').read()
syms={}
for line in open('/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r141/symbol-index.txt'):
    p=line.split()
    if len(p)>=3:
        try: syms[int(p[0],16)]=p[2]
        except: pass
off=int(sys.argv[1],16); cnt=int(sys.argv[2]) if len(sys.argv)>2 else 0x1b0//4
for i in range(cnt):
    o=off+i*4
    if o+4>len(sec1): break
    p=struct.unpack('>I',sec1[o:o+4])[0]
    if p==0 or p>=len(sec1)-8:
        print(f'vt+{i*4:03x} [{o:#x}]: {p:#x} (null/bad)'); continue
    code,toc=struct.unpack('>II',sec1[p:p+8]); code=(code+0x8E90)
    n=syms.get(code) or syms.get(code-2)
    print(f'vt+{i*4:03x} [{o:#x}]: ->{p:#x} code {code+0x8e90:#x} {n or ""}')
