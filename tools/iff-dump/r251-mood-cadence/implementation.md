# R251 — mood law implementation round (Room weight decode + table split + pin repoint)

This is the implementation round that the r251 decode round prepared. The mood
aggregation law was recovered by r251 (`decode.md`) and is authoritative here:

    mood = Σ(wᵢ·mᵢ) / Σwᵢ   over the 8 mood participants, NO smoothing.

The STR# weight curves (`HappyWeight`/`HappyWeightChild`) were already loaded by
the port but were NOT wired into the mood path (the port computed the old
equal-weight `(Σ7 + room) / 8`). This round wires them in, pins the Room
weight, reconciles the child/adult table split, repoints the affected autotest
pins, and adds an opt-in `moodlaw` check.

Source binary re-pinned (matches `decode.md`/`verify.py`):
SHA256 `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`
(`game-data/The Sims/The Sims Complete`, PEF for PowerPC Mac).

---

## 1. The new mood formula (as implemented)

`FreeSO/TSOClient/tso.simantics/Entities/VMTS1MotiveDecay.cs`,
`ComputeMood(VMAvatar, int roomScore)`, called from `Tick` on the existing
2-game-minute cadence (the `minutes/2 == LastMinute` gate is unchanged):

```csharp
// Over MoodOrderMotives = {Hunger, Energy, Comfort, Fun, Hygiene, Social, Bladder},
// in the native CalcHappy table order (rodata 0x5a45b8 = 7,5,6,15,8,14,9,13):
double num = 0, den = 0;
for (int i = 0; i < MoodOrderMotives.Length; i++) {
    var m = avatar.GetMotiveData(MoodOrderMotives[i]);
    double w = 1.0;
    if (curves != null && i < curves.Length) w = curves[i].GetPoint((float)m);
    num += w * m;  den += w;
}
num += RoomWeight * room;  den += RoomWeight;          // Room, constant weight
return (short)(num / den);
```

