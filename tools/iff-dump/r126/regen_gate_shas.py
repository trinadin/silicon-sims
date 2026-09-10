#!/usr/bin/env python3
"""R126 gate-canon regenerator (the r121/r122/r124/r125 pattern): derives every
'uibuild' pin in AutotestRunner.cs FROM THE DISK BYTES and writes the marker
block. NEVER hand-edit the generated block.

Pins:
  - R126BldTipsSha : full-chunk sha256 of UIText.iff STR# 139 'BldTips'
                     (includes the 76-byte chunk header, gate convention)
  - R126BldTips    : all 16 English entries, verbatim
  - R126BuildArt   : the 11 mounted build tool plaques (lowercase full path,
                     byte length, W, H, sha256) from UIGraphics.far
Asserts structure BEFORE writing (label, raw counts, entry count, BMP shape).
"""
import struct, hashlib, os, sys, importlib.util, json

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
GATE = os.path.join(ROOT, 'Client/Simitone/Simitone.Client/AutotestRunner.cs')

spec = importlib.util.spec_from_file_location('r126mod', os.path.join(HERE, 'make_r126_buildcat_canon.py'))
r126 = importlib.util.module_from_spec(spec)
sys.modules['r126mod'] = r126
spec.loader.exec_module(r126)

UITEXT = os.path.join(ROOT, 'game-data/The Sims/GameData/UIText.iff')
UIFAR = os.path.join(ROOT, 'game-data/The Sims/UIGraphics/UIGraphics.far')


def cs_quote(s):
    return json.dumps(s)


def die(msg):
    print('FATAL: ' + msg)
    sys.exit(1)


data = open(UITEXT, 'rb').read()
chunks = r126.r122.walk_chunks(data)
hits = [c for c in chunks if c['cc'] == b'STR#' and c['id'] == 139]
if len(hits) != 1:
    die('STR# 139 found %d times' % len(hits))
ch = hits[0]
if ch['label'] != 'BldTips':
    die('STR# 139 label %r' % ch['label'])
raw = data[ch['off']:ch['off'] + ch['size']]
sha_full = hashlib.sha256(raw).hexdigest()
t = r126.r122.table_info(ch, data)
eng = [v.decode('mac_roman') for i, v in t['eng']]
if t['declared'] != 288 or len(eng) != 16:
    die('STR# 139 declared=%d english=%d' % (t['declared'], len(eng)))
if eng[0] != 'Terrain Tool' or eng[11] != 'Hand Tool' or eng[15] != 'Next Page':
    die('STR# 139 entry-shape sanity failed: %r / %r / %r' % (eng[0], eng[11], eng[15]))

uidata = open(UIFAR, 'rb').read()
truth = {}
for nm, dlen, doff in r126.r124.walk_far_manifest(uidata):
    truth[nm.lower()] = (nm, dlen, doff)
art_rows = []
seen = set()
for idx, name, sym in r126.STRIND_TO_MEMBER:
    hit = truth.get(name.lower())
    if hit is None:
        die('art member missing: %s' % name)
    nm, dlen, doff = hit
    if nm.lower() in seen:
        die('duplicate member pin: %s' % nm)
    seen.add(nm.lower())
    b = uidata[doff:doff + dlen]
    if b[:2] != b'BM':
        die('%s not a BMP' % nm)
    w, h = struct.unpack('<ii', b[18:26])
    bpp = struct.unpack('<H', b[28:30])[0]
    comp = struct.unpack('<I', b[30:34])[0]
    if bpp != 8 or comp != 1 or not (20 <= w <= 250 and 16 <= h <= 60):
        die('unexpected shape for %s: %dx%d bpp=%d comp=%d' % (nm, w, h, bpp, comp))
    art_rows.append((name.lower(), dlen, w, h, hashlib.sha256(b).hexdigest()))
if len(art_rows) != 11:
    die('expected 11 art rows, got %d' % len(art_rows))

block = []
block.append('        // ==== R126 REGENERATED CANON (tools/iff-dump/r126/regen_gate_shas.py) — NEVER hand-edit ====')
block.append('        private const string R126BldTipsSha = "%s";' % sha_full)
block.append("        // STR# 139 'BldTips' — all 16 English entries, verbatim ([0..11] tool")
block.append('        // captions, [12..13] Undo/Redo Last, [14..15] Previous/Next Page). The')
block.append('        // build subcategory row mounts [0..10]; [11..13] have no port surface.')
block.append('        private static readonly string[] R126BldTips = new string[] {')
for i, e in enumerate(eng):
    block.append('            %s,%s' % (cs_quote(e), '   // [%d]' % i))
block.append('        };')
block.append('        // build tool plaques (kToolBtn 64..77): {lowercase full path, byte length, W, H, sha256}')
block.append('        private static readonly string[][] R126BuildArt = new string[][] {')
for low, dlen, w, h, s in art_rows:
    block.append('            new string[] { %s, "%d", "%d", "%d", "%s" },' % (cs_quote(low), dlen, w, h, s))
block.append('        };')
block.append('        // ==== END R126 REGENERATED CANON ====')
block_text = '\n'.join(block) + '\n'

src = open(GATE, encoding='utf-8').read()
marker = '// ==== END R126 REGENERATED CANON ===='
start = '// ==== R126 REGENERATED CANON'
if start in src:
    i0 = src.index(start) - len('        // ')
    i1 = src.index(marker) + len(marker)
    src = src[:i0] + block_text.rstrip('\n') + src[i1:]
else:
    anchor = '// ==== END R125 REGENERATED CANON ===='
    if anchor not in src:
        die('R125 end-marker not found in AutotestRunner.cs')
    src = src.replace(anchor, anchor + '\n' + block_text.rstrip('\n'), 1)
open(GATE, 'w', encoding='utf-8').write(src)
print('WROTE R126 canon block: sha=%s... entries=%d art=%d rows -> %s'
      % (sha_full[:12], len(eng), len(art_rows), os.path.basename(GATE)))
