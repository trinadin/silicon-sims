# R238 independent skeptical review

Reviewed maintained source after the common font correction and the owner's
production captures from September4,2026,21:03–21:04 CDT. This is a visual
regression review against recovered original layout laws, not a claim of
pixel comparison against a running original Mac executable.

## Confirmed

- TallSubpanel is conditional on the original double-byte-UI language
  predicate. The excess empty blue height is absent in surveyed English LIVE.
- Budget uses original measured rows/fonts/indents and native popup tiles,
  with height528 and centered native217x52 OK. Help uses measured179-pixel
  title list, twelve26-pixel rows,340-pixel body, native579x398 window.
- Four-column cTSWinBtn active+hover uses frame1. Frame3 is disabled; the
  selected-hover3 branch is exclusive to six-column images. Updating old
  tests that expected3 is an evidence correction, not weakening a test.
- Common normalization follows original runtime destination arithmetic.
  A final source search found no remaining raw-T2 destination in UI drawing:
  raw values remain only in parsing, metrics and the GlyphY implementation.
- The GPU gate reports all30 independent alpha hashes and offset fixtures
  exact. Independent negative-control raster reconstruction shows28 fixtures
  fail with the old raw-T2 draw; the two48px zero-offset controls do not move.
- Original neighborhood-number+4 and tooltip+2 manual compensators are
  removed at pen origin, preserving final ink placement under the shared fix.

## Visual checks

Directly inspected the current21:04 captures for Create-a-Sim,
Create-a-Family, Select-a-Family, picture dialog, congratulations dialog,
report card, House and Roof. All displayed text is readable and remains
within its intended native panel/control. Picture-body wrapping clears the
image and button. Family and CAS captions have no new clipping. The report
grade has ample clearance. House labels fit beside the value ticks.

Directly inspected21:03 single- and two-line tooltip captures: text stays
inside the borders with the original inset, and multiline text is clear.
Root independently inspected normalized LIVE, Job, Personality, Buy and
Build captures; those are not claimed as independent checks here.

The21:01 specialized Help/Budget/Phonebook/Scrapbook audit captures predate
these normalized general captures. Their frames and button captions were
previously reviewed clean; final normalized versions should also be retained
and checked before packaging. This timestamp caveat was sent to root.

## Scope of conclusion

No additional concrete clipping regression was found in the reviewed
normalized surfaces. The corrected original laws and passing alpha fixtures
support this restoration. Legacy generic VectorFont Height/YOff values and
broader unimplemented original-game interactions remain separate parity
work; this review does not certify all possible UI/game states as identical.

## Final source/document pass

Read `/tmp/r238-final-default.log`: normalized default suite completed129
passed,0 failed,0 skipped at21:14:17, followed by clean Run/Dispose.
Subsequent Scrapbook confirmation guards prevent editing or navigating the
underlying album while confirmation is open, and the list double-click clock
uses monotonic Stopwatch ticks. These final small changes still require the
planned packaged focused rerun.

Current R238 handoff/restoration prose clearly distinguishes recovered
Complete-engine base-game surfaces from unexpanded2000 retail equivalence,
and retains generic edit metrics, gestures, localization/Retina, original
runtime input comparison and live CAS as residuals. No new source-level
release blocker was found. The inherited PARITY.md claim that no residual
needs human eyes contradicts this visual audit; root was asked to supersede
that statement. Final pending-verification text should be replaced with the
actual final package/size evidence once those runs finish.

## Size audit and discovered viewport blocker

Final specialized package run reported16/0. Fresh800x600 and1280x800
captures were directly reviewed for Help, Budget, Phonebook, selected
Scrapbook caption and LIVE. Native579x398,470x528,652x426 and700x560 dialogs
fit wholly within those physical/logical canvases, and text/controls are clear.

The initial requested800x600@2x run actually produced1600x1022 PNGs (PNG
IHDR and runtime capture logs agree), not1600x1200. macOS clamped the window
height. The old division by configured2 yielded only800x511 logical pixels,
clipping Budget's frame and Scrapbook's top/footer/buttons. Help and Phonebook
fit, but this was a real release blocker and was reported to root immediately.
The first size folders did not contain CAS/generic-dialog/tooltip captures;
those cannot be claimed as checked at all sizes from that first run.

