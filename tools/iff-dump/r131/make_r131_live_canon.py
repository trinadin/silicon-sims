#!/usr/bin/env python3
"""R131 live-gauge canon: byte-verbatim decode of the LIVE-TAB mood gauge art.

  1. Full inventory of cpanel\\backgrounds\\* in UIGraphics.far (the family the
     gauge backdrop lives in) + every gauge/mood/live-flavored cpanel member.
  2. Byte pins + RLE8 ASCII reads of LiveGadget.bmp (kLiveModeGauge backdrop,
     RT id 4911) and its composition siblings if the art reveals them.
  3. Res_CPanel.RT lines for the gauge/level-button families verbatim.
  4. UIText.iff STR scan: mood/gauge strings (does the gauge carry labels?).

FAR walk + rle8_ascii reused from r124; STR scan idiom from r122 (r130 shape).
"""
import struct, hashlib, sys, os, importlib.util

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(
    os.path.abspath(__file__)))))


def load(name, relpath):
    spec = importlib.util.spec_from_file_location(
        name, os.path.join(HERE, '..', relpath))
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
    return mod


r124 = load('r124mod', 'r124/make_r124_descpanel_canon.py')
r122 = load('r122mod', 'r122/make_r122_buycat_canon.py')

UITEXT = os.path.join(ROOT, 'game-data/The Sims/GameData/UIText.iff')
UIFAR = os.path.join(ROOT, 'game-data/The Sims/UIGraphics/UIGraphics.far')
RT = os.path.join(ROOT, 'game-data/The Sims/UIGraphics/Res_CPanel.RT')

FLAVOR = ("gadget", "gauge", "mood", "live", "flame", "diamond", "plumb",
          "pointer", "needle", "bar", "level")
NAME_WORDS = ("mood", "gauge")


def die(msg):
    print("FATAL: " + msg)
    sys.exit(1)


def bmp_facts(b):
    if b[:2] != b'BM':
        return None
    comp = struct.unpack('<I', b[30:34])[0]
    w, h = struct.unpack('<i i'.replace(' ', ''), b[18:26])
    bpp = struct.unpack('<H', b[28:30])[0]
    colors = struct.unpack('<I', b[46:50])[0]
    return dict(w=w, h=h, bpp=bpp, comp=comp, colors=colors)


def plain8_rows(b, want_w, want_h):
    off = struct.unpack('<I', b[10:14])[0]
    stride = (want_w * 1 + 3) & ~3
    out = []
    for y in range(want_h):
        row = b[off + (want_h - 1 - y) * stride:
                off + (want_h - 1 - y) * stride + want_w]
        out.append(''.join(chr(c) if 32 <= c < 127 else '.' for c in row))
    return out


uidata = open(UIFAR, 'rb').read()
if uidata[:8] != b'FAR!byAZ':
    die("UIGraphics.far magic %r" % uidata[:8])
far = r124.walk_far_manifest(uidata)
lower = {name.replace('/', '\\').lower(): (name, dlen, doff)
         for name, dlen, doff in far}


def fetch(member):
    key = member.replace('/', '\\').lower()
    if key not in lower:
        die("member missing from FAR: " + member)
    name, dlen, doff = lower[key]
    return uidata[doff:doff + dlen]


print("=== R131 cpanel\\backgrounds INVENTORY (UIGraphics.far) ===")
fam = sorted(k for k in lower if k.startswith('cpanel\\backgrounds\\'))
for k in fam:
    name, dlen, doff = lower[k]
    b = uidata[doff:doff + dlen]
    f = bmp_facts(b)
    dims = ("%dx%d comp=%d" % (f['w'], f['h'], f['comp'])) if f else "NOT-BMP"
    print(f"{k:56s} len={len(b):6d} {dims} sha256={hashlib.sha256(b).hexdigest()}")

print("\n=== gauge/mood/live-flavored cpanel members (all dirs) ===")
for k in sorted(lower):
    base = k.rsplit('\\', 1)[-1]
    if any(n in base for n in FLAVOR):
        name, dlen, doff = lower[k]
        b = uidata[doff:doff + dlen]
        f = bmp_facts(b)
        dims = ("%dx%d comp=%d" % (f['w'], f['h'], f['comp'])) if f else "NOT-BMP"
        print(f"{k:56s} len={len(b):6d} {dims}")

# ---- byte pins + ASCII reads: the gauge family ----
READS = ["cpanel\\backgrounds\\livegadget.bmp"]
print("\n=== RLE8/PLAIN ASCII READS ===")
for m in READS:
    b = fetch(m)
    facts = bmp_facts(b)
    print(f"\n---- {m} ({facts['w']}x{facts['h']} comp={facts['comp']} "
          f"colors={facts['colors']}) len={len(b)} sha256={hashlib.sha256(b).hexdigest()} ----")
    if facts['comp'] == 1 and facts['bpp'] == 8:
        try:
            pal, pix, rows, rfacts = r124.rle8_ascii(
                b, cols=min(160, facts['w']), rows=facts['h'])
            for r in rows:
                print(r)
            print("rle8 facts: %r" % (rfacts,))
            print("palette[0..15]: %s" % ' '.join('%02x%02x%02x' % tuple(c) for c in pal[:16]))
        except Exception as ex:
            print("rle8_ascii failed: %r" % ex)
    elif facts['comp'] == 0 and facts['bpp'] == 8:
        for line in plain8_rows(b, facts['w'], facts['h']):
            print(line)
    else:
        print("(unhandled combination; raw head: %s)" % b[:64].hex())

print("\n=== Res_CPanel.RT gauge/level/live lines verbatim ===")
rt = open(RT, 'rb').read().decode('latin1')
for line in rt.splitlines():
    if any(w in line for w in ("Gauge", "gauge", "Level", "Live", "Mood")):
        print(line)

print("\n=== UIText.iff scan: mood/gauge strings ===")
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
