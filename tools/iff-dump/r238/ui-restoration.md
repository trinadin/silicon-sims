# R238 — Approved base-game desktop UI restoration

The owner approved the R237 findings and requested skeptical collaboration.
Changes belong to maintained `simitone-fork` (`mac-port`), not upstream copies,
package caches, original game data or generated distributions. Source baseline:
R237 `da3abf1`. Generated captures are owner-local and ignored.

## Restored composition and interaction

- LIVE: the tall backdrop mounts only for the original double-byte language set
  {15,17,18,19,20}. English and other single-byte Needs/Job/Report Card/
  Personality/Relationships/House tabs use the existing native 804×100 PanelBack.
  The extra 600×50 upper strip is gone; child control positions are unchanged.
- Budget: reconstructed the original 20-row table, font-height stacking, 12px
  indent units, measured three-column spacing including both headers, centered
  totals, PopupInfoTiles frame and 100px footer. Native 217×52 OK now has its
  original 20-point caption and state offsets. The English fixture is 470×528.
- Help: identified the original TextList, measured 47 topic titles, 12 visible
  rows with font 12 height+3, 340px body pane, system font 11 and 100×33 OK.
  Native English composition 579×398; text clips to its own panes. Scrolling
  no longer forces the old selection back into view. Enter/Escape dismiss.
- Phonebook: corrected the class identity from Text to TextList. Two 280px
  lists show 10 rows of 26px, with original navy background, original state ink,
  native WinScrol scrolling and clipped rows. Restored board (17,8), target
  label (75,16), natural 652×426 frame, opposite-corner Call/Cancel and selected
  neighbor portrait. Removed the invented dropdown. All families/members can
  be reached instead of truncating the census. First family/member auto-select.
- Options: local cache invalidation after replacing section children or
  resetting section state. Graphics/Sound/Play transitions and production
  button callbacks now repaint immediately, with no control geometry change.
- Scrapbook: native transparent 520×72 multiline caption editor at (90,435),
  font 10, 4096-character capacity. Editing uses original glyph metrics and
  supports keyboard text, selection, clipboard, undo/redo and scrolling.
  Navigation commits outgoing captions; Done saves; reopening reads saved
  captions. Delete confirmation suspends editor input; original No behavior
  reloads the stored caption, discarding the draft without saving. Corrected hidden filename label to its original (100,415) origin and
  450x20 area. Native 700×560 board recenters on resize.
- Shared four-state sheet buttons: original CalcRowCol keeps active+hover on
  state 1. The former state 3 assertion confused the six-column branch with
  four-column sheets and showed the disabled image while pressed. Affects
  Buy plaques/back, Build tools/undo/redo/roof, Camera, Scrapbook and
  relationship/interest pagers, relationship sort plaques and LIVE category
  tabs. No button dimensions or hit regions change.
- Budget/Help/Phonebook/Scrapbook captions use the original capital-A ink
  centering and pressed offsets, rather than a fixed 13px baseline or full
  line-height centering. Normal/hover/pressed/disabled colors are decoded.

- Shared text: recovered InitBitmapped runtime top-bearing normalization and
  applied it to all nine drawing paths, including the default vector bridge.
  Raw FFN metadata, atlas samples, advances and line heights remain unchanged.
  Removed prior local tooltip/neighborhood-number compensations to preserve
  their already-correct final ink. Independent GPU fixtures verify 30 complete
  rendered alpha hashes across 15 faces, including negative-top glyphs.

- Desktop viewport fitting: macOS may clamp the requested physical window.
  The runtime now limits the uniform UI scale to preserve an 800×600 logical
  canvas at startup and resize. The saved preference is retained and takes
  effect fully when space permits. Native control rectangles remain unchanged.
  Saved dimensions are separate from fitted runtime dimensions, preventing
  Options saves from making the next launch progressively larger. Explicit
  resizing still remembers physical window size using the requested DPI.
  This is a Mac viewport adaptation, not a recovered original scaling feature.
- Fractional-scale chrome: shared tiled popup frames now snap both ends of each
  tile to the same physical pixel boundary. This removes gaps between tiles
  while preserving native source clipping, opacity and all logical geometry.
  The change covers PopupInfoTiles dialogs and the neighborhood lot popup.
- Scaled tooltips keep the native 7-point font and scale its pixels and wrap
  metrics together, instead of selecting a different font at higher DPI.

## Independent evidence and rejected older assumptions

The skeptical agents re-decoded the owner's original executable rather than
accepting older prose or the test suite as the specification:

- [Budget rows and fonts](../r238-budget/budget-layout-law.md)
- [Help layout](../r238-budget/help-layout-law.md)
- [Phonebook and TextList](../r238-phonebook/corrected-law.md)
- [Scrapbook editor](../r238-scrapbook/corrected-law.md)
- [Shared font runtime and button-state law](../r238-budget/font-runtime-law.md)

Budget’s 20 means row count, not pixel pitch. STR#146 (40,100) is a final margin
and footer, not the row-list origin. Phonebook’s 10 means visible rows, not
pixel pitch; RGB (0,0,57) is a background, not text ink. The alleged picker is
a call-target label. Scrapbook’s (520,72) is editor size, not filename origin.
These findings supersede the corresponding R161/R162/R190/R192/R195 claims.

