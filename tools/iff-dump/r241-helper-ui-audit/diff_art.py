#!/usr/bin/env python3
"""Whole-image diff: capture casorig-cac art area vs original CreateACharBack.
Tests whether the mauve hole is a color remap of the same art or a different image."""
from PIL import Image
import numpy as np

UIGR = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/uigr-png/0106_CreateACharBack.png"
CAP = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/build/ui-audit/r240/default/uisurvey-casorig-cac.png"

art = np.array(Image.open(UIGR).convert("RGB"), dtype=np.float64)
cap = np.array(Image.open(CAP).convert("RGB"), dtype=np.float64)

# default run: art at top-left (0,0), 800x600 within 1024x768
A = art
C = cap[:600, :800]

diff = np.abs(A - C).mean(axis=2)
print(f"whole-art mean abs diff = {diff.mean():.1f}")
# map of differences by region: 10x10 blocks, report blocks with mean diff > 25
print("high-diff blocks (x0,y0 -> meandiff):")
blocks = []
for by in range(0, 600, 60):
    for bx in range(0, 800, 80):
        d = diff[by:by+60, bx:bx+80].mean()
        if d > 25:
            blocks.append((bx, by, d))
for bx, by, d in sorted(blocks, key=lambda t: -t[2])[:20]:
    print(f"  ({bx:3d},{by:3d}) {d:.0f}")

# hole region stats again (art-space)
hole_art = A[145:365, 618:718]; hole_cap = C[145:365, 618:718]
print(f"hole art mean={hole_art.reshape(-1,3).mean(axis=0).round(0)} cap mean={hole_cap.reshape(-1,3).mean(axis=0).round(0)}")

# per-channel linear fit cap = a*art + b inside hole
for ch, name in enumerate("RGB"):
    x = hole_art[:, :, ch].ravel(); y = hole_cap[:, :, ch].ravel()
    a, b = np.polyfit(x, y, 1)
    resid = y - (a*x+b)
    print(f"  {name}: fit y={a:.2f}x+{b:.1f} residual std={resid.std():.1f} (art std={x.std():.1f})")

# control: same fit in a NON-hole textured art region (button fan edges 400..560, 100..380)
fA = A[100:380, 400:560]; fC = C[100:380, 400:560]
for ch, name in enumerate("RGB"):
    x = fA[:, :, ch].ravel(); y = fC[:, :, ch].ravel()
    a, b = np.polyfit(x, y, 1)
    resid = y - (a*x+b)
    print(f"  fan {name}: fit y={a:.2f}x+{b:.1f} residual std={resid.std():.1f} (art std={x.std():.1f})")
