#!/usr/bin/env python3
"""R159: canon decode of the people-panel string tables.

UIText.iff STR# 240 (relationship sort tooltips), 241 (inventory sort
tooltips), 164 'signs' (zodiac names); Live.iff STR# 139 (ReportCard).
Byte-verbatim, same chunk-walk as r111/parse_uitsplash.py.
"""
import struct, hashlib, sys

BASE = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/"

def walk(path):
    data = open(path, "rb").read()
    assert data[:20].startswith(b"IFF FILE 2.5"), "unexpected IFF header"
    out, off = [], 0x40
    while off + 8 <= len(data):
        cid = data[off:off+4]
        (sz,) = struct.unpack(">I", data[off+4:off+8])
        cidnum, flags = struct.unpack(">HH", data[off+8:off+12])
        label = data[off+12:off+76].split(b"\0")[0].decode("mac_roman", "replace")
        body, dsz = off + 76, sz - 76
        out.append((cid, cidnum, flags, label, off, sz, dsz, body))
        off = body + dsz
    return data, out

def decode(d, body, dsz):
    p = body
    (fc,) = struct.unpack("<h", d[p:p+2]); p += 2
    ents = []
    if fc == 0:
        (count,) = struct.unpack("<H", d[p:p+2]); p += 2
        for i in range(count):
            (l,) = struct.unpack("<H", d[p:p+2]); p += 2
            ents.append((d[p:p+l], None)); p += l
    elif fc == -3:
        (count,) = struct.unpack("<H", d[p:p+2]); p += 2
        for i in range(count):
            lang = d[p]; p += 1
            e = d.index(b"\0", p); val = d[p:e]; p = e + 1
            e = d.index(b"\0", p); com = d[p:e]; p = e + 1
            ents.append((val, (lang, com)))
    else:
        raise ValueError("formatCode %d" % fc)
    return fc, ents

TARGETS = [("UIText.iff", {240, 241, 164}), ("Live.iff", {139})]
for fname, ids in TARGETS:
    data, chunks = walk(BASE + ("GameData/UIText.iff" if fname == "UIText.iff" else "GameData/Live.iff"))
    for (cid, cidnum, flags, label, off, sz, dsz, body) in chunks:
        if cid != b"STR#" or cidnum not in ids: continue
        fc, ents = decode(data, body, dsz)
        raw = data[off:off+76+dsz]
        print(f"== {fname} STR# {cidnum} {label!r}: flags=0x{flags:x} chunkBytes={len(raw)} "
              f"sha256={hashlib.sha256(raw).hexdigest()}")
        print(f"   format={fc} rawEntries={len(ents)}")
        for i, (val, meta) in enumerate(ents):
            s = val.decode("mac_roman", "replace")
            extra = f"  [lang={meta[0]} com={meta[1].decode('mac_roman','replace')!r}]" if meta else ""
            print(f"   [{i:3d}] {s!r}{extra}")
        print()
