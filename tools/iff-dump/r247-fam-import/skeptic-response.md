# R247-fam-import — response to the skeptic pass (provenance of this revision)

The skeptic's independent re-derivation (`skeptic-corrections.md`) accepted
the decode with one surgical fix (C1) and wording/cosmetic corrections
(C2–C8). Every correction was applied; before each edit the claim was
re-checked against the round's own disassembly excerpts and, where the
evidence was new, against fresh capdis2 runs of the executable (SHA256
`33c76da2…c06a5f`, unchanged). verify.py was rewritten per C8 and re-run:
**PASS — 230 instruction pins, 23 data pins, 16 record pins, 7 binary-bound
fixtures**, from three different working directories; `ruff check` and
`eclint check` clean. Nothing outside this directory was written.

## Fix-by-fix confirmation

* **C1 (HIGH, RelMatrix) — ACCEPTED AND APPLIED.** Re-verified in
  `nbhd-importfamily.txt` before editing: the inner loop's source calls take
  `addi r4, r21, 0` (0x38950000 at 0xae700/0xae740; r21 = `lha r21, 4(r30)`,
  r30 walking the EXPi id array at r1+0xa4) while the destination calls take
  `addi r4, r23, 0` (0x38970000 at 0xae714/0xae724/0xae754; r23 =
  `lha r23, 0(r29)`, r29 walking the newIds array at r1+0x70); both array
  pointers are re-initialised to their bases at 0xae6e8/0xae6ec each outer
  iteration, so the pair loop covers ALL member ids per member; the
  `n == 0` leg falls through 0xae70c (`bne +0x14`) to
  `RemoveArray` (0xae718). decode.md §3 law + §3 answer bullet, §6
  pseudocode, and §7 step 16 rewritten; verify.py gained a dedicated fixture
  that decodes the argument-register wiring of all five call sites from the
  pinned words (`relmatrix_law`) plus 35 new pins on the site instructions.
* **C2 (SIMI global 26) — ACCEPTED AND APPLIED.** §4 now carries a
  dedicated paragraph: the "FAM SIMI carries g26 ≠ 0" latch is
  decode-derived (inherited from r247-tut-lifecycle, same loader, same
  sim+0x44 offset as LoadHouse) and explicitly **not byte-provable from the
  staged file** (raw body+0x44 is 0; the ReconBuilder mapping remains
  r247-UNRESOLVED). verify.py asserts nothing about a g26 value — only the
  read site 0xadeb0 is pinned — and the round's UNRESOLVED section records
  the C2 limitation.
* **C3 (guard-order leak) — ACCEPTED AND APPLIED.** §5 gained the full
  paragraph (guards at 0xae8a4/0xae8f4..0xae99c fire after the character
  deletion 0xae0c4, eviction mutation 0xae088..0xae0c0 and member creation
  0xae114..0xae608; no model rollback; `Save__12NeighborhoodFl` unreached on
  any failure; the only rollback is the file-level `.tmp` rename), §7 states
  "replicate" as the port policy at step 19, and verify.py fixture
  `guard_order_leak` derives the ordering from the pinned words: both guard
  failures converge at 0xae9c4 (`li r17, −1` in both legs), return via
  0xaea14 → 0xaee18, and a scan of the function proves 0xaecbc is the ONLY
  `bl Save__12NeighborhoodFl` — which the failure jump spans past.
* **C4 (timer gate) — ACCEPTED AND APPLIED.** Fresh disassembly of
  TSOnTimerMsg confirmed `lbz r0, 0x16c(r29)` (0x472ce8), the
  `DoInternetUpdate` branch (0x472cf8) exiting at 0x472d0c → 0x472e98 —
  past both the CheckForNewImports call (0x472e74) and the 1000 ms re-arm
  (0x472e8c) — plus the +0x191 first-tick latch. decode.md §1.1/§7 step 0b
  and verify.py fixture `scan_and_timer_gate` (with 8 new pins) now encode
  "re-arms only while win+0x16c == 0".
* **C5 (one-arg scan call) — ACCEPTED AND APPLIED.** Confirmed twice over:
  between the append `bl` (0x46f4d0) and the scan `bl` (0x46f4d8) only
  `addi r3, r1, 0x40` executes (no r4 write), and 0x4aa010's body consumes
  only r3 (`addi r31, r3, 0` at 0x4aa018) with the
  `FindFirstFileA`/INVALID_HANDLE/`FindClose` shape (0x4aa048/0x4aa050-54/
  0x4aa05c). decode.md §1.2 rewritten (two-arg
  `cTSDirectory::…(path, pattern)` called out as an r141 mis-attribution);
  the scan LAW (any `*.FAM` in `UserData/Import/`) is unchanged.
