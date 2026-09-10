# R245 — response to independent skeptic review (verdict: FIX FIRST, then SAFE TO IMPLEMENT)

Dispositions were decided against the binary, not against the review text.
All six corrections were re-verified instruction-by-instruction and applied
in full; no claim was rejected. Everything lives in this directory only.
Re-verification: `verify.py` pins 118 instruction words (14 skeptic-repair
sites + 5 ev→caption `addi` words added) and 11 data pins, runs 7 fixture
groups; `ruff check` clean; runs from any cwd.

## C1 — ev 9/11 caption swap (ACCEPTED, HIGH, material)

The skeptic is right. The secondary-jump-table handlers are
9 → 0x156720 (`addi r4, r31, 0x1d` = "rel") and 11 → 0x1566e8
(`addi r4, r31, 0x19` = "job"); the original draft transposed them when
summarizing. decode.md §2 step 7 now states the true law
(2="motives", 9="rel", 11="job", 13="skill", 15="per.ity"), the decision
table row carries it, and the §6 script-side table was reordered. verify.py
now pins the five `addi r4, r31, off` words (0x1566CC/0x156704/0x15673C/
0x156774/0x1567AC) and adds the `page_captions` fixture that re-derives
ev→caption through the pinned jump table + string table, so this mapping can
never silently regress. (The pinned `expected_jt2` handler addresses were
already correct; only the prose was swapped.)

## C2 — +0x20c accessors understated (ACCEPTED, HIGH, material)

Confirmed against the binary: `__ct__12cTSWinMgrW95Fv` (0x51fe80)
initializes the field (`stw r4, 0x20c(r31)` at 0x51ff68);
`DoModalWin__12cTSWinMgrW95FP6cTSWinP6cTSWin` (0x51ca60) hides a
flag-visible highlighter when a modal opens (GetFlag(1) at 0x51ca88..0x51cab0,
HideWindow slot 0xa0 at 0x51cac4) and SHOWS it again after (slot 0x9c at
0x51cda4, both re-reading +0x20c at 0x51cab8/0x51cd9c);
`CleanUpWindowReferences__12cTSWinMgrW95FP6cTSWin` (0x51dd60) nulls the
field when the dying window matches (compare 0x51dde8/0x51ddec, store 0 at
0x51ddf4/0x51ddf8) WITHOUT Release; `Shutdown__12cTSWinMgrW95Fv` (0x51fac0)
Releases (slot 0x10 at 0x51fb64) and nulls (0x51fb74). The "only the two
HiliteForTutorial impls read it" claim is removed; §5 now documents all
accessors, the modality suppress/restore law, and the no-Release cleanup
quirk (a port-relevant ownership asymmetry). Also adopted: the two winmgr
TOC holders (−0x735c poll/flash side vs −0x71fc Hilite/timer side) are now
stated explicitly in §5, and the base-TVector reference recount is 98 slots
(re-find of word 0x143c8 in the unpacked data), not 97; both mentions in
decode.md corrected.

## C3 — poll re-entrancy and the TreeSim +0x24 latch (ACCEPTED, HIGH, material)

Confirmed: `OnIdle__12cTSFrameWorkFv` (0x4b6d00) dispatches
`cSimsApp::Simulate` through vtable slot byte 0xe0 (`lwz r12, 0xe0(r12)` at
0x4b6d24); the viewer +0x110/+0x111 gates at 0x217160/0x21716c guard only
the following `cSimulator::Simulate` (0x217188), so the 0x21715c poll runs
before them; `TryDialog__8cXObjectFP9StackElemP10XPrimParam` (0xf1770) can
enter `DoSimsModalDialog`'s nested message loop, making a nested
poll/RunTree on the same owner reachable. The named-tree worker inside
`RunOneTickTree__7TreeSimFP8BehaviorssPs` (0x153e60; head 0x153ec0) refuses
while `TreeSim+0x24 != 0` (`lha r0, 0x24(r3)` at 0x153eec → return 0),
stores the finishing tree id there (`sth r4, 0x24(r25)` at 0x15402c, loaded
from the finished stack elem), and `Reset__7TreeSimFP8Behaviors` (0x154320)
clears it (`sth r0, 0x24(r29)` at 0x154370). decode.md §1 gained a
Re-entrancy paragraph with all seven addresses; a port must model the
single-slot named-tree latch.

## C4 — FindRelButton contract (ACCEPTED, MEDIUM, material)

Confirmed: the `__dynamic_cast` success path exits through the 0x156d38
epilogue with the cast result as the return value (`li r3, 0` at 0x156d34 is
the not-found path), and the `int` argument is dead — it is only forwarded
into the recursion at 0x156c50, never compared. With the RTTI slots named
(TOC−0x6fd0 = cWinRelationship, −0x6fd4 = cTSWin, −0x6fd8 = cTSWinBtn),
FindRelButton returns the FIRST window downcasting to cWinRelationship; in
`Tut_FlashRelButton` the neighbor lookup only gates existence (a failed
FindNeighborByID returns before any window search). §3 rewritten
accordingly, and the "exact RTTI class names UNRESOLVED" item is removed
from UNRESOLVED (the same naming fixes the FindButton bullets: casts target
cTSWinBtn). The dead-argument fact is port-relevant: a C# port must not
"helpfully" filter rel windows by person id.

## C5 — TryElement dispatch mask (ACCEPTED, MEDIUM, material)

Confirmed: `rlwinm r0, r0, 0, 0x11, 0xf` (MB=17 > ME=15) masks
0x0000.7FFF — it clears bit 0x8000 only, so the index is
`(s16)opcode & 0x7fff`, not `& 0xfffe` as the draft printed (a bit-numbering
direction error). `cmplwi r0, 0x33` bounds the 52-entry table; odd opcodes
have their own entries, e.g. 0x23 → 0xf0c6c =
`TryUserEvent__8cXObjectFP9StackElemP10XPrimParam`. decode.md §6 fixed;
the `primitive_0x22` fixture now records the index law and the 0x23 route.

## C6 — verify.py fixture bugs (ACCEPTED, MEDIUM)

* Fixture (e) `center_x`: the original encoded `srawi(d − 1, 1)` for
  negative d, but the binary computes `srwi r0, d, 31` (LOGICAL shift →
  sign = 1 for negative, 0 otherwise), `add`, then `srawi 1` — i.e.
  trunc((d + 1)/2) for negative d, not trunc((d − 1)/2). For
  (anchor 200, targetW 50, hlW 80) the binary gives 200 + srawi(−29, 1) =
  186; the old fixture asserted 185. Rewritten to the exact three-
  instruction sequence with a regression assert (`srawi(−29,1) = −14` while
  python's `-29 >> 1 = −15`), and decode.md §5 step 3 states the corrected
  law.
* Fixture (b) `phase_track`: the flip guard is `bge skip` off
  `cmpwi r0, 0` — `dir < 0` strictly, not `dir <= 0`. Unreachable state in
  practice (ShowWindow sets dir=1), but the fixture now matches the
  instructions exactly.

No changes were made outside `tools/iff-dump/r245-tut-highlight/`.
`python3 tools/iff-dump/r245-tut-highlight/verify.py` → `118 instruction
words, 11 data pins and 7 fixture groups verified` (also passes from /tmp
and /); `ruff check` clean.
