#!/usr/bin/env python3
"""R190: byte-verbatim canon of the SCRAPBOOK string tables.

STR# 141 and STR# 144 from UIText.iff. cWinScrapbook::Init (0x29f5a2) loads
template 0x4b0 (1200), registers string-set 144 (0x90) via LoadUIStringIntoLabel
0x25f440, and parses '(x;y)' anchors from it via 0x25f110 StringPosToPoint.
Usage: python3 make_r190_canon.py [UIText.iff]
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
    if data[off:off+4] == b"STR#" and cidnum in (140, 141, 144):
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
        print(f"== STR# {cidnum} ({label!r}) entries={count} chunkBytes={len(raw)}")
        print(f"   sha256(chunk) = {hashlib.sha256(raw).hexdigest()}")
        n = count
        for i in range(min(n, count)):
            print(f"   [{i}] {ents[i]!r}")
        print()
    off += sz
