#!/usr/bin/env python3
"""R145: capdis with symbol resolution for both file addr and file+2 (symbol
index keys are linker addrs = file offset + 2, so plain capdis misses most)."""
import sys
import capstone

BIN = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete'
data = open(BIN, 'rb').read()
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


def note(tgt):
    if tgt in syms:
        return f'   ; {syms[tgt]}'
    if tgt + 2 in syms:
        return f'   ; {syms[tgt + 2]}'
    return ''


def dis(start, end):
    out = []
    for insn in md.disasm(data[start:end], start):
        n = ''
        if insn.mnemonic in ('bl', 'b') and insn.op_str.startswith('0x'):
            n = note(int(insn.op_str, 16))
        out.append(f'{insn.address:08x}: {insn.bytes.hex()}  {insn.mnemonic:8s} {insn.op_str}{n}')
    return '\n'.join(out)


if __name__ == '__main__':
    print(dis(int(sys.argv[1], 16), int(sys.argv[2], 16)))
