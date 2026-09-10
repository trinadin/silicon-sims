# R251 — Adversarial review of the mood-law implementation

Reviewer role: adversarial. I did **not** trust the implementation agent's self-report.
Everything below was verified independently against the source, the raw PPC words, and
the live build artifacts.

Source binary re-pinned (never written):
`game-data/The Sims/The Sims Complete`, SHA256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`.

---

## 0. Verdict (crisp)

**PASS — the decoded law is faithfully implemented, and the pins were repointed
correctively (not weakened). No FIX-FIRST logic bug.** Two documentation nits and a
real build-artifact caveat accompany this; none block the change itself. The
highest-value verification (the child/adult table-split direction) came out
**CONFIRMED, and matches the implementation.**

| Area | Verdict |
|---|---|
| Engine `ComputeMood` | PASS (2 minor robustness nits) |
| Table-split direction (children/adults → table) | PASS — independently adjudicated |
| Engine-vs-harness curve mapping | PASS (identical per-motive) |
| Pin repoint (MoodMatches tol 3) | PASS — corrective, not weaker |
| Regression to death-reach / moodAllOk / other consumers | PASS (stale comment nit) |
| `moodlaw` opt-in check | PASS (sub-frame timing nit) |
| `VMAvatarMotiveDecay` (TSO) intact | PASS |
| Honesty of `implementation.md` | PASS (accurate disclosures) |
| "DLLs restored" build state | CAVEAT — restored to pre-R251 build; must rebuild before a verify run |

---

## 1. The table-split direction — INDEPENDENT ADJUDICATION (highest value)

This was the dispute between two prior decode reads. I decoded the raw words three
places and they agree; the implementation's direction is **correct**.

### 1a. The CalcHappy gate (raw words at file offsets, decoded by hand + `ppc_decode.py`)

`r251-disasm-calchappy.txt`:

```
0x10b9c8: lha   r4, 0x600(r3)   ; r4 = (short) person[1536]
0x10b9d0: extsh. r0, r4          ; CR0 set on sign-extended r4
0x10b9d8: beq   0x10b9e8         ; if person[1536]==0  -> skip, r5 stays 0
0x10b9dc: cmpwi r4, 0x12         ; compare with 18
0x10b9e0: bge   0x10b9e8         ; if person[1536]>=18 -> skip, r5 stays 0
0x10b9e4: li    r5, 1            ; ONLY reached for person[1536] in [1,17] -> r5=1
0x10b9e8: clrlwi. r0, r5, 0x18   ; r0 = r5 & 0xff ; CR0[EQ] set iff r5==0
0x10b9ec: beq   0x10b9f8         ; r5==0 (adult path) -> load table at TOC-0x5900
0x10b9f0: lwz   r4, -0x58fc(r2)  ; r5!=0 (child path) -> load table at TOC-0x58fc
0x10b9f8: lwz   r4, -0x5900(r2)  ; adult table
```

So the gate is: **`person[1536]` in `[1,17]` → table at `-0x58fc`; `==0` or `>=18` →
table at `-0x5900`.** Verified word values via `moodlaw-verify.py` (11 words, all match).

### 1b. Which physical resource lives in each TOC slot — `cXPerson::Initialize`

Decoded `cXPerson::Initialize` (file `0x111cd0`) around the two `MotiveCurveSet::LoadFromFile`
calls (`bl 0x9f500`, the curve loader):

```
0x111e78: lwz   r27, -22784(r2)   ; r27 = *[TOC-0x5900]   (22784 = 0x5900)
   ... loop allocates the table entries (bl 0x117d70) ...
0x111ed8: addi  r3, r27, 0        ; r3 = the -0x5900 table
0x111edc: addi  r5, r0, 502       ; resource id 502
0x111ee0: bl    0x9f500           ; LoadFromFile(table[-0x5900], 502)   -> STR#502

0x111ee8: lwz   r27, -22780(r2)   ; r27 = *[TOC-0x58fc]   (22780 = 0x58fc)
   ... loop allocates the table entries ...
