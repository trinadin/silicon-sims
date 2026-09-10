#!/usr/bin/env python3
"""R143 tolerant disassembler: skips undecodable words, annotates TOC slots + symbols.
Usage: rdump.py 0xSTART 0xEND [out.txt]"""
import sys
import capstone
import struct

BIN = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete'
DATA = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r142/tsui/data-sec1-unpacked.bin'
KINDS = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r142/tsui/reloc-kinds.json'
import json
raw = open(BIN, 'rb').read()
sec1 = open(DATA, 'rb').read()
kinds = json.load(open(KINDS))
SEC1_LEN = len(sec1)
md = capstone.Cs(capstone.CS_ARCH_PPC, capstone.CS_MODE_32 | capstone.CS_MODE_BIG_ENDIAN)
md.detail = False
syms = {}
for line in open('/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r141/symbol-index.txt'):
    p = line.split()
    if len(p) >= 3:
        try:
            syms[int(p[0], 16)] = p[2]
        except ValueError:
            pass


def toc(off):
    o = 0x8000 + off
    if 0 <= o <= SEC1_LEN - 4:
        v = struct.unpack('>I', sec1[o:o + 4])[0]
        k = kinds.get(str(o // 4), '')
        if k == 'C':
            fo = v + 0x8E90
            return f'CODEPTR file {fo:#x}'
        elif k == 'D':
            if v < SEC1_LEN:
                s = sec1[v:v + 40]
                printable = sum(1 for c in s if 32 <= c < 127)
                extra = f' "{s.split(bytes([0]))[0][:32].decode("latin1","replace")}"' if printable > 20 else ''
                return f'DATA off {v:#x}{extra}'
            return f'BSS off {v:#x}'
        return f'{v:#x} kind={k}'
    return None


def dis(start, end):
    out = []
    off = start
    while off < end:
        got = list(md.disasm(raw[off:min(off + 4, end)], off, count=1))
        if got:
            i = got[0]
            note = ''
            if i.mnemonic in ('bl', 'b') and i.op_str.startswith('0x'):
                t = int(i.op_str, 16)
                n = syms.get(t) or syms.get(t - 2) or syms.get(t + 2)
                if n:
                    note = f'   ; {n}'
            if i.mnemonic == 'lwz' and i.op_str.endswith('(r2)'):
                try:
                    o = int(i.op_str.split(',')[1].strip().replace('(r2)', ''), 0)
                    d = toc(o)
                    if d:
                        note += f'   ; TOC[{o:+#x}]->{d}'
                except ValueError:
                    pass
            out.append(f'{i.address:08x}: {i.bytes.hex()}  {i.mnemonic:8s} {i.op_str}{note}')
            off += i.size
        else:
            w = int.from_bytes(raw[off:off + 4], 'big')
            out.append(f'{off:08x}: {w:08x}  .word')
            off += 4
    return '\n'.join(out)


if __name__ == '__main__':
    s, e = int(sys.argv[1], 16), int(sys.argv[2], 16)
    t = dis(s, e)
    if len(sys.argv) > 3:
        open(sys.argv[3], 'w').write(t + '\n')
        print(f'wrote {sys.argv[3]} ({t.count(chr(10))+1} lines)')
    else:
        print(t)
