#!/usr/bin/env python3
"""Identify the Vita-hole content by comparing same screen region across captures."""
from PIL import Image
import numpy as np

R240 = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/build/ui-audit/r240"

def load(p):
    return np.array(Image.open(p).convert("RGB"), dtype=np.int32)

def reg(a, x, y, w, h):
    r = a[y:y+h, x:x+w].reshape(-1, 3)
    return r.mean(axis=0), r.std(axis=0)

sets = ["default", "800x600", "1280x800"]
names = ["uisurvey-buy.png", "uisurvey-build.png", "uisurvey-casorig-cac.png"]
for s in sets:
    print(f"== {s}")
    for n in names:
        try:
            a = load(f"{R240}/{s}/{n}")
        except FileNotFoundError:
            continue
        H, W = a.shape[:2]
        if W < 730:
            print(f"  {n}: {W}x{H} too small for hole coords, skip")
            continue
        m, sd = reg(a, 628, 155, 80, 200)
        print(f"  {n}: hole-region mean=({m[0]:.0f},{m[1]:.0f},{m[2]:.0f}) std=({sd[0]:.0f},{sd[1]:.0f},{sd[2]:.0f})")

# candidates
a = load(f"{R240}/default/uisurvey-casorig-paf.png")
m, sd = reg(a, 700, 20, 200, 100)   # gray out-of-world wedge (top of paf)
print(f"paf gray wedge: mean=({m[0]:.0f},{m[1]:.0f},{m[2]:.0f}) std=({sd[0]:.0f},{sd[1]:.0f},{sd[2]:.0f})")

# pip-avatar skin (default)
try:
    a = load(f"{R240}/default/pip-avatar.png")
    H, W = a.shape[:2]
    m, sd = reg(a, W//2-20, H//2-20, 40, 40)
    print(f"pip-avatar center: mean=({m[0]:.0f},{m[1]:.0f},{m[2]:.0f}) std=({sd[0]:.0f},{sd[1]:.0f},{sd[2]:.0f})")
except Exception as e:
    print("pip-avatar:", e)

# exact-difference test: is the hole region byte-identical to the same region in buy capture?
c = load(f"{R240}/default/uisurvey-casorig-cac.png")
b = load(f"{R240}/default/uisurvey-buy.png")
d1 = np.abs(c[155:355, 628:708] - b[155:355, 628:708]).mean()
print(f"mean abs diff hole(cac) vs same-region(buy) = {d1:.2f}")
# and art-frame control region (should differ? both draws of same art -> identical)
d2 = np.abs(c[150:350, 592:612] - b[150:350, 592:612]).mean()
print(f"mean abs diff art-frame(cac) vs same-region(buy) = {d2:.2f}")
