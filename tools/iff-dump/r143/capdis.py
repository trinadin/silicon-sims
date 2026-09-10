#!/usr/bin/env python3
"""R142: capstone-based PPC disassembler helper (ground truth over ppc_decode.py,
which mis-orders X-form `or` operands and swaps some branch senses)."""
import sys
import capstone

BIN = '/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete'
data = open(BIN, 'rb').read()
md = capstone.Cs(capstone.CS_ARCH_PPC, capstone.CS_MODE_32 | capstone.CS_MODE_BIG_ENDIAN)
md.detail = False

# symbol lookup from r141 index
syms = {}
for line in open('/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r141/symbol-index.txt'):
    p = line.split()
    if len(p) >= 3:
        try:
            syms[int(p[0], 16)] = p[2]
        except ValueError:
            pass


def dis(start, end):
    out = []
    for insn in md.disasm(data[start:end], start):
        note = ''
        if insn.mnemonic in ('bl', 'b') and insn.op_str.startswith('0x'):
            tgt = int(insn.op_str, 16)
            if tgt in syms:
                note = f'   ; {syms[tgt]}'
        out.append(f'{insn.address:08x}: {insn.bytes.hex()}  {insn.mnemonic:8s} {insn.op_str}{note}')
    return '\n'.join(out)


if __name__ == '__main__':
    start = int(sys.argv[1], 16)
    end = int(sys.argv[2], 16)
    print(dis(start, end))
