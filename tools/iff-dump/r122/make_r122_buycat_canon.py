#!/usr/bin/env python3
"""R122 buy-catalog canon: byte-verbatim decode of GameData/UIText.iff STR# 150
'BuyModeCatalogSortTips' (the buy-mode catalog room/function sort + tip string
table), plus every other STR# table whose label case-insensitively contains one
of: buy / catalog / room / sort / function / category / build. Also prints the
full STR# inventory (id, label, raw declared count) and the label inventory for
ids 130..200 so the neighborhood of these tables is on record.

Idioms reused VERBATIM from r121/make_r121_optionstrs_canon.py (which itself
mirrors the gate's FindStrChunkByID/ParseLang1 in AutotestRunner.cs:4435/4462):
  - 60-byte Maxis header, first chunk @0x40, chunks walked linearly;
  - 76-byte chunk header: 4CC | u32 BE chunkSize | u16 BE chunkID | u16 BE flags
    | 64-byte null-padded label; body = chunkSize-76 bytes;
  - full-chunk sha256 hashes the 76-byte header TOO (gate convention: the
    gate's FindStrChunkByID returns the whole chunk);
  - STR# data is LITTLE-endian: i16 formatCode;
      0  -> u16 count + pascal strings (no language byte: all entries count as
             the game-index strings)
     -3 -> u16 count + {u8 langCode, cstring value, cstring comment}
    lang 1 = English; the game string index == position among lang-1 entries;
  - trailing bytes after the declared count are 0xA3 padding (FreeSo TS1 STR
    writer quirk) and must not be walked as entries.

FAILS LOUDLY (non-zero exit) if STR# 150 is absent, duplicated, or mislabeled.
"""
import struct, hashlib, sys, os, re

DEFAULT = ("/Users/nathannoom/Developer/Games/The Sims/simitone-fork/"
           "game-data/The Sims/GameData/UIText.iff")
TARGET_ID = 150
TARGET_LABEL = "BuyModeCatalogSortTips"
KEYWORDS = ("buy", "catalog", "room", "sort", "function", "category", "build")
NEIGHBOR_RANGE = range(130, 201)   # label-inventory window around the target
BONUS_IDS = [139]                  # 'BldTips' = build-mode tips, sibling of the
                                   # buy-mode target but its label contains none
                                   # of the KEYWORDS; dumped as a marked bonus.
LAYOUT_RE = re.compile(r"\(\d+;\d+\)")


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
    """Returns (formatCode, entries, trailer); raises ValueError on desync.
    entries are (lang_or_None, value_bytes, comment_bytes_or_None)."""
    if len(body) < 4:
        raise ValueError("STR# body too short (%d bytes)" % len(body))
    (fc,) = struct.unpack("<h", body[0:2])
    ents, p = [], 2
    if fc == 0:
        (count,) = struct.unpack("<H", body[p:p + 2]); p += 2
        for i in range(count):
            if p + 2 > len(body):
                raise ValueError("pascal entry %d desyncs" % i)
            (l,) = struct.unpack("<H", body[p:p + 2]); p += 2
            ents.append((None, body[p:p + l], None)); p += l
    elif fc == -3:
        (count,) = struct.unpack("<H", body[p:p + 2]); p += 2
        for i in range(count):
            if p >= len(body):
                raise ValueError("lang entry %d desyncs (p=%d, len=%d)" % (i, p, len(body)))
            lang = body[p]; p += 1
            val, p = cstring(body, p)
            com, p = cstring(body, p)
            ents.append((lang, val, com))
    else:
        raise ValueError("formatCode %d not handled" % fc)
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
    """lang-1 entries (format -3); for format 0 (no language byte) every entry
    is a game-index string, so all count as English."""
    return [(i, v) for i, (l, v, _) in enumerate(ents) if l == 1 or l is None]


def raw_declared_count(body):
    """u16 at body+2 (after the i16 format code) for both formats 0 and -3."""
    if len(body) < 4:
        return -1
    return struct.unpack("<H", body[2:4])[0]


def table_info(ch, data):
    body = data[ch["off"] + 76: ch["off"] + ch["size"]]
    raw = data[ch["off"]: ch["off"] + ch["size"]]
    info = {"ch": ch, "body": body, "raw": raw,
            "sha_full": hashlib.sha256(raw).hexdigest(),
            "sha_body": hashlib.sha256(body).hexdigest(),
            "declared": raw_declared_count(body)}
    try:
        fc, ents, trailer = decode_str_body(body)
    except ValueError as e:
        info["err"] = str(e)
        return info
    eng = english_entries(ents)
    info.update(fc=fc, ents=ents, trailer=trailer, eng=eng,
                langs=sorted({l for l, _, _ in ents if l is not None}))
    return info


