# R174 — native options and lot-selection parity

## Scope and evidence boundary

R174 answers the reported Options-screen mismatch and the Simitone-looking lot
selection card. The PowerPC executable used for recovery is the owner's local
`game-data/The Sims/The Sims Complete`, SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`.

This round commits source, tests, and evidence notes only. It does not commit
or redistribute the executable, game data, original art, or screenshots.

## 1. Desktop lot clicks do not open the Simitone card

The diagonal half-screen `UIHouseSelectPanel`, duplicated description text,
and large `Enter Lot` / `More` pills are port-authored touch UI. The original
desktop `RegularModeLotHandler` resolves the clicked lot and dispatches the
load directly. R174 therefore bypasses `UIHouseSelectPanel` for regular
desktop browsing while retaining it unchanged for software-keyboard/touch
layouts. Desktop move-in mode also retains the validated Simitone selection
surface for now because its separate native occupied/zoning/budget/MagiCoin
handler has not yet been recovered; bypassing those guards would be unsafe.

Original empty-lot behavior is preserved at the load boundary:

- an unoccupied residential lot in regular neighborhood mode refuses entry;
- unoccupied private Magic Town lots 90–92 refuse entry;
- occupied lots and public expansion lots dispatch normally; and
- the refusal uses UIText.iff STR# 133 `RegularModeStrs` entries 0 and 1 in an
  original-chrome one-button dialog.

The obsolete selected-card zoom/tween is therefore absent on desktop. A lot
click has exactly one action path, preventing the duplicated text and controls
shown in the submitted capture.

## 2. Native lot-hover plaque

The original desktop supplies one shared, zero-delay, noninteractive
`cWinLotPopup`. It is not the old bottom-left cyan caption and it does not use
hotspot photos. R174 restores the recovered composition:

- font: `variablesans_10.ffn`, 19px line height;
- text color: RGB `(195,205,205)`;
- content width: first-line measure clamped to 150–320px;
- outer padding: 25px on every text edge;
- resident portraits: generated 25×25 heads, one or two rows;
- chrome: RT 5107 `Nbhd/HotspotPopupTiler.TGA`, a 111×111 sheet tiled as a
  37px nine-slice through the original `BltEdge` law;
- position: above the full lot-button window, falling below only when top
  space is insufficient, then clamped one pixel inside the screen; and
- transition: the open-house thumbnail and popup share the executable's
  linear 300ms constant-time ramp, including the wrapped text (container
  opacity is not inherited by this UI framework).

Adaptive sizing includes portrait height before the 2:3 aspect test. Each
width retry recomputes portrait columns and rows because widening the plaque
can collapse two portrait rows into one.

Every payload begins with the native street address followed by exactly two
newlines. `StreetNames.iff` STR# 2001 maps each lot to its STR# 2000 format;
the executable's STR# 134 fallbacks cover Sim Lane, Downtown, Vacation,
Studio Town, and Magic Town addresses when that mapping is absent.

Downtown lots 21–30 read `Houses/DTDesc.iff`; Vacation lots 40–48 read
`Houses/VIDesc.iff`. This is intentionally corrected in the Simitone screen
instead of modifying the upstream FreeSO submodule, whose shared provider
historically routed every house below 80 through `NeighborhoodDesc.iff`.

Template selection is exact for the regular browse path:

- occupied generic lot: STR# 134 [0];
- vacant residential lot: [1];
- vacant community lot: [17];
- vacant private Magic lot 90–92: [29];
- occupied private Magic lot 90–92: [34]; and
- occupied Magic community lot 93–99: generic [0], not [34].

The core executable addresses are `cWinLotPopup::Init` `0x2d753c`, layout
`0x2d69ec..0x2d70c8`, tiled edge dispatch `0x2d6d18..0x2d6e34` to
`BltEdge` `0x4852b0`, draw `0x2d6e98..0x2d6ed0`, and placement
`0x504660..0x504820`.

## 3. Original Options band

`cWinOptions` owns the complete 804×100 bottom band at main-panel local
`(0,0)`. Its six primary controls remain mounted while a detail group is
active. Their captions are tooltips only; the visible `Save`, `Neighborhood`,
and `Quit` labels in the submitted capture were synthetic.

| control | anchor | natural frame |
|---|---:|---:|
| Save | (29,6) | 39×39 |
| Neighborhood | (88,6) | 49×37 |
| Quit | (159,6) | 50×40 |
| Graphics | (29,58) | 38×40 |
| Sound | (89,58) | 47×40 |
| Play | (151,58) | 55×40 |

The 201×2 horizontal toothpick paints at `(16,51)` and the 2×89 vertical
toothpick at `(222,6)`. Graphics, Sound, and Play are behavior-2 selectors;
clicking the active selector returns to the six-button state. The separate
green house/category plaque and fake back button are removed.

### Graphics

The four 20×21 check controls are at x=245, y=7/27/47/67. Their labels use
font 9 at x=275, y=9/29/49/69. Terrain and Character labels end at x=480,
with logical 80×20 tri-radio controls at `(485,30)` and `(485,56)`. Each radio
composes three 20px cells at local x=0/30/60, with Low/Med/High text above it
in font 8.

### Sound

F/X, Music, and Vox labels use font 9 at x=305 and y=13/41/69. Their native
logical sliders are 130×14; the track begins 15px after the widest label and
retains the original 28px row pitch.

### Play

Play uses two four-row checkbox columns and one 22×30 Reset Tutorial button
at `(panelWidth-25,7)`. Free Will and Edge Scrolling update their live engine
states; the other recovered switches persist their original choices.

## 4. Options informational popup

`PopupOpt*.bmp` is only the illustration pane. The actual `cWinLivePopup` is
a dynamic 559px-wide window built from SystemBMP(5),
`CPanel/Backgrounds/PopupInfoTiles.bmp`, tiled as a 12px nine-slice.

```text
title rect:  (178,12) .. (545,31), font 10
body rect:   (178,31), width 367, font 9, line pitch 17
height:      max(19 + 17 * wrappedBodyLines, 103) + 24
placement:   (optionsWidth - 559, -height)
```

The natural-size 133×103 illustration is centered in the 168px left pane.
The popup is informational and noninteractive. Selecting the same option
toggles it, selecting another option replaces it, and leaving the Options
hierarchy dismisses it. Relevant executable routines are
`cWinLivePopup::RebuildBuffer` `0x278630`, `DrawPane` `0x279b70`, `Init`
`0x2795f0`, and the Options mouse-exit handlers `0x280600` / `0x2806e0`.

## 5. Zero-size root cause

The live gate found a second defect after the art and string pins had passed:
the custom option controls and both popup containers assigned `Size`, but
`UIElement.Size` intentionally has a no-op setter. They consequently drew
textures while reporting 0×0 geometry; label hit regions and popup placement
could not be correct.

R174 gives original-glyph labels, native checks/radios/sliders, and both popup
containers explicit backing size properties. Glyph drawing remains metric
based, so this repairs layout and interaction geometry without changing the
established text rasterization.

## 6. Permanent checks and validation

`uiopts` pins STR# 145 byte-for-byte, all referenced original art hashes and
dimensions, the six persistent primary controls, every graphics/sound/play
row, live settings callbacks, selector state, label fonts, and the complete
dynamic help-popup geometry. Its render probe traverses the production
root-to-leaf matrix chain and samples the popup through the owning cached
options panel above the 100px band, then verifies that the same pixel becomes
transparent after dismissal.

`uilotq` pins STR# 132/133/134, the 5107 tiler bytes, direct regular-desktop
dispatch, touch and move-in fallbacks, regular and Magic empty-lot policy,
original font/padding, adaptive plaque dimensions, synchronized text/chrome
ramp opacity, and above-lot placement. The plaque must emit pixels at opacity
one and none at zero; placement checks both sides of the centered artboard's
screen-space top boundary.

The final Simitone.Client Release build completed with 0 errors and 75
existing dependency warnings. The final self-contained `osx-arm64` publish
and packaged app client are byte-identical:

```text
b54e5cd583cda7bf4943b3e0bc7e12e0d1c4868fc07f1544918a6de3364ba166
```

The final packaged focused run passed **8 passed, 0 failed, 0 skipped**, with
clean `after Run` / `after Dispose` and wrapper exit 0 at 2026-08-30
04:30:36 CDT. Transient `/tmp/r174-focused-owner2.log` has 255 lines and
SHA-256
`09a424bf352f76ed8a3cd73dc86d0f1e54f3c1c8da08c0b28fd0ed50df3c059a`.

The final packaged full-default run passed **107 passed, 0 failed, 0 skipped**,
with clean `after Run` / `after Dispose` and wrapper exit 0 at 2026-08-30
04:34:54 CDT. Transient `/tmp/r174-full-final4.log` has 1,645 lines and SHA-256
`02c837257efea7d29b49c44e17ce1d6498803f71fbe94d6af221e2dc78ef6cc6`.
It contains zero `SimAnticsExc` and zero `bad-routine-frame` records.

## 7. Honest residuals

- Reset Tutorial mounts the native control and help content, but the port has
  no tutorial-reset backend state to invoke.
- Several recovered Play/Graphics switches persist choices before every
  corresponding renderer/backend feature exists; Free Will, Edge Scrolling,
  volume, and anti-aliasing have live effects.
- Desktop English at 1024×768 is the validated target. The touch card is
  deliberately retained as a separate port UI and is not claimed to match
  the original desktop game.
- Desktop move-in mode still uses that validated card until the original
  move-in handler's eligibility and payment branches are recovered; regular
  desktop lot browsing no longer does.
- The known self-recovering startup `splashprogress mount` null-reference log
  remains outside this UI scope.
