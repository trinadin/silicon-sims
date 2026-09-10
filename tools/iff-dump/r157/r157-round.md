# R157 — the per-person gate trace: WHERE 'process' dies

The R156 recommendation, executed: instrument the CarPortal 'process' (4100)
execution to name the failing node — "which gate line skips the working sim
(obj=21), or whether the iterator never reaches it". Baseline: main 9bff522 /
FreeSO e4a31fab, green via r156-gate-run1.log (95/95, clean exit).

## 1. STATIC DECODE (corrects the r155/r156 "-2" law)

CarPortal.iff re-extracted from Objects.far (FAR1 manifest, 16-byte records —
`/tmp/r157/CarPortal.iff`), all instructions decoded with the port's OWN
schemas (VMExpressionOperand `[lhs:i16][rhs:i16][IsSigned][Operator][LhsOwner][RhsOwner]`,
VMVariableScope, VMExpressionOperator, VMSetToNextOperand; tool:
`annotate_bhav.py`, listings `bhav*.txt` in this directory).

### 1.1 'process' (4100, 58 ins) — the LIVE Create-Car chain is −1, not −2

* Entry: ins0 CALL global 321 → ins1 `Attr[8] = -1` → **T43** → ins43
  `Local[5]=Tuning[130]` → ins44 `+2` → ins46 **CALL 4125 'process warnings
  car pools'** → ins47 CALL 4126 'warn/fire people who miss work' → ins2
  `hour == Tuning[130]+2` (daily grades-drop gate) → ins3 `Attr[6] -= 200`
  (timer) → **ins4 `Attr[0] = -1`** → ins28 CALL 4115 'check for school bus
  schedule' → ins5 `StkObjID = 0` → ins6 setToNext Person (the loop).
* Per person: ins7 `StkPD[56] > 0` (JobType) → ins42 `StkPD[69] != -1`
  (last-day-at-work) → ins11 `StkObj[34] == 1` (skip) → ins9/ins10 stash
  `Temp[0]=w56, Temp[1]=w57` → ins48 `Local[4] = JobData[12]` (**at the sim's
  OWN level** — temp1 = promotion level word 57) → ins49 `-1` → wrap → ins52
  `hour == Local[4]` → **bookmark** (ins53 `Attr[0] == -1?` → ins54
  `Attr[0] = JobData[12]` → ins56/57 track `Attr[8]` = max w57, re-bookmark
  when a more senior worker matches).
* Post-loop staging: ins12-14 compute `Attr[0]-2 == hour` with **T34/F34 —
  both pointers equal: DEAD CODE** (a Maxis Edith leftover). The LIVE chain is
  ins34 `Local[3] = Attr[0]` → ins20 `-= 1` → ins35 wrap → **ins17
  `Local[3] == Global[0]`** → ins19 `Attr[3]==0` → ins27
  `StackObject = MyObj[11]` (the saved car id) → **ins25 CALL 4106 'Create
  Car'** → ins31 `Attr[6] = 1800`.
  **The carpool spawns ONE hour before work start** (hour == Attr[0]−1), in the
  SAME hourly run that writes the bookmark (hour == JobData[12]−1) — the classic
  original-game behavior. The r155/r156 "Global[0] == Attr[0]-2" law followed
  the dead sequence; the −2 compare actually belongs to 4125's warning staging
  (its ins6-7: warnings fire at start−2). R156's −125 min rewind margin
  covered both windows, so no harness harm.
* 4106 'Create Car' dispatches on `Attr[1] == Tuning[1..11]` (the car-type
  table; Attr[1] is set by 4107 'set car type' = CARR JobData[21] CarType) with
  createObjectInstance per type — F255 on failure.

### 1.2 Why nothing ever fired — the block point is BEFORE the loop

r156-window-run2 facts re-read against the decode:

* At 7:00 `Attr[8]` flips 0→−1 (ins1 RAN) and 4125 was first-seen as a live
  frame (ins46 RAN).
* **4126 was NEVER seen on any stack** (distinct stayed {4096,280,4100,4125})
  — 4125 never RETURNED (neither true nor false; F253 on a CALL falls through
  to the other pointer, so a plain `false` return would still reach ins47).