With root's explicit authorization, SimitoneGame.cs now computes runtime
desktop scale=min(valid configured scale, viewportWidth/800,viewportHeight/600)
at initialization and resize. It retains the config preference, preserves a
uniform minimum800x600 logical canvas and updates current UIScreen scale and
matrix invalidation before GameResized. Double limits plus a one-ULP downward
correction avoid truncating a limiting logical dimension to599. Requested2
remains2 whenever1600x1200 is available; the clamped1600x1022 case fits at
approximately1.703333. Fractional density is already supported by the port.
SoftwareKeyboard behavior is unchanged.

Blast radius: desktop screen/child matrices, cached UI target sizes, existing
pixel-to-logical mouse conversions and viewport-based world resize. Cached
pie-head geometry remains200logical pixels because its creation-time target
size and inverse draw scale cancel; no forced pie dismissal is necessary.
Root owns independent scale fixtures and updated UI-bounds/size capture gates.
The correction requires the pending final rebuild and expanded size rerun;
this review does not mark the clipped first2x captures as passing.

The initial clipped runs are now retained in `build/ui-audit/r238/before-fit-*`.
The viewport-fit rerun reported12/0 at each requested size and produced54
captures per size, now retained in `before-tile-*`. Direct review confirmed
the formerly clipped Budget/Scrapbook/CAS controls fit after the adjustment.
The2x request used physical1600x1022, effective1.7033333 and logical939x600.
Reviewed Create-a-Sim/Create-a-Family/Select-a-Family at800x600 and fitted2x,
plus Create-a-Sim/Select-a-Family at1280x800: captions, boards and bottom
buttons fit; no new text clipping was found.

That visual review found a second fractional-DPI defect despite green bounds
checks: Help/Budget tiled backgrounds had one-pixel grid gaps exposing the
world. DrawLocalTexture floored each tile origin but retained fractional
tile width, so neighboring12px cells did not always cover shared edges.
With root authorization, only UIOriginalDialogChrome's tiled-cell/corner
path now snaps both transformed endpoints and draws the resulting physical
rectangle. It preserves source clipping, opacity and the existing cached
batch transform; it does not add a cover fill or change global UIElement
rendering. Integer1x is unchanged. Root is adding independent opaque-alpha
coverage checks and preparing fresh captures; the `before-tile-*` images
are evidence of this defect, not passing final visual captures.

The nested tooltip path was also corrected under root authorization to
select native7px first, then apply runtime DPI to drawing and measurement.
Previously a2x request selected14px while keeping scaled13px line pitch.
Wrapping remains294logical pixels, the stored density preference is unchanged,
and1x behavior is identical. Final tooltip font/scale pins belong to root's
rerun. Pixel-perfect original-runtime comparison remains outside this audit.

## Final matrix acceptance

The final all-fixes matrix is retained in the original named directories
`build/ui-audit/r238/800x600`, `1280x800` and `800x600@2x`; each contains54
captures and its size log reports13 passed,0 failed. The separately retained
`before-fit-*` and `before-tile-*` directories remain failed historical
evidence. Final native dialog bounds checks pass at all three settings.

Directly inspected the final fractional Help, Budget, Create-a-Sim and selected
Scrapbook captures: the Help/Budget grid holes are gone, their complete frames
and buttons fit, and CAS/Scrapbook controls remain fully visible. The fitted
case is correctly described as1600x1022 physical,939x600 logical, runtime
density approximately1.7033333 with the configured2 preference preserved.
It is not an unclamped1600x1200 display or an integer2x visual comparison.

Read the final size logs: GPU tile coverage is zero uncovered pixels at
densities1,1.25,1.7033333 and2. Root also compared final Help/Budget RGB with
their pre-tile captures at both1x sizes and found exact identity. No remaining
release-blocking clipping or tile-seam defect was found in the reviewed final
matrix. Fractional rendering naturally resamples the original bitmap art;
that is a desktop viewport adaptation, not a change to the native layout law.
The exact final package's full default129-check run is owned by root and was
still running when this final visual acceptance was recorded.

## Final package result recorded by root

The exact final package completed all 129 default checks with zero failures
and zero skips at 21:43 CDT, followed by Run/Dispose and wrapper exit 0.
All 30 font alpha fixtures, native 07-face tooltip scaling, four-density tile
coverage and configured-resolution roundtrip passed. The three final size
runs passed 13 checks each. See `../r238/final-default.log`, `size-*.log`
and package/image SHA-256 manifests.
