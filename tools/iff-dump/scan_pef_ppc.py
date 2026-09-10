#!/usr/bin/env python3
"""R90: reverse-engineer sprite placement/timing from the ORIGINAL Mac engine.

The original engine (The Sims Complete, PowerPC PEF — game-data/The Sims/)
hardcodes every neighborhood-screen sprite position and frame timing in code.
The IFF corpus (Res_Nbhd.RT) only declares bitmaps, so the engine binary is
the only on-disk source for placement.

Method (engine-independent, byte-level):
  1. Parse the resource-id map from the original Res_Nbhd.h (R83 extract).
  2. Walk the PEF file 4-byte-aligned as big-endian PPC instructions.
  3. Decode immediate-bearing instructions (li/addi/ori/oris/cmpwi/mulli...).
     A hit whose 16-bit immediate equals a known id (kNess1=5050, ...) is an
     anchor: the original C++ passes sprite x/y/frame-time as register
     arguments adjacent to the id (classic Mac ABI: args in r3..r10), so the
     surrounding immediates ARE the placement/timing constants.
  4. Print an instruction-lite window around each anchor cluster.

Usage:
  python3 scan_pef_ppc.py <binary> <Res_Nbhd.h> [--window N] [--all-ids]
"""
import re
import struct
import sys
from collections import defaultdict

OPCODES = {
    14:  "addi",   # li = addi rD, 0, simm
    12:  "addic",
    13:  "addic.",
    7:   "mulli",
    24:  "ori",
    25:  "oris",
    26:  "xori",
    27:  "xoris",
    11:  "cmpwi",
    10:  "cmpli",
}
IMM_OPS = set(OPCODES)


def simm16(v):
    return v - 0x10000 if v & 0x8000 else v


def decode(word):
    """Return (mnemonic, rD, rA, imm) for immediate ops, else None/'other'."""
    op = word >> 26
    if op in IMM_OPS:
        rD = (word >> 21) & 31
        rA = (word >> 16) & 31
        imm = word & 0xFFFF
        name = OPCODES[op]
        if name == "cmpwi":
            return (name, rD, rA, simm16(imm))
        return (name, rD, rA, simm16(imm) if op in (14, 12, 13, 7) else imm)
    return None


def decode_ctx(word):
    """Simplified decode for context lines."""
    op = word >> 26
    if op in IMM_OPS:
        return decode(word)
    if op == 18:  # b/bl
        off = simm16(word & 0xFFFC) if not (word & 2) else (word & 0xFFFC)
        return ("bl" if word & 1 else "b", (word >> 21) & 31, (word >> 16) & 31, off * 4)
    if op in (32, 36, 34, 38):  # lwz/stw/lbz/stb
        return ("lwz" if op == 32 else "stw" if op == 36 else "lbz" if op == 34 else "stb",
                (word >> 21) & 31, (word >> 16) & 31, simm16(word & 0xFFFF))
    if op == 31:
        xo = (word >> 1) & 0x3FF
        if xo == 231:  # mullw
            return ("mullw", (word >> 21) & 31, (word >> 11) & 31, (word >> 16) & 31)
        if xo == 266:  # add
            return ("add", (word >> 21) & 31, (word >> 11) & 31, (word >> 16) & 31)
    if op == 15:  # addis
        return ("addis", (word >> 21) & 31, (word >> 16) & 31, (word & 0xFFFF) << 16)
    return None


