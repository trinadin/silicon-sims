#!/usr/bin/env python3
"""R124 gate-canon regenerator (the r121/r122/r119 pattern): derives every
'uidesc' pin in AutotestRunner.cs FROM THE DISK BYTES and writes the marker
block. NEVER hand-edit the generated block.

Pins:
  - R124Str160Sha  : full-chunk sha256 of UIText.iff STR# 160 'CatalogRatings'
                     (includes the 76-byte chunk header, gate convention)
  - R124Str160     : all 20 English entries, verbatim
  - R124DescArt    : the two description-panel art members (lowercase full
                     path, byte length, W, H, sha256) from UIGraphics.far
Asserts structure BEFORE writing (label, raw counts, entry count, dims).
"""
import struct, hashlib, os, sys, importlib.util, json

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
GATE = os.path.join(ROOT, 'Client/Simitone/Simitone.Client/AutotestRunner.cs')

spec = importlib.util.spec_from_file_location('r124mod', os.path.join(HERE, 'make_r124_descpanel_canon.py'))
r124 = importlib.util.module_from_spec(spec)
sys.modules['r124mod'] = r124
spec.loader.exec_module(r124)
m = r124.str_mod()

UITEXT = os.path.join(ROOT, 'game-data/The Sims/GameData/UIText.iff')
UIFAR = os.path.join(ROOT, 'game-data/The Sims/UIGraphics/UIGraphics.far')


def cs_quote(s):
    return json.dumps(s)   # JSON string escaping is valid C# for these entries


def die(msg):
    print('FATAL: ' + msg)
    sys.exit(1)


data = open(UITEXT, 'rb').read()
chunks = m.walk_chunks(data)
hits = [c for c in chunks if c['cc'] == b'STR#' and c['id'] == 160]
if len(hits) != 1:
    die('STR# 160 found %d times' % len(hits))
ch = hits[0]
if ch['label'] != 'CatalogRatings':
    die('STR# 160 label %r' % ch['label'])
raw = data[ch['off']:ch['off'] + ch['size']]
sha_full = hashlib.sha256(raw).hexdigest()
t = m.table_info(ch, data)
eng = [v.decode('mac_roman') for i, v in t['eng']]
if t['declared'] != 400 or len(eng) != 20:
    die('STR# 160 declared=%d english=%d' % (t['declared'], len(eng)))
if '%d' not in eng[0] or not eng[7].startswith('+ ') or 'Group Activity' not in eng[16]:
    die('STR# 160 entry-shape sanity failed: %r / %r / %r' % (eng[0], eng[7], eng[16]))

uidata = open(UIFAR, 'rb').read()
truth = {}
for nm, dlen, doff in r124.walk_far_manifest(uidata):
    truth[nm.lower()] = (nm, dlen, doff)
art_rows = []
for name, note in r124.ART_MEMBERS:
    hit = truth.get(name.lower())
    if hit is None:
        die('art member missing: %s' % name)
    nm, dlen, doff = hit
    b = uidata[doff:doff + dlen]
    w, h = struct.unpack('<ii', b[18:26])
    if (w, h) not in ((559, 127), (36, 36)):
        die('unexpected dims for %s: %dx%d' % (nm, w, h))
    art_rows.append((name.lower(), dlen, w, h, hashlib.sha256(b).hexdigest()))

block = []
block.append('        // ==== R124 REGENERATED CANON (tools/iff-dump/r124/regen_gate_shas.py) — NEVER hand-edit ====')
block.append('        private const string R124Str160Sha = "%s";' % sha_full)
block.append('        // STR# 160 \'CatalogRatings\' — all 20 English entries, verbatim ([0..6] \'X: %d\' motive')
block.append('        // formats, [7..13] \'+ Skill\', [14..19] usage flags with NO corpus data — see the')
block.append('        // R124 negative-space finding; the port renders [0..13] only).')
block.append('        private static readonly string[] R124Str160 = new string[] {')
for i, e in enumerate(eng):
    block.append('            %s,%s' % (cs_quote(e), '   // [%d]' % i))
block.append('        };')
block.append('        // description plaque art: {lowercase full path, byte length, W, H, sha256}')
block.append('        private static readonly string[][] R124DescArt = new string[][] {')
for low, dlen, w, h, s in art_rows:
    block.append('            new string[] { %s, "%d", "%d", "%d", "%s" },' % (cs_quote(low), dlen, w, h, s))
block.append('        };')
block.append('        // ==== END R124 REGENERATED CANON ====')
block_text = '\n'.join(block) + '\n'

src = open(GATE, encoding='utf-8').read()
marker = '// ==== END R124 REGENERATED CANON ===='
start = '// ==== R124 REGENERATED CANON'
if start in src:
    # replace the existing block wholesale
    i0 = src.index(start) - len('        // ')
    i1 = src.index(marker) + len(marker)
    src = src[:i0] + block_text.rstrip('\n') + src[i1:]
else:
    anchor = '// ==== END R122 REGENERATED CANON ===='
    if anchor not in src:
        die('R122 end-marker not found in AutotestRunner.cs')
    src = src.replace(anchor, anchor + '\n' + block_text.rstrip('\n'), 1)
open(GATE, 'w', encoding='utf-8').write(src)
print('WROTE R124 canon block: sha=%s... entries=%d art=%d rows -> %s'
      % (sha_full[:12], len(eng), len(art_rows), os.path.basename(GATE)))
