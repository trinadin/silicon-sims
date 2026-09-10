# R222 — the career Chance Card trace (chancetrace; opt-in)

The PARITY residual: "Chance Cards and alternate career/school outcome
branches remain untraced" (the R163 carreturn row; carried in the career
progression `[GAP-partial]` and AUTOTEST's carreturn/schoolreturn row). This
round decodes the Chance Card law from the staged CarPortal.iff and traces it
live end-to-end through the real commute/pay fixture.

## The static law (decoded from game-data CarPortal.iff; annotate_bhav.py listings in this dir)

Chunk ids are big-endian u16 in this corpus (the r157 canon: 4100 'process',
4102 'At Work', 4103 'get paid'). The three chance trees:

| id | label | instructions | careers dispatched |
|------|-----------------|--------------|--------------------|
| 4134 | Chance Cards 3  | 123 | 17–21 (fashion/education/animal/food/circus), else cascade |
| 4124 | Chance Cards 2  | 225 | 1–10 (the ten base tracks), else cascade |
| 4133 | Chance Cards    | 120 | 12–16 (musician/slacker/paranormal/journalism/hacker) |

**The fork** — `get paid` (4103), at the end-of-shift pay:

- ins38 `MyPD[57] != 0` (level != 0 gate),
- ins39/40 `Local[6] = rnd(0..99)`, `Local[7] = rnd(0..99)`,
- ins41/42 `Local[7] < Local[6] -> Local[6] = Local[7]` (**min of two rolls**),
- ins43 `MyPD[63] > Local[6]` → T15: CALL 4104 `test promotion`;
  **F54: CALL 4134 'Chance Cards 3'** — and 4104 returning false (unmet
  next-level skill/friend requirements, the ins4–19 ladder) ALSO lands on F54.

So a chance card fires on every pay where the promotion roll/tree does not
succeed — probability `P(perf <= min(r1,r2)) = ((100-perf)/100)^2` plus every
failed promotion test.

**The cascade** — 4134 ins2–6 dispatch careers 17–21 to their own card bodies;
any other career falls to ins120 `CALL 4124` (T254/F255). 4124 ins2–11
dispatch careers 1–10; others fall to ins222 `CALL 4133`. 4133 ins2–6
dispatches 12–16. Each tree's entry (ins1) routes through a `MyPD[24] == 0`
gate (the "card already shown" latch, reset to 0 by get paid's ins49/50
during the pay sequence, set to 1 at ins94/ins219 after a card runs).

**The card body** (example — the fixture's card, Science = career 9, level 5,
in 4124):

```
ins10  career == 9  -> ins53
ins53  level == 5   -> ins54          (F117: next level ladder)
ins54  playSound (3801)
ins55  dialogPrivateStrings opd=002c150010000408   <- THE CARD
ins56  budget  opd=0307881302000200  (+5000)
ins57  MyPD[12] += 100 (cap 1000)    (mechanical)
ins60  MyPD[11] += 100 (cap 1000)    (cooking)
...    -> T219 chains -> ins219 MyPD[24] = 1 -> return true
```

The dialog operand (VMDialogOperand, LE): Cancel=0, IconName=0x2c (1-based
STR#301[43] = `'job $Local:9'` — the named career icon, Local[9] = PD[56] set
at ins0), Message=0x15 (STR#301[20]), Yes=0/No=0 (defaults), Type=0 (Message),
Title=4 (STR#301[3] = `'$Me'`), Flags=0x08 (IconMode=Named, Continue NOT set →
**BLOCKING**). Corpus pins (English set, format -3, 1960 total / 98 English):

```
STR#301 'Dialog prim string set' len=760028 sha256=773c06ed1fde0df4f56b9e81552e998c519f79631c7e72ff0c41b5a4d9afcdbf
  [3]  '$Me'                                              sha256=5c7be5733463a82db3adaca11ce60b20042db56526721d403c9f820d9a7b5547
  [20] 'Your latest invention is a huge success! ...'     sha256=8226253205e9ac1707f877f3cbfde404763f418b80d262da014506a50af8447a
  [43] 'job $Local:9'                                     sha256=4cae653e6e3f9db56afdbe8fe59c1a85b20c0530f2e9faf66cd27338b7aac162
```

STR#301 entries 12–55+ carry every career's card texts (the Maxis comments
name them: 'Career: Chance Card: Business01' ... 'Science02' ...), the
pay/promotion/demotion notices (entries 2/4/5), and the school texts.

