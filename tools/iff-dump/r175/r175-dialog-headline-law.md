# R175 — ObjectDialog headline and portrait law

## Reported defect

The Easel creativity-point dialog rendered `Congratulations!` as a tiny black
label in the upper-left of a navy window. The body remained readable. This was
not a palette tweak: the port had classified a picture-less/avatar dialog as its
generic alert composition.

## Original asset provenance

- `Objects.far` member: `Easel.iff`
- member length: `551339`
- member SHA-256 prefix: `315441bf…`
- STR# 301, `Dialog prim string set`, IFF offset `0x2d5e`
- chunk SHA-256 prefix: `ec56b70d…`
- English 6: `Congratulations!`
- English 7: `What vision, what talent! $Me received one Creativity Skill Point.`
- BHAV 4112 `add points`, instruction 10: primitive 36, operands
  `00 00 07 00 00 00 06 00`

The zero icon bits request the automatic icon. `$Me` selects Bella, so the
native dialog includes her 45x45 portrait rather than omitting the picture.

## Native class and typography

`ObjectDialog::SetupDialog` at file address `0x0cc5d0` constructs
`cWinPictureDialog`. This remains the class even when no picture is ultimately
available. `SetTitle` at `0x2974b0` loads font table slot 14 at
`0x2974b8/0x2974dc` and calls `SetFont` at `0x297514`.

- Title font: `Fonts\variablesans_14.ffn`
- Title font SHA-256:
  `9075d4b9422e804b9f8614601843f1e8f94fffb05566ee811b299c5dc991efcc`
- Title color: `RGB(195,205,205)` / `#C3CDCD`
- `Congratulations!` natural extent: `157x27`
- Title rect top: `y=21`; visible baseline: `y=38`
- Body font: slot 12, 23px line height, also `#C3CDCD`
- Chrome: `PopupInfoTiles.bmp`, resource 3008, 36x36 tiled thirds

The title renderer never sets black. It is a transparent, one-line text control
with no opaque title strip.

The exact English portrait composition resolves to:

- outer window `407x207`
- `SimStub.bmp` at `(21,21)`, size `133x103`
- portrait `45x45`, centered at `(65,50)`
- title rect `(191,21)-(348,48)`
- body control origin `(21,66)`; first line begins at `x=172` after the 151px
  portrait exclusion
- OK button `100x33` at `(153,153)`

For a legitimate no-image ObjectDialog, the title stays slot 14 / `#C3CDCD`,
at `y=21`, centered across the whole outer window.

## Separate generic message-box law

Generic `cTSWinMsgBox` is not the ObjectDialog class. `DefaultLabel` uses system
font slot 0, mapped by `SetupWinCtrlMgr` to font table slot 11. Its title and
body use `#C3CDCD`; the title begins at `(16,16)` and a titled body at `(16,45)`.
The implementation therefore preserves two distinct native classes rather than
globally forcing every dialog title to slot 14.

## Port mapping and gate

- `UILotControl` marks real VM Dialog primitives as picture-dialog class before
  mounting the alert, renders game-object icons at 120x120, and now renders
  automatic-mode avatar icons through the 45x45 `SimStub` branch. Other icon
  modes cannot accidentally inherit the current VM stack entity.
- `UIMobileAlert` keeps picture-dialog class identity independent of icon
  presence, uses slots 14/12, and removes the phantom image exclusion in the
  true no-image branch.
- `UIMobileDialog` gives generic desktop message boxes their separate slot-11
  font, color, and native origin.
- `uidialog` and `uidlgchrome` pin the font slots, line heights, colors, class
  routing, no-image geometry, exact `Congratulations!` portrait geometry,
  icon-mode decoding, empty-body draw safety, button law, and eyes-on survey
  artifacts.

Focused packaged run on 2026-08-30:

`AUTOTEST RESULT PASS passed=7 failed=0`
