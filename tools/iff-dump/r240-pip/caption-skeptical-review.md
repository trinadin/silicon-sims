# Independent PIP caption and placement recovery

Source is the owner's local `game-data/The Sims/The Sims Complete`, SHA256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`.
Addresses below are file offsets, matched against `r153/r153-symbols.txt`.
The new `*-skeptic.txt` files are fresh Capstone decodes using
`/opt/homebrew/bin/python3 tools/iff-dump/r145/capdis2.py START END`.
No original assets were modified or copied into distribution output.

## CP mode is mini UI, not live mode

`CPState::GetCPMode`20c9f0 reads+220, independently of main mode+8.
`SetCPMode`20c988..998 tests mode==1 and passes that boolean to
`cDDDSimsView::SetMiniUI`212bf0. Consequently PIP Open299d30..f18
places the bottom edge5 pixels above the viewport bottom for mini UI.
Normal UI uses105, or155 when IsDoubleByteUI returns true (language15,
17,18,19,20). Right margin is5 in all branches. A normal live/buy/build
mode selector is not a substitute for CP mode.

## Caption font, color and geometry

BuildImage2992d4..93d0 checks nonempty caption+e0 and autosnapshot flag+e8
false, then uses global font table+30: slot12. The client-local paragraph
rect is `(5, H-5-L*fontHeight, W-5, H-5)` where L is the native wrapped
line count at widthW-10. DrawTextPara receives style0, freshly verified
at4ad898..c4 as left alignment. Its line step is font+18 (LineHeight).
There is no separate title, background plate, shadow or caption button.

InitSimsColors25d680..6a0 creates RGB195,205,205 in the global loaded into
r29. The freshly recovered25d9cc..da78 loop initializes23 font-table slots
and applies this color via font virtual+28. Thus slot12's ordinary ink is
#C3CDCD, not the generic port paragraph's #FFFFF0. The PIP-local renderer
now explicitly uses the recovered ink.

CalculateWordsToFitInWidth4ad3b0..5cc uses next glyph's ink width to test
fit, then substitutes its advance when moving to the following character.
The predicates at4ace40..ed8 are: whitespace=space; return=LF; breaking
character=space or hyphen. A returned row includes the breaking character,
consumes following spaces, and preserves source character counts. With no
break, the count>1 fallback returns count-1; if too few characters fit it
advances through the whole token to a space/LF, then guarantees progress.
This differs from UIOriginalParagraph's whole-word-only splitting and
space reconstruction, so only PIP uses the new local implementation.

Native DrawTextPara4ad974 compares Y against bottom as unsigned. An
otherwise valid bottom-anchored paragraph whose full line count produces
negative startingY draws no lines. This quirk is retained. For validY,
font glyphs are clipped to the full WxH image buffer; the five-pixel text
rect controls layout, not the actual clipping boundary. Port glyph source
rectangles are intersected with these exact bounds. CRLF normalization is
an existing port compatibility convention; this is not a claim to restore
the original multibyte font machinery.

BuildImage2993d4..9484 handles automatic snapshots separately: it saves
the caption-free image, assigns scrapbook description, clears caption and
pending flag. The UI follows that separation.

## Renderer vertical framing

For an object, BuildImage299218..268 reads last-damage rect at the current
main viewer zoom, then computes
`offset = -(damageHeight >> (mainZoom - requestedZoom + 3))`.
DGRPRenderer's Bounding is in native discrete-zoom sprite pixels, without
PreciseZoom multiplication, so its height supplies the matching source.

For an avatar, independent skeptic recovered the constants at
CODE59b7a0/file5a4630 and CODE59b7b8/file5a4648, plus sqrt import5a1b78.
The body-height factor is `float(2 * float(2*sqrt(6)))`. BuildImage scales
by `1 << (mainZoom+1)`, truncates to integer, divides by2 and negates.
Offsets for main zoom0,1,2,3 are -9,-19,-39,-78. The requested PIP zoom
is deliberately absent from this branch. UI now passes the signed value
to the dedicated renderer; negative moves the target's floor point below
the image center. See the other skeptic's constant evidence for provenance.

## Review corrections and bounded tests

An initial suspicion about LivePIP=false suppression was rejected after
independent review. Generic DoPictureInPicture213540..580 can create a
static image and Fade299908..928 handles that path. However the typed
opcode35 route performs its own earlier option filter in TryUserEvent
facf8..fad24. Root's synchronous Suppressed response correctly represents
that earlier filter and avoids the second yield. No suppression change
was made based on the generic-caller observation.

Close button command299fcc..a000 clears target and live flag and cancels
the timer. Opcode close2999c4..a08 first checks target identity. Root's
common close and foreign-close protection agree. A remaining Open edge
was reported to root: native installs new target/caption/duration before
its SkipIfVisible early return299b54..c3c, without hiding an already visible
window. The first implementation called Hide; root owns any lifecycle
correction and its integration test.

`AutotestPIPCaption240.Check` tests independent fixed-width fixtures,
including hyphen, LF, consumed spaces, too-narrow token and count-1 fallback;
object/avatar numerical framing; actual font12 GPU rendering in a temporary
132x132 target; no ink outside the100px window; and the native overheight
caption no-draw condition. It restores GPU state and owns/disposes only
its temporary target and batch. Integrated execution belongs to root.

## Integrated review findings resolved

The first caption pixel fixture failed because its unattached UIElement had a
position assigned without CalculateMatrix; the draw API uses the cached
matrix. The fixture now explicitly calculates that matrix. The actual game
screenshot already showed the expected caption inside PIP. Independently,
root and both reviewers traced the displaced close button to UIButton.Width
being a resize override (zero for natural images), whereas Size.X is the
resource30 frame width. Root corrected the placement calculation to Size.X
and added its geometry assertion. Neither issue justified replacing the
original asset or changing the native four-pixel button inset.

The focused integrated package subsequently passed the caption gate at
23:47:37 and finished13 passed/0 failed at23:47:39. The gate includes actual
font12 ink containment and native overheight behavior after the fixture
matrix correction. Root owns final renderer crop and package verification.