**The dialog response law**: a Message-type dialog exits GOTO_TRUE once
`Responded` (or the 60-sim-sec shared timeout) — the card's effect branch
(ins56+) runs on any click. The probe answers through the exact
`VMNetDialogResponseCmd` TS1 law (Responded=true, ResponseCode=0, latch
release, LastSpeedMultiplier restore).

## The probe (check `chancetrace`, opt-in like carreturn/schoolreturn)

Rides the R163 carreturn machinery (state 3) on the same House-5 fixture:

1. **Fixture** (discovery): obj21, Science (PD[56]=9), level 0 — the probe
   sets `JobPromotionLevel := 5` (the R219 hunger-collapse state-set
   precedent) so the day's card is KNOWN: Science level 5 → 4124 ins53 → the
   Science02 card.
2. **Fork determinism**: at level-5 CARR end − 1h (past the morning warn/fire
   pass), the probe collapses `PD[63]` to −50. The ins43 gate
   `perf > min(rnd, rnd)` is then deterministically false → F54 → the card.
3. **Dense sampling**: the existing car-owned frame walker records every
   CarPortal routine id first-seen (with routine:ip stack chains); the budget
   observer records every TS1 budget mutation with author iff:routine:ip.
4. **The player move**: `ChanceTraceRespondDialog` runs at the TOP of
   StateCarpool (before the generic R157 unpause could orphan the latch):
   when the blocking dialog's owner stack carries 4124/4133/4134, answer it.
5. **PASS law**: fixture valid AND pay window ran (4103 sampled post-end or
   the CARR transfers) AND ≥1 chance-tree id seen AND the probe answered the
   card dialog AND a card effect landed after the response (a cascade-authored
   budget event or a worker PD change: skills/PD[24]).

## Observed (run 1 — honest FAIL, harness ordering bug)

The fixture held and the ENGINE ran the level-5 day correctly (the portal
re-bookmarked attr[0]=10 for the 10:00 start, `4105 'send to work'` +
`4102 'At Work'` executed at 10:00, the outbound car spawned and despawned,
performance drifted to 32) — but the HARNESS's wait target was computed from
the schedule loop that reads the CARR at the sim's live level BEFORE the
probe's level-set ran (discovery order): `latestEnd` = the level-0 end 15:00,
so the run finished at 16:15, before both the 18:00 perf collapse and the
19:00 pay. `chancetrace: FAIL` (chanceIds=[], no pay events) — the correct
honest verdict for a run that never reached the window. Fix: the fixture
block now sets level 5 BEFORE the schedule loop (run 2).

## Observed (run 2 — honest FAIL: the probability gate discovered)

The corrected wait target (returnEnd=1140) held; the perf collapse ran at
18:00 (PD[63] 31 → −50); the return car spawned at 19:00 and `4103 'get
paid'` executed — but the card never showed: `CarPortal.iff:4103:9=+540`
(the plain salary), no 4104, no cascade frames, no promotion, level
unchanged, and one obj21-owned blocking dialog orphaned by the generic R157
unpause at the pay moment. The post-mortem re-read of 4134's entry found the
missing law (my first reading had mislabeled it dead code): **the entry gate
is ins1 → ins90 `MyPD[24]==0` → ins121 `Temp[0]=rnd(0..99)` → ins122
`Temp[0] < Tuning[135]`** — and `Tuning[135]` decodes as BCON 4097 key 7
(tableID = 4096+(135>>7) = 4097, key = 135&0x7F = 7) = **12**: a **12%
chance-card probability per pay**. Run 2 rolled ≥ 12 honestly and took the
no-card return: card-false → 4103 F28 → ins28 `PD[63] > 0` false → the
fine ladder (ins19/20/29 — Local[1] negated, no demotion this run) → the
orphaned pay dialog → ins9 +540.

