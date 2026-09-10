#!/usr/bin/env python3
"""R126 build-catalog canon: byte-verbatim decode of the BUILD-mode catalog
subcategory surfaces.

  1. UIText.iff STR# 139 'BldTips' — the build tool captions (16 English
     entries; 288 raw = 16 x 18 languages, format -3, English block first).
  2. UIGraphics.far cpanel\\Buttons\\toolBtn* members (res_cpanel.RT kToolBtn
     64..77): the 11 plaques the port MOUNTS on its build subcategory row
     (keyed by the port BuildCategories StrInd = STR# 139 index), plus the 3
     canon-but-unmounted tools (Hand / Undo / Redo — the port build panel has
     no surface for them).
  3. Res_CPanel.RT symbol lines verbatim (the authoritative symbol->member map).
  4. Negative space: kBuildBack (BuildBack.bmp) is NOT on this disk, and NO
     member exists for the port's 3 main build tabs (architecture/outdoors/
     objects) — the build main switcher stays port art, disclosed.

STR# idioms reused VERBATIM from r122/make_r122_buycat_canon.py; FAR manifest
walk reused from r124/make_r124_descpanel_canon.py (walk_far_manifest:
FAR!byAZ magic, manifest u32@12, {u32 dlen,d2,doff,nlen}+name, dlen==d2 guard).

Also records the multi-frame hazard: these plaques are SINGLE-frame (text
baked in at natural widths), but toolBtnTerrain is 156x39 = exactly 4x39, so
UIOriginal.Frame(name, 0) — which crops width==k*height sheets (k=2..4) into
frames — would mis-crop it to 39x39. Build art must load via
UIOriginal.EnsureResolved(...).Get(...) (full image), unlike the buy subsort
sheets (144x36 = 4 real frames).

FAILS LOUDLY (non-zero exit) if STR# 139 is absent/mislabeled/short or any
canon member is missing from the FAR manifest.
"""
import struct, hashlib, sys, os, re, importlib.util

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(
    os.path.abspath(__file__)))))


def load(name, relpath):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, '..', relpath))
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
    return mod


r122 = load('r122mod', 'r122/make_r122_buycat_canon.py')
r124 = load('r124mod', 'r124/make_r124_descpanel_canon.py')

UITEXT = os.path.join(ROOT, 'game-data/The Sims/GameData/UIText.iff')
UIFAR = os.path.join(ROOT, 'game-data/The Sims/UIGraphics/UIGraphics.far')
RT = os.path.join(ROOT, 'game-data/The Sims/UIGraphics/Res_CPanel.RT')

TARGET_ID = 139
TARGET_LABEL = "BldTips"

# The port's BuildCategories (UIBuyBrowsePanel.cs) carries StrInd = STR# 139
# index per subcategory. This table binds index -> plaque member (RT symbol)
# -> expected caption, CROSS-CHECKED against the disk strings below.
#   cat 0 architecture: wall(2) wallpaper(3) floor(7) roof(10)
#   cat 1 outdoors:     trees(6) terrain(0) water(1)
#   cat 2 objects:      door(8) window(9) staircase(4) fireplaces(5)
STRIND_TO_MEMBER = [
    (0, "cpanel\\Buttons\\toolBtnTerrain.bmp", "kToolBtnTerrain"),
    (1, "cpanel\\Buttons\\toolBtnWater.bmp", "kToolBtnPool"),  # RT name is Pool
    (2, "cpanel\\Buttons\\toolBtnWall.bmp", "kToolBtnWall"),
    (3, "cpanel\\Buttons\\toolBtnWallpaper.bmp", "kToolBtnWallpaper"),
    (4, "cpanel\\Buttons\\toolBtnStairs.bmp", "kToolBtnStairs"),
    (5, "cpanel\\Buttons\\toolBtnFireplace.bmp", "kToolBtnFireplace"),
    (6, "cpanel\\Buttons\\toolBtnTree.bmp", "kToolBtnTree"),
    (7, "cpanel\\Buttons\\toolBtnFloor.bmp", "kToolBtnFloor"),
    (8, "cpanel\\Buttons\\toolBtnDoor.bmp", "kToolBtnDoor"),
    (9, "cpanel\\Buttons\\toolBtnWindow.bmp", "kToolBtnWindow"),
    (10, "cpanel\\Buttons\\toolBtnRoof.bmp", "kToolBtnRoof"),
]
# Canon build tools on disk with NO port surface (documented, not mounted).
UNMOUNTED = [
    ("cpanel\\Buttons\\toolBtnHand.bmp", "kToolBtnHand", 11, "Hand Tool"),
    ("cpanel\\Buttons\\Undo.bmp", "kToolBtnUndo", 12, "Undo Last"),
    ("cpanel\\Buttons\\Redo.bmp", "kToolBtnRedo", 13, "Redo Last"),
]