* `Attr[0]` stays 0 — the portal's spawn value: **ins4 (`Attr[0] = -1`) never
  executed**, so the tree NEVER completed past the warnings block. The person
  loop (ins6+) — the entire R156 "per-person iteration" residual — never ran
  AT ALL. It is not a per-person gate skipping obj=21.
* The same log carries **143 suppressed `[SimAnticsExc]`** lines (three kinds:
  NRE @ VMStackFrame.cs:131, NRE @ VMThread.cs:732, and 129× IndexOutOfRange
  @ "VMAvatar.cs:789" — whose current source line is a bounds *check*, i.e.
  stale Release line attribution). Suppressed exceptions RESET the frame's
  Callee+Caller — a crash inside the 4100→4125 chain would produce exactly the
  observed never-completes loop: crash → portal reset → main → (minutes<5) →
  4100 → ins1 (Attr[8] stays −1) → ins46 → crash → …

## 2. INSTRUMENTATION (this round)

* **Engine (FreeSO)**: the `[SimAnticsExc]` console mirror (added R~118,
  throttled 600 ticks) printed only the message — it now appends
  `{callee=<id> iff=<owner> > routine:ip …}` (top 6 frames), so every
  suppressed exception is attributed to its entity and VM stack.
* **Harness (AutotestRunner carseek)**:
  * `portal-stack` tracer — every CHANGE of the portal's live stack signature
    (routine:ip per frame + top StackObject), deduped. A crash shows as a
    collapse back to [4096:…]; a block shows as a frozen signature.
  * `bad-routine-frame` detector — frames with null/empty Routine (the
    VMStackFrame:131 NRE source), reported once per entity with owner IFF.
  * `gate-replica` — at each hour:00, per avatar w56/w57/w69 +
    `Jobs.GetJobData(w56, w57, 12)` (the tree's own data path; −999 = provider
    threw) + BOOKMARK-NOW verdict (hour == start−1).
  * Discovery plan now reads the sim's OWN level (`GetJobLevel(jtype, jlevel)`)
    — r156 read level 0, which is not the tree's law (ins10: temp1 = w57).

## 3. RUNTIME RESULT (r157-targeted-run1 + r157-probe-run2)

The instrumentation named every node of the failure chain:

* **run1** (first instrumented build): `portal-stack [4096:0][280:0]` at soak
  start → at 7:00 exactly `[4096:2][4100:46][4125:15] stkObj=16` — and the
  signature **never changed again** (3.5 sim-hours). 'process' is frozen at
  4125 ins15 = `dialogPrivateStrings` (Flags=0x08, Continue NOT set — a
  BLOCKING dialog) while iterating person obj=16. `bad-routine-frame`:
  Paintings/Sofas objects carry null-Routine frames (the pre-existing
  `[SimAnticsExc] VMStackFrame:131` noise — separate, harmless-to-lot issue).
  The enriched exception mirror attributed the 129 r156-era avatar OOBs to
  `MAGIC - Ghost Social Test → Interaction - * TEST` check trees on obj=16/18
  (another separate residual, disclosed below). `gate-replica` at hour 8:
  `obj=21 w56=9 w57=0 w69=0 start=9 BOOKMARK-NOW` — the tree's data path is
  PERFECT; nothing is wrong with the person gates or JobData.
* **run2** (dialog-state probe): `dialog-state@discovery gbd=296/HelpSystemMagic.iff
  speed=1` and hourly `gbd=296/HelpSystemMagic.iff speed=1 portalBS=- wait=-` —
  the portal thread has NO BlockingState (so no timeout can ever accrue) and
  the global latch points at the Makin' Magic help object since lot load.

### 3.1 The complete causal chain (all nodes evidenced)

1. Lot load: HelpSystemMagic (obj 296) fires its blocking help dialog →
   engine law: `GlobalBlockingDialog = caller`, `SpeedMultiplier = -2` (pause).
