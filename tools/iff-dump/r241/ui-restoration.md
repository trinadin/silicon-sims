# R241: original tutorial presentation and lesson recall

This round continues the owner's approved base-game UI/UX restoration in the
maintained Simitone and FreeSO forks. Original executables, resources and saves
are read-only inputs. Proprietary image captures remain local and uncommitted.

## Restored behavior

- The current tutorial owner's private BMP300 supplies the native icon, at a
  one-pixel inset from the view's upper-right corner. The base-game icon is 45×45.
  A missing bitmap retains the original 45×45 clickable outline fallback.
- Tutorial-owned dialogs use the original quadrant/corner positioning rule and
  remember the preceding dialog rectangle. Opening and closing draw three
  black/white/black outlines with 500 ms linear edge interpolation. Replacing an
  open or opening lesson follows the shrink-then-expand path. The real dialog
  remains hidden during expansion; closing immediately unregisters its window
  and modal blocker before the independent shrink animation.
- Only Tutorial GUID 0xc3249a1d suppresses its title and uses opacity 200/255.
  The complete dialog, including text/buttons, is rendered opaque into a private
  buffer and composited once. Other tutorial owners retain their title/opacity.
- Nonmodal tutorial dialogs replace the primary bottom button with original
  closebox 31 for owners, 30 for unowned type 4. The natural-sized button is inset
  five pixels from the upper-right edge. Modal dialogs retain the primary bottom
  button. Short lesson dialogs use the recovered width-halving/bracketing search;
  the tutorial path also honors the original alternate 1.2 aspect target.
- Enter answers the primary action; Escape answers the last action. Space binds
  the primary action only without secondary buttons and without editor focus.
  The actual top registered dialog owns keys before world shortcuts run.
- Clicking the idle tutorial icon executes the owner's private named behavior
  `show situation info`. It preserves the running main stack and pending dialogs;
  the original lesson script decides what to redisplay. It does not reopen a
  cached message or fabricate a dialog response.
- Original opcode 49 only wakes a target currently executing opcode 0/17. Other
  instructions do not retain an interrupt that could skip a later wait. A TS1
  sleep consumes a wake before decrementing or cycling the RNG. TSO retains its
  previous behavior.

## Scope and independent review

The tutorial presenter and opt-in dialog paths contain the presentation changes.
Shared ordinary-dialog defaults and touch presentation remain intact. The shared
UI layer gains a read-only top-visible-dialog query for tutorial key routing.
The new engine command resolves the owner at execution time. No save format or
JIT version change is required for the restored valid tutorial action.

The skeptic independently recovered geometry, opacity, keyboard and animation
rules from executable instructions, resource metadata and resolved vtable calls.
Review caught the misleading apparent width200 call (actually opacity), modal
cleanup, secondary-button Space behavior, key ownership under other dialogs,
interrupted replacement geometry and group compositing. See
[presentation contract](../r241-tutorial-evidence/contract.md) and
[engine action law](../r241-tutorial-action/restoration-law.md).

The engine tests use the real original BHAV 4099 and 4146 in an isolated VM. UI
tests use an isolated unticked VM with the production dialog sink and actual
GPU drawing. They cover resource geometry, ownership/GUID distinctions, anchors,
transition boundaries/replacement, keys, modal cleanup and pixel comparisons of
the single opacity composite. They preserve the active household and its dialogs.

## Validation

The final self-contained arm64 package passes **137/137 default regression
checks**, **14/14 focused checks**, and **22/22 checks in each of three size
runs**, with no skipped checks and clean Run/Dispose. The default wrapper exits
zero. `validation.json` records the results and verifies every retained capture
against its SHA256 manifest. Committed text logs have trailing whitespace removed.

| Requested window / scale | Actual logical size | Physical viewport | Effective scale | Checks |
| --- | --- | --- | --- | --- |
| 800×600 / 1 | 800×600 | 800×600 | 1 | 22/22 |
| 1280×800 / 1 | 1280×800 | 1280×800 | 1 | 22/22 |
| 800×600 / 2 | 939×600 | 1600×1022 | 1.7033333 | 22/22 |

The last row is the host's clamped fractional-DPI configuration, not an exact
2× result. The final default run retains 68 captures; each size run retains
57 UI captures, 17 PIP renders and five tutorial renders (305 images across the
default and size runs). Images remain owner-local under `build/ui-audit/r241`.
Visual review covers tutorial open/outline/icon states, anchoring and the retained
Needs correction. Broad survey captures alone do not certify every original
interaction or the real Create-a-Sim flow.

Settings were restored byte-for-byte (`config-restoration.json`). All nine
published/package assembly hashes match (`package-assemblies.json`), including
FSO.UI. The owner-local original executable still matches the prior input hash
(`original-input.json`). The updated app is `dist/The Sims-arm64.app`.

`validate.py` and `run-ui-sizes.py` run serially and restore settings bytes even
on failure. Early failed logs are diagnostic history, not final validation.
The first fractional-DPI run exposed a fixture sampler mismatch: the opaque
reference used PointClamp while the production UI and private composite use
LinearClamp. Independent pixel review found the differences on glyph/frame
detail, with correct flat-color alpha. The capture now uses the production
UIBegin path for both images; no production rendering change was made for this
fixture correction. `size-800x600@2x-pre-sampler.log` retains that failed run.
The corrected fractional rerun passes all 22 checks. Independent pixel review
of the archived 1600×1022 captures found a maximum alpha-equation error of
0.491 byte and zero pixels outside the two-byte tolerance.

## Remaining parity work

This restores tutorial presentation and lesson recall, not the entire guided
tutorial. UI-event polling, control highlighting, tutorial-family setup/reset
and completion calls still need original-backed implementation and end-to-end
lesson testing. Ordinary text wrapping retains the existing font/wrapping model.
The 500 ms animation phases run in the port's UI update cycle, whereas the original
prepares/samples them during view drawing; the transition between phases can
differ by one render frame. Original compiled primitive-error propagation remains
an engine limitation for invalid targets, separate from the verified valid action.

Camera cutaway/fades, original OBJM event continuation import and broader
non-tutorial UI audits are reserved for the optional other chat. The coordinator
and three non-overlapping assignments are in [helper-prompts.md](helper-prompts.md).
The broader UI helper has reported a premultiplied-alpha defect in the CAS
preview cutout, a wide-window toolbar question requiring more original evidence,
and survey contamination requiring a clean production-flow reproduction. These
are follow-up findings, not changes in this package. The camera and OBJM audits
are still producing their handoffs.

Those helpers own evidence directories only; this chat owns implementation,
integration and runtime validation. Their work is not a dependency of this round.
