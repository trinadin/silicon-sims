# R146 — far-zoom BLACK ROAD/GROUND investigation (photo 3 report)

User report: "road and ground turns black when zoomed out. Sometimes pops
back in." (attached 170x183 crop of the black region; also visible in the
photo-1 lot view: surround dark beyond the lot).

## Reproduction harness (NEW in uisurvey, states Z*)

`zoomDump(tag, zoom, preciseZoom)` forces the zoom through the REAL
WorldState properties (InvalidateZoom/InvalidatePreciseZoom), runs
`world.PreDraw(gd)` (the world renders into the PPX backbuffer — the scene
the frame loop blits), then snapshots the PPX backbuffer texture to
`uisurvey-<tag>.png` with a dark-pixel census. Dumped states:
zoomcur (control), zoomfar, zoommed, zoomdec1 (Medium enum + 0.25 precise),
zoomdec2 (Far enum + 0.5), zoomfar2 (repeat), plus a subworld culling probe.

## Results (1024x768, dark = RGB sum < 30, sampled)

| state | zoom × precise | dark |
|---|---|---|
| zoomcur (first render) | Medium × 0.5 | 5543/14839 = **37%** |
| zoomfar | Far × 0.25 | 14072/14839 = **95%** |
| zoommed (after Far) | Medium × 0.5 | 9029/14839 = **61%** |
| zoomdec1 | Medium × 0.25 | 12196/14839 = **82%** |
| zoomdec2 | Far × 0.5 | 12245/14839 = **83%** |
| zoomfar2 | Far × 0.25 | 14162/14839 = **96%** |

Spatial maps (dumps/uisurvey-zoom*.png): the healthy control shows the full
isometric lot diamond; each subsequent zoom switch SHRINKS the rendered
content — the wall/object band survives (drawn via the _2D sprite caches)
while the TERRAIN (TerrainComponent / GrassEffect / FloorGeometry path, which
2D-immediate mode draws directly) goes black. The degradation is
**cumulative across zoom switches** (37 → 95 → 61 → 82 → 83 → 96), not a pure
function of (zoom, precise) — dec2 at the same parameters as the healthy
control's zoom is 83% dark. That matches the user's "sometimes pops back in"
(the damage level shifts with each camera event) and the fact that a fresh
lot load renders correctly.

## Findings

1. **Reproduced headlessly.** First synchronous render healthy; terrain
   progressively black with zoom cycling. Walls/objects keep drawing.
2. **NOT subworld culling.** Simitone never creates subworlds at all —
   `RestoreSurroundings` (VMLotTerrainRestoreTools) is only called from TSO
   netplay/UIOptions paths; the TS1 client loads lots without it
   (subprobe: "no subworlds"). The surround the user sees is the MAIN
   TerrainComponent's own beyond-lot region (grass + roads painted by
   RestoreTerrain from vm.TSOState.Terrain.Roads).
3. **Candidate mechanisms** (not yet proven — needs the dedicated round):
   - `BlueprintChanges.PreDraw`: PRECISE_ZOOM ⇒ DrawImmediate=true; the
     static-surface rebuild in `WorldStatic.PreDraw` is SKIPPED whenever
     DrawImmediate is set, so the static cache (which bakes the terrain)
     only rebuilds on a frame where nothing zoomed — a state that may never
     arrive while SmoothZoom tweens run.
   - The immediate-mode terrain goes through GrassShader + FloorGeometry
     per-(zoom,rotation) caches; a bad rebuild after the first zoom
     invalidation would persist (cumulative damage pattern).
4. The FreeSO submodule is upstream-vanilla for these files (single upstream
   commit) — this is an upstream FreeSO behavior on this MonoGame/macOS
   build, not a fork regression.

## Next round proposal

Dedicated terrain-renderer round: instrument GrassEffect constants +
FloorGeometry cache rebuilds across a zoom cycle (the Z* harness already
reproduces deterministically in-gate), compare the static-surface path
(healthy bake) vs the immediate path, and fix whichever state leaks. The
surrounding-lots system (subworlds) is a separate, currently DORMANT feature
in this client — restoring it (RestoreSurroundings wiring + the §SubWorld
draw paths) would also bring true neighboring-lot rendering to far zoom.
