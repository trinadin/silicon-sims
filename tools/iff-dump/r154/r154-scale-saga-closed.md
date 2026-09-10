# R154 — THE INTEREST-SCALE SAGA RESOLVED: THE MIGRATION PIPELINE IS REAL AND WIRED

Round target (the R153 recommendation): trace who calls the script-side
'Convert Interests to 0-1000' routine — the ×100 bridge the r151 §2.4
tension hypothesized — and decide whether the port needs it. Baseline:
main 6a51d68 / FreeSO e4a31fab, green via r153-gate-run1.log (93/93).

## THE ROUND IN ONE SENTENCE

The research agents first delivered a "dead script" verdict built on
byte-swapped ids; the new `uiconv` gate check FAILED on the real data and
exposed the misread; the corrected scan reverses the verdict: **the
Hot-Date-era interest migration pipeline EXISTS, IS WIRED, and runs for
exactly five expansion NPC species — and the port converges with it
without any code change (disclosed).**

## 1. THE ID RESCUE (how the misread was found and what the true ids are)

The agents reported "BHAV 9761 / 39200" and a five-way negative (0 callers
anywhere). The `uiconv` check — pinning those ids in the mounted
PersonGlobals.iff — failed with `BHAVs=473 idRange=8192..8664`. Direct
re-parse of Global.far's members (big-endian container rows, LE chunk
bodies — the IFF container is BE; FSO's IffFile.AddChunk reads size/id/flags
BE, bodies LE) shows:

* PersonGlobals.iff: 473 BHAVs, ids exactly 8192..8664. `9761`/`39200` DO
  NOT EXIST. The converter is **BHAV 8486** (0x2126) and the top-up is
  **BHAV 8345** (0x2099) — the "9761"/"39200" were **byte-swapped reads**
  of the little-endian rsmp u16s (0x2126 read BE = 0x2621 = 9761; 0x2099 BE
  = 0x9920 = 39200). The same swap hit the routine-callers' ids: 'person
  main loop' is chunk **8283** (0x205B, agent wrote 23328 = 0x5B20) and
  'load tree' is **8244** (0x2034, agent wrote 13344 = 0x3420).
* The routines themselves are exactly what the agents decoded (their
  instruction listings are valid, only ids were swapped): 8486 = 23 ins,
  ins0 `op=2 T=1 F=254 word=46 lit=10` (the ≤10 gate — same law as the
  engine's randomizer gate, r153), raw `020001fe2e000a00000f1207`; 8345 in
  PersonGlobals = 38 ins, ins0 `T=254 F=254 word=13` (DISARMED entry).

## 2. THE CORRECTED VERDICT (r154-callers-rescan-trueids.txt)

**DIRECT CALL HITS: 15** — the migration pipeline is WIRED in exactly the
five expansion NPC globals that carry the ARMED 8345 (SalesClerk,
VacationCarnie, VacationDirector, VacationMascot, Waiter — all in
Global.far):

* **'person main loop' (8283)**: instr[38] `CALL 8345` (T→39, F→253) then
  instr[39] `CALL 8486` (T→1 = loop head, F→253). Unconditional per
  main-loop iteration; the callees' own gates make it a one-shot
  migration: 8345's guard = all five HD words (13/14/16/20/26) == 0 →
  top up raw 0..10 with tier caps {2,2,1}; 8486's gate = all eight base
  words 46..53 ≤ 10 → ×100 on all 15 topics. After one pass both gates
  skip forever (words now ×100).
* **'load tree' (8244)**: instr[17] `CALL 8345` (T→18) behind an age check
  (word 58 = PersonsAge) — the load-time top-up without conversion;
  conversion happens on the first main-loop tick.
* **PersonGlobals (residents)**: 8486 armed but UNCALLED; 8345 disarmed.
  Residents NEVER migrate — consistent with the R153 load-order finding
  (house saves keep raw words forever).
* RentalClerkGlobals: 8345 disarmed, no callers (the downtown rental clerk
  was exempted).

Event semantics: when a Hot-Date/Vacation-era expansion NPC (sales clerk,
carnie, director, mascot, waiter) instantiates with pre-HD raw interests,
its main loop tops up the HD topics raw and converts everything ×100 —
the engine-side companion to the shipped ×100 TemplateNPC data (r151).

## 3. WHAT STANDS FROM THE AGENT ROUNDS (id-independent)

* **Engine binary: 0 references** — re-verified with the TRUE ids
  (r154-anchor-9761.txt RESCAN section: 0 STRONG/WEAK immediates of
  0x2126/0x2099, 4 branch-displacement coincidences; constscan true-id
  file: 2 unaligned data-byte coincidences). The structural finding
  stands unconditionally: the engine hardcodes NO numeric tree id — all
  46 tree-runner sites use the data-driven ObjFnTable::GetTreeID.
* **Decoded TTABs: no wiring for these routines** — rescan with true ids
  (r154-ttab-rescan-trueids.txt): the only 8345 hit is
  CatGlobals/DogGlobals TTAB 33024 `test=8345` = the UNRELATED 'Interaction
  - Hug TEST' id collision (pets; legitimate wiring of a different routine
  that shares the id). 8486: no TTAB references anywhere. The 55 raw
  byte-pair hits in degenerate TTABs are unvalidated noise (disclosed).
