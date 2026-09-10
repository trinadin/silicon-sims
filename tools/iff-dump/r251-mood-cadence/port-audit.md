# R251 — Port-side mood audit (mood-half parity prep)

Read together with `tools/iff-dump/r223/r223-mood-room-law.md` (the native-law
evidence round). This is an INVESTIGATION-ONLY audit of the port's mood path.
Nothing below modifies production source. It maps what the port computes today,
where it diverges from the decoded native law, what data is reachable, and —
the core deliverable — a concrete fixture/test plan plus a proposed engine seam
so the decode agent's findings can land without integration drift.

All paths below are relative to the repo root
`/Users/nathannoom/Developer/Games/The Sims/simitone-fork`.

---

## 0. TL;DR (what the caller needs first)

**Current port formula & cadence.** The port's mood is computed in
`FreeSO/TSOClient/tso.simantics/Entities/VMTS1MotiveDecay.cs`
(VMAvatarMotiveDecay.cs is the TSO path, not used in TS1). In `Tick()`, every
**2 game-minutes** it computes:

```
moodSum = Hunger + Comfort + Hygiene + Bladder + Energy + Fun + Social   (post-decay short values)
moodSum += roomScore                                                     (raw, unclamped)
Mood    = (short)( moodSum / 8 )                                          // integer-division, truncates toward 0
```

That is a **pure, equal-weight average of 8 items (7 motives + room)**, assigned
directly (no smoothing/blend/decay toward a target, no weight curves). The room
score (`context.GetRoomScore`) is re-read and stored into motive[13] **every
frame**, but the mood assignment and the motive decay both only run on the
`minutes/2` cadence (every 2 game-minutes).

**Top divergences from the native law** (r223 `cXPerson::CalcHappy @0x10b9c2`):

