# R247 skeptic-response — decode → skeptic review → repair provenance

The r247 lifecycle decode (decode.md as originally written) was independently
re-derived by an adversarial skeptic pass; the full evidence is in
`skeptic-corrections.md` (verdict: SAFE TO IMPLEMENT after corrections 1–3).
This file records, per finding, what the skeptic claimed, how it was
re-verified against the executable before applying, and what changed.

## C1 (HIGH) — `tutorial` cheat direction was inverted

* Original decode: the cheat case 0x250128 sets the spawner-inhibit flag to
  `(param != 0)`, so "tutorial 1 forces the spawn off".
* Skeptic: the case computes `(param == 0)`; the cheat parser maps the special
  words "on"/"off" to param 1/0.
* Re-verification (independent, before applying): emulated
  `clrlwi r0,r24,0x18 → cntlzw → rlwinm r0,r0,27,24,31` for param 0/1/2 →
  flag = 1/0/0, i.e. **flag = (param == 0)**. Parse sites confirmed in the raw
  words: 0x24e62c = `li r24, 1` ("on", blob+0x57e), 0x24e658 = `li r24, 0`
  ("off", blob+0x581), init `li r24, 0` at 0x24e524.
* Repair: decode.md §F now states inhibit = (param == 0) — `tutorial on`/1 =
  spawn ALLOWED, `tutorial off`/0 = spawn DISABLED — and the LoadHouse gate
  (`*( *(TOC−0x727c) ) != 0 → skip`) is unchanged. New pins: 0x250130
  (cntlzw), 0x250134 (rlwinm), 0x24e62c, 0x24e658.

## C2 (HIGH) — "+0x492/+0x494" were stack addresses, not object/chunk fields

* Original decode: TutorialCompleted writes "NGBH record fields +0x492/+0x494"
  on the reconstituted Neighborhood (and separately "+0x12a/+0x12c on the
  live object").
* Skeptic: the temp Neighborhood lives at r1+0x368 (ctor at 0xadb08), so
  `sth r26, 0x492(r1)` (0xadb74) / `sth r0, 0x494(r1)` (0xadb80) write object
  offsets **+0x12a / +0x12c — the same fields as the live stores**.
* Re-verification: 0x368 + 0x12a = 0x492 and 0x368 + 0x12c = 0x494 — exact.
  The instruction pins at 0xadb74/0xadb80 were already correct; only the
  framing was wrong.
* Repair: decode.md §C (instruction-exact note, pseudocode) and the §7
  contract now state the single-field-pair law: Neighborhood object
  **+0x12a = arg+1, +0x12c = 0, written on BOTH the NGBH-reconstituted temp
  (persisted via ReconstituteSaveObject 0xb85c0 into the neighborhood file)
  and the live object**. verify.py fixture (d) asserts this corrected law.
  The skeptic also anchored the NGBH-file identity (second IFFResFile2::Open
  passes the nbhd object as path, same idiom as Neighborhood::Load 0xb5a0c;
  InitGame 0x889e4..0x88a30 builds GetString(7)+GetString(0xE) =
  'Neighborhood.iff') — folded into §C.

## C3 (MEDIUM-HIGH) — DoSave gate framing inverted

* Original decode: "DoSave returns 0 unless app+0xbf (save-enabled byte) != 0
  AND GetHouseNumber() != 0 — you cannot reset the tutorial unless a save is
  possible."
* Skeptic: gate failure (not dirty OR no house) returns **1 = PROCEED**;
  +0xbf is the save-DIRTY byte; the style-3 prompt maps {1 → 0 abort,
  4 → SaveGame (fail 0 / success clears +0xbf and 1), other → 1}.
* Re-verification: full tail disassembled (`dosave-tail.txt`,
  0x2593d0..0x2594ac). Gate failure lands on 0x259468 (`li r31, 0`) →
  0x259470 `beq 0x2594a4` → **`li r3, 1` at 0x2594a4**. Result-1 path:
  `li r3, 0` at 0x259438. Result-4 test: `subfic r0, r3, 4; cntlzw; srwi
  r31, r0, 5` at 0x259440..0x25944c; `*( *(TOC−0x7110) )` byte opt-out at
  0x259474..0x259480; `SaveGame` at 0x259488; success clears the dirty byte
  (`stb r0, 0xbf(r30)` with r0=0 at 0x25949c..0x2594a0). All exactly the
  skeptic's law. ResetTutorial's own branch (`(DoSave & 0xff) == 0 → return`)
  was always correct; the caller-visible law is "abort iff DoSave == 0".
* Repair: decode.md §B rewritten (pseudocode comment + full DoSave law
  bullet); §7 contract updated; new pins 0x259398, 0x259438, 0x259440,
  0x259468, 0x259470, 0x259494, 0x25949c, 0x2594a0, 0x2594a4.

## C4 (MEDIUM) — CancelTutorial physical key resolved: ESC

* Skeptic: the TSOnKeyDown switch compares the key code against 0x1b and
  branches to the CancelTutorial case at 0x217ac8.
* Re-verification: 0x217610 = 0x2C1E001B (`cmpwi r30, 0x1b`), 0x217614 =
  0x418204B4 (`beq 0x217ac8`) — confirmed (the skeptic cited the compare at
  0x217608; the byte-exact site is 0x217610, substance unchanged).
* Repair: decode.md §A states ESC (0x1B); the UNRESOLVED item is removed;
  pins 0x217610/0x217614 added.

## C5 (LOW-MEDIUM) — price float source

* Original decode: the price multiplier was read "at HouseInfo+0x40".
* Skeptic: `lwz r7, -0x7274(r2)` at 0xb0af4 REASSIGNS r7 before
  `lfs f2, 0(r7)` at 0xb0b14; the float lives at `*(TOC−0x7274)` = BSS
  0x852b0 (runtime global, writer unknown). HouseInfo+0x40 is the SIMI
  reconstitute position out-param (r7 = r30 = arg10 at 0xb0a80), not a price
  input.
* Re-verification: slot TOC−0x7274 → 0x852b0 confirmed from the packed data;
  instruction order confirmed from the disassembly.
* Repair: decode.md §G table rows and the UNRESOLVED item corrected; pin
  0xb0af4 added.

## C6 (LOW) — GetHouseFileInfo size

* Original decode said "~1100" bytes. r141 lists 288 (0xb0a42−2 = 0xb0a40,
  ends 0xb0b60); every cited pin falls inside. Cast table corrected. (The
  size was in the symbol index all along — an avoidable error.)

## C7 (INFO) — style-3 enumeration context

Quit's style-3 mapping {4 = save+proceed, 2 = proceed-without-saving,
3 = cancel} is recorded next to the style-1 "3 = proceed" invariant in
decode.md's UNRESOLVED note; no decode change required.

## verify.py changes

* Pin comments fixed (no pin itself was wrong — the two HIGH errors lived in
  prose, exactly as the skeptic's audit predicted).
* 17 new skeptic pins added (ESC compare, DoSave tail law, cheat cntlzw +
  on/off parse, price float source, temp-Neighborhood ctor): 116 → 133.
* Fixture (d): the dead `tutorial_completed()` tautology deleted; the fixture
  now asserts the corrected one-field-pair law (`+0x12a = arg+1, +0x12c = 0`
  on both copies; arg-0-only g26 clears) and records the cheat polarity.

## Post-repair state

verify.py PASS: `133 instruction words, 13 data pins and 7 fixture groups
verified` (run from a foreign cwd; writes only
`verified-tut-lifecycle.json`). `ruff check .` clean. No file outside this
directory was modified.
