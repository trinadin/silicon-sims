#!/usr/bin/env python3
"""R131 engine scan: the live-mode gauge (cWinPeople) in the ORIGINAL PPC binary.

R123 left a single strong anchor: `li r?, kLiveModeGauge` at 0x28eee4 inside
cWinPeople::Init. CodeWarrior symbols sit at each function's END (u16 length +
identifier with leading dot or containing '__'; R129 recipe). This scan finds
every People/Live/Gauge/Motive/Mood-flavored symbol so the cWinPeople cluster
can be mapped before decoding Init around the anchor.
"""
import re
import struct

BIN = ("/Users/nathannoom/Developer/Games/The Sims/simitone-fork/"
       "game-data/The Sims/The Sims Complete")
data = open(BIN, "rb").read()

IDENT = re.compile(rb"^[A-Za-z_.$][A-Za-z0-9_.$]*$")

NEEDLES = ("People", "LiveMode", "Live", "Gauge", "Motive", "Mood", "Plumb")


def scan_symbols(start, end):
    out = []
    a = start
    while a + 2 < min(end, len(data)):
        ln = struct.unpack(">H", data[a:a + 2])[0]
        if 4 <= ln <= 72 and a + 2 + ln <= len(data):
            s = data[a + 2:a + 2 + ln]
            if IDENT.match(s):
                t = s.decode()
                if ("__" in t or t.startswith(".")) and any(
                        n in t for n in NEEDLES):
                    out.append((a, t))
                    a += 2 + ln
                    continue
        a += 1
    return out


hits = []
for start in range(0x1000, 0x600000, 0x100000):
    hits += scan_symbols(start, start + 0x100000)
hits.sort()
print("=== People/Live/Gauge/Motive/Mood symbols ===")
for a, t in hits:
    print(f"{a:#08x} {t}")
print(f"total {len(hits)}")
