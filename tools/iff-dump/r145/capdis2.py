#!/usr/bin/env python3.13
"""R145: chunk PPC disassembler — skips invalid words (data-in-code / jump tables)
instead of stopping. Usage: capdis2.py START END"""
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

# r145 law: symbol index addr == file offset + 2
fisyms = {a - 2: n for a, n in syms.items()}


def dis(start, end):
    out = []
    pc = start
    while pc < end:
        if pc in fisyms:
            out.append(f'                ; ==== {fisyms[pc]} ====')
        chunk = md.disasm(data[pc:end], pc)
        got = False
        for insn in chunk:
            got = True
            note = ''
            if insn.mnemonic in ('bl', 'b') and insn.op_str.startswith('0x'):
                tgt = int(insn.op_str, 16)
                if tgt in syms:
                    note = f'   ; {syms[tgt]}'
                elif tgt in fisyms:
                    note = f'   ; {fisyms[tgt]}'
            elif insn.op_str.startswith('0x') and int(insn.op_str, 16) in fisyms:
                note = f'   ; DATA {fisyms[int(insn.op_str, 16)]}'
            out.append(f'{insn.address:08x}: {insn.bytes.hex()}  {insn.mnemonic:8s} {insn.op_str}{note}')
            pc = insn.address + insn.size
        if not got:
            out.append(f'{pc:08x}: {data[pc:pc+4].hex()}  .long    0x{int.from_bytes(data[pc:pc+4], "big"):08x}')
            pc += 4
    return '\n'.join(out)


if __name__ == '__main__':
    start = int(sys.argv[1], 16)
    end = int(sys.argv[2], 16)
    print(dis(start, end))