## Observed (run 3 — honest FAIL: the Callee split discovered)

Run 3 mounted the worker's `TuningReplacement` (4097,7)=100 — the card still
did not fire (and this run's fine ladder took the demotion variant: level
5→4, salary re-read at the demoted level, `4103:9=+450`, the demotion dialog
orphaned). The post-mortem found the scope split in VMMemory:
**`MyPersonData` resolves through `frame.Caller`** (the thread owner — the
worker: that is why the salary reads tracked the probe's level-5 set) **but
`Tuning` resolves through `frame.Callee`** — and Callee is INHERITED down
the CALL chain from the frame 'At Work' was pushed with: the **CarPortal**
(the code-owning interaction target). The worker-side override never touched
the roll. (Both fine-ladder variants are now live-evidenced: fine-only
run 2, demotion run 3.)

## Observed (run 4 — PASS, the card traced live)

With the odds override mounted on the PORTAL (and the worker copy kept),
the full chain executed and was caught by the new worker-stack tracer at the
exact decoded sites:

```
19:00  worker-stack [4096:1][8193:4][8283:7][8233:2][4102:13][4103:54][4134:120][4124:55] bs=VMDialogResult
       person main → person globals → 'At Work' ins13 (the CALL site) → 'get paid' ins54
       (the fork edge) → 'Chance Cards 3' ins120 (the cascade CALL) → 'Chance Cards 2' ins55
       (the Science02 card dialog, BLOCKING)
19:00  chance-card dialog ANSWERED (ResponseCode=0) — the exact VMNetDialogResponseCmd TS1 law
…     card effects: CarPortal.iff:4124:56 = +5000 (3827→8827)
       PD[12] 0→100 (Mechanical +100), PD[24] 0→1 (the shown-latch)
…     card returns true → 4103 T7 → the pay re-loop blocks at [4103:14]
       (the 'Welcome home! You brought home $$Local:0' dialog, orphaned by the
       generic R157 unpause, resolved by its own 60-sim-sec timeout)
…     CarPortal.iff:4103:9 = +540 (the level-5 salary) → tree returns true
```

Verdict clauses: fixture ✓, pay window ✓ (4103 post-end + transfers), cascade
live ✓ (portal-owned 4134+4124 sampled AND the tracer's first-seen), the
probe answered the card dialog ✓, effects after the response ✓ (the +5000
budget event authored inside 4124 + the PD writes). `chancetrace: PASS`
(6/0 with corpus). One diagnostic bug found and fixed after run 4: the
observer's card-budget counter sat after the `routine != 4103` early return
(the +5000 event logged fine but the counter read 0).

## Observed (run 5 — honest FAIL: natural variance + a detector bug)

The worker never went to work: at the 10:00 pickup its thread was blocked in
a NON-CarPortal dialog (a phone call — `phones.iff:8213:23 = 1350` in the
budget events), the carpool left without it, and the day produced no
`4102`/`4103` at all (`payWindowRan=false`). Natural soak variance of the
same class the carreturn family has always had — the check FAILED honestly.
The run also exposed a probe bug: the cascade detector and worker-stack
tracer matched routine ids 4124/4133/4134 WITHOUT IFF attribution, and the
worker's private-IFF frames (User00009.iff/PersonGlobals — which also carry
a 4124) false-positived `cascadeFrameSeen` — the r155 paper-carrier lesson
relearned. Fixed: all three sites (tracer, cascade detector, responder) now
require the frame's `ScopeResource.MainIff` to be CarPortal.iff (tracer
marks CarPortal-owned frames with a `*` in the signature).

## Observed (run 6 — the reproducibility run on the fixed binary)

