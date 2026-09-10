#!/usr/bin/env python3
"""R132 gate-canon regenerator (the r121-r131 pattern): derives every 'uirate'
art pin in AutotestRunner.cs FROM THE DISK BYTES and writes the marker block.
NEVER hand-edit the generated block.

Pins (R132RateArt): the People-panel ratings/tracking art family from
UIGraphics.far ({lowercase full path, byte length, W, H, sha256}):
  - the 3 PIE strips: Buttons/HouseBars.bmp / JobBars.bmp / RelBars.bmp
    (kHouseBars 4800 / kJobBars 4801 / kRelBars 4802, 63x26 RLE8)
  - the subpanel bar sheets: HouseSubBars.bmp (kHouseSubpanelBars 4900,
    40x16 24bpp), JobSubBars.bmp (kJobSubpanelBars 4901, 40x11 24bpp),
    FameSubBars.bmp (kFameSubpanelBars 4822, 80x16 RLE8)
  - the Off backdrops: Backgrounds/HouseSubBarsOff.BMP (4919, 40x16),
    JobSubBarsOff.bmp (4920, 40x11), FameSubBarsOff.bmp (4823, 80x16)
(cpanel\\People\\TrackingTarget.bmp 45x45 stays pinned by the R131 block.)
Asserts structure BEFORE writing (expected dims per member).
"""
import struct, hashlib, os, sys, importlib.util, json

HERE = os.path.dirname(os.path.abspath(__file__))
IFFDUMP = os.path.dirname(HERE)
ROOT = os.path.dirname(os.path.dirname(IFFDUMP))
GATE = os.path.join(ROOT, 'Client/Simitone/Simitone.Client/AutotestRunner.cs')


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    m = importlib.util.module_from_spec(spec)
    sys.modules[name] = m
    spec.loader.exec_module(m)
    return m


r124 = load('r124walk', os.path.join(IFFDUMP, 'r124', 'make_r124_descpanel_canon.py'))

UIFAR = os.path.join(ROOT, 'game-data/The Sims/UIGraphics/UIGraphics.far')

EXPECT_DIMS = {
    "cpanel\\buttons\\housebars.bmp": (63, 26),
    "cpanel\\buttons\\jobbars.bmp": (63, 26),
    "cpanel\\buttons\\relbars.bmp": (63, 26),
    "cpanel\\housesubbars.bmp": (40, 16),
    "cpanel\\jobsubbars.bmp": (40, 11),
    "cpanel\\famesubbars.bmp": (80, 16),
    "cpanel\\backgrounds\\housesubbarsoff.bmp": (40, 16),
    "cpanel\\backgrounds\\jobsubbarsoff.bmp": (40, 11),
    "cpanel\\backgrounds\\famesubbarsoff.bmp": (80, 16),
}


def cs_quote(s):
    return json.dumps(s)


def die(msg):
    print('FATAL: ' + msg)
    sys.exit(1)


uidata = open(UIFAR, 'rb').read()
if uidata[:8] != b'FAR!byAZ':
    die('UIGraphics.far magic %r' % uidata[:8])
truth = {}
for nm, dlen, doff in r124.walk_far_manifest(uidata):
    truth[nm.replace('/', '\\').lower()] = (nm, dlen, doff)

art_rows = []
for low in EXPECT_DIMS:
    hit = truth.get(low)
    if hit is None:
        die('member missing: %s' % low)
    nm, dlen, doff = hit
    b = uidata[doff:doff + dlen]
    if b[:2] != b'BM':
        die('%s not a BMP' % nm)
    w, h = struct.unpack('<ii', b[18:26])
    if (w, h) != EXPECT_DIMS[low]:
        die('unexpected shape for %s: %dx%d (want %dx%d)'
            % (nm, w, h, EXPECT_DIMS[low][0], EXPECT_DIMS[low][1]))
    art_rows.append((low, dlen, w, h, hashlib.sha256(b).hexdigest()))
if len(art_rows) != 9:
    die('expected 9 art rows, got %d' % len(art_rows))

block = []
block.append('        // ==== R132 REGENERATED CANON (tools/iff-dump/r132/regen_gate_shas.py) — NEVER hand-edit ====')
block.append('        // the People-panel RATINGS family ({lowercase full path, byte length, W, H, sha256}):')
block.append('        // the 3 pie strips (kHouseBars 4800 / kJobBars 4801 / kRelBars 4802, 63x26),')
block.append('        // the subpanel bar sheets (kHouseSubpanelBars 4900 40x16, kJobSubpanelBars')
block.append('        // 4901 40x11, kFameSubpanelBars 4822 80x16) and their k*SubBarsOff backdrops')
block.append('        // (4919/4920/4823). Engine fill = 4*(value/10) px — a 10-step bar, 40 px = full sheet.')
block.append('        private static readonly string[][] R132RateArt = new string[][] {')
for low, dlen, w, h, s in art_rows:
    block.append('            new string[] { %s, "%d", "%d", "%d", "%s" },' % (cs_quote(low), dlen, w, h, s))
block.append('        };')
block.append('        // ==== END R132 REGENERATED CANON ====')
block_text = '\n'.join(block) + '\n'

src = open(GATE, encoding='utf-8').read()
marker = '// ==== END R132 REGENERATED CANON ===='
start = '// ==== R132 REGENERATED CANON'
if start in src:
    i0 = src.index(start) - len('        // ')
    i1 = src.index(marker) + len(marker)
    src = src[:i0] + block_text.rstrip('\n') + src[i1:]
else:
    anchor = '// ==== END R131 REGENERATED CANON ===='
    if anchor not in src:
        die('R131 end-marker not found in AutotestRunner.cs')
    src = src.replace(anchor, anchor + '\n' + block_text.rstrip('\n'), 1)
open(GATE, 'w', encoding='utf-8').write(src)
print('WROTE R132 canon block: %d art rows -> %s' % (len(art_rows), os.path.basename(GATE)))
