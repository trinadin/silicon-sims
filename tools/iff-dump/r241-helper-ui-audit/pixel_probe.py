#!/usr/bin/env python3
"""Offline pixel probes for r241 UI audit. Read-only on captures; prints stats only."""
from PIL import Image
import numpy as np
import sys, os

R240 = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/build/ui-audit/r240"
UIGR = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/uigr-png"

def load(p):
    return np.array(Image.open(p).convert("RGB"), dtype=np.int32)

def region_stats(a, x, y, w, h, label):
    r = a[y:y+h, x:x+w]
    mean = r.reshape(-1, 3).mean(axis=0)
    std = r.reshape(-1, 3).std(axis=0)
    print(f"  {label}: mean RGB=({mean[0]:.0f},{mean[1]:.0f},{mean[2]:.0f}) std=({std[0]:.0f},{std[1]:.0f},{std[2]:.0f})")
    return mean

def cas_hole_probe(setname, ox, oy):
    p = f"{R240}/{setname}/uisurvey-casorig-cac.png"
    a = load(p)
    print(f"[{setname}] {os.path.basename(p)} size={a.shape[1]}x{a.shape[0]} artOffset=({ox},{oy})")
    # VITA_RECT at (618,145) 100x220 in art space; sample inner area away from edges
    hx, hy, hw, hh = 618+ox+10, 145+oy+10, 80, 200
    mh = region_stats(a, hx, hy, hw, hh, "vita hole inner")
    # surrounding painted art frame (between window frame and hole edge): art coords ~ (590..614, y 150..360)
    region_stats(a, 590+ox, 150+oy, 20, 200, "art frame left of hole")
    region_stats(a, 722+ox, 150+oy, 20, 200, "art frame right of hole")
    # world visible outside panel (default run only)
    if a.shape[1] > 820:
        region_stats(a, 850, 200, 100, 100, "world terrain right of panel")
    return mh

art = load(f"{UIGR}/0106_CreateACharBack.png")
print("[original art] 0106_CreateACharBack.png", art.shape[1], "x", art.shape[0])
region_stats(art, 628, 155, 80, 200, "original vita window inner")
region_stats(art, 590, 150, 20, 200, "original art frame left")
region_stats(art, 722, 150, 20, 200, "original art frame right")

# default run: art at top-left (engine quirk), so offset (0,0)
cas_hole_probe("default", 0, 0)
# 800x600: art fills window, offset (0,0)
cas_hole_probe("800x600", 0, 0)
# 800x600@2x: physical 2x
try:
    a2 = load(f"{R240}/800x600@2x/uisurvey-casorig-cac.png")
    print(f"[800x600@2x] size={a2.shape[1]}x{a2.shape[0]}")
    region_stats(a2, (628)*2, (155)*2, 80*2, 200*2, "vita hole inner (2x coords)")
except Exception as e:
    print("2x probe:", e)
# 1280x800
cas_hole_probe("1280x800", 0, 0)

# Buy catalog: empty tile check. default run buy panel: item grid starts ~x=413, tiles 47px?
# Instead scan the item-grid band for near-uniform tiles.
def buy_tiles(setname):
    p = f"{R240}/{setname}/uisurvey-buy.png"
    a = load(p)
    H, W = a.shape[0], a.shape[1]
    print(f"[{setname}] buy {W}x{H}")
    # grid band: bottom area right of category panel; find via the known light-blue empty tile at ~ (695,690) in 1024x768 default
    if W == 1024:
        band = a[675:745, 410:1010]
        # tile grid: first row y ~675..722, 12 columns pitch 47 starting x=418
        y0 = 676
        for row, y in enumerate((676, 723)):
            cells = []
            for c in range(12):
                x = 418 + c*47
                cell = a[y+4:y+40, x+4:x+43]
                m = cell.reshape(-1,3).mean(axis=0); s = cell.reshape(-1,3).std(axis=0)
                cells.append("EMPTY" if s.mean() < 6 and m[2] > m[0] and m[2] > 80 else "item")
            print(f"  row{row}: {cells}")
buy_tiles("default")
buy_tiles("800x600")
