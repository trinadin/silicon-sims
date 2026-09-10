#!/usr/bin/env python3
"""R127 build-tool canon: byte-verbatim decode of the BUILD-mode terrain tool
ITEM surfaces (the catalog cells inside the Terrain subcategory).

  1. The 8 MOUNTED members from UIGraphics.far (res_cpanel.RT kBldSbTl* cell
     plaques + kBldPopup* thumbs; mapping cross-checked against the FSO
     provider's own TSO comments — icon=plaque, thumb=popup):
       id 0 raise -> cpanel\\Build\\TerrainUpIcon.BMP    / PopupTerrainUp.bmp
       id 1 level -> cpanel\\Build\\TerrainLevelIcon.BMP / PopupTerrainLevel.bmp
       id 2 grass -> cpanel\\HDBuild\\TerrainGrassIcon.BMP / popupGrass.bmp (Hot Date)
       id 3 lower -> cpanel\\Build\\TerrainDownIcon.BMP  / PopupTerrainDown.bmp
     (the port merged raise/lower into one tool; R127 splits them back — the
     original has distinct icons AND distinct popups per direction)
  2. rle8_ascii reads of all 8 — the plaques/popups carry baked text; this is
     the only name/instruction source on disk if UIText has no table.
  3. A UIText.iff scan: every STR# English entry containing terrain/grass/
     raise/lower/flatten/level — to find any original name table (the port
     names are hardcoded English; TSO used f107, absent here).
  4. Corpus facts (documented, NOT gate-pinned — future rounds): the roof
     family (roofpatterntemplate 180x45 = 4x45 sheet, popuproofpattern,
     popuproofpitch, roofflat/shallow/medium/steep) + Hand/Pool/Water icons
     and popups.
  5. Res_CPanel.RT symbol lines verbatim.

STR# idioms reused from r122/make_r122_buycat_canon.py; FAR walk + rle8_ascii
reused from r124/make_r124_descpanel_canon.py. FAILS LOUDLY if any MOUNTED
member is missing or not an 8-bit RLE8 BMP.
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

# (id, label, icon member, thumb member)
MOUNTED = [
    (0, "raise", "cpanel\\Build\\TerrainUpIcon.BMP", "cpanel\\Build\\PopupTerrainUp.bmp"),
    (1, "level", "cpanel\\Build\\TerrainLevelIcon.BMP", "cpanel\\Build\\PopupTerrainLevel.bmp"),
    (2, "grass", "cpanel\\HDBuild\\TerrainGrassIcon.BMP", "cpanel\\HDBuild\\popupGrass.bmp"),
    (3, "lower", "cpanel\\Build\\TerrainDownIcon.BMP", "cpanel\\Build\\PopupTerrainDown.bmp"),
]
# documented-only (no mount this round)
CORPUS = [
    "cpanel\\Build\\HandIcon.bmp", "cpanel\\Build\\PoolToolIcon.bmp",
    "cpanel\\Build\\PopupHand.bmp", "cpanel\\Build\\PopupPool.BMP",
    "cpanel\\Build\\popuproofpattern.bmp", "cpanel\\Build\\popuproofpitch.bmp",
    "cpanel\\Build\\roofflat.bmp", "cpanel\\Build\\roofshallow.bmp",
    "cpanel\\Build\\roofmedium.bmp", "cpanel\\Build\\roofsteep.bmp",
    "cpanel\\Build\\roofpatterntemplate.bmp",
    "cpanel\\HDBuild\\WaterToolIcon.bmp", "cpanel\\HDBuild\\PopupWaterTool.bmp",
]
NAME_WORDS = ("terrain", "grass")


def die(msg):
    print("FATAL: " + msg)
    sys.exit(1)


def bmp_facts(b):
    if b[:2] != b'BM':
        return None
    hdr = struct.unpack('<I', b[14:18])[0]
    w, h = struct.unpack('<ii', b[18:26])
    return {'w': w, 'h': h, 'bpp': struct.unpack('<H', b[28:30])[0],
            'comp': struct.unpack('<I', b[30:34])[0], 'hdr': hdr}


def main():
    out = []
    out.append("R127 canon dump — BUILD-mode terrain tool items: kBldSbTl* plaques + kBldPopup* thumbs")
    out.append("Sources: %s" % UIFAR)
    out.append("        %s" % UITEXT)
    out.append("        %s" % RT)

    uidata = open(UIFAR, 'rb').read()
    if uidata[:8] != b'FAR!byAZ':
        die("UIGraphics.far magic %r" % uidata[:8])
    truth = {}
    for nm, dlen, doff in r124.walk_far_manifest(uidata):
        truth[nm.lower()] = (nm, dlen, doff)

    def fetch(member):
        hit = truth.get(member.lower())
        if hit is None:
            die("member missing from UIGraphics.far: %s" % member)
        nm, dlen, doff = hit
        b = uidata[doff:doff + dlen]
        f = bmp_facts(b)
        if f is None or f['bpp'] != 8 or f['comp'] != 1:
            die("%s is not an 8-bit RLE8 BMP: %r" % (nm, f))
        return nm, dlen, b, f

    out.append("")
    out.append("=== MOUNTED members (8): id/role, manifest name, len, WxH, sha256, RLE8 ASCII read ===")
    for tid, label, icon, thumb in MOUNTED:
        for role, member in (("icon", icon), ("thumb", thumb)):
            nm, dlen, b, f = fetch(member)
            sha = hashlib.sha256(b).hexdigest()
            out.append("")
            out.append("id %d %-5s %s  %s  len=%d  %dx%d" % (tid, role, nm, label, dlen, f['w'], f['h']))
            out.append("sha256 %s" % sha)
            pal, pix, rows, facts = r124.rle8_ascii(b, cols=120, rows=(12 if role == "icon" else 26))
            out.append("RLE8 ASCII (baked text read; 256-color, first palette entry is the transparency-key color):")
            for r in rows:
                out.append("  " + r)

    out.append("")
    out.append("=== CORPUS facts (documented, NOT gate-pinned — future rounds) ===")
    for member in CORPUS:
        nm, dlen, b, f = fetch(member)
        out.append("%-46s len=%-5d %3dx%-3d %s" % (nm, dlen, f['w'], f['h'], hashlib.sha256(b).hexdigest()))
    out.append("roofpatterntemplate is 180x45 = 4x45: a 4-frame 45x45 sheet like ThumbTemplate —")
    out.append("the original roof cell composes a swatch INSIDE this template (a compose round).")
    out.append("popuproofpitch + roofflat/shallow/medium/steep = the pitch selection UI (navigation round).")

    # ---- UIText scan for original tool-name strings ----
    out.append("")
    out.append("=== UIText.iff scan: STR# English entries naming terrain tools ===")
    data = open(UITEXT, 'rb').read()
    chunks = r122.walk_chunks(data)
    strn = [c for c in chunks if c['cc'] == b'STR#']
    hits = 0
    for c in sorted(strn, key=lambda c: c['id']):
        t = r122.table_info(c, data)
        if 'eng' not in t:
            continue
        for idx, (_ri, val) in enumerate(t['eng']):
            s = val.decode('mac_roman', 'replace')
            low = s.lower()
            if any(w in low for w in NAME_WORDS) or re.match(r'^(raise|lower|flatten|level)\b', low):
                out.append("STR# %-4d %-40r [%d] %r" % (c['id'], c['label'], idx, s))
                hits += 1
    out.append("matches: %d" % hits)
    if hits == 0:
        out.append("NO original tool-name strings exist in UIText.iff — the names live in the")
        out.append("plaques/popups as baked art (RLE8 reads above); port names stay port-authored (disclosed).")

    # ---- RT symbol lines ----
    out.append("")
    out.append("=== Res_CPanel.RT symbol lines verbatim (kBldSbTl*/kBldPopup*/kRoofPattern*) ===")
    rttxt = open(RT, 'rb').read().decode('latin1')
    seen = set()
    for m in re.finditer(r'\{[A-Z]+,k(?:BldSbTl|BldPopup|RoofPattern|ToolBtn)[A-Za-z0-9]*,"[^"]+"\}', rttxt):
        s = m.group()
        if s not in seen:
            seen.add(s)
            out.append(s)
    if not seen:
        die("no kBld symbol lines found in Res_CPanel.RT")

    text = "\n".join(out) + "\n"
    outpath = os.path.join(HERE, "r127-terraintools-canon.txt")
    with open(outpath, "w") as fh:
        fh.write(text)
    sys.stdout.write(text)
    print("\nSUMMARY: 8 mounted members decoded (+%d corpus); wrote %s" % (len(CORPUS), outpath))


if __name__ == "__main__":
    main()