* **By-name: 0** — unchanged (names are strings; 0/2,479 resolutions).
* **Port static paths: 0** — the port never initiates these calls; only
  restored NPC script stacks could.

## 4. PORT CONSEQUENCE (no code change, disclosed)

The port converges with the original's final state without porting the
migration: (a) freshly generated NPCs get ×100 interests directly from the
R152 generator (= post-migration state); (b) expansion NPCs loaded from
original saves whose threads sit in 'person main loop' re-execute
instr[38/39] in the port's VM — the migration runs itself; (c) house-5
gate avatars (PersonGlobals species) are unaffected — no callers, matching
uiexpint's zeros pins. Stored-word divergences for snapshot NPCs that
never re-run the loop are display-neutral via the R150 bridge. Porting the
migration as an explicit load step would be INTERP-driven; not done.

## 5. GATE (93 → 94)

NEW check `uiconv` (`CheckInterestConversion`): pins in the STAGED data at
runtime — PersonGlobals BHAV 8486 (23 ins, label verbatim, ins0 = the ≤10
gate T1/F254 w46/lit10, ins1 → w47), PersonGlobals BHAV 8345 (38 ins,
disarmed ins0 T254/F254 w13/lit0), and SalesClerkGlobals BHAV 8283
instr[38]=8345 / instr[39]=8486 (the wired migration sequence). The
corpus-caller census and engine zero are static RE (docs + PARITY). The
check itself is the round's canary: its first run FAILED against the
agents' byte-swapped ids and forced the rescue.

## 6. Files (tools/iff-dump/r154/)

Corrected scans: r154-callers-rescan-trueids.txt (15 hits),
r154-ttab-rescan-trueids.txt (pet-collision only),
r154-engine-constscan-trueids.txt. Agent originals (ids superseded by §1,
machinery/structure valid): r154-script-callgraph.md,
r154-engine-wiring.md, r154-callers-dump.txt, r154-bhav-index.json,
r154-ttab-encoded-scan.txt, r154-routine-listings.txt, r154-caller-graph.*,
r154-engine-trees.txt, r154-anchor-9761.txt (RESCAN section appended).
Runs: r154-targeted-run1/2/3/4/5.log (run1 = the canary FAIL),
r154-gate-run1.log (see PARITY).

## 7. Residuals (honest)

1. Whether the five-species migration fires for any SPECIFIC shipped save
   NPC in practice (old pre-HD downtown residents-of-record) is a data
   question — the port's convergence makes it display-neutral; a downtown
   soak could observe it live (follow-up).
2. The 55 raw TTAB byte-pair hits in degenerate tables are unvalidated
   (no decoder for those variants); the 1,355 field-encoded tables are
   clean.
3. 8345's armed top-up law (tier caps {2,2,1} raw 0..10) is decoded but
   not ported (nothing in the port calls it; see §4).
4. The engine-wiring agent's vtable/census residuals carry over unchanged.
