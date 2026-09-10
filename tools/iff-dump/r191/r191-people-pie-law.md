# R191 — The people pie (family Sim selector)

Date: 2026-08-31

## Evidence boundary

Owner's local `game-data/The Sims/The Sims Complete` PPC executable
(SHA-256 `33c76da2…c06a5f`), `UIGraphics.far`, `UIText.iff`. Behavior,
constants, and hashes only.

## What the people pie is

The `cTSPieMenu` pair built in `cWinPeople::Init` (0x28eee0–0x28f244):
the family Sim selector popped from the live-mode gauge. Callers prove
its role: `GetPeoplePie` 0x28b3d0 is used by `cDDDSimsView::CancelPieMenu`
0x2125ec and the picker tools, and `cWinPeople::TrackPerson` 0x28bf00 —
the slice click — is invoked from `cWinPeople::TSOnCommand` 0x28d05c and
`cDDDSimsView::ComputeUpDownState` 0x217a68. Selection = track that person.

## Decoded configuration (r159 scout §4, re-verified)

- `factory(kLiveModeGauge 4911, this+548)` — the pop target; the pie is
  built immediately after the gauge factory call in the same block.
- `factory(kPieFaceBkg 800, this+208)` → `cpanel\pieFace1.bmp`, then
  `0x48e380(buf,255,0,255)` — the chroma key.
- Two `cTSPieMenu` instances (main + sub-pie, ctor 0x52a4f0, 396 B):
  - font type 1; `field+100 = {0,40,140}` (base)
  - `SetSelectedColor {255,255,255}` (vt+580)
  - `SetHighlightColor {165,195,214}` (vt+576)
  - `SetLowlightColor {0,255,255}` (vt+584)
  - `SetMaxRadius(150)` (vt+488) @0x28f0ac / 0x28f1b4
  - `SetInactiveRadius(35)` (vt+476) @0x28f0d4 / 0x28f1cc
  - `pie+372 = 60; pie+376 = 30` (two literal slots)
  - `SetPopupBackgroundBuffer(this+208, 0)` — the PieFace buffer
- Portrait slots: **8** `cTSWinBtn` (loop bound `cmpwi 8` @0x28f340) at
  `this->240[0..7]`, each `+236/+240 = 1`, mounted by `TrackPerson`.

## Art fact

`cpanel\pieFace1.bmp` (121,460 bytes in UIGraphics.far) is **entirely
magenta**: 201x201 = 40,401 of 40,401 pixels are the (255,0,255) key. It is
a pure chroma-key/mask buffer — the visible disc is engine-drawn. (No
PieFace2 exists.)

## Layout (0x5290c0, partial)

`cTSPieMenu::Layout` distributes items over the **8 compass directions**
(the `cmpwi r4, {1,2,4,8}` direction dispatch at 0x52948c–0x5294d4 picks
one of eight offsets; the angle math scales an index by the r25 float pair
plus a base — consistent with 45-degree steps). `GetCurItemCenterX/Y`
0x529882/0x529892 expose slice centers. The full TSPaint label-frame
placement was not fully resolved this round; the port models the label ring
at the max radius and the portrait ring at 75 (both disclosed).

## Port

- `UI/Panels/UIOriginalPeoplePie.cs` — pops at a global center: r=100 disc
  drawn in the decoded base color with a lowlight rim (PieFace1 is the key
  mask), the r=35 inactive center disc holding the current portrait, 8
  compass slots (12 o'clock, clockwise) of `UIOriginalWebcamButton` at the
  portrait radius, name labels framed on the max-radius ring in the decoded
  palette (selected white, hover highlight, else base-blue frame), slot
  click = `vm.MyUID = avatar` (the TrackPerson law) + close, click-away and
  Escape close.
- `UIOriginalPeopleChrome` — the LiveGadget region (193,0,108,100) now
  carries the gauge hotspot; clicking pops the pie at the gauge's global
  center (247,50 people-local).

## Residuals (disclosed)

- The sub-pie (GetPeopleSubPie — pets/large families) is configured in the
  engine but not ported.
- The exact TSPaint label-frame geometry and the popup window's engine
  frame are modeled (45-degree ring at the decoded radii), not
  instruction-exact.
- The disc fill is a solid base color; the original's shaded disc interior
  was not pixel-decoded.

## Gate

`uipie` pins: the pieFace1 201x201 member + resolvability, the decoded
constants (150/35/201/8/60/30 + the four colors), the 45-degree compass
slot law with tolerance, the live pop (8 slots at the ring positions,
pie-local), the selection law (vm.MyUID write + close), and the close path.
Targeted soak `uipie,corpus`: PASS 6/0. Full default gate: see PARITY.md
R191 row.