def main():
    binpath = sys.argv[1]
    hpath = sys.argv[2]
    window = int(sys.argv[sys.argv.index("--window") + 1]) if "--window" in sys.argv else 44

    ids = {}
    with open(hpath) as f:
        for m in re.finditer(r"const\s+Sint32\s+(\w+)\s*=\s*(\d+)\s*;", f.read()):
            ids[int(m.group(2))] = m.group(1)
    print(f"# ids parsed from Res_Nbhd.h: {len(ids)} (range {min(ids)}..{max(ids)})")

    data = open(binpath, "rb").read()
    n = len(data) // 4
    words = struct.unpack(f">{n}I", data[: n * 4])

    # --at 0xADDR --span N: dump one decoded window and exit ---------------
    if "--at" in sys.argv:
        at = int(sys.argv[sys.argv.index("--at") + 1], 0)
        span = int(sys.argv[sys.argv.index("--span") + 1], 0) if "--span" in sys.argv else 80
        lo, hi = at // 4, min(n, at // 4 + span)
        for i in range(lo, hi):
            w = words[i]
            d = decode_ctx(w)
            tag = ""
            if d and d[3] in ids and d[0] in OPCODES.values():
                tag = f"   <== id {ids[d[3]]} ({d[3]})"
            if d:
                if d[0] in ("lwz", "stw", "lbz", "stb"):
                    print(f"  {i*4:#08x}: {w:08x}  {d[0]} r{d[1]}, {d[3]}(r{d[2]}){tag}")
                elif d[0] in ("b", "bl"):
                    print(f"  {i*4:#08x}: {w:08x}  {d[0]} {d[3]:+d}{tag}")
                else:
                    print(f"  {i*4:#08x}: {w:08x}  {d[0]} r{d[1]}, r{d[2]}, {d[3]}{tag}")
            else:
                print(f"  {i*4:#08x}: {w:08x}  .long{tag}")
        return

    # --- anchor hits -----------------------------------------------------
    hits = []  # (word_index, id, name, decoded, strong)
    for i, w in enumerate(words):
        d = decode(w)
        if not d:
            continue
        val = d[3] if d[3] >= 0 else None
        if val is not None and val in ids:
            # strong = li form (addi rD, r0, imm): a true immediate the code
            # passes as an argument. rA != 0 means base+offset — usually a
            # coincidental value (jump-table / struct offset), kept as weak.
            strong = d[2] == 0
            hits.append((i, val, ids[val], d, strong))

    print(f"# anchor hits (imm == known id): {len(hits)} "
          f"(strong li: {sum(1 for h in hits if h[4])})")
    by_id = defaultdict(lambda: [0, 0])
    for _, v, name, _, strong in hits:
        by_id[(v, name)][0 if strong else 1] += 1
    for (v, name), (s, wk) in sorted(by_id.items()):
        print(f"#   {name} ({v}): strong={s} weak={wk}")

    # --- cluster hits within `window` instructions -----------------------
    clusters = []
    cur = [hits[0]] if hits else []
    for h in hits[1:]:
        if h[0] - cur[-1][0] <= window:
            cur.append(h)
        else:
            clusters.append(cur)
            cur = [h]
    if cur:
        clusters.append(cur)
    print(f"# clusters (gap > {window} instrs): {len(clusters)}")

    out = []
    for ci, cl in enumerate(clusters):
        lo = max(0, cl[0][0] - 24)
        hi = min(n, cl[-1][0] + 24)
        out.append(f"\n===== cluster {ci} @ file:0x{lo*4:x}..0x{hi*4:x} "
                   f"ids: {sorted(set((h[2], h[1]) for h in cl))} =====")
        for i in range(lo, hi):
            w = words[i]
            d = decode_ctx(w)
            mark = ""
            for h in cl:
                if h[0] == i:
                    mark = f"   <== {h[2]} ({h[1]})"
            if d:
                if d[0] in ("lwz", "stw", "lbz", "stb"):
                    out.append(f"  {i*4:#08x}: {w:08x}  {d[0]} r{d[1]}, {d[3]}(r{d[2]}){mark}")
                elif d[0] in ("b", "bl"):
                    out.append(f"  {i*4:#08x}: {w:08x}  {d[0]} {d[3]:+d}{mark}")
                else:
                    out.append(f"  {i*4:#08x}: {w:08x}  {d[0]} r{d[1]}, r{d[2]}, {d[3]}{mark}")
            elif mark:
                out.append(f"  {i*4:#08x}: {w:08x}  .long{mark}")
    print("\n".join(out))


if __name__ == "__main__":
    main()