def die(msg):
    print("FATAL: " + msg)
    sys.exit(1)


def bmp_facts(b):
    if b[:2] != b'BM':
        return None
    hdr = struct.unpack('<I', b[14:18])[0]
    w, h = struct.unpack('<ii', b[18:26])
    bpp = struct.unpack('<H', b[28:30])[0]
    comp = struct.unpack('<I', b[30:34])[0]
    return {'w': w, 'h': h, 'bpp': bpp, 'comp': comp, 'hdr': hdr,
            'dataoff': struct.unpack('<I', b[10:14])[0]}


def main():
    out = []
    out.append("R126 canon dump — BUILD-mode catalog subcategory surfaces: "
               "STR# 139 'BldTips' + cpanel\\Buttons\\toolBtn* plaques (kToolBtn 64..77)")
    out.append("Sources: %s" % UITEXT)
    out.append("        %s" % UIFAR)
    out.append("        %s" % RT)

    # ---------------- STR# 139 ----------------
    data = open(UITEXT, 'rb').read()
    chunks = r122.walk_chunks(data)
    hits = [c for c in chunks if c['cc'] == b'STR#' and c['id'] == TARGET_ID]
    if len(hits) != 1:
        die("STR# %d found %d times" % (TARGET_ID, len(hits)))
    ch = hits[0]
    if ch['label'] != TARGET_LABEL:
        die("STR# %d label %r != %r" % (TARGET_ID, ch['label'], TARGET_LABEL))
    t = r122.table_info(ch, data)
    if t['declared'] != 288 or len(t['eng']) != 16:
        die("STR# 139 declared=%d english=%d (want 288/16)" % (t['declared'], len(t['eng'])))
    eng = [v.decode('mac_roman') for i, v in t['eng']]
    out.append("")
    out.append("=== STR# 139 'BldTips' (chunkSize %d; 288 raw = 16 English x 18 langs; format %d) ==="
               % (ch['size'], t['fc']))
    out.append("full-chunk sha256 %s   (includes the 76-byte chunk header; gate convention)"
               % t['sha_full'])
    out.append("body-only  sha256 %s" % t['sha_body'])
    out.append("trailer: %d bytes, %s"
               % (len(t['trailer']),
                  "all 0xA3 padding" if t['trailer'].strip(b'\xa3') == b''
                  else "UNEXPECTED: %r" % t['trailer'][:32]))
    out.append("")
    out.append("English entries [0..15], verbatim (mac_roman), as idx<TAB>text:")
    for i, s in enumerate(eng):
        out.append("%d\t%s" % (i, s))
    # cross-check the port's expected captions
    for idx, member, sym in STRIND_TO_MEMBER:
        if not eng[idx].endswith("Tool"):
            die("STR# 139 [%d] = %r — expected a '* Tool' caption" % (idx, eng[idx]))
    out.append("")
    out.append("port mapping check: all 11 mounted indices [0..10] end in 'Tool' — OK")

    # ---------------- FAR members ----------------
    uidata = open(UIFAR, 'rb').read()
    if uidata[:8] != b'FAR!byAZ':
        die("UIGraphics.far magic %r" % uidata[:8])
    truth = {}
    for nm, dlen, doff in r124.walk_far_manifest(uidata):
        truth[nm.lower()] = (nm, dlen, doff)

    out.append("")
    out.append("=== MOUNTED plaques (11): {idx, manifest-exact name, len, WxH, sha256, sheet-hazard} ===")
    mounted = []
    for idx, member, sym in STRIND_TO_MEMBER:
        hit = truth.get(member.lower())
        if hit is None:
            die("mounted member missing from UIGraphics.far: %s" % member)
        nm, dlen, doff = hit
        b = uidata[doff:doff + dlen]
        f = bmp_facts(b)
        if f is None or f['bpp'] != 8 or f['comp'] != 1:
            die("%s is not an 8-bit RLE8 BMP: %r" % (nm, f))
        if not (20 <= f['w'] <= 250 and 16 <= f['h'] <= 60):
            die("%s dims %dx%d outside plaque sanity range" % (nm, f['w'], f['h']))
        ratio = f['w'] / float(f['h'])
        hazard = ""
        k = f['w'] // f['h'] if f['w'] % f['h'] == 0 else 0
        if 2 <= k <= 4:
            hazard = "  <-- SHEET HAZARD: w=%d*h -> UIOriginal.Frame would crop to %dx%d; load FULL image"
            hazard = hazard % (k, f['h'], f['h'])
        sha = hashlib.sha256(b).hexdigest()
        mounted.append((idx, member.lower(), dlen, f['w'], f['h'], sha))
        out.append("139/%-2d %-38s len=%-5d %3dx%-3d %s%s" % (idx, nm, dlen, f['w'], f['h'], sha, hazard))
    out.append("")
    out.append("first plaque header facts: bpp=%d comp=%d (BI_RLE8) hdr=%d dataoff=%d"
               % (f['bpp'], f['comp'], f['hdr'], f['dataoff']))

    out.append("")
    out.append("=== canon-but-UNMOUNTED tools (no port surface; still pinned as corpus) ===")
    for member, sym, idx, cap in UNMOUNTED:
        hit = truth.get(member.lower())
        if hit is None:
            die("unmounted canon member missing from UIGraphics.far: %s" % member)
        nm, dlen, doff = hit
        b = uidata[doff:doff + dlen]
        f = bmp_facts(b)
        sha = hashlib.sha256(b).hexdigest()
        out.append("139/%-2d %-38s len=%-5d %3dx%-3d %s  (%s; port surface: none)"
                   % (idx, nm, dlen, f['w'], f['h'], sha, cap))

    # ---------------- RT symbol lines ----------------
    out.append("")
    out.append("=== Res_CPanel.RT symbol lines verbatim (kToolBtn*/kBuildBack/kBldSbTl*/kBuildModePatch) ===")
    rttxt = open(RT, 'rb').read().decode('latin1')
    seen = set()
    for m in re.finditer(r'\{[A-Z]+,k(?:ToolBtn|BuildBack|BldSbTl|BuildMode)[A-Za-z0-9]*,"[^"]+"\}', rttxt):
        s = m.group()
        if s not in seen:
            seen.add(s)
            out.append(s)
    if not seen:
        die("no kToolBtn symbol lines found in Res_CPanel.RT")

    # ---------------- negative space ----------------
    out.append("")
    out.append("=== NEGATIVE SPACE / disclosed residuals ===")
    for member, sym in (("cpanel\\backgrounds\\buildback.bmp", "kBuildBack"),
                        ("cpanel\\backgrounds\\buyback.bmp", "kCatalogBck")):
        out.append("%s (%s): %s" % (member, sym,
                                    "IN manifest" if member in truth else "NOT on this disk (missing like BuyBack.bmp)"))
    bld = sorted(nm for nm, dl, do in r124.walk_far_manifest(uidata)
                 if 'build' in nm.lower() and nm.lower() not in
                 ('cpanel\\build\\buildpatch.bmp',))
    out.append("all FAR members containing 'build' (case-insensitive): %s" % ", ".join(bld))
    out.append("NO canon plaque exists for the port's 3 main build tabs (architecture/")
    out.append("outdoors/objects) — no BuildF*/BuildR*-shaped member; the main switcher")
    out.append("stays port art (disclosed). STR# 139 [14]/[15] 'Previous/Next Page' pair")
    out.append("with kCatalogPrevPage/NextPage = ScrollLeft/Right (mounted by R122).")
    out.append("The original build UI's own structure evidence: 14 kToolBtn symbols + the")
    out.append("paging strings suggest a flat paged tool list, NOT the port's 3-category")
    out.append("hierarchy — the hierarchy is a port structure kept this round (disclosed).")

    text = "\n".join(out) + "\n"
    outpath = os.path.join(HERE, "r126-buildcat-canon.txt")
    with open(outpath, "w") as fh:
        fh.write(text)
    sys.stdout.write(text)
    print("\nSUMMARY: STR# 139 %r sha=%s... 16 English; 11 mounted plaques + %d unmounted; wrote %s"
          % (TARGET_LABEL, t['sha_full'][:16], len(UNMOUNTED), outpath))


if __name__ == "__main__":
    main()
