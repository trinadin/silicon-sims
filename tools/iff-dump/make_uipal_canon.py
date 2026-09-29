#!/usr/bin/env python3
"""UI-33 uipal byte-pin canon: re-derive the ORIGINAL UI palette from the
original UIGraphics.far member bytes (engine-independent, read-only).

The R67-era OriginalUIPalette canon claimed 13 anchor colors from five
UIGraphics.far members but the uipal gate only range-checked UIStyle against
the hardcoded array — no byte re-derivation. This tool closes that: it parses
the FAR1 manifest (byte-verbatim, same idiom as make_r87_canon.py), extracts
the five anchor members, builds a per-member pixel histogram from the BMP
data, and VERIFIES every claimed anchor color against actual pixel counts.
Claims the original bytes do not support are reported and the tool exits
nonzero (honest-fail) — nothing is silently dropped or invented.

Emits (all under tools/iff-dump/uipal/):
  uipal-canon.json   full evidence (members, sha256, verified anchors + counts)
  uipal-canon.txt    human-readable evidence
  uipal-canon.cs     C# canon for AutotestCatalogCanon (byte pins + anchors)

No original asset is modified or copied into distribution output.
"""
import struct, hashlib, os, json, sys
from collections import Counter

FAR = "game-data/The Sims/UIGraphics/UIGraphics.far"
OUT = "tools/iff-dump/uipal"

# The R67/R83-era claimed anchors, with the member each is attributed to.
CLAIMS = [
    ("PanelBack navy anchors", [
        (0x000029, "cpanel\\Backgrounds\\PanelBack.bmp"),
        (0x000052, "cpanel\\Backgrounds\\PanelBack.bmp"),
        (0x00004A, "cpanel\\Backgrounds\\PanelBack.bmp"),
        (0x080852, "cpanel\\Backgrounds\\PanelBack.bmp"),
    ]),
    ("PanelBack steel-blue highlights", [
        (0x73739C, "cpanel\\Backgrounds\\PanelBack.bmp"),
        (0x636394, "cpanel\\Backgrounds\\PanelBack.bmp"),
        (0x5A5A8C, "cpanel\\Backgrounds\\PanelBack.bmp"),
    ]),
    ("CreateACharBack anchors", [
        (0x00186B, "cpanel\\Backgrounds\\CreateACharBack.BMP"),
        (0x00106B, "cpanel\\Backgrounds\\CreateACharBack.BMP"),
        (0x001063, "cpanel\\Backgrounds\\CreateACharBack.BMP"),
    ]),
    ("Steel-blue backing tones (R67 said 'motive-bar backing (PersBkg)'; the "
     "re-derivation locates both colors ONLY in the catalog sheet — "
     "attribution corrected)", [
        (0x72729E, "cpanel\\Catalog\\BuySTDining.bmp"),
        (0x7474A0, "cpanel\\Catalog\\BuySTDining.bmp"),
    ]),
    ("Cyan accent", [
        (0x00FFFF, "cpanel\\Buttons\\Mood.bmp"),
        (0x00FFFF, "cpanel\\Buttons\\JobFriendSmiley.bmp"),
    ]),
]

MEMBERS = sorted({m for _, pairs in CLAIMS for _, m in pairs})

MIN_PIXELS = 16  # a claimed anchor must appear in at least this many pixels


def read_far(path):
    """FAR1 manifest with inline member names (make_r87_canon.py idiom)."""
    data = open(path, "rb").read()
    man = struct.unpack("<I", data[12:16])[0]
    num = struct.unpack("<I", data[man:man + 4])[0]
    out = {}
    off = man + 4
    for _ in range(num):
        if off + 16 > len(data):
            break
        dlen, d2, doff, nlen = struct.unpack("<IIII", data[off:off + 16])
        nm = data[off + 16:off + 16 + nlen].decode("latin1", "replace")
        off += 16 + nlen
        if dlen != d2 or not (0 < doff < len(data) and doff + dlen <= len(data)):
            continue
        out[nm] = (dlen, doff)
    return data, out


