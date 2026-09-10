# R239 tutorial ownership and dialog lifecycle

This is a bounded engine/lifecycle restoration, **not completion of the guided
tutorial UI**. User approval covers original-backed base-game restoration.
No proprietary resource was changed or copied into the source tree. This folder
contains metadata and disassembly from the owner's local installation.

## Source and independent evidence

Original executable: `game-data/The Sims/The Sims Complete`, SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`.
Addresses below are file offsets; r141 symbol entries have a +2 convention.

- `TryTutorial` f7080–f7140 (`primitive.txt`): primitive 10 interprets only
  operand byte 0. Mode 0 attempts ownership for the caller; mode 1 releases only
  if caller equals current owner. Other values report primitive error 53.
- `SetTutorialObject` e2c70–e2d30 (`owner-lifecycle.txt`): any nonnull owner
  prevents acquisition, including repeat acquisition by itself. Clearing an
  owner aborts its outstanding object dialogs, then emits global event 0x107.
  Null-to-null succeeds without emitting an event.
- Object destruction ca270–ca288 (`owner-delete.txt`) clears the tutorial latch
  if and only if the dying object owns it. `RemoveObject` also aborts every
  dialog belonging to the removed object at e48c8–e48d8.
- `ObjectModule::DoStream` e7fd8–e8040 (`owner-stream.txt`) serializes the owner
  as a signed object ID and resolves it against the loaded object table. This
  state is persistent, not a disposable UI reference.
- The OBJM trailer begins after a terminal **four-byte ReconMark**. Existing
  instance skip pointers land at that mark, not after it. `ReconMark`
  11e570–11e618 (`recon-mark.txt`) proves the four-byte consumption.
- `ObjectDialog::SetParams` cca8c–ccab0 (`dialog-flags.txt`) marks a tutorial
  dialog by **caller ownership**, regardless of dialog type. ccb10–ccb28
  independently reads flags bit 0 for Continue and bit 7 for nonmodal display.
  `ObjectDialog::Show` cc54c–cc550 passes the latter's inverse to
  `DoSimsModalDialog`. Async Continue is not synonymous with nonmodal.
- `ObjectDialog::Abort` cc3d0 stores state 5 and destroys its window. A future
  `TryDialog` treats an aborted dialog as absent (f17cc–f17d4), rather than
  receiving a fabricated successful button result.

`python3 tools/iff-dump/r239-tutorial/scan.py` independently scans loose IFF/FAM
files and FAR members. The current owned corpus contains 3,575 IFF files,
13 opcode-10 call sites, and 849 OBJM chunks, with zero trailer decode failures.
35 OBJM chunks have nonzero owners, including duplicated neighborhood templates:
House21 owner 212, House23 owner 37, House29 owner 130. These are actual stored
owners, not guesses inferred from object names.

Nine opcode-10 sites are in base-game archives: Tutorial.iff has acquire/release
in main BHAV 4096 instructions 2/4; HelpSystem.iff has five sites; Global.iff has
two sites in BHAV 274. The other four occur in HDHelpSystem.iff. All observed
operands are byte-0 modes 0 or 1 with seven zero reserved bytes. `corpus.json`
records exact paths, chunk IDs, instruction indices, branches, and operands.
IFF IDs are big endian; BHAV payloads are little endian. The scan explicitly
corrects the historical helper's chunk-ID endian assumption.

## Implemented scope and impact

- Registers primitive 10, restoring true/false/error branches. The JIT return
  classification is now true/false instead of unconditional true. TSO's former
  no-op behavior remains unchanged if this opcode is encountered there.
- Compiled-module version advances from 2 to 3. Optional `-jit` module selection
  rejects old/future versions at discovery and cached lookup, falling back to
  source-IFF interpretation. Compatible version-3 modules retain the existing
  initialization/caching path. This is necessary because an old compiled
  unconditional branch cannot be repaired by the new primitive handler.
  Source-content checksum validation remains the preexisting AOT residual.
- Stores `TutorialObjectID` in TS1 platform state, with a current-VM object lookup
  for runtime access. Ownership changes notify the client. Owner-only release
  clears the owner's pending VM dialog state and its global blocking slot. It
  preserves a foreign dialog and preserves a previously paused speed of zero.
- Object removal clears ownership before removing the entity. Explicit clear
  also normalizes a dangling imported ID to zero without a false change event.
- Imports the original OBJM trailer owner. Existing original-house data remains
  untouched. Native save version 39 adds only a signed short to the TS1 platform
  payload; version-38 reads default to no owner. TSO platform payload bytes are
  unchanged. Older builds do not know the new TS1 save payload.
- Tracks open tutorial/owner dialogs, replaces a prior open dialog from the
  same caller, and dismisses only that owner's windows on release. Context
  replacement and lot-control removal unsubscribe and close tracked windows.
- Type-4 dialogs and dialogs from the current tutorial owner honor the original
  nonmodal bit (0x80). Ordinary unowned dialog routing remains unchanged.
  No dialog geometry, title, closebox artwork, highlighter, or icon was guessed.

## Verification contract

`Client/Simitone/Simitone.Client/AutotestTutorial.cs` exposes
`internal static bool Check(out string diagnostics)` for the root's `uitutorial`
gate. It creates a separate unticked VM with two inert original Tutorial
entities; it does not run their initialization or main trees, mutate the active
lot, or save the owner's files. Thread-local `VM.UseWorld` and `VM.GlobTS1` are
restored in `finally`.

The helper checks registration, exact operand roundtrip including nonzero spare
bytes, initial/repeated/contended acquisition, foreign release, unsupported
modes 2/255, owner and foreign deletion, own/foreign dialog isolation, prior
speeds 0/2, stale-ID normalization, v38 sentinel preservation, exact v39 payload
bytes and roundtrip, and v38/v39 TSO payload boundaries. Five independent
hand-encoded OBJM fixtures include nonzero owners and a marked object instance;
they prevent confusing the terminal zero mark with the owner field. Dialog
fixtures separate the Continue and nonmodal bits and test an owned type-0
message against an unowned one. Compiled-module fixtures independently pin the
version boundary: versions 0, 2, and 4 are rejected; version 3 is accepted.

Source review and independent Python fixtures completed here; root owns builds,
runtime gates, visual captures, packaging, and final test results. This report
must not be read as claiming those runtime checks passed before root runs them.

## Explicit remaining tutorial gaps

The original cTutorialIcon uses the current owner's private BMP_ 300, exposes
`show situation info`, animates open/close transitions, and remembers a last
target rectangle. None of those visible controls existed in the maintained
client, and this round does not invent substitutes. Original owned-dialog
closebox, anchoring, title suppression for Tutorial GUID 0xc3249a1d, and picture
dialog animation therefore remain incomplete. The highlighter resource
TutHigh is separate from the tutorial icon.

The UI event polling/highlight system (`Tut_CheckForEvents`, control lookup and
HiliteForTutorial), automatic tutorial-family setup/reset, and tutorial
completion generic calls still need separate original-backed implementation and
end-to-end lesson testing. The primitive-level split between async and nonmodal
for unusual flags combinations (bit 7 set while bit 0 clear) is not fully
restored; this change corrects UI modality, while existing VM dialog wait/pause
semantics remain. Ordinary unowned nonmodal dialogs also remain outside this
bounded tutorial routing change. Passing the new gate establishes the restored
ownership/persistence contract, not full base-game tutorial parity.

## Independent review of concurrent camera changes

Readback/source texture ownership, physical-to-logical capture scaling, JPEG
first-save/reload ownership, and legacy PNG source paths were reviewed. A
concrete regression was found: directory enumeration had escaped the old load
exception guard and could abort lot entry for an unreadable album. It was
reported to root and the camera agent; the camera agent restored the guard.
No additional high-confidence blocker was found in that bounded code review.
