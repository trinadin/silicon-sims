# R221 — the censorship blur's pixel-level mosaic check (censorpixel; default suite)

The PARITY row said: "Censorship blur — trigger→PreFrame→renderer pipeline verified
end-to-end (IFF-literal pd30, live exec 0→3→0, render pair); **pixel-level mosaic
check not run**." This round ran it — and closed the row.

## Discovery 1: the R36 render pair was evidence-free

The saved `r36-censor-{off,on}.png` pair (committed R36, regenerated every gate
run since) **differs by exactly 0 pixels** — measured this round. `GetLotThumb`
never rasterizes the avatar censor overlay, so the "render pair" proved nothing
pixel-level. Any real check must capture through the LIVE world composition.

## The ORIGINAL's mosaic law (decoded from the binary)

Symbols (r141 index; NOTE the index addresses for this region are **+2 off** —
the real prologues sit at symbol+2, found by scanning for `mflr r0`):

```
HouseViewer::Censor(const Rect*, int, int, int, int, RenderParam*) @0x1ce894 (1412B)
HouseViewer::RenderCensoredBlocks(const Rect*, RenderParam*)           @0x1ce5b4 (584B)
SAnimator::GetCensorPt / GetCensorRect / GetPixelated / SetPixelated /
         UpdateCensorDressing                                        @0x3538xx
```

`Censor` (the fill loop, disassembly in this dir):
- accumulates R/G/B sums over each block's pixels (r20/r21/r22, count r27),
- `divw r20/r21/r22, r27` — **per-cell MEAN of the underlying pixels**,
- then THREE calls to a random source (0x1411d0), each `clrlwi r3,r3,0x1c`
  (= &0xF) minus 8 — **a ±8-per-channel jitter added to the mean**,
- clamp ladder 0..255 per channel, packed `r22 | r20<<16 | r21<<8` — written
  as one flat color per cell. The jitter is a fingerprint no static texture
  reproduces.

`RenderCensoredBlocks` carries the zoom cell ladder — pairs
(1,3), (2,6), (4,12) = block w×h per zoom tier (tall cells, ×2 per zoom-out).
`SAnimator` tracks the censor rect at the sim and the pixelated flag;
`UpdateCensorDressing` drives the dress-state transitions (the pd30 side the
legacy `censor` check already pins IFF-literally).

## The port model (what the check pins)

`Avatar.DrawCensoredMeshesPixelated`: a generated 64×64 texture = an **8×8 grid
of 12 skin-tone palette colors** (deterministic `(gx*3+gy*7)%4` pattern), drawn
opaque via SpriteBatch at the projected PELVIS position, sized by zoom tier
(35/50/70 px wide, 1.4× tall, PointClamp). **Disclosed approximation vs the
original**: no underlying-pixel averaging, no ±8 jitter, and a fixed grid
instead of the 1×3/2×6/4×12 cell ladder. Flag flow: `AvatarComponent:216`
passes `CensorshipFlags` into `Avatar.DrawGeometry`.

## The probe (check `censorpixel`, now in the DEFAULT suite)

At LOT-READY: frame the camera on avatar0 (simvis idiom), grab the live
composition (`world.PreDraw+Draw` to a render target, read back) with the
component's `CensorshipFlags` forced 0 then 3, restore everything, save the
pair to UserDir, then analyze. The analysis path took four honest iterations
(all logs kept):
1. raw diff bbox — animation drift between grabs inflates it (run 1/2);
2. palette-color mask over the whole frame — the palette IS skin tones, so the
   sim's own body matches (run 3: mask spread over the whole screen);
3. fixed window on the sim's ground point — the mosaic draws at the PELVIS,
   ~125px ABOVE it, diluting density/palette (run 4);
4. **window search** (final): slide a 96×112 window over the sim's vicinity,
   take the densest diff cluster, analyze there (run 5: PASS).

## Observed (run 5, the passing law)

```
totalDiff=5979px  window=96x112 @(464,181)  (simScreen=(513,306) — pelvis above)
winDiff=5824 (97% of ALL diff)  density=0.54  paletteFrac=0.80
medianRun=7  longestRun=70   (the 8x8 grid cells over the zoom rect)
PASS gates: winDiff>=1000 && density>=0.25 && (medianRun>=3||longest>=24) && paletteFrac>=0.8
```

The mosaic block holds essentially the entire diff; the flat-run median of 7
matches the port's grid law (rect/8 ≈ 6–9 px cells at this zoom tier).

## Round result

- Targeted soak `CHECKNAME,censorpixel,censor,corpus`: **passed=7 failed=0**
  (runs 1–4 kept as the honest analysis-evolution record).
- `censorpixel` PROMOTED to the default suite (simvis precedent): the count
  goes **127 → 128**; full gate `gate-run.log` — 128 checks, 0 failed.
- PARITY: the Censorship blur `[GAP-partial]` **closes** — every stage is now
  verified end-to-end INCLUDING the pixel level; residual disclosed as a port
  approximation (static palette texture vs the original's mean+jitter mosaic —
  the original's exact effect would need a shader-side downsample+noise pass).
- Frame PNGs deliberately NOT committed (EA-derived renders; the r36 pair
  remains the historical precedent).
