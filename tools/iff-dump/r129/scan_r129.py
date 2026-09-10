#!/usr/bin/env python3
"""R129: symbol map + field-reader sites for the STR#159 consumer decode.

Extends the R128 map: (a) CodeWarrior symbol boundaries over the TestSim
family (0x100000-0x108000) and the cObjPickerTool cluster (0x180000-0x188000);
(b) binary-wide D-form load/store sites touching the unresolved fields
(entity+1550 bits, entity+1536 type gate, entity+284, viewer+102 bit5) and
the TOC -28948 global, each with nearest preceding symbol; (c) all callers
of the two TryClick setup callees (0x182060 / 0x182840).
"""
import re
import struct

BIN = ("/Users/nathannoom/Developer/Games/The Sims/simitone-fork/"
       "game-data/The Sims/The Sims Complete")
data = open(BIN, "rb").read()

IDENT = re.compile(rb"^[A-Za-z_.$][A-Za-z0-9_.$]*$")


def scan_symbols(start, end):
    out = []
    a = start
    while a + 2 < min(end, len(data)):
        ln = struct.unpack(">H", data[a:a + 2])[0]
        if 4 <= ln <= 72 and a + 2 + ln <= len(data):
            s = data[a + 2:a + 2 + ln]
            if IDENT.match(s):
                t = s.decode()
                if "__" in t or t.startswith("."):
                    out.append((a, t))
                    a += 2 + ln
                    continue
        a += 1
    return out


REGIONS = [(0x100000, 0x108000), (0x180000, 0x188000)]
syms = sorted(s for r in REGIONS for s in scan_symbols(*r))


def nearest(addr):
    best = None
    for a, t in syms:
        if a <= addr:
            best = (a, t)
        else:
            break
    return best


print("=== SYMBOL MAP (0x100000-0x108000, 0x180000-0x188000) ===")
for a, t in syms:
    print(f"{a:#08x} {t}")

FIELDS = {1550, 1536, 284, 102}
LOADOPS = {32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47}
hits = []
for a in range(0x1000, min(0x400000, len(data) - 3), 4):
    w = struct.unpack(">I", data[a:a + 4])[0]
    op = w >> 26
    off = w & 0xFFFF
    if op in LOADOPS and off in FIELDS:
        rD = (w >> 21) & 31
        rA = (w >> 16) & 31
        hits.append((a, f"off{off} op{op} r{rD},{off}(r{rA})"))
    elif op == 32 and off == 0x8EEC and ((w >> 16) & 31) == 2:
        hits.append((a, "TOC-28948 lwz r?, -28948(r2)"))

print(f"\n=== FIELD-READER SITES ({len(hits)}) ===")
for a, t in hits:
    n = nearest(a)
    tag = f"{n[1]}+{a - n[0]:#x}" if n else "?"
    print(f"{a:#08x} {t:28s} {tag}")

print("\n=== CALLERS of 0x182060 / 0x182840 ===")
for a in range(0x1000, min(0x400000, len(data) - 3), 4):
    w = struct.unpack(">I", data[a:a + 4])[0]
    if (w >> 26) == 18 and (w & 1):
        tgt = a + (w & 0xFFFC if (w & 0x8000) == 0 else (w & 0xFFFC) - 0x10000)
        if tgt in (0x182060, 0x182840):
            n = nearest(a)
            tag = f"{n[1]}+{a - n[0]:#x}" if n else "?"
            print(f"{a:#08x} bl {tgt:#x}  from {tag}")

print("\n=== 104-off LOADS/STORES in cluster 0x180000-0x188000 ===")
for a in range(0x180000, 0x188000, 4):
    w = struct.unpack(">I", data[a:a + 4])[0]
    op = w >> 26
    if op in (32, 34, 36, 38, 11) and (w & 0xFFFF) == 104:
        n = nearest(a)
        tag = f"{n[1]}+{a - n[0]:#x}" if n else "?"
        print(f"{a:#08x} op{op} 104(r{(w >> 16) & 31})  {tag}")
