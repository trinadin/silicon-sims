#!/usr/bin/env python3
"""R153 task 1: caller graph for the interest-chain routines.

Scans all of sec0 (the raw code section, file 0x8E90..0x5C22E8) for
  * op 18 (b/bl) with LK -> direct calls
  * op 18 without LK -> tail jumps
  * op 16 (bc) with AA/LK -> absolute branch-calls (rare; reported if any)
against the target set. Resolves each site's enclosing function via
r153-symbols.json. Also resolves TOC-indirect references: a TOC entry (in the
unpacked data sec1 image, r2 = image+0x8000, values sec0-relative) equal to
(target-0x8E90) is searched, then every `lwz rX, disp(r2)` loading that slot
is listed (candidate indirect/virtual call sites).

Usage: r153-caller-graph.py  -> writes r153-caller-graph.txt
"""
import json
import struct
import sys

BIN = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete"
SEC1 = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r104/sec1-unpacked.bin"
OUTD = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r153"
SEC0_OFF, SEC0_LEN = 0x8E90, 0x5B9458

TARGETS = {
    0x111650: "RandomizeAllInterests__8cXPersonFv",
    0x111910: "Reset__8cXPersonFUc",
    0x111CD0: "Initialize__8cXPersonFv",
    0x0B4A80: "LoadInterestData__12NeighborhoodFP8cXPerson",
    0x0A6340: "ChangeSimBaseType__12NeighborhoodF...",
    0x1411D0: "GetNextRandomNumber__Fv",
}

b = open(BIN, "rb").read()
sec1 = open(SEC1, "rb").read()
syms = json.load(open(f"{OUTD}/r153-symbols.json"))
starts = sorted(s["start"] for s in syms)
names = {s["start"]: s["name"] for s in syms}


def enclosing(addr):
    import bisect
    i = bisect.bisect_right(starts, addr) - 1
    if i < 0:
        return ("<before first>", 0)
    return names[starts[i]], starts[i]


out = open(f"{OUTD}/r153-caller-graph.txt", "w")
out.write("# R153 caller graph: direct b/bl (op18) + absolute bc, whole sec0\n")

calls = {t: [] for t in TARGETS}
jumps = {t: [] for t in TARGETS}
absbr = {t: [] for t in TARGETS}

for off in range(SEC0_OFF, SEC0_OFF + SEC0_LEN - 4, 4):
    w = struct.unpack_from(">I", b, off)[0]
    op = w >> 26
    if op == 18:
        li = (w >> 2) & 0xFFFFFF
        if li & 0x800000:
            li -= 0x1000000
        tgt = off + li * 4
        if tgt in TARGETS:
            (calls if (w & 1) else jumps)[tgt].append((off, w))
    elif op == 16 and (w & 2):  # bc with AA (absolute)
        bd = w & 0xFFFC
        if bd & 0x8000:
            bd -= 0x10000
        if bd in TARGETS:
            absbr[bd].append((off, w))

for t in sorted(TARGETS):
    out.write(f"\n== {TARGETS[t]} @ {t:#x}\n")
    for tag, lst in (("bl", calls[t]), ("b(tail)", jumps[t]), ("bc abs", absbr[t])):
        for off, w in lst:
            fn, fs = enclosing(off)
            out.write(f"  {tag} from {off:#08x} word {w:08x}  in {fn} (starts {fs:#x})\n")
    # TOC indirect: find sec1 image offsets holding the sec0-relative pointer
    val = struct.pack(">I", t - SEC0_OFF)
    idx = 0
    slots = []
    while True:
        i = sec1.find(val, idx)
        if i < 0 or i + 4 > len(sec1):
            break
        if i % 4 == 0:
            slots.append(i)
        idx = i + 1
    out.write(f"  TOC/data slots holding {t-SEC0_OFF:#x} (sec0-relative): {[hex(s) for s in slots]}\n")
    for s in slots:
        disp = s - 0x8000  # r2 displacement (signed)
        if not (-0x8000 <= disp <= 0x7FFF):
            out.write(f"    slot {s:#x}: outside r2 +-32K window, skipped\n")
            continue
        ud = disp & 0xFFFF
        # find lwz rX, ud(r2)
        hits = []
        for off in range(SEC0_OFF, SEC0_OFF + SEC0_LEN - 4, 4):
            w = struct.unpack_from(">I", b, off)[0]
            if (w >> 26) == 32 and (w & 0xFFFF) == ud and ((w >> 16) & 31) == 2:
                hits.append((off, w))
        for off, w in hits:
            fn, fs = enclosing(off)
            out.write(f"    lwz r{(w>>21)&31}, {disp}(r2) @ {off:#08x} in {fn} (starts {fs:#x})\n")

out.close()
print(open(f"{OUTD}/r153-caller-graph.txt").read())
