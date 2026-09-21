#!/usr/bin/env python3
# UI-30: extract Mac resource-fork 'STR#' id 10000 (InsertMacCredits law,
# coordination/evidence/UI-29/credits-law.md §4.1) from The Sims Complete.
# TEXT ONLY — no art, no code. MacRoman -> UTF-8 for the port-side data file.
import struct, sys

RFORK = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete/..namedfork/rsrc"
WANT_TYPE = b'STR#'
WANT_ID = 10000

data = open(RFORK, 'rb').read()
assert data[0:4] == b'\x00\x00\x01\x00', "not a classic resource fork"
data_off, map_off, data_len, map_len = struct.unpack_from('>IIII', data, 0x100)
m = map_off
# map: 16 bytes header copy, next map (4), file ref (2), attrs (2), type list off (2), name list off (2)
type_off = struct.unpack_from('>H', data, m + 24)[0]
name_off = struct.unpack_from('>H', data, m + 26)[0]
n_types = struct.unpack_from('>h', data, m + type_off)[0] + 1
found = None
for t in range(n_types):
    base = m + type_off + 2 + t * 8
    rtype = data[base:base + 4]
    n_refs = struct.unpack_from('>h', data, base + 4)[0] + 1
    ref_off = struct.unpack_from('>H', data, base + 6)[0]
    if rtype != WANT_TYPE:
        continue
    for r in range(n_refs):
        rb = m + type_off + ref_off + r * 12
        rid, name_o = struct.unpack_from('>hH', data, rb)
        attrs_doff = struct.unpack_from('>I', data, rb + 4)[0]
        doff = attrs_doff & 0x00FFFFFF
        if rid != WANT_ID:
            continue
        db = data_off + doff
        dlen = struct.unpack_from('>I', data, db)[0]
        found = data[db + 4: db + 4 + dlen]
        print(f"'STR#' {rid}: data len {dlen}", file=sys.stderr)
if found is None:
    print("NOT FOUND", file=sys.stderr); sys.exit(1)

count = struct.unpack_from('>h', found, 0)[0]
pos = 2
lines = []
for i in range(count):
    ln = found[pos]; pos += 1
    s = found[pos:pos + ln]; pos += ln
    lines.append(s.decode('mac_roman'))
print(f"count={count}", file=sys.stderr)
for ln in lines:
    print(ln)
