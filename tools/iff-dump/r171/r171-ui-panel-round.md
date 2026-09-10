# R171 — remaining primary UI panel repair

## Scope and evidence boundary

R171 is the user-authorized desktop UI follow-up for the visible defects in the
submitted captures:

- a LIVE category panel remained visible behind its replacement;
- Needs/Job content was duplicated or positioned as if several panels were
  drawing at once;
- empty family slots displayed repeated and inverted portrait cells;
- the `Go Here` pie caption crossed its button caps;
- action-queue cells could appear as solid blue squares;
- the Job panel overflowed the 100px band and repeated rating content; and
- terrain, pool/water, wall, and roof-pitch controls drew complete state sheets
  as tiny repeated pictures.

The user separately called out the neighborhood surround and the curved seam
between the UCP plate and the main panel. Both were inspected against the
original assets and the already recovered coordinate law. They are authentic
in the supplied captures, so this round deliberately does not alter them.

The executable used for original-engine recovery is the owner's local PowerPC
PEF binary:

- `game-data/The Sims/The Sims Complete`
- SHA-256 `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`

Original art and game data are read only from the owner's local corpus. This
round commits source, regression coverage, and this evidence narrative. It
does not commit screenshots, original art, executable bytes, or any other
proprietary payload.

## 1. Desktop child-window replacement is atomic

### Cause

`UISubpanel.Kill()` leaves the retiring child mounted during a 300ms opacity
tween. The desktop owner used that mobile transition for both mode changes and
ordinary category changes. A fast Mood → Needs/Job switch could therefore
paint the new child over one or more retiring children; a subsequent BUILD
selection could expose their green/red gauges and icons behind the tool band.

The original desktop `cWinCPanel`/`cWinPeople` composition switches mutually
exclusive child windows. It hides the outgoing child before the replacement is
shown; it does not cross-fade two complete panels.

### Repair

`UIMainPanel.SetSubpanel` now applies the desktop law to every mounted
`UISubpanel`, including an older child already waiting for delayed removal:

1. hide every retiring desktop child synchronously;
2. retain `Kill()` for normal cleanup and removal;
3. mount the incoming child at the decoded LIVE or BUILD/BUY host origin;
4. set its opacity to 1 immediately; and
5. add a later 1→1 hold so the constructor's queued 0→1 tween cannot make the
   first desktop frame translucent.

Touch layouts retain their authored fades. The existing mode-boundary chrome
exclusion and `Visible` guards remain in force.

Permanent `uilive` and `uibuild` coverage drives the real panel owner and
requires:

- the outgoing desktop child to be non-drawable immediately;
- a render-target probe to find zero outgoing pixels, not merely a false
  `Visible` flag;
- the incoming child to remain at opacity 1 after both queued tween writes;
- exactly one visible desktop subpanel; and
- no People/Arch post-blit pixels while those chrome owners are hidden.

The focused package recorded:

```text
desktopAtomic=True old=UIMotiveSubpanel/px0
new=UIJobSubpanel/a1 visible=1
```

## 2. Family cells, pie captions, and queue icons

### Empty family slots

`UnknownFace.bmp` is a 180x135 multi-row portrait/state sheet. The prior code
cropped its first 45x45 cell as an empty-slot face; the sheet also contains an
inverted row, explaining the repeated upside-down pet-like heads in the
submitted family selector.

The original `PeopleTemplate.bmp` is already the authored 45x45 webcam frame.
An unoccupied slot now keeps its blank normal template cell. Only an occupied
slot composites its live avatar portrait. Draw and the regression probe share
one `ResolveBaseTexture` production path, so `uibargeom` proves the empty slot
selects the same 45x45 normal template used by rendering.

The existing portrait compositor remains a disclosed interpretation: the
original custom webcam draw internals have not been fully disassembled.

### `Go Here` pie caption

The original bitmap font can extend the final glyph beyond its measured
advance. A four-pixel total-side assumption let `Go Here` touch or cross the
right cap. All original pie buttons—including normal actions, overflow, and
Back—now use an eight-pixel margin per side. `uidlgchrome` constructs the real
button path, measures the shipped font, and requires the resulting width and
hit region to contain the complete advance plus the 16px safety total.

