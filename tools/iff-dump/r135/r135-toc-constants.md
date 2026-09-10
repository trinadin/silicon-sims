# R135 — the TOC-resolution harvest: the exact gauge law + the upkeep law, and the BSS proofs

Round protocol: TOC resolver (main thread) + Hermes pseudo-code agent →
constants bound → port → gate → evidence. Gate: **78/78 PASS** first try
(`r135p1.log`).

## 1. The resolver (resolve_toc.py — the R134 recipe, toolized)

`[TOC − X]` reads the TOC entry at `sec1_unpacked[0x8000 − X]`; the entry's
u32 V is section-relative. Disambiguation verdicts (raw dumps in
r135-toc-resolution.txt):

* **V ≥ 0x7bf80 → read CODE-REL (file 0x8e90+V)** when the target is a clean
  float/double const pool. FOUR hits:
  * **[TOC-20376] → file 0x5a45ec** — the GAUGE pool (floats):
    0.75, 8, 15, −25, 25, **0, 0.5, 11, 100, 201, 5**, 176.
  * **[TOC-20380] → file 0x5a4618** — 2.0f, 1/3, two int→double magic pairs.
  * **[TOC-23512] → file 0x5a2fb4** — **0.0 / 1.0 / 100.0** (+ 2.25).
  * **[TOC-23516] → file 0x5a2fc0** — double 4.0 + the magic pair.
* **V ≥ 0x7bf80 pointing at instruction streams** when read code-rel
  (mid-prologue nonsense) **proves BSS** (runtime-only): the upkeep weights
  [TOC-29328/29332], the gauge {dx,dy} offset pairs [TOC-20396/-20400/-20404],
  the track-global, the yard globals [TOC-29456/-29516/-29520/-29524], the
  gamestate pointers [TOC-30604/-30608], the HouseInit language tables.

## 2. The exact gauge rating law (constants bound to the Hermes pseudo-code)

cWinPeople::TSPaint 0x28d718-0x28d7a8, with K20=0.0, K24=0.5, K28=11.0,
K32=100.0, K36=201.0, K40=5.0 from the recovered pool:

```
f1     = 11.0·(100.0 + mood) / 201.0 − 5.0
rating = symround(f1), clamped to [−5, +5]      // symround: (int)(0.5+f) / (int)(f−0.5)
rating < 0 → kRedBars; rating > 0 → kGreenbars; bar height = |rating|·5 px
```

Check points: 100→+5, 50→+3, 1→+1, 0→0, −1→0, −50→−2, −60→−3 (15px),
−100→−5 (25px), ±400→±5. **R131 CORRECTIONS:** the "float-eased display" was
a misread — the chain is a MEMORYLESS per-paint transform (no ease state; the
port's StepEase now tracks directly); the sign mapping stands (rating<0 → red);
the "0x43000000 (128.0)" note was wrong — the constant is 0x43300000, the
int→double magic high word. R131's gate pins all still hold under the exact
law (100→25px, −60→15px, −400→25px coincide).

## 3. The upkeep law + the score getters (constants bound)

* **GetUpkeepScore** = `int(100.0f · clamp((float)(dividend / 2³¹), 0.0, 1.0))`,
  guard objectCount ≠ 0 (FS pool 0.0/1.0/100.0 from file; correcting the
  pseudo-code agent's slip: ratio = x/2³¹, not x/2³¹−1 — num = −x, den = −2³¹).
  The dividend's weights are runtime-NORMALIZED (B = 2³¹/N ⇒ score 100 when
  nothing is broken — the corpus's wording), A/B are BSS. Port: the r=0 case
  (broken contributes nothing) → `100·n_ok/(n_ok+n_broken)`, disclosed.
* **GetYardScore** = `int(clamp(v·KY0, 0, 100))` — KY0 at [TOC-29456]+0 is BSS.
* **GetFurnishingsScore**: x = (v==0) ? 100 : 1127219200.0/v (v = the
  gamestate→32→+160 aggregate; 1127219200 = 0x43300000); ladder gamestate→100.
* **GetSizeScore**: x = squareFeet / numSims (the corpus's "not just the size
  ... but also the number of Sims"); ladder gamestate→96.
* **LadderEval** (exact): entries {f32 key, f32 val} at t+0, slopes f32[] at
  t+4, count t+8; scan down; x ≥ key[n−1] → val[n−1]; below all → val[0];
  middle → `val[j] + (x−key[j])·slopes[j]·(val[j+1]−val[j])`; count 0 → 0.

## 4. Port

* `UIOriginalLiveGauge.Rating` — the exact law as a static (gate-pinned at 10
  check points); `FillHeight = |Rating|·5`; `StepEase` tracks directly (the
  engine has no ease state — disclosed correction).
* `OriginalHouseStats.Result.UpkeepScore` — the decoded shape with the
  disclosed r=0 normalization; `UIHouseSubpanel` wires the **Upkeep bar LIVE**
  (full while every object works; the other four bars stay 0 — their inputs
  are the runtime-built ladders / BSS constants, proven absent r134/r135).

## 5. Gate

`uigauge` adds the exact-law pins (rt(100)=5 … rt(−400)=−5); `uihouse` adds
upkeep-bar equality with the independent compute + the nothing-broken ⇒ 100
invariant. Both PASS; 78/78.

## 6. Residuals banked

* The four remaining score bars (Size/Furnishings/Yard/Layout): the ladders
  and KY0 are runtime-only (proven); LayoutScore's tail formula reads
  House+48..+92 room-connection sums — decodable in a future round but its
  output feeds no ladder (it IS the score) — candidate R136.
* The gauge's second channel + {dx,dy} placements: BSS, proven.
* Runtime-only constants could still be captured from a LIVE original process
  (out of static-RE scope, disclosed).
