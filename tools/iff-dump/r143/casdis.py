#!/usr/bin/env python3
"""R143: capstone PPC disassembler with TOC-slot annotation + reloc-kind aware.
Memory map (validated this round):
  - code section: virtual 0, file container 0x8E90..0x5C22E8 -> file = virt + 0x8E90
  - data section: virtual 0 == data offset 0, unpacked image 0x7BF80 bytes (BSS beyond)
  - reloc kinds (r142 reloc-kinds.json): 'C' word = code pointer (store = code-virt),
    'D' = data pointer. TOC slot at r2+off -> data offset 0x8000+off.
Usage: casdis.py 0xSTART 0xEND [out.txt]
"""
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
            extra = ''
            if fo <= len(raw) - 4:
                w = struct.unpack('>I', raw[fo:fo + 4])[0]
                extra = f' firstword={w}({w:#x})'
            return v, f'CODE file {fo:#x}{extra}'
        elif k == 'D':
            if v < SEC1_LEN:
                s = sec1[v:v + 40]
                printable = sum(1 for c in s if 32 <= c < 127)
                extra = f' str="{s.split(bytes([0]))[0][:32].decode("latin1","replace")}"' if printable > 20 else ''
                return v, f'DATA off {v:#x}{extra}'
            return v, f'BSS off {v:#x}'
        return v, f'kind={k}'
    return None, None


def dis(start, end):
    out = []
    for insn in md.disasm(raw[start:end], start):
        note = ''
        m, ops = insn.mnemonic, insn.op_str
        if m in ('bl', 'b') and ops.startswith('0x'):
            tgt = int(ops, 16)
            n = syms.get(tgt) or syms.get(tgt - 2) or syms.get(tgt + 2)
            if n:
                note = f'   ; {n}'
        if m == 'lwz' and ops.endswith('(r2)'):
            try:
                off = int(ops.split(',')[1].strip().replace('(r2)', ''), 0)
                v, desc = toc(off)
                if v is not None:
                    note += f'   ; TOC[{off:+#x}]->{desc}'
            except ValueError:
                pass
        out.append(f'{insn.address:08x}: {insn.bytes.hex()}  {m:8s} {ops}{note}')
    return '\n'.join(out)


if __name__ == '__main__':
    start = int(sys.argv[1], 16)
    end = int(sys.argv[2], 16)
    txt = dis(start, end)
    if len(sys.argv) > 3:
        open(sys.argv[3], 'w').write(txt + '\n')
        print(f'wrote {sys.argv[3]} ({txt.count(chr(10))+1} lines)')
    else:
        print(txt)
