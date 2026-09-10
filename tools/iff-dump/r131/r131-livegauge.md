# R131 — the ORIGINAL live-tab mood gauge + the roof panel's PAGES

Round 131 (broadened scope per user direction: three fronts, Hermes x2 concurrent).
Engine binary: `game-data/The Sims/The Sims Complete` (6,486,820 bytes), decoded with
the R129-corrected `tools/iff-dump/ppc_decode.py` (canonical branch encodings:
beq=0x4182 / bne=0x4082 / ble=0x4080 / bge=0x4081 — the R129 validated rule).

## 1. cWinPeople cluster map (engine)

Symbol scan (`scan_r131_engine.py`, `r131-engine-scan.txt`) — symbols at each
function's END, bodies precede them:

| symbol (END) | body |
|---|---|
| 0x286fe0 `.BuildGiftPanel` | ... |
| 0x28b2dc `.UpdateViewFromCPState` | ~0x28ab20-0x28b2dc |
| 0x28c068 `.TrackPerson` | |
| 0x28d234 `.TSOnCommand` | |
| 0x28daac `.TSPaint` | ~0x28d510-0x28daac |
| 0x28de44 `.Plot` | 0x28dca0-0x28de44 |
| 0x291828 `.Init` | ~0x28ee3c-0x291828 |
| 0x292454 `.__ct` | |

Plus subpanels: cWinSubpanelHouse / Job / ReportCard / CatSkills / DogSkills / Fame.

## 2. The gauge (kLiveModeGauge = 4911)

`cWinPeople::Init` gadget inventory (factory calls to 0x3b6190; slot = the
member the factory writes through):

| engine id | RT name | art | slot |
|---|---|---|---|
| 4911 | kLiveModeGauge | CPanel/Backgrounds/LiveGadget.BMP (108x100) | this+548 |
| 800 | kPieFaceBkg | | this+208 (reused) |
| 4506 | kGreenbars | CPanel/Greenbars.bmp (27x25, 24bpp!) | this+372 |
| 4510 | kRedBars | CPanel/Redbars.bmp (27x25) | this+368 |
| 100/101 | kCatalogPrevPage/NextPage | CPanel/Buttons/ScrollLeft/Right.bmp (36x49) | (page buttons — same ids as the roof panel) |
| 4601 | kTrackingTarget | CPanel/People/TrackingTarget.bmp (45x45) | this+576 |

`Plot` (0x28dca0) is a switch on the panel index (this+244, guard `> 7` skip),
each case calling the update virtual (vtable+372) on per-page member gadgets.

### TSPaint composition (0x28d5fc-0x28da68)

1. Backdrop blit: reads the gauge (this+548) rect (+20/+24/+28/+32), builds
   dest/clip rects, blits through the panel canvas (this+92, vtable+160).
2. Animated value: float chain — `lfs f1, [result+12]` from a call, limits from
   the TOC global [TOC−20376] (floats +20/+24), fcmpo, `base ± value`,
   fctiwz → int at sp+244. THREE fctiwz+stfd sequences = three animated values.
   Constant 0x43000000 (128.0) is the fctiwz→int conversion idiom.
3. Fill strips: `rating = min(value,5)`; if value > 0 → strip of this+368
   (kRedBars), if value < 0 → strip of this+372 (kGreenbars), height
   `rating*5` px (0..25 = the FULL 27x25 bar art height), src = the TOP rows
   of the bar gadget's rect, dest offset by [TOC−20400] {dx,dy} + window
   origin. Two trailing 2px-sliver blits draw the bar edges.
4. The sign mapping CLOSES only sign-flipped: the engine's rating is
   `base − value` — positive mood → negative rating → GREEN bar; negative
   mood → RED bar. With the full art height at |mood| = 100, the scale is
   height = |mood|/4 px (rating = |mood|/20, ×5).

