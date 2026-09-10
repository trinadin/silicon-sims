#!/usr/bin/env python3
"""R130 engine scan: roof/pitch classes in the ORIGINAL PPC binary.

Finds CodeWarrior symbols containing Roof/Pitch (function names at their
ENDs — R129 recipe), then decodes candidate float stores: PPC materializes
floats as lis+ori/addis+addic word pairs; roof pitch constants would appear
as immediate pairs near the roof tool code.
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
                if ("__" in t or t.startswith(".")) and (
                        "oof" in t or "itch" in t):
                    out.append((a, t))
                    a += 2 + ln
                    continue
        a += 1
    return out


hits = []
for start in range(0x1000, 0x600000, 0x100000):
    hits += scan_symbols(start, start + 0x100000)
hits.sort()
print("=== Roof/Pitch symbols ===")
for a, t in hits:
    print(f"{a:#08x} {t}")