0x111f3c: addi  r3, r27, 0        ; r3 = the -0x58fc table
0x111f40: addi  r5, r0, 504       ; resource id 504
0x111f44: bl    0x9f500           ; LoadFromFile(table[-0x58fc], 504)   -> STR#504
```

**Therefore `TOC-0x5900` = STR#502 (adult curves, `HappyWeight`) and
`TOC-0x58fc` = STR#504 (child curves, `HappyWeightChild`).**

### 1c. Conclusion

| person[1536] | r5 | TOC slot | resource | port field |
|---|---|---|---|---|
| 1..17 (child) | 1 | `-0x58fc` | **STR#504** | `HappyWeightChild` |
| 0 or ≥18 (adult) | 0 | `-0x5900` | **STR#502** | `HappyWeight` |

This **exactly** matches `implementation.md` §3 and the engine/harness code. Both prior
readings were wrong (one swapped A/B labels; one wrongly singled out `==0`, misreading
the `bge` as a fall-through). The implementation is correct. Note: even if this had been
reversed, the pins would stay self-consistent (engine and harness use the same split) —
but the port would then have given children adult curves. It does not; it's right.

Cross-corroboration: `WorldGlobalProvider.InitCurves()` binds `HappyWeight = Get<STR>(502)`
and `HappyWeightChild = Get<STR>(504)` (`FreeSO/TSOClient/tso.content/WorldGlobalProvider.cs:67,69`),
which is the same resource->field pairing. And `VMFindBestAction.cs:544` already uses the
identical `age > 0 && age < 0x12` child test, so the port's `PersonData[58]` field is the
established `person+1536` value.

---

## 2. Engine `ComputeMood` — PASS (with 2 minor nits)

`FreeSO/TSOClient/tso.simantics/Entities/VMTS1MotiveDecay.cs`.

Verified:
- **Formula** `Σ(wᵢ·mᵢ + W_room·room) / Σ(wᵢ + W_room)` over the 8 participants — correct
  (lines 176-200). Matches native `fdivs f0,f6,f5` (0x10bb14 / `stfs` 0x10bb18).
- **Curve per motive**: `MoodOrderMotives = {Hunger, Energy, Comfort, Fun, Hygiene, Social,
  Bladder}` (lines 31-35), which is the native table order `(7,5,6,15,8,14,9,13)` and the
  STR#502/504 index order (curve[0]=Hunger … curve[6]=Bladder). Verified against the pinned
  rodata `0x5a45b8`.
- **Table split**: `child = (age>=1 && age<0x12)` → `HappyWeightChild`, else `HappyWeight`
  (lines 181-183). Correct per §1c.
- **Room**: scalar `RoomWeight=1f` (line 36), documented default, disclosed. Correct per
  the task's ground-truth fact #5.
- **Diff is scoped**: only the aggregation changed. The 7-motive decay loop and the
  `minutes/2 == LastMinute` cadence gate are byte-for-byte untouched (confirmed by `git diff`:
  only `moodSum`/`/8` removed and `ComputeMood`/constants added).
- **No smoothing**: the store is the recomputed target directly (line 165).

### Nit A — `global == null → return 0` (line 180)
`Content.Content.Get().WorldObjectGlobals` is a public field assigned in the `Content`
constructor (`tso.content/Content.cs:171`), so it never returns null in a running sim.
The guard is effectively dead. But **if it ever did**, it would silently pin every sim to
mood **0** (neutral) with no diagnostic — a silent-fault mask rather than a loud failure.
Recommendation: log a warning / throw, or fall through to the equal-weight path with a
warning, instead of returning a silent neutral. **Not a blocker** (unreachable).

### Nit B — `curves == null` / short `curves.Length` fall to `w = 1.0` (lines 189-191)
If `HappyWeight`/`HappyWeightChild` is null (curves not loaded) the loop silently uses an
**equal-weight** law. That diverges from the decoded law with no assertion. Because the
harness has the identical guard, the pin stays self-consistent, but the port would diverge
from native. `InitCurves()` runs during Content init (before any sim), so it does not fire in
practice. **Not a blocker**; consider asserting `curves.Length == 7`.

### `den <= 0 → return 0` (line 198)
`den` always includes `RoomWeight = 1f`, so `den >= 1` for any non-negative weights; the guard
is dead unless a curve returns a negative weight (the STR#502/504 minima are all ≥1). Harmless.

---

## 3. Engine-vs-harness curve mapping — PASS (identical)

Engine iterates `MoodOrderMotives` (Hunger, Energy, Comfort, Fun, Hygiene, Social, Bladder).
Harness `ComputeMoodLaw` (`AutotestRunner.cs:1653`) iterates `DecayMotives`
(Hunger, Comfort, Hygiene, Bladder, Energy, Fun, Social) with `map = {0,2,4,6,1,3,5}`.

| row | harness motive | map[i] | engine curve | motive |
|---|---|---|---|---|
| 0 | Hunger | 0 | curve[0] | Hunger |
| 1 | Comfort | 2 | curve[2] | Comfort |
| 2 | Hygiene | 4 | curve[4] | Hygiene |
| 3 | Bladder | 6 | curve[6] | Bladder |
| 4 | Energy | 1 | curve[1] | Energy |
| 5 | Fun | 3 | curve[3] | Fun |
| 6 | Social | 5 | curve[5] | Social |

This reproduces the engine's per-motive curve **exactly**. No mismatch. The `mood`/`moodlaw`
pins use the same curve-per-motive as the engine. (Mathematically verified by substitution.)

---

## 4. Pin repoint — PASS (corrective, not weakened)

`MoodMatches` (`AutotestRunner.cs:1683-1688`) now asserts
`|stored − ComputeMoodLaw(row)| <= 3` instead of `/8`. This is a **correction**, not a
weakening: the old `/8` asserted the known-wrong formula.

### Is tolerance 3 justified? Yes.
- **float→short**: both the engine and `ComputeMoodLaw` cast `(short)(num/den)` the same way,
  so rounding cancels out of the comparison; tolerance 3 is well above the residual.
- **Between-recompute staleness**: the stored mood is recomputed every 2 game-min; the harness
  recomputes the law from current motives each frame. Per-motive decay over a 2-min window is
  ≤ ~0.6 points (comfort active: `ToFixed1000(0.4)=400` → 0.4; energy 375→0.375; hunger 2·(100+h)/1000).
  The weighted mood shift from any one motive is `w·Δ / Σw`; with `Σw ≥ 10` and `w ≤ 5`,
  the worst-case drift is ≈ `5·0.6/13 ≈ 0.23` points. Far inside 3.
- **Room**: `RoomWeight=1` with `Σw ≥ 10`, so a room change of **>30 points** in 2 game-min
  would be needed to break tolerance 3. Not typical.
- **Not more fragile than `/8`**: when motives are low, the weighted law's *larger* denominator
  makes the mood *less* volatile. So the repoint is strictly more faithful and not more fragile.

The `mood` default pin is an **internal-consistency** check (engine stored mood ↔ decoded law),
which is exactly what validates this change. It does **not** (and cannot) check parity with the
original save — correct per r251's state-dependence finding.

---

## 5. Regression to other checks — PASS

- **Death-reach mood-align** (`AutotestRunner.cs:2200-2205`): now
  `ComputeMoodLaw(SnapshotAvatar(aC))`, clamped. Correct. It fires only after `MoodGateDone`
  is set (line 2252), i.e. after the `mood` pin completes, so it cannot disturb the `mood` pin.
- **`SnapshotAvatar` 10-element row** (line 1637): `[7 motives, room, mood, age]`. The only
  consumers (`ComputeMoodLaw`, `MoodMatches`, `AllMoodOk`, death-reach) all index via
  `DecayMotives.Length` constants; none assume length 9. `ComputeMoodLaw`/`MoodMatches` guard
  `row.Length < DecayMotives.Length + 3`. No break.
- **`moodAllOk` diagnostic** (2151-2160): logs `ComputeMoodLaw` but does **not** call `Fail`.
  The real `mood` pass/fail is `AllMoodOk()` over `_samples` (line 2251) — consistent.
- **`motive` pin** (2254-2268): reads `DecayMotives` indices only; unaffected.

### Nit C — stale comment (AutotestRunner.cs ~2184)
The death-reach header still says *"Engine recomputes stored Mood each decay tick to
(sum+room)/8"* (line 2184). That is now **false** (engine uses the weighted law). Comment-only; no
functional effect, but it misrepresents the engine.

### Nit D — misleading diagnostic label (line 2145)
`hypEngine = (short)((s7 + rawRoom)/8)` is labeled as if it were the engine's formula. After
this change it is the *old* formula shown for comparison. It's honest evidence (both `hypEngine`
and `hypLaw` are printed side-by-side), but "hypEngine" now reads as if the engine still uses `/8`.
Low impact.

---

## 6. `moodlaw` opt-in check — PASS (one timing nit)

`AutotestRunner.cs:1712-1772`. Not in the default `Config.Checks` string (line 75 unchanged;
the diff touches only line 662 gate and adds the check at 2177).

Verified numerically — I re-derived the weighted law from the STR#502 curves:

| room | moodA | moodB | Δ |
|---|---|---|---|
| 0 | 56.667 | 59.770 | 3.103 |
| 50 | 60.543 | 63.606 | 3.064 |
| -20 | 55.116 | 58.235 | 3.119 |
| 80 | 62.868 | 65.908 | 3.040 |

So `implementation.md`'s "moodA≈60.5 / moodB≈63.6 (Δ≈3)" is correct (for room=50), and the
Δ stays in **[3.02, 3.12]** across any nominal room. The check's `|moodB − moodA| > 1`
assertion (line 1761) is satisfied with ~3x margin, so it won't falsely fail on that term.
Phase B correctly demonstrates **no smoothing**: the mood reaches law(B) on the next recompute
(instead of lagging toward it), and the step change moved it by >3.

### Nit E — sub-frame timing race (opt-in only)
The Phase A/B assertion is a **one-shot** at the first frame where `minute − setMinute >= 2`.
If the autotest tick is ordered *before* the engine's recompute in that game-minute, the stored
mood is still the pre-boundary value and the assertion could false-fail. The `_moodLawSetMinute`
"wait for a recompute boundary" is a heuristic (integer `minutes/2` boundary) that assumes the
mood recompute has already run. It also doesn't guard `avatar.Hidden` (the TS1 `Tick` returns
early without recomputing if `Hidden > 0`, line 85). Given this check is opt-in and the fixture
avatar is not hidden, this is a robustness note, not a bug.

---

## 7. `VMAvatarMotiveDecay` (TSO path) — PASS

`FreeSO/TSOClient/tso.simantics/Entities/VMAvatarMotiveDecay.cs:156` still computes
`(short)(moodSum / 8)` and is untouched. The only `SetMotiveData(VMMotive.Mood, ...)` in the
TS1 path is `VMTS1MotiveDecay.cs:165`. The engine change is correctly **scoped to TS1**.

---

## 8. "DLLs restored" / build-artifact cleanliness — CAVEAT (must rebuild)

Target: `./tools/run-autotest.sh` runs `dist/The Sims-arm64.app/Contents/MacOS/TheSims`.

Findings:
- Only source files modified (`git status`): `FreeSO/.../VMTS1MotiveDecay.cs` and
  `Client/.../AutotestRunner.cs`, plus the untracked `tools/iff-dump/r251-mood-cadence/` evidence.
  Working tree is clean/buildable at the source level.
- `dist` and `publish` are git-ignored build artifacts.
- **The agent's "restored the original DLLs" claim is accurate.** Byte-scan of the deployed
  assemblies:
  - `dist/The Sims-arm64.app/Contents/MacOS/Simitone.Client.dll` (mtime Sep 8 03:21) contains
    **old** literals `hypEngine`, `moodAllOk`, `AUTOTEST mood: MISMATCH`, and **no** new
    `moodlaw`/`hypLaw`/`lawTarget`/`ComputeMoodLaw`.
  - `dist/.../FSO.SimAntics.dll` (Sep 8 03:21) has **no** `ComputeMood`/`MoodOrderMotives`
    (the `HappyWeight`/`HappyWeightChild` strings it has come from pre-existing free-will code,
    not R251).
  - The nested `Simitone.Desktop.app/Contents/MacOS/Simitone.Client.dll` (Sep 7 20:13) is the
    same (old).
- **Consequence:** the currently-deployed `dist/The Sims-arm64.app` **does not contain the R251
  change**. A verify run launched against it as-is would exercise the **old `/8`** code: the `mood`
  pin would run the old self-consistent `/8` check, and the `moodlaw` opt-in symbol would be absent.
- **Risk for the run I'm about to verify:** if it used the dist app without a rebuild, its results
  would **not** reflect R251. **A rebuild (or staging the freshly-built `Simitone.Client.dll` +
  `FSO.SimAntics.dll`) is required before trusting an autotest verification.**
- The restored build is internally consistent in *content* (all assemblies are the pre-R251 build);
  the mtime skew (two files at 03:21, the rest at 20:13) is cosmetic — it simply reflects that the
  restore touched exactly the two assemblies that had been rebuilt.

---

## 9. Honesty of `implementation.md` — PASS

- Room weight: explicitly labeled a "DOCUMENTED BEST-SUPPORTED DEFAULT" and "could NOT pin…
  NON-authoritative" (§2). Not presented as proven. Matches the ground-truth fact #5 requirement.
- Table split: presented as "RECONCILED from raw hex" with the word-by-word gate — and it is
  correct (§1).
- Mapping: labeled "verified against the extracted Global.iff STR# 502/504" — correct.
- Default `Config.Checks` "stays byte-identical": confirmed (no diff to line 75).
- The only honesty issue is the stale `/8` comment in the death-reach block (§5, Nit C) and the
  `hypEngine` label (§5, Nit D) — both in *code comments*, not `implementation.md`.

---

## 10. What I could NOT verify

- **The native Room weight.** Confirmed non-pinnable statically (pin-verify.py §5: table slots
  resolve to BSS `0x858b8/0x85958` beyond the unpacked init data; knot arrays are heap-allocated
  at runtime from STR#502). `RoomWeight=1` is a documented default. I could not confirm it is
  correct vs the native; a live trace of `CalcHappy` would be needed. Note: the native *does*
  evaluate a knot array for the Room entry (0x10ba34 path), so the native Room weight may be a
  curve or `[r8+4]`(0.0) rather than the constant 1 — my analysis says 1 is a plausible default,
  not a proof.
- **The suit gate / `[r8+0x30]`=13.0 behavior** is not modeled in the port. The native drops a
  motive exactly equal to 13.0 for suits 2-5. The port always accumulates all motives (suit 0/1
  behavior). This is a known divergence, negligible in practice (measure-zero value; standard
  suits are 0/1), and outside this round's law definition — but it is unmodeled.
- **Whether the `mood` default pin passes end-to-end on a real soak.** Statically the drift is
  <0.3 pts (well inside 3) and the engine/harness compute the same law, so it should pass — but I
  could not run the soak (the deployed app is the old build; a rebuild is needed first).
- The absolute wall-clock unit of the native 60-unit cadence (unresolvable statically, per
  pin.md §6) — though the port keeps its own 2-game-minute cadence, which is accepted as a port
  convention per the task.

---

## 11. Prioritized follow-ups

1. **Rebuild before any autotest verification.** The deployed dist app is the pre-R251 build
   (§8); a run against it is meaningless for this change. (**FIX-FIRST for the verification
   workflow, not the code.**)
2. (*optional*) Fix the stale `(sum+room)/8` comment in the death-reach block (line ~2182) and
   relabel `hypEngine` (line 2145).
3. (*optional*) Replace the `global == null → return 0` and `curves == null → w=1.0` silent
   fallbacks with a warning/assert (lines 180, 189-191) so a missing-curves fault is loud.
4. (*optional*) Harden the opt-in `moodlaw` boundary wait against sub-frame ordering and the
   `Hidden` avatar (lines 1729, 1749).
