# R147 round summary — far-zoom black road/ground: root cause + fix

Scope: the dedicated terrain round promised by R146 (user photo 3, plus the
photo-1 wedge). Full investigation in `zoom-black-rootcause.md`.

## What happened

1. **Battery v1** (real-setter zoom dumps + internal-state diag): bisected
   the zoom-dirty machinery — none of FullReset/ClearTextureCache/
   RecacheWalls/WorldSpace.Invalidate caused the black; even the no-machinery
   silent-far draw went "black".
2. **Battery v2/v3** (terrain-only isolation renders, static-surface
   snapshots, lighting dumps): the terrain renders HEALTHY at every state —
   the "dark bands" are the asphalt ROADS (13,14,12 with dashed markings);
   dark-censuses counted roads + void. No light-map collapse, no depth leak,
   no cumulative corruption (t3 == t1 byte-identical).
3. **The `zoomlive` live-frame probe** (the decisive instrument): drove the
   game's own frame loop — TargetZoom tween to far and back. Findings:
   - Live PreciseZoom is 1.0 for the whole session (R146's forced 0.25/0.5
     states were unreachable).
   - Live far-centered view: fully covered, healthy.
   - Scrolling toward a corner: 1% → 89% black, frozen after stopping —
     the camera left the lot terrain; beyond it is void.
4. **Root cause**: `World.BoundView` never bounded a TS1 lot (factors
   1.20/1.05/0.5 × lot width), and the lot loads with the camera at the lot
   corner (`center=0,0`) — the wedge in the user's photo 1. Original TS1
   cages the camera to the terrain.

## The fix

- FreeSO submodule `cc8bf5a7` (committed FIRST): BoundView rewritten to the
  terrain-covering cage — clamp u=(x−y), v=(x+y−lotW) so the view rectangle
  stays inside the terrain diamond bbox; center when the view is larger; 2D
  modes only.
- Gate: NEW `uizoomcage` (87→88) pins the cage law deterministically.
- uisurvey: Z-battery removed (unreachable states — disclosed in
  AUTOTEST.md), `zoomlive` live probe kept as the user-flow regression
  watcher.

## Verification

- Narrowed run: uizoomcage PASS (15/15 extremes clamped × 3 zooms),
  uisurvey PASS, probe censuses healthy (far 32% dark incl. roads; medium
  return 1.8%; scroll saturates 41% = original edge-sliver vs 89% pre-fix).
- Full gate: see `r147-gate.txt`.
