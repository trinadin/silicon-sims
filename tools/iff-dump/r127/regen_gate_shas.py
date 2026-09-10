#!/usr/bin/env python3
"""R127 gate-canon regenerator (the r121/r122/r124/r125/r126 pattern): derives
every 'uibldt' pin in AutotestRunner.cs FROM THE DISK BYTES and writes the
marker block. NEVER hand-edit the generated block.

Pins:
  - R127TerrainArt : the 8 mounted terrain-tool members (4 kBldSbTl* plaques +
                     4 kBldPopup* thumbs; lowercase full path, byte length,
                     W, H, sha256) from UIGraphics.far
Asserts structure BEFORE writing (8-bit RLE8, expected dims per role).
"""
import struct, hashlib, os, sys, importlib.util, json

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
GATE = os.path.join(ROOT, 'Client/Simitone/Simitone.Client/AutotestRunner.cs')

spec = importlib.util.spec_from_file_location('r127mod', os.path.join(HERE, 'make_r127_terraintools_canon.py'))
r127 = importlib.util.module_from_spec(spec)
sys.modules['r127mod'] = r127
spec.loader.exec_module(r127)

UIFAR = os.path.join(ROOT, 'game-data/The Sims/UIGraphics/UIGraphics.far')

ICON_DIMS = {(144, 33), (156, 33), (144, 32), (144, 34)}
THUMB_DIMS = {(145, 103)}


def cs_quote(s):
    return json.dumps(s)


def die(msg):
    print('FATAL: ' + msg)
    sys.exit(1)


uidata = open(UIFAR, 'rb').read()
if uidata[:8] != b'FAR!byAZ':
    die('UIGraphics.far magic %r' % uidata[:8])
truth = {}
for nm, dlen, doff in r127.r124.walk_far_manifest(uidata):
    truth[nm.lower()] = (nm, dlen, doff)

art_rows = []
seen = set()
for tid, label, icon, thumb in r127.MOUNTED:
    for member, dims in ((icon, ICON_DIMS), (thumb, THUMB_DIMS)):
        hit = truth.get(member.lower())
        if hit is None:
            die('member missing: %s' % member)
        nm, dlen, doff = hit
        if nm.lower() in seen:
            die('duplicate pin: %s' % nm)
        seen.add(nm.lower())
        b = uidata[doff:doff + dlen]
        if b[:2] != b'BM':
            die('%s not a BMP' % nm)
        w, h = struct.unpack('<ii', b[18:26])
        bpp = struct.unpack('<H', b[28:30])[0]
        comp = struct.unpack('<I', b[30:34])[0]
        if bpp != 8 or comp != 1 or (w, h) not in dims:
            die('unexpected shape for %s: %dx%d bpp=%d comp=%d' % (nm, w, h, bpp, comp))
        art_rows.append((member.lower(), dlen, w, h, hashlib.sha256(b).hexdigest()))
if len(art_rows) != 8:
    die('expected 8 art rows, got %d' % len(art_rows))

block = []
block.append('        // ==== R127 REGENERATED CANON (tools/iff-dump/r127/regen_gate_shas.py) — NEVER hand-edit ====')
block.append('        // terrain tool members (kBldSbTl* plaques + kBldPopup* thumbs, id order 0/1/2/3):')
block.append('        // {lowercase full path, byte length, W, H, sha256}')
block.append('        private static readonly string[][] R127TerrainArt = new string[][] {')
for low, dlen, w, h, s in art_rows:
    block.append('            new string[] { %s, "%d", "%d", "%d", "%s" },' % (cs_quote(low), dlen, w, h, s))
block.append('        };')
block.append('        // ==== END R127 REGENERATED CANON ====')
block_text = '\n'.join(block) + '\n'

src = open(GATE, encoding='utf-8').read()
marker = '// ==== END R127 REGENERATED CANON ===='
start = '// ==== R127 REGENERATED CANON'
if start in src:
    i0 = src.index(start) - len('        // ')
    i1 = src.index(marker) + len(marker)
    src = src[:i0] + block_text.rstrip('\n') + src[i1:]
else:
    anchor = '// ==== END R126 REGENERATED CANON ===='
    if anchor not in src:
        die('R126 end-marker not found in AutotestRunner.cs')
    src = src.replace(anchor, anchor + '\n' + block_text.rstrip('\n'), 1)
open(GATE, 'w', encoding='utf-8').write(src)
print('WROTE R127 canon block: %d art rows -> %s' % (len(art_rows), os.path.basename(GATE)))
