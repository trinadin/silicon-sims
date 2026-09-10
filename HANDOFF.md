# HANDOFF — integrated source and validation boundaries

Read [CURRENT](../CURRENT.md) for next-session priorities and the
[task records](../coordination/tasks/) for live ownership. Snapshot: 2026-09-10
(consolidation).

## Source state

[CAS-01](../coordination/tasks/CAS-01.md) integrates native portrait frames,
4×2 family layout, bounded captions and decoded fonts. Component checks 60/0;
packaged real CAS flow 111/0 and record-format checks 28/0. See its compact
[result](../coordination/evidence/CAS-01/result.md) and native verification boundary.
This package also includes [PLUMB-01](../coordination/tasks/PLUMB-01.md);
its 63 offscreen checks pass, but selected-character live review remains open.

Main parent HEAD: the squashed `mac-port` baseline — the 2026-09-09/10
integration wave **committed**, then squashed (2026-09-10, user-directed) into a
single commit on the upstream fork point `c293c9c` (origin/simitone-forked); all
343 workspace commits collapse into one with a byte-identical tree. Engine
FreeSO HEAD: the equally squashed `mac-port-rel` tip (67 engine commits → one,
tree byte-identical to the former 71b76b37). The tree builds clean
(Simitone.Client Release, 0 errors, 2026-09-10), and a fresh checkout of these
commits contains the full integrated implementation — the old "preserve the
uncommitted working tree" warning is obsolete. Nested FSOMonoGame is clean at
31547ae. Original data, package caches and generated distributions are not
maintained source. Recovery tags `backup/pre-squash-20260910` exist in both
repos until the squashed baseline is confirmed and pushed.

The original R253 count/tail serialization fixes and verdict/signature hardening
remain baseline obligations; detailed evidence is in
[the R253 round](tools/iff-dump/r253-serialization-fix/round.md).

Today's coordinator records integration of CAS preview/focus (UI-02), audio
bus/duck/interpreter/teardown work (AUD-03/07/09 and later AUD-11), financial
ledger (SIM-09), save safety/default/appearance work (SAV-02/03/08), the approved
bounded GenericCall13 path (SIM-13), neighborhood management (NBR-02), custom
animation safety (CC-03), an export tranche (SAV-06), and harness/bootstrap
fixes. **These are now committed** (inside the squashed baseline). Consult
those task records for exact files, accepted boundaries and remaining tranches.
TRV-02 re-delivery and AUD-05/save/export follow-ups are not silently integrated.

## Verification boundaries

Recorded integrated default suite: 142/0. Focused integration gates include
10/0 (engine/save/audio/budget), 2/0 neighborhood management (25 assertions),
and 7/0 animation/corpus. [REL-04](../coordination/evidence/REL-04/result.md)
records the multi-group regression matrix, standalone-only CAS/tutorial flows,
private corpus integrity and package/signature checks for its tested snapshot.
Those passes do not certify all newer edits or full audible/visual/game parity.

REL-04's career group is unresolved. SIM-14 exonerated the integration wave;
SIM-15 is validating the multi-day trigger/boarding protocol. During day-close,
new run1 lacked a verdict and run2 remained live. Its owner must report the final
results; no game was started, stopped, rebuilt, repackaged or re-signed by cleanup.

## Working safely

Use the coordinator-reviewed launcher/bootstrap (ENV-01/02), separate mutable
fixtures/userdirs and serialized runs. Read [SHARED](../coordination/SHARED.md)
before launches AND publish/packmac that replaces an in-use bundle. Run tests
against a pinned current source/package snapshot; do not mix fixture-mutating
checks or accept zero/unfinished verdicts.

Preserve source and active failed-run evidence. Keep current state compact,
link task results, and delete confirmed redundant output without archives.
Future UI changes still require scoped approval; only the recorded approved
CAS/other proposals are authorized, not arbitrary follow-on geometry changes.
