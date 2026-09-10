# R237 — Base-game desktop UI restoration review

Status: assessment and proposed patch; product layout changes await the owner's
scoped approval under the supplied global UI/UX protection instructions.

## Confirmed defect: the extra blue LIVE background

The user's screenshot is reproduced by the current R236 package. At 1024×768,
the extra layer occupies screen (220,618)–(820,768), including 50 pixels above
the normal toolbar top at y=668. It is `UIMainPanel.TallSubpanel`, mounted in
R193 as a 600×150 image at MainPanel-local (0,-50).

The original executable contradicts R193's interpretation:

- `CPState::IsDoubleByteUI` at 0x20c670 returns true for language IDs
  15,17,18,19,20 (Japanese, Simplified/Traditional Chinese, Thai, Korean).
- Init calls it at 0x270230. `beq 0x270484` at 0x270238 skips the tall
  resource load when false. English must not load this backdrop.
- Paint independently repeats the condition at 0x26f62c/0x26f634 and then
  checks the People window's visibility. It offsets the double-byte backdrop
  by 520 pixels at 0x26f6ac, not the port's 220-pixel screen origin.
- The older R144 toolbar law correctly recorded the double-byte-only condition.
  R193 inverted it in prose, implementation, and its regression assertion.

Fresh instruction decode: `original-tall-background-decode.txt`, produced from
the owner's executable with Capstone 5.0.7. No game assets are added here.

## Proposed first correction and complete impact

Apply `proposed-tall-background.patch`: recover the original language predicate
and condition the tall-image construction on it. English and the other
single-byte layouts then expose the existing native 804×100 PanelBack.

This affects every desktop LIVE category because they share one MainPanel:
Needs, adult Job, child Report Card, Personality, Relationships, House, and
the Complete Collection's Interests/Inventory tabs. At 1024×768 the extra
600×50 upper strip disappears; the lower 600×100 region uses PanelBack
instead of being overpainted by TallSubPanel. No control coordinates, fonts,
gauge sizes, portrait sizes, click targets, or transition logic are changed.
BUY, BUILD, CAMERA, OPTIONS, CAS, neighborhood, and mobile layout code are
outside this patch. Single-byte behavior at other window sizes changes by
the same missing layer; native PanelBack clipping remains in force.

The existing double-byte placement remains a known approximation in this
small correction. Exact double-byte composition also involves buffer cropping
and alpha, so merely moving its image to x=520 would not establish parity.

Required verification after approval: retain all art-dimension checks;
replace the incorrect unconditional-mount expectation with an independent
256-value language truth table, absence in single-byte desktop state, native
PanelBack dimensions, and correct mode visibility where a tall layer exists.
Capture LIVE categories before/after with the production renderer; check the
removed upper strip and unchanged controls; publish, package, run focused and
default checks, and compare package/publish DLL hashes.

## Assessment boundaries

The owned original reference is The Sims Complete. Base-game surfaces can be
audited against its code, but this does not establish that every 2000 retail
visual or expansion-era addition matches the unexpanded release. Existing
expansion tabs and neighborhood controls are not implicitly removed.

The previous `uitotal` check validates texture provenance on the LIVE tree
and a paused state, not pixel equivalence. It permits original art in the
wrong location. The R193 `uismall` check explicitly requires the defective
mount, so passing the current suite is not evidence that this UI is original.

## Wider findings, in restoration order

### 1. LIVE background: confirmed, patch prepared

Covered above. This is the screenshot defect and the smallest evidence-backed
restoration. Approval is pending; production source has not been edited.

### 2. Budget: confirmed layout and caption failures

`uisurvey-audit-budget.png` shows the empty blue OK button covering expense
rows and both column headers overlapping. `UIOriginalBudgetDialog` explains
all three: its button is centered vertically at y=274, its value-column
widths exclude the widths of "Today" and "3 Days", and its navbar button
receives a tooltip but no rendered caption. `UpdatePosition` also scales
the entire 800×600 board by GraphicsHeight/600 (1.28 at 1024×768), enlarging
its typography relative to the native toolbar.

The original instruction sequence disproves the recorded placement law.
The initial button layout at 0x263fdc–0x264034 halves **only X**; the final
layout at 0x264c18–0x264c88 places it centered horizontally in the footer:
`x=(windowWidth-buttonWidth)/2`,
`y=windowHeight-footerHeight+(footerHeight-buttonHeight)/2`.
The window is resized from measured rows at 0x264bd4–0x264c10. The initial
SetArea at 0x263f10–0x263f24 creates a 100×100 starting rect; it does not
grow an 800×600 board by 100. Resource 3008 is loaded at 0x263efc; the
existing TSPaint decode uses its thirds for the frame. This calls for
restoring the whole measured dialog composition, not simply moving its
button down on the current fixed board. The current `uibudget` assertion
pins the incorrect y=274 and must be replaced along with that implementation.

