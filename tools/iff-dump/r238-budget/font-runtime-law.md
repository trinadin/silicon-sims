# R238 skeptical font-runtime and button-state audit

The executable and all FFNs are the owner's mounted originals. Run
`python3 tools/iff-dump/r238-budget/recover-font-runtime.py` to reproduce the
45-table metadata inventory and address-labelled disassembly in
`recovered-font-runtime.txt`. No original font assets are copied here.

## Vertical glyph offsets are normalized runtime destinations

`InitBitmapped` initializes `minT2=0` and maximum bottom to zero at
`0x4b4530–4550`. For each nonempty glyph except character160, it updates the
maximum of ink-height plus raw T2 (`0x4b4594–45d0`) and minimum T2
(`0x4b45d4–4600`). When the minimum is negative, it subtracts the minimum
from BOTH font height (`0x4b4640–4660`) and every runtime glyph's +0x14
field (`0x4b4664–47dc`). The second operation was omitted by the port.

This is not an atlas repacking adjustment. `TSDrawChar` loads +0x14 at
`0x4b359c` and adds it directly to destination pen Y at `0x4b35ac`.
The adjusted Y multiplies destination pitch at `0x4b3638`, then adds the
destination pixel base at `0x4b3660`. Source-atlas coordinates are taken
independently from the glyph rectangle at `0x4b3634–3658`.

Thus the exact law is `drawY = penY + rawT2 - min(0, minimumEligibleT2)`.
`LineHeight` already applies this normalization to height, so this correction
must not change row heights, text advances, atlas U/V, or raw FFN metadata.

English-font downward shifts are: 07=2; 08/09/10=3; 11/12=4;
11_bs/11_s/12_bs/14/16=5; 14_bs=6; 18/20=7; 48=0 pixels.
Polish/Russian 18 is6; Russian48 is18; see the inventory for every face.

## Blast radius and preservation requirements

Direct draw paths: `OriginalGlyphFont.Draw`, `UIOriginalText.Draw`,
`OriginalVectorFont.Draw`, `UIBigButton`, clipped TextEdit/TextList rows,
clipped Help body, and both clipped Job-subpanel draws. The default vector
bridge means labels, buttons, lists, text edits, tooltips and headlines also
change. Explicit font consumers cover LIVE tabs, Buy/Build, dialog windows,
CAS/family views, neighborhood screens, pies, loading screens and toolbars.
This is a product-wide ink-origin correction, not a local dialog tweak.

One existing compensator MUST be removed with the common correction:
`UINeighbourhoodSwitcher.CurrentNumberGlyphT2Normalization=4`, currently
added to the current-number label's Y at its construction. Its existing gate
must assert the same final ink location with common normalization, avoiding
a second four-pixel shift.

The nested maintained FreeSO fork also has an explicit +2 compensator in
`FreeSO/TSOClient/FSO.UI/UILayer.cs::DrawTooltip` (`tooltipGlyphTopNormalization`).
Remove it from the tooltip pen origin when the default vector bridge adopts
common normalization. The native tooltip inset stays1, height13+2, so final
tooltip ink is unchanged. This is a maintained fork source change, not a
generated output or upstream mirror edit.

`ButtonCaptionY` must subtract the normalized capital-A top. The original
caption law is `(buttonHeight-A.inkHeight)/2-A.runtimeTop`, so normalizing
both origin and draw offset cancels exactly for centered button ink. Keep
raw T2 intact for existing byte-pinned glyph assertions. Tests should assert
the independently recovered offset per font and actual transformed glyph
destination, including negative-top accented glyphs and clipped rows.

Visual checks should cover all changed text surfaces after the common fix,
especially short Needs bars, active Job rows, tooltips, small toolbar text,
scrapbook edit fields, CAS captions and neighborhood current number.

## Applied patch and independent render fixtures

`prepare-font-normalization-patch.py` writes only
`font-normalization-review.patch`; it does not modify product files. The
10-path patch preserves raw T2 and existing metrics, stores RuntimeTopOffset
once during parsing, and routes all current glyph destinations through
GlyphY. The patch was applied after impact review and the 51 baseline
captures recorded in `r238/before-font-images.sha256`. It is retained as
historical review evidence, not a patch to reapply to current source. It includes
the nested FreeSO tooltip compensator, neighborhood compensator and related
gate update. Generic label/vector `Height` and `YOff` approximations remain
separate unresolved metrics questions; this patch does not alter them.

`recover-glyph-render-fixtures.py` independently extracts source nibble ink
from owner FFNs and composes expected alpha at native size using the original
engine's recovered destination law. `glyph-render-fixtures.txt` contains
30 scalar/hash fixtures (capitalA and the minimum-top glyph for each English
face), with no source pixels. Runtime render gate procedure: load the named
font, clear a64x80 RenderTarget2D to transparent, SpriteBatch.Begin with
AlphaBlend/PointClamp, call font.Draw for that one byte-character at(4,4),
read GetData<Color>, hash its5120 alpha bytes in row-major order. Compare
the hardcoded independently generated hash and RuntimeTopOffset from the
fixture. Retain raw-T2 pin checks. Every nonzero-offset face must fail with
the old renderer; _48 is a zero-offset control. This verifies actual glyph
placement and pixels rather than simply mirroring GlyphY arithmetic.

For shared UI paths also capture post-normalization clipped Help/list/editor
rows and compare the visible native rectangles. Rechecking only the
SpriteBatch path cannot by itself establish that every UI consumer was
converted; the nine callsite audit above and survey are required.

## Four- versus six-column button images

`CalcRowCol` at `0x50d2e0–d37c` explicitly handles image column counts4/6.
For enabled buttons, active always selects1 for four-column art, including
hover; only six-column art selects3 for active+hover (`0x50d300–d320`).
Enabled inactive hover selects2; idle selects0. Disabled four-column art
selects3 (`0x50d348–d354`); six-column selects4 or5 based on active state.
There is no selected+hover frame3 exception for this native four-column
branch. The generic sum-of-active-and-hover branch at `0x50d3b8–d3d8`
is only reached for other column counts. `+0xf0==3` bypasses CalcRowCol
entirely and preserves caller-specified frame; such custom controls require
their own explicit handling.

The same frame index selects caption palettes and custom offsets. Native
four-column buttons therefore use their pressed (+2,+2) caption offset
even while hovered. Scrapbook Done/Delete SetImage passes4x1 at
`0x29f744–750` and supplies offsets[(0,0),(2,2),(0,0),(0,0)].
