# R163 — engine parity batch + natural career-loop closure

Round mandate: advance as much evidence-backed, non-UI parity as possible. The
standing camera/scrapbook UI item was deliberately not touched: established UI
geometry is protected and that work needs explicit approval plus visual
validation.

Baseline: Simitone `d72bbc6`, FreeSO `412daf45`, full gate 99/99.

## 1. Complete expansion-era PersonData space

Original base-game OBJM/NBRS records can persist a prefix shorter than the
expansion-era VM address space, while mounted Complete Collection BHAVs read at
least through word 84 (anchors: Global 488 reads 80/81; PersonGlobals 8645 reads
84). The old live avatar could therefore retain a short array and throw when an
original tree addressed a later word.

Engine changes:

- normalize persisted PersonData to 101 signed words (`0..100`) at both
  `VMAvatar.Load` and `InheritNeighbor`, copying the saved prefix and
  zero-extending only the absent tail;
- keep all original TS1 words raw even where their numeric slots alias TSO-only
  semantics: word 21 (outfit state vs housemate), word 70 (zodiac vs skill lock),
  the online-job slots, and word 98's TSO initializer;
- retain word 74 (`RenderDisplayFlags`) after applying its renderer side effect;
- use the single 101-word constant for the getter/setter bounds.

The `persondata` gate proves every House 5 avatar has width 101, round-trips
words 21/70/74/80/81/84/100, keeps the TS1 word-98 tail zero, executes both
anchored original check trees successfully, and zero-extends a real NBRS
neighbor through the production inheritance path.

## 2. Travel companion inventory replacement

`VMTS1InventoryOperations` modes 5/6 create Type-2 tokens with GUID 10
(auto-follow) and 11 (follow-home), whose `Count` is the neighbor id supplied in
Temp 0. The replacement branches incorrectly searched for GUID 0/1 and wrote
the primitive's decoded `count` field, so repeated calls appended stale tokens
instead of replacing the latest companion.

The fix searches the GUID it creates and writes Temp 0 in both branches. The
`travelinv` gate pins the original PhoneGlobals call sites and runs `1 -> 2`
through both production modes, requiring exactly one GUID-10 and one GUID-11
token whose count is 2; the scratch inventory is restored exactly afterward.

## 3. Free-will dynamic action variants

`VMFindBestAction` scored every variant emitted by Change Action String, but
added the first variant repeatedly. Distinct autonomous choices therefore
collapsed to duplicate menu records even though their StackObject-derived
parameters differed.

The production aggregation now adds each `item`. `freewillvar` invokes that
same helper with two variants (`Param0 = 111/222`) and pins identity, order,
score, and callee without mutating the live lot. The corpus scan in
`r163-freewill-variants-scan.txt` establishes practical reach: 2,238 physical
IFF records, 692 BHAVs in 268 containers containing opcode 50 (Change Action
String), and 16 conservative direct autonomous TTAB anchors. That is a lower
bound because indirect/global/semiglobal tests are intentionally excluded.

## 4. VM scope parity

Two interpreter/JIT mismatches were closed:

- JIT scopes 12/28/29 (`TreeAdRange`, `TreeAdPersonalityVar`, `TreeAdMin`) now
  emit the existing interpreter `GetTreeAd`/`SetTreeAd` contract with types
  1/2/0. The JIT cache version advances 1 -> 2 so stale compiled trees cannot
  retain the old zero/throw/no-op behavior. `tools/jit_scope_probe` pins reads,
  writes, compound writes, and the cache version.
- `NeighborhoodData` scope 34 now reads signed NGBH neighborhood words 1..15
  in TS1, rejects word 0/out-of-range/non-TS1 access, and remains read-only.
  The JIT delegates to the interpreter contract rather than compiling a
  constant zero. `tools/neighborhood_data_probe` parses and passes all nine
  owned-game `Neighborhood.iff` fixtures. The reproducible corpus scan covers
  all operand schemas carrying `VMVariableScope`: both physical and mounted
  views contain the same 30 reads, 0 writes, all opcode-2 Equals operands
  (word 1: 3 uses; word 2: 27 uses).

## 5. Natural career return, pay, and promotion

The R158 residual was exercised without directly calling the career trees. An
optional `carreturn` soak carries House 5's real Science worker through the
natural 09:00-15:00 shift, then 75 minutes beyond EndTime. The verdict observes
committed state changes at their engine boundaries:

- successful object creations are attributed to exact scope IFF + routine:ip;
- committed household budget mutations are attributed likewise;
- each observer isolates subscriber exceptions and is detached in every
  `Finish` path, so instrumentation cannot change primitive outcomes;
- the pass law requires exactly one outbound and one return `CarJunk.iff`
  creation (GUID `0x4B71ECDA`), the two exact pay transfers, and the exact
  promotion level. Broad `filename contains car` sampling is diagnostic only;
  it cannot false-pass on `PaperCarrier` or recycled object ids.

Observed in `r163-carreturn-run1.log`:

- 08:00 — one outbound create: `CarPortal.iff:4106:20`;
- 15:00 — one distinct return create: `CarPortal.iff:4121:19` (caller is the
  worker avatar; the authoritative scope attribution is essential);
- Science level `0 -> 1`;
- `CarPortal.iff:4103:51 = +460`, with mounted tuning 132 equal to 2;
- `CarPortal.iff:4103:9 = +230` (the newly promoted salary);
- attributed pay is exactly 690. The household changed by +680 only because an
  unrelated `Fridges.iff:4113:4 = -10` expense occurred during the natural
  six-hour soak; aggregate budget delta is therefore correctly diagnostic.

This closes the adult commute return/get-paid/test-promotion residual. School
bus and Chance Card end-to-end branches remain separate runnable traces.

## Validation

- Release arm64 build: success, 0 errors (existing dependency/analyzer warnings
  remain); self-contained publish + `packmac.sh arm64` produced
  `dist/The Sims-arm64.app`.
- `tools/jit_scope_probe`: PASS.
- `tools/neighborhood_data_probe`: 9/9 original `Neighborhood.iff` fixtures
  PASS.
- targeted packaged run: 13 passed, 0 failed, clean exit
  (`r163-targeted-run1.log`).
- natural career-return packaged run: 11 passed, 0 failed, clean exit
  (`r163-carreturn-run1.log`).
- complete packaged default gate: **102 passed, 0 failed, 0 skipped**, clean
  exit (`r163-gate-run1.log`).
- both maintained worktrees pass `git diff --check`.

No UI layout, spacing, sizing, hit targets, gestures, animation, control
placement, or presentation changed in this round. No proprietary game asset was
added; all probes read the owner's staged local corpus at runtime.

## Remaining high-value work

1. Natural-death decision-to-ghost trace (standing R77 residual).
2. School bus and Chance Card end-to-end career branches.
3. Expansion gameplay systems (Superstar/Vacation/Unleashed/Makin' Magic) via
   narrow corpus-backed engine slices.
4. Camera panel/scrapbook UI only after explicit approval for its geometry and
   an agreed visual-validation route.