**Port mapping (disclosed, engine-derived):** mood short [−100,100] →
`UIOriginalLiveGauge.FillHeight = min(|mood|,100)*25/100`, polarity
green ≥ 0 / red < 0, eased display value (engine easing constants live in
TOC globals — unrecoverable statically; the port eases at 0.15/frame).
The backdrop's narrow segmented strip + right wide channel are part of the
art; the SECOND channel's data source was not decoded (residual). The gauge
sits LEFT of the needs (LivePatch.bmp 213x153 covers that column in the
original panel); port places it at (10,36) of UIMotiveSubpanel with the
'SMood Rating' caption (STR# 154 [15]) above it — port-chosen positions,
composition engine-faithful.

### Strings (STR# 154 'MiscStrings', full-chunk sha d2eaec7f…600a)

Ratings family: [12] 'House Rating', [13] 'Friend Rating', [14] 'Job Rating',
[15] 'Mood Rating'. Also [0]/[1] Previous/Next Page (the generic pager
captions). The gauge caption pin = [15].

### Art pins (r131-live-canon.txt, regen_gate_shas.py → R131LiveArt, 9 rows)

livegadget 108x100 RLE8 · greenbars/redbars 27x25 24bpp plain ·
trackingtarget 45x45 (a crosshair reticle — the People-panel follow-Sim
button, corpus) · scrollleft/scrollright 36x49 · lev1 84x11 / lev2 88x15 /
levroof 88x19 (view-level toolbar corpus, surfaceless).

**New missing-from-disk member:** `kLiveBack` → CPanel/Backgrounds/LiveBack.bmp
(referenced by Res_CPanel.RT, absent from UIGraphics.far — same family as
BuyBack/OptionsBack/BuildBack). Disclosed residual.

## 3. The roof panel's PAGES (front D)

R130 left the STR# 147 page titles [14]/[16] surfaceless. R130's own engine
decode already showed cWinRoofPanel pages (command ids 3/4 →
SetCurPage(page∓1), buttons kCatalogPrevPage 100 / kCatalogNextPage 101).
R131 ports the paging: page 0 'Roof Pitch' [14] = the 4 pitch sub-tools,
page 1 'Roof Patterns' [16] = the swatches; ScrollLeft/Right art buttons
clamped + disabled at the bounds. FullCategory keeps the R130 flat order
(display names, search); FilterCategory picks the page. Port-chosen: the
title/button strip sits at y=106 of the catalog subpanel (the caption row —
the engine panel's own window coords (74;3)/(62;20)/(460;20) belong to its
cWinRoofPanel surface the port's catalog replaces); buttons scaled 0.42 to
fit the 22px strip (36x49 natural).

## 4. Corrections/residuals banked this round

- kLevelRoof (2025) is NOT a roof-panel plaque: neighbors kLevel1=2023 /
  kLevel2=2024 — the build-mode VIEW-LEVEL toolbar (Lev1/Lev2/LevRoof.bmp).
  Art pinned as corpus; the level-view system itself is a camera/rendering
  feature (future round).
- Greenbars/Redbars are 24-bpp plain BMPs (not 8-bit) — R86-era mounts were
  dimension-correct; bpp now pinned by sha.
- The gauge's second channel + kTrackingTarget surface, the exact easing
  constants, [TOC−20400] offsets, and LiveBack.bmp remain residuals.
- cWinPeople also owns ids 800 (kPieFaceBkg), 132, 3750 (kSocialPopupIcon x2)
  — contexts not decoded this round.

## 5. Gate

- NEW check `uigauge` (76 total): STR# 154 canon (full-chunk sha + ratings
  [12..15]), 9 art pins, live control — art at engine dims, caption pinned to
  'Mood Rating', mapping verified through StepEase convergence (100→25px green,
  −60→15px red, −400→25px cap) and polarity via CurrentBarTex.
- `uiroof` EXTENDED (not weakened): the live section now also asserts the
  pager — page 0 = 4 sub-tools + title [14] + prev disabled; page 1 =
  patterns + title [16] + next disabled; clamping at both bounds.
- `r131p1.log`: 76/76 PASS first run, AUTOTEST_WRAPPER_EXIT=0.
