# R158 — the carpool COUNT law: "18 cars" was one car (probe artifact), count CLOSED

The R157 recommendation: decode the 18-cars-vs-one count. Baseline: main
a5e61a6 / FreeSO aceb9774, green via r157-gate-run1.log (95/95, clean exit).

## 1. Static decode (callers + the type law)

All call sites of the car-creation trees inside CarPortal.iff (scan of every
instruction opcode; listings in this directory):

* **4106 'Create Car' has exactly ONE caller**: 4100 'process' ins25 (the
  staging). Its structure (pointer table verified): ins0 saves StackObject →
  ins8 restores it → ins22 `Attr[1]==Tuning[0]` → the type dispatch
  (ins1..ins28, Tuning[1..11]) → each branch creates ONE car → ins7
  `findLocationFor` → **ins3 `Attr[4] = StkObjID`** (the create sets StackObject
  to the new object — saves the car id) → **ins2 `Attr[3] = 1`** (sets the
  staging flag that blocks re-creation) → END. ins21's CarStd create is only
  the no-type-matched DEFAULT branch (targeted solely by ins28F). No loop
  inside 4106; the earlier r157 reading ("ins20→ins21 chains") was wrong.
* **4107 'set car type'** (called by 4100 ins55, right after the bookmark
  ins54): career-track special cases (JobType 1..10 → Tuning[k]; the level-7
  limo check), otherwise **ins18: `Attr[1] = JobData[21]`** — the CARR
  **CarType** field at the sim's own level (the r157 "Attr[1]=7 with no traced
  setter" residual: RESOLVED — 7 is Test Subject's CarType).
* 'At Work' (4102, runs on the AVATAR) calls 4105 'send to work' + 4120 'set
  car type from work' + 4121 'create car from work' (the RETURN car at
  EndTime); 'At School' (4116) calls 4105 + 4119 'Create School Bus'.
  4120/4121 mirror 4107/4106 but on StkObjAttr (the sim's saved car slot,
  MyObj[11]).
* The companion guid in 4106's table (b2ed6ddb) = **CarStd.iff** (corpus
  OBJD scan) — the default-branch car; the Tuning[8] branch guid daec714b =
  CarJunk.iff (the observed spawn — CarType 7).

## 2. Instrumentation

* **Engine (FreeSO): `[CarCreate]` mirror** in VMCreateObjectInstance — every
  car-IFF object creation logs the created entity + guid + CREATOR (entity,
  owning IFF, routine:ip, StackObject). Volume-bounded by construction (only
  car-named IFFs: carpool bursts + the morning paper carrier).
* **Harness**: `spawn-detail` on every spawn-count change — per car entity:
  multitile group base id, group size, tile position.

## 3. The verdict (r158-run1/run2)

* `[CarCreate] obj=308 iff=CarJunk.iff guid=0x4B71ECDA by=8/CarPortal.iff
  routine=4106:20` — **exactly ONE createObjectInstance** made the entire
  spawn (plus the paper carrier's own `[CarCreate]` by PedPortal later).
* `spawn-detail n=18 [292(g308x18@43,36), 298(g308x18@44,36), 308(g308x18@45,36),
  310..324(g308x18@...)]` — all 18 entities are ONE multitile group (base
  308) at tiles x 43-45, y 31-36: **a 3x6 curb footprint — one car parked
  along the street**.
* Statically confirmed: **CarJunk.iff contains 19 OBJD chunks** (master 16807
  + slaves 16808-16825) — the car IS a large multitile street object.

**The r157 "18 cars" residual was this probe's own counting artifact**: the
entity scan counted every multitile PART of the single car. The port spawns
ONE carpool car per household per morning — the original's law. NO port gap.

## 4. Corrections + pin

* `spawnedCars` now counts DISTINCT MULTITILE GROUPS (not entities); the pass
  law's `>= 5` (set in r157 on the artifact) is corrected to `>= 1` group —
  an ERRATUM on r157's residual, disclosed here and in PARITY; the rest of
  the r157 law is untouched (4105/4102 portal-owned, bookmark > 0).
* RESULT line and spawn-detail remain as diagnostics.

## 5. Residuals (honest)

1. 4103 'get paid' / 4104 'test promotion' / 4119 school bus / Chance Cards
   fire at/after work END — beyond the soak window, unobserved (standing
   r157 residual).
2. The paper carrier's `[CarCreate] by=6/PedPortal.iff routine=4104:1` —
   PedPortal has its own same-id tree space; fine, logged for the record.
3. Ghost-social-test OOB exceptions + null-Routine frames (standing r157
   residuals, untouched this round).

## 6. Files

r158-round.md (this doc), bhav4102/4105/4107/4115/4116/4119/4120/4121.txt
(annotated listings), r158-run1.log ([CarCreate] attribution + 95/0),
r158-run2.log (spawn-detail group proof + 95/0), r158-gate-run1.log (full
gate under the corrected metric).
