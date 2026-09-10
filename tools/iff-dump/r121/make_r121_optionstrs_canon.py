#!/usr/bin/env python3
"""R121 'optionstrs' canon: byte-verbatim decode of GameData/UIText.iff STR# 145
'optionstrs' (the original options-panel string table), plus a survey of the
neighboring STR# tables with chunk IDs 140..160. Tables whose label contains
'option' or 'help' are dumped in full in the same run.

Idioms reused from prior rounds (r111/parse_uitsplash.py, r117 dump, and the
gate's own FindStrChunkByID/ParseLang1 in AutotestRunner.cs:4435/4462):
  - 60-byte Maxis header, first chunk @0x40, chunks walked linearly;
  - 76-byte chunk header: 4CC | u32 BE chunkSize | u16 BE chunkID | u16 BE flags
    | 64-byte null-padded label; body = chunkSize-76 bytes;
  - full-chunk sha256 hashes the 76-byte header TOO (r117p2 lesson: the gate's
    FindStrChunkByID returns the whole chunk);
  - STR# data is LITTLE-endian: i16 formatCode;
      0  -> u16 count + pascal strings
      -3 -> u16 count + {u8 langCode, cstring value, cstring comment}
    lang 1 = English; the game string index == position among lang-1 entries;
  - trailing bytes after the declared count are 0xA3 padding (FreeSo TS1 STR
    writer quirk) and must not be walked as entries.

FAILS LOUDLY (non-zero exit) if STR# 145 is absent, mislabeled, or the decode
desynchronizes.
"""
import struct, hashlib, sys, os

DEFAULT = ("/Users/nathannoom/Developer/Games/The Sims/simitone-fork/"
           "game-data/The Sims/GameData/UIText.iff")
TARGET_ID = 145
TARGET_LABEL = "optionstrs"
NEIGHBOR_RANGE = range(140, 161)          # inclusive survey window
DUMP_SUBSTRS = ("option", "help")         # neighbor labels to dump in full


def die(msg):
    print("FATAL: " + msg)
    sys.exit(1)


def walk_chunks(data):
    """Linear chunk walk from 0x40, same as the gate's FindStrChunkByID."""
    if not data.startswith(b"IFF FILE 2.5:TYPE FOLLOWED BY SIZE"):
        die("unexpected IFF header: %r" % data[:40])
    chunks = []
    off = 0x40
    while off + 76 <= len(data):
        cc = data[off:off + 4]
        (sz,) = struct.unpack(">I", data[off + 4:off + 8])
        if sz < 76 or off + sz > len(data):
            break
        cid, flags = struct.unpack(">HH", data[off + 8:off + 12])
        label = data[off + 12:off + 76].split(b"\0")[0].decode("mac_roman", "replace")
        chunks.append({"cc": cc, "id": cid, "flags": flags, "label": label,
                       "off": off, "size": sz})
        off += sz
    return chunks


def cstring(d, p):
    e = d.index(b"\0", p)
    return d[p:e], e + 1


def decode_str_body(body):
    """Returns (formatCode, entries, trailer) where entries are
    (lang_or_None, value_bytes, comment_bytes_or_None)."""
    if len(body) < 4:
        die("STR# body too short (%d bytes)" % len(body))
    (fc,) = struct.unpack("<h", body[0:2])
    ents, p = [], 2
    if fc == 0:
        (count,) = struct.unpack("<H", body[p:p + 2]); p += 2
        for i in range(count):
            if p + 2 > len(body):
                die("pascal entry %d desyncs" % i)
            (l,) = struct.unpack("<H", body[p:p + 2]); p += 2
            ents.append((None, body[p:p + l], None)); p += l
    elif fc == -3:
        (count,) = struct.unpack("<H", body[p:p + 2]); p += 2
        for i in range(count):
            if p >= len(body):
                die("lang entry %d desyncs (p=%d, len=%d)" % (i, p, len(body)))
            lang = body[p]; p += 1
            val, p = cstring(body, p)
            com, p = cstring(body, p)
            ents.append((lang, val, com))
    else:
        die("formatCode %d not handled" % fc)
    return fc, ents, body[p:]


def lang_layout_desc(ents):
    """'blocked' = all of one language then the next; 'interleaved' = per-index
    language groups; '' if single/none."""
    codes = [l for l, _, _ in ents if l is not None]
    if len(set(codes)) < 2:
        return "single-language"
    runs = 1 + sum(1 for a, b in zip(codes, codes[1:]) if a != b)
    if runs == len(set(codes)):
        return "blocked-by-language (English block first)" if codes[0] == 1 else "blocked-by-language"
    if runs == len(codes):
        return "interleaved-by-index"
    return "mixed (%d runs over %d entries)" % (runs, len(codes))


def english_entries(ents):
    return [(i, v) for i, (l, v, _) in enumerate(ents) if l == 1]


