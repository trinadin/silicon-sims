#!/usr/bin/env python3
"""Test wrong-palette hypothesis: group capture hole pixels by simulated art index;
low intra-index variance => capture is the same art through a different palette.
Then recover the effective palette for the window's indices and compare with the real one."""
import struct, io
import numpy as np
from PIL import Image
import sys
sys.path.insert(0, "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/r241-helper-ui-audit")
from rle_sim import decode_rle8, blob, w, h  # reuse the extraction + faithful-law decode

ROOT = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims"

# real palette
size = struct.unpack("<I", blob[14:18])[0]
pal_off = 14 + size
pal = []
for i in range(256):
    b, g, r, _ = blob[pal_off+i*4: pal_off+i*4+4]
    pal.append((r, g, b))

idx = decode_rle8(blob, 1078, w, h, "bmp")  # correct-law decode -> true indices
cap = np.array(Image.open("/Users/nathannoom/Developer/Games/The Sims/simitone-fork/build/ui-audit/r240/default/uisurvey-casorig-cac.png").convert("RGB"), dtype=np.int32)[:600, :800]

hole = idx[150:360, 622:714]
caph = cap[150:360, 622:714]
groups = {}
for y in range(hole.shape[0]):
    for x in range(hole.shape[1]):
        groups.setdefault(int(hole[y, x]), []).append(caph[y, x])
print("distinct indices in hole:", len(groups))
variances = []
for k, px in sorted(groups.items()):
    if len(px) < 40:
        continue
    arr = np.array(px)
    v = arr.std(axis=0).mean()
    variances.append((k, len(px), v, tuple(arr.mean(axis=0).round(0)), pal[k]))
for k, n, v, mean, real in variances[:15]:
    print(f"idx {k:3d} n={n:5d} intra-std={v:5.1f} capture-mean={mean} real-palette={real}")
overall = np.mean([v for _, _, v, _, _ in variances])
print(f"mean intra-index std = {overall:.1f}  (tiny => same art, different palette)")
