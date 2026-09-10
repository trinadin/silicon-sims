# CAS editor follow-up: independent skeptical review

Read-only code/binary review except for this report. The initial production diff
replaces only the desktop biography editor with UIOriginalTextEdit. Name and
family-name controls remain inherited. Tutorial changes were not reviewed or
modified. The root task owns build/runtime/visual validation.

## Migration assessment

The biography requests the same native editor class, font slot 10, transparent
background and unlimited lines as the restored Scrapbook editor. Capacity is
2048 and white ink, correctly retained by the initial migration. LoadCaption
loads variablesans_10.ffn; the inherited TextStyle size 10 selected that same
face. CurrentText callers were changed to Text without changing the persistence
path. The new setter additionally enforces capacity immediately, resets its
caret/viewport, and clears undo history when another Sim is loaded; native
SetText also resets viewport/selection before replacing text (0x52fbec..fc38).
No concrete migration API or capacity blocker was found in that diff.

The initial zero-inset migration does **not** reproduce the native placement
exactly. Fresh Capstone reads of the owner's original PPC executable identify
the following geometry. Addresses below are raw file offsets, not runtime PCs.
Original SHA-256 is 33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f.

## Native placement and wrapping

- SetArea 0x5330f4..533108 writes the text rectangle at +108 as
  `(5,0,width,height)`. Init 0x534f54..534f8c initializes the same rectangle
  if it has not already been set.
- Init 0x534fb0..534fcc sets visible row count at +120 to
  `max(1, height / font.TSCharHeight())`.
- For linesAllowed other than 1, Init 0x534ff4..535008 writes wrap width
  at +f8 as `textRect.Width()-5`, therefore **width-10**, and +fc=32767.
  The scrollbar hide/show paths repeat that width law at 0x53053c..530550
  and 0x53063c..530650.
- Rebuild writes each row origin as `(5, rowIndex*fontHeight)` at
  0x530a68..530a8c. Empty rows also use x=5 at 0x530780..53079c.
- Init 0x53507c..535094 sets +f4 to
  `(height-visibleRows*fontHeight)/2`, truncating toward zero.
  Scroll at 0x5317c4..5317d0 sets viewport y at +f0 to
  `f4-firstVisibleRow*fontHeight`.
- Paint 0x534700..534728 adds control origin, viewport offset (+ec/+f0),
  and row origin. Thus the 761x96 biography uses **x=5, wrap width=751**.
  The recovered English font10 runtime height is 19
  (`../r238-budget/recovered-font-runtime.txt`), giving five complete rows
  and a zero vertical offset. Its text begins at screen x=27, y=425 before
  the individual glyph's normalized vertical offset.

This evidence permits a CAS-local inset correction while keeping the existing
outer 761x96 hit rectangle. It should not silently change Scrapbook placement
as a side effect. Wrap, hit testing, clipping and caret x must use the same
inset law; moving rendered glyphs alone would leave mouse selection misaligned.

## Input integration risks

UIContainer.Update updates every child even when its parent is invisible.
UIOriginalTextEdit.Update checks only its own Visible. CAS transitions hide the
DesktopCAS ancestor without clearing biography focus. ShowConfirmation likewise
has no explicit editor blur/disable. The inherited control already had a similar
exposure, but the migrated biography should be tested for hidden-screen and
modal input isolation. A CAS-local blur/disable guard is appropriate; scope it
to this screen so Scrapbook behavior is not changed by this pass.

The new mounted-field test exercises active-endpoint arrow movement and
capacity-preserving replacement. Full integration should also verify returning
to another Sim resets the draft/undo state and that copy/cut/paste remains
available through the existing shared input adapter. This report does not
claim those runtime checks have passed.

## Why single-line names require a separate change

- Init 0x534fd0..534fec sets single-line wrap width to **32767**, not the
  displayed width. A wrapped editor with LF removed is therefore insufficient;
  horizontal viewport behavior must be recovered and implemented.
- TSOnCharacter 0x5336f4..533700 normalizes CR to LF. At
  0x533708..533744, LF with linesAllowed=1 sends parent command 0x17 and
  returns without insertion. Other control characters below space, except LF,
  are rejected at 0x533748..53375c. Typed Enter is not merely stripped text.
- Paste uses InsertText directly; InsertText 0x5324a0..53270c has no
  linesAllowed check. Do not assume typed and pasted newline policies match.
- The native single-row vertical offset for height25/fontHeight19 is 3.
  Existing inherited name margins are 2 pixels, so a replacement has a real
  placement change in addition to new navigation behavior.
- R143 Init sites explicitly focus the first-name and family-name editors.
  The current CAS controller contains no equivalent focus assignment.
- Name enables flash-on-empty but disables focused-frame drawing; family
  enables flash-on-empty and retains the default focus-frame behavior. The
  Scrapbook editor implements neither frame option.
