#!/usr/bin/env python3
# R119 'uivfont' canon: metrics for the regular variablesans ladder tables the
# OriginalVectorFont family mounts (07,08,09,10,11,12,14,16,18,20,48), read
# BYTE-VERBATIM from the R87 canon extraction (tools/iff-dump/uigr-orig/0450..0464
# = English Fonts\variablesans_NN.ffn from UIGraphics.far, sha-pinned by the
# 'uiglyph' gate). Advance convention is R109's uniform w+t0 with space=5px --
# the exact semantics of OriginalGlyphFont.Advance (UIOriginalText.cs:176-181).
# Known-value cross-checks (must reproduce prior rounds' pins exactly):
#   _08 "The Sims"=54 (R106), _10 "Lot 20"=43 (R97), _11 "12:05 AM"=69 (R99),
#   atlas dims _08 256x107, _09 256x115, _10 256x121, _11 256x152,
#   _14 256x224 (round evidence; r96-format-notes.md quotes _07 as 256x84 --
#   stale prose, the bytes AND the shipping parser (UIOriginalText.cs:89-90)
#   both read 256x83; _07 was never gate-pinned).
# _48 is computed for reference only: 16 glyphs (letters+space, NO digits) --
# the wordmark display font, unfit for general text, so the R119 ladder caps
# at _20 and never selects _48.
import struct, sys

BASE = "tools/iff-dump/uigr-orig/"
LADDER = [("07", "0450_07.ffn"), ("08", "0451_08.ffn"), ("09", "0452_09.ffn"),
          ("10", "0453_10.ffn"), ("11", "0454_11.ffn"), ("12", "0457_12.ffn"),
          ("14", "0459_14.ffn"), ("16", "0461_16.ffn"), ("18", "0462_18.ffn"),
          ("20", "0463_20.ffn")]  # 48 omitted from ladder: 16-char charset
PROBES = ["The Sims", "Lot 20", "12:05 AM", "Options", "Bella", "1234567"]

def parse(path):
    d = open(path, "rb").read()
    assert d[0:4] == b"FNTF", "not FNTF"
    num_chars = struct.unpack_from("<H", d, 10)[0]
    dir_off = struct.unpack_from("<I", d, 20)[0]
    shape_off = struct.unpack_from("<I", d, 28)[0]
    adv = {}
    for i in range(num_chars):
        p = dir_off + i * 11
        ch, w, h, u, v, t0, t1, t2 = struct.unpack_from("<HBBHHhbb" if False else "<HBBHHbbb", d, p)
        adv[ch] = (w + t0) if w > 1 else (5 if ch == 32 else 0)
    atlas_w, atlas_h = struct.unpack_from("<HH", d, shape_off + 4)
    return adv, atlas_w, atlas_h, num_chars

def measure(adv, s):
    return sum(adv.get(ord(c), 0) for c in s)

known = {("08", "The Sims"): 54, ("10", "Lot 20"): 43, ("11", "12:05 AM"): 69,
         ("07", "atlas"): (256, 83), ("08", "atlas"): (256, 107),
         ("09", "atlas"): (256, 115), ("10", "atlas"): (256, 121),
         ("11", "atlas"): (256, 152), ("14", "atlas"): (256, 224)}
fails = []
for name, fn in LADDER:
    adv, aw, ah, nc = parse(BASE + fn)
    if (name, "atlas") in known and known[(name, "atlas")] != (aw, ah):
        fails.append(f"{name} atlas {aw}x{ah} != known {known[(name,'atlas')]}")
    line = f"variablesans_{name}: chars={nc} atlas={aw}x{ah}"
    for probe in PROBES:
        m = measure(adv, probe)
        line += f" | '{probe}'={m}"
        if (name, probe) in known and known[(name, probe)] != m:
            fails.append(f"{name} '{probe}' {m} != known {known[(name,probe)]}")
    print(line)
if fails:
    print("CROSS-CHECK FAILURES:", fails); sys.exit(1)
print("all known-value cross-checks reproduced")
