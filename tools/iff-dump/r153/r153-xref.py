#!/usr/bin/env python3
"""R153: generic direct-call xref over sec0. Usage: r153-xref.py 0xADDR [0xADDR...]
Prints every b/bl (op18) targeting each address with enclosing symbol names.
"""
import bisect
import json
import struct
import sys

BIN = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete"
OUTD = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r153"
SEC0_OFF, SEC0_LEN = 0x8E90, 0x5B9458

b = open(BIN, "rb").read()
syms = json.load(open(f"{OUTD}/r153-symbols.json"))
m = {s["start"]: s["name"] for s in syms}
starts = sorted(m)


def fn(off):
    i = bisect.bisect_right(starts, off) - 1
    return "<pre>" if i < 0 else (m[starts[i]] + ("" if starts[i] == off else f"+{off-starts[i]:#x}"))


targets = [int(a, 16) for a in sys.argv[1:]]
n = SEC0_LEN // 4
words = struct.unpack_from(f">{n}I", b, SEC0_OFF)
res = {t: [] for t in targets}
for i, w in enumerate(words):
    if (w >> 26) != 18:
        continue
    li = (w >> 2) & 0xFFFFFF
    if li & 0x800000:
        li -= 0x1000000
    tgt = SEC0_OFF + 4 * i + li * 4
    if tgt in res:
        res[tgt].append((SEC0_OFF + 4 * i, w, w & 1))
for t in targets:
    nm = fn(t)
    print(f"== {t:#x} {nm}")
    for a, w, lk in res[t]:
        print(f"   {'bl' if lk else 'b '} from {a:#08x} word {w:08x} in {fn(a)}")
    if not res[t]:
        print("   (no direct b/bl)")