def dump_table(ch, data, out, per_string_sha=True):
    body = data[ch["off"] + 76: ch["off"] + ch["size"]]
    raw = data[ch["off"]: ch["off"] + ch["size"]]
    fc, ents, trailer = decode_str_body(body)
    eng = english_entries(ents)
    langs = sorted({l for l, _, _ in ents if l is not None})
    out.append("")
    out.append("=== STR# %d %r (chunkSize %d; %d raw entries; format %d; %s) ==="
               % (ch["id"], ch["label"], ch["size"], len(ents), fc, lang_layout_desc(ents)))
    out.append("file range 0x%x..0x%x  flags=0x%04x"
               % (ch["off"], ch["off"] + ch["size"], ch["flags"]))
    out.append("full-chunk sha256 %s" % hashlib.sha256(raw).hexdigest())
    out.append("body-only  sha256 %s" % hashlib.sha256(body).hexdigest())
    out.append("languages present: %s  (lang 1 = English)" % langs)
    if eng and len(ents) % len(eng) == 0:
        out.append("raw %d = %d English x %d languages"
                   % (len(ents), len(eng), len(ents) // len(eng)))
    if trailer:
        allpad = trailer.strip(b"\xa3") == b""
        out.append("trailer: %d bytes, %s"
                   % (len(trailer), "all 0xA3 padding (FreeSo STR writer quirk)" if allpad
                      else "UNEXPECTED non-0xA3 bytes: %r" % trailer[:32]))
    out.append("")
    out.append("English entries [0..%d], verbatim (mac_roman):" % (len(eng) - 1))
    for idx, (raw_i, val) in enumerate(eng):
        s = val.decode("mac_roman", "replace")
        nonascii = "  <-- non-ASCII byte(s) " + val.hex() if any(b > 127 for b in val) else ""
        line = "%3d  (%3d B)  %r%s" % (idx, len(val), s, nonascii)
        if per_string_sha:
            line += "  sha256=%s" % hashlib.sha256(val).hexdigest()
        out.append(line)
    return eng


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else DEFAULT
    if not os.path.exists(path):
        # fall back to repo-relative location before giving up
        # (script lives at <repo>/tools/iff-dump/r121/, so 4 dirnames = repo root)
        root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(
            os.path.abspath(__file__)))))
        alt = os.path.join(root, "game-data", "The Sims", "GameData", "UIText.iff")
        if os.path.exists(alt):
            path = alt
        else:
            die("UIText.iff not found at %r (or %r)" % (path, alt))
    data = open(path, "rb").read()
    chunks = walk_chunks(data)
    strn = [c for c in chunks if c["cc"] == b"STR#"]
    out = []
    out.append("R121 canon dump — UIText.iff STR# 145 'optionstrs' + neighbors 140..160")
    out.append("Source: %s (%d bytes; full-file sha256 %s)"
               % (path, len(data), hashlib.sha256(data).hexdigest()))
    out.append("Parser: 60-byte Maxis header, first chunk @0x40, 76-byte chunk headers, "
               "BE sizes; STR# data LE, format -3 = language pairs "
               "{u8 lang, cstring value, cstring comment}, lang 1 = English; "
               "full-chunk sha includes the 76-byte header (gate convention).")
    out.append("Total chunks walked: %d (%d STR# tables)" % (len(chunks), len(strn)))

    # --- the target: STR# 145 'optionstrs' — FAIL LOUDLY if absent ----------
    target = [c for c in strn if c["id"] == TARGET_ID]
    if not target:
        die("STR# %d NOT FOUND in %s — options canon cannot be established" % (TARGET_ID, path))
    if len(target) > 1:
        die("STR# %d appears %d times" % (TARGET_ID, len(target)))
    t = target[0]
    if t["label"] != TARGET_LABEL:
        die("STR# %d found but label is %r, expected %r" % (TARGET_ID, t["label"], TARGET_LABEL))
    out.append("")
    out.append("################ TARGET ################")
    eng = dump_table(t, data, out, per_string_sha=True)

    # --- neighbor survey: STR# ids 140..160 --------------------------------
    out.append("")
    out.append("################ NEIGHBORS (STR# ids 140..160) ################")
    out.append("")
    out.append("id    label")
    neigh = [c for c in strn if c["id"] in NEIGHBOR_RANGE]
    for c in sorted(neigh, key=lambda c: c["id"]):
        out.append("%3d   %r" % (c["id"], c["label"]))
    out.append("")
    out.append("dumping full entries for labels containing %s:"
               % "/".join(repr(s) for s in DUMP_SUBSTRS))
    dumped = []
    for c in sorted(neigh, key=lambda c: c["id"]):
        if c["id"] == TARGET_ID:
            continue
        if any(s in c["label"].lower() for s in DUMP_SUBSTRS):
            dump_table(c, data, out, per_string_sha=False)
            dumped.append((c["id"], c["label"]))
    if not dumped:
        out.append("(none)")

    text = "\n".join(out) + "\n"
    sys.stdout.write(text)
    print("\nSUMMARY: STR# %d %r -> %d English entries; %d neighbors in 140..160; "
          "dumped option/help tables: %s"
          % (TARGET_ID, t["label"], len(eng), len(neigh),
             ", ".join("%d %r" % d for d in dumped) or "(none)"))


if __name__ == "__main__":
    main()
