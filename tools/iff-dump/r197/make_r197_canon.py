#!/usr/bin/env python3
"""R197 lot-click canon: byte-verbatim decode of GameData/UIText.iff STR#
tables 131 / 132 / 134 (the neighborhood lot-query string families the PPC
engine consumes around cWinLotPopup + the lot-mode handlers).

Idioms reused from r121/make_r121_optionstrs_canon.py (which itself follows
the gate's FindStrChunkByID/ParseLang1):
  - 60-byte Maxis header, first chunk @0x40, linear chunk walk;
  - 76-byte chunk header: 4CC | u32 BE chunkSize | u16 BE chunkID | u16 flags
    | 64-byte null-padded label;
  - STR# bodies are LITTLE-endian; format 0 = u16 count + pascal strings,
    format -3 = u16 count + {u8 langCode, cstring value, cstring comment};
    game string index == position among lang-1 entries.

FAILS LOUDLY (non-zero exit) if any target table is absent, mislabeled, or
the decode desynchronizes.
"""
import struct, hashlib, sys

DEFAULT = ("/Users/nathannoom/Developer/Games/The Sims/simitone-fork/"
           "game-data/The Sims/GameData/UIText.iff")
TARGETS = {131: None, 132: "MoveInModeStrs", 134: "NghRollover"}


def die(msg):
    print("FATAL: " + msg)
    sys.exit(1)


def walk_chunks(data):
    if not data.startswith(b"IFF FILE 2.5:TYPE FOLLOWED BY SIZE"):
        die("unexpected IFF header: %r" % data[:40])
    chunks = {}
    off = 0x40
    while off + 76 <= len(data):
        (sz,) = struct.unpack(">I", data[off + 4:off + 8])
        if sz < 76 or off + sz > len(data):
            break
        cid = struct.unpack(">H", data[off + 8:off + 10])[0]
        label = data[off + 12:off + 76].split(b"\0")[0].decode("latin-1")
        chunks[cid] = (data[off:off + sz], label)
        off += sz
    return chunks


def parse_str(body):
    """Return (formatCode, [english strings]). lang-1 entries only, in order."""
    fmt = struct.unpack("<h", body[76:78])[0]
    n = struct.unpack("<H", body[78:80])[0]
    vals = []
    p = 80
    if fmt == 0:
        for _ in range(n):
            ln = body[p]; p += 1
            vals.append(body[p:p + ln].decode("latin-1")); p += ln
    elif fmt == -3:
        seen_other = 0
        for _ in range(n):
            lang = body[p]; p += 1
            end = body.index(b"\0", p)
            val = body[p:end].decode("latin-1")
            p = end + 1
            end2 = body.index(b"\0", p)
            p = end2 + 1
            if lang == 1:
                vals.append(val)
            else:
                seen_other += 1
        n = len(vals)
    else:
        die("unknown STR# format %d" % fmt)
    return fmt, vals


def main():
    data = open(DEFAULT, "rb").read()
    chunks = walk_chunks(data)
    out = []
    for cid, want in TARGETS.items():
        if cid not in chunks:
            die("STR# %d absent" % cid)
        raw, label = chunks[cid]
        if want is not None and label != want:
            die("STR# %d label %r != %r" % (cid, label, want))
        fmt, vals = parse_str(raw)
        sha = hashlib.sha256(raw).hexdigest()[:16]
        out.append("== STR# %d '%s' (format %d, %d en entries, chunk sha %s) ==" %
                   (cid, label, fmt, len(vals), sha))
        for i, v in enumerate(vals):
            out.append("[%d] %s" % (i, v.replace("\r", "\\r")))
        out.append("")
    print("\n".join(out))


if __name__ == "__main__":
    main()
