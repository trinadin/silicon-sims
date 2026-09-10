# R155 — THE CAREER COMMUTE TAIL: MISATTRIBUTION CORRECTED, PROBE MADE OWNER-PRECISE

Round target (the R154 recommendation): pin the career commute tail — 4102
'At Work' + 4106 'Create Car', "observed live in four consecutive soaks".
Baseline: main 001472b / FreeSO e4a31fab, green via r154-gate-run1.log
(94/94, clean exit).

## VERDICT UP FRONT

**The R151-R154 "commute tail observed" record was a MISATTRIBUTION, now
corrected: the 4102/4106 ids seen live were owned by
`People\PaperCarrierF1.iff` — labels 'special drop function' / 'special
drop tabloid function' — NOT CarPortal.iff's 'At Work' / 'Create Car'.**
The carseek probe attributed frames by "owning IFF filename contains car",
which matches Paper**Car**rier; and "car-entity-max=2" was CarPortal (the
always-present portal entity) + the PaperCarrier NPC — **no carpool car
has ever spawned in any soak**. The career residual is back to its true
R84 shape, now with a decoded guard chain and a runtime probe aimed at
the failing input. The genuine CarPortal-owned live set (5/5 stable
across r151-r154) is exactly {4096 'main', 280 'idle', 4100 'process',
4125 'process warnings car pools'} — and that is what the extended pass
law now requires, owner-attributed and label-anchored.

Same failure mode as R154's byte-swap: id matching without (owner, label)
attribution. In both cases the pin/gate discipline caught or exposed the
error; in both cases the fix is owner-precise identification.

## 1. THE EVIDENCE (from the existing soaks, no new interpretation)

* first-seen lines (every r151-r154 gate log):
  `id=4102 label=special drop function owner=People\PaperCarrierF1.iff`,
  `id=4106 label=special drop tabloid function owner=PaperCarrierF1.iff`,
  `id=4099 label=drop paper owner=PaperCarrierF1.iff` — the paper
  carrier's own tree set. CarPortal-owned first-seens: 4096/280/4100/4125
  only.
* car-entity-change lines: `carEnt=1 [CarPortal.iff#id8]` →
  `carEnt=2 [CarPortal.iff#id8, People\PaperCarrierF1.iff#id24]` → `1`
  — the "2" is portal + paper carrier arriving ~9:07, not two carpools.
* RESULT lines r151 run1/run2, r152, r153, r154: IDENTICAL id sets and
  car-entity-max=2 — the stability that made the wrong pin attractive was
  the stability of the paper carrier's morning round.

## 2. THE GUARD CHAIN (why Create Car never fires — static decode)

CarPortal.iff (Objects.far member; BHAVs extracted + decoded with the
port's own operand schema — VMExpressionOperand: lhs i16, rhs i16, signed
u8, operator u8, lhsScope u8, rhsScope u8):

* **'process' (4100, 58 ins)** iterates persons (op 31 walk from ins5-6);
  per person: skip NPCs `StkPD[32] > 0`, require `StkPD[56] > 0` (JobType)
  and `StkPD[69] != -1` (per-day bookmarker), then compares bookmarked
  hours (portal Attr[0], Attr[8] = min of family `JobData[12]` start
  hours, ins53-57) against `Global[0]` (clock hour) with ±24h wraps and
  day-bookmarks (Attr[7] vs Global[1]), setting Attr[2]/Attr[3] stage
  flags — and at ins25 **calls 4106 'Create Car'** when the staged flags
  line up (also ins28 'check for school bus schedule' 4115, ins30
  'lower all kids grades' 4117, ins46 4125 'process warnings').
* 'send to work' (4105): `MyPD[58] < 18` (child vs adult split), then
  `Global[0] < Tuning[130]` arrival cutoff.
* 'main' (4096): `Global[5] < 5` top-of-hour gating around the 4100 call;
  `Global[20] flagSet 4` edition flag (the activator writes GlobalState[20]=255).

The failing input is NOT yet fully identified — JobData scope (33) IS
implemented in the port (VMMemory.cs:136-138, reads the Jobs provider via
temp0/temp1). Run1's new diagnostics already eliminate one suspect: the
working avatar (obj=21, JobType=9) has word69=0 (not -1), so the
already-gone-today skip does NOT fire — the failure is deeper, in the
Attr[0]/[8] bookmark vs Global[0] hour matching or the Attr[2]/[3] stage
flags. **The soak now logs all of these at discovery** (portal-attrs
[0..8] + per-avatar JobType/PersonType/w68/w69) so the next run names the
failing condition.

