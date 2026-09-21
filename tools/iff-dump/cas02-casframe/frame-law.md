# CAS-02 — the native CAS preview framing law + the single-line name-editor law

Owned PPC executable `The Sims Complete`, SHA256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`.
Addresses are raw file offsets (code virtual + 0x8e90). Reproduce with
`ppc_decode.py <binary> <start> <end>`; raw disasm in this directory
(vitabtn-*.txt, vitasolo-ctor.txt, edit-*.txt, singleton-87eb0.txt,
vitaboy-ctor.txt, cas-tsonkeydown.txt). TOC reads use the relocated data
image from `pef_load.py` (`image[0x8000 + slot]`), validated against the
r240 pair (slot -0x4358 -> code 0x59c3a8).

## 1. Which class the CAS uses

- CAS Init creates `cWinVitaBtn` (r143 §3.2: `operator new(500)` +
  `__ct__11cWinVitaBtnFf` 0x2dc700). NOT cWinVitaBtnSolo (0x2dbd80) —
  the Solo class is a different window (its UpdateTransform keeps the
  matrix at +0x1a4, the base at +0x190).
- Methods (r153 symbol map): UpdateTransform 0x2dbef0, SetPerson 0x2dc1a0,
  TSPaint 0x2dc200, Init 0x2dc590, ctor 0x2dc700.
- TSPaint copies the SAnimator's 16-float root matrix (this+0x190..0x1cc,
  flag +0x1d0) into the VitaBoy object (animator->0xc, offsets +0x18..+0x58)
  every paint, then calls `VitaBoy::Render(VitaBoy*, Viewport3D*, int)`
  0x38a6d0 (symbol-confirmed; second caller = Solo TSPaint 0x2dba58).

## 2. The root transform (cWinVitaBtn::UpdateTransform 0x2dbef0)

- Sway rotation: `angle = pi * gB[0] + sine(t)` with gB = TOC[-0x4ce4] ->
  code 0x59b9a0 (file 0x5a4830): gB[0] = 0.25, gB[1] = 32767.0, gB[2] = 0.5,
  gB[4] = 176.0. So the live facing = **pi/4 + 0.25*sin** (10s sine, r209's
  SineGenerator: SetPeriod 10000 ms). The ctor (0x2dc748-0x2dc764) seeds the
  rest matrix at angle pi * gB[2] = pi/2, replaced on the first paint.
- Translation vector built with the 0x4330/xoris int->double trick:
  - X = `left + (right - left)/2` — **the window's horizontal center**
    (`subf r5, r5, r4` computes right-left; fields this+0x1c/0x24 = the
    cTSWin area l/r, r143's SetArea (618,145)-(718,365)).
  - Y = `bottom - 20` (this+0x28 - 0x14) — **the root sits 20px above the
    window bottom**. (Solo adds the same law; pets shift x -20 and y -50.)
  - Z = gB[1] = 32767 — a depth sentinel, not a world height.
- The vector is transformed by the active 3D scene's matrix
  (singleton 0x87eb0 -> object+0xa4) and composed under the sway rotation
  (Transform library 0x11ac00/0x11a810/0x11a930/0x11ab50).

## 3. Scale

- StartVitaBoy (SAnimator::StartVitaBoy 0x361970) writes the model-scale
  fields 0x1f0..0x204 from the constant table TOC[-0x4bb8] -> code 0x59bbc4
  (file 0x5a4a54): the recurring multiplier is **5.33333** (= 16/3) with a
  **0.5** half-ratio on two axes (the 2:1 iso compression) and a 0.125
  tertiary; per-type factors come from vtbl+0x128(0x13..0x17) — but the
  SAnimator vtable is CFM-glued (r209's disclosed slot-mapping boundary) and
  statically unrecoverable. The table ALSO carries **45.0** (idx 17), the
  same table the 5.33333/0.5/0.125 scale chain comes from.
- The port renders the raw Vitaboy mesh 1:1 in world units (AvatarComponent
  World = translation only; head bone /3 -> tiles confirms ~3.4 WU sims).
- The first implementation chained 5.33333/0.25 = 21.333 px/unit = 64
  px/WU; the live run DISPROVED it (head clipped at row 0, bounds
  8,0-91,207) and measured the mesh: feet row 188 with the look at
  1.40625 WU means the world-up axis projects at **cos 30 = 0.866** through
  the shared camera's RotX(30) — the derivation had omitted that factor.
- Corrected scale: the constant-table **45.0 px/unit** (= the
  uisurvey-calibrated PreciseZoom 1.5, 45.25 px/WU, the previously accepted
  render size). Full idle cycle contained: clipping needs a >= 5.1 WU
  pose reach (impossible for TS1 sims).
- View (WorldCamera.CalculateView): look point (CenterTile.X*3,
  CenterTile.Z*3, CenterTile.Y*3), RotY(rotation) * RotX(30deg) — the 30deg
  pitch matches CAS-01's decoded portrait camera pitch.
- Root anchor law -> look point: the root must land on surface (50, 200);
  90px of screen = 90/(45.25 x 0.866) = 2.297 world units -> CenterTile.Z =
  2.297/3 = **0.7653** (derived; the run-measured 0.46875 put the feet on
  row 188 exactly as this chain predicts, which is the reconciliation
  receipt).

## 4. The framing law (what the port implements)

1. Surface = the cWinVitaBtn rect (618,145)-(718,365), 100x220 at 800x600
   (r143 §3.2, unchanged); the sim is CLIPPED to it (TSPaint
   vtbl+0x48 SetRect + vtbl+0x2c Enable on this->0x5c->0x5c).
2. Skeleton root at surface (50, 200) — window center-x, bottom - 20.
3. Render scale 45.25 px/world unit (Near, PreciseZoom 1.5; the table's
   45.0 within 0.6%).
4. Facing: the model 45deg to the iso camera plus the ±0.25 rad sway
   (r209 canon; the port's reviewed pose path already produces the 45deg
   relative facing — untouched).
5. Resolution independence: every constant is surface-local; the surface
   draws 1:1 inside the 800x600 UI plane, so 800x600 and 1024x768 render
   identically — PROVEN LIVE by uicasflow's
   `preview-projection-independent-of-host-viewport` PASS (both runs).

Port fix: `UIOriginalVitaPreview.CreateNativeCamera`. Run-measured
reconciliation at the first constants (64 px/WU, look 0.46875): feet row
188 = 110 + 1.40625 x 0.866 x 64 (the omitted cos 30), bottom 207 = feet +
the ground shadow ellipse, top 0 = the neutral head clipped — all three
measured pixels explained by the corrected model; at 45.25 px/WU and look
0.7653 the feet land on 200 and the whole idle cycle is contained.

## 5. The single-line name-editor law

- Field: cTSWinTextEdit2 at (275,52)-(530,77), font[10], white, transparent,
  `SetLinesAllowed(1)` (0x2cf4a4; setter 0x52ff10 writes this->0x124),
  `SetCapacity(25)` (16 when the language byte == 15), SetFlashFrameOnEmpty(1),
  SetDrawFrameOnFocus(0), TSSetFocus at Init (r143 §3.3).
- `cTSWinTextEdit2::TSOnCharacter` 0x5336b0 (recovered block
  cas-editor-followup/native-editor.txt "Typed Return", verified in
  edit-tsonkeydown.txt context): CR normalizes to LF; with
  `linesAllowed == 1` (0x124) the LF is **never inserted** — gate 0x50==0
  swallows it, else it sends command (3, 0x17) to the parent (0x51d930).
- `cWinDesignCharacter::TSOnCommand` 0x2cd3b0: command 0x17 ->
  0x2cdff0 = forward to `cTSWinGenDlg::TSOnCommand` — the dialog DEFAULT
  command, i.e. Done.
- Control chars < 0x20 (except the multiline LF) are rejected (0x533748).
- The port defect: the shared InputManager `ApplyKeyboardInput` INSERTS a
  literal '\n' into the buffer on Enter (InputManager.cs line ~188), and the
  NameBox (UITextEdit.MaxLines defaults to int.MaxValue) accepted pasted
  line breaks — the name buffer could carry '\n' into SaveFamily.

Port fix: `UIOriginalPersonNameBox : UITextBox` (r240 filter-box pattern) —
suppresses Keys.Enter before the shared mutation and raises the base
OnEnterPress event (wired to Done, guarded by the disabled state — the
disableAccept law covers the native empty-flash), and strips CR/LF from the
frame text stream. Capacity 25/16(lang 15) per r143. MaxLines=1.
Initial focus + horizontal caret scrolling: already restored
(TS1CASScreen.UpdateDesktopNameFocus, r143 TSSetFocus law; cas240-pinned).
