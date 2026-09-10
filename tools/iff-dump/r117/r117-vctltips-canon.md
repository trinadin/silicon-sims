# R117 — UCP tooltips from the original UIText.iff 138 'VCtlTips' (+ corpus map for 240/241/145/150)

## What the port had

A 2-session Hermes survey (tooltip inventory + corpus dump) proved the UCP was the ONLY
un-localized tooltip surface in the client: `UIDesktopUCP.cs` assigned 16 literals — 15
button tooltips + the 4-element speed array — all hardcoded paraphrases, none via
`GameFacade.Strings`. Every other tooltip in `Client/Simitone` is either dynamic
(interaction-queue names, avatar `ToString()`), already canon-backed (`GetString("137")`
placement errors, `GetString("159")` error tips), or dead code.

## Canon (disk-pinned in the gate, byte-verbatim)

UIText.iff STR# 138 'VCtlTips' — chunkSize 28762, format -3, 432 raw entries = 24 English
x languages; **full-chunk sha256 d3dc987e72222692218cec96a497b53541581f7a2bf40cfbceca13fdbc
95ee7a** (body-only 40a5193b…, trailer 0xA3 x count = FreeSo STR writer quirk). All 24
English entries pinned verbatim — see `r117-vctltips-dump.txt`.

## The 19 swaps (UIDesktopUCP ctor now reads GetString("138", …) for every one)

| Port control (old literal) | Canon idx | Canon value |
|---|---|---|
| LiveButton ("Live Mode") | 10 | `Live Mode - F1` |
| BuyButton ("Buy Mode") | 11 | `Buy Mode - F2` |
| BuildButton ("Build Mode") | 12 | `Build Mode - F3` |
| OptionsButton ("Options") | 13 | `Options Mode - F5` |
| FloorUpButton ("Floor Up") | 1 | `Second Story` |
| FloorDownButton ("Floor Down") | 0 | `First Story` |
| RoofButton ("Roof") | 2 | `Roof` |
| WallsUpButton ("Walls Up") | 7 | `Walls Up` |
| WallsCutButton ("Walls Cutaway") | 8 | `Walls Cutaway` |
| WallsDownButton ("Walls Down") | 9 | `Walls Down` |
| ZoomInButton ("Zoom In") | 5 | `Zoom In  +` (double space in canon) |
| ZoomOutButton ("Zoom Out") | 6 | `Zoom Out  -` (double space in canon) |
| RotateCWButton ("Rotate Clockwise") | 4 | `Rotate Right` |
| RotateCCWButton ("Rotate Counter-Clockwise") | 3 | `Rotate Left` |
| SpeedButtons[0] ("Normal Speed") | 16 | `Normal Speed - 1` |
| SpeedButtons[1] ("Fast") | 17 | `High Speed - 2` |
| SpeedButtons[2] ("Ultra Fast") | 18 | `Ultra Speed - 3` |
| SpeedButtons[3] ("Pause") | 15 | `Pause - P` |

Interpretations disclosed (ours, not canon): (a) floor arrows — canon carries the story
NAMES, not directions; up-arrow→'Second Story', down-arrow→'First Story' is our pairing;
(b) rotate — canon says Left/Right; CW→Right, CCW→Left is our pairing. Everything else is
verbatim, including the keyboard hints the port dropped and the double spaces.

## Stayed port-authored (disclosed in-source)

- `EyedropperButton` "Eyedropper Tool (E)" — NO original exists (zero corpus hits; the
  eyedropper stencil button is a port addition).
- The tooltip RENDERER: `UILayer.DrawTooltip` lives in the FSO.UI submodule and draws with
  the modern font — `OriginalGlyphFont` cannot live there (FSO.UI cannot reference
  Simitone.Client). Values are canon; drawing is ours — the same disclosure class as
  engine-drawn fonts/placements.

## Mapped-UNUSED residuals (canon exists, port has no consumer yet)

- 138[14] 'Funds' / [19] 'Camera Mode - F4' / [20] 'Family Friend Count' / [21]-[22] 'AM'
  'PM' (the clock!) / [23] 'Help' — the port has no tooltips/labels wired to these.
- 240 'Relationship Panel Sort Button Tooltip Text' (Family Only/Friends Only/Famous
  Only/All) and 241 'Inventory Panel Sort Button Tooltip Text' (Other Items/Magic
  Items/Ingredients) — the port's relationship/inventory subpanels assign NO tooltips at
  all (grep zero hits), so there is nothing to swap; wiring them would be new UI.
- 145 'optionstrs' (84 English entries — the whole options tree: Graphics/Sound/Play
  Options, Anti-alias, Shadows, Lighting, Terrain/Character Detail, F/X, volumes,
  Auto-Centering, **'Free Will' [59]**, Edge Scrolling, Sim In Background, …) — the port
  reads only [1]/[3]/[5] (Neighborhood/Save/Quit). **R118 candidate: the options panel.**
- 150 'BuyModeCatalogSortTips' (rooms/functions/expansion sub-sorts) — buy catalog sort UI.

## Gate ('uidtips', 65→66 checks)

`CheckUITooltips()`: disk-reads UIText.iff, walks STR# 138 (label 'VCtlTips', raw count
432, full-chunk sha256, ALL 24 English entries verbatim — fail-closed on locale drift),
then CONSTRUCTS the real `UIDesktopUCP` through its own ctor (uijob's money-panel
precedent; dispatched post-`CheckUIDump`) and requires all 19 tooltips == the disk-parsed
canon, plus the eyedropper residual == its disclosed port-authored literal.

## Honest FAIL log

- r117p1 FAIL (gate-side): pinned the count as the English count (24) — `ParseLang1`
  yields the RAW declared total (432) in `totalEntries` (uicas pins the same way: 400 for
  chunk 128); live tooltips were already correct in this run (wired=True).
- r117p2 FAIL (gate-side): the sha constant was the survey's BODY-only hash;
  `FindStrChunkByID` returns the full chunk (76-byte header included) and the gate hashes
  that. Recomputed full-chunk sha per the gate's own bytes.
- r117p3: **66/66 PASS**, full soak, clean exit (`canon=True wired=True
  eyedropperPortAuthored=True`).