Proposed next scope: Budget only, including measured header/value columns,
caption, native frame, final footer placement, and scaling. Recover the
remaining font/row/footer inputs before approving exact replacement geometry.
Its estimated financial figures and missing historical accumulators remain
a separate behavioral limitation.

### 3. Help: confirmed text collisions, tile seams, unlabeled close control

`uisurvey-audit-help.png` shows long topic names overlapping the right pane,
tightly overlapping body lines, visible grid seams through the tiled frame,
and an empty close button. Source uses a default font with fixed 18px list
and 14px body pitches, a fixed 160px topic column, no topic text clipping,
and GraphicsHeight/600 scaling. The custom Draw paints the close button's
sheet but never paints its caption. The pane geometry is explicitly
disclosed as invented in `UIOriginalHelpDialog`; original art alone does
not validate it. The observed seams require checking scaled tile placement
before changing shared `UIOriginalDialogChrome`, which also serves other
dialogs and lot popups.

Proposed next scope: Help's measured text layout, close caption, and native
scale, after recovering the original list widget's geometry. Any shared
tile-rendering change needs separate all-consumer validation. The tutorial
lesson/highlight system is still absent; Help is not a substitute for it.

### 4. Phonebook: confirmed unreadable and overlapping list composition

`uisurvey-audit-phonebook.png` shows dark text on the dark board, with rows
above the authored list wells. The selected-family picker capture additionally
shows list text painting over the picker. Source paints RGB(0,0,57) without
a column backing, uses 10px rows with an assumed caption font, and adds
member-row children after the popup child. The popup therefore loses sibling
paint priority when `SelectFamily` rebuilds the members. It also retains all
picker rows without a viewport; static columns stop at 19/22 rows, with no
scroll implementation. Constructor/census tests do not exercise this visual
composition or overflow.

Proposed next scope: recover the complete original list-control background,
font, row origin and scrolling contract; then compose a topmost picker and
prove reachability for long lists. Do not guess a brighter text color or
new row spacing from the screenshot alone.

### 5. Options: repaint coverage needs interactive confirmation

The immediate Graphics→Sound→Play capture initially repeated the graphics
bitmap despite `ShowScreen` changing the child tree. `UIOriginalOptionsPanel`
inherits a cached container; Add/Remove and ForceState assignments do not
themselves guarantee cache invalidation. A real pointer event can invalidate
the cache, so this is a diagnostic finding, not yet a proved normal-click
failure. The opt-in audit keeps pre-refresh captures and explicitly invalidates
the cache for separate captures of each requested screen. Restoration must
validate click and keyboard transitions across real frames before changing
the shared cache behavior.

### 6. Camera/Scrapbook and other interaction residuals

The native camera toolbar and empty scrapbook board are visibly composed.
Scrapbook captions are `UIOriginalParagraph` display text, not editable input,
although the original `SetDescription` path is already identified in R190.
PNG persistence, the picture-in-picture view, and frame-highlight differences
remain disclosed. People-pie label placement, the view-pie tear-off gesture,
queue easing, and modern edit-box text are also approximations; they require
original-runtime interaction comparisons, not an art-provenance check.

## Surface coverage and limits

