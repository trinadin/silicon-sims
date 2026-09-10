#!/usr/bin/env python3
"""R131 gate-canon regenerator (the r121-r130 pattern): derives every 'uilive'
pin in AutotestRunner.cs FROM THE DISK BYTES and writes the marker block.
NEVER hand-edit the generated block.

Pins:
  - R131Str154Sha / R131Str154Ratings : UIText.iff STR# 154 'MiscStrings'
      (full-chunk sha256 incl. the 76-byte header — the gate convention —
      plus the ratings family entries [12..15] House/Friend/Job/Mood Rating,
      pure-ASCII sentinel pins; the remaining entries carry mac-roman chars
      (Maxis(TM)/(C) lines) covered byte-exactly by the chunk sha alone)
  - R131LiveArt : the 9 live-gauge + pager + level-button members from
      UIGraphics.far ({lowercase full path, byte length, W, H, sha256}):
      kLiveModeGauge backdrop, kGreenbars/kRedBars fills, kTrackingTarget
      (People-panel corpus), kCatalogPrevPage/NextPage (ScrollLeft/Right),
      kLevel1/kLevel2/kLevelRoof (view-level toolbar corpus).
Asserts structure BEFORE writing (8-bit BMP, expected dims per member).
"""
import struct, hashlib, os, sys, importlib.util, json

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
GATE = os.path.join(ROOT, 'Client/Simitone/Simitone.Client/AutotestRunner.cs')


def load(name, relpath):
    spec = importlib.util.spec_from_file_location(
        name, os.path.join(HERE, '..', relpath))
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
    return mod


r124 = load('r124mod', 'r124/make_r124_descpanel_canon.py')
r122 = load('r122mod', 'r122/make_r122_buycat_canon.py')

UIFAR = os.path.join(ROOT, 'game-data/The Sims/UIGraphics/UIGraphics.far')
UITEXT = os.path.join(ROOT, 'game-data/The Sims/GameData/UIText.iff')

EXPECT_DIMS = {
    "cpanel\\backgrounds\\livegadget.bmp": (108, 100),
    "cpanel\\greenbars.bmp": (27, 25),
    "cpanel\\redbars.bmp": (27, 25),
    "cpanel\\people\\trackingtarget.bmp": (45, 45),
    "cpanel\\buttons\\scrollleft.bmp": (36, 49),
    "cpanel\\buttons\\scrollright.bmp": (36, 49),
    "cpanel\\buttons\\lev1.bmp": (84, 11),
    "cpanel\\buttons\\lev2.bmp": (88, 15),
    "cpanel\\buttons\\levroof.bmp": (88, 19),
}


def cs_quote(s):
    return json.dumps(s)


def die(msg):
    print('FATAL: ' + msg)
    sys.exit(1)


# ---- STR# 154 ----
tdata = open(UITEXT, 'rb').read()
chunk = None
for c in r122.walk_chunks(tdata):
    if c['cc'] == b'STR#' and c['id'] == 154:
        chunk = c
        break
if chunk is None:
    die('STR# 154 not found in UIText.iff')
if chunk['label'] != 'MiscStrings':
    die('STR# 154 label %r' % chunk['label'])
full_chunk = tdata[chunk['off']:chunk['off'] + chunk['size']]
str_sha = hashlib.sha256(full_chunk).hexdigest()
t = r122.table_info(chunk, tdata)
if 'eng' not in t:
    die('STR# 154 has no English block')
eng = [val.decode('mac_roman', 'replace') for _ri, val in t['eng']]
if len(eng) != 23:
    die('STR# 154 English count %d (want 23)' % len(eng))
ratings = eng[12:16]
if ratings != ['House Rating', 'Friend Rating', 'Job Rating', 'Mood Rating']:
    die('STR# 154 ratings family drifted: %r' % (ratings,))

# ---- art members ----
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
    bpp = struct.unpack('<H', b[28:30])[0]
    comp = struct.unpack('<I', b[30:34])[0]
    if bpp not in (8, 24) or comp not in (0, 1) or (w, h) != EXPECT_DIMS[low]:
        die('unexpected shape for %s: %dx%d bpp=%d comp=%d' % (nm, w, h, bpp, comp))
    art_rows.append((low, dlen, w, h, hashlib.sha256(b).hexdigest()))
if len(art_rows) != 9:
    die('expected 9 art rows, got %d' % len(art_rows))

block = []
block.append('        // ==== R131 REGENERATED CANON (tools/iff-dump/r131/regen_gate_shas.py) — NEVER hand-edit ====')
block.append('        // STR# 154 \'MiscStrings\' — the ratings family [12..15] House/Friend/Job/Mood')
block.append('        // Rating (the live gauge caption is [15]; full-chunk sha256 includes the')
block.append('        // 76-byte chunk header, the gate convention; the remaining entries carry')
block.append('        // mac-roman chars and are covered by the chunk sha, not string pins).')
block.append('        private const string R131Str154Sha = "%s";' % str_sha)
block.append('        private static readonly string[] R131Str154Ratings = new string[] {')
for s in ratings:
    block.append('            %s,' % cs_quote(s))
block.append('        };')
block.append('        // the 9 live/pager/level members ({lowercase full path, byte length, W, H, sha256}):')
block.append('        // kLiveModeGauge backdrop + kGreenbars/kRedBars fills + kTrackingTarget corpus')
block.append('        // + kCatalogPrevPage/NextPage (ScrollLeft/Right) + kLevel1/kLevel2/kLevelRoof corpus.')
block.append('        private static readonly string[][] R131LiveArt = new string[][] {')
for low, dlen, w, h, s in art_rows:
    block.append('            new string[] { %s, "%d", "%d", "%d", "%s" },' % (cs_quote(low), dlen, w, h, s))
block.append('        };')
block.append('        // ==== END R131 REGENERATED CANON ====')
block_text = '\n'.join(block) + '\n'

src = open(GATE, encoding='utf-8').read()
start = '// ==== R131 REGENERATED CANON'
if start in src:
    marker = '// ==== END R131 REGENERATED CANON ===='
    i0 = src.index(start) - len('        // ')
    i1 = src.index(marker) + len(marker)
    src = src[:i0] + block_text.rstrip('\n') + src[i1:]
else:
    anchor = '// ==== END R130 REGENERATED CANON ===='
    if anchor not in src:
        die('R130 end-marker not found in AutotestRunner.cs')
    src = src.replace(anchor, anchor + '\n' + block_text.rstrip('\n'), 1)
open(GATE, 'w', encoding='utf-8').write(src)
print('WROTE R131 canon block: 1 STR# 154 pin (sha + %d ratings) + %d art rows -> %s'
      % (len(ratings), len(art_rows), os.path.basename(GATE)))
