# R147 — "road and ground turns black when zoomed out": ROOT CAUSE + FIX

User report (R146 photos 1/3): road and ground turn black when zoomed out,
sometimes pops back in. Photo 1 (community lot, medium zoom) additionally
showed a large screen-aligned black WEDGE cutting into the lot's bottom-left
with sprites floating in it.

## The R146 misdirection (and why it was wrong)

R146's harness forced `State.Zoom` + `State.PreciseZoom` through the real
setters and snapshotted the PPX backbuffer synchronously. It measured
"far = 95% black, cumulative across zoom switches" and suspected the terrain
render path (BlueprintChanges.DrawImmediate / FloorGeometry caches).

R147's differential battery (silent-zoom bisectors, terrain-only isolation
renders, static-surface snapshots, lighting dumps) found the terrain render
itself HEALTHY at every state — the "black" was the beyond-lot VOID seen
through framing the live game never produces. Decisive corrections:

1. **The live game never changes PreciseZoom.** `UILotControl.Update` (2D
   branch) only sets `World.State.Zoom` (the enum); PreciseZoom stays 1.0
   from lot load for the whole session (`zoomlive` probe: `pz=1` at every
   frame across zoom tweens). All R146 states forcing pz=0.25/0.5 were
   unreachable states; their tiny "diamond in void" images were mismatched
   synchronous framing, not a render failure.
2. The terrain-only renders (private PPX targets, TerrainComponent.Draw
   alone) show the complete lot terrain — olive grass, brown dirt, and the
   DARK ASPHALT ROADS with dashed markings — at every (zoom enum × precise)
   combination. The "(13,14,12) dark band" that looked like missing tile
   rows is a road. `dark`-census counts roads + void, not missing terrain.
3. The static surface bake and blit are consistent: the bake renders the
   same terrain into the 1536x1280 scroll buffer; the far bake's "98% dark"
   is the small-in-buffer diamond, correct for (Far, pz=1).

## The real defect

**The camera is allowed to look past the lot terrain, and there is nothing
beyond it.** Three concrete manifestations, all matching the user's photos:

- `World.BoundView` clamped CenterTile to `boundfactor × lotWidth` from the
  lot center with factors 1.20/1.05/0.5 — on a 52-tile TS1 lot that allows
  ±62/±55/±26 tiles: effectively NO bound at Near/Medium. The camera roams
  freely off-lot; everything beyond the terrain mesh renders as the PPX
  backbuffer's transparent black. Scrolling toward a corner at medium went
  1% → 89% black (R147 `zoomlive` probe, pre-fix), frozen until the camera
  moved back — the user's "turns black... sometimes pops back in".
- The lot loads with the camera at the LOT CORNER (`terrain-extent`
  probe: `center=0,0` right after load). The first frames therefore show the
  corner view — a screen-aligned black wedge with sprites floating in it —
  exactly the user's photo 1 (community lot) shape.
- At far zoom the same void appears whenever the view is not centered; with
  the old no-op bounds the user's zoom/scroll positions routinely exposed it
  (photo 3).

The original TS1 lot view never shows this: its camera is CAGED to the lot
terrain — scrolling stops when the view reaches the terrain bounds, and
zoom levels keep the lot covering the screen. (TS1 lots are one fixed size;
the original does show a thin black edge sliver at maximum scroll, which the
new law reproduces faithfully.)

## The fix (FreeSO submodule commit cc8bf5a7)

`World.BoundView` rewritten: clamp the rotated view coordinates

- `u = CenterTile.x − CenterTile.y` (screen-X direction, tile units)
- `v = CenterTile.x + CenterTile.y − lotWidth` (screen-Y direction)

so that `|u| ≤ lotW − viewHalfW/tilePxWidthHalf` and
`|v| ≤ lotW − viewHalfH/tilePxHeightHalf` (clamped to ≥ 0; a view larger
than the terrain centers on the lot). The uv rectangle IS the terrain
diamond (isometric 45° rotation), so this keeps the view rectangle inside
the terrain's bounds at every zoom. 3D camera modes are excluded (unchanged
behavior).

Measured cage on the 52-tile autotest lot (1024x768):
Far |u|≤20 |v|≤4 · Medium |u|≤36 |v|≤28 · Near |u|≤44 |v|≤40.

## Post-fix verification

- `uizoomcage` gate check (NEW, deterministic): at Near/Medium/Far, slam the
  camera to 5 far off-lot extremes each, run the real `world.PreDraw`, and
  assert CenterTile lands inside the cage. 15/15 positions clamped at all 3
  zooms.
- `zoomlive` live-frame probe (user-flow evidence, stays in uisurvey):
  baseline → far tween → medium return → scroll-to-corner across ~320 real
  game frames. Post-fix: far centered fully covered (dark 32%, roads
  counted), medium return clean (dark 1.8%), scroll saturates at dark 41%
  (view corners touching the terrain edge at max scroll — the original's
  edge-scroll behavior) versus 89% pre-fix, and recovers.
- The R146/R147 synchronous battery was REMOVED from uisurvey (its states
  were unreachable; see `r146/zoom-black-notes.md` for the superseded
  readings).

## Follow-ups (next rounds, optional)

- Decode the ORIGINAL's exact camera-cage law from the PPC binary (the
  current law is the terrain-covering bbox cage; the original may stop at
  the diamond edge proper rather than its bbox corners).
- The neighborhood SURROUND feature (SubWorlds + RestoreSurroundings,
  `WorldConfig.SurroundingLots`, user config SurroundingLotMode=2) is still
  dormant in the TS1 client — wiring it would render real neighboring
  terrain beyond the lot instead of stopping at the edge. That is a content
  feature, not a defect: the original base-game lot view also ends at the
  lot terrain.
