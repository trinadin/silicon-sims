# R247 skeptic re-verification (adversarial pass)

Independent re-derivation of the r247 tutorial-lifecycle decode straight from the
executable (own capstone driver, own bl/constant scans, inline PEF unpack).
Method: every cited range re-disassembled with context; bl-target scans over the
full code section for ResetTutorial/TutorialCompleted/CancelTutorial/codec and
GUID lookups; lis/addi and raw-word scans for GUID 0xc3249a1d; branch polarity
read instruction-by-instruction; all 116 verify.py pins mechanically re-checked
(0 mismatches) plus the data pins re-derived from the packed data section.
verify.py PASS reconfirmed.

Per-claim verdicts:

1. ResetTutorial — CONFIRMED (with DoSave prose correction, #3 below).
   Dialog slot 0x114, args (TOC−0xbe4, TOC−0xbe0, style 1, 0), `cmpwi r3,3;
   bne exit` at 0x258398; DoSave gate branch law correct; path build
   (FindDataDirectory ×2 + GetString(7)='UserData/' / GetString(0x58)='UserData/Import/'
   + last-char sep fixup vs 0x2f/0x5c + 'Tutorial.FAM' at blob 0x534d8+0x1621)
   instruction-exact; CopyFileA(src,dst,1), ==0 → style-0 dialog (−0xbe8,−0xbe0);
   attr==0xFFFFFFFF test (addis r0,r4,1; cmplwi 0xffff) exact; SetFileAttributes
   (attr & ~1 via rlwinm 0,0,30); success → DoNbhdScreen(app,1). Return-3 law
   cross-checked in Quit (0x257b90): BOTH style-1 dialogs map result 3 → r27=1,
   and r27!=0 is what performs the quit; style-3 mapping there is
   {4=save+quit, 2=quit-without-saving, 3=cancel}.
2. TutorialCompleted — CONFIRMED (with +0x492 framing correction, #2 below).
   Exactly 3 bl sites (0xf1f94/f1fa8/f1fbc); jump-table data 0x485a8 entries
   0/8/9 → 0xf1f88/f1f9c/f1fb0, args 0/1/2, receiver = *(app+0x24);
   house-number source nbhd+0x164 = GetHouseNumber (0xafb50 = `lwz r3,0x164(r3)`);
   SIMI patch arg-0-only (temp sim 0x8d720 → SetGlobal(26,0) → 0x8d9b0 with
   loaded pos; live sim = holder→app→core+0x20, arg-0-only); live +0x12a=arg+1,
   +0x12c=0; SetGlobal = sth at sim+0x10+2i (0x138c20, also r168-pinned).
3. CancelTutorial — CONFIRMED; KEY RESOLVED. Sole site 0x217ad0; core+0x14
   module; GetTutorialObject = `lwz r3,0xac(r3)` (0xe2d80); tree name =
   strtable 0x45604+0xc5 = "cancel tutorial" (RunTree 0xefd30 appends its r6 as
   the name); KillObject((s16)owner+0xf0); caller hides winmgr+0x20c via vt+0xa0.
   The keydown switch is `cmpwi r30, 0x1b; beq 0x217ac8` at 0x217608/0x217614,
   where r30 = TSOnKeyDown arg2 (key code): **ESC (0x1B)** — closes the
   decode's UNRESOLVED. (Neighbors: 0x09 tab, 0x0d return.)
4. Spawner — guards CONFIRMED; cheat direction INVERTED in decode (#1 below).
   Order/polarity at 0xb0540..0xb05a0: `*(TOC−0x727c)!=0 → skip`;
   `lha 0x12a; cmpwi 3; bge skip`; `lha 0x44(r27); cmpwi 0; beq skip` —
   spawn requires **global 26 != 0** (decode polarity correct), sim = r27 =
   *(app)+0x1c per LoadHouse head; GetObjectByGUID(0xc3249a1d) != 0 → skip;
   GetSelectorByGUID == 0 → skip; MakeNewOutOfWorldObject. GUID built twice
   (0xb0568/0xb0570 with an interleaved `mr`; 0xb0584/0xb0588); exhaustive
   scans: no other lis 0xC325 construction in the code section and no raw
   0xC3249A1D word anywhere in the file → LoadHouse tail is the only creation
   path. Sweep law (0xb04c8..0xb053c) matches: ((w>>1)&3)==1 ∧ (s16)+0x606==0
   ∧ (s16)+0x612 != nbhd+0x164 → KillObject((s16)+0xf0).
5. HouseInfo — CONFIRMED (price float source corrected, #5 below).
   GetHouseFileInfo (r141 size **288**, not "~1100"): temp-sim reconstitute
   from 'SIMI' v1 with position → *p6; +0x18=(s16)g54 (sim+0x7c);
   +0x20=(s16)g69 (sim+0x9a); +0x14=(s16)g58 (sim+0x84, stored unconditionally);
   +0x10 = 1 iff g58==0 ∧ g53(sim+0x7a)==0; no SIMI save-back (read-only).
   Caller 0x232c48..0x232c68 wires (hi+0x1c, hi+0x14, …) and this=app+0x24.
   MoveInModeLotHandler (0x471d80): lotData = this+0xec[lot−1]→+0x18c; FIRST
   guard `lwz r0,0x14(r25); cmpwi 0; beq` → !=0 → MessageDialog(const char*,
   style 0) and exit, before family-valid (GetFamilyInfo) and the +0x10/price
   guards. Refusal polarity/order CONFIRMED.
6. NGBH codec/file — CONFIRMED. ReconstituteLoadObject<12Neighborhood> (0xb83b0)
   wraps ReconBuilder::Reconstitute (0x11dd90); ReconstituteSaveObject (0xb85c0)
   wraps ReconBuilder::Compact (0x11de80) — the standard recon-stream codec.
   The second IFFResFile2::Open passes the nbhd object itself as the path, the
   same idiom as Neighborhood::Load (0xb5a0c opens the file with r4=nbhd, then
   reconstitutes NGBH from it); InitGame (0x889e4..0x88a30) builds
   GetString(7)+GetString(0xE='Neighborhood.iff') and stores the singleton at
   app+0x24. The NGBH write therefore targets the NEIGHBORHOOD file, and only
   the house file (Houses/House%02d.iff) receives the arg-0 SIMI patch.

## Corrections (severity-ordered)

1. **HIGH — `tutorial` cheat direction inverted (decode.md §F and its pin
   comment).** Case 0x250128 computes `flag = (param == 0)`, not `(param != 0)`:
   `clrlwi r0,r24,0x18; cntlzw r0,r0; rlwinm r0,r0,27,24,31; stw` yields 1 iff
   param&0xFF == 0. r24 is the parsed param (init `li r24,0` at 0x24e524;
   type-2 parse path 0x24e58c..; special words blob 0x534d8+0x57e = **"on"** →
   r24=1 (0x24e62c) and +0x581 = **"off"** → r24=0 (0x24e658); numeric parse
   otherwise). So `tutorial on`/1 → inhibit 0 → spawn allowed; `tutorial off`/0
   → inhibit 1 → spawn disabled. decode.md's "tutorial 1 forces the spawn off,
   tutorial 0 re-allows" is backwards.
2. **HIGH — "+0x492/+0x494" are stack addresses, not object/chunk fields.**
   The temp Neighborhood is at r1+0x368 (0xadb08); `sth r26, 0x492(r1)`
   (0xadb74) and `sth r0, 0x494(r1)` (0xadb80) therefore write object offsets
   **+0x12a and +0x12c — the same fields as the live Neighborhood** — before
   ReconstituteSaveObject re-serializes. The decode's pseudocode
   (`rec.tutorialState = arg + 1  # obj +0x492`) and the JSON law string
   "(+0x12a/+0x492)" invite a port to write a bogus NGBH-chunk offset 0x492.
3. **MEDIUM-HIGH — DoSave internals inverted in prose (decode.md §B note).**
   The gate-failure path (app+0xbf==0 OR GetHouseNumber()==0) branches
   0x2593d8→0x259468→0x259470→0x2594a4 and **returns 1 (proceed)**, not 0.
   Full law: if +0xbf (dirty byte, cleared after a successful save at
   0x2594a0 — not a "save-enabled byte") is 0 or no house is loaded → return 1;
   else prompt slot-0x114 style 3 (TOC−0xbd4/−0xbd0): result 1 → return 0;
   result 4 → if *(TOC−0x7110) byte set return 1, else SaveGame(): fail → 0,
   success → clear +0xbf and return 1; any other result → return 1. The
   ResetTutorial branch itself (`(DoSave & 0xff)==0 → return`) is correct, but
   a port implementing DoSave from the decode's sentence ("you cannot reset the
   tutorial unless a save is possible") would abort the reset when not in a
   house — the original proceeds. ResetTutorial's caller-visible law is only:
   abort iff DoSave returns 0.
4. **MEDIUM — CancelTutorial physical key resolved: ESC (0x1B)** — see claim 3.
   Replaces the decode's UNRESOLVED item; no extra guard beyond the function
   head's child-window check (view+0x16c).
5. **LOW-MEDIUM — price-law float source corrected.** `lfs f2, 0(r7)` at
   0xb0b14 loads from `*(TOC−0x7274)` = **BSS 0x852b0**, a runtime global
   (writer not found; no `lwz -0x7274(r2)`+stfs pair in the code section) —
   not "the float at HouseInfo+0x40": HouseInfo+0x40 is the SIMI reconstitute
   position out-param (long*, written by the 0x8d720 call via r7=r30=arg10).
   Price = fctiwz(f * (double)convert(sim+0xe4 word, xoris 0x8000 magic))
   + u32[sim+0xdc] + u32[sim+0xe0], stored to p1 (HouseInfo+0x1c).
6. **LOW — GetHouseFileInfo size** is 288 bytes per r141 (0xb0a40..0xb0b60),
   not "~1100" as the decode's cast table says (all cited pins still fall
   inside the real function).
7. **INFO — style-3 dialog enumeration context** (from Quit): 4 = save+proceed,
   2 = proceed without saving, 3 = cancel/no-op. Only "3 = proceed" for style 1
   is invariant across dialogs (two independent Quit sites + ResetTutorial).

## verify.py audit

- PASS reconfirmed; all 116 instruction pins mechanically re-read from the
  binary: 0 mismatches. Data pins (TOC slots, strtable/blob strings, jump
  tables, 'SIMI', IFF magic, cheat names) re-derived independently: all match.
- Pins are genuine site words, not tautologies, but pins alone never encode
  polarity — the two interpretation errors above (items 1 and 3) live in the
  prose/fixtures, which is exactly where this audit found them.
- Fixture tautologies: fixture (d) contains dead padding (`tutorial_completed()`
  computing `state*0 + (g26*0)`); fixtures (c)–(g) transcribe interpretations
  (internal consistency only); fixtures (a)/(b) decode real binary tables
  (generic-call entries; GetString(7)/(0x58)) and are sound.

## Port cross-check

- `FreeSO/TSOClient/tso.simantics/Primitives/VMGenericTS1Call.cs`: operand is
  one byte = `VMGenericTS1CallMode` — the same index space as the original's
  (s16)operand jump table (original bounds it with `cmplwi 0x2b`).
- Modes 0/8/9 are **not implemented**: `Model/VMGenericTS1CallMode.cs` names
  them HouseTutorialComplete=0, FamilyTutorialComplete=8,
  ArchitectureTutorialComplete=9 (FreeSO upstream names — independently
  corroborates r247's semantics), but the switch has no cases for them;
  unmatched modes fall through to `return VMPrimitiveExitCode.GOTO_TRUE`
  (silent no-op). No conflicting meanings to untangle.
- r168 (modes 12/13/14) came from the same jump table (its entry 14 = file
  0xf273c equals this round's gen[14]); this pass's independent table read
  agrees, so 0/8/9 → TutorialCompleted(0/1/2) is the correct reading of the
  same dispatch.

Verdict: **SAFE TO IMPLEMENT after applying corrections 1–3** (cheat direction;
+0x12a/+0x12c object-field framing instead of "+0x492/+0x494"; DoSave
proceed-on-gate-failure semantics). All other load-bearing claims re-derived
and confirmed.
