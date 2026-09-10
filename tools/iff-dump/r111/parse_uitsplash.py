#!/usr/bin/env python3
"""R111: byte-verbatim decode of GameData/UIText.iff STR# 'splashprogress'
(the original boot/title-screen rotating status strings).

Chunk layout (FSO tso.files IffFile.AddChunk, verified against the file):
  4CC 'STR#' | u32 BE chunkSize | u16 BE chunkID | u16 BE flags |
  64-byte null-padded label | (chunkSize-76) data
STR data is LITTLE-endian: i16 formatCode;
  0 -> u16 count + pascal strings
  -3 (0xFFFD) -> u16 count + {u8 langCode, cstring value, cstring comment}
"""
import struct, hashlib, sys

path = sys.argv[1] if len(sys.argv) > 1 else \
    "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/GameData/UIText.iff"
data = open(path, "rb").read()
hdr = data[:60]
assert hdr.startswith(b"IFF FILE 2.5:TYPE FOLLOWED BY SIZE"), "unexpected IFF header"

off = 0x40
strn = []
while off + 8 <= len(data):
    cid = data[off:off+4]
    (sz,) = struct.unpack(">I", data[off+4:off+8])
    cidnum, flags = struct.unpack(">HH", data[off+8:off+12])
    label = data[off+12:off+76].split(b"\0")[0].decode("mac_roman", "replace")
    body = off + 76
    dsz = sz - 76
    if cid == b"STR#":
        strn.append((cidnum, flags, label, off, sz, dsz, body))
    off = body + dsz

print(f"{path}: {len(strn)} STR# tables")

def decode_str(d, body, dsz):
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
        raise ValueError("formatCode %d not handled" % fc)
    return fc, ents

target = "splashprogress"
for (cidnum, flags, label, off, sz, dsz, body) in strn:
    if label != target: continue
    raw = data[off:off+76+dsz]
    fc, ents = decode_str(data, body, dsz)
    print(f"\n== STR# {target!r}: chunkID={cidnum} flags=0x{flags:x} file=0x{off:x}..0x{off+76+dsz:x}")
    print(f"   chunk bytes (4CC..end) = {len(raw)}  sha256 = {hashlib.sha256(raw).hexdigest()}")
    print(f"   data ({dsz} bytes) sha256 = {hashlib.sha256(data[body:body+dsz]).hexdigest()}")
    print(f"   formatCode = {fc} ({'lang-pairs' if fc == -3 else 'pascal'}), {len(ents)} entries")
    for i, (val, meta) in enumerate(ents):
        s = val.decode("mac_roman", "replace")
        extra = f"  [lang={meta[0]} comment={meta[1].decode('mac_roman','replace')!r}]" if meta else ""
        print(f"   [{i:3d}] ({len(val):3d}) {s!r}{extra}")

# inventory of loading-adjacent tables for the evidence file
print("\n-- loading/splash-adjacent STR# labels --")
for (cidnum, flags, label, off, sz, dsz, body) in strn:
    low = label.lower()
    if any(k in low for k in ("splash", "loading", "load", "progress", "boot", "title")):
        print(f"   {label!r} id={cidnum}")
