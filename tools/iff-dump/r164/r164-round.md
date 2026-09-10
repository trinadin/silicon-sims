# R164 — atomic TS1 saved-continuation repair

Round mandate: close the SimAntics faults exposed by the R163 full gate without
changing generic VM snapshot semantics, UI behavior, or proprietary data. The
fix is deliberately limited to the TS1 OBJM import boundary and is backed by a
reproducible scan of the owner's staged House 5 and mounted object resources.

## 1. Diagnosis

The R163 full gate showed three forms of the same compatibility failure:

- `Paintings.iff` objects 133 and 226 reached
  `VMStackFrame.GetCurrentInstruction` with a null saved routine
  (`VMStackFrame.cs:131` in that build). Object 134 carried the same latent bad
  frame and was visible to `bad-routine-frame`, although it had not reached the
  exception site during that gate.
- `phones.iff` object 10, `Lamps.iff` objects 123/131/194, and `Sofas.iff`
  objects 173/174/191/192 faulted when an executable child returned and
  `VMThread.Pop` tried to dispatch the result through a null saved parent
  (`VMThread.cs:751` in that build).
- `Counters.iff` objects 114–117 restored private main 4096 at instruction 3,
  but the current routine has only two instructions. They later faulted on the
  out-of-range instruction access.

The load path explained why these states survived until a later tick:

1. `VMTS1ActivatorNew.ConvertThread` translated every OBJM frame's tree id,
   node id, and code-owner GUID directly into `VMStackFrameMarshal`.
2. `VM.Load` constructed a `VMThread`; `VMThread.Load` appended each restored
   frame directly to `Stack`.
3. This bypassed both normal runtime guards: `ExecuteSubRoutine` handles a
   missing routine as an error return, and `Push` refuses an empty BHAV.
4. A direct top-frame execution therefore dereferenced the bad routine/IP, or
   a valid child eventually returned into the bad parent through `Pop`.
5. The generic exception path then reset the entity, but only after emitting a
   SimAntics exception and at a timing-dependent point in simulation.

This is not an alternate “empty BHAV succeeds” semantic. The saved
continuations refer to private routines that no longer exist, or to an
instruction beyond the current replacement routine. The OBJT format itself
stores init/main version information for resetting changed object types. The
source save is valid historical TS1 state, but it is stale against the mounted
Complete Collection replacements; the port bug was accepting that state as an
executable current stack.

## 2. Owned-corpus proof

`r164-saved-frame-compat-scan.py` reuses the R154 FAR/IFF walkers, corrects the
IFF header's big-endian chunk id at the boundary, decodes House 5's
field-encoded OBJM, and resolves private/global/semiglobal routines from the
current `Objects.far` and `Global.far`. It writes only
`r164-saved-frame-compat-scan.txt`; no shared summary is touched.

Pinned inputs:

- `House05.iff` SHA-256
  `a6d18d81c5072c507b118b68598ee12ea57403634d6507c39b7a04ad4c6cdd99`;
- `Objects.far` SHA-256
  `b029f449a91289b0a789810ff226da0e3a578b0931f1b5bcd104433e316efbf0`;
- `Global.far` SHA-256
  `c301952612dc2adfc4a94b9d9e6e78c3cef5635b989e1cb4342b29989d13b5fb`.

The scan passes a hard-pinned census of **245 instances and 522 saved frames**.
It finds **12 missing-routine frames across 11 objects** plus **four stale-IP
frames across four Counter objects**, for 15 objects whose stacks must be
rejected atomically:

| Current resource | Saved object id / owner GUID | Invalid saved frame(s) | Saved OBJT main version | Current replacement main |
| --- | --- | --- | ---: | --- |
| `phones.iff` | 10 / `6b264238` | private `4096:1`, private `4113:0` | 2 | PhoneGlobals `8284`, BHAV v4, 13 instructions |
| `Lamps.iff` | 123 / `28975923`; 131 / `df77ee12`; 194 / `fd746ad2` | private `4109:0` on each | 3 | LightGlobals `8197`, BHAV v2, 5 instructions |
| `Paintings.iff` | 133 / `d1cf9596`; 134 / `fbe586b7`; 226 / `7d2089b6` | private `4096:0`; `4096:2`; `4096:0` | 6 | ArtGlobals `8194`, BHAV v1, 8 instructions |
| `Sofas.iff` | 173 / `d5ca2d74`; 174 / `24e26386`; 191 / `c59350c6`; 192 / `23a15547` | private `4096:1` on each | 5 | SofaGlobals `8208`, BHAV v6, 7 instructions |
| `Counters.iff` | 114–117 / `91767a36` | private `4096:3`, current length 2, on each | 5 | private `4096`, BHAV v2, 2 instructions |

The exact missing-routine object set is
`10,123,131,133,134,173,174,191,192,194,226`; the exact stale-IP set is
`114,115,116,117`. Nine additional frames on avatar ids 16/18/21 are explicitly
excluded from the static Objects-FAR classification because their character
IFFs live outside `Objects.far`; the live regression gate resolves and checks
them normally.

