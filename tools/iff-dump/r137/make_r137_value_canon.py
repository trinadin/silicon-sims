#!/usr/bin/env python3
"""R137: generate the 'uivalue' gate canon block (STR# 146/134/131 from
UIText.iff — the value-display corpus) as C# constant declarations.

Follows the r133 canon-script pattern (60-byte IFF header, 76-byte chunk
header, format -3 STR# bodies, English = lang 1, game index = position
among lang-1 entries; sha256 over the FULL chunk incl. the header).
"""
import hashlib
import os
import struct

HERE = os.path.dirname(os.path.abspath(__file__))
IFFDUMP = os.path.dirname(HERE)
UITEXT = os.path.join(IFFDUMP, "../../game-data/The Sims/GameData/UIText.iff")

data = open(UITEXT, "rb").read()
assert data.startswith(b"IFF FILE 2.5:"), "not an IFF"


def find_str(tid):
    off = 0x40
    while off + 76 <= len(data):
        fourcc = data[off:off + 4]
        size = struct.unpack(">I", data[off + 4:off + 8])[0]
        cid = struct.unpack(">H", data[off + 8:off + 10])[0]
        label = data[off + 12:off + 76].split(b"\0")[0].decode("mac_roman")
        raw = data[off:off + size]
        if fourcc == b"STR#" and cid == tid:
            return label, raw
        off += size
    return None, None


def parse_lang1(raw):
    body = raw[76:]
    fmt = struct.unpack("<h", body[:2])[0]
    assert fmt == -3, f"format {fmt}"
    count = struct.unpack("<H", body[2:4])[0]
    pos = 4
    out = []
    for _ in range(count):
        lang = body[pos]
        pos += 1
        end = body.index(0, pos)
        val = body[pos:end].decode("mac_roman")
        pos = end + 1
        cend = body.index(0, pos)
        pos = cend + 1
        if lang == 1:
            out.append(val)
    return out


def csharp_lit(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"').replace("\r", "\\r").replace("\n", "\\n") + '"'


print("// ==== R137 REGENERATED CANON (tools/iff-dump/r137/make_r137_value_canon.py)")
print("//      — the value-display corpus; NEVER hand-edit ====")
for tid, picks in ((146, (30, 34, 35)), (134, (0,)), (131, (3,))):
    label, raw = find_str(tid)
    assert raw is not None, f"STR# {tid} not found"
    sha = hashlib.sha256(raw).hexdigest()
    entries = parse_lang1(raw)
    var = {146: "B", 134: "N", 131: "E"}[tid]
    print(f'private const string R137{var}Label = {csharp_lit(label)};')
    print(f'private const string R137{var}Sha = "{sha}";')
    for i in picks:
        print(f'private const string R137{var}{i} = {csharp_lit(entries[i])};')
print("// ==== END R137 REGENERATED CANON ====")