This is a protected containment correction, not a claim that an unrecovered
engine constant equals eight.

### Action queue

`IconFrame.bmp` is an opaque button face, not a transparent rim. The port drew
the action bitmap first and then painted that face over it, producing a solid
blue square. The original composition order is now frame → action icon →
cancel overlay. `uiqueuegeom` renders a sentinel icon through the production
control and pins the surviving icon pixel as well as the existing 45x45 cell
geometry.

## 3. Job panel: decoded 427x100 desktop composition

The previous desktop panel reused the wide mobile layout. Its labels were
spread across a 504px host, modern and original-font copies could both draw,
and a second Job Rating block overlapped the six skill columns. Several art
members were also treated as single pictures even though they are state
sheets.

The original `cWinSubpanelJob` initializer yields the following desktop law:

| part | recovered geometry |
|---|---|
| source child window | 427x100 |
| title | `(35,2)` |
| skill labels | `(93, 6 + 15*i)`, six rows |
| skill pips | `(157, 7 + 15*i)`, six rows |
| summary icon column | `x=3` |
| summary rows | `y=4,45,65,85` |
| career sheet | 112x484 = four 28px states × twenty-two 22px rows |
| salary plaque | 52x11 = four 13x11 states |
| performance plaque | 52x12 = four 13x12 states |
| friend plaque | 40x11 = four 10x11 states |

The port now instantiates that compact desktop branch. It crops one career row
while preserving the four button states, crops one state from each summary
plaque, restores the fourth-row friend count, keeps the six skills in 15px
rows, bounds job/salary strings, and suppresses the duplicate modern rating
block. Mobile behavior is unchanged.

`uijob` pins the complete live result: a 504x100 host containing the recovered
427px child law, six pip rows, exactly one visible performance bar, zero visible
modern duplicate labels, correct art parts, and live values. The focused gate
recorded:

```text
desktopGeometry=True host=504x100 pips=6 bars=1 modernLabels=0
parts=True/True/True/True/True valuesWired=True
```

The child Report Card branch remains a separately disclosed residual. Its
desktop placement and original grade-letter lookup were not shown in this
round's captures and are not claimed exact here.

## 4. Build subtools: crop one authored state

The ordinary product catalog and the build subtools use different sheet laws:

- ordinary object and wall-style product art: two horizontal states;
- terrain buttons: four horizontal states;
- roof-pitch buttons: four horizontal states;
- pool and water buttons: four horizontal states.

Treating the latter three families as one image and fitting the complete strip
into 45x45 produced the rows of tiny repeated symbols in the submitted terrain,
water, and wall captures.

`UICatalogItem.IconStateSource` is now the shared equal-cell crop law. Both
catalog-cell implementations select normal/selected/hover state before fitting
the single cell. Ordinary object sheets retain the R170 two-state 37x37 law.
Wall-style 74x37 sheets now explicitly take one 37x37 half rather than drawing
both halves.

Pool and water are represented by floor sentinel IDs for execution, but their
UI is not an isometric floor thumbnail. The new
`UIOriginalPoolWaterResProvider` preserves `UIFloorPainter` and
`WorldFloors` data while resolving the original dedicated UI members:

| sentinel | cell art | cell law | popup art |
|---|---|---|---|
| pool 65535 | `cpanel\Build\PoolToolIcon.bmp` | 180x45 → one 45x45 state | `PopupPool.BMP`, 145x103 |
| water 65534 | `cpanel\HDBuild\WaterToolIcon.bmp` | 180x45 → one 45x45 state | `PopupWaterTool.bmp`, 145x103 |

The popup fallback also carries the cell's paired-state metadata, preventing a
wall fallback from reintroducing the side-by-side image.

`uibuild` pins the pure crop rectangles and the live controls:

- terrain 144x33 → 36x33 states;
- level/related build art 156x33 → 39x33 states;
- pool/water 180x45 → 45x45 states;
- wall 74x37 → 37x37 states;
- exactly two pool/water sentinel products; and
- original icon/popup dimensions through the real category construction.

## 5. Reported surfaces inspected and intentionally preserved

### Neighborhood screen

