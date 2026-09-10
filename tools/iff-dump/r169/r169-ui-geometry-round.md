# R169 — UI geometry repair round

Status: the exact R169 source was published and packaged successfully. The
post-hit-target packaged UI gate passed 13/13 at 2026-08-29 22:34:54 CDT. A
second exact-source packaged run then passed the complete 107-check default at
2026-08-29 22:43:04 CDT. R169 is the current packaged baseline.

This round responds to the reported desktop failures: the stray UCP arc and
fabricated `1st`, stretched/off-screen dialog borders, misplaced neighborhood
chrome, compressed bottom-bar art, and non-original action-queue geometry. No
proprietary asset is added; the port only mounts the owner's existing data.

## 1. Control-panel plate

`cWinViewControl::BlitPrivateBufferToParent` (`r144/toolbar-disasm-viewcontrol-blit.txt`,
function `0x2b5460`) selects a patch at `0x2b5590-0x2b5614` and adds its
plate-local destination at `0x2b5650-0x2b568c`:

| mode | art | size | exact offset |
|---|---|---:|---:|
| Live | LivePatch | 213x153 | (7,30) |
| Buy | BuyPatch | 156x148 | (64,35) |
| Build | BuildPatch | 107x132 | (113,51) |
| Options | OptionsPatch | 37x100 | (183,83) |
| Camera | CameraPatch | 55x100 | (165,83) |

Every patch therefore ends at `(220,183)`. The LivePatch circular edge is
literal source art; the defect was drawing that 213x153 patch at `(0,0)`.
Camera's offset is decoded evidence only in this round: the port exposes no
camera main-panel mode and therefore mounts/gates the four reachable patches.

The plate now keeps complete original state sheets. Camera/wall/story/speed
buttons are 4x1; Pause is 4x2 (60x30, 15x15 cells), with the second row selected
through `ButtonFrame`. State columns are `0 up`, `1 pressed/selected`, `2 hover`,
`3 disabled`. Loop handlers capture speed `i+1` before binding, fixing the
closure that made all three speed buttons select the same speed on desktop and
touch.

The original has direct Lev1/Lev2 selectors, not a separate ordinal label, so
the fabricated `1st`/shadow/glyph controls are removed. The exact enable law in
`cWinViewControl::Update` is:

```text
Lev2 enabled = (mode == BUILD && Is2ndLevelFloorable()) || HasBeenTo2ndLevel()
```

Evidence is `r144/toolbar-disasm-viewcontrol-update.txt` at
`0x2b3f48-0x2b3fa4`; when false while level 2 is active, the engine forces level
1 at `0x2b3fcc-0x2b3ff4`.

**Disclosed port mapping:** the two original getters read engine-global bytes
(`Is2ndLevelFloorable` at `0x20ce20`, `HasBeenTo2ndLevel` at `0x20ce70`), whose
writers are not port-modeled. Simitone maps floorable to any true tile in
`VMArchitecture.Supported[0]` and tracks the visit bit per lot/UI instance.

## 2. Neighborhood coordinate spaces

The decoded desktop artboard law remains:

```text
offX = max(0, (windowWidth  - 800) / 2)
offY = max(0, (windowHeight - 600) / 2)
```

Background/banner/current number/bottom credits and all 14 toolbar controls
follow that offset. Normal controls are banner children; credits add the banner
origin explicitly. The selection-panel center is therefore
`(offX+400, offY+300)`. At 640x480 it remains `(400,300)` and clips at
the viewport instead of moving to negative coordinates; at 1024x768 it is
`(512,384)`.

R143's prior “credits parked offscreen” interpretation is corrected in
`r143/nbhd-layout-law.md`. The language switch writes credits anchors
`(41,543)` for English/French/German/Spanish (`0x4742f8-0x474314`) and
`(19,524)` for Japanese (`0x4742d4-0x4742f0`). The constructor loads banner
left/top at `0x474424-0x474430`; index 4's special path adds them at
`0x474884-0x47488c` before SetArea at `0x4748b0`. Thus English's final desktop
position is `(41+offX,543+offY)`. The current English port uses that result.
The top Sims logo remains banner-local `(5,0)`: its BSS anchor writer is
`0x474d64` with stores at `0x474e14-0x474e18`, and the banner parent supplies
the final offX/offY.

