# R223 — the mood/room formula: curves + structure recovered, exact law narrowed (evidence round)

The R4-era `[GAP-partial]` ("stored 80/81/83 vs engine 73") revisited with the
R90/R141/R221 RE infrastructure. Evidence-only round (the R128 precedent): no
engine change — nothing below is proven to the pin bar yet, and the R128 rule
applies: not changed blind.

## Recovered (IFF/binary-factual)

**STR# 502 'mood weight curves'** (Global.far!Global.iff, format -3, 253
bytes, sha256 `1fe774b076ddf91a989aef7369eb7a6e2ab414c5bac72c5b891b21c3cb3f01fd`),
loaded by `cXPerson::Initialize` through
`MotiveCurveSet::LoadFromFile(this, ?, 502)` (disasm 0x111ee0, bl 0x9f502):

```
[0] (-100;15) (-60;5) (-40;3) (0;1) (100;1)
[1] (-100;10) (-80;3) (-40;1) (100;1)
[2] (-100;5)  (-80;3) (-40;1) (100;1)
[3] (-100;10) (-80;3) (-60;1) (100;1)
[4] (-100;5)  (-40;2) (40;2)  (100;5)
[5] (-100;2)  (0;1)    (100;2)
[6] (-100;5)  (-40;2) (40;2)  (100;3)
```

Neighboring tables: **STR# 504** = the same seven with `[4]` at 6s not 5s (a
variant — the second weight table), **STR# 501/503** = ten
`(-100;-100)…` ad-effect curves (the MotiveEffects/GetInteractionScore
family, 0x9f9d2), **STR# 505** = the R186 house-score curves.

**The CalcHappy table order** (rodata at file 0x5a45b8, CodeWarrior
immediate array): `(7, 5, 6, 15, 8, 14, 9, 13)` = **Hunger, Energy,
Comfort, Fun, Hygiene, Social, Bladder, Room** — eight entries, room IS a
mood participant. A sorted copy `(5,6,7,8,9,13,14,15)` sits at 0x5a32f8
(the Motives iteration order). A third short array `(7,6,3,5,2)` follows at
0x5a45d8 (includes motive 3 = Mood itself — role unresolved).

**`cXPerson::CalcHappy` @0x10b9c2 (+2; 352B)** — structure (full disasm in
this dir, float-decode support added to ppc_decode.py this round):

- type check on `person+1536` vs 18 selects one of TWO weight tables
  (TOC−22780/−22784 — the ghost/type split);
- per 20-byte table entry: motive index at +16 reads
  `person + 1932 + 4*idx` (**+1932 = the 16-slot float motive array**;
  +1944 − +1932 = 4·3 → mood IS slot 3, matching VMMotive.Mood);
- the knot array (stride 8, doubles) is scanned BACKWARD for the segment
  containing the motive value, then lerped — inline PiecewiseFn evaluation;
- **`person+1550` flags gate the accumulation** (the R128 audience-flag
  field): bit-ops on the entry index skip entries per-person;
- accumulation `f6 += m·w` (fmadds), `f5 += w` (fadds), tail
  `fdivs f0 = f6/f5` → **`stfs person+1944`** = mood.

So the aggregation IS `mood = Σ(w_i·m_i)/Σ(w_i)` with w from the seven
STR#502 curves (+ a room weight), gated by a per-person entry mask.

## The falsification (why nothing was changed)

The ORIGINAL saves carry **two consecutive motive states** (OBJM
`MotiveDataOld[16]` + `MotiveData[16]`). Five persons across the staged
House05/House07 saves (r223-save-tuples-*.txt; house_probe --motives
extended this round) show:

- **all eight inputs byte-identical between old and new** — yet mood moved:
  Cassandra 81.575→80.086, Bella 79.912→80.816, Mortimer 81.927→82.819,
  **Bob 74.073→57.385, Betty 75.118→60.511**.

A pure weighted average cannot produce that (no positive weight vector fits
— the nullspace solve is inconsistent; per-sample subset fits overfit with
different masks/room-weights each). Combined with Bella-vs-Cassandra (lower
needs, HIGHER mood), the stored mood is **state-dependent**: either smoothed
toward the CalcHappy target, recomputed on a slower cadence than the motives
(CalcHappy is reached only through a VTABLE — no direct bl callers), or the
entry mask (+1550) itself changed between the captures.

Per-sample fits DO bracket the law: the best single-subset fit
({Comfort, Hunger, Bladder, Fun, Room}, room weight 8) lands within 1.73 of
all five stored moods — consistent with curve-weighting + staleness drift,
but NOT pin-grade.

## Also quantified: the ROOM SCORE divergence

The port's engine computes room −9 for the Goth lot at load where the
originals stored **86.333** (House05) / **50.222** (House07) in motive[13].
The port's `RefreshRoomScore` (Σ RoomImpact / max(1, area/12) − 10, outside
÷30 − 15) is the divergence; rodata near the first index array holds the
original room constants (`0.1875, 3, 5.333, 0.5, 2, 0.125, 1, −100, 100` —
the r134 `RoomScoreConstants` family). This is the concrete, most
user-visible half of the gap — a room-score decode round is the natural
follow-up (UpdateCurrentRoom @0x109452 + the room-score math).

## Follow-ups (named, in priority order)

1. **Room score decode** (UpdateCurrentRoom + the scoring math) — the port
   is ~95 points off on the fixture; biggest user-visible delta.
2. The mood entry mask: the +1550 flag bits vs the entry-index parity ops
   in CalcHappy 0x10bad4-0x10bb04 (which motives a person skips).
3. The mood update cadence (CalcHappy's virtual caller) + smoothing law —
   settles the old/new pairs.

## Tool upgrades this round

- `tools/iff-dump/ppc_decode.py`: float instruction support (lfs/stfs/lfd,
  primaries 59/63 with the CORRECTED A-form field order — frC at 16-20,
  frB at 21-25, 5-bit XO; the first cut misread xo as 10 bits and decoded
  fmadds-family words as invalid).
- `tools/house_probe`: `--motives` dumps the OBJM MotiveDataOld/MotiveData
  float arrays + MotiveDeltas + FirstFloats per person.