## Verification

The updated gates use independent original numeric fixtures, real production
constructors and input callbacks. `uiaudit` is still a visual capture tool,
not a claim of pixel equality with an original executable.

- Focused development run 1: 10 passed/1 failed; Phonebook test exposed the
  inherited no-op Size setter. Added explicit native Size/GetBounds.
- Focused development run 2: 12 passed/0 failed, clean Run/Dispose.
  Graphics/Sound/Play panel regions were RGB-pixel-identical across direct,
  click and explicit-refresh captures; the first world render can differ.
- Focused development run 3: 13 passed/0 failed, clean Run/Dispose. Includes
  native Budget/Help/Phonebook, language predicate, Options and Scrapbook
  typing/newlines/undo/redo/backwards copy/capacity/navigation/persistence.
- First normalized full run: 128 passed/1 failed. The sole failure was an old
  LIVE tab expectation of disabled frame 3 on active+hover; corrected to the
  independently recovered four-column frame 1.
- Full regression run after that correction: 129 passed/0 failed, clean
  Run/Dispose (`normalized-default.log`).
- Packaged focused run after modal-input and monotonic-clock fixes: 16 passed,
  0 failed, including 30 exact GPU fixtures and original Scrapbook cancel.
- The first size matrix passed its capture checks but visual review rejected
  the doubled-scale Scrapbook: macOS clamped the physical window to
  1600×1022, leaving only 800×511 logical pixels. This exposed a global
  viewport-fit defect. `before-fit-*` evidence preserves that failure.
- The first fitted matrix passed bounds checks but the skeptics rejected its
  fractional popup frames: separately rounded tiles exposed the world through
  one-pixel seams. `before-tile-*` evidence retains this intermediate defect.
  The shared painter now uses common snapped edges; a GPU gate checks full
  interior alpha coverage at 1×, 1.25×, 1.7033333× and 2×.
- The first tile GPU test omitted the UI `PreDraw` lifecycle, so the isolated
  test element still had an identity transform. Its failure is retained in
  `tile-test-missing-predraw.log`; the test now performs the production lifecycle.
  Both native 07-font tooltip checks (2× and 1.7×) passed in that run.
- Final packaged size matrix: 13 passed/0 failed per run, with 54 fresh
  captures at each setting. Actual physical sizes are 800×600, 1280×800 and
  1600×1022; the last uses effective scale 1.7033333 and a 939×600 logical
  canvas. All four restored dialog rectangles fit. Original config bytes were
  restored after the matrix. See `size-*.log` and image SHA-256 manifests.
- GPU coverage found zero uncovered interior pixels at all four tile scales.
  Config save/reload preserves requested dimensions/DPI and unrelated settings;
  explicit resolution changes still persist.
- `verify-ui-captures.py` compares RGB pixels within the actual Options panel,
  excluding the world and fractional outermost uncovered viewport pixel.
  Direct/click/refresh panel pixels match at all three settings. Native 1×
  Help and Budget crops are pixel-identical before/after tile snapping at
  both 800×600 and 1280×800.
- Final exact-package full default run: **129 passed, 0 failed, 0 skipped**,
  clean Run/Dispose and wrapper exit 0 at September 4, 2026, 21:43 CDT.
  See `final-default.log`. All 30 font GPU fixtures, four tile densities,
  native tooltip scaling, Scrapbook input/persistence and config roundtrip pass.
  Final independent visual acceptance is recorded in the linked skeptical reviews.

Scrapbook tests temporarily swap the in-memory album and use a unique temp
storage directory, restoring both and deleting fixtures in finally. Clipboard
checks use an in-memory substitute and restore the player's clipboard handler.
Phonebook visual fixtures temporarily supply known relationships in memory,
restore them in finally, and never place calls. Populated album captures use
synthetic textures and never save them into the player's album.

## Remaining limits

This is a restoration step, not a 100% UI parity certification. The owned
reference is The Sims Complete, so base-game surfaces can be recovered but
unexpanded 2000 retail visuals are not independently established.

Budget still uses the port's disclosed financial approximations rather than
original historical accumulators. Double-byte tall-background composition,
tutorial lessons/highlights, camera PIP/frame presentation/JPEG family albums,
people/view-pie gesture and animation details, generic modern edit-box metrics,
and exact wrapped-editor caret affinity remain open. Broader original-runtime
input comparisons, localized coverage and live end-to-end CAS remain
necessary. The shared raw-glyph baseline defect is corrected; generic vector/edit-box
layout metrics remain separate from that verified raster-placement law.

## Package and source ownership

The Apple Silicon app is generated at `dist/The Sims-arm64.app` from the
maintained forks. `package-assemblies.sha256` records byte-identical publish
and packaged Simitone.Client, FSO.UI and FSO.Common assemblies. Nested FreeSO
commit: `4453710b`. Packaging reports codesign skipped; no signing claim is made.
The pre-existing FSOMonoGame working changes and R202 probe binaries remain
untouched. No original game assets or screenshot pixels are added to Git.

The pre-existing startup splash-progress NullReferenceException still appears
in logs and is outside these restored UI changes. Passing checks and clean
shutdown do not mean the logs contain no existing warnings.
