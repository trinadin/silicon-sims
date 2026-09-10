# R240: original event views and Mac text workflows

This round continues the owner's approved base-game UI/UX restoration. Maintained
sources are the Simitone fork and its FreeSO fork. The original executable,
IFFs and image/font assets remain read-only local inputs. No proprietary
image captures are included in the source commit.

## Changes

- Opcode35 now follows the recovered prepare/dispatch/complete yields, signed
  literal/local timeout, unsigned operand indices, caller eligibility, caption
  substitution and option routing. A typed event connects it to the lot screen.
  Saved per-frame continuations use marshal version40; yielding compiled code uses JIT4.
- Picture-in-picture has the original100/200/300-pixel sizes, native zoom0..3,
  resource30 close button, original language-dependent position, caption font12
  and ink, native wrapping/clipping, timer ownership and close/recenter routes.
- An independent isometric renderer draws the live lot architecture, Sims,
  objects, headlines and particles. Its cameras, terrain/sprite/particle caches
  and render targets are separate. GPU bindings, shader values and PPX target
  state are restored before returning to the main view.
- Automatic PIP and main-camera photographs use the family album and native
  caption/quality paths. Main-camera crop coordinates stay physical at high DPI.
- Desktop CAS remembers head/body selections separately for all four age/gender
  types, clamps choices when skin catalogs change, and resets edit sessions.
  Family-name typing uses the exact native forbidden-byte set; native paste
  and programmatic assignments bypass that typing filter.
- A macOS SDL clipboard handler and native Command selection/copy/cut/paste
  shortcuts restore the shared text-field clipboard workflow.

## Independent evidence and review

See [primitive-law.md](../r240-pip/primitive-law.md) and
[CAS restoration review](../r240-cas/restoration-review.md). Derived local
instruction traces and reproducible scan scripts accompany those reports.
The skeptic rejected premature conclusions about synchronous opcode completion,
zoom0, caption ink, snapshot DPI, the close button's effective width and
render-target lifetimes. Those findings drove specific changes and checks.

## Validation

Final owner-local self-contained package: `dist/The Sims-arm64.app`,2.1GB.
The packaging script reports ad-hoc signing skipped; it is not notarized.
Eight compiled/package assembly SHA256 pairs match in `package-assemblies.json`.

| Run | Passed | Failed | Skipped | Fresh captures |
|---|---:|---:|---:|---|
| Focused final |13|0|0|17 PIP renders plus composite|
| Default final |135|0|0|46 UI views plus17 PIP renders|
|800x600, DPI1|20|0|0|57 UI views plus17 PIP renders|
|1280x800, DPI1|20|0|0|57 UI views plus17 PIP renders|
|Requested800x600, DPI2|20|0|0|57 UI views plus17 PIP renders|

The last run is host-clamped to1600x1022 physical pixels,939x600 logical,
and effective DPI1.7033333. This is not an exact2.0-scale certification.
All processes exited cleanly through Run/Dispose; the default wrapper returned0.
`config-restoration.json` verifies the original settings bytes were restored.
The native clipboard test separately verifies every original pasteboard item,
format and data payload after restoration, without logging their contents.

Visual review covered the restored Needs strip, CAS, dialogs, PIP composition,
all four PIP camera rotations and a Sim target. The shader-camera projection
check compares physical foot and world-matrix foot against native framing;
the focused Cassandra fixture projects at(100.99994,177.90918) versus expected
(100,178), with animated pelvis at(100.75685,148.3512). This guards against
accepting a detailed image whose actual subject is outside its frame.
The native close button is visible and its four-pixel inset and actual click
are checked. Caption rendering and native overheight suppression pass GPU tests.

Pixels remain owner-local under `build/ui-audit/r240`; only logs, source-derived
evidence and image hashes are committed. `default-images.sha256` and the three
size manifests identify those captures. `run-final-validation.py` reproduces
focused/default/size runs serially. Earlier failing fixtures are not final evidence.

## Remaining scope

This is a substantial restoration round, not a declaration of complete
base-game equivalence. Guided tutorial presentation and unreviewed UI flows
still need work. Avatar fully-visible suppression currently errs on showing
an event when sprite damage bounds are unavailable. Native notification0x10c
semantics remain unrecovered; the operand is retained without speculative
queue mutations. The primitive phase is now preserved per stack frame, including nested-frame
isolation; original OBJM user-event phases are not yet reconstructed. Surrounding
subworlds are outside the original base-game lot view and are not rendered by
this secondary camera. The secondary view follows the current wall/cutaway configuration; exact native
secondary cutaway recomputation remains unverified. Native fade transitions
remain to be matched. The existing main renderer projects
upper floors slightly taller than the original58/116/232-pixel lookup; the
restored crop law retains native dimensions, and this shared projection
difference remains explicit rather than silently changing every camera.
