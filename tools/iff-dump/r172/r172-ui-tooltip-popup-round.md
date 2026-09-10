# R172 — tooltip and catalog-popup composition repair

## Scope and evidence boundary

R172 is the user-authorized follow-up for four visible desktop defects:

- tooltip captions sat too high inside their one-line boxes;
- the permanent left UCP plate met the swapping right panel with a visibly
  severed curve;
- catalog hover descriptions could be covered by the bottom toolbar; and
- the selected-product description could collapse to a 45px slice and remain
  on screen.

The PowerPC executable used for original-engine recovery is the owner's local
`game-data/The Sims/The Sims Complete`, SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`.
The original font table is the owner's
`Fonts\\variablesans_07.ffn`, SHA-256
`aaa09e67871e115279771412bd22711e557944a68ff6cd96941e9dfc96e38032`.

This round commits only source, tests, and this metadata/evidence narrative.
Original executable bytes, font bytes, art, game data, and screenshots remain
local and are not redistributed.

## 1. Tooltip geometry recovered from `cDefaultTTWindow`

### Original law

The original `cDefaultTTWindow` path supplies concrete geometry rather than a
generic centered-label rule:

- `Init` requests face 1 at size 7 (`0x3b9884..0x3b98a0`);
- `SetToolTip` sizes the window to `textWidth + 6` by `charHeight + 2`
  (`0x3b9428..0x3b9448`); and
- the paint path places the text at x=3, y=1
  (`0x3b9634..0x3b964c`).

The shipped `_07` FFN carries a -2 top bearing. The original cached glyph
sheet normalizes that bearing before paint; the port renders the raw FFN and
must therefore add two pixels to the logical y origin to reproduce the same
visible baseline.

### Cause and repair

The port requested size 8, reserved ten horizontal/four vertical padding
pixels, and centered each line. At the user's 2x display path that selected a
different table and made the already-tight box visibly top-heavy.

`UILayer.DrawTooltip` now uses the recovered size-7 law:

- 294px content safety wrap inside a 300px maximum;
- width `maxLineWidth + 6`;
- height `13 * lineCount + 2`;
- left origin x=3; and
- logical y=3 (the engine's y=1 plus the raw-FFN 2px cache normalization).

Multiline pitch remains the original 13px. DPI scales the table request,
insets, height, and pitch together; the 1x recovered law is independently
render-pinned so a saved UI zoom cannot change the evidence probe.

### Permanent render contract

`uidtips` now executes the production renderer into transparent 128x64
targets. With `Plant Tool` at `(32,32)`, it requires:

```text
outer box: x=32..95, y=17..32
font ink:  x=35..90, y=21..28
font table: 7
```

The top and bottom interior ink gaps are therefore equal. A second
`Plant Tool\nPlant Tool` render at `(32,48)` pins the 13px multiline pitch:

```text
outer box: x=32..95, y=20..48
line 1 ink: x=35..90, y=24..31
line 2 ink: x=35..90, y=37..44
gap ink: false
```

The local artifacts are `~/Documents/Simitone/uisurvey-tooltip.png` and
`uisurvey-tooltip-multiline.png`; neither is committed.

## 2. UCP-to-main-panel seam: corrected parent/child paint order

R171 correctly identified `BackPatch.bmp` as the original 52x100 bridge at
main-panel-local `(-52,0)`, screen x=168..220. It incorrectly treated the
port's sibling order as equivalent to the original paint order.

The original `cWinCPanel` paints its opaque `kBackPatch` parent background
before child windows. The `cWinViewControl`/UCP child then paints its authored
quarter-round alpha edge above that bridge. In the port, `DesktopUCP` was
added first and `MainPanel` second; the latter's opaque bridge therefore
covered the UCP's rightmost 52 pixels and cut the curve flat.

`UISimitoneFrontend` now keeps the same components and coordinates but
re-adds `DesktopUCP` after `MainPanel`, producing the original composition:

```text
PanelBack + BackPatch parent surface -> DesktopUCP child surface
```

No bridge or plate art was replaced, resized, shifted, or regenerated.
`uibargeom` requires exactly one `MainPanel`, exactly one `DesktopUCP`, and
the UCP's frontend sibling index to be above the panel while retaining every
R171 52x100/x=168..220 local-art pin.

## 3. Hover-product description depth and lifecycle

### Cause

`UIBuyBrowsePanel.SetupBand` constructed and mounted its dedicated
`UIOriginalCatalogPopup` while the browse panel itself was still in its
constructor. `UIMainPanel.SetSubpanel` appended the completed browse panel
afterward. The popup was consequently below the catalog in sibling draw order.
For a second-row product the popup ended exactly at catalog y=50, so the bar
covered its lower 50 pixels and made the description appear low or truncated.

The popup could also remain visible after a selected cell received MouseOut.
That exception was port-authored; selection and hover descriptions are
separate original surfaces.

### Original law and repair

The recovered catalog path enables the popup from hover
(`0x26a76c..0x26a7b4`), disables it on child exit
(`0x26a8c0..0x26a920`), composes it as an overlapping child at show time
(`0x26e000`), removes it with the catalog (`0x26df40`), and anchors it
above-left from the measured surface (`0x26dd98..0x26dea4`).

`ShowBandPopup` now re-adds the already-owned popup to `MainPanel` at show
time. `UIContainer.Add` moves the existing child to the top without replacing
or disposing its texture. MouseOut always hides the hover popup, including
for a selected cell. Click hides the hover surface before opening the separate
selected-product plaque; pickup, put-down, delete, deselect, and custom-control
release also clear it.

`uibandlaw` now requires the popup to be the topmost main-panel child above the
browse panel, verifies unconditional MouseOut dismissal with and without a
selection, and retains the icon ownership, dynamic-height, anchor, and detach
contracts from R170/R171.

## 4. Selected-product description is atomic on desktop

### Cause

`UIQueryPanel.SetShown` inherited a touch/mobile expansion animation. It
resized the original 559px-wide product plaque between its measured height and
a 45px summary. Entering the world to place an object invoked
`SetShown(false)` while `Active` deliberately stayed true, exposing that crop
as the half-panel seen in the submitted capture. A separate 0.5-second opacity
tween could leave the old card fading over the next selection.

### Repair

Desktop now treats the selected-product plaque as one complete composed
surface:

- `Active=true/false` applies visibility and opacity immediately;
- `SetShown(false)` retains full measured geometry but makes the panel wholly
  invisible;
- `SetShown(true)` restores the whole surface at opacity 1; and
- the touch/software-keyboard branch retains its authored summary/expansion
  animation unchanged.

`uidesc` exercises a real selected catalog item and requires all three desktop
states—shown, hidden, restored—to preserve `Size.Y == FullHeight`,
`ShowPCT == 1`, and the full-height bottom anchor. Hidden means `Visible=false`
and opacity 0, not a residual crop.

`uisurvey` adds two production artifacts:

- `uisurvey-buy-query.png`: the complete selected-product plaque above the
  toolbar; and
- `uisurvey-buy-query-hidden.png`: the same catalog after world-entry hide,
  with no 45px remnant.

It also retains `uisurvey-buy-popup.png` for the hover surface. All artifacts
remain local under `~/Documents/Simitone/`.

## 5. Exact-source validation

The integrated Release client build completed with 0 errors and 78 existing
warnings. Exact current source was published self-contained for `osx-arm64`
and packaged with `packmac.sh arm64` as
`dist/The Sims-arm64.app` (approximately 2.1GB).

The publish and packaged `Simitone.Client.dll` are byte-identical:

```text
1c8b42e915dd1595462da014bc9ea8197513787963958c906d9aa02e692c29b9
```

Focused command:

```sh
NSUnbufferedIO=YES 'dist/The Sims-arm64.app/Contents/MacOS/TheSims' \
  -ApplePersistenceIgnoreState YES \
  "-path$PWD/game-data/The Sims" -autotest 5 \
  -autotest-opts lot,corpus,uinav,uibandlaw,uibuild,uibargeom,uisurvey,uilive,uijob,uidlgchrome,uiqueuegeom,uicp,uidtips,uidesc \
  -autotest-timeout 600000