2. The autotest harness's LOT-READY unpause (`AutotestRunner`) sets speed=1 —
   orphaning the shown-but-unanswered dialog WITHOUT releasing the latch
   (only the player's click — VMNetDialogResponseCmd — released it, and the
   timeout path didn't clear it either: two engine-side gaps).
3. 7:00: portal 'process' → 4125's warning dialog hits the primitive's
   queue branch (`GlobalBlockingDialog != null → CONTINUE_NEXT_TICK`) —
   STATELESS, no BlockingState, no timeout → **frozen forever**.
4. 'process' never reaches ins4/ins6+: Attr[0] never resets, the person loop
   never runs, no bookmark, no 'Create Car'. Every soak since the fixture
   existed. (A real player clicking the help dialog would NOT leak — but any
   unclicked dialog + external unpause reproduces it; the engine law is
   still wrong.)

### 3.2 THE FIX (engine + harness)

* **Engine (VMDialogPrivateStrings.ExecuteGeneric, three coordinated changes):**
  1. The queue branch now sets a QUEUED `VMDialogResult` (HasDisplayed=false)
     — WaitTime accrues every tick, so the shared DIALOG_MAX_WAITTIME (60
     sim-sec) bounds the wait: the tree proceeds exactly as a displayed-but-
     unanswered dialog times out (Message → GOTO_TRUE).
  2. The not-displayed path: while ANOTHER caller owns the latch, wait; when
     the slot is free, show AND take the latch + pause (a queued dialog that
     displays after a release must own the slot for the release law to work).
  3. The Responded/timeout path now releases the latch when THIS caller owns
     it (`GlobalBlockingDialog == Caller`) and restores the paused speed —
     VMNetDialogResponseCmd's release law applied to the timeout path (the
     leak). 
* **Harness**: all three unpause sites (LOT-READY, StateSample, StateCarpool)
  now orphan the stale latch when they force speed 1 — the unpause is what
  orphaned the dialog, so the unpause releases its latch; the shown dialog's
  own thread then times out cleanly via its BlockingState.

Expected post-fix sequence: 7:00 → warning dialog queues → (latch released at
load by the harness) shows + pauses → harness unpause releases → 60s timeout →
'process' completes → ins4 resets Attr[0] → 8:00 run: bookmark Attr[0]=9 +
staging (9−1 == 8) → **'Create Car' (4106) fires — the carpool arrives.**

(run3 verdict below)

### 3.3 THE VERDICT (r157-fixed-run3) — THE CARPOOL IS LIVE

Every decoded law verified live, in order:

* 7:00:00 — `portal-stack [4096:2][4100:46][4125:15] stkObj=16 bs=VMDialogResult`
  (the QUEUED state — the fix working; previously `bs=-` forever).
* 8:00:00.750 — `dialog-state gbd=null speed=1 portalBS=VMDialogResult wait=1800`
  — the shared timeout landing exactly at DIALOG_MAX_WAITTIME (1800 ticks =
  1 sim-hour at speed 1; the port's standing displayed-dialog timeout law,
  now applied to queued dialogs). Same second: the stack unwound to
  `[4096:1][280:0]` — **'process' COMPLETED for the first time ever**.