- `curves` is `WorldGlobalProvider.HappyWeight` (STR#502, adults) or
  `HappyWeightChild` (STR#504, children), selected by the table split (section 3).
- `curves[i]` is the STR#502/504 **weight curve #i**; `GetPoint(motive)` is the
  piecewise-lerped weight. The curve→motive mapping is the native table order
  (Hunger→curve[0], Energy→curve[1], Comfort→curve[2], Fun→curve[3],
  Hygiene→curve[4], Social→curve[5], Bladder→curve[6]; Room is scalar).
- `RoomWeight` is a scalar (see section 2).
- No smoothing: the store is the direct target, matching the native CalcHappy
  pure recompute (proven in `decode.md`).

The port's decay behaviour for the 7 decrementing motives is UNCHANGED (only the
final aggregation was replaced). `VMAvatarMotiveDecay` (the TSO path, unused in
TS1) still uses `/8` and was deliberately left alone.

---

## 2. Task A — the Room weight

### What is known (PROVEN)
STR#502 and STR#504 both contain **exactly 7** weight-curve strings. Verified by
extracting `Global.iff` from `GameData/Global/Global.far` and parsing the STR#
chunks (format code -3 / language code):

```
STR# 502: (-100;15) (-60;5) (-40;3) (0;1) (100;1)   [Hunger curve[0]]
          (-100;10) (-80;3) (-40;1) (100;1)         [Energy curve[1]]
          (-100;5)  (-80;3) (-40;1) (100;1)         [Comfort curve[2]]
          (-100;10) (-80;3) (-60;1) (100;1)         [Fun curve[3]]
          (-100;5)  (-40;2) (40;2) (100;5)          [Hygiene curve[4]]
          (-100;2)  (0;1)   (100;2)                 [Social curve[5]]
          (-100;5)  (-40;2) (40;2) (100;3)          [Bladder curve[6]]
STR# 504: same 7, but Hygiene is (-100;6) (-40;2) (40;2) (100;6)
```

The native CalcHappy table-order array (rodata file 0x5a45b8, 8 × 32-bit) is
`(7,5,6,15,8,14,9,13)` = Hunger, Energy, Comfort, Fun, Hygiene, Social, Bladder,
Room — eight participants. The 8th (Room, motive index 13) is NOT covered by any
of the 7 STR curves, so the Room weight is a **scalar**, not a curve.

### Why it could NOT be pinned exactly
1. The TOC-relative pointer slots that drive the table (`lwz r4, -0x5900(r2)` /
   `-0x58fc(r2)` in CalcHappy; `lwz r27, -22784/-22780(r2)` in
   `cXPerson::Initialize`) point at **unrelocated placeholder** values (the r251
   skeptic notes §4: dereferencing them yields string data like "rites" /
   "cWinTransformMeDlg", not a `{entries,count}` struct). So the table count and
   the 8th entry's knot data cannot be resolved statically.
2. The native saved moods are **state-dependent** (proven by r251: Bob
   74.073→57.385 and Betty 75.118→60.511 with byte-identical 8 inputs), so a
   single `mood = f(inputs)` does NOT hold — you cannot fit a consistent Room
   weight from the save tuples (the solve is ill-posed; fitting Bob's *new* mood
   yields a nonsensical W≈57 because the stored mood is not `f(inputs)`).

### The decision (documented default, flagged honestly)
I could NOT pin the native Room weight. Per the task's fallback instruction, I
used **`RoomWeight = 1`** — the value that makes Room contribute comparably to a
motive whose curve weight is 1 (the flat/satiated part of every curve). It is
kept identical in the engine (`VMTS1MotiveDecay.RoomWeight = 1f`) and the harness
(`AutotestRunner.MoodRoomWeight = 1f`) so the pins and the engine agree.

R223's least-squares single-subset fit used a room weight of **8**; that was a
subset guess on state-dependent data and is **NOT** treated as authoritative. A
direct solve against Bob's new mood gives ~57, which is also non-authoritative
(the mood is not a pure function of the inputs). Neither is used.

---

## 3. Task A.2 — the child/adult table split (RECONCILED from raw hex)

The prior decode conflict (prior agent: "person[1536] in [1,17] → A, {0,≥18} → B";
hand re-decode: "==0 → B, else A") is resolved. The raw words are authoritative.
`CalcHappy` gate (r251 `r251-disasm-calchappy.txt`, decoded with the repo's
`ppc_decode.py`, which was branch-corrected in r251):

```
0x10b9c8: lha   r4, 0x600(r3)     ; r4 = person[1536]  (signed halfword)
0x10b9d8: beq   0x10b9e8          ; if person[1536] == 0 -> r5 stays 0 (adult path)
0x10b9dc: cmpwi r4, 0x12          ; compare with 18
0x10b9e0: bge   0x10b9e8          ; if person[1536] >= 18 -> r5 stays 0 (adult path)
0x10b9e4: li    r5, 1             ; only reached for person[1536] in [1,17] -> r5 = 1
0x10b9e8: rlwinm r0, r5, 0x18      ; r0 = r5 & 0xff
0x10b9ec: beq   0x10b9f8          ; if r5 == 0 -> load table A (0x10b9f8)
0x10b9f0: lwz   r4, -0x58fc(r2)   ; r5 != 0 (child) -> table B
0x10b9f4: b     0x10b9fc
0x10b9f8: lwz   r4, -0x5900(r2)   ; r5 == 0 (adult) -> table A
```

So:
- **`person[1536]` in [1,17] (child) → `-0x58fc` table → STR#504 (`HappyWeightChild`).**
- **`person[1536]` == 0 or >= 18 (adult/other) → `-0x5900` table → STR#502 (`HappyWeight`).**

Cross-checked against `cXPerson::Initialize` (0x111cd0), which calls
`MotiveCurveSet::LoadFromFile` exactly twice:
- `0x111edc: addi r5, r0, 502` on `r27 = lwz -22784(r2)` → STR#502 → the `-0x5900` slot.
- `0x111f40: addi r5, r0, 504` on `r27 = lwz -22780(r2)` → STR#504 → the `-0x58fc` slot.

`-22784 = -0x5900`, `-22780 = -0x58fc` — SAME slots CalcHappy loads for adult
(r5==0) vs child (r5!=0). Therefore **children use STR#504 (child curves),
adults use STR#502 (adult curves).** Both prior readings were wrong (one reversed
the A/B labels; the other wrongly singled out `==0`).