| Surface | Evidence inspected | Result / next validation |
| --- | --- | --- |
| Needs, Job, Report Card, Personality, Relationships, House | Fresh production-rendered baseline states and popups; R144/R193 source/decode | Shared tall backdrop defect confirmed. Contents remain within the 100px band in these captures. |
| Interests/Inventory | Source and baseline capture inventory | Same shared backdrop path; expansion behavior remains outside the base-game work. |
| UCP controls, portrait cells, gauges | Full LIVE/BUY/BUILD/Camera captures; existing geometry checks | Preserve the native control geometry. Baseline uses synthetic test state, so incidental values/floaters are not gameplay evidence. |
| Buy catalog and description | Fresh catalog, hover-card, selected-description captures | Single-cell art and wrapped popup composition observed; original popup paint and x clamp still disclosed. |
| Build, terrain, walls/water, roof | Fresh ordinary/terrain/roof visual inspection; other mode captures retained | No additional gross sizing defect established in inspected states. Cursor/drag/cancel workflows still need interactive assessment. |
| Options | Supplemental graphics/sound/play captures and cache source trace | Separate state repaint from layout; confirm transitions through real input. |
| Budget | Fresh dialog capture and original Init/TSPaint | Multiple confirmed failures; earlier placement interpretation wrong. |
| Help | Fresh dialog capture and source | Confirmed text overlap, seams, blank close control; original pane layout unresolved. |
| Phonebook | Fresh unselected/selected picker captures and source | Confirmed visibility/stacking failures and missing long-list handling. |
| Camera/Scrapbook | Toolbar and empty album capture, source, R190 decode | Board present; caption editing and other disclosed behavior remain unfinished. |
| Object/system dialogs and tooltips | Fresh picture-dialog and tooltip artifacts; existing tests | The inspected picture dialog wraps and fits. This does not validate every string length/locale. |
| Neighborhood | Fresh 1024×768 capture and existing navbar laws | Native centered 800×600 artboard observed; Complete Collection art/control set is not a retail base-game-only reference. |
| Create Sim / Create Family / family picker | Fresh harness layout fixtures and production source | Fixtures are not live end-to-end CAS: blank preview/family content cannot be declared a product regression from these captures. Actual avatar/name/bio/save/cancel interaction remains to verify. |
| Loading, speech, cheats, pies, focus/keyboard | Existing checks and source/residual review | No comprehensive real-input or original-runtime visual comparison in this round; no claim of full parity. |

All fresh captures in this round are 1024×768. Native 800×600, wider windows,
Retina scaling, resize transitions, localized text, populated albums, long
phonebooks, and full input workflows remain explicit validation gaps.

## Verification record

- Starting maintained fork: `fd9d5ed` (R236), branch `mac-port`.
- Unchanged packaged default: **129 passed, 0 failed, 0 skipped**, wrapper
  exit 0, clean `after Run` and `after Dispose`, 2026-09-04 20:16:52 CDT.
- Preserved 44 baseline images in ignored `build/ui-audit/r237/baseline/`;
  their SHA-256 manifest is `baseline-images.sha256`.
- Added opt-in `uiaudit` capture tooling only. It does not invoke Save, Call,
  Delete, or settings-action callbacks and restores the previous MainPanel
  state. It captures dialogs through their production constructors and modal
  mounting path. `corpus` must be included because it runs the UI checks.
- Supplemental self-contained publish was successful. The initial effective
  `lot,corpus,uiaudit` run captured eight states and passed **7/0**, clean exit.
  A preceding `lot,uiaudit` attempt ran only `lot`; it is not accepted as
  capture evidence. The final tooling additionally retains three pre-refresh
  Options diagnostics (11 artifacts total).
- Final exact-tooling `lot,corpus,uiaudit`: **7 passed, 0 failed, 0 skipped**,
  exit 0 and clean Run/Dispose at 20:22:56 CDT. All 11 images were preserved
  under `build/ui-audit/r237/supplement/` with `supplement-images.sha256`.
  Refreshed Graphics/Sound/Play images show their distinct expected controls;
  the immediate stale-cache captures remain a separate diagnostic. This is
  a publish-output test, not a newly packaged/default-suite certification.
- Logs: `baseline-default.log` and `supplement-final.log`. Source and prose
  whitespace checks pass; verbatim logs retain emitted trailing spaces and
  the proposed patch retains required blank context prefixes. The draft
  background patch passes `git apply --check` but has not
  been applied, compiled, or visually verified after application.
- Product UI source, upstream mirrors, engine submodules, installed dist,
  and original game data are unchanged by this assessment. Existing nested
  FSOMonoGame work and the two untracked R202 probe binaries are preserved.

The pending approval concerns only the first background correction. The
additional findings require their own recovered geometry and reviewed scope.

## Local visual evidence

These links point to ignored, owner-local captures, not distributed assets:

- [LIVE baseline](../../../build/ui-audit/r237/baseline/uisurvey-live.png)
- [Budget](../../../build/ui-audit/r237/supplement/uisurvey-audit-budget.png)
- [Help](../../../build/ui-audit/r237/supplement/uisurvey-audit-help.png)
- [Phonebook picker](../../../build/ui-audit/r237/supplement/uisurvey-audit-phonebook-picker.png)
- [Options sound](../../../build/ui-audit/r237/supplement/uisurvey-audit-options-sound.png)
- [Scrapbook](../../../build/ui-audit/r237/supplement/uisurvey-audit-scrapbook.png)
