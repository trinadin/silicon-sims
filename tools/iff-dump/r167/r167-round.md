# R167 — Generic Sims Call 13: abort interactions

## Result

R167 closes the valid owned-corpus path for TS1 Generic Sims Call mode 13,
`AbortInteractions`. The previously unimplemented FreeSO case now removes
every other person's queued or current interaction associated with the stack
object, preserves unrelated interactions and the target person, and returns
true. This is a non-UI scheduler correction; no layout, input geometry,
rendering, animation, or presentation code changed.

The original invalid-target aftermath is deliberately not generalized. The
owned engine reports error 10 and enters `CleanupPeople(null)`, which invokes a
much broader object/person teardown path. Both owned-corpus callers first set
Stack Object ID to their own valid object id. The port therefore fails loudly
on an unresolved id rather than guessing at destructive whole-lot cleanup.

## Original-engine recovery

`r167-original-engine-decode.py` reads the owner's original Complete
Collection PPC PEF in place and emits only addresses, hashes, decoded law, and
selected instruction words. No executable bytes or proprietary asset payload
are copied into the repository.

- original engine SHA-256:
  `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`;
- packed data at file `0x5c22f0`, length `0x6d834`, unpacked to `0x7bf80`;
- unpacked-data SHA-256:
  `f740c1dfa1dea8187b12ebf222ca1a39223365d26ad5d2f2e353d5efba247be3`;
- `TryGenericSimCall` table entry 13 resolves to virtual `0xe9848`, file
  `0xf26d8`; entry 14 begins at file `0xf273c`;
- mode-13 code `[0xf26d8,0xf273c)` SHA-256:
  `7914065c684f782e3eeec6670887d1b42616e188ac88671377f61c5f9111c9a8`;
- `ObjectModule::CleanupPeople` `[0xe3bc0,0xe3c40)` SHA-256:
  `edae6f356befb750cb8d7434e324221ca57ee83fd0419ea9b46e2498526140d7`;
- base `cXObject::Cleanup` `[0xca0d0,0xca2a0)` SHA-256:
  `c789780f61c31dc9879d62d94d820676f5edd1185f92824199e862f5087ec05b`;
- `cXPerson::Cleanup` `[0x1081d0,0x108d94)` SHA-256:
  `52f32a941f06930695598cc3331d32d1876d07a80cca85f35f38a1a7a8173bfd`.

The recovered valid-target law is:

1. Resolve the target from Stack Object ID.
2. Visit every object except that target and invoke virtual `Cleanup(target)`.
3. Base objects do nothing for a non-null target; people inspect current
   object-use/stack state before their queue.
4. A queued 60-byte interaction record matches when either its `+0x08`
   TargetID or `+0x0c` Icon field identifies the target. Remove each match and
   invoke its Queue Skipped path.
5. A current object-use/stack match enters the current-interaction cleanup
   path. The generic-call common exit is true.

The deterministic report SHA-256 is
`d392328ebc078d502e9aec8aa081be224bd71b59a9d7917b8f4df9298c0c3b57`.
It is byte-identical when generated from the repository, `/tmp`, and optimized
Python; the script passes `ruff check` and Python compilation.

## Owned-corpus callers

`r167-generic-call13-scan.py` follows the production IFF/FAR formats and walks
loose IFF/FAM files, top-level FAR members, and one nested FAR level. It scans
8,871 files, 2,238 IFFs, and 28,246 BHAV chunks. There are exactly two mode-13
sites, both in `GameData/Global/Global.far!PhoneGlobals.iff`:

- BHAV 8252, `import lot`, instruction 1, two-instruction payload SHA-256
  `6ac6bb6ca589640ccfc4b857fb752eef905bd66187efed6fa5477083b82c220e`;
- BHAV 8284, `main`, instruction 1, thirteen-instruction payload SHA-256
  `7f81534e9514558fe18a5da09c85dda881ede0db6dc5eb3128264201e01597f2`.

Both routines enter through the exact expression operand
`00 00 0b 00 00 05 0a 03`, decoded as
`Stack Object ID := My Object.ObjectId`. The scanner hard-asserts both sites,
both payload signatures, both entry expressions, and the complete counts, so
optimized Python cannot discard its validation. Its deterministic report
SHA-256 is
`6d94afa3f98267c7738a3d8344be987427d1d8148b9679fcab8091d9f8b75440`.

## Port implementation

