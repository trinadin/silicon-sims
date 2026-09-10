#!/usr/bin/env python3
"""R133 canon builder: the House subpanel's own string tables in Live.iff —
STR# 133 'HouseSubpanelLabels', 135 'HouseSubpanelPopupText', 138
'HouseSubpanelSize'. Prints full-chunk sha256 (76-byte header included, the
gate convention) + all English entries, and the gate row constants."""
import hashlib, os, sys, importlib.util

HERE = os.path.dirname(os.path.abspath(__file__))
IFFDUMP = os.path.dirname(HERE)
ROOT = os.path.dirname(os.path.dirname(IFFDUMP))
LIVE = os.path.join(ROOT, 'game-data/The Sims/GameData/Live.iff')

def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    m = importlib.util.module_from_spec(spec)
    sys.modules[name] = m
    spec.loader.exec_module(m)
    return m

r122 = load('r122walk', os.path.join(IFFDUMP, 'r122', 'make_r122_buycat_canon.py'))

data = open(LIVE, 'rb').read()
WANT = {133: 'HouseSubpanelLabels', 135: 'HouseSubpanelPopupText', 138: 'HouseSubpanelSize'}

for c in r122.walk_chunks(data):
    if c['cc'] == b'STR#' and c['id'] in WANT:
        full = data[c['off']:c['off'] + c['size']]
        sha = hashlib.sha256(full).hexdigest()
        t = r122.table_info(c, data)
        eng = [v.decode('mac_roman', 'replace') for _ri, v in t['eng']]
        assert c['label'] == WANT[c['id']], (c['label'], WANT[c['id']])
        print('== STR# %d %r size=%d sha256=%s English=%d ==' % (c['id'], c['label'], c['size'], sha, len(eng)))
        for i, s in enumerate(eng):
            print('  [%2d] %r' % (i, s))
        print()
