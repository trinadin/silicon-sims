# R241 independent tutorial presentation contract

This is a read-only audit of the owner's original executable and maintained
source at the beginning of R241. No product code, original data, build, or app
launch was performed by this reviewer. Root owns integration and runtime QA.
The R239 ownership report was used as a starting index; the presentation rules
below were independently decoded. Derived metadata only is stored here.

Original: `game-data/The Sims/The Sims Complete`, SHA256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`.
Addresses below are file offsets, two less than r141 symbol-index addresses.
`decode.py` records the main disassemblies; ancillary files contain focused
regions. PEF code-relative pointers require +0x8e90 to map to file offsets.

## Eligibility and presentation flags

- `ObjectDialog::SetParams` cca8c..ccab0 sets +3c by comparing caller against
  ObjectModule's current tutorial owner. This is independent of dialog type4.
- ccab8..ccadc sets +3d for caller GUID **0xc3249a1d**, independent of ownership.
  Only this GUID suppresses title in SetupDialog cc634..cc67c.
- Operand byte5 low7 supplies type. Its high bit sets +3e. Flags byte7 bit0
  supplies Continue (+38); byte7 bit7 is inverted to produce modal (+39).
- SetupDialog cc6e0..cc764: nonmodal replaces primary bottom button with a
  closebox returning result2. An owned caller uses resource31 (cc700..cc70c);
  another caller uses default resource30 (cc714..cc71c). Modal keeps the primary
  bottom button. Types1/2 can add result3, type2 result4. Type4 does not by
  itself remove the bottom primary button.
- cc878..cc8a0 omits the ordinary 60000ms timeout for type3, type4, or owned
  tutorial dialogs. Other types receive timeout to the last button result.
- cc8a4..cc8d8 maps Enter to primary result2, Escape to last result, and Space
  to primary when there is no secondary button. Closebox-only does not remove
  these mappings.
- cc8dc..cc900 calls **SetTransparent(true,200)** for the GUID, not SetSize.
  Vtable+64 resolves to506060; GetOpacity502770 returns byte+a8 when the
  transparent flag is set, versus255 for ordinary opaque windows. This is
  opacity200/255. The rule applies even if this GUID is not current owner.
- cc904..cc91c sets picture-dialog target aspect ratio +138 to1.2 when the
  high type bit is present. Default is2.0 (297af0..297b00). This is not width.

## Icon resource, placement, input, and lifetime

- cTutorialIcon ctor25ef50 initializes rows6, columns1, current row/column0,
  45x45 cell size, animation0, target null, and last rectangle all−1.
- ObjectChanged25e0e0..25e1c8 clears animation and last rectangle; if an owner
  exists it shows the icon, loads that owner's private BMP_300, and places it
  at home. Otherwise it hides the icon.
- SetIcon25ec20..25ed48 destroys its prior buffer and loads private BMP_300 via
  LoadBuffer(ColorType4). Width45 forces columns1; height45 forces rows1;
  otherwise the prior grid value is retained. Row/column reset0. Native cell
  size is bufferWidth/columns by bufferHeight/rows. No frame-cycling writes
  occur in cTutorialIcon methods; idle paints frame0.
- Actual base resource Tutorial.iff!BMP_300 is TutorialIcon.bmp,45x45,8bpp.
  Its four corners are opaque blues and it contains no magenta pixels.
  Metadata is in resource-metadata.json and icon-color-metadata.json.
- This icon path never sets a color key: LoadBuffer3b6430→BMPFactory22b780→
  LoadBMP16, ordinary buffer. Closebox magenta-key handling must not be
  inferred for the tutorial icon. Missing icon falls back to45x45 and the
  same three-outline drawing as animation, including a clickable hit area.
- WindowSetup255d90..255dbc passes (view.Right−1,view.Top+1) to
  SetIconTopRight25e980. That produces home=(x−iconWidth,y,x,y+iconHeight).
  Init alone uses right640/top0, superseded by the window setup call.
- Left mouse down while animation0 calls ObjectModule::ShowTutorialInfo;
  right mouse down consumes the event without action. Animated icon ignores
  left activation. e2bf0..e2c20 runs the owner's named tree
  **"show situation info"**. It does not simply reshow a cached dialog.
  That name is BHAV4099 in the base Tutorial.iff fixture.
- BMP.GetTexture in maintained tso.files creates a fresh texture per call,
  so presenter disposal of its own BMP texture is valid (not cache disposal).

## Anchoring and animation

SetupDialog cc920..cc9fc handles owned callers with an icon: use last target
rectangle if it has positive width and height (GetLastTargetArea25ed90), else
use current icon rectangle; call SetPositioning(1,rect); hide real dialog;
show icon; focus icon unless flag0x2000; StartOpenAnimation(icon,dialog).

Positioning1 is precisely decoded in TSBeginModal29635c..2965f0:

1. Split main window into TL,TR,BL,BR quadrants, integer half coordinates.
2. Choose the quadrant with largest positive intersection area with reference
   rectangle. Strict greater-than means ties choose the first; empty choosesTL.
3. Align corresponding dialog corner to corresponding reference corner.
   Thus initial top-right icon yields dialog.Right=icon.Right and
   dialog.Top=icon.Top. Reopening uses last dialog corner.
4. There is **no clamp** after this case; case2's separate bounds corrections
   do not apply. A port fit accommodation must be identified as adaptation.

Icon Animate25e200 is called by main-view Draw216d34..216d40. It is elapsed
wall-clock time, independent of simulation speed/pause. Four RampGenerators
interpolate L,T,R,B linearly, for500ms. Setup14e890 stores end, slope and end
clock; GetVal14e930 returns end−remainingTime*slope and clamps at end. Each
edge is converted using fctiwz (truncate toward zero).

| Original state | Action |
| --- | --- |
|0| Idle bitmap/fallback; input allowed |
|1| Prepare current icon rectangle→dialog rectangle ramps; become2 |
|2| Sample expansion; on end hide icon, show target dialog, clear target, idle |
|3| Prepare lastTarget→home ramps; become4 |
|4| Sample shrink; on end set home rectangle and idle, icon remains visible |
|5| Prepare lastTarget→home ramps before another open; become6 |
|6| On end set home and state1; next Draw begins expansion |

StartOpen25e8d0 saves target; current icon rect==home selects1, otherwise5.
StartClose25e810 copies dialog rect to current icon and lastTarget, selects3.
TSPaint25de40 paints bitmap only with image and state0. Otherwise it paints
three nested **1px black, white, black rectangle outlines**. Buffer vtable+58
resolves to DrawRectangleOutline488c20 (resolved-indirections.json). It does
not scale a dialog bitmap or fade dialog text during the rectangle transition.

BtnPressed cc270..cc2f8 shows/focuses icon and starts shrink for owner dialog.
Results are processed immediately, independent of the500ms animation. Close
should unregister the real dialog/modal blocker immediately and keep only
outline state. Abort cc3f4..cc458 instead saves owner dialog rect in LastTarget
and destroys the nonmodal window without starting shrink. A replacement may
therefore shrink then reopen based on the icon's retained current rectangle.
Owner change cancels the animation and resets saved target geometry.

## Closebox and body geometry

AddCloseBox296f20 loads system BMP31/30, sets key from TOC[-7208], configures
four states/one other sheet dimension, sets result2, and adds it separately
from the bottom-button vector. InitSimsColors25d84c..25d874 proves the key is
RGB(255,0,255). DoLayout2961c8..296228 positions it at
**(dialogWidth−buttonWidth−5,5)**, at its native cell size. TryLayout performs
no separate body/title exclusion for it. It does not consume a bottom row.

DoLayout starts outer300x185 (295db8..295dd4), not200px. Text layout uses the
existing cWinPictureDialog law: frame thirds +9px inner gutters; titleless
body origin=(frameCellWidth+9,frameCellHeight+9). No-title/no-image minimum
width consideration begins100 in TryLayout2955cc..295610, but existing text
width can be wider. The outer aspect search uses target2.0 or1.2. This audit
has not implemented/revalidated all ordinary cWinTextWrap wrapping rules.
Existing port's width search only expands a too-tall result, whereas original
DoLayout also halves an overly wide candidate (296020..296094, max10trials)
then bisects bracket(max10,stop difference<=20). Existing ordinary layout
approximation must not be relabeled exact tutorial body parity merely because
anchoring, title, closebox, and animation have been restored.

## Initial R241 code review findings sent to root

At the first implementation read, the following were actionable:

- Close only hid the mounted real dialog, leaving registered modal blocker and
  stale window; unregister it at shrink start and clear callback/reference.
- Open always expanded from Home; omitted native shrink-before-reopen path.
- Missing BMP set0x0/invisible instead of45x45 outlined/clickable fallback.
- No original Enter/Escape/Space routing for closebox-only lesson windows.
- GUID opacity assigned only inside owner branch despite independent rule.
- Type4 fallback in tracking/nonmodal helper is wider than original ownership
  predicate; do not attach ownership-only behavior to type4 alone.

These are timestamped review findings, not assertions that root left them
unfixed. Root is integrating concurrently and owns final focused/full tests.

## Required independent validation and remaining scope

Pin all eligibility combinations (owner/unowned, GUID/other, modal/nonmodal,
type0/type4), resource keys/cell size, all four anchor quadrants/ties, visible
state at0/250/499/500ms, shrink-before-open at500/1000ms, owner-clear during
animation, same-owner replacement, modal blocker removal and keyboard focus.
Render the mounted base icon and real titleless/no-button lesson at1x and
fractional fit scale. Ensure outline edges remain continuous at fractional
DPI, since generic DrawLocalTexture independently truncates position/size.
None of those runtime checks were run by this reviewer in R241.

Full lesson event polling/control highlighting, tutorial-family setup/reset,
and tutorial completion are separate remaining systems. This presentation
contract does not establish end-to-end tutorial lesson parity.

## Second implementation review (during root build)

Root's revised presenter now unregisters the dialog at shrink start, keeps
current geometry for replacement transitions, draws the45px missing-BMP
fallback, snaps outline endpoints together, and applies GUID opacity outside
the owner branch. These close the corresponding initial findings by source
inspection. Root also added elapsed-time, modal-removal, key, anchor and GPU
fixtures through an isolated real UILotControl dialog sink.

Remaining precise findings sent to root in this review:

1. HandleTutorialKeys selects the last UIMobileDialog, not the actual top
   registered dialog. Help/Budget/Phonebook/Scrapbook use different classes;
   a lesson underneath can consume their Enter/Escape. New tutorial key
   routing must honor all dialog/modal layers, with an explicit overlay test.
2. Space is currently assigned even for owned YesNo/YesNoCancel. Original
   SetupDialog only assigns Space when there is no secondary button.
3. Tutorial width-search halving uses requested lowWidth repeatedly after
   the evaluator clamps it. Original next halving reads actual current width
   (296020..296028), proposes half, records that pre-TryLayout candidate
   (296064..296070), then evaluates. Thus at a100px minimum native repeatedly
   proposes50 rather than50,25,12,6,... . Midpoint bounds are updated from
   post-TryLayout width (2960e8..296110). Match these two distinct read points;
   simply updating every bound to evaluated width is also not exact.
4. During interrupted expansion/shrink, state5's source is LastTarget, not
   current interpolated outline (25e398..25e4d0). Completed-open replacement
   has equal rectangles and does not exercise this distinction. Add250ms
   interruption coverage. Native state6 schedules expansion for the next Draw;
   beginning the expansion clock immediately at the transition is a one-frame
   timing accommodation, not an exact instruction-level reproduction.

These findings are delivered before final root validation and may be fixed
concurrently. This reviewer has not run the build or test fixture.

## Opacity composition follow-up

The200 opacity applies to the **composited entire dialog**, including child
text and buttons, not just the background. This is established independently
from native buffer routing and paint order:

- cWinPictureDialog::Init2977f8..29780c invokes vtable+17c, resolved to
  cTSWin::PrivateBuffer505840, with(true,false).
- cTSWin::SetBufferToDrawTo505f94..505fd0 walks the parent chain to the nearest
  private buffer for children without their own buffer. Recursive routing is
  in505e70..505f1c (`window-buffer-routing.txt`).
- cTSWin::Plot first paints itself(506890..506898), then calls visible children's
  Plot(5069d8..506a10), then PostChildDraw(506a68..506a70). Only afterward does
  it invoke BlitPrivateBufferToParent(506c0c..506c1c), vtable+d8→506200.
- That final blit calls GetOpacity(506220..506228) and the transparent-window
  branch passes the opacity byte with the complete private surface to the
  compositor(5062a0..5062c0; `window-buffer-blit.txt`).

Therefore a port that paints translucent background but opaque body/button
children is incomplete. Setting every child's alpha200 independently also
is not algebraically the same: text then blends over an already translucent
background. The faithful operation paints the dialog contents at full opacity
into its own surface, then composites the complete surface once at200/255.
This finding is limited to the new tutorial GUID transparency scope; it does
not request a global UIElement opacity rewrite.

## Final focused-package capture review

Independently inspected all five PNGs in the owner's
`Documents/Simitone/ui-audit/r241/`: tutorial-open, opaque-reference, icon,
opening, closing. These captures are1024x768. Native icon/closebox are visible,
body fits without clipping, title is suppressed, and the three-outline
transitions are continuous. Pixel comparison performed separately in Python
against `opaqueReference*(200/255)+[45,65,45]*(55/255)` gives maximum absolute
RGB error0.491 byte and zero pixels with error above2. This confirms group
opacity over the supplied backdrop, including body and button pixels.

Final source inspection confirms real top-dialog key ownership, Space disabled
for secondary-button dialogs, immediate modal unregistration, LastTarget
interrupted-animation source, minimum-width-aware tutorial search, and native
missing-image hit area/fallback. The reported UI-Update versus native Draw
one-frame scheduling accommodation remains explicit and accepted as a bounded
port adaptation; it is not concealed as exact scheduling parity.

A final resource issue was reported to root: TutorialBuffers retains every
prior full-viewport target until close. Repeated live resize of a persistent
lesson can therefore accumulate hundreds of megabytes. Keeping a currently
bound predecessor alive is necessary, retaining all historical sizes is not.
A bounded retirement/pool correction is needed before this source review can
unconditionally sign off. Root owns that correction and its focused regression.
No new product edit, build, or game launch was performed by this reviewer.

## Buffer-retirement correction review

The final retirement correction closes the outstanding resource issue by
source inspection. PrepareTutorialComposite captures its incoming render
bindings after pausing the outer batch, retains the current target and any
older target referenced by the saved texture0/render-target bindings, and
disposes other historical sizes. The saved bindings restored in finally
therefore remain alive; unrelated intermediate resize targets no longer
accumulate. The focused fixture now changes viewport eight times and asserts
at most two retained targets in the normal tutorial UI path.

**Skeptical source/visual signoff for the bounded tutorial presentation scope:**
no remaining high-confidence release blocker found. Earlier capture/pixel
validation remains valid because retirement does not alter drawing geometry
or colors. Root is running final rebuilt-package focused/full/size validation;
this signoff does not claim those pending reruns were performed by this
reviewer, nor does it claim complete guided-lesson/event-highlighting parity.

## Fractional-DPI fixture diagnosis

The first1600x1022 run at effective1.7033333 failed the alpha comparison for
12620 pixels. Independent inspection found the errors concentrated in glyph
and frame detail, with flat interior alpha error only0.294 byte. The opaque
reference image was visibly point sampled; the composite image was linearly
sampled, with identical placement.

Source confirms the mismatch belongs to the fixture: opaque capture explicitly
used Begin(Deferred,AlphaBlend,PointClamp), while the new private composition
uses UIBegin(AlphaBlend,Immediate). Production UILayer493/501 also uses UIBegin;
UISpriteBatch139 calls Begin without a sampler and mounted FSOMonoGame
SpriteBatch94 defaults it to LinearClamp. The faithful comparison must use
the same production sampling path on both sides. A fixture-only correction to
UIBegin is justified; no production rendering change is indicated. Matching-
sampler fractional rerun is pending with root at this review point.

## Fractional rerun closure

The matching-sampler rerun is independently confirmed. The stable archived
captures in `build/ui-audit/r241/800x600@2x/` are1600x1022; after production
UIBegin sampling is used for both paths, the maximum alpha-equation RGB error
is0.491 byte and zero pixels exceed2 bytes. Both images were visually inspected:
geometry aligns, frame/text sampling matches, body and closebox remain visible,
and no clipping was found. The adjacent size log finishes22 passed/0 failed
at01:27:41.922. The mutable Documents output had already been overwritten by
another1024x768 run, so this check deliberately uses the archived fractional
pair rather than attributing those later1x images to the fractional run.

The fractional failure was a test reference sampling mismatch. This closes
that review item without a production change. Bounded tutorial presentation
source/visual signoff remains valid; root continues the packaged regression.
