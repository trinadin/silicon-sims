#!/usr/bin/env python3
"""UI-26: scan bl call sites of the native cOptionsMgr getters.

The symbol index (r141/symbol-index.txt) addresses are +2 shifted relative
to the raw file dump (R260 correction, verified: index 0x239fd2 -> prologue
at 0x239fd0 decodes clean; index+0 -> misaligned .long stream). Every
address below is (index - 2).

bl (op 18): target = pc + s24(LI)*4.
Calls are attributed to the nearest preceding symbol start (same -2 fix).
"""
import struct, sys

BIN = "../../../game-data/The Sims/The Sims Complete"
IDX = "../r141/symbol-index.txt"

# symbol -> true address (index - 2)
GETTERS = {
    "GetSimInBackground": 0x239fd2 - 2,
    "GetFreeWill":        0x23a022 - 2,
    "GetEdgeScrolling":   0x239eb2 - 2,
    "GetLivePIP":         0x239c22 - 2,
    "GetAutoSnapshots":   0x239ca2 - 2,
    "GetExportHTML":      0x239ba2 - 2,
    "GetLighting":        0x23a932 - 2,
    "GetShadows":         0x23a9f2 - 2,
    "GetAA":              0x23aab2 - 2,
    "GetCharacterDetail": 0x23a612 - 2,
    "GetTerrainDetail":   0x23a832 - 2 - 0xE0,  # placeholder, fixed below
    "GetBoboVision":      0x23a872 - 2,
    "GetVoxVolume":       0x23a152 - 2,
    "GetMusicVolume":     0x23a252 - 2,
    "GetFXVolume":        0x23a342 - 2,
}
GETTERS["GetTerrainDetail"] = 0x23a6b2 - 2 + 0x180  # resolved after symbol walk

data = open(BIN, "rb").read()

syms = []  # (addr, name)
for line in open(IDX):
    parts = line.split()
    if len(parts) >= 3:
        try:
            syms.append((int(parts[0], 16) - 2, parts[2]))
        except ValueError:
            pass
syms.sort()

def sym_before(a):
    lo, hi = 0, len(syms) - 1
    best = None
    while lo <= hi:
        mid = (lo + hi) // 2
        if syms[mid][0] <= a:
            best = syms[mid]; lo = mid + 1
        else:
            hi = mid - 1
    return best

import bisect
addrs = [s[0] for s in syms]
def sym_before2(a):
    i = bisect.bisect_right(addrs, a) - 1
    return syms[i] if i >= 0 else None

# Fix GetTerrainDetail address properly: find by name.
for a, n in syms:
    if n == ".GetTerrainDetail__11cOptionsMgrFv":
        GETTERS["GetTerrainDetail"] = a

# SECTION 0: containerOffset 36496, containerLength 6001752 (pef_load.py).
BASE, LENGTH = 36496, 6001752
target_map = {}
for name, addr in GETTERS.items():
    target_map.setdefault(addr, []).append(name)

hits = {}
for a in range(BASE, BASE + LENGTH - 3, 4):
    w = struct.unpack(">I", data[a:a + 4])[0]
    if (w >> 26) != 18:  # bl only (Lk=1 -> 0x48000001; accept both link bits)
        continue
    li = w & 0x03FFFFFC
    if li & 0x02000000:
        li -= 0x04000000
    tgt = a + li
    if tgt in target_map:
        caller = sym_before2(a)
        for name in target_map[tgt]:
            hits.setdefault(name, []).append((a, caller[1] if caller else "?"))

for name in sorted(hits):
    print(f"== {name} ({len(hits[name])} bl site(s))")
    for site, caller in hits[name][:40]:
        print(f"   {site:#08x}  <- {caller}")
