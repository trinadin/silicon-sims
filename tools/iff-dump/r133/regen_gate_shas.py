#!/usr/bin/env python3
"""R133 gate-canon regenerator (the r121-r132 pattern): derives the 'uihouse'
STR# pins in AutotestRunner.cs FROM THE DISK BYTES of Live.iff and writes the
marker block. NEVER hand-edit the generated block.

Pins (R133HouseStr):
  - R133H133Sha / R133H133 : STR# 133 'HouseSubpanelLabels' (10 English
      entries: the 'House' header, the five score-bar labels
      Size/Furnishings/Yard/Upkeep/Layout, and the four value formats
      'Sq. Ft.: %d' / 'Bedrooms: %d' / 'Bathrooms: %d' / 'Lot: %s')
  - R133H135Sha / R133H135Titles : STR# 135 'HouseSubpanelPopupText'
      (18 English title+help pairs; the NINE titles at even indices are
      pinned, the full-chunk sha covers the bodies — incl. the corpus's own
      statements of the classification rules: bedroom = any room with a bed,
      bathroom = any room with a toilet/tub/shower, square feet = the area
      enclosed by walls)
  - R133H138Sha / R133H138 : STR# 138 'HouseSubpanelSize' (Small/Medium/Large)
Asserts labels/counts/sentinels BEFORE writing.
"""
import hashlib, os, sys, importlib.util, json

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


r122 = load('r122walk', os.path.join(IFFDUMP, 'r122', 'make_r122_buycat_canon.py'))

LIVE = os.path.join(ROOT, 'game-data/The Sims/GameData/Live.iff')


def cs_quote(s):
    return json.dumps(s)


def die(msg):
    print('FATAL: ' + msg)
    sys.exit(1)


data = open(LIVE, 'rb').read()
want = {133: 'HouseSubpanelLabels', 135: 'HouseSubpanelPopupText', 138: 'HouseSubpanelSize'}
found = {}
for c in r122.walk_chunks(data):
    if c['cc'] == b'STR#' and c['id'] in want:
        if c['label'] != want[c['id']]:
            die('STR# %d label %r (want %r)' % (c['id'], c['label'], want[c['id']]))
        full = data[c['off']:c['off'] + c['size']]
        t = r122.table_info(c, data)
        if 'eng' not in t:
            die('STR# %d has no English block' % c['id'])
        found[c['id']] = (hashlib.sha256(full).hexdigest(),
                          [v.decode('mac_roman', 'replace') for _ri, v in t['eng']])
for i in want:
    if i not in found:
        die('STR# %d not found in Live.iff' % i)

sha133, e133 = found[133]
sha135, e135 = found[135]
sha138, e138 = found[138]
if len(e133) != 10: die('133 English count %d (want 10)' % len(e133))
if len(e135) != 18: die('135 English count %d (want 18)' % len(e135))
if len(e138) != 3: die('138 English count %d (want 3)' % len(e138))
if e133[0] != 'House' or e133[1] != 'Size' or e133[5] != 'Layout' \
        or e133[6] != 'Sq. Ft.: %d' or e133[9] != 'Lot: %s':
    die('133 sentinel entries drifted')
if e135[0] != 'House Size' or e135[10] != 'Square Feet' or e135[16] != 'Lot Size':
    die('135 sentinel entries drifted')
if e138 != ['Small', 'Medium', 'Large']:
    die('138 sentinel entries drifted')

titles = [e135[i] for i in range(0, 18, 2)]

block = []
block.append('        // ==== R133 REGENERATED CANON (tools/iff-dump/r133/regen_gate_shas.py) — NEVER hand-edit ====')
block.append('        // the House subpanel\'s OWN tables in Live.iff (the corpus that corrects the R132')
block.append('        // \'House Rating\' reading — full-chunk sha256 incl. the 76-byte header).')
block.append('        private const string R133H133Sha = "%s";' % sha133)
block.append('        // STR# 133 \'HouseSubpanelLabels\': [0] header, [1]-[5] Size/Furnishings/Yard/Upkeep/Layout,')
block.append('        // [6]-[9] the value formats.')
block.append('        private static readonly string[] R133H133 = new string[] {')
for s in e133:
    block.append('            %s,' % cs_quote(s))
block.append('        };')
block.append('        private const string R133H135Sha = "%s";' % sha135)
block.append('        // STR# 135 \'HouseSubpanelPopupText\': 18 title+help pairs — the nine TITLES pinned;')
block.append('        // the sha covers the help bodies (which state the classification rules).')
block.append('        private static readonly string[] R133H135Titles = new string[] {')
for s in titles:
    block.append('            %s,' % cs_quote(s))
block.append('        };')
block.append('        private const string R133H138Sha = "%s";' % sha138)
block.append('        // STR# 138 \'HouseSubpanelSize\': the lot words for the decoded ladder')
block.append('        // (dimension < 40 Small / < 50 Medium / >= 50 Large).')
block.append('        private static readonly string[] R133H138 = new string[] {')
for s in e138:
    block.append('            %s,' % cs_quote(s))
block.append('        };')
block.append('        // ==== END R133 REGENERATED CANON ====')
block_text = '\n'.join(block) + '\n'

src = open(GATE, encoding='utf-8').read()
marker = '// ==== END R133 REGENERATED CANON ===='
start = '// ==== R133 REGENERATED CANON'
if start in src:
    i0 = src.index(start) - len('        // ')
    i1 = src.index(marker) + len(marker)
    src = src[:i0] + block_text.rstrip('\n') + src[i1:]
else:
    anchor = '// ==== END R132 REGENERATED CANON ===='
    if anchor not in src:
        die('R132 end-marker not found in AutotestRunner.cs')
    src = src.replace(anchor, anchor + '\n' + block_text.rstrip('\n'), 1)
open(GATE, 'w', encoding='utf-8').write(src)
print('WROTE R133 canon block: 133(10) + 135 titles(9) + 138(3) -> %s' % os.path.basename(GATE))
