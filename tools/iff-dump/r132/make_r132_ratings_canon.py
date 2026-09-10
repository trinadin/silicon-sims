#!/usr/bin/env python3
"""R132 canon builder: the People-panel ratings/tracking art family ground truth.
Walks UIGraphics.far, inventories dims/bpp/sha for every ratings family member,
and prints RT-header context (id -> path) for the evidence file."""
import struct, hashlib, os, sys, importlib.util

HERE = os.path.dirname(os.path.abspath(__file__))
IFFDUMP = os.path.dirname(HERE)
ROOT = os.path.dirname(os.path.dirname(IFFDUMP))
sys.path.insert(0, os.path.join(IFFDUMP, 'r124'))
sys.path.insert(0, os.path.join(IFFDUMP, 'r122'))
import importlib
r124 = importlib.import_module('walk_far') if False else None
# load helpers the established way (r130 pattern): re-exec the modules by path
def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    m = importlib.util.module_from_spec(spec)
    sys.modules[name] = m
    spec.loader.exec_module(m)
    return m


r124 = load('r124walk', os.path.join(IFFDUMP, 'r124', 'make_r124_descpanel_canon.py'))

FAR = os.path.join(ROOT, 'game-data/The Sims/UIGraphics/UIGraphics.far')
data = open(FAR, 'rb').read()
assert data[:8] == b'FAR!byAZ'

MEMBERS = [
    "cpanel/Buttons/HouseBars.bmp",      # kHouseBars 4800 (pie strip)
    "cpanel/Buttons/JobBars.bmp",        # kJobBars 4801 (pie strip)
    "cpanel/Buttons/RelBars.bmp",        # kRelBars 4802 (pie strip)
    "cpanel/HouseSubBars.bmp",           # kHouseSubpanelBars 4900
    "cpanel/JobSubBars.bmp",             # kJobSubpanelBars 4901
    "cpanel/FameSubBars.bmp",            # kFameSubpanelBars 4822
    "cpanel/Backgrounds/HouseSubBarsOff.BMP",   # kHouseSubBarsOff 4919
    "cpanel/Backgrounds/JobSubBarsOff.bmp",     # kJobSubBarsOff 4920
    "cpanel/Backgrounds/FameSubBarsOff.bmp",    # kFameSubBarsOff 4823
    "cpanel/People/TrackingTarget.bmp",  # kTrackingTarget 4601 (R131 pinned; re-verify)
]

truth = {}
for nm, dlen, doff in r124.walk_far_manifest(data):
    truth[nm.replace('/', '\\').lower()] = (nm, dlen, doff)

print('== R132 ratings/tracking family: FAR ground truth ==')
rows = []
for member in MEMBERS:
    low = member.replace('/', '\\').lower()
    hit = truth.get(low)
    if hit is None:
        print('%-46s MISSING' % member)
        continue
    nm, dlen, doff = hit
    b = data[doff:doff + dlen]
    w, h = struct.unpack('<ii', b[18:26])
    bpp = struct.unpack('<H', b[28:30])[0]
    comp = struct.unpack('<I', b[30:34])[0]
    sha = hashlib.sha256(b).hexdigest()
    rows.append((low, dlen, w, h, bpp, comp, sha))
    print('%-46s len=%-6d %3dx%-3d bpp=%d comp=%d sha=%s'
          % (low, dlen, w, h, bpp, comp, sha))
print()
print('== gate rows (path,len,W,H,sha256) ==')
for low, dlen, w, h, bpp, comp, sha in rows:
    print('    new string[] { "%s", "%d", "%d", "%d", "%s" },' % (low.replace('\\', '\\\\'), dlen, w, h, sha))
