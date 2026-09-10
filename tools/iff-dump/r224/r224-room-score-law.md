# R224 — the room-score decode: ground truth + constants + the named blocker (evidence round)

The R223-named follow-up: the port computes room **−9** where the ORIGINAL
stored **86.333** (Goth) / **50.222** (House07) in motive[13]. This round
banked the port-side ground truth, confirmed the constants mapping, and
mapped `Room::ComputeRoom` — but the exact float pipeline did NOT yield to
the decoder, so per the R128 "not changed blind" rule NOTHING was changed
in the engine. The port's approximation stays disclosed.

## The port's own numbers (new permanent `roomlaw` diagnostic)

`house_probe`/harness gained a one-shot room-internals dump (mood-gated,
log-only, zero behavior change): per room — area, support structure,
Σ RoomImpact, entity counts, computed score — plus each avatar's
position → room → score. Ground truth banked for both fixtures:

**House05 (Goth, stored room 86.333):** outside base r1 (area 2618,
Σimpact 190/11 ents, score −9); interiors r2 (20 t, imp 60 → 26), r3
(9 t, imp 1 → −9), r4 (35 t, imp 65 → 12), r5 (27 t, imp 40 → 7), r7/8
(32+3 t, imp 10 → −7). **House07 (stored 50.222):** outside base r1 (area
2225, imp 350 → −4); interiors r2 (28, 20 → −2), r3 (40, 1 → −10, **Bob &
Betty's room**), r4 (12, 0 → −10). The port's live formula confirmed
exactly: `score = Σimpact / (area/12f) − 10` inside, `/30 − 15` outside.

The two stored originals vs the port's numbers are the fit constraints any
future law must satisfy; they also PROVE the original's score is dominated
by inputs the port does not model (rooms with Σimpact ≈ 1 score 50–86 in
the original — walls/floors/windows/doors, the r134 collector outputs).

## The original's ComputeRoom map (0x1267d0, 2796B — clean disasm this round)

- The r141 symbol index is **+2-shifted in this region** (bl targets land
  at symbol+0; found via `bl 0x124fe0` = `Room::IsOutside` at symbol
  0x124fe2) — R221's regional-offset note reconfirmed. The corrected start
  0x1267d0 yields a coherent prologue/flow.
- Entry globals (r134's TOC mechanism: **r2 = sec1+0x8000**):
  `r28 = TOC−29216` (the world/house module), `r29 = TOC−22592`, `r30 =
  TOC−22588` — the r134-resolved **RoomScoreConstants**:
  doubles @file 0x5a3590 = [0.5, 0x4330000000000000 magic, 1.0, 2.0, 6.0,
  0.99, magic]; floats @file 0x5a3538 = [0.8, 255, 0.1875, 3, 5, 0, 1,
  0.5, 100, 0.01, −100, 0.0625, 30, 60, −30, −20].
- Structure: `IsOutside(room)` splits; the INSIDE path reads a type from
  `world+…+24` (values 2/4/0 seen) selecting `r30[+20/+24/+28]` =
  **0.0 / 1.0 / 0.5**, squares a global float (`r28+12`) into
  `room+108`, and jumps to the tail; the OUTSIDE path converts the
  integer collector stats (`room+56` walls, `room+72`, `room+80/+84`
  pool/wear) to doubles via the 0x4330 magic and normalizes against
  `r30+32 = 100.0`; a second phase reads `room+84/+80` with `r29+8 = 1.0`
  and TOC-float mixes; windows/doors (`room+96/+100`) and the r134
  collector outputs feed later phases.

## The blocker (named precisely)

The float pipeline at **0x126890–0x1268c0** renders as
`fsubs f5,f1,f0; fdivs f5,f5,f0; lfs f3,-10880(r2); fsubs f0,f0,f0;
fdivs f5,f5,f0; fmuls f5,f5,f3` — the third-from-last zeroes f0 and the
next divides by it. The words are byte-verified and the field extraction
is spec-checked (binary-string audit in the round notes), so either this
toolchain emits an idiom the plain PPC spec reading mishandles, or the
region is a data island interleaved with code. Until that resolves, the
exact arithmetic cannot be proven, and R128's rule applies: **no blind
engine change**. A dedicated session with a reference disassembler (or
runtime tracing of the fpu registers in an emulator) is the path.

## What this round ships

- The `roomlaw` harness diagnostic (permanent, mood-gated) + the two-house
  ground-truth logs (banked here).
- The ComputeRoom map + the corrected region-offset note (+2 shift) +
  the constants/TOC confirmation.
- The fit constraints (the two stored values + the port's per-room
  inputs) for the follow-up that completes the law and fixes
  `VMContext.RefreshRoomScore`.
- Gate: log-only change. The first FULL GATE attempt hit a `mood` MISMATCH
  transient (stored 72 vs computed 76 — one point over the ±3 tick-boundary
  tolerance; an avatar crossing rooms changes the room input discretely and
  the mood slot lags the 2-sim-min cadence — gate-run.log, the R168
  variance class; both earlier runs of the same binary had passed 6/0). The
  unchanged rerun: **128 passed, 0 failed** (gate-run2.log), wrapper exit 0,
  clean Run/Dispose; `dist` byte-matches `publish`. The tolerance-widening
  temptation is refused — "never weaken a pin"; a future hardening may
  require the mismatch to persist across consecutive samples instead.

## Follow-up

R225 candidate: resolve the 0x126890 idiom (reference disassembler pass
over ComputeRoom), finish the law, then implement + pin. Alternatively
park it and take the sound audible-fidelity trace.
