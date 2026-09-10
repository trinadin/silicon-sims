#!/usr/bin/env python3
"""R241 helper: accurate big-endian PPC32 disassembly of the original
executable via capstone, with a symbol-name annotator for bl targets.

Read-only input: game-data/The Sims/The Sims Complete (PEF, PPC).
Offsets are PEF file offsets (symbol index entries minus the historical +2).

Usage: python3 disasm.py 0xSTART 0xEND
"""
import sys
import capstone

EXE = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete"
SYM = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r141/symbol-index.txt"


def load_symbols():
    syms = {}
    with open(SYM) as fh:
        for line in fh:
            parts = line.split(None, 2)
            if len(parts) == 3:
                addr = int(parts[0], 16) - 2  # historical +2 convention
                syms[addr] = parts[2].strip()
    return syms


def main():
    start = int(sys.argv[1], 16)
    end = int(sys.argv[2], 16)
    syms = load_symbols()
    data = open(EXE, "rb").read()
    md = capstone.Cs(capstone.CS_ARCH_PPC, capstone.CS_MODE_32 | capstone.CS_MODE_BIG_ENDIAN)
    md.detail = False
    for insn in md.disasm(data[start:end], start):
        note = ""
        if insn.mnemonic == "bl":
            try:
                target = int(insn.op_str.replace("0x", ""), 16)
            except ValueError:
                target = int(insn.op_str, 16) if insn.op_str.startswith("0x") else None
            if target is not None and target in syms:
                note = "  ; " + syms[target]
        print("0x%08x: %-8s %s%s" % (insn.address, insn.mnemonic, insn.op_str, note))


if __name__ == "__main__":
    main()