Correction: the disclosed banner title at `(152,19)` was port-authored and must
not be drawn. Complete's `kMarquee` slot is dead. The original instead formats
the current neighborhood number and draws it directly—without blitting the
blue Current.bmp template—inside the 23x30 rect at `(124,3)`. Centering gives
the engine text origin `(131,6)` for `1`; its bitmapped-font initializer then
normalizes glyph T2 by `-minT2` (`0x4b4554-0x4b47dc`). For
`variablesans_12.ffn`, `minT2=-4`, so the original ink begins at y=11. The
port's raw-FFN `UIOriginalText` receives an isolated +4 anchor correction
(position y=10, ink y=11); the global text renderer is deliberately unchanged.

## 3. Bottom bar, people hit geometry, and survey composition

Desktop `PanelBack.bmp` is a fixed 804x100 buffer beside the 220px UCP
(`220+804=1024`). `UIImage.SetSize` scales pixels, so sizing it to the current
visible width compressed the artwork; desktop now retains 804x100 and lets the
viewport clip narrow windows. Touch retains its established stretch behavior
and refreshes it on resize.

The custom-drawn people buttons inherited `UIButton` from a 1x1 white base
texture. `UIButton.Size` applies only its X component, so the webcam and tab
art looked correct while their mouse rectangles remained only one pixel high.
Each custom button now owns its exact reported size and mouse bounds from the
decoded `SetImage` cells: eight webcams at 45x45, Mood at 32x39, and the other
six tabs at 27x30. This changes interaction geometry only; their custom paint
coordinates and pixels are unchanged.

The webcam binding also no longer closes over the `for` variable (which is 8
after construction). It routes through the clicked button's immutable `Index`,
so each visible family cell selects its own avatar instead of indexing past the
eight-element array.

The UI survey now draws the manager tree once. `CurrentUIScreen` is already a
child of `UILayer.mainUI`; the former explicit second draw darkened
antialiased text and overlays and did not represent a real frame.

## 4. Original action queue

`cDDDSimsView::UpdateActionIcons` begins at `0x2146b0`. Its exact desktop law is:

- 45x45 cells at target top-left `(32 + 50*i, 32)` (`0x214b34`,
  `0x214b5c`/`0x214bc0`, pitch `+50` at `0x214bec`);
- a pie-created icon starts at `(clickX-16, clickY-16)`
  (`0x214a78-0x214a80`);
- reposition duration is 500 ms (`0x214bc4-0x214bc8`, `StartAnimating`
  at `0x205700`);
- `cpanel/Buttons/IconFrame.bmp` is a 90x45 two-cell frame and
  `Other/QueueCancel.bmp` is a 45x45 overlay;
- canonical 45/180/270-wide sheets select column zero, and 135px-high sheets
  select their middle 45px row.

The desktop hit box remains 45x45 in active, queued, and cancelled states.
**Disclosed port mapping:** the original `RampGenerator` curve is not decoded;
the existing `TweenQuad.EaseOut` curve is retained for the exact 500 ms span.

## 5. Dialog and picture-dialog repair

Generic `GenDlg.bmp` remains a 30x30, 10px-cell frame. Its repair is mechanical:
`DrawLocalTexture` consumes scale multipliers, so an edge with destination
length `L` uses `L/10`, not `L`. The old `L` multiplier produced borders ten
times too long and visibly extended them off-screen.

`cWinPictureDialog` is a different paint/layout path. The complete decode is in
`picture-dialog-law.md`; its executable entry points are `TSPaint 0x295050`,
`TryLayout 0x295550`, and `DoLayout 0x295d50`. System slot 5 is resource 3008,
`CPanel/Backgrounds/PopupInfoTiles.bmp` 36x36. Dividing it into 12px thirds
gives client `(12,12)` and content/image `(21,21)`. `TSPaint` calls
`cTSBuffer::BltEdge`: corners stay native 12x12, while edges and the center are
tiled and the final tile is clipped. The picture frame is therefore not the
generic stretched nine-slice and has no separate solid-fill interior.

Images retain their exact crop and render at scale 1. The automatic-object path
composes and displays `(0,0,120,120)` natively at `(21,21)`. An exact 45x45
image instead mounts the 133x103 `SimStub.BMP`, paints it at `(21,21)`, and
centers the native 45x45 crop within it.

The port-owned 120x120 composition target is replaced and disposed when the
icon changes, disposed when the alert is removed, and rendering restores the
caller's prior target in a `finally` block. This is a resource-lifecycle repair,
not evidence that the composed pixels reproduce `Product::DrawIcon`.

