# R120 — bottom-bar composition: MainPanel no longer overlaps the DesktopUCP (`uimpanel`)

## User report

"the general UI has issues, too. Look at the bottom-right. It's got overlap and
repetition. Something is wrong there." (with a live-mode screenshot, window
2272x1816, lot in buy mode)

## Diagnosis

Screenshot forensics (the user's image, cropped/upscaled + color-classified):

- The bottom-left round **UIDesktopUCP** renders at ~2.09x scale (292px asset
  -> ~610px on screen) and is **clipped mid-panel** at y~1663 — everything
  below (speed pills, bottom of the time plaque, bottom of the options button)
  falls into a **153px pure-black band** under the game view. The whole render
  (world + UI) sits in a 4:3-ish rect (~2135x1594) centered in the 5:4 window:
  the backbuffer was still the 4:3 boot size (config 1024x768) while the
  window had been resized — the live resize path never reallocated it.
- The four "repeated" blue squares at the bottom-right of the world are LOT
  OBJECTS (pool tiles), not UI (vision-confirmed; evenly spaced by
  coincidence).
- The overlapping/repeated CONTROL surfaces: the modern touch MainPanel
  (Simitone's catalog panel) open at the bottom-right **overlapping the UCP**.

## Reproduction (engine-level, aspect-independent)

Full gate run with `GraphicsWidth=1136, GraphicsHeight=908` (5:4, config backed
up and restored): the lot loads in buy mode (`vm.GetGlobalValue(32) > 0` opens
the MainPanel at boot) and the gate's own `uidump-lot.png` (1136x908, full-bleed
— proving the ENGINE composes correctly at 5:4) shows the open MainPanel's left
edge ~136px INSIDE the UCP's right side, covering the speed pills (local
x158-278), the time plaque and the options button. Analyzer read of the dump:
"the wide blue panel... OVERLAPS the round panel — covering roughly the right
~15% of the round panel."

Root cause: `UISimitoneFrontend` placed `MainPanel.X = 64+15 (+100 desktop)`
= **179**, a fixed offset unrelated to the UCP (which spans x15..307).

## The port

1. `UISimitoneFrontend` ctor: the DesktopUCP is now created FIRST; desktop
   mode places `MainPanel.X = DesktopUCP.X + DesktopUCP.Size.X + 8` (= 315 at
   the 292px UCP) — the open catalog panel starts right of the UCP with an 8px
   gap, matching the original's one-bottom-bar composition (UCP dial left,
   catalog right, never overlapping). `GameResized()` re-derives X the same
   way. Touch mode (Desktop == false) is unchanged.
2. `SimitoneGame.Window_ClientSizeChanged` (the black-band class): rewritten
   to treat the **GraphicsDevice.Viewport as the single source of truth** after
   ApplyChanges — the batch buffer and the logical GraphicsWidth/Height are
   derived from the actually-allocated viewport (ClientBounds can be points on
   macOS retina while the backbuffer is pixels), and the logical /DPI recompute
   now ALWAYS runs (the old early return when no screen existed yet left the
   settings in PHYSICAL units, doubling every later layout at DPI>1). Reentrant
   guard moved to try/finally.

Engine-derived choices (disclosed): the +8px gap and panel-above-bottom float
(15px, pre-existing R85+ placement) are ours; the no-overlap composition and
the UCP-left/catalog-right arrangement are the original game's.

## Gate (`uimpanel`, check 68 of 68)

Runs with uidump (post-lot-load, live screen):
- `MainPanel.X >= DesktopUCP.X + DesktopUCP.Size.X` (no overlap);
- UCP fully inside the screen (the clipped-UCP class);
- MainPanel.Y + 128 inside the screen;
- **buffer invariant**: `ScreenWidth x DPIScaleFactor == Viewport.Width` (and
  Height) — catches settings-in-physical-units / stuck-backbuffer drift on
  every future run.

## Residuals

- The MainPanel's CONTENTS are still the modern touch UI (category switcher,
  subpanels) — original buy-catalog UI is future rounds (150
  'BuyModeCatalogSortTips' candidate).
- The live-window black band requires the window to be resized after this fix
  (the stuck 4:3 backbuffer of an already-running session cannot be repaired
  retroactively); the new handler keeps buffer and settings consistent from
  now on.
- The original toolbar is one 128px strip flush at the screen bottom; the port
  floats the panels 15px above it (pre-existing, disclosed R85+).

## Verification (r120p1.log)

- `AUTOTEST uimpanel desktop bottom-bar composition: ok=True ucp=15,475 292x281 panel=315,625 ucpRight=307 screen=1024x768 viewport=1024x768 dpi=1` → PASS (first run)
- `AUTOTEST RESULT PASS passed=68 failed=0` (all 67 prior checks unchanged-green)
- Repro artifacts: full gate at 1136x908 (pre-fix) showed the overlap in the
  gate's own uidump; post-fix gate at 1024x768 pins the no-overlap geometry.
