# R173 — neighborhood and camera-control parity

## Scope and evidence boundary

R173 answers the reported neighborhood-screen composition mismatch and the
apparently low Options button. The PowerPC executable used for recovery is the
owner's local `game-data/The Sims/The Sims Complete`, SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`.

This round commits source, tests, and evidence notes only. It does not commit
or redistribute the executable, game data, original art, or screenshots.

## 1. The Options button was already at the original anchor

`cWinViewControl::Init` places the 92x23 four-state `options.bmp` at UCP-local
`(193,125)`. The apparent empty bubble above it was not spacing: the port had
omitted the separate 116x29 four-state `camera.bmp` at `(175,95)`. Camera ends
at y=124 and Options starts at y=125, preserving the original one-pixel seam.

R173 mounts `CameraPatch.bmp` (55x100 at `(165,83)`), restores the Camera
button, routes F4 to Camera and F5 to Options, and gives Camera the original
disabled/selected behavior.

The complete `cWinCamPanel` 804x100 band is restored from UIText.iff STR# 140
and `cWinCamPanel::TSPaint` (`0x2667a0`):

| control | full sheet | panel-local anchor | tooltip |
|---|---:|---:|---:|
| Small frame | 128x27 | (82,70) | 140:1 |
| Medium frame | 128x29 | (82,36) | 140:3 |
| Large frame | 128x28 | (82,4) | 140:5 |
| Custom frame | 240x58 | (11,11) | 140:7 |
| View album | 244x35 | (226,35) | 140:9 |
| Low quality | 128x28 | (154,70) | 140:11 |
| Medium quality | 128x29 | (154,36) | 140:13 |
| High quality | 128x28 | (154,4) | 140:15 |

All controls are 4x1 state sheets. The 2x89 separators paint at `(133,6)` and
`(206,6)`, and CPState defaults both selections to Medium.

## 2. Neighborhood coordinate space and banner contents

The original artboard offset is:

```text
offX = max(0, (windowWidth  - 800) / 2)
offY = max(0, (windowHeight - 600) / 2)
```

The prior port interpretation left the top controls in raw window coordinates.
The constructor actually creates them at banner-local anchors and calls
`banner->AddChild(button)` (`0x474614..0x474624`); `CalcAbsoluteArea` then adds
the centered banner origin. Credits are the special root child and add the same
origin explicitly. At 1024x768, map, banner, logo, controls, number, and credits
therefore all receive `(112,84)` exactly once.

The synthetic `Old Town`/other screen title is removed. Complete's `kMarquee`
slot is dead. `Current.bmp` is also not painted: the original loads its 23x30
dimensions, formats the neighborhood number, and emits white variablesans_12
text from `PostChildDraw` after the Prev/Next children. The number rect is
banner-local `(124,3)` and the centered text origin is:

```text
x = 124 + floor(23/2) - 1 - floor(textWidth/2)
y =   3 + floor(30/2) - 1 - floor(lineHeight/2) + 4
```

The final `+4` is not an adjustment by eye. `InitBitmapped`
(`0x4b4554..0x4b47dc`) subtracts the table's minimum T2 from every cached glyph;
variablesans_12 has minT2=-4, while the port's `UIOriginalText` intentionally
draws raw FFN T2. The correction is isolated to this number rather than
changing every original-text surface. The port currently mounts the owned
Neighborhood-1 save, so the displayed value is `1`.

Resize calculations now use the live `UIScreen` dimensions instead of stale
saved settings.

## 3. Neighborhood child paint order and clipping

Every `cWinLotBtn::ImageBlt` receives the neighborhood bounds at `this+0x220`.
The port now crops each thumbnail/composite source against local `(0,0,800,600)`
before drawing, preventing house art from escaping into the blue surround.
Lots retain neighborhood STR/vector source order; the port-authored Y sort is
removed.

Paint phases are screen-specific:

