#!/usr/bin/env python3
"""Locate blank catalog tiles precisely by scanning the whole grid band."""
import numpy as np
from PIL import Image

R240 = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/build/ui-audit/r240"
cap = np.array(Image.open(f"{R240}/default/uisurvey-buy.png").convert("RGB"), dtype=np.int32)

# grid band approx y 668..745, x 410..1015. Slide a 45px window to find uniform light-blue cells
band = cap[660:750, 405:1020]
h, w = band.shape[:2]
results = []
for y0 in range(0, h-45, 4):
    for x0 in range(0, w-45, 4):
        cell = band[y0:y0+45, x0:x0+45]
        m = cell.reshape(-1,3).mean(axis=0)
        s = cell.reshape(-1,3).std(axis=0)
        # uniform light blue: std < 8, blue > green > red, brightness > 90
        if s.mean() < 8 and m[2] > 120 and m[1] > 90 and m[0] < m[1] and m[1] < m[2]:
            results.append((x0+405, y0+660, tuple(m.round(0))))
# cluster
seen = []
for x, y, m in results:
    if not any(abs(x-sx) < 20 and abs(y-sy) < 20 for sx, sy, _ in seen):
        seen.append((x, y, m))
for s in seen:
    print("uniform light-blue 45x45 cell at", s)
