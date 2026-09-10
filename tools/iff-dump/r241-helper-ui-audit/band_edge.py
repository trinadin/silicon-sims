#!/usr/bin/env python3
"""Measure the live/band backdrop right edge at 1280x800 vs 1024x768."""
import numpy as np
from PIL import Image

R240 = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/build/ui-audit/r240"
for s in ("1280x800", "default"):
    a = np.array(Image.open(f"{R240}/{s}/uisurvey-live.png").convert("RGB"), dtype=np.int32)
    h, w = a.shape[:2]
    row = a[h-8]  # near-bottom row
    # scan from right for the first navy-ish pixel (b > r+30 and b > 60)
    edge = None
    for x in range(w-1, 0, -1):
        r, g, b = row[x]
        if b > int(r) + 30 and b > 60:
            edge = x
            break
    print(f"{s}: window {w}x{h}, rightmost navy band pixel at x={edge}")
    # also check band uniformity between x=1000..edge-4 at that row
    if edge:
        seg = row[edge-40:edge-4]
        print(f"   band color near right edge: mean={seg.reshape(-1,3).mean(axis=0).round(0)}")
