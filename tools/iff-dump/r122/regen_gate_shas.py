#!/usr/bin/env python3
"""R122 gate-sha regenerator: writes the ENTIRE R122 canon block into
AutotestRunner.cs between the R122 REGEN markers, from disk truth only
(UIText.iff for the string tables, UIGraphics.far for the art). This is the
R119/R121 discipline: 64-hex sha pins are NEVER hand-typed.

Reuses the proven walks: make_r122_buycat_canon.py (STR# chunks) and
make_r122_art_canon.py (FAR1 manifest).
"""
import os, sys, importlib.util

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
GATE = os.path.join(ROOT, 'Client/Simitone/Simitone.Client/AutotestRunner.cs')
UITEXT = os.path.join(ROOT, 'game-data/The Sims/GameData/UIText.iff')

# load the sibling tools as modules (without running their mains)
def load(name, fn):
    src = open(os.path.join(HERE, fn)).read()
    src = src.replace('if __name__ == "__main__":\n    main()', '')
    ns = {'__file__': os.path.join(HERE, fn), '__name__': name}
    exec(compile(src, fn, 'exec'), ns)
    return ns

strs = load('strs', 'make_r122_buycat_canon.py')
art = load('art', 'make_r122_art_canon.py')

# ---- string truth ----
data = open(UITEXT, 'rb').read()
chunks = strs['walk_chunks'](data)
by_id = {}
for c in chunks:
    if c['cc'] == b'STR#':
        by_id.setdefault(c['id'], []).append(c)
assert len(by_id[150]) == 1 and len(by_id[210]) == 1 and len(by_id[154]) == 1
t150 = strs['table_info'](by_id[150][0], data)
t210 = strs['table_info'](by_id[210][0], data)
t154 = strs['table_info'](by_id[154][0], data)
assert t150['ch']['label'] == 'BuyModeCatalogSortTips' and len(t150['eng']) == 48
assert t210['ch']['label'] == 'Function Sub Sort Extra Strings' and len(t210['eng']) == 5
assert t154['ch']['label'] == 'MiscStrings' and len(t154['eng']) == 23
assert t154['eng'][0][1] == b'Previous Page' and t154['eng'][1][1] == b'Next Page'
sorts150 = [v.decode('mac_roman') for _, v in t150['eng']]
extra210 = [v.decode('mac_roman') for _, v in t210['eng']]
subsorts, shas200 = [], []
for t in range(200, 208):
    ti = strs['table_info'](by_id[t][0], data)
    assert len(ti['eng']) == 6, (t, len(ti['eng']))
    names = [v.decode('mac_roman') for _, v in ti['eng']]
    assert names[4] == '' and names[5] == '', (t, names)
    subsorts.append(names[:4])
    shas200.append(ti['sha_full'])

# ---- art truth ----
fdata = open(os.path.join(ROOT, 'game-data/The Sims/UIGraphics/UIGraphics.far'), 'rb').read()
truth = art['far_truth'](fdata)
# same member list/order as the canon evidence
art_rows = []
for name, note in art['MEMBERS']:
    t = truth[name.lower()]
    art_rows.append((name.lower(), t[1], t[2], t[3], t[4]))

def cs(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'

block = []
block.append('        // ==== R122 REGENERATED CANON (tools/iff-dump/r122/regen_gate_shas.py) — NEVER hand-edit ====')
block.append('        private const string R122Str150Sha = %s;' % cs(t150['sha_full']))
block.append('        private const string R122Str210Sha = %s;' % cs(t210['sha_full']))
block.append('        private const string R122Str154Sha = %s;' % cs(t154['sha_full']))
block.append('        private static readonly string[] R122Str200Shas = new string[] {')
block.append('            ' + ', '.join(cs(s) for s in shas200) + ' };')
block.append('        // all 48 English sort names of STR# 150 (rooms [0..7], functions [8..15],')
block.append('        // Downtown [16..23], Vacation [24..31], Old Town [32..39], Studio Town [40..47])')
block.append('        private static readonly string[] R122Sorts150 = new string[] {')
for i in range(0, 48, 6):
    block.append('            ' + ', '.join(cs(s) for s in sorts150[i:i+6]) + ',')
block.append('        };')
block.append('        // STR# 200-207 English entries [0..3] (the subsort slot names; [4]/[5] are empty)')
block.append('        private static readonly string[][] R122SubSortNames = new string[][] {')
for names in subsorts:
    block.append('            new string[] { ' + ', '.join(cs(s) for s in names) + ' },')
block.append('        };')
block.append('        // STR# 210 English: Back, Other, All, Pets, Magic')
block.append('        private static readonly string[] R122Extra210 = new string[] {')
block.append('            ' + ', '.join(cs(s) for s in extra210) + ' };')
block.append('        // every catalog art member the port mounts: { lowercase full path, len, W, H, sha256 }')
block.append('        private static readonly string[][] R122CatalogArt = new string[][] {')
for low, dlen, w, h, sha in art_rows:
    block.append('            new string[] { %s, "%d", "%d", "%d", "%s" },' % (cs(low), dlen, w, h, sha))
block.append('        };')
block.append('        // ==== END R122 REGENERATED CANON ====')
new_block = '\n'.join(block)

src = open(GATE).read()
M1 = '        // ==== R122 REGENERATED CANON'
M2 = '        // ==== END R122 REGENERATED CANON ===='
if M1 in src and M2 in src:
    start = src.index(M1)
    end = src.index(M2) + len(M2)
    src = src[:start] + new_block + src[end:]
else:
    # first run: insert the block after the R121 sha const
    anchor = 'private const string R121OptionStrsSha ='
    i = src.index(anchor)
    j = src.index('\n', i) + 1
    src = src[:j] + new_block + '\n' + src[j:]
open(GATE, 'w').write(src)
print('regen: wrote %d string pins + %d art rows into AutotestRunner.cs' % (48 + 5 + 24 + 8, len(art_rows)))
