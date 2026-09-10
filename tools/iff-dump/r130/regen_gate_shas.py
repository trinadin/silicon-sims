#!/usr/bin/env python3
"""R130 gate-canon regenerator (the r121-r127 pattern): derives every 'uiroof'
pin in AutotestRunner.cs FROM THE DISK BYTES and writes the marker block.
NEVER hand-edit the generated block.

Pins:
  - R130RoofStr147Sha / R130RoofStr147 : UIText.iff STR# 147 'roofpanelstrs'
      (full-chunk sha256 incl. the 76-byte header — the gate convention —
      plus all 18 English entries: 4 pitch names, the layout directives,
      pager captions, page titles and the two lore texts)
  - R130RoofArt : the 7 roof family members from UIGraphics.far
      ({lowercase full path, byte length, W, H, sha256})
Asserts structure BEFORE writing (8-bit RLE8, expected dims per member).
"""
import struct, hashlib, os, sys, importlib.util, json

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
GATE = os.path.join(ROOT, 'Client/Simitone/Simitone.Client/AutotestRunner.cs')

spec = importlib.util.spec_from_file_location('r130mod', os.path.join(HERE, 'make_r130_roof_canon.py'))
r130 = importlib.util.module_from_spec(spec)
sys.modules['r130mod'] = r130
spec.loader.exec_module(r130)

UIFAR = os.path.join(ROOT, 'game-data/The Sims/UIGraphics/UIGraphics.far')
UITEXT = os.path.join(ROOT, 'game-data/The Sims/GameData/UIText.iff')

EXPECT_DIMS = {
    "cpanel\\build\\roofpatterntemplate.bmp": (180, 45),
    "cpanel\\build\\roofflat.bmp": (100, 16),
    "cpanel\\build\\roofshallow.bmp": (104, 16),
    "cpanel\\build\\roofmedium.bmp": (104, 17),
    "cpanel\\build\\roofsteep.bmp": (104, 23),
    "cpanel\\build\\popuproofpattern.bmp": (145, 103),
    "cpanel\\build\\popuproofpitch.bmp": (133, 103),
}


def cs_quote(s):
    return json.dumps(s)


def die(msg):
    print('FATAL: ' + msg)
    sys.exit(1)


# ---- STR# 147 ----
tdata = open(UITEXT, 'rb').read()
chunk = None
for c in r130.r122.walk_chunks(tdata):
    if c['cc'] == b'STR#' and c['id'] == 147:
        chunk = c
        break
if chunk is None:
    die('STR# 147 not found in UIText.iff')
if chunk['label'] != 'roofpanelstrs':
    die('STR# 147 label %r' % chunk['label'])
full_chunk = tdata[chunk['off']:chunk['off'] + chunk['size']]
str_sha = hashlib.sha256(full_chunk).hexdigest()
t = r130.r122.table_info(chunk, tdata)
if 'eng' not in t:
    die('STR# 147 has no English block')
eng = [val.decode('mac_roman', 'replace') for _ri, val in t['eng']]
if len(eng) != 18:
    die('STR# 147 English count %d (want 18)' % len(eng))
if eng[1] != 'Shallow Roof Pitch' or eng[5] != 'Steep Roof Pitch' \
        or eng[12] != 'Flat Roof' or eng[14] != 'Roof Pitch' \
        or eng[16] != 'Roof Patterns':
    die('STR# 147 sentinel entries drifted')

# ---- art members ----
uidata = open(UIFAR, 'rb').read()
if uidata[:8] != b'FAR!byAZ':
    die('UIGraphics.far magic %r' % uidata[:8])
truth = {}
for nm, dlen, doff in r130.r124.walk_far_manifest(uidata):
    truth[nm.replace('/', '\\').lower()] = (nm, dlen, doff)

art_rows = []
for member in r130.MEMBERS:
    low = member.replace('/', '\\').lower()
    hit = truth.get(low)
    if hit is None:
        die('member missing: %s' % member)
    nm, dlen, doff = hit
    b = uidata[doff:doff + dlen]
    if b[:2] != b'BM':
        die('%s not a BMP' % nm)
    w, h = struct.unpack('<ii', b[18:26])
    bpp = struct.unpack('<H', b[28:30])[0]
    comp = struct.unpack('<I', b[30:34])[0]
    if bpp != 8 or comp != 1 or (w, h) != EXPECT_DIMS[low]:
        die('unexpected shape for %s: %dx%d bpp=%d comp=%d' % (nm, w, h, bpp, comp))
    art_rows.append((low, dlen, w, h, hashlib.sha256(b).hexdigest()))
if len(art_rows) != 7:
    die('expected 7 art rows, got %d' % len(art_rows))

block = []
block.append('        // ==== R130 REGENERATED CANON (tools/iff-dump/r130/regen_gate_shas.py) — NEVER hand-edit ====')
block.append('        // STR# 147 \'roofpanelstrs\' — the ORIGINAL roof panel strings (full-chunk sha256')
block.append('        // includes the 76-byte chunk header, the gate convention; 18 English entries:')
block.append('        // layout directives, the 4 pitch names, pager captions, page titles, lore).')
block.append('        private const string R130RoofStr147Sha = "%s";' % str_sha)
block.append('        private static readonly string[] R130RoofStr147 = new string[] {')
for s in eng:
    block.append('            %s,' % cs_quote(s))
block.append('        };')
block.append('        // the 7 roof family members ({lowercase full path, byte length, W, H, sha256}):')
block.append('        // kRoofPatternTemplate 4-frame sheet + 4 kBldSbTlRoof* plaques + 2 kBldPopup* thumbs.')
block.append('        private static readonly string[][] R130RoofArt = new string[][] {')
for low, dlen, w, h, s in art_rows:
    block.append('            new string[] { %s, "%d", "%d", "%d", "%s" },' % (cs_quote(low), dlen, w, h, s))
block.append('        };')
block.append('        // ==== END R130 REGENERATED CANON ====')
block_text = '\n'.join(block) + '\n'

src = open(GATE, encoding='utf-8').read()
marker = '// ==== END R130 REGENERATED CANON ===='
start = '// ==== R130 REGENERATED CANON'
if start in src:
    i0 = src.index(start) - len('        // ')
    i1 = src.index(marker) + len(marker)
    src = src[:i0] + block_text.rstrip('\n') + src[i1:]
else:
    anchor = '// ==== END R127 REGENERATED CANON ===='
    if anchor not in src:
        die('R127 end-marker not found in AutotestRunner.cs')
    src = src.replace(anchor, anchor + '\n' + block_text.rstrip('\n'), 1)
open(GATE, 'w', encoding='utf-8').write(src)
print('WROTE R130 canon block: 1 STR# 147 pin (%d entries) + %d art rows -> %s'
      % (len(eng), len(art_rows), os.path.basename(GATE)))
