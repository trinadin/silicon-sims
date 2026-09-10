#!/usr/bin/env python3
"""R130 roof canon: byte-verbatim decode of the BUILD-mode ROOF surfaces.

  1. The 7 roof family members from UIGraphics.far (res_cpanel.RT symbols
     kRoofPatternTemplate + kBldSbTlRoof{Flat,Shallow,Medium,Steep} +
     kBldPopupRoof{Pattern,Pitch}):
       cpanel\\Build\\roofpatterntemplate.bmp  180x45 (RT type {BITMAP}, the others RLEBMP)
       cpanel\\Build\\roofflat.bmp             100x16
       cpanel\\Build\\roofshallow.bmp          104x16
       cpanel\\Build\\roofmedium.bmp           104x17
       cpanel\\Build\\roofsteep.bmp            104x23
       cpanel\\Build\\popuproofpattern.bmp     145x103
       cpanel\\Build\\popuproofpitch.bmp       133x103
  2. rle8_ascii reads — do the plaques carry baked text (Flat/Shallow/...)? is
     the template a 45x45-cell border sheet (which frame layout)?
  3. GameData/Roofs facts — the pattern swatch source (count, dims, order).
  4. Res_CPanel.RT symbol lines verbatim (roof family).

FAR walk + rle8_ascii reused from r124; STR# idioms from r122 (not needed
here, but the loader stays symmetric with r127).
"""
import struct, hashlib, sys, os, importlib.util

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(
    os.path.abspath(__file__)))))


def load(name, relpath):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, '..', relpath))
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
    return mod


r124 = load('r124mod', 'r124/make_r124_descpanel_canon.py')
r122 = load('r122mod', 'r122/make_r122_buycat_canon.py')

UITEXT = os.path.join(ROOT, 'game-data/The Sims/GameData/UIText.iff')
UIFAR = os.path.join(ROOT, 'game-data/The Sims/UIGraphics/UIGraphics.far')
RT = os.path.join(ROOT, 'game-data/The Sims/UIGraphics/Res_CPanel.RT')
ROOFS = os.path.join(ROOT, 'game-data/The Sims/GameData/Roofs')

NAME_WORDS = ("roof", "pitch", "shallow", "steep")

MEMBERS = [
    "cpanel\\Build\\roofpatterntemplate.bmp",
    "cpanel\\Build\\roofflat.bmp",
    "cpanel\\Build\\roofshallow.bmp",
    "cpanel\\Build\\roofmedium.bmp",
    "cpanel\\Build\\roofsteep.bmp",
    "cpanel\\Build\\popuproofpattern.bmp",
    "cpanel\\Build\\popuproofpitch.bmp",
]


def die(msg):
    print("FATAL: " + msg)
    sys.exit(1)


def bmp_facts(b):
    if b[:2] != b'BM':
        return None
    comp = struct.unpack('<I', b[30:34])[0]
    w, h = struct.unpack('<ii', b[18:26])
    bpp = struct.unpack('<H', b[28:30])[0]
    colors = struct.unpack('<I', b[46:50])[0]
    return dict(w=w, h=h, bpp=bpp, comp=comp, colors=colors)


def plain8_rows(b, want_w, want_h):
    """Fallback: uncompressed bottom-up 8-bit rows -> ASCII (1 char/pixel)."""
    off = struct.unpack('<I', b[10:14])[0]
    stride = (want_w * 1 + 3) & ~3
    out = []
    for y in range(want_h):
        row = b[off + (want_h - 1 - y) * stride: off + (want_h - 1 - y) * stride + want_w]
        out.append(''.join(chr(c) if 32 <= c < 127 else '.' for c in row))
    return out


uidata = open(UIFAR, 'rb').read()
if uidata[:8] != b'FAR!byAZ':
    die("UIGraphics.far magic %r" % uidata[:8])
far = r124.walk_far_manifest(uidata)
# the manifest mixes '/' and '\\' separators (and chaotic case); normalize.
# NOTE: walk_far_manifest yields (name, dlen, doff) — length BEFORE offset.
lower = {name.replace('/', '\\').lower(): (name, dlen, doff)
         for name, dlen, doff in far}


def fetch(member):
    key = member.replace('/', '\\').lower()
    if key not in lower:
        die("member missing from FAR: " + member)
    name, dlen, doff = lower[key]
    return uidata[doff:doff + dlen]


print("=== R130 ROOF FAMILY CANON (UIGraphics.far, byte-verbatim) ===")
blobs = {}
for m in MEMBERS:
    b = fetch(m)
    blobs[m] = b
    facts = bmp_facts(b)
    if facts is None:
        die("not a BMP: " + m)
    print(f"{m:46s} len={len(b):5d} {facts['w']}x{facts['h']} "
          f"bpp={facts['bpp']} comp={facts['comp']} colors={facts['colors']} "
          f"sha256={hashlib.sha256(b).hexdigest()}")

print("\n=== RLE8 ASCII READS ===")
for m in MEMBERS:
    b = blobs[m]
    facts = bmp_facts(b)
    print(f"\n---- {m} ({facts['w']}x{facts['h']} comp={facts['comp']}) ----")
    if facts['comp'] == 1 and facts['bpp'] == 8:
        try:
            pal, pix, rows, rfacts = r124.rle8_ascii(
                b, cols=min(160, facts['w']), rows=facts['h'])
            for r in rows:
                print(r)
        except Exception as ex:
            print("rle8_ascii failed: %r" % ex)
    elif facts['comp'] == 0 and facts['bpp'] == 8:
        for line in plain8_rows(b, facts['w'], facts['h']):
            print(line)
    else:
        print("(unhandled combination; raw head: %s)" % b[:64].hex())

print("\n=== GameData/Roofs pattern swatch corpus ===")
names = sorted(os.listdir(ROOFS))
print("count=%d" % len(names))
for n in names[:60]:
    with open(os.path.join(ROOFS, n), 'rb') as f:
        b = f.read(64)
    if b[:2] != b'BM':
        print(f"{n:32s} NOT-BMP")
        continue
    w, h = struct.unpack('<ii', b[18:26])
    bpp = struct.unpack('<H', b[28:30])[0]
    comp = struct.unpack('<I', b[30:34])[0]
    print(f"{n:32s} {w}x{h} bpp={bpp} comp={comp}")

print("\n=== Res_CPanel.RT roof symbol lines verbatim ===")
rt = open(RT, 'rb').read().decode('latin1')
for line in rt.splitlines():
    if 'Roof' in line or 'roof' in line:
        print(line)

print("\n=== UIText.iff scan: English entries naming roofs/pitch ===")
import re
hits = 0
tdata = open(UITEXT, 'rb').read()
for c in sorted((c for c in r122.walk_chunks(tdata) if c['cc'] == b'STR#'),
                key=lambda c: c['id']):
    t = r122.table_info(c, tdata)
    if 'eng' not in t:
        continue
    for idx, (_ri, val) in enumerate(t['eng']):
        s = val.decode('mac_roman', 'replace')
        low = s.lower()
        if any(w in low for w in NAME_WORDS):
            print("STR# %-4d %-40r [%d] %r" % (c['id'], c['label'], idx, s))
            hits += 1
print("matches: %d" % hits)
if hits == 0:
    print("NO original roof/pitch name strings in UIText.iff — plaques are pictorial")
    print("(RLE8 reads above); port names stay port-authored (disclosed).")