Nested engine commit `2ff915c6` adds only mode 13 in
`VMGenericTS1Call.cs`:

- resolve `context.StackObject` and exclude it from cleanup;
- snapshot the avatar collection and skip dead or threadless avatars;
- calculate current `Callee`/`IconOwner` and frame
  `Callee`/`StackObject` matches before any cancellation callback can mutate
  the stack;
- cancel the current action first, then snapshot and filter the queue on
  `Callee` or `IconOwner`;
- preserve Temp0 and return `GOTO_TRUE`.

The port routes removal through the existing `VMThread.CancelAction` contract.
That is an intentional scheduler adaptation: it supplies the port's existing
graceful active/non-skippable behavior and Queue Skipped dispatch; it is not a
claim that the original queue record contains the port's TTAB/MustRun flags.

## Verification

### Standalone actual-dispatch probe

`tools/generic_ts1_abort_probe` invokes the production
`VMGenericTS1Call.Execute` handler in Release configuration. It proves:

- isolated queued `Callee` and `IconOwner` filtering, including adjacent
  matches;
- active direct-`Callee`, frame-`Callee`, and frame-`StackObject` cleanup;
- graceful cancellation of a non-skippable VM action and priority reset;
- preservation of an unrelated avatar's priority, flags, and queue;
- target-person exclusion and safe dead/threadless handling;
- Temp0 preservation and true exit.

Its ordering fixture uses a real local entry point 4 as Queue Skipped. That
callback clears the actor's stack while a later icon-only queue match remains.
The probe passes only if the handler records and cancels the active frame match
before the synchronous callback mutates the stack.

```sh
dotnet run --project \
  tools/generic_ts1_abort_probe/generic_ts1_abort_probe.csproj -c Release
```

```text
PASS: Generic Sims Call mode 13 actual dispatch verified: isolated
Callee/IconOwner queue filtering, VM non-skippable graceful cancellation,
active Callee and frame Callee/StackObject unwind, unrelated-state
preservation, target-person exclusion, active-before-Queue-Skipped ordering,
Temp0 preservation, and true exit.
```

### Packaged integration

The Release client builds with 0 errors. A self-contained `osx-arm64` publish
and `packmac.sh arm64` produce the tested 2.1 GB application bundle.

The focused check dispatches the production handler on an isolated loaded-lot
fixture while pinning both mounted PhoneGlobals callers. Its three queued
records use Callee-only, IconOwner-only, and unrelated matches; only the
unrelated UID 3 remains, Temp0 stays 12345, and the handler exits true.

```sh
NSUnbufferedIO=YES 'dist/The Sims-arm64.app/Contents/MacOS/TheSims' \
  -ApplePersistenceIgnoreState YES \
  "-path$PWD/game-data/The Sims" -autotest 5 \
  -autotest-opts lot,corpus,genericcall13 -autotest-timeout 1800000
```

`r167-genericcall13-run3.log` is a clean 224-line segment: **7 passed, 0
failed, 0 skipped**, clean Dispose; SHA-256
`67fd583ab4d02d0f46cc9e2521698165f43fe4caa06b3e4315ac30bb48f1ce2e`.

After regenerating the `/tmp` IFF probes, the full packaged default gate is
**105 passed, 0 failed, 0 skipped**, wrapper exit 0, clean Run/Dispose, and
contains no `SimAnticsExc`, `bad-routine-frame`, or `FAIL`. The natural soak
reaches 10:15, creates and removes the original `CarJunk.iff` group, and sees
CarPortal 4105 and CarJunk 4121. `r167-gate-run2.log` SHA-256:
`4949be1fee6ed303557247aae9dc1b5c2ff2df86aaf83f6676676f3eae0a7e2f`.

## Boundaries and next target

R167 closes every known owned-corpus invocation, not arbitrary malformed or
mod-authored invalid Stack Object IDs. Reproducing the original null-target
path requires a separately decoded broad object/person teardown contract and
must not be approximated destructively.

No original asset, executable slice, or machine-code payload was committed.
No UI layout, spacing, sizing, hit target, gesture, animation, control
placement, or presentation changed.

The most efficient adjacent R168 target is an original-engine decode plus
owned-corpus census of mode 14, `HouseRadioStationEqualsTemp0`. The port already
implements that name as a global write, but the adjacent PPC case has not yet
proven the direction, value, or exit law. If it matches, close it with evidence;
if not, correct it with the same actual-dispatch and packaged-gate discipline.