## 3. THE PROBE FIX (AutotestRunner carseek)

* Frames are attributed per owner IFF (`_careerFrameOwner`), and the RESULT
  line splits `portal-owned=[...]` from `paper-owned=[...]`.
* Entity counting gains `portalEnt`/`portal-entity-max` (CarPortal.iff
  entities only); the loose car-substring count is still logged.
* Discovery label anchors all four CarPortal routine labels
  (4096 'main', 280 'idle', 4100 'process', 4125 'process warnings car
  pools') into `_carLabelsAnchored`.
* **Second attribution nuance (found by run1's own FAIL):** CarPortal.iff
  contains NO chunk 280 (direct parse) — the live 'idle'(280) frames are
  global.iff's routine executing on the portal's frame SCOPE. The label
  anchor therefore requires the three real chunks (4096 'main', 4100
  'process', 4125 'process warnings car pools'); 280 stays required as a
  portal-SCOPE id in the pass law.
* **Pass law (strictly stronger than R84's, extend-never-weaken):** old =
  frames>0 && 4100 in ids && 4100 label 'process'. New = old AND
  portal-scope 4096/280/4100/4125 present AND the three-label anchor AND
  portal-entity-max >= 1. Every R84-true run stays true; five runs of
  r151-r154 logs satisfy the new law (their first-seen/owner lines contain
  the four CarPortal ids).
* Diagnostics: portal-attrs + commute-inputs lines at discovery (§2).

## 4. RECORD CORRECTIONS

* PARITY current-gaps: the career entry's "(R151 soak update: 4102 'At
  Work' and 4106 'Create Car' WERE observed live ...)" claim is RETRACTED
  (paper-carrier misattribution); the residual is the full commute tail
  4105/4106/4102/4103/4104/4119, with the guard chain + runtime probe as
  the new narrowing.
* Historical ledger rows 151/153/154 keep their text (they are the record
  of what was believed then); this row + the gap text carry the correction.
* The carseek RESULT format changed (portal-owned/paper-owned split) —
  old logs remain interpretable via their first-seen lines.

## 5. Files (tools/iff-dump/r155/)

r155-attribution-fix.md (this doc), r155-targeted-run1.log (the honest
labels4=False FAIL that exposed the chunk-280 nuance, + diagnostics),
r155-targeted-run2.log (7/7 PASS, attribution-fixed RESULT), and the
full-suite log r155-gate-run1.log (94/94 PASS clean exit, same RESULT
format). Static decode evidence:
CarPortal BHAV listing regenerated on demand via tools/dump_iff_bhavs.py
on Objects.far!CarPortal.iff (846 KB member); decode schema = the port's
VMExpressionOperand/VMVariableScope (cited file:line in the round doc).

## 6. Residuals (honest)

1. The commute tail (4105/4106/4102/4103/4104/4119) has NEVER fired in any
   soak — now correctly stated, with diagnostics in place to identify the
   failing guard input on the next run.
2. The exact semantics of op 31 (the person iterator) and op 36 in
   'process' were read structurally (from the control flow), not
   instruction-schema-decoded; the guard-chain summary is INTERP at those
   two nodes.
3. Whether a real-machine original would spawn carpools in the same
   fixture is assumed (the flow is original IFF); no original-run
   comparison exists.

---
**R157 CORRECTION (the "-2" law was dead code):** section 2's "ins12-14:
Local[3] = Attr[0]-2" reading of 'process' followed a DEAD sequence — ins14's
pointers are T34/F34 (both equal): the compare has no control effect, and
ins34 reassigns Local[3] = Attr[0] before the live chain ins20 (Local[3]-=1)
-> ins17 (`Local[3] == Global[0]`). The LIVE Create-Car law is
**hour == Attr[0]-1** (one hour before start, same run as the bookmark at
hour == JobData[12]-1). The "-2" compare belongs to 4125's warning staging
(its ins6-7: warnings at start-2). The r155 guard-chain operand decode itself
stands (scopes/indices unchanged); only the -1/-2 attribution moved. See
../r157/r157-round.md + bhav4100.txt (full annotated listing).
