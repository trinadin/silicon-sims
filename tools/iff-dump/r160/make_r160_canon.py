#!/usr/bin/env python3
"""R160: byte-verbatim canon of the NAVBAR string tables.

STR# 151 'NghbBtnTips' (the 14-toolbar tooltips, 20 languages x 14 = 280
entries) pins chunk sha256 + the English block; the destination screens'
own tooltip tables 169 (Downtown) / 170 (Vacation Island) / 173 (Studio
Town) / 174 (Magicland) pin their English heads (entry 0 = 'Return to
Neighborhood View' on all four).

Chunk layout: FSO tso.files IffFile.AddChunk — 4CC 'STR#' | u32 BE size |
u16 BE id | u16 BE flags | 64-byte label | data. STR data LITTLE-endian,
formatCode -3 = { u16 count, { u8 lang, cstring value, cstring comment } }.
"""
import struct, hashlib, sys

path = sys.argv[1] if len(sys.argv) > 1 else \
    "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/GameData/UIText.iff"
data = open(path, "rb").read()
assert data.startswith(b"IFF FILE 2.5:TYPE FOLLOWED BY SIZE")

WANT = {151: 14, 169: 5, 170: 5, 173: 5, 174: 6}

off = 0x40
found = 0
while off + 76 <= len(data):
    (sz,) = struct.unpack(">I", data[off+4:off+8])
    cidnum = struct.unpack(">H", data[off+8:off+10])[0]
    label = data[off+12:off+76].split(b"\0")[0].decode("mac_roman", "replace")
    body, dsz = off + 76, sz - 76
    if data[off:off+4] == b"STR#" and cidnum in WANT:
        raw = data[off:off+sz]
        p = body
        (fc,) = struct.unpack("<h", data[p:p+2]); p += 2
        assert fc == -3
        (count,) = struct.unpack("<H", data[p:p+2]); p += 2
        ents = []
        for _ in range(count):
            p += 1  # langCode
            e = data.index(b"\0", p); val = data[p:e]; p = e + 1
            e = data.index(b"\0", p); p = e + 1  # comment
            ents.append(val.decode("mac_roman", "replace"))
        n = WANT[cidnum]
        print(f"== STR# {cidnum} ({label!r}) entries={count} chunkBytes={len(raw)}")
        print(f"   sha256(chunk) = {hashlib.sha256(raw).hexdigest()}")
        for i in range(n):
            print(f"   [{i}] {ents[i]!r}")
        found += 1
    off += sz

sys.exit(0 if found == len(WANT) else 1)