```

Final focused result: **18 passed, 0 failed, 0 skipped**, wrapper exit 0 and
clean `after Run` / `after Dispose` at 2026-08-30 01:35:31 CDT. Transient
`/tmp/r172-focused-final.log` has 737 lines and SHA-256
`5cadc3d5cf022c21c53dc51cba6074458dd1238d7ecb433816a64ff0b2a85097`.

Final full-default result: **107 passed, 0 failed, 0 skipped**, wrapper exit 0
and clean `after Run` / `after Dispose` at 2026-08-30 01:39:48 CDT. Transient
`/tmp/r172-full-final.log` has 1,627 lines and SHA-256
`00e44ad612dddd9ec7c4412a7b45b787066a94eaf53f8c122f4efd497afc0fe7`.

The full log contains zero `SimAnticsExc` and zero `bad-routine-frame`
records. Its only startup exception is the known self-recovering
`splashprogress mount` null reference.

## 6. Honest residuals

- The catalog hover surface still carries R170's disclosed flat dark surface
  approximation and safe x=0 clamp; this round fixes its depth/lifecycle, not
  unrecovered chrome.
- Visual surveys cover the packaged 1024x768 English desktop path. The
  geometry code remains DPI-aware, but every locale, custom object, graphics
  backend, and window size was not visually sampled.
- The selected-product panel's content/art laws remain those recovered in
  R124/R169/R170. This round changes only desktop show/hide composition.
- Touch/software-keyboard animation is intentionally unchanged and is not
  claimed to match the original desktop UI.