* 8:01 — `attrs[0=9, 1=7, 2=0, 3=1, 4=308, 5=16, 6=1800, 7=0, 8=0]`:
  Attr[0]=9 (the ins54 bookmark: JobData[12] at the sim's own level),
  Attr[1]=7 (car type), Attr[3]=1 (staging flag, ins2 of 4106),
  Attr[6]=1800 (the ins31 timer reset AFTER Create Car) — the full decoded
  bookmark+staging chain, live.
* 8:00-8:01 — `car-entity-change carEnt=19`: **18 CarJunk.iff cars spawned**
  (guid 1265757402); first-seen portal-owned **4102 'At Work'** (8:00:02),
  **4105 'send to work'** (8:00:02), **4116 'At School'** (8:00:04).
* 8:01-9:00 — the cars despawn after the pickup window (carEnt back to 1,
  then the paper carrier's morning round as usual).
* RESULT: `portal-owned=[280,4096,4100,4102,4105,4116,4125]`,
  `spawnedCars=18` — and the run's own SUMMARY: **passed=95 failed=0**
  (under the OLD pass law; the law is extended this round, see below).

The r155/r156 residual — "the commute-CAR tail never observed in any soak" —
is CLOSED: 4105/4102/4106/4116 all executed CarPortal-owned, cars spawned and
left. Root cause was never the person gates (they were perfect: gate-replica
showed BOOKMARK-NOW at hour 8 from run1 onward) — it was the port's TS1
dialog latch freezing 'process' at 4125 ins15 since lot load, every session.

### 3.4 PIN EXTENSION (never weakened)

carseek pass law = R155's portal-scoped law AND `commuteLive`:
portal-owned 4105 + 4102 present, `_portalAttr0Max > 0` (the bookmark sampled
at a top-of-hour), `_spawnedCarMax >= 5` (car-IFF entities beyond the portal
and paper carrier). RESULT line gains `spawnedCars=`/`attr0Max=`.

## 4. Residuals (honest)

1. **18 cars, not 1**: the spawn created 18 CarJunk cars in one window (the
   original shows one carpool car per household commuter set). Likely the
   minutes-0-4 re-runs of 'process' and/or the car-type dispatch with
   Attr[1]=7 hitting multiple create branches; NOT decoded this round —
   disclosed, next-round candidate (count law + 'set car type' 4107 flow).
2. The queued-dialog timeout is 1 SIM-HOUR (DIALOG_MAX_WAITTIME=1800 ticks
   at speed-1 tick rate) — it happened to bridge warning (start-2) to car
   (start-1) exactly in this fixture; in the original the player's click is
   instant. The wait is bounded now (the fix's goal); its duration is the
   port's standing timeout law, unchanged this round.
3. `MAGIC - Ghost Social Test → Interaction - * TEST` check trees throw
   person-data IndexOutOfRange on obj=16/18 continuously (129 in the r156
   window log; attributed by the enriched mirror this round) — separate
   residual, avatar check trees, no lot breakage observed.
4. Paintings/Sofas/phones/Lamps objects carry null-Routine frames
   (`bad-routine-frame`; the `[SimAnticsExc] VMStackFrame:131` noise) —
   separate residual, harmless to the lot, next-round candidate.
5. 4103 'get paid'/4104 'test promotion'/4119 'Create School Bus'/Chance
   Cards not yet observed live (they fire at/after work END ~15:00-17:00,
   beyond the soak target); the tail beyond the morning window stays open.

## 5. Files

r157-round.md (this doc), annotate_bhav.py (the decoder tool),
bhav4096/4100/4106/4125/4126.txt (annotated listings), r157-targeted-run1.log,
(and after the fix) r157-fixed-run*.log, r157-gate-run1.log.
r157-round.md (this doc), annotate_bhav.py (the decoder tool),
bhav4096/4100/4106/4125/4126.txt (annotated listings),
r157-targeted-run1.log (instrumented soak — the frozen [4125:15] signature +
gate-replica BOOKMARK-NOW), r157-probe-run2.log (dialog-state probe —
gbd=296/HelpSystemMagic.iff, portalBS=-; killed after the evidence landed),
r157-fixed-run3.log (THE VERDICT: carpool live, 95/0 under the old law),
r157-gate-run1.log (FULL GATE 95/95 under the EXTENDED law —
spawnedCars=18 attr0Max=9 portal-owned 4102/4105/4116; clean exit chain;
DLL byte-match vs publish verified: Simitone.Client 8c874a91…,
FSO.SimAntics 25c8f3cb…).

Errata: appended to ../r155/r155-attribution-fix.md (the -2 dead-code law)
and ../r156/r156-round.md (window + residual framing).

---
**R158 ERRATUM (residual 1 "18 cars, not 1"):** the 18 was the probe's own
artifact — the spawnedCars counter counted lot ENTITIES, and one carpool car
is a MULTITILE object with 18 parts (CarJunk.iff = 19 OBJDs). [CarCreate]
proved exactly ONE createObjectInstance (portal 4106:20) made the spawn, and
spawn-detail showed all 18 entities as one group (g308x18) at a 3x6 curb
footprint. The port spawns ONE car per household — the original's law. The
pin's >=5 became >=1 GROUP in r158. See ../r158/r158-round.md.
