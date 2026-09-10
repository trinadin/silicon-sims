# R217 — the "phantom buttons on hold": the ViewMenu sheets are PIE MENU items

User report: clicking rotate/zoom with a lot loaded pops ADDITIONAL
rotate/zoom buttons next to the real ones, only while the mouse is held.

## What R193 had mounted

Four `UIOriginalNavbarButton` plaques (ViewMenuZoomIn/Out 9x1, 
ViewMenuRotateLeft/Right 5x1) on the desktop UCP, `Visible = Button.IsDown`,
cell quantized from the port's camera state. R193 disclosed the visibility
law as modeled ("shown while the matching small button is held") and the
ladder geometry as undecoded. The user-visible result read as duplicated
buttons — and the model was wrong.

## The corrected decode

**1. The gadget class and owner.** The four sheets are created in
`cDDDSimsView::Init` (0x218c64–0x218dd4, r193 disasm): four
`new(0x18c) + cTSWinBtn` (ctor 0x50d720) with `SetImage(buf, 9/5, 1)`,
stored at view+0x12c (ZoomIn), +0x130 (ZoomOut), +0x134 (RotateLeft),
+0x138 (RotateRight). Immediately after, view+0x13c gets
`new(0x18c) + ctor 0x52a4f0` — symbol index: **`cTSPieMenu::__ct`**.

**2. The registration.** Init 0x218ee8–0x218f98 calls, for each label,
`0x4f4c90(&str, tag)` (cTSString from data) then
`pie->vt[0x1b0](str, gadget)` — symbol: **`cTSPieMenu::InsertString(const
cTSString&, cTSWinBtn*)`** @0x5283f2. The four sheets are PIE MENU ITEMS,
handed to the pie with tag names. They are not control-panel children and
never sit "next to the small buttons".

**3. The pie popup chrome.** Init loads factory member **825 (0x339)**
into view+0x118, preps it (0x487660) and calls `pie->vt[0x230](buf, 0)`
with the gray `0x505780(this, 0xbb, 0xbb, 0xbb)` color at pie+0x64 — the
`SetPopupBackgroundBuffer` family (symbol @0x529f52). Member 825 is
`cpanel\ViewMenuBackground.bmp` — **17x17, 940 bytes** (R193 never
identified it). The pie also gets pie->0x50 = the view (owner backptr),
pie->0x174 = 6, pie->0x178 = 0x28, command id 0x5a via vt[0x1e8], and
`vt[0x254](0)` (hidden initially). cTSPieMenu is a full POPUP subsystem:
`Popup/PupupBegin/PopupEventLoop/EndPopup`, `Layout` (radial),
`CalcItem` (angle/distance selection), `TSOnMouseMove/TSOnMouseUpL`
(drag-select, commit) — items render ONLY while the popup is up.

**4. The frame law.** `cDDDSimsView::UpdateViewMenu(bool)` @0x215b42
(r217-disasm-updateviewmenu.txt), called from `TSOnCommand` when the pie
sends cmd 13 (sender == view+0x13c): reads the pie's state enum
(vt[0x1e0]) and two float getters (vt[0x1d0]/vt[0x1d8]), quantizes their
difference (the 0x30C30C31 mulhw magic ≈ /42), clamps pending deltas into
view+0x1164 (zoom) / view+0x1168 (rotation, ±2 bounds) against the current
state at view+0x115c (0/1/2/4), then sets the four item FRAMES via
`0x50bff0(gadget, cell)` — ZoomIn=r4, ZoomOut=r28, RotateLeft=r30,
RotateRight=r29 — i.e. each item's ladder cell tracks the current/target
camera state, refreshed while the pie is active.

**5. The panel buttons' own law.** `cWinViewControl::TSOnCommand`
zoom/rotate branches (0x2b4c30–0x2b5094 region) call
`DoSpeedTransitionSound(this, dir, amount)` @0x2b2f90 + a float speed
curve (0x591720) — an ANIMATED camera step per click. No pie popup, no
flyout. (Whole-region field scan: view+0x13c is referenced ONLY by Init,
CleanUp, UpdateViewMenu, TSOnCommand — the pie is never triggered from the
panel buttons or the view's mouse handlers.)

**6. The art confirms it** (local PIL extraction to /tmp/r217-art, ASCII
maps in the round transcript): ViewMenuZoomIn/Out are 9-cell ladders whose
icon position inside each tall 42x84 cell encodes the level;
RotateLeft/Right are 5 wide cells of direction indicators;
ViewMenuBackground is a flat 17x17 chip — pie item chrome, not panel
plaques.

## Port (R217)

- The R193 mounts are RETIRED: the four label fields, their ctor block and
  the Update hold-gating/quantization are deleted from UIDesktopUCP. The
  small zoom/rotate buttons keep their click-step behavior (and their
  UseRotateHold/UseZoomHold continuous-drag paths, which are port-native
  camera controls, unchanged).
- Gate: `uismall` re-pinned — the art family now includes
  ViewMenuBackground 17x17, and the live label pins are REPLACED by an
  absence assertion (R214 precedent): zero ViewMenu-sized cells anywhere in
  the desktop UCP subtree + the four small buttons still mounted.

## Residuals disclosed

- The four ViewMenu sheets are not wired as ITEMS into the ported UIPieMenu
  (the R142 interaction pie, which already carries the disc/button art,
  palette, radius 90 and the slice quantizer). Their engine trigger is the
  pie's own popup path (the pie is a window-manager-level popup); the
  panel buttons never open it. cObjPickerTool is a second pie consumer in
  the engine to account for.
- The panel buttons' ANIMATED step + transition sound
  (DoSpeedTransitionSound) is not ported (the port steps the camera
  directly) — separate refinement.

## Gate

Targeted soak uismall+corpus: PASS 6/0 first run. Full default gate:
**passed=126 failed=0**, AUTOTEST_WRAPPER_EXIT=0. Simitone-only change
(no FreeSO submodule move this round).

Evidence: `r217-disasm-updateviewmenu.txt`,
`r217-disasm-dddsimsview-tsoncommand.txt`, `targeted-soak.log`,
`gate-run.log`. Art analyzed locally in-place; no proprietary payload.
