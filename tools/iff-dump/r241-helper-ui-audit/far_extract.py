#!/usr/bin/env python3
"""Extract candidate CAS background BMPs from corpus FARs and stat the Vita window region.
Read-only on game data; prints analysis only."""
import struct, sys, io
from PIL import Image
import numpy as np

ROOT = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims"

def far_members(path, want):
    data = open(path, "rb").read()
    magic = data[:8]
    if magic != b"FAR!byAZ":
        # FAR v3 ('FAR!byAZ' still) - handle only v1 here
        print(f"  {path}: magic={magic!r} skip")
        return
    man = struct.unpack("<I", data[12:16])[0]
    num = struct.unpack("<I", data[man:man+4])[0]
    off = man + 4
    for i in range(num):
        dlen, d2, doff, nlen = struct.unpack("<IIII", data[off:off+16])
        nm = data[off+16:off+16+nlen].decode("latin1", "replace")
        off += 16 + nlen
        base = nm.replace("\\", "/").split("/")[-1].lower()
        if base in want:
            blob = data[doff:doff+dlen]
            yield nm, blob

def bmp_stat(nm, blob):
    try:
        im = Image.open(io.BytesIO(blob)).convert("RGB")
    except Exception as e:
        print(f"  {nm}: {len(blob)}B not-direct-BMP ({e})")
        return
    a = np.array(im, dtype=np.int32)
    h, w = a.shape[:2]
    print(f"  {nm}: {w}x{h}")
    if (w, h) == (800, 600):
        r = a[155:355, 628:708].reshape(-1, 3)
        m = r.mean(axis=0); s = r.std(axis=0)
        print(f"    vita-window inner mean=({m[0]:.0f},{m[1]:.0f},{m[2]:.0f}) std=({s[0]:.0f},{s[1]:.0f},{s[2]:.0f})")

WANT = {"createacharback.bmp"}
import glob
for far in [f"{ROOT}/UIGraphics/UIGraphics.far", f"{ROOT}/Deluxe/Deluxe.far"]:
    print(f"== {far}")
    for nm, blob in far_members(far, WANT):
        bmp_stat(nm, blob)

# Also: what does the uigr-png say (known-good extraction of UIGraphics.far uigr 106)
a = np.array(Image.open("/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/uigr-png/0106_CreateACharBack.png").convert("RGB"), dtype=np.int32)
r = a[155:355, 628:708].reshape(-1, 3)
m = r.mean(axis=0)
print(f"uigr-png 0106 vita-window mean=({m[0]:.0f},{m[1]:.0f},{m[2]:.0f})")

# capture mauve for reference
c = np.array(Image.open("/Users/nathannoom/Developer/Games/The Sims/simitone-fork/build/ui-audit/r240/default/uisurvey-casorig-cac.png").convert("RGB"), dtype=np.int32)
r = c[155:355, 628:708].reshape(-1, 3)
m = r.mean(axis=0); s = r.std(axis=0)
print(f"capture hole mean=({m[0]:.0f},{m[1]:.0f},{m[2]:.0f}) std=({s[0]:.0f},{s[1]:.0f},{s[2]:.0f})")
# correlation: is capture hole texture correlated with art at slightly offset? try a few offsets
best = None
for dy in range(-8, 9, 2):
    for dx in range(-8, 9, 2):
        art = np.array(Image.open("/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/uigr-png/0106_CreateACharBack.png").convert("L"), dtype=np.float32)
        cap = c[155:355, 628:708].mean(axis=2)
        sub = art[155+dy:355+dy, 628+dx:708+dx]
        c1 = cap - cap.mean(); c2 = sub - sub.mean()
        corr = (c1*c2).sum()/np.sqrt((c1*c1).sum()*(c2*c2).sum()+1e-9)
        if best is None or corr > best[0]:
            best = (corr, dx, dy)
print(f"best art/capture grayscale correlation in hole: corr={best[0]:.3f} at offset dx={best[1]} dy={best[2]}")
