# R218 — the view pie WIRED

User directive after R217's retirement: "I would like everything wired up."
This round ports the engine's view pie (cDDDSimsView+0x13c cTSPieMenu with
the four ViewMenu sheet items) as a live, interactive surface.

## What was already decoded (r217 + r142)

- The four sheets are cTSPieMenu ITEMS — cDDDSimsView::Init 0x218c64-0x218f98
  creates them (cTSWinBtn, SetImage 9x1 / 5x1) and InsertString's them into
  the pie in the order **ZoomOut (+0x130), RotateRight (+0x138), ZoomIn
  (+0x12c), RotateLeft (+0x134)** (the call order at 0x218ef0/0x218f24/
  0x218f58/0x218f8c).
- The pie's chrome: member 825 ViewMenuBackground.bmp 17x17 chip
  (SetPopupBackgroundBuffer), max radius 90, label RGB(187,187,187),
  highlight cyan, selected white, lowlight (185,185,208); click sounds on;
  the r142 Layout quantizer (4 items -> 8 slices); CalcItem = angle/distance
  from the popup center; TSOnMouseUpL completes the gesture; TSOnKeyDown
  Escape cancels.
- UpdateViewMenu @0x215b42 refreshes the item ladder cells (current state
  view+0x115c, clamped pending deltas +0x1164/+0x1168).
- The panel buttons' CLICK path is step-only (DoSpeedTransitionSound 0x2b2f90
  + a float speed curve; whole-region scan: view+0x13c is referenced only by
  Init/CleanUp/UpdateViewMenu/TSOnCommand).

## What is NOT statically recoverable (disclosed)

- **The pop gesture.** The popup is invoked through the window-manager
  virtual layer (no direct or field-referencing caller exists in the binary;
  the PEF vtable relocations needed to trace the dispatch were not
  preserved in the r142 evidence). The port models the classic tear-off:
  **dragging >= DragThreshold (10 px) while a small zoom/rotate button is
  held opens the radial at that button; a plain click still steps.** This is
  consistent with every decoded fact: the pie handles its own MouseDownL
  (pop-then-drag gesture), the button click path provably steps only, and
  the pie completes on release.
- The exact UpdateViewMenu cell arithmetic (the /42 quantize against two
  pie float getters, the key-state adjustments). Port model: zoom items
  preview their step from the port's 3-level zoom (base cell = level*2 of
  9); rotate items sit at +/-1 of the 5-cell center 2.
- The InsertString tag strings (TOC-resident, unrecoverable) — the items
  carry the STR# 138 tooltip names ([6] zoom out, [4] rotate right, [5]
  zoom in, [3] rotate left) as their captions.
- DoSpeedTransitionSound's sound event (an f64 TOC table through 0x3093e0,
  unrecoverable). The port's zoom already eases (InitiateSmoothZoom); no
  sound is invented — the pie itself uses the ported pie sounds
  (PieMenuAppear/Highlight/Select, the engine's click-sounds flag = 1).

## Port

**`UIOriginalViewPie`** (new, Simitone.Client/UI/Panels/): a UIContainer
centered at its anchor — the original disc chip scaled by the same grow
animation the interaction pie uses; the four items as cropped sheet cells
(Texture2D crops, provenance-registered) on the radius-90 even spokes of
the 8-slice quantizer in the engine's InsertString order (ZoomOut top,
RotateRight right, ZoomIn bottom, RotateLeft left); captions in the pie
label color on font 8; CalcItem-style angle/distance hover with
PieMenuHighlight on hover change; completion on the release of the opening
gesture (selection -> PieMenuSelect + the UCP's own step handler) or Escape
(cancel); an onClose callback ALWAYS drops the owner's reference (the
first soak run caught the bug: cancel left it stale, suppressing the step
handlers forever — the gate earned its keep).

**`UIDesktopUCP`**: `UpdateViewPieGesture` — arm on small-button press,
open at >= DragThreshold px of drag while held (the pie anchors at the
button's screen center, the UIPieMenu mounting idiom); the step handlers
(ZoomControl/RotateClockwise/RotateCounterClockwise) no-op while the pie is
up (the gesture belongs to the pie; a plain click never drags so it still
steps). Probe path: `OpenViewPieForProbe(buttonKey)`,
`SelectViewPieItem(key)` (the real dispatch).

## Gate

NEW `uiviewpie` in the default suite (**126 -> 127 checks**): the real open
path (4 items, engine order/keys, cell dims 42x84 / 84x42, radius-90 spoke
centers), the ladder cells + the two consts, dedupe (one pie at a time),
selection dispatch through the UCP's own handlers (Rotation steps unless
the port-native camera hold-mode gate applies — pinned honestly either
way), and cancel/close cleanup + re-open. Targeted soak
uiviewpie/uismall/uipie + corpus: 8/8 (after the honest first-run fix).
Full gate: **passed=127 failed=0**, AUTOTEST_WRAPPER_EXIT=0.

## Evidence

`r218-disasm-dddsimsview-tsoncommand.txt` (carried from the r217 capture —
the cmd-13/pie decode), `targeted-soak.log` (both runs), `gate-run.log`.
Simitone-only change (no FreeSO submodule move). No proprietary payload.
