# R136 — LayoutScore: the route-history decode, the exact tail law, and the live Layout bar

Round protocol: Hermes x2 (port room-graph survey / Sim-movement survey) +
main-thread engine decode via a purpose-built SYMBOLIC INTERPRETER
(`decode_layout_tail.py` — re-reads the binary, decodes the FP ops the R133
tool printed as `.long`, and executes the tail with symbolic GPR/FP/stack
tracking so the formula falls out with zero hand-transcription risk). Gate:
**78/78 PASS first try** (`r136p1.log`).

## 1. The route-history model (House+48..+92)

The House keeps twelve counters = six (total, flagged) generations:

* A-side (total): **+48, +56, +64, +72, +80** snapshots and the live
  accumulator **+88**; B-side (flagged): **+52, +60, +68, +76, +84** and
  accumulator **+92**.
* **`House::AddLayoutTick(flag)` 0x8c4f0** (symbol trailer 0x8c514, size
  0x24): `if (flag & 0xFF) H92++;  H88++;` — one sample per call.
* **ONE call site (0x10c2d8)**, inside the per-Sim update loop: gated by
  `tick % 10 == 0` (the `0x66666667` mulhw div-10 idiom at 0x10c254-0x10c274),
  two s16 flags (r25+1484, r25+142), a float threshold (r25+0x7b8) and an
  object-scan bit — a movement-quality predicate whose exact condition is
  NOT decoded (the only unresolved piece of the channel semantics).
* **The rotation** (EnterLiveMode case 220, 0x8c8d4-0x8c92c): the four
  history pairs all ← the current pair, the current pair ← the accumulators,
  accumulators ← 0.
* **`House::ClearRouteHistory` 0x8c430**: reseeds all twelve from
  `*([TOC-29460])`. Called from the house-rebuild/stat-snapshot paths
  **0xb05f4 / 0xb1684 / 0xb1c20** (all three call the 0x138cxx stat family —
  renovation/reload contexts; dumps in `r136-clearroute-callers.txt`).

## 2. The exact tail law (0x8c2ec-0x8c3d8, machine-verified)

```
SA   = H48+H56+H64+H72+H80+H88        (the A/total sum)
SB   = H52+H60+H68+H76+H84+H92        (the B/flagged sum)
guard: if SA < 1 -> SA = 1            (0x8c338-0x8c34c — a DIVISION guard)
den  = double{0x43300000, u32(H76)} - (2^52 + 2^31)     [magic, RAW low word]
sub  = double{0x43300000, (2*SB)&0x7FFFFFFF ^ 0x80000000} - (2^52 + 2^31)
ratio= (den - sub) / den              (fdivs — SINGLE precision)
clamp ratio into [0.0, 1.0]           ([TOC-23512]+0 = 0.0f, +4 = 1.0f)
score= fctiwz( 100.0 * ratio )        ([TOC-23516]+16 = 100.0 double, fmul)
stats+24 = score
```

Const pools (from file, R135-resolved): `[TOC-23512]` → 0x5a2fb4 =
{0.0f, 1.0f, 100.0f}; `[TOC-23516]` → 0x5a2fc0 = {4.0, 2^52+2^31, 100.0}.
The magic subtraction with a RAW (non-XORed) low word is only the correct
signed conversion when the field's bit 31 is set — i.e. `den = N0 +
count(+76)` where **N0 is the seed ClearRouteHistory installs**.

## 3. The BSS proof + the disclosed normalization

`[TOC-29460]` = 0x852a0 → code-rel file 0x8e130 reads `3bc40000 93a1fff4
7c7d1b78...` — an instruction stream, the r134/r135 BSS signature. **The
seed N0 is runtime-only, statically unrecoverable.** With any plain
(non-bit-31) counter values the machine code degenerates (ratio > 1 clamps
to a constant 100), so the seed is load-bearing and unknowable.

The engine's own `max(SA, 1)` division guard (computed at 0x8c338-0x8c34c)
pins the intended divisor as the **total movement count**. The port
therefore normalizes by the totals — disclosed:

```
LayoutScore = (int)(100.0 * clamp(1f - 2f * flagged / max(1, total), 0, 1))
```

(f32 ratio / f64 scale / fctiwz truncation, matching the engine's op mix.)

## 4. Port

* **`OriginalRouteHistory`** (UI/Model): the counters + sampler + law.
  Observation point: a Sim's `VMStackObjectVariable.Room` changing between
  samples (maintained per move by the engine's own `VMAvatar.PositionChange`
  — no submodule edits). The flag channel marks transitions NOT through a
  door portal (`RoomInfo[from].Portals`) — teleports/carries/wall-gap
  crossings — as the non-smooth side (the engine predicate is the one
  undecoded piece). `Clear()` hooks `Architecture.WallsChanged` — the
  renovation signal matching the three engine clear-paths.
* **`UIMainPanel.Update`** samples every 30 frames while in LIVE mode (the
  engine samples every 10th movement tick inside the VM).
* **`UIHouseSubpanel`**: the **Layout bar LIVE** (`ScoreBars[4]`), joining
  Upkeep; Size/Furnishings/Yard stay 0 (ladders/KY0 runtime-proven absent,
  r134/r135).

## 5. Gate

`uihouse` math block: the law pinned over synthetic feeds — fresh ⇒ 100,
(10,0) ⇒ 100, (10,2) ⇒ 60, (10,5) ⇒ 0, (10,8) ⇒ 0 (clamped), (0,0) ⇒ 100
(the max-guard), and Clear() resetting. Live block: `rh.Sample(vm)` +
recompute, `ScoreBars[4] == Stats.LayoutScore`, equality with an independent
inline recompute of the law from the raw counters, range [0,100]. PASS
78/78.

## 6. Residuals banked

* The flag predicate at 0x10c2d8 (the movement-quality condition) — the
  call site's full decode (two s16 flags + float threshold + object scan).
* The seed N0 (BSS) and the rotation cadence (when EnterLiveMode msg 220
  fires) — both runtime-only; the port's WallsChanged clear + total
  normalization are the disclosed stand-ins.
* cFixedWorld::ComputeArchValue 0x15eef0 (live house value) — the natural
  R137: the port's biggest economic gap (ValueInArch file-read only).
