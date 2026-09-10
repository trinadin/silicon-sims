# R135 pseudo-code (Hermes-produced, word-verified against the binary; persisted by the parent)

The full agent report is preserved in the session log; the load-bearing results,
now BOUND to the file-recovered constants (r135-toc-resolution.txt):

## The gauge pool [TOC-20376] @ file 0x5a45ec (floats)
+0x00 0.75, +0x04 8, +0x08 15, +0x0c -25, +0x10 25, +0x14 0, +0x18 0.5,
+0x1c 11, +0x20 100, +0x24 201, +0x28 5, +0x2c 176
=> K20(threshold)=0.0, K24(round bias)=0.5, K28=11.0, K32=100.0, K36=201.0, K40=5.0

## cWinPeople::TSPaint rating chain (0x28d718-0x28d7a8), constants bound:
    x  = (double)(int32)motiveValue        // intended input: the rounded P[3] motive
    f3 = 100.0 + x
    f3 = 11.0 * f3
    f2 = (float)(f3 / 201.0)
    f1 = f2 - 5.0
    rating = (f1 >= 0) ? (int)(0.5 + f1) : (int)(f1 - 0.5)      // symmetric round
    rating = clamp(rating, -5, +5)
    sign: rating < 0 -> kRedBars (this+368); rating > 0 -> kGreenbars (this+372)
    bar height = |rating| * 5 px  (of the 27x25 art)
Check points: mood 100 -> +5; 50 -> +3; 0 -> 0; -50 -> -2; -60 -> -3 (15 px);
-100 -> -5 (25 px). R131's "float-eased display" was a misread: the chain is a
memoryless per-paint transform (no ease state in the engine chain).

## HouseStats computed getters (constants bound)
* GetUpkeepScore = int( 100.0f * clamp((float)(dividend/2^31), 0.0, 1.0) ),
  guard objectCount != 0. (FS pool @ [TOC-23512] file 0x5a2fb4: 0.0/1.0/100.0.
  Correcting the agent's slip: num = -x, den = -2^31 -> ratio = x / 2^31.)
  The dividend = B*n_ok + A*n_broken with runtime-normalized weights
  (B = 2^31/N gives score 100 when nothing is broken — matches the corpus).
* GetYardScore = int( clamp(v * KY0, 0.0, 100.0) ) — v = gamestate->36->+76
  float, KY0 = [TOC-29456]+0 float (BSS — runtime-only).
* GetFurnishingsScore: x = (v==0) ? 100.0 : 1127219200.0/v (v = gamestate->32->
  +160 int); ladder at gamestate->100.
* GetSizeScore: x = (double)squareFeet / numSims; ladder at gamestate->96.
* LadderEval(t, x): entries {f32 key@0, f32 val@4} at t+0, slopes f32[] at t+4,
  count at t+8; scan down; x>=key[n-1] -> val[n-1]; x<=all -> val[0]; middle ->
  val[j] + (x-key[j]) * slopes[j] * (val[j+1]-val[j]); count==0 -> 0.