The generated report is deterministic when invoked from the repository or
another working directory. Its SHA-256 is
`c8422368cf60bd7233d7b6adf1d5ba4e6da3751d571bc8afa7dcf89d230b5c5c`.

## 3. TS1-only atomic rejection and deterministic recovery

`VMTS1ActivatorNew` now validates the complete marshalled stack while it still
has TS1 import context. A frame makes the continuation non-restorable when its
code owner/scope cannot resolve, its routine or instruction array is null, the
routine is empty, or its saved instruction pointer is outside the current
array.

If any frame is invalid, the **entire continuation** is rejected. The importer
clears the stack and action queue, sets the active queue block to -1, clears the
action UID and interrupt, and clears avatar interaction priority,
non-interruptibility, and the interaction-cancelled flag. It records the owning
object id and exact invalid frame descriptions for post-load recovery. Pruning
only one frame would be unsafe: parent/child control flow, locals, parameters,
callee/use state, and the active queue are one causal unit.

The post-load order is also deliberate. `VM.Load` first restores all entities
and multitile references, `UpdateFreeObjectID` runs, and the activator creates
any missing TS1 controller objects. Only then does behavior recovery run. This
keeps controllers available to reset entry points and matches the lifecycle
that the old exception-driven first-tick Reset observed.

The importer publishes the ascending rejected-object list in the transient,
non-serialized `VMTS1LotState.SanitizedThreadObjectIDs`, then visits that same
order and calls `entity.Reset(VM.Context)` exactly once per object before the
first simulation tick. A repaired avatar additionally runs
`ClearMotiveChanges()` after Reset so abandoned motive deltas cannot survive the
discarded continuation. This selects each replacement main through the current
entry-point table while removing the exception and its timing dependency.
There is no per-thread repair bit; `[TS1StackRepair]` logs pin the invalid
descriptors and deterministic 15-object order.

The compatibility rule lives only in `VMTS1ActivatorNew`. Generic TSO/network
`VMThread.Load` behavior and serialized snapshot contracts are unchanged.

## 4. `savedthreads` regression gate

The new `savedthreads` check is part of the default autotest list and runs
immediately after lot restore. It independently:

- walks every live non-routing/non-direct-control frame and requires a code
  owner, non-empty routine, and in-range instruction pointer;
- requires the owned fixture to be House 5 loaded from an actual
  `House05.iff`, not a cached serialized lot;
- reads the transient lot-state report and requires its sanitized ids to equal
  exactly
  `10,114,115,116,117,123,131,133,134,173,174,191,192,194,226`;
- pins every repaired object's GUID, live multitile membership, and replacement
  main id (`8284`, `4096`, `8197`, `8194`, or `8208` as listed above); and
- requires the live stack root to be that current main at IP 0, with a
  non-action self caller/callee frame, an empty queue, active queue block -1,
  and interrupt clear.

The focused packaged run reports `repaired=15`, `invalid-live=0`, every current
main mapping, `fixture=True`, and `savedthreads: PASS`.

## 5. Validation

- Owned-data diagnostic: PASS with all census, frame, GUID, version, and
  replacement-main assertions.
- Focused packaged gate (`lot,savedthreads`): **2 passed, 0 failed, 0 skipped**,
  clean exit on the final semantics (`r164-savedthreads-run2.log`). It reports
  the exact 15 ids, `invalid-live=0`, and all roots at the pinned main/IP 0 with
  queue length 0.
- Natural packaged soak (`lot,savedthreads,carseek`): **3 passed, 0 failed, 0
  skipped**, clean exit after reaching **10:15** (`r164-threadsoak-run1.log`).
  The entire soak contains **zero** `SimAnticsExc` and **zero**
  `bad-routine-frame` records.
- Release arm64 build of the final semantics: `Build succeeded`, 153 existing
  warnings and **0 errors** (`r164-client-build2.log`).
- Self-contained osx-arm64 publish completed successfully to
  `publish/osx-arm64` (`r164-publish2.log`).
- `packmac.sh arm64` completed successfully and produced the executable
  `dist/The Sims-arm64.app` package.

### Final full default gate

- Packaged default gate: **103 passed, 0 failed, 0 skipped**
  (`r164-gate-run2.log`). The wrapper exited 0 and shutdown reached both `after
  Run` and `after Dispose` cleanly.
- The complete log contains **zero** `SimAnticsExc` and **zero**
  `bad-routine-frame` records. Its SHA-256 is
  `403a2b113b2acf10be7cee731dd87f026140d484ab65df0316cf55ce7813d58d`.
- `tools/run-autotest.sh` now sets `NSUnbufferedIO=YES` and passes
  `-ApplePersistenceIgnoreState YES`. A sampled invisible macOS restored-state
  modal blocked the superseded run; these are harness-only launch controls and
  make no product or UI behavior change.

No UI layout, sizing, spacing, hit targets, gestures, animation, control
placement, or presentation changed in this round. No proprietary asset was
added; the diagnostic reads the owner's local corpus and records metadata only.