Port mapping: the native `person[1536]` field surfaces as
`VMPersonDataVariable.PersonsAge` (attr 58). The port already uses the identical
child test in `VMFindBestAction` (`IsChild = PersonsAge > 0 && PersonsAge < 0x12`),
which matches the native gate. The engine reads the same field in `ComputeMood`.

---

## 4. Task C — autotest pin repoint + `moodlaw`

`Client/Simitone/Simitone.Client/AutotestRunner.cs`:

- **`SnapshotAvatar`** now appends the age (person+1536) to each row so `MoodMatches`
  can select the adult/child table. Row = `[7 decay motives, raw room, stored mood, age]`.
- **`ComputeMoodLaw(short[] row)`** returns the SAME weighted-average short the
  engine stores. It maps the `DecayMotives` order (Hunger, Comfort, Hygiene,
  Bladder, Energy, Fun, Social) to the curve order via
  `{0,2,4,6,1,3,5}`, selects adult/child by `age` in [1,17], and applies
  `MoodRoomWeight = 1f` to the room.
- **`MoodMatches`** now asserts `|storedMood - ComputeMoodLaw(row)| <= 3`
  (tick-boundary tolerance). **This is a CORRECTIVE repoint**, not a weakening:
  the old pin asserted the KNOWN-WRONG equal-weight `/8` approximation; the new
  pin asserts the correctly-decoded weighted law. It is not a tolerance change
  (the 3-point tick-boundary tolerance is unchanged, and both engine and pin now
  compute the same float → short truncation).
- The **mood load-phase diagnostic** and the **`moodAllOk`** block now log the
  weighted target (`hypLaw`/`computed`) via `ComputeMoodLaw` instead of `/8`.
- The **death-reach mood-align** block now aligns the stored mood with the new
  weighted law (`ComputeMoodLaw(SnapshotAvatar(aC))`) so the mood gate stays
  consistent.
- **`moodlaw`** opt-in check (NOT in the default `Config.Checks` string, which
  stays byte-identical; added only to the state-2 gate list). It drives a known
  motive state (re-pinned every frame so decay has no net effect) in two phases:
  - **Phase A** (`{30,60,80,20,70,40,90}` in DecayMotives order) asserts
    `|storedMood - ComputeMoodLaw(row)| <= 3` (aggregation law).
  - **Phase B** (step change `{10,60,80,90,70,40,20}`) asserts the new target is
    reached on the next recompute AND that the mood moved by more than 1 point
    (`|moodB - moodA| > 1`) — proving NO smoothing (a lerp would lag; a pure
    recompute follows immediately). A straight Python check of the law on these
    states shows moodA≈60.5 / moodB≈63.6 (Δ≈3) for any nominal room, so the
    step change is well within the assertion.

Only the default-`mood` check path and the equivalent `moodlaw` assertion use the
new law; every other default pin (motive, brainlive, deathchain, …) does not read
the mood value and is unaffected.

---

## 5. Files changed

| File | Change |
|---|---|
| `FreeSO/TSOClient/tso.simantics/Entities/VMTS1MotiveDecay.cs` | `ComputeMood` + `MoodOrderMotives` + `RoomWeight` + table split; `/8` removed |
| `Client/Simitone/Simitone.Client/AutotestRunner.cs` | `SnapshotAvatar` (+age), `ComputeMoodLaw`, `MoodMatches` repoint, moodAllOk/load/diagnostic + death-reach mood-align, `moodlaw` opt-in check, state-2 gate |

## 6. Evidence / verification

- STR#502/504 extraction and the moodlaw numeric check: `moodlaw-verify.py`.
- CPU decoding of the CalcHappy gate words and the table-order array: see the
  verify-script assertions in `moodlaw-verify.py`.
