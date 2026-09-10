#!/usr/bin/env python3
"""R204: canon decode of the remaining Tier B string families.

UIText.iff STR# 148 (Pause), 152 (DefaultDialogButtons), 162 (FriendCountDlg),
165 (SignPopups). Byte-verbatim, same chunk-walk as r111/r159.
"""
import struct, hashlib

BASE = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/GameData/UIText.iff"

data = open(BASE, "rb").read()
assert data[:20].startswith(b"IFF FILE 2.5"), "unexpected IFF header"

TARGETS = {148, 152, 162, 165}
off = 0x40
while off + 76 <= len(data):
    cid = data[off:off+4]
    (sz,) = struct.unpack(">I", data[off+4:off+8])
    cidnum, flags = struct.unpack(">HH", data[off+8:off+12])
    label = data[off+12:off+76].split(b"\0")[0].decode("mac_roman", "replace")
    if cid != b"STR#" or cidnum not in TARGETS:
        off += sz
        continue
    body, dsz = off + 76, sz - 76
    p = body
    (fc,) = struct.unpack("<h", data[p:p+2]); p += 2
    ents = []
    if fc == 0:
        (count,) = struct.unpack("<H", data[p:p+2]); p += 2
        for i in range(count):
            (l,) = struct.unpack("<H", data[p:p+2]); p += 2
            ents.append((data[p:p+l], None)); p += l
    elif fc == -3:
        (count,) = struct.unpack("<H", data[p:p+2]); p += 2
        for i in range(count):
            lang = data[p]; p += 1
            e = data.index(b"\0", p); val = data[p:e]; p = e + 1
            e = data.index(b"\0", p); com = data[p:e]; p = e + 1
            ents.append((val, (lang, com)))
    else:
        raise ValueError("formatCode %d" % fc)
    raw = data[off:off+76+dsz]
    print(f"== STR# {cidnum} {label!r}: flags=0x{flags:x} chunkBytes={len(raw)} sha256={hashlib.sha256(raw).hexdigest()}")
    print(f"   format={fc} rawEntries={len(ents)}")
    for i, (val, meta) in enumerate(ents):
        s = val.decode("mac_roman", "replace")
        extra = f"  [lang={meta[0]} com={meta[1].decode('mac_roman','replace')!r}]" if meta else ""
        print(f"   [{i:3d}] {s!r}{extra}")
    print()
    off += sz