1. **No weight curves.** Native uses the seven STR#502 piecewise weight curves
   `wᵢ(mᵢ)` and a room weight, `mood = Σ(wᵢ·mᵢ)/Σwᵢ`. The port weights every
   motive 1.0 and uses a fixed denominator 8. The native denominator `Σwᵢ` is
   NOT constant (it varies with each motive's value), so even the *normalization*
   is wrong, not just the numerator.
2. **Integer storage, integer math.** Port keeps `MotiveData` as `short[16]` and
   truncates via `(moodSum / 8)`. Native stores a `float` (`stfs person+1944`).
   The port therefore cannot represent fractional mood (the native 74.073 /
   57.385 values) — this alone hides the "state-dependent" evidence in the saves.
3. **No smoothing / no target.** Native mood is *state-dependent*: identical 8
   inputs across the old/new save pair produce a *moved* mood (Bob 74.073→57.385,
   Betty 75.118→60.511). The port recomputes synchronously from the current
   motives, so it can only follow them exactly — it cannot reproduce a
   cadence/smoothing law.
4. **No per-person entry mask.** Native gates participation via `person+1550`
   (audience/type bitfield: bit0=cat, bit1=dog). The port's `VMTS1MotiveDecay`
   unconditionally sums all eight participants; it never reads a mask.
5. **Order differs, set is the same.** Both the native 8-set and the port's
   partition are {Hunger, Energy, Comfort, Fun, Hygiene, Social, Bladder, Room};
   only the order and the weights differ.

**Recommended seam signature.** Add a single method on `VMIMotiveDecay`
implemented by `VMTS1MotiveDecay` (see section 5). It must separate "derive the
target mood" from "advance the displayed mood toward it", because the decode
agent's law is state-dependent. Concretely:

```csharp
// Returns the STEADY-STATE mood target for the given inputs (STR#502 curves +
// room weight + entry mask) — a pure function of the inputs.
short ComputeMoodTarget(
    in ReadOnlySpan<short> motives,  // the 16-slot array (or the 8 participants)
    short roomScore,                  // raw (unclamped) room score
    ushort personMask,                // person+1550 audience/entry bits
    int personType);                  // adult/child/pet, selects the ghost/type weight table (person+1536)

// Advances the DISPLAYED mood toward the target at the recompute cadence.
short AdvanceMood(
    short currentMood,
    short targetMood,
    int elapsedSimMinutes);           // >= 1 on a recompute boundary (cadence)
```

The port's `Tick` should call `ComputeMoodTarget` on the cadence boundary, then
`AdvanceMood` to blend/step toward it. Both are trivially shimmable so the
decode agent's law can be dropped into `ComputeMoodTarget` / `AdvanceMood`
without touching the engine loop.

---

## 1. Current mood computation path (every production site)

There are exactly **two** production sites that write `VMMotive.Mood`, and only
one is reachable in the TS1 port.

### 1a. `VMTS1MotiveDecay.Tick` — the TS1 path (the one that matters)

File: `FreeSO/TSOClient/tso.simantics/Entities/VMTS1MotiveDecay.cs`

- Selected at runtime: `VMAvatar.cs:264`
  `MotiveDecay = (Content.Content.Get().TS1) ? (VMIMotiveDecay)new VMTS1MotiveDecay() : new VMAvatarMotiveDecay();`
  → TS1 always uses `VMTS1MotiveDecay`.
- Called from `VMAvatar.Tick` → `VMAvatar.cs:580` `MotiveDecay.Tick(this, Thread.Context);`
  (once per avatar per sim tick/frame).
- Formula (**lines 52–129**):

  ```csharp
  var roomScore = context.GetRoomScore(context.GetRoomAt(avatar.Position)); // line 45
  avatar.SetMotiveData(VMMotive.Room, roomScore);                            // line 46 (EVERY frame)
  var minutes = context.Clock.Minutes;                                       // line 47
  if (minutes/2 == LastMinute || avatar.GetValue(VMStackObjectVariable.Hidden) > 0) return; // line 48 (gate)
  ...
  int moodSum = 0;                                                           // line 52
  for (int i = 0; i < 7; i++) { ... moodSum += motive; }                    // lines 54–126 (post-decrement value)
  moodSum += roomScore;                                                      // line 127
  avatar.SetMotiveData(VMMotive.Mood, (short)(moodSum / 8));                // line 129
  ```

- **Cadence:** the room score is refreshed every frame (line 45–46, before the
  gate). The mood recompute + the motive decrement only execute when the
  `minutes/2` bucket changes (i.e. every 2 game-minutes) **and** the avatar is
  not hidden. `LastMinute` is set to `minutes/2` (line 50) after the gate, so the
  actual interval is 2 game-min.
- **Room participation:** yes — it reads a room score and adds it as one of the
  8 averaged terms.
- **Smoothing/blending?** **None.** `SetMotiveData(Mood, moodSum/8)` is a direct
  assignment. No lerp, no decay-toward-target, no history.

### 1b. `VMAvatarMotiveDecay.Tick` — the TSO/online path (NOT used in TS1)

File: `FreeSO/TSOClient/tso.simantics/Entities/VMAvatarMotiveDecay.cs`

- Same structure: **line 154** `moodSum += roomScore;`, **line 156**
  `avatar.SetMotiveData(VMMotive.Mood, (short)(moodSum / 8));`.
- Cadence gate: line 106 `if (context.Clock.Minutes == LastMinute) return;`
  → runs every **1 game-minute** (different cadence than the TS1 path's 2-min).
- Decay constants come from content tuning (`simmotives` / `lotmotives`), and the
  whole class is gated behind TS1=false. **Not relevant to the TS1 port** but
  noted because it is the second (and only other) production mood site, and it
  shares the same `moodSum/8` equal-weight approximation.

### 1c. Room score source

- `VMContext.GetRoomScore(ushort room)` — `VMContext.cs:1528`:
  returns `RoomInfo[RoomInfo[room].Room.LightBaseRoom].Light.RoomScore` (a `short`).
- Recalculated only when `RefreshRoomScore(room)` is invoked (on room-impact
  object changes / lighting refreshes), `VMContext.cs:704`. For **inside** rooms
  in TS1 it uses the decoded `ComputeRoomScoreOriginal` (`VMContext.cs:782`) with
  the disclosed approximation `RoomScoreK_Room108 = 2.05f` (`VMContext.cs:775`).
  For **outside** rooms it falls back to the old port formula
  (`Σ RoomImpact / max(1, area/12) − 10`, `/30 −15` outside) at `VMContext.cs:753–758`.
- So the room score is relatively stable (recomputed on object/lighting change),
  NOT re-derived every frame — but `MotiveDecay.Tick` re-reads and re-stores it
  into motive[13] every frame.

### 1d. Motive storage & accessor

File: `FreeSO/TSOClient/tso.simantics/Entities/VMAvatar.cs`

- `private short[] MotiveData = new short[16];` (**line 72**) — a **single** 16-slot
  short array. `VMMotive.Mood = 3` (`Model/VMMotive.cs:8`) so mood is slot 3.
- `GetMotiveData` (**line 1032**) returns `MotiveData[(ushort)variable]` — the
  comment "//needs special conditions for ones like Mood." is a *stub*, the
  implementation is a plain array read.
- `SetMotiveData` (**line 1038**) clamps to
  `max(old, TuningCache.GetLimit(variable) ?? 100)` upper, −100 lower. In TS1,
  `VMTuningCache.GetLimit` returns 100 (`VMTuningCache.cs:16`, `UpdateTuning`
  early-returns for TS1 leaving `MotiveOverfill` empty), so mood is clamped to
  [−100, 100]. **The clamp upper uses `max(old, limit)`** so "overfill" above 100
  is preserved if the old value was already above it.

**No `MotiveDataOld`.** The port has a single array; there is no old-vs-new
motive snapshot on the avatar. (The raw OBJM has both — see section 3.)

---

## 2. Native law → port equivalence map

| Native law piece (r223) | Port equivalent | Status |
|---|---|---|
| `mood = Σ(wᵢ·mᵢ)/Σwᵢ`, w from 7 STR#502 curves | `moodSum/8`, all w=1 | **WRONG** (no curves, fixed divisor) |
| Eight motives {7,5,6,15,8,14,9,13} = Hunger,Energy,Comfort,Fun,Hygiene,Social,Bladder,Room | Same 8-set as the `DecrementMotives`+Room partition | **OK** (set is correct) |
| Room is a mood participant | `moodSum += roomScore` | **OK** (participates, but raw w=1) |
| Per-person entry mask `person+1550` gates entries | **absent** — all 8 always summed | **MISSING** |
| Type split at `person+1536` (ghost/type → 2 weight tables) | **absent** — single path | **MISSING** |
| Float result `stfs person+1944` | `(short)(moodSum/8)` → integer truncation | **WRONG** (float→short loses fraction) |
| State-dependent (smoothed / slower cadence) | Direct synchronous recompute | **MISSING** (no smoothing/cadence law) |
| CalcHappy reached only via vtable (unknown cadence) | Recompute on `minutes/2` (2 game-min) | **UNKNOWN/mismatched** (decode agent to settle) |

What the port already gets right:
- The **participating motive set** (the 8) matches the native.
- **Room participates** in the sum.
- Mood is stored at motive slot 3, matching native `person+1944`.

What is missing / wrong:
- The **STR#502 weight curves** entirely (the core of the aggregation law).
- The **variable denominator** `Σwᵢ` (native) vs the fixed `/8`.
- The **per-person entry mask** and the **ghost/type weight-table split**.
- **Float** precision & the fractional part of mood (the port stores integers).
- Any **smoothing / cadence / target-convergence** behavior — the display value is
  recomputed fresh, so it cannot be "state-dependent" the way the native is.

---

## 3. Data availability

### Motive storage
- Port avatar lives in `short[16] MotiveData` (`VMAvatar.cs:72`); slot 3 = Mood.
- The autotest harness reaches it via `a.GetMotiveData(VMMotive)` /
  `a.SetMotiveData(VMMotive, short)`, and `a.ReplaceMotiveData(short[])`
  (`VMAvatar.cs:1048`).
- The 16-slot array and the VMMotive slot mapping are **available** in the harness.

### "Old vs new" motive snapshot
- **Not on the avatar** (single array, no `MotiveDataOld`).
- **Available at the raw OBJM/IFF layer**:
  `FreeSO/TSOClient/tso.files/Formats/IFF/Chunks/OBJM.cs:203–204` declares
  `public float[] MotiveDataOld; public float[] MotiveData;` (both `float[16]`,
  parsed at lines 257–269).
- The **import path discards the old state**:
  `FreeSO/TSOClient/tso.simantics/Utils/VMTS1ActivatorNew.cs:450–453`
  `MotiveData = person.MotiveData.Select(motive => (short)Math.Round(motive)).ToArray()`.
  `MotiveDataOld` is simply not carried over.
- The `tools/house_probe` tool reads **both** states statically
  (`tools/house_probe/Program.cs`, `--motives`), so the old/new pair is reachable
  *as evidence* from the staged saves even though the live engine discards it.

### Room score
- Stored into motive[13] = `VMMotive.Room` each tick (`VMTS1MotiveDecay.cs:46`);
  the underlying score is `VMContext.GetRoomScore` (`VMContext.cs:1528`).
- The **raw** (unclamped) score is what the port adds to `moodSum` (`VMTS1MotiveDecay.cs:127`);
  the **clamped** value lives in motive[13]. The autotest already distinguishes
  these: `SnapshotRoom` (`AutotestRunner.cs:1608`) uses the raw `GetRoomScore`
  path, `GetMotiveData_room` (`AutotestRunner.cs:1616`) reads the clamped motive.
- **Reachable** in the harness (`_vm.Context.GetRoomScore(GetRoomAt(pos))`).
- Room score is recomputed only on object/lighting-change events, so it is
  effectively stable over a short fixture window unless test objects are moved.

### Per-person entry mask / personality flags
- **There is no dedicated "entry mask" field** consumed by the mood path. The
  nearest raw-native-address mapping: the person word layout comment
  (`Model/VMPersonDataVariable.cs:18–19`, R151) says person word N sits at
  `cXPerson + 0x58c + 2N`. `person+1550` ⇒ word index (1550−1420)/2 = **65** ⇒
  `VMPersonDataVariable.Gender` (cat/dog are encoded as gender 8/9/16/17 in the
  port, see `VMAvatar.cs:369–372`). So the **type/audience bits are present in
  the model** (`PersonType=32`, `Gender=65`) but are **not hooked into the mood
  formula** anywhere.
- The personality traits the port DOES read in the mood path are only used for
  **decay rates**, not for an entry mask: `ActivePersonality` (line 66) and
  `OutgoingPersonality` (line 107). Neither gates mood participation.

---

## 4. Fixture / autotest plan

### 4a. Where it registers

- The harness is `Client/Simitone/Simitone.Client/AutotestRunner.cs`. It is
  gated by `CheckEnabled(name)` (`AutotestRunner.cs:241`) reading `Config.Checks`.
- **The default `Config.Checks` string must stay byte-identical**
  (`AutotestRunner.cs:75`, currently `"corpus,lot,motive,mood,load,..."`). Both the
  existing `mood` and `roomlaw` checks are already in that default string, and
  `mood` is the **current** pin that asserts `Mood === (Σ7 + room)/8`
  (`MoodMatches`, `AutotestRunner.cs:1632`).
- A **new check must be additive/opt-in** — e.g. add a distinct name like
  `moodlaw` (or `moodcadence`) and gate it with `CheckEnabled("moodlaw")`, NOT by
  editing the default string. This mirrors the established pattern for focused
  checks (`freewillwin` → `AutotestFreeWillWin249.cs`, which is opt-in and not in
  the default list).
- Verdict conventions: `Pass("moodlaw")` / `Fail("moodlaw")`
  (`AutotestRunner.cs:22424/22430`) emit `AUTOTEST moodlaw: PASS/FAIL`; the game
  exits 0 only when every **enabled** check passes.
- The harness already has the machinery: it loads a house via `PlayHouse`,
  reaches `StateWaitLot` (`AutotestRunner.cs:448`), populates `_vm` and `_avatars`,
  runs on `GameThread.EveryUpdate`, and samples per frame. The `mood` check's
  window (`minute - _motiveStartMinute >= 10`, line 2111) shows how the harness
  advances and samples over real sim time.

### 4b. Inputs the fixture needs and whether they are reachable

| Input | Source | Reachable from harness? |
|---|---|---|
| The 16 motive values | `a.SetMotiveData`/`GetMotiveData` | **Yes** (`_avatars`, `_vm`) |
| A frozen motive state (step change) | pin via `SetMotiveData` each frame | **Yes** (death-reach collapse precedent, line 2060) |
| Room score | `_vm.Context.GetRoomScore(GetRoomAt(pos))` | **Yes** |
| Person type / audience bits | `a.GetPersonData(PersonType=32)` / `(Gender=65)` | **Yes** |
| STR#502 weight curves | `_vm.Context.Global.Resource.Get<STR>(502)`; iterate `GetString(0..6)` | **Yes** — Global.iff is loaded (STR 301/304/305 already read at runtime); STR 502 is the 7 curve strings, format "(x;w) (x;w) ..." (r223). |
| Elapsed sim time / cadence | `_vm.Context.Clock.Minutes`, `_vm.Context.Clock.TicksPerMinute` | **Yes** |
| The old-vs-new native save pair | `tools/house_probe --motives` on staged `HouseNN.iff` | **Yes as evidence** (static, not the live engine — see §3) |

### 4c. Concrete fixture assertions

**Goal: exercise BOTH the aggregation law AND the cadence/smoothing law over
time, not a single value.**

Design a two-phase soak inside the standard 10-game-minute window:

1. **Pin a controlled motive state and re-pin it every frame** so the decay
   tick has no net effect (motives stay constant). Pin the 8 participants to a
   known, non-degenerate set (e.g. `Hunger=30, Energy=60, Comfort=80, Fun=20,
   Hygiene=70, Social=40, Bladder=90`), and freeze room by not disturbing the
   room (or by asserting the room score is constant across the window).

2. **Phase A — steady-state law (aggregation).** After the re-pin has settled
   (skip ≥2 recompute boundaries), assert the stored mood equals the **decoded
   weighted target**, not the `/8` average:
   `Mood_ss ≈ Σ(wᵢ·mᵢ)/Σwᵢ` with `w` from STR#502 and the room's weight curve,
   i.e. **assert the port's formula is no longer `(Σ7+room)/8`**. Gate this behind
   the decoded target value being available (see §5 seam). Tolerance ~1–2.

3. **Phase B — cadence/smoothing (trajectory).** Apply a step change to the
   pinned motives (e.g. drop Hunger 30→10 and raise Fun 20→90) and capture the
   stored mood on **every recompute boundary** (every 2 game-minutes, or whatever
   the decode agent proves is the native cadence). Assert:
   - The mood does **NOT** jump to the new steady-state in a single boundary
     (that would disprove the state-dependent law). 
   - Instead it **monotonically converges** toward the new SSR target over the
     decoded number of cadence steps / time constant, within a tolerance band.
   - If the native law is a fixed smoothing/`lerp` or recompute-cadence, assert
     the *number of boundaries* to reach ~95% of the target matches the decoded law.

This is the only design that distinguishes "pure recompute" (port today) from
"state-dependent" (native), which is exactly what r223's old/new save pair
demonstrates (identical inputs, moved mood).

**Also assert** (mask behavior, once the law lands): toggling the person's
type/audience bits (`PersonType`/`Gender`) changes the participating set — i.e.
certain motives stop contributing when the entry-mask bit is set, matching the
native `person+1550` parity ops at `0x10bad4–0x10bb04`. This requires the port
to first *expose* the mask (it does not today).

### 4d. Fixture construction options

- **Option 1 (recommended): direct motive pinning in the harness** — the
  established, deterministic route (same idiom as the death-reach collapse at
  `AutotestRunner.cs:2060`). Freeze motives each frame, drive the clock, sample
  per recompute boundary. No save-file wrangling, fully controllable inputs. Use
  house 5 (Goth) or house 7 (the r223 fixture house) for room score determinism.
- **Option 2: staged original save through `house_probe`** — load the existing
  staged `House05/House07` saves and replay the first ticks. Good for *evidence*
  (the old/new pair is already byte-identical on inputs and moved on mood), but
  the live port **discards `MotiveDataOld` at import** (`VMTS1ActivatorNew.cs:450`),
  so Option 2 cannot exercise the old/new law on the live engine without also
  importing the old array. Prefer Option 1 for the pin; keep Option 2 as a
  cross-check that records the imported motives vs the original pair.

### 4e. Implication for the EXISTING `mood` pin

The current `mood` check (`MoodMatches`, `AutotestRunner.cs:1632`) asserts the
`/8` average. **Once the port implements the weighted/state-dependent law, this
existing pin will fail** unless its formula is updated. The task constraint says
"do not weaken or change any existing autotest pins", so this must be handled
deliberately in the implementation round (likely re-point the `mood` pin to the
new law and keep its name, or retire it in favor of the new `moodlaw` check) —
flagging it here as a forward-compatibility note, not changing it now.

---

## 5. Cross-agent contract / seam proposal

Because the port-mood code and the test will be built in parallel with the decode
agent, expose two clean seams so the law can be dropped in without touching the
engine loop. Put them on the existing `VMIMotiveDecay` interface
(`VMAvatarMotiveDecay.cs:8`) implemented by `VMTS1MotiveDecay`, so `Tick` stays
owned by the port and the law is a pure function you can unit-test.

```csharp
// A) Steady-state target — pure function of inputs. Drop the decoded STR#502 law here.
short ComputeMoodTarget(
    in ReadOnlySpan<short> motives,   // 16-slot (Mood at [3] ignored as input)
    short roomScore,                  // RAW (unclamped) score, not motive[13]
    ushort personMask,                // person+1550 audience/type bits (bit0=cat, bit1=dog)
    int personType);                  // adult/child/pet -> selects the ghost/type weight table

// B) Displayed-value advance — pure function of current target + elapsed time.
short AdvanceMood(
    short currentMood,
    short targetMood,
    int elapsedSimMinutes);           // >= 1 on a recompute boundary -> cadence/smoothing law

// Optional helper so the caller doesn't re-derive the mask every tick:
ushort GetMoodEntryMask(VMAvatar avatar);
```

Then `VMTS1MotiveDecay.Tick` becomes roughly:

```csharp
var target = ComputeMoodTarget(MotiveDataAsSpan(avatar), roomScore,
                              GetMoodEntryMask(avatar), avatar.GetPersonData(PersonType));
if (recomputeBoundary)                                // the existing minutes/2 gate
    avatar.SetMotiveData(VMMotive.Mood,
        AdvanceMood(avatar.GetMotiveData(VMMotive.Mood), target, elapsed));
```

Contract notes for the decode agent:
- **Inputs are `short`** (port storage) but the native math is `float`. The seam
  takes shorts and returns a short; the law function should upcast internally and
  round/short the result, and disclose the rounding (native preserves a fraction).
- The **`ComputeMoodTarget` must be a pure function of its arguments** (no avatar
  state beyond what's passed) so it is unit-testable and the test can compute the
  expected value independently.
- The **`personMask`/`personType`** inputs are the bridge for the two unsolved
  r223 follow-ups (the `+1550` entry mask and the `+1536` ghost/type table split).
  If the decode agent settles the cadence but not the mask, the port can still
  land `ComputeMoodTarget` with `personMask=0` and `AdvanceMood` (test asserts the
  no-mask trajectory), then add the mask later.

---

## 6. Honest caveats / cannot-reach-from-harness

- **`MotiveDataOld` is not available on the live avatar** (single `short[16]`,
  import drops it at `VMTS1ActivatorNew.cs:450`). The old/new native law can only
  be exercised on the live engine after the import path is changed to preserve the
  old array; until then it's a static-evidence cross-check via `house_probe`.
- **Room score in the fixture**: `RefreshRoomScore` recomputes on object/lighting
  events, and the fixture's object set must be held constant for the room score to
  be a stable input. If test actions spawn/move objects, the room score drifts and
  the mood target changes — pin the room by not interacting with room-impactful
  objects, or assert the score is constant each sample.
- **The exact native cadence & smoothing constant is not yet pinned** (it's
  r251's decode target). The `AdvanceMood` signature is deliberately time-agnostic
  so the decoded constant can be supplied later without a signature change. Any
  assertion about "number of boundaries to converge" is a placeholder until the
  decode lands — the fixture asserts the *shape* (no instant snap, monotone
  convergence) so it is meaningful even before the constant is known.
- **The `/8` in `VMTS1MotiveDecay.cs:129` and `VMAvatarMotiveDecay.cs:156`** are
  the disclosure points: both are the port's equal-weight approximation. The exact
  integer-division truncation means the port cannot match the native fractional
  mood even if the numerator were correct.
- I did **not** modify `game-data/` or any production source; only created
  `tools/iff-dump/r251-mood-cadence/port-audit.md`.

---

## 7. File index (for the implementation round)

- `FreeSO/TSOClient/tso.simantics/Entities/VMTS1MotiveDecay.cs` — TS1 mood law (lines 45, 48, 52, 125, 127, 129)
- `FreeSO/TSOClient/tso.simantics/Entities/VMAvatarMotiveDecay.cs` — TSO mood (lines 104–156)
- `FreeSO/TSOClient/tso.simantics/Entities/VMAvatar.cs` — `MotiveData[16]` (72), `MotiveDecay` select (264), `Tick` call (580), `Get/SetMotiveData` (1032/1038)
- `FreeSO/TSOClient/tso.simantics/Model/VMMotive.cs` — `Mood=3` slot, `Room=13`
- `FreeSO/TSOClient/tso.simantics/Model/VMMotiveChange.cs` — per-motive change ticks
- `FreeSO/TSOClient/tso.simantics/Model/VMPersonDataVariable.cs` — `PersonType=32`, `Gender=65`, person-word offset comment
- `FreeSO/TSOClient/tso.simantics/VMContext.cs` — `GetRoomScore` (1528), `RefreshRoomScore` (704), `ComputeRoomScoreOriginal` (782), `RoomScoreK_Room108=2.05f` (775), `GetRoomAt` (1496)
- `FreeSO/TSOClient/tso.simantics/Utils/VMTS1ActivatorNew.cs` — import discards `MotiveDataOld` (450–453)
- `FreeSO/TSOClient/tso.files/Formats/IFF/Chunks/OBJM.cs` — `MotiveDataOld/MotiveData float[16]` (203–204, 257–269)
- `FreeSO/TSOClient/tso.files/Formats/IFF/Chunks/STR.cs` — `GetString(i)` for curve strings (100)
- `Client/Simitone/Simitone.Client/AutotestRunner.cs` — default `Config.Checks` (75), `CheckEnabled` (241), `MoodMatches` (1632), `SnapshotAvatar`/`SnapshotRoom` (1608/1623), `AllMoodOk` (4456), `Pass`/`Fail` (22424/22430)
- `Client/Simitone/Simitone.Client/AutotestFreeWillWin249.cs` — model for an additive opt-in focused check
- `tools/house_probe/Program.cs` — reads `MotiveDataOld`/`MotiveData` statically (`--motives`)
- `tools/iff-dump/r223/r223-mood-room-law.md` — the native law evidence this audit builds on