- generic animated water/delta layers paint before lot-button children;
- Vacation paints only its five water frames below lots, then
  `PostChildDraw__12cWinVacation` paints `visland_port.bmp` and
  `visland_trees.bmp` after lots in that order (`0x43e134..0x43e21c`);
- Studio Town loads background resource 5321 and its car system, but the loose
  `DScreen_top_layer.bmp` is absent from Res_Nbhd.RT and has no original load or
  paint path, so the synthetic overlay is removed.

One shared mount routine defines both the live hierarchy and the structural
gate: under-lot layers, source-order lots, then post-lot overlays.

## 4. Old Town water cadence

`cWinNeighborhoodUL::TSPaint` uses `timeGetTime`, advances when
`1000 / integerElapsed <= 2*pi` (float `6.2831855`), increments a mod-12
counter, and selects `floor(counter/2)` from six wave buffers. The earliest
integer event is 160 ms; each visible frame is held for two events, at least
320 ms. SetImage replaces the current frame: there is no next-frame alpha
crossfade. A long paint stall advances once and resets the timestamp rather
than catching up.

Only the Old Town wave family receives this discrete policy. Downtown,
Vacation, Magic Town, and the other established animation state machines keep
their existing per-family behavior.

## 5. Permanent checks

- `uinav` pins the shared `(offX,offY)` law for every top control, the dead
  marquee, unpainted Current template, exact number centering/normalization,
  PostChildDraw ordering, live resize dimensions, and representative lot-source
  clipping cases.
- `uinbhd` pins the live mount trace
  `[under Vacation water, lot, post port, post trees]`, the Vacation 5/2 split,
  zero Studio image overlays, Old Town's 160 ms/two-count/mod-12/no-catch-up
  law, and a non-Old-Town interpolation negative control.
- `uicp` pins five mode patches and buttons, Camera/Options anchors and F-key
  routing, all eight camera controls, their STR# 140 tooltips, separators,
  Medium defaults, and mutually exclusive selection.
- `uisurvey` adds `uisurvey-camera.png` and retains packaged neighborhood/UCP
  captures. Survey files remain local under `~/Documents/Simitone/`.

## 6. Exact-source validation

The final Release build completed with 0 errors and 78 existing warnings.
The self-contained `osx-arm64` publish and packaged app client are
byte-identical:

```text
0382c9fd6d7e836198391843c2263401e5c147e4d55a74809dc90e4043050b5d
```

The final packaged focused run passed **16 passed, 0 failed, 0 skipped** with
`layerPhases=True`, `ulCadence=True`, `lotClip=True`, clean `after Run` /
`after Dispose`, and wrapper exit 0 at 2026-08-30 02:33:25 CDT. Transient
`/tmp/r173-focused-final2.log` has 744 lines and SHA-256
`cdad8cbc7481c2ea0a0d4bc461cb314c363544feb5183bdf0f532447a07fd9d3`.

The final packaged full-default run passed **107 passed, 0 failed, 0 skipped**,
including the mood, career-car, neighborhood, and camera checks, with clean
`after Run` / `after Dispose` and wrapper exit 0 at
2026-08-30 02:37:40 CDT. Transient `/tmp/r173-full-final2.log` has 1,642 lines
and SHA-256
`07fd2b8b6e80d2f1a4d749cfe7a6f58919e7f6466b0645050c420eb02c2b0fe8`.
It contains zero `SimAnticsExc` and zero `bad-routine-frame` records. Its only
startup exception is the known self-recovering `splashprogress mount` null
reference.

## 7. Honest residuals

- The restored camera panel exposes size, quality, and album events, but the
  custom capture-frame interaction and scrapbook/album modal backend are not
  yet implemented. This round restores their original visible controls and
  state law; it does not claim those backend workflows.
- Visual survey covers the packaged 1024x768 English desktop path. The geometry
  code retains the established touch branch, but touch and every locale/window
  size were not visually sampled in this round.
- The known self-recovering startup `splashprogress mount` null-reference log
  remains outside this UI scope.
