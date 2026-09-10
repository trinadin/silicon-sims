#!/usr/bin/env python3
"""R161: byte-verbatim canon of the BUDGET string table.

STR# 146 'budgetstrs' (648 entries = 18 languages x 36) pins chunk sha256 +
the English block. Layout of the block (the engine consumes it in
cWinBudgetDlg::Init @0x263e70):
  [0] '(40;25)'  [1] '(40;100)'   the two (x;y) ANCHOR pairs (parsed into
                                   dlg+0x124/dlg+0x12c via 0x25f110)
  [2] 'OK' [3] 'Budget' [4] '3 Days' [5] 'Today'
  [6..34 even] the 15 row labels; odd = the row TOOLTIPS
  [30]/[32]/[34] are '%s' FORMAT lines (the totals), formatted whole.
Usage: python3 make_r161_canon.py [UIText.iff]
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
    if data[off:off+4] == b"STR#" and cidnum == 146:
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
        print(f"== STR# 146 ({label!r}) entries={count} chunkBytes={len(raw)}")
        print(f"   sha256(chunk) = {hashlib.sha256(raw).hexdigest()}")
        for i in range(36):
            print(f"   [{i}] {ents[i]!r}")
        sys.exit(0)
    off += sz
sys.exit(1)