def rle8_indices(b, off_bits, w, h):
    """Decode BI_RLE8 into a flat palette-index buffer (w*h, bottom-up origin).
    Delta/EOL semantics per the BMP spec; undecoded pixels stay index 0."""
    buf = [0] * (w * h)
    x, y, i, end = 0, h - 1, off_bits, False
    while i < len(b) - 1 and not end:
        cnt, idx = b[i], b[i + 1]
        i += 2
        if cnt > 0:
            for k in range(cnt):
                if x < w and 0 <= y < h:
                    buf[y * w + x] = idx
                x += 1
        elif idx == 0:
            y -= 1
            x = 0
        elif idx == 1:
            end = True
        elif idx == 2:
            dx, dy = b[i], b[i + 1]
            i += 2
            x += dx
            y -= dy
        else:
            for k in range(idx):
                if x < w and 0 <= y < h:
                    buf[y * w + x] = b[i + k]
                x += 1
            if idx % 2:
                i += 1  # absolute runs pad to a 2-byte boundary
            i += idx
    return buf


def bmp_histogram(b):
    """Minimal BMP RGB histogram: 8-bit (uncompressed or RLE8) + 24/32-bit
    BI_RGB. Orientation is irrelevant for a histogram. Returns
    Counter{(r,g,b): pixels} or raises on unsupported."""
    if b[0:2] != b"BM":
        raise ValueError("not a BMP")
    off_bits = struct.unpack("<I", b[10:14])[0]
    hsize = struct.unpack("<I", b[14:18])[0]
    w = struct.unpack("<i", b[18:22])[0]
    h = struct.unpack("<i", b[22:26])[0]
    bpp = struct.unpack("<H", b[28:30])[0]
    comp = struct.unpack("<I", b[30:34])[0]
    if w <= 0 or h == 0:
        raise ValueError("bad dims %dx%d" % (w, h))
    hist = Counter()
    if bpp == 8:
        pal_off = 14 + hsize
        pal = []
        for i in range(256):
            e = b[pal_off + i * 4: pal_off + i * 4 + 4]
            pal.append((e[2], e[1], e[0]))  # BGRX -> RGB
        if comp == 0:
            top_down = h < 0
            h = abs(h)
            stride = ((w * bpp + 31) // 32) * 4
            for row in range(h):
                y = row if top_down else h - 1 - row
                base = off_bits + y * stride
                for idx in b[base:base + w]:
                    hist[pal[idx]] += 1
        elif comp == 1:
            h = abs(h)
            for idx in rle8_indices(b, off_bits, w, h):
                hist[pal[idx]] += 1
        else:
            raise ValueError("unsupported 8-bit compression %d" % comp)
    elif bpp in (24, 32):
        if comp != 0:
            raise ValueError("unsupported compression %d" % comp)
        top_down = h < 0
        h = abs(h)
        stride = ((w * bpp + 31) // 32) * 4
        step = bpp // 8
        for row in range(h):
            y = row if top_down else h - 1 - row
            base = off_bits + y * stride
            for x in range(w):
                e = b[base + x * step: base + x * step + 3]
                hist[(e[2], e[1], e[0])] += 1
    else:
        raise ValueError("unsupported bpp %d" % bpp)
    return hist


def locate_color(rgb, data, members, exclude=(), limit=4):
    """Scan every FAR member that parses as a BMP for one exact RGB color.
    Returns [(name, pixels)] best-first, up to limit. Slow path, miss-only."""
    hits = []
    for nm, (dlen, doff) in members.items():
        if nm in exclude:
            continue
        try:
            h = bmp_histogram(data[doff:doff + dlen])
        except Exception:
            continue
        n = h.get(rgb, 0)
        if n >= MIN_PIXELS:
            hits.append((nm, n))
    hits.sort(key=lambda t: -t[1])
    return hits[:limit]


def main():
    os.makedirs(OUT, exist_ok=True)
    data, members = read_far(FAR)
    print("FAR members scanned: %d" % len(members))

    extracts = {}
    for m in MEMBERS:
        if m not in members:
            print("MISSING member: %s" % m)
            sys.exit(1)
        dlen, doff = members[m]
        raw = data[doff:doff + dlen]
        sha = hashlib.sha256(raw).hexdigest()
        try:
            hist = bmp_histogram(raw)
        except ValueError as e:
            print("PARSE FAIL %s: %s" % (m, e))
            sys.exit(1)
        extracts[m] = {
            "bytes": dlen, "sha256": sha, "pixels": sum(hist.values()),
            "distinct": len(hist),
            "top": [{"rgb": "%02X%02X%02X" % c, "n": n}
                    for c, n in hist.most_common(12)],
            "_hist": hist,
        }
        print("%-42s %8d B  %d px, %d distinct" % (m, dlen, sum(hist.values()), len(hist)))

    verified, misses = [], []
    for label, pairs in CLAIMS:
        for hexcol, member in pairs:
            hexstr = "%06X" % hexcol if isinstance(hexcol, int) else hexcol
            rgb = tuple(int(hexstr[i:i + 2], 16) for i in (0, 2, 4))
            n = extracts[member]["_hist"].get(rgb, 0)
            entry = {"claim": label, "rgb": "%02X%02X%02X" % rgb,
                     "member": member, "pixels": n}
            if n >= MIN_PIXELS:
                verified.append(entry)
            else:
                # honest re-derivation: locate the best container across the
                # WHOLE FAR (the R67 attribution may have named the wrong
                # member), not just the five claimed anchors
                best = locate_color(rgb, data, members,
                                    exclude={member} | set(extracts))
                entry["best_container"] = best[0][0] if best and best[0][1] >= MIN_PIXELS else None
                entry["best_pixels"] = best[0][1] if best else 0
                entry["locate_scan"] = [(m, n) for m, n in best[:4]]
                misses.append(entry)

    hist_out = {m: {k: v for k, v in d.items() if k != "_hist"} for m, d in extracts.items()}
    canon = {"far": FAR, "members": hist_out, "verified": verified, "misses": misses}
    with open(os.path.join(OUT, "uipal-canon.json"), "w") as f:
        json.dump(canon, f, indent=1)

    with open(os.path.join(OUT, "uipal-canon.txt"), "w") as f:
        f.write("uipal byte-pin canon (make_uipal_canon.py; %s)\n\n" % FAR)
        for m, d in extracts.items():
            f.write("%s\n  bytes=%d sha256=%s pixels=%d distinct=%d\n" % (m, d["bytes"], d["sha256"], d["pixels"], d["distinct"]))
            f.write("  top: %s\n" % ", ".join("%s x%d" % (t["rgb"], t["n"]) for t in d["top"][:8]))
        f.write("\nVERIFIED anchors (>= %d px):\n" % MIN_PIXELS)
        for v in verified:
            f.write("  %-34s %s in %s x%d\n" % (v["claim"], v["rgb"], v["member"], v["pixels"]))
        if misses:
            f.write("\nMISSES (claim not in attributed member):\n")
            for v in misses:
                f.write("  %-34s %s in %s -> %d px (best: %s)\n" % (
                    v["claim"], v["rgb"], v["member"], v["pixels"],
                    v.get("best_container") or "NOWHERE"))

    with open(os.path.join(OUT, "uipal-canon.cs"), "w") as f:
        f.write("// Generated by tools/iff-dump/make_uipal_canon.py — do not hand-edit.\n")
        f.write("// Byte-verbatim UIGraphics.far members (name+len+sha256) + verified anchor colors.\n")
        f.write("public static readonly R87ResourceCanon[] UIPaletteCanon = new R87ResourceCanon[] {\n")
        for m in MEMBERS:
            d = extracts[m]
            f.write('    new R87ResourceCanon("%s", %d, "%s"),\n' % (m.replace("\\", "\\\\"), d["bytes"], d["sha256"]))
        f.write("};\n")
        f.write("// (rgbHex, memberIndex) verified at >= %d px\n" % MIN_PIXELS)
        f.write("public static readonly (string rgb, int member)[] UIPaletteAnchors = new (string, int)[] {\n")
        for v in verified:
            f.write('    ("%s", %d),\n' % (v["rgb"], MEMBERS.index(v["member"])))
        f.write("};\n")

    print("\nverified=%d misses=%d" % (len(verified), len(misses)))
    for v in misses:
        print("  MISS %s in %s (%d px; best %s)" % (v["rgb"], v["member"], v["pixels"], v.get("best_container")))
    sys.exit(1 if misses else 0)


if __name__ == "__main__":
    main()
