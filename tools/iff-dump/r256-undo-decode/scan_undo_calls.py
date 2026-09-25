#!/usr/bin/env python3
"""UI-22: scan the PPC code section for bl-call sites of the undo machinery.

Targets (file offsets = code-virtual, symbol-index addr - 2):
  0x194990 UndoManager::__ct          0x194680 UndoManager::SubmitUndoable
  0x194550 UndoManager::UndoLastCommand  0x1942c0 UndoManager::RedoLastCommand
  0x194180 UndoManager::ReleaseUndoList  0x193f20 UndoManager::ReleaseRedoList
  0x193eb0 UndoManager::FlushCommandQueues
  0x192bc0 Undoable::Commit           0x192ab0 Undoable::UndoIt
  0x192a30 Undoable::RedoIt           0x1929f0 Undoable::GetState
Branch law (r129-corrected, re-validated vs ppc_decode output):
  LI22 = signext22((w >> 2) & 0x3FFFFF); target = addr + LI22*4
Prints each caller with the nearest preceding symbol from r141/symbol-index.txt.
"""
import struct, sys, bisect, re

BIN = "game-data/The Sims/The Sims Complete"
CODE_START, CODE_END = 0x8E90, 0x5C22E8
SYM = "tools/iff-dump/r141/symbol-index.txt"

TARGETS = {
    0x194990: "UndoManager::__ct",
    0x1948e0: "UndoManager::__dt",
    0x194680: "UndoManager::SubmitUndoable",
    0x194550: "UndoManager::UndoLastCommand",
    0x1942c0: "UndoManager::RedoLastCommand",
    0x194180: "UndoManager::ReleaseUndoList",
    0x193f20: "UndoManager::ReleaseRedoList",
    0x193eb0: "UndoManager::FlushCommandQueues",
    0x192bc0: "Undoable::Commit",
    0x192ab0: "Undoable::UndoIt",
    0x192a30: "Undoable::RedoIt",
    0x1929f0: "Undoable::GetState",
    0x192e30: "vecimp:begin",
    0x193d90: "vecimp:ctor",
    0x193760: "vecimp:reserve?",
}

def s22(v):
    return v - 0x400000 if v & 0x200000 else v

def main():
    base = sys.argv[1] if len(sys.argv) > 1 else "."
    with open(f"{base}/{BIN}", "rb") as f:
        f.seek(CODE_START)
        code = f.read(CODE_END - CODE_START)

    # symbol map for caller attribution
    syms = []
    with open(f"{base}/{SYM}") as f:
        for line in f:
            m = re.match(r"^([0-9a-f]{8})\s+(\d+)\s+(\S+)", line)
            if m:
                syms.append((int(m.group(1), 16) - 2, m.group(3)))
    syms.sort()
    saddr = [a for a, _ in syms]

    def nearest(addr):
        i = bisect.bisect_right(saddr, addr) - 1
        if i < 0:
            return "<none>"
        return syms[i][1]

    found = {}
    for off in range(0, len(code) - 3, 4):
        w = struct.unpack_from(">I", code, off)[0]
        if (w >> 26) == 18 and (w & 3) == 1:  # bl (AA=0, LK=1)
            addr = CODE_START + off
            tgt = addr + 4 * s22((w >> 2) & 0x3FFFFF)
            if tgt in TARGETS:
                found.setdefault(tgt, []).append(addr)

    for tgt, name in sorted(TARGETS.items(), key=lambda kv: kv[0]):
        callers = found.get(tgt, [])
        print(f"\n== {name} @0x{tgt:x}: {len(callers)} call(s)")
        for c in callers:
            print(f"   0x{c:06x}  in {nearest(c)}")

if __name__ == "__main__":
    main()