Title/body typography is also exact. Title uses regular
`Fonts\variablesans_14.ffn`, line height 27; body uses regular
`Fonts\variablesans_12.ffn`, line height 23. Both inherit font-default
RGB(195,205,205). The runtime heights follow
`max(glyphHeight+t2)-min(0,min(t2))`, not the zero FFN ascent/descent header.
For a titled 120x120 image, title y is 21..48, body y is 66, exclusion width is
138, and exclusion height rounds 93 to 92 (four 23px lines). Native font wrap
uses the narrow exclusion width first, then full body width.

Final outer size is `(bodyWidth+42, verticalCursor+12)`. `DoLayout` starts its
search at 300x185, seeks outer aspect 2.0 within 0.05 by doubling and binary
searching body width (at most 10 midpoint iterations or a width gap <=20), and
keeps the closer candidate. There is no 420px cap, screen-minus-80 clamp, or
200px minimum. Every button retains its label-derived width with an individual
minimum of 100; spacing is `(bodyWidth-sum(width))/(n+1)` inside the body rect.

**Disclosed bridges:** the original centers in the application main-window
rect, which only equals the port's screen bounds when those rects coincide;
RGB inputs may quantize in the original packed pixel format; the port rebuilds
the native byte-string wrapping entry point with exact FFN measurements and
space boundaries; and the 120x120 display crop does not prove that the port
duplicates `Product::DrawIcon` pixels. Any caption-width heuristic used before
the individual 100px button minimum is likewise port-side, not decoded.

## 6. Permanent checks changed

- `uicp`: exact patch offsets, complete state sheets/cells, no ordinal, and the
  Lev2 availability truth table.
- `uibargeom`: natural 804x100 PanelBack, no ordinal, and exact people-control
  `Size` plus `GetBounds()` mouse rectangles (8x45x45, 32x39, 6x27x30), with
  webcam indices 0..7.
- `uinav`: artboard offset/center laws, shifted banner/current/title/credits,
  raw top controls, and small-window clipping origin.
- `uidlgchrome`: generic edge scale; picture tile dimensions and tiled-paint
  law; native automatic-object crop; the separate 45x45 `SimStub` composition;
  font resources/heights/colors; exclusion wrap; adaptive aspect search; two
  differently sized buttons retaining independent widths (each minimum 100)
  and decoded spacing; acceptance of inclusive endpoints 1.95/2.05 and interior
  1.97/2.03, with 1.94/2.06 rejected; plus a survey image.
- `uiqueuegeom` (new default check): art dimensions, positions, pitch, source
  inset, duration, source rectangles, hit box, and cancel/frame overlays.
- `uisurvey`: one real manager-tree draw instead of double composition.

Final exact-source release publish/package completed with 0 build errors; only
the expected package-restore warnings were reported. The packaged focused
invocation of
`lot,corpus,uicp,uibargeom,uiqueuegeom,uinav,uinbhd,uidlgchrome,uisurvey`
passed **13 passed, 0 failed, 0 skipped** at 2026-08-29 22:34:54 CDT. Its
`uibargeom` record includes
`peopleHit=8x45x45+32x39+6x27x30`. Final survey images
for dialog/UCP/live/buy/build were visually inspected: the dialog is centered
and on-screen with its tiled frame, the UCP has no ordinal, and the bottom bars
retain natural art. The run reached clean `after Run` / `after Dispose`, with
zero `SimAnticsExc` or `bad-routine-frame`; its one self-recovering startup
record is the existing `splashprogress mount` null-reference log. Transient
`/tmp/r169-authoritative-focused.log` has 699 lines and SHA-256
`aa9a03104ae13d15ea1d953387ab042932c9da9ca1fe98d246966700a301d4de`.

The exact-source packaged full-default rerun passed **107 passed, 0 failed, 0
skipped** at 2026-08-29 22:43:04 CDT. It retained the same
`peopleHit=8x45x45+32x39+6x27x30` pin, reached clean `after Run` / `after
Dispose`, and contained zero `SimAnticsExc` or `bad-routine-frame` records. Its
only startup exception is the same self-recovering `splashprogress mount`
null-reference record. Transient `/tmp/r169-authoritative-default-rerun.log`
has 1,611 lines and SHA-256
`708e8a9d85985319d5f10f9710db07f11ae14c81f3c4270e9238fb1bddecbc72`.

One prior unchanged packaged attempt stopped making progress in the known
runtime-dialog harness path and was manually aborted. No source/package change
preceded this passing exact rerun, so the aborted attempt is recorded as a
harness wedge rather than a product failure.
