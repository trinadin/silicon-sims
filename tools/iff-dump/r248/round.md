# R248: the FAM import consumer — the tutorial reset loop completes end-to-end

The decoded import law is now implemented: staging `Tutorial.FAM` into
`Import/` (via the Options reset flow or the `restore_tut` cheat) leads to the
neighborhood screen's 1-second poll auto-importing it — the occupying family
of house 7 is evicted with its net worth preserved, its characters are
replaced by fresh originals built from the FAM's NBRS/uChr records, the FAM's
house chunks become `Houses/House07.iff`, the tutorial-house latch is set, the
neighborhood saves, and the screen refreshes. The `uitutorial-lifecycle`
battery's import tiers — wired since R247 — all pass against the real flow,
closing the last gap of the tutorial campaign.

## Law provenance

[`r247-fam-import/decode.md`](../r247-fam-import/decode.md) was adversarially
verified this round: the skeptic confirmed the chunk law, the eviction law
(first bin family claiming the house — not FIFO), the id law (keep FAMI id if
free, else 0), the `.tmp` file-move dance and the tutorial latch, and issued
one surgical correction — C1, the RelMatrix copy (source rows indexed by the
OLD FAM member ids, destination by the NEW ids, `RemoveArray(new)` when the
source row is empty, inner loop over all member pairs) — plus C2–C5
(decode-derived vs byte-proven facts; the native's post-mutation guard leak;
the timer re-arm gate; the one-arg `*.FAM` scan). The decode agent applied
every correction and, while replacing tautological fixtures with falsifiable
binary-bound ones, self-caught two more errors (pets gate bit 0x20, dog
template GUID 0x4a70df92). verify.py now pins 230 instruction words with
7 binary-bound fixtures.

## Implementation

Engine (nested `FreeSO`, branch `mac-port-rel`):

* New chunk codecs: `EXPi` (byte-identical round-trip on Tutorial.FAM's
  pinned record), `uChr` (STR −3 member bodystrings), `Gtab` (GUID-map pairs,
  reconstructed layout — disclosed), `FINV` (positional inventories,
  bounds-checked); `FAMh` stays raw; `NBRS.RemoveNeighbor` added.
* `TS1NeighborhoodProvider.ImportFamFile` — the corrected 25-step law with a
  documented step-map; guards fire pre-mutation (a disclosed, strictly safer
  deviation from the native's C3 leak); eviction preserves net worth;
  characters are deleted (NBRS + world registration + on-disk file, now
  UserPath-contained) and recreated through the proven template-person path
  with uChr→STR# 200 wholesale replacement, CTSS name/bio via the native
  pid+2000-then-pid two-step, lowest-free NeighbourID, pd re-keys, and the
  corrected C1 RelMatrix carry; Gtab pairs spliced into the FAM atomically
  (temp + `File.Replace`) before the `.tmp` move-over with rollback;
  tutorial latch +0x12c/+0x12e; immediate neighborhood save after the move.
* `CheckForNewImports` — the stub replaced by the real poll contract
  (int tri-state: silent-skip / imported / refusal), literal-`Tutorial.FAM`
  auto-import per the native gate.
* `VMTS1TutorialSpawner.SpawnEvidence` — a production-inert seam so the
  battery can assert the spawner law even when the imported family's
  data-script reaction removes the object within ticks (the native would
  share that reaction; disclosed in the battery).

Client (parent): the poll/refresh/error-dialog wiring from R247 now consumes
the tri-state (refresh gated on house ≠ 0, native §1.2 law); the battery's
import tiers activated and pass.

## Review and repair record

The adversarial review returned FIX-FIRST and every finding was repaired:

* **P0** — the template-person STR# 200 "snapshot" was a shallow clone, so the
  restore was a no-op: one import would permanently corrupt the shared
  template and leak imported bodystrings into every future CAS character.
  Deep-cloned/restored, with a new battery assertion proving the template's
  27 slots are value-identical after a real import (`template-str200-restored`).
* **P1** — the Gtab splice rewrote the staged FAM in place (a mid-write
  failure would poison the poll forever) and was the one post-mutation
  refusal, contradicting the doc comment: now atomic (temp + replace), the
  staged file byte-identical on failure, and the guard block lists exactly
  which refusals are pre-mutation with the late-splice leak disclosed.
* **P1** — character-file deletion is now UserPath-contained.
* **P2 batch** — corrected dog-GUID comment (0x4a70df92), the native
  two-step catalog-name read, honest round-trip-scope docs on the new codecs,
  the house-gated refresh, and a NBRS writer-shape comment.

## Validation

* Baseline before work: 142/0.
* Focused packaged run `-autotest-opts uitutorial-lifecycle`:
  **AUTOTEST RESULT PASS — passed=81 failed=0** (`focused-final.log`),
  clean exit; publish and package carry the same `Simitone.Client.dll`
  SHA-256 `a2461740df48ee1f210930c8d4df57855849c1efb8662f7bb991727b49c2ce19`.
* Full default suite: **PASS 142/0** (`default-final.log`).
* Import-tier assertions all PASS: file consumed, FAM-becomes-House07.iff
  (superset check), FAMI id law on the played neighborhood, "Newbie"/665/two
  members, eviction net-worth, two fresh character files with correct OBJD/
  STR# 200, tutorial-state unchanged with tutorial-house 7, template restore.
* game-data byte-identical across all runs (in-run pins + external hash
  aggregate). Visual evidence: the post-import neighborhood screen
  (`tutlifecycle-post-import-neighborhood.png`) and the R247 dialog captures.

## Disclosed residuals

* `Export/` re-export of imported/evicted families is unimplemented (nothing
  in the port reads `Export/`); portrait regeneration is N/A (cards render
  from live data).
* Gtab/FINV serializations are reconstructed without shipped samples
  (scope-documented in the codecs); the selector type-attr sweep is
  native-inert pending its UNRESOLVED bit.
* A persistently failing splice leaves each retry's member pair orphaned in
  the live model (model-only, mirroring the native's C3 leak; disclosed at
  the call site).
* The generic import UI (`CycleThroughImports` file-picker flow) remains
  decoded-but-unimplemented; only the Tutorial.FAM auto-import is in scope
  for the reset loop.
* The port's house saves remain deliberately incompatible with the original
  engine (pre-existing policy), which bounds what the moved FAM-as-house
  file must preserve.
