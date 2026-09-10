# R242: reviewed helper handoffs and bounded integration

This round integrates two independently checked findings in the maintained
Simitone and FreeSO forks. Original game files remain read-only inputs.

## CAS preview compositing

The existing Create-a-Character background cutout now clears premultiplied RGB
with alpha. The old alpha-only write added the painted blue placeholder to
whatever was rendered behind the preview. Only the existing VITA_RECT
(618,145,100,220) changes. Geometry, artwork outside it and controls are retained.
The background texture is cached and shared across repeated DesignChar panels;
there is no private clone. The corrected operation is idempotent and the only
production caller is DesignChar.

A GPU fixture draws the production background over a varied underlay and
requires all 22,000 preview pixels, including edges, to preserve the underlay.
The full survey also captures the real background in context. This is a
compositing check, not an end-to-end certification of the real CAS workflow.
The before/after artboard comparison finds zero changed pixels outside the
preview at 1× (`cas-before-after.json`). All three retained size comparisons
likewise confine differences to that rectangle plus its one-pixel linear-sampling
border at fractional DPI (`cas-size-comparison.json`). The isolated survey still
shows its underlying lot/screen through the window; it does not contain a Sim.

## Original OBJM event phase import

The importer now maps saved generic primitive state 1/2 into the existing
user-event phase field only after resolving the actual current opcode as 35.
It runs after atomic stale-stack validation. Other primitives/states, invalid
routines or instruction pointers, native save format and JIT behavior retain
their existing contracts.

**Correction to the helper report:** House56 object292 is not a saved Social2
PIP event. OBJT entry146 is GUID bedd7b26 (Dunginator9000 / cat litter box),
whose private tree4110 node1 is opcode27. The helper matched a private tree ID
without resolving its owner. The independent fixture uses that real save as a
negative case and the original Social Attack opcode35 routine for constructed
positive phase cases. No real saved PIP continuation is claimed from House56.
See [the independent import review](../r242-objm/restoration-review.md) for the
owner lookup and recovered serializer/primitive proof.

## Camera and broader UI follow-up

All three helper reports have now arrived. The camera fixture, disassembly and
final report were independently reviewed. Native default animation is 333 ms, while the
port currently shows/hides immediately. Native different-floor PIP also
recomputes and restores cutaways; clearing the port cutaway array is incomplete.
Some helper decoder branch labels are reversed; only byte-verified conclusions
are retained. See [the independent camera review](../r242-review/camera-and-cas.md) for the
assessment and prerequisites.
These camera changes are not implemented in this package.

The broad UI helper's wide-window toolbar claim still needs the original
>1024-pixel composition law. Its mid-survey Magic Town dialog is an evidence
contamination lead, not a confirmed base-game product bug. Real-flow CAS,
populated Pick-a-Family and additional dialog states remain audit gaps.

## Validation

The final self-contained arm64 package passes **138/138 default regression
checks**, **16/16 focused checks**, and **23/23 checks at each display setting**,
with no skipped checks, clean Run/Dispose, and default wrapper exit zero.
`validation.json` records the results and verifies every retained image manifest.

| Requested window / scale | Actual logical size | Physical viewport | Effective scale | Checks |
| --- | --- | --- | --- | --- |
| 800×600 / 1 | 800×600 | 800×600 | 1 | 23/23 |
| 1280×800 / 1 | 1280×800 | 1280×800 | 1 | 23/23 |
| 800×600 / 2 | 939×600 | 1600×1022 | 1.7033333 | 23/23 |

The host clamps the requested 2× run to the fractional scale shown above.
The default run retains 69 captures and the size runs 80 each (309 total),
including one CAS GPU fixture, five tutorial captures and 17 PIP renders per
run. Images remain owner-local under `build/ui-audit/r242`; only derived text,
scripts and image hashes are committed. Committed text logs have trailing
whitespace removed.

Settings were restored byte-for-byte (`config-restoration.json`); all nine
published/package assembly hashes match (`package-assemblies.json`), and the
original executable retains its recorded hash (`original-input.json`).
The updated app is `dist/The Sims-arm64.app`.
