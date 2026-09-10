# R247: tutorial lifecycle — reset, completion, cancel, spawner and the tutorial-house flag

The tutorial is now a complete loop rather than a set of parts. The original's
lifecycle is implemented end to end: the Options "Reset Tutorial" button runs
the native confirm→stage flow; the neighborhood screen polls for staged
imports; house entry can spawn the Tutorial object through the native
five-guard law; generic Sim Calls 0/8/9 record tutorial completion in the
neighborhood's persistent state; ESC cancels a running tutorial; and the
move-in refusal for tutorial-flagged houses is genuinely reachable for the
first time (R197's open item is resolved). The associated `uitutorial-lifecycle`
battery (opt-in) pins the whole loop headlessly, driving the real cheat
console, the real dialogs and fresh-from-disk parses.

## Decode phase (decode → skeptic → repair provenance)

Two decodes, both adversarially verified:

* [Lifecycle](../r247-tut-lifecycle/decode.md) — ResetTutorial (Options
  command slot 0x21 + the `restore_tut` cheat): confirm dialog (style 1,
  proceed only on result 3), a DoSave gate whose failure means PROCEED, then a
  pure file action: copy `UserData/Tutorial.FAM` → `UserData/Import/`
  (fail-if-exists, clear READONLY), error dialog on failure, neighborhood
  screen on success. TutorialCompleted: generic calls 0/8/9 → args 0/1/2;
  writes Neighborhood-object fields **+0x12a = arg+1 (u16), +0x12c = 0** in
  both the NGBH-reconstituted temp saved to the neighborhood file and the live
  object (state 3 permanently disables the spawner); arg 0 additionally clears
  sim global 26 live and in the current house file's SIMI. CancelTutorial:
  ESC (0x1B) → owner tree "cancel tutorial" → kill owner → hide highlighter.
  Spawner (LoadHouse tail): inhibit-flag → state≥3 → **spawn requires
  global 26 ≠ 0** → GUID-present → selector → out-of-world creation — the only
  creation path for the Tutorial object. HouseInfo origin (R197 closed):
  GetHouseFileInfo fills +0x14 from the house file's SIMI **global 58**; the
  move-in refusal (+0x14 ≠ 0) is the first move-in guard. verify.py: 133
  instruction pins after the skeptic pass.
* [Import law](../r247-fam-import/decode.md) — the consumer of the staged FAM:
  CheckForNewImports runs on neighborhood-screen init and every 1000 ms; scans
  `UserData/Import/*.FAM`; **only a file literally named Tutorial.FAM
  auto-imports**; ImportFamily deletes that house's characters, evicts the
  occupying bin family (keeps funds), creates the family (FAMI id if free
  else 0), creates fresh `Characters/User%05d.iff` from NBRS/uChr, **moves the
  FAM over Houses/HouseNN.iff**, sets the tutorial-house fields, saves
  immediately, and exports both families. A hand-walk of the owner's actual
  Tutorial.FAM yields the exact post-import UserData end state. verify.py: 135
  pins + 16 record pins.
* The lifecycle skeptic issued SAFE-after-corrections: the `tutorial` cheat
  polarity was inverted in the decode (`on`→spawn allowed, `off`→disabled;
  flag = (param==0) numerically), the "+0x492/+0x494" in the first decode were
  stack addresses (the real fields are +0x12a/+0x12c), and the DoSave gate
  framing was inverted (failure = proceed). All applied with re-verified
  evidence ([skeptic-response.md](../r247-tut-lifecycle/skeptic-response.md)).

## Implementation

Engine (nested `FreeSO`, branch `mac-port-rel`):

* `NGBH.cs` — typed `TutorialState` (word 1 = +0x12a) / `TutorialHouse`
  (word 2 = +0x12c) accessors with the offset-correspondence proof (the
  chunk's word block starts at native object +0x128; shipped data reads
  [0,1,7,…] exactly as the import law writes it).
* `SIMI.cs` — GlobalData retention 38→64 shorts (Hot Date+ saves carry
  globals 32–63, needed for g58); the old `Write` wiped GlobalData to zeros on
  every save — fixed; v0x3E files keep their exact size.
* `TS1NeighbourProvider` — `TutorialState`/`TutorialHouse` (persisted via
  SaveNeighbourhood), `StageTutorialReset()` (the native file action),
  `IsTutorialHouse(house)` (house-file SIMI g58 ≠ 0), `PatchHouseSimiGlobal`
  (a chunk-level 2-byte in-place SIMI patch: after the lazy-parse
  `IffFile.Write` NRE surfaced in the gate, the fix patches exactly the two LE
  bytes — byte-lossless, matching the native reconstitute-patch-save law),
  `CheckForNewImports()` — **stub returning false, disclosed** (the import
  law needs EXPi/uChr codecs and the character-creation pipeline; next slice).
* `VMTS1TutorialSpawner` (new) — the five-guard law at the IffData lot-load
  tail, on the VM thread; the TutorialObject latch deliberately left to the
  object's own TryTutorial primitive.
* `VMGenericTS1Call` modes 0/8/9 — the completion law with the
  house-file-first ordering (a failed house patch skips every write) and
  immediate neighborhood save.
* `VMCheatContext` — `tutorial` (numeric law flag=(param==0)) and
  `restore_tut`; `VMContext.RequestTutorialCancel()` — owner tree
  "cancel tutorial" then kill, per the ESC law.

Client (parent, branch `mac-port`):

* Options reset row: real confirm dialog (proceed only on Yes) → the port's
  save machinery adapted to the DoSave law (disclosed deltas) →
  `StageTutorialReset()` → success returns to the neighborhood screen;
  failure → error dialog; second reset fails fail-if-exists by design.
* Neighborhood import poll at the native 1000 ms cadence, frontmost-gated,
  with once-per-episode error dialogs; wired to the engine contract and
  inert while it is a stub.
* Move-in refusal un-pinned (was a literal `false`): now
  `IsTutorialHouse(house)` — inert on shipped v0x3E data (g58 absent = 0,
  same as native) and live for v>0x3F saves.
* ESC cancel: owner present → tree + kill + highlighter clear; key not
  consumed (native case law), modal-guarded.
* `tutorial`/`restore_tut` registered in the real cheat console; the battery
  drives them through the production parse path.
* `AutotestTutorialLifecycle247.cs` + opt-in `uitutorial-lifecycle` (the
  default Checks string is byte-identical): the reset flow (confirm → staged →
  second reset fails → error dialog), spawner tiers (armed spawn, g26=0
  refusal, state-3 refusal, inhibit refusal), completion law (state
  transitions, live + on-disk g26 clear read back raw), ESC cancel, the
  move-in guard order, the cheat polarity matrix through the real console,
  and game-data byte-identity pins.

## Review and repair record

The adversarial review returned FIX-FIRST with three P1s, all repaired: the
cheats were unreachable (registered in the console's definitions; the numeric
polarity law fixed in the handler), `TutorialCompleted` ignored the house-patch
result (now the decode's abort-all-writes ordering), and three battery tiers
could false-green with a present-but-broken engine (now hard-asserting the
real transitions — ground truth confirmed they exercise live flips: shipped
state=1, live g26=1). P2 repairs: the About-popup disclosure made honest, the
1 Hz error-dialog spam debounced, a null guard added, a vacuous cadence assert
replaced with a real two-stage probe, and the pristine-data assert gated on
the shipped NGBH mirror. The earlier gate crash (lazy-parse `IffFile.Write`
NRE on house files) was root-caused and fixed by the targeted patch rather
than a lossy full-file write.

## Validation

* Baseline before work: 142/0.
* Focused packaged run `-autotest-opts uitutorial-lifecycle`:
  **AUTOTEST RESULT PASS — uitutorial-lifecycle passed=65 failed=0**
  (`focused-final.log`), clean exit; publish and package carry the same
  `Simitone.Client.dll` SHA-256
  `fa190b532300a14abf21957723453c587936ff722f4b2ea510d3a1e73d7718c7`.
* Full default suite: **PASS 142/0** (`default-final.log`) — the opt-in
  wiring provably does not touch the default path.
* Visual evidence `ui-audit/r247` (staged in this directory): the real Reset
  Tutorial confirm dialog with the canon about text and Yes/No buttons, the
  error dialog, and the post-import neighborhood capture.

## Disclosed residuals (next slice)

* `CheckForNewImports` is a stub: the import consumer (FAM→neighborhood law:
  character files from NBRS/uChr, EXPi/uChr codecs, FAM-over-HouseNN.iff, the
  4000-Strays special case) is fully decoded and battery-wired
  (PENDING-ENGINE tiers activate automatically) but not yet implemented — the
  reset flow therefore stages the file and stops, honestly.
* The generic import UI (`CycleThroughImports` file picker flow) is decoded
  but unimplemented; only the Tutorial.FAM auto-import path matters for the
  reset loop.
* Shipped data is SIMI v0x3E (32 globals): g58 is absent (=0), so the
  move-in refusal is live only for v>0x3F saves — identical to native
  behavior on the same data.
* The cross-house object sweep at the native LoadHouse tail is unported
  (moot on the port's whole-lot restore path).
* Dialog literal texts for the reset flow use the port's pinned strings;
  two native runtime-localized literals are unrecoverable (disclosed in-code).