def dump_table(ch, data, out):
    """Full per-table evidence block. English entries as 'idx<TAB>text'."""
    t = table_info(ch, data)
    out.append("")
    out.append("=== STR# %d %r (chunkSize %d; %d raw entries; format %s; %s) ==="
               % (ch["id"], ch["label"], ch["size"], t["declared"],
                  t.get("fc"), lang_layout_desc(t["ents"]) if "ents" in t else "?"))
    out.append("file range 0x%x..0x%x  flags=0x%04x"
               % (ch["off"], ch["off"] + ch["size"], ch["flags"]))
    out.append("full-chunk sha256 %s   (includes the 76-byte chunk header; gate convention)"
               % t["sha_full"])
    out.append("body-only  sha256 %s" % t["sha_body"])
    out.append("raw declared count: %d   English (lang-1) count: %d"
               % (t["declared"], len(t.get("eng", []))))
    if "err" in t:
        out.append("DECODE ERROR: %s" % t["err"])
        return t
    out.append("languages present: %s  (lang 1 = English)" % t["langs"])
    if t["eng"] and len(t["ents"]) % len(t["eng"]) == 0:
        out.append("raw %d = %d English x %d languages"
                   % (len(t["ents"]), len(t["eng"]), len(t["ents"]) // len(t["eng"])))
    if t["trailer"]:
        allpad = t["trailer"].strip(b"\xa3") == b""
        out.append("trailer: %d bytes, %s"
                   % (len(t["trailer"]), "all 0xA3 padding (FreeSo STR writer quirk)" if allpad
                      else "UNEXPECTED non-0xA3 bytes: %r" % t["trailer"][:32]))
    n_layout = sum(1 for _, v in t["eng"] if LAYOUT_RE.search(v.decode("mac_roman", "replace")))
    out.append("entries containing an '(x;y)' layout directive: %d of %d English"
               % (n_layout, len(t["eng"])))
    out.append("")
    out.append("English entries [0..%d], verbatim (mac_roman), as idx<TAB>text:"
               % (len(t["eng"]) - 1))
    for idx, (_raw_i, val) in enumerate(t["eng"]):
        s = val.decode("mac_roman", "replace")
        nonascii = "  <-- non-ASCII byte(s) " + val.hex() if any(b > 127 for b in val) else ""
        out.append("%d\t%s%s" % (idx, s, nonascii))
    return t


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else DEFAULT
    if not os.path.exists(path):
        # fall back to repo-relative location before giving up
        # (script lives at <repo>/tools/iff-dump/r122/, so 4 dirnames = repo root)
        root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(
            os.path.abspath(__file__)))))
        alt = os.path.join(root, "game-data", "The Sims", "GameData", "UIText.iff")
        if os.path.exists(alt):
            path = alt
        else:
            die("UIText.iff not found at %r (or %r)" % (path, alt))
    data = open(path, "rb").read()
    chunks = walk_chunks(data)
    strn = sorted([c for c in chunks if c["cc"] == b"STR#"], key=lambda c: c["id"])
    out = []
    out.append("R122 canon dump — UIText.iff STR# 150 'BuyModeCatalogSortTips' + "
               "all buy/catalog/room/sort/function/category/build-labeled STR# tables")
    out.append("Source: %s (%d bytes; full-file sha256 %s)"
               % (path, len(data), hashlib.sha256(data).hexdigest()))
    out.append("Parser: 60-byte Maxis header, first chunk @0x40, 76-byte chunk headers, "
               "BE sizes; STR# data LE, format -3 = language pairs "
               "{u8 lang, cstring value, cstring comment}, lang 1 = English; "
               "full-chunk sha includes the 76-byte header (gate convention).")
    out.append("Total chunks walked: %d (%d STR# tables)" % (len(chunks), len(strn)))

    # --- SECTION 1: full STR# inventory (id, label, raw declared count) ------
    out.append("")
    out.append("################ INVENTORY: ALL STR# TABLES (id<TAB>label<TAB>raw declared count) ################")
    out.append("")
    for c in strn:
        body = data[c["off"] + 76: c["off"] + c["size"]]
        out.append("%d\t%r\t%d" % (c["id"], c["label"], raw_declared_count(body)))

    # --- SECTION 2: the target, STR# 150 — FAIL LOUDLY if absent -------------
    out.append("")
    out.append("################ TARGET: STR# %d ################" % TARGET_ID)
    target = [c for c in strn if c["id"] == TARGET_ID]
    if not target:
        die("STR# %d NOT FOUND in %s — buy-catalog canon cannot be established" % (TARGET_ID, path))
    if len(target) > 1:
        die("STR# %d appears %d times" % (TARGET_ID, len(target)))
    t0 = target[0]
    if t0["label"] != TARGET_LABEL:
        die("STR# %d found but label is %r, expected %r" % (TARGET_ID, t0["label"], TARGET_LABEL))
    tgt = dump_table(t0, data, out)

    # --- SECTION 3: every OTHER keyword-matching table -----------------------
    out.append("")
    out.append("################ KEYWORD MATCHES (labels containing %s; target excluded) ################"
               % "/".join(KEYWORDS))
    matches = [c for c in strn
               if c["id"] != TARGET_ID
               and any(k in c["label"].lower() for k in KEYWORDS)]
    out.append("")
    out.append("matched: %s" % ", ".join("%d %r" % (c["id"], c["label"]) for c in matches))
    kw_info = []
    for c in matches:
        kw_info.append(dump_table(c, data, out))

    # --- SECTION 4: neighborhood label inventory, ids 130..200 ----------------
    out.append("")
    out.append("################ NEIGHBORHOOD: STR# ids 130..200 ################")
    out.append("")
    for c in strn:
        if c["id"] in NEIGHBOR_RANGE:
            out.append("%3d   %r" % (c["id"], c["label"]))

    # --- SECTION 5: bonus — BldTips (build-mode tips sibling) -----------------
    out.append("")
    out.append("################ BONUS (manual add; label contains none of the keywords) ################")
    for bid in BONUS_IDS:
        b = [c for c in strn if c["id"] == bid]
        if b:
            dump_table(b[0], data, out)

    text = "\n".join(out) + "\n"
    here = os.path.dirname(os.path.abspath(__file__))
    outpath = os.path.join(here, "r122-buycatalog-strs.txt")
    with open(outpath, "w") as f:
        f.write(text)
    sys.stdout.write(text)
    print("\nSUMMARY: STR# %d %r -> %d English entries (of %d raw); %d keyword tables: %s; "
          "wrote %s"
          % (TARGET_ID, t0["label"], len(tgt.get("eng", [])), tgt["declared"], len(matches),
             ", ".join("%d %r" % (c["id"], c["label"]) for c in matches), outpath))


if __name__ == "__main__":
    main()