The submitted main-menu capture matches the established original composition:
an unscaled 800x600 neighborhood artboard at `(112,84)` inside a 1024x768
`LargeBack` surround. The Cocoa title bar accounts for the screenshot-space
offset. The blue surround is shipped original art recovered in R170, not a
stretchable map border. No neighborhood geometry or art was changed in R171.

### UCP-to-panel transition

The curved/white-blue transition is the original `BackPatch.bmp`:

- natural size 52x100;
- main-panel-local position `(-52,0)`;
- screen endpoints x=168..220 at 1024x768;
- drawn after the 804x100 `PanelBack`; and
- local-asset SHA-256
  `3190ca09dec7fc046832547570aa13d7e83a29b684b711978039b790d3a71c0c`.

`uibargeom` now pins its size, local/screen positions, and draw order. Replacing
or flattening it would reduce fidelity, so no product change was made.

## 6. Survey and regression expansion

The packaged `uisurvey` run now emits distinct 1024x768 local artifacts for:

- `uisurvey-live.png`;
- `uisurvey-job.png`;
- `uisurvey-build-terrain.png`;
- `uisurvey-build-water.png`;
- `uisurvey-build-wall.png`;
- `uisurvey-roof.png`;
- `uisurvey-neighborhood.png`; and
- `uisurvey-ucp.png`.

These remain local under `~/Documents/Simitone/` and are not committed. Eyes-on
inspection found blank empty family slots, one active LIVE panel, the compact
Job layout, separate full-size terrain/pool/water/wall cells, clean roof
controls, and the original neighborhood/UCP composition.

The permanent focused checks exercised in the exact package were:

```text
lot,corpus,uinav,uibandlaw,uibuild,uibargeom,uisurvey,
uilive,uijob,uidlgchrome,uiqueuegeom,uicp
```

## 7. Exact-source validation

The integrated Release client build completed with **0 errors** and 78 existing
warnings. Exact current source was published self-contained for `osx-arm64`,
then packaged with `packmac.sh arm64` as `dist/The Sims-arm64.app`.

The publish and packaged `Simitone.Client.dll` are byte-identical:

```text
0f12cc7444d74ed56c86e779d3f8b49feb7601cd57dd9e821beb682e7fc48faf
```

Focused command:

```sh
NSUnbufferedIO=YES 'dist/The Sims-arm64.app/Contents/MacOS/TheSims' \
  -ApplePersistenceIgnoreState YES \
  "-path$PWD/game-data/The Sims" -autotest 5 \
  -autotest-opts lot,corpus,uinav,uibandlaw,uibuild,uibargeom,uisurvey,uilive,uijob,uidlgchrome,uiqueuegeom,uicp \
  -autotest-timeout 600000
```

Final focused result: **16 passed, 0 failed, 0 skipped**, wrapper exit 0, clean
`after Run` and `after Dispose` at 2026-08-30 00:57:25 CDT. Transient
`/tmp/r171-focused-final.log` has 730 lines and SHA-256
`ea408b1f761249af9b3276039490f7ca52cbac0842f3cb2cfaa688b5409944ab`.

Final full-default result: **107 passed, 0 failed, 0 skipped**, wrapper exit 0,
clean `after Run` and `after Dispose` at 2026-08-30 01:01:32 CDT. Transient
`/tmp/r171-full-final.log` has 1,624 lines and SHA-256
`3f07a16673639df622402d495db9000ee1cd51b08308f6a8b87b81794f85ce9a`.

The full log contains zero `SimAnticsExc` and zero `bad-routine-frame`
records. Its only startup exception is the known, self-recovering
`splashprogress mount` null reference.

## 8. Honest residuals

- The eight-pixel pie-caption margin is a tested containment correction; its
  exact numeric value has not been recovered from the original binary.
- Occupied family portraits still use the port's established 3D-head
  compositor; only the clearly incorrect empty-slot face is closed here.
- The child Report Card's desktop geometry and grade-letter indirection remain
  incomplete.
- Visual surveys cover the packaged 1024x768 English desktop path, not every
  resolution, locale, custom object, or graphics backend.
- The R170 catalog-popup safety/art disclosures remain: x is clamped to the
  screen, the flat surface is approximate, and the object-thumb cache is keyed
  by GUID for the panel lifetime.