PASS again, identical law: the card dialog answered at
`[4102:13][4103:54][4134:120][4124:55]` at 19:01,
`CarPortal.iff:4124:56 = +5000 (2967→7967)` (the corrected counter now reads
`effectBudgetEvents=1`), `PD[12] 0→100`, `PD[24] 0→1`, then the salary
`CarPortal.iff:4103:9 = +540`. Two consecutive passing runs (4 and 6) on the
same binary law; run 5 between them documents the natural-variance failure
mode (phone dialog at pickup → no workday → honest FAIL).

## The complete fork law (final, live-confirmed)

```
'At Work' (4102) ins13 → CALL 4103 'get paid'      [observed: 4102:13]
4103  ins0→ins6→ins39/40  Local[6],Local[7] = two rnd(0..99)
      ins41/42            Local[6] = min(Local[6], Local[7])
      ins43  MyPD[63] > Local[6]?
        TRUE  → ins15 CALL 4104 'test promotion'
                  TRUE  → promote: ins49/50 PD[24]=0, ins10 PD[57]+=1, bonus =
                          salary × Tuning[132] (=BCON4097[4]=2) at ins51, the
                          promotion dialog ins5, salary ins9   [R163's +460/+230]
                  FALSE → ins54 (the card)
        FALSE → ins54 CALL 4134 'Chance Cards 3'   [observed: 4103:54]
4134  ins90  MyPD[24] == 0?        (the shown-latch; reset by ins49/50 at pay)
      ins121 Temp[0] = rnd(0..99)
      ins122 Temp[0] < Tuning[135] (=BCON4097[7]=12 — 12% per pay)?
        FALSE → return false (no card today)
        TRUE  → ins2-6 career dispatch: 17-21 own cards; else ins120 → 4124
                (careers 1-10 own; else ins222 → 4133: careers 12-16)
                → the per-career/level card body → BLOCKING STR#301 dialog
                  → response → budget/PD/sound effects → PD[24]=1 → true
4103  ins54 TRUE  → ins7/8/1 (salary re-read at the card-modified level),
                   ins17/48, ins14 welcome dialog, ins9 salary  [observed]
      ins54 FALSE → ins28 MyPD[63] > 0?
                   TRUE  → the plain pay (ins14+ins9)
                   FALSE → the fine/demotion ladder (fine-only and demotion
                            variants both observed — runs 2/3)
```

## Round result

- `chancetrace` added as an OPT-IN check (the carreturn/schoolreturn family;
  default suite count stays **128**). Targeted soaks: run 4 **PASS** 6/0 and
  run 6 **PASS** 6/0 (the corrected-counter + IFF-attribution binary —
  `effectBudgetEvents=1`); runs 1-3 and 5 kept as the honest
  analysis-evolution record (harness ordering bug → the 12% probability gate
  → the Callee split → the phone-call natural-variance miss + the per-IFF id
  collision). FULL GATE on the final binary: **128 passed, 0 failed**,
  wrapper exit 0, clean Run/Dispose, zero `SimAnticsExc`, only the known
  self-recovering `splashprogress` startup exception (gate-run.log, 1,778
  lines). `dist` byte-matches `publish` (`Simitone.Client.dll` SHA-256
  `1ccf211959e36f8a3dfeab64d315712fc9297bf08595e3616faf0cbadb44673f`).
- PARITY: the career-progression residual narrows — **Chance Cards are now
  decoded and traced live end-to-end** (fork → probability gate → cascade →
  card dialog → effects → latch → the post-card pay re-loop). Remaining in
  the row: alternate career/school outcome branches beyond the card (the
  school side has no cards — kids get the A++/missed-school events, already
  pinned by R163/R165).
- Disclosed probe state-sets (all harness-side, no engine change): job
  level 0→5, PD[63]→−50 at end−1h, TuningReplacement (4097,7)=12→100 mounted
  on the portal (the roll's Callee) and the worker. The 12% original odds
  and the PD[24] latch are the shipped law; the overrides only make the
  fixture deterministic.
- No proprietary asset committed; the STR#301 corpus pins are hashes only.
