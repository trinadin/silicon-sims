#!/usr/bin/env python3
"""R125 gate-canon regenerator: derives the 'uitt' pins FROM THE DISK BYTES and
writes the marker block into AutotestRunner.cs. NEVER hand-edit the block.

Pins:
  - R125Str159Sha : full-chunk sha256 of UIText.iff STR# 159 'ObjectTTs'
  - R125Str159    : all 9 English entries, verbatim
Asserts structure (label, 180 raw, 9 English) BEFORE writing.
"""
import hashlib, os, sys, json, importlib.util

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
GATE = os.path.join(ROOT, 'Client/Simitone/Simitone.Client/AutotestRunner.cs')

spec = importlib.util.spec_from_file_location('r122mod', os.path.join(HERE, '..', 'r122', 'make_r122_buycat_canon.py'))
m = importlib.util.module_from_spec(spec)
sys.modules['r122mod'] = m
spec.loader.exec_module(m)

UITEXT = os.path.join(ROOT, 'game-data/The Sims/GameData/UIText.iff')


def die(msg):
    print('FATAL: ' + msg); sys.exit(1)


data = open(UITEXT, 'rb').read()
hits = [c for c in m.walk_chunks(data) if c['cc'] == b'STR#' and c['id'] == 159]
if len(hits) != 1 or hits[0]['label'] != 'ObjectTTs':
    die('STR# 159 not found/mislabeled')
ch = hits[0]
raw = data[ch['off']:ch['off'] + ch['size']]
sha = hashlib.sha256(raw).hexdigest()
t = m.table_info(ch, data)
eng = [v.decode('mac_roman') for _, v in t['eng']]
if t['declared'] != 180 or len(eng) != 9:
    die('counts %d/%d' % (t['declared'], len(eng)))
if 'no actions' not in eng[0] or 'active character' not in eng[3] or 'user-directed' not in eng[8]:
    die('entry-shape sanity failed')

block = []
block.append('        // ==== R125 REGENERATED CANON (tools/iff-dump/r125/regen_gate_shas.py) — NEVER hand-edit ====')
block.append('        private const string R125Str159Sha = "%s";' % sha)
block.append("        // STR# 159 'ObjectTTs' — the original LIVE-MODE object hover reasons, all 9 English")
block.append('        // entries verbatim. [2] \'...kids\' has NO corpus anchor (r125 scan: zero kids-only')
block.append('        // TTAB sets exist); [0]/[8] split and [3] are the round\'s disclosed interpretations.')
block.append('        private static readonly string[] R125Str159 = new string[] {')
for i, e in enumerate(eng):
    block.append('            %s,%s' % (json.dumps(e), '   // [%d]' % i))
block.append('        };')
block.append('        // ==== END R125 REGENERATED CANON ====')
block_text = '\n'.join(block)

src = open(GATE, encoding='utf-8').read()
start = '// ==== R125 REGENERATED CANON'
marker = '// ==== END R125 REGENERATED CANON ===='
if start in src:
    i0 = src.index(start) - len('        // ')
    i1 = src.index(marker) + len(marker)
    src = src[:i0] + block_text + src[i1:]
else:
    anchor = '// ==== END R124 REGENERATED CANON ===='
    if anchor not in src:
        die('R124 end-marker not found')
    src = src.replace(anchor, anchor + '\n' + block_text, 1)
open(GATE, 'w', encoding='utf-8').write(src)
print('WROTE R125 canon block: sha=%s... entries=%d' % (sha[:12], len(eng)))