* **C6 (ctor seed; id-assign order) — ACCEPTED AND APPLIED.** +0x188 is
  seeded from app->+0x64 in the ctor (0x231d9c/0x231da0, pinned) and
  overwritten by the filler; §2.1's table row now says so. In
  Neighborhood::ImportFamily `*outNew` is written at 0xaded8 and the id
  assign lands at 0xadf28 BEFORE the null check at 0xadf2c; §3's law and §6
  pseudocode now show that order, and all five words are pinned.
* **C7 (evicted-export guard) — ACCEPTED AND APPLIED.** §2.3's wrapper law
  now states the literal guard `fam = GetFamily(famB_id); fam &&
  fam->+0x10c != 0` (0x231f04..0x231f18; re-checked in
  `importfamily-free.txt`).
* **C8 (fixtures) — ACCEPTED AND APPLIED.** All seven tautological
  fixtures were deleted. The rewritten verify.py binds every fixture to
  pinned binary words or the staged files:
  - `addnewchar_template_selector` — a mini PPC interpreter (cmpwi/bc/b/
    lis/addi) re-executes the pinned branch tree at 0xb3780..0xb37f4 and
    derives the type→GUID table from the executed lis/addi pairs (GUID
    constants are not fixture inputs);
  - `uchr_type_dispatch` — decodes GetString index 13, the strtable offsets
    joined to the 'dog'/'cat' literals, the gate rlwinm mask, the gate
    branch targets, and the three type immediates;
  - `relmatrix_law` — the C1 register wiring (see above);
  - `guard_order_leak` — the C3 ordering (see above);
  - `scan_and_timer_gate` — C4/C5 call shapes (see above);
  - `refresh_fields` — cross-binds the caller's stack displacements
    (0x270/0x2b0 − base 0x160) to the ctor/filler stores;
  - `iff_files` — re-parses Tutorial.FAM and proves byte identity of EVERY
    common chunk with pristine House07.iff (the skeptic's stronger
    all-chunks result, now asserted; 'rsmp' pinned as the sole differing
    chunk) plus the EXPi/FAMI little-endian field decodes.
  The malformed `TEMPLATE_GUIDS` dict is gone.
* **C2-adjacent downgrade**: §4's byte-identity claim now states the full
  skeptic result (all common chunks identical; rsmp the only difference)
  and verify.py asserts exactly that.

## Two additional corrections found while applying C8 (F1/F2)

Both were caught by the new binary-bound fixtures — which is precisely the
falsifiability C8 asked for — and are fixed in decode.md, verify.py, and
this revision's outputs:

* **F1 — pets-gate bit value.** `rlwinm. r0, r0, 0, 0x1a, 0x1a`
  (0x540006B5) extracts MSB-index 26, whose bit VALUE is 2^(31−26) =
  **0x20**, not 0x40. The gate is `(s16)app+0x66 & 0x20`. All decode.md
  occurrences and the fixture (`gate_mask == 0x20`, derived from the word
  via `rlwinm_value_mask`) now carry 0x20. (The skeptic's summary repeated
  the original 0x40; the instruction math above is the authority.)
* **F2 — dog template GUID.** The selector builds the dog GUID as
  `lis r3, 0x4a71` + `addi r22, r3, −0x206e` (0x3AC3DF92): 0x4A710000 −
  0x206E = **0x4A70DF92**, not 0x4A71DF92. decode.md §3 and the fixture
  (`derived_guids[1] == 0x4A70DF92`, derived by execution) are corrected.
  The interpreter initially flagged this by failing against the
  mis-transcribed constant — the intended mode of failure.

## Verdict

All C1–C8 applied; F1/F2 self-caught and applied. decode.md §7 is the
skeptic-verified 25-step canonical operation order with the C1/C3/C5
corrections folded in. `verify.py` PASS (230 pins / 23 data pins / 16
record pins / 7 binary-bound fixtures) from any cwd; `ruff` and `eclint`
clean. The decode is SAFE TO IMPLEMENT against the corrected §7 contract.