- Preserve R240's exact 20 forbidden typed family-name characters and its
  paste/programmatic bypass. Rejection must preserve the selected text.

The migration improves biography editing; it is not certification of complete
CAS cTSWinTextEdit2 parity. Remaining native undo coalescing, double-click
system timing, scrollbar presentation and multibyte behavior also require
separate verification.

## Follow-up implementation review

Reviewed the optional horizontalInset implementation, CAS opt-in value5,
controller blur calls, and mounted CAS239/CASFlow test changes. Rendering,
selection fill, hit testing and caret share the same inset. Wrap uses width-10
while the clip rectangle retains the native left5/rightWidth bounds. The
optional default0 preserves existing Scrapbook callers.

The capacity fixture is correct: a full2048-character buffer replaces two
selected characters with the first two of five incoming characters and retains
its existing XY suffix. The focus fixtures call Update before invoking the
controller, so the editor has acquired the private InputManager needed for
Blur to clear the manager's focus as well as IsFocused. The six-paragraph flow
fixture now exceeds the biography's five visible rows.

One transition race was reported to root: SetMode's immediate Blur leaves the
outgoing editor visible for part of the one-second transition. A click during
that interval can refocus it before the ancestor hides. Blur should also occur
at the actual FamilySimInterp visibility boundary, or the outgoing editor
should be disabled immediately. A test should refocus between SetMode and
that boundary. Apart from this race, no concrete blocker was found in the
reviewed change. Build/runtime/visual results remain root-owned.

Root follow-up: implemented the requested visibility-boundary blur in
FamilySimInterp and added a regression that refocuses during the outgoing
transition before hiding the panel. Focused CAS239 and shared Scrapbook
runtime checks passed with this correction.

## Transition race correction

The actual visibility boundary now calls BioEdit.Blur when FamilySimInterp
hides DesktopCAS. The added fixture refocuses the biography after SetMode,
advances FamilySimInterp to0, and verifies no pending typed text is applied.
This closes the reported transition race in the reviewed code and exercises
the intended interleaving. Root reports the correction tested; root retains
ownership of the final integrated suite result.

## Preview capture triage

Inspected casflow-03-characteredit-preview.png. Its 100x220 aperture shows
floor/scenery and only part of a person at the lower edge; multiple carousel
bodies are visible outside the fixed800x600 panel. This is **not safely
dismissible as a capture artifact**.

TS1CASScreen.Update's desktop SimEdit branch assigns VitaPreview=BodyAvatars[0],
moves all carousel avatars away, then sets that preview's VisualPosition to
VitaWorldPos and its direction to VitaFacing. Later in the *same Update*, the
unconditional BodyAvatars loop overwrites every body's position/direction with
the mobile circular carousel layout, including VitaPreview. The adjacent head
loop alone has the `if (!Original)` guard. This is a concrete production path
that contradicts the dedicated desktop preview, independent of screenshot
render-target behavior.

AutotestCASFlow.Capture warms UI caches, calls Scenes.PreDraw/Draw, then draws
the current UI into a render target. It does not assign avatar positions or
camera targets. Its warm draw/target handling can still warrant comparison
with an ordinary presented frame, but cannot explain away the production
position overwrite.

Next bounded correction: restrict the body carousel positioning to the mobile
path, matching the head loop. Verify after a complete desktop Update that
VitaPreview retains its dedicated position/direction and every other carousel
body stays hidden, then inspect a fresh normal-frame and capture at800x600 and
a larger viewport. Do not claim that guard alone proves framing: the existing
VitaCenterTile/VitaZoom values are explicitly calibrated approximations, and
the fixed top-left aperture may still need resolution-aware camera work.
No preview code was changed by this review.

## Preview coordinate convention

A second source-level defect is supported by the rendering chain:
VMAvatar.VisualPosition (tso.simantics/Entities/VMAvatar.cs150..153) forwards
its value directly to WorldUI.Position. AvatarComponent.Position stores it
unchanged and uses Z as altitude; Draw (AvatarComponent.cs222..223) converts
it with WorldSpace.GetWorldFromTile. WorldState.cs483 defines three world
units per tile, and GetWorldFromTile684..686 maps tile(X,Y,Z) to
world(3X,3Z,3Y).

Therefore assigning VitaWorldPos=(90,0,58) directly to VisualPosition produces
world(270,174,0), far above/away from the preview camera. If that constant is
intended as world(90,0,58), the correct stored coordinate is tile(30,58/3,0),
subject to the component's terrain-altitude offset. This inverse transform is
proven by code. The *intention* behind the constant is strong inference from
its name and exact match with the last Mode2D world-position vector; it is not
recovered native evidence. R143's report explicitly labels the Vita constants
as first-cut calibration needing live verification. Correcting units is
justified but does not certify final screen framing or the original camera.
