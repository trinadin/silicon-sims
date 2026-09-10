# R251: the mood half — the cadence/smoothing question answered and the weighted law implemented

The PARITY "room half" closed in R235; this round closes the **mood half**. The
open question from R223 was whether the stored mood is *smoothed* toward the
CalcHappy target, *recomputed on a slower cadence*, or masked per person — the
original saves carried two motive snapshots with byte-identical inputs yet a
*changed* mood (Bob 74.073→57.385, Betty 75.118→60.511). R251 settles it from
the actual PPC binary and lands the law.

## The answer: there is NO smoothing

`cXPerson::CalcHappy` (0x10b9c0) computes and stores the mood directly; it never
reads the previous mood, so there is no `mood += (target−mood)·k` blend. The mood
is the **pure recompute** `mood = Σ(wᵢ·mᵢ)/Σwᵢ`, written straight to
`person+1944` (motive slot 3). The whole-image search confirms `person+1944` has
exactly one real writer (CalcHappy); the two other constant-1944 stores use the
stack/TOC bases. The earlier "state-dependent smoothing" inference was a
temporal-misalignment artifact, not a blend.

The recompute is **rate-limited**: `cXPerson::Simulate` (0x10bff0) gates on
`person[2064]` with `addi r0,r3,0x3c` / `cmpl` / `blt` (once per **60 time-units**)
before `Motives::Sim` (0x09eab0) → `CalcHappy`, all direct `bl` calls — **R223's
"VTABLE only" was wrong**. The absolute clock unit is not statically resolvable.
The port's own 2-game-minute recompute cadence is an accepted port convention and
was left unchanged.

## The aggregation law (implemented)

`mood = Σ(wᵢ·mᵢ)/Σwᵢ` over the 8 participants in native CalcHappy table order
`(7,5,6,15,8,14,9,13)` = Hunger, Energy, Comfort, Fun, Hygiene, Social, Bladder,
Room. The `wᵢ` are the piecewise STR#502 (adult) / STR#504 (child) "Happy Weight"
curves — already loaded by the port as `WorldGlobalProvider.HappyWeight` /
`HappyWeightChild` (via `FSO.Common.TS1.TS1Curve.GetPoint`) but never wired into
the mood path (the port computed the equal-weight `(Σ7+room)/8`).

Curve→motive mapping is **index order** (pinned by decoding
`MotiveCurveSet::LoadFromFile` 0x9f500, which fills `curves[idx-1]` from STR# curve
`#idx`, and by CalcHappy iterating `curves[i]` for `table_order[i]`):

| motive | curve |
|--------|-------|
| Hunger | 0  (−100;15)(−60;5)(−40;3)(0;1)(100;1) |
| Energy | 1  (−100;10)(−80;3)(−40;1)(100;1) |
| Comfort | 2 (−100;5)(−80;3)(−40;1)(100;1) |
| Fun | 3  (−100;10)(−80;3)(−60;1)(100;1) |
| Hygiene | 4 (−100;5)(−40;2)(40;2)(100;5) |
| Social | 5 (−100;2)(0;1)(100;2) |
| Bladder | 6 (−100;5)(−40;2)(40;2)(100;3) |

Table split: `person+1536` in **[1,17] → STR#504 (`HappyWeightChild`)**, else
`==0` or `≥18` → **STR#502 (`HappyWeight`)** (CalcHappy gate 0x10b9c8-0x10b9e8
adjudicated from raw hex; corroborated by `cXPerson::Initialize` loading STR#502
into the `-0x5900` TOC slot and STR#504 into `-0x58fc`). The port surfaces the same
field as `VMPersonDataVariable.PersonsAge` (attr 58) and uses the identical
`age>0 && age<0x12` child test that `VMFindBestAction` already uses.

## The Room weight — a disclosed default, not proof

STR#502/504 contain **exactly 7** curves, so Room has no curve; it is a scalar.
The native value could **not** be pinned statically (the TOC pointer slots are
unrelocated placeholders and the saved moods are state-dependent). This round uses
`RoomWeight = 1f` as a **documented best-supported default** (Room contributes
comparably to a weight-1 motive). R223's least-squares `8` and a state-derived
`~57` are both non-authoritative and are not used.

Two niche native details were deliberately not modeled: the suit zero-suppression
gate (`person[1550]`, drop a motive equal to the constant **13.0** for suits 2-5;
standard suits are 0/1) and the exact accumulators' float precision.

## Implementation

* Engine (`VMTS1MotiveDecay`): added `ComputeMood` computing the weighted target on
  the existing 2-game-minute cadence; no-smoothing preserved; the 7-motive decay
  is untouched. `VMAvatarMotiveDecay` (the TSO path, unused in TS1) still uses
  `/8` and was deliberately left alone.
* Harness (`AutotestRunner`): `MoodMatches` repointed to the weighted law (corrective
  — the old pin asserted the known-wrong `/8`); `SnapshotAvatar` +a field;
  `ComputeMoodLaw` (mirrors the engine); death-reach mood-align uses the new law;
  new opt-in `moodlaw` check (NOT in the default `Config.Checks`, additive like
  `freewillwin`).

## Validation

The skeptic confirmed all six decode claims at the raw-instruction level; all three
verify scripts (`verify.py`, `pin-verify.py`, `moodlaw-verify.py`) re-pin the
SHA256 `33c76da2…c06a5f` and assert the cited instruction words — all PASS.
The adversarial review confirmed the table-split direction, the engine/harness
curve map, and that the pin repoint is corrective (tolerance-3 justified).

* Focused `-autotest-opts corpus,lot,motive,mood,load,moodlaw`:
  `moodlaw` **PASS** (moodA=56→moodB=59, the step change reaches the new target on
  the next recompute — aggregation + no-smoothing, end-to-end). `mood` FAILs in
  THIS run only because `moodlaw` re-pins the motives each frame and so mutates the
  live sim that `mood` samples (a harness interaction, not a production defect; the
  checks are run separately in practice). `motive` PASS.
* Full default suite: recorded in `default-final.log` (final HANDOFF).

## Disclosed residuals

* The Room weight is a documented default (`1`), not pinned; the native may use a
  per-entry curve or a different constant.
* The suit zero-suppression gate (person[1550] × 13.0) is unmodeled in the port
  (negligible — measure-zero; standard suits 0/1).
* The port stores motives/mood as `short`, the native as floats; the float→short
  truncation is a pre-existing representation difference, not introduced here.
* `moodlaw` re-pins motives and therefore must not be combined with `mood` in a
  single focused soak.
