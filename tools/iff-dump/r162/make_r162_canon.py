#!/usr/bin/env python3
"""R162: byte-verbatim canon of the HELP string table.

STR# 166 'Help' (1598 entries; the English block [0..93] = 47 TOPIC PAIRS,
title even / body odd) pins chunk sha256. Consumed by cWinHelp::Init
@0x277cc0 (window template 3008 via the 0x3b6190 loader — the standard
engine modal, same family as cWinBudgetDlg) with the scrollable topic
list (TSPaint @0x277b20 draws the proportional thumb, 0x55555556 /3
arithmetic) and SetSelected/GetSelected persistence @0x2777c2/0x277822.
Usage: python3 make_r162_canon.py [UIText.iff]
"""
import struct, hashlib, sys

path = sys.argv[1] if len(sys.argv) > 1 else \
    "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/GameData/UIText.iff"
data = open(path, "rb").read()
off = 0x40
while off + 76 <= len(data):
    (sz,) = struct.unpack(">I", data[off+4:off+8])
    cidnum = struct.unpack(">H", data[off+8:off+10])[0]
    label = data[off+12:off+76].split(b"\0")[0].decode("mac_roman", "replace")
    if data[off:off+4] == b"STR#" and cidnum == 166:
        raw = data[off:off+sz]
        p = off + 76
        (fc,) = struct.unpack("<h", data[p:p+2]); p += 2
        assert fc == -3
        (count,) = struct.unpack("<H", data[p:p+2]); p += 2
        ents = []
        for _ in range(count):
            p += 1
            e = data.index(b"\0", p); val = data[p:e]; p = e + 1
            e = data.index(b"\0", p); p = e + 1
            ents.append(val.decode("mac_roman", "replace"))
        print(f"== STR# 166 ({label!r}) entries={count} chunkBytes={len(raw)}")
        print(f"   sha256(chunk) = {hashlib.sha256(raw).hexdigest()}")
        for i in range(94):
            tag = "T" if i % 2 == 0 else "B"
            print(f"   [{i:2d}]{tag} {ents[i]!r}")
        sys.exit(0)
    off += sz
sys.exit(1)
