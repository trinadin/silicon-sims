# R245 skeptic re-verification — corrections to decode.md

Independent adversarial re-derivation from the binary (SHA256 re-checked:
33c76da2…c06a5f). All addresses are file offsets. Everything not listed below
was independently CONFIRMED (instruction bodies, all bl-site counts, both jump
tables word-by-word, the string table, all 11 TOC data pins, the float table
P = {0.0, 0.5, 1000.0, 1.0, 6.0} at file 0x5a5270, period 6.0 at 0x5a41f0,
the highlighter vtable columns, the cWinLotBtn vtable word 0x10168 at data
0x5c908, and the 167 ms interval = trunc(1000/6 + 0.5)).

## C1 (HIGH — behavior): event codes 9 and 11 are swapped in decode.md §2 step 7

decode.md says "ev 2 → "motives", 9 → "job", 11 → "rel"". The raw second jump
table (data 0x4ab78) says:

* ev 9 → handler 0x156720 → appends st+0x1d = **"rel"**
* ev 11 → handler 0x1566e8 → appends st+0x19 = **"job"**

(ev 2="motives", 13="skill", 15="per.ity" are correct.) verify.py is
internally consistent (it pins the table and the strings but never their
combination), so it passes while the prose is wrong. Correct law:
**2="motives", 9="rel", 11="job", 13="skill", 15="per.ity".**

## C2 (HIGH — behavior): cTSWinMgrW95+0x20c is NOT read only by the two HiliteForTutorial impls

decode.md §5: "Whole-code scan for +0x20c accesses finds no other
readers/writers" — falsified. Full-code displacement scan (all D-form ops)
finds, besides HiliteForTutorial base (0x5034e8) and the cWinLotBtn override
(0x2d2b0c) and SetTutorialHighlighter (0x51b694 read, 0x51b6b0 store):

* `cTSWinMgrW95::__ct__` 0x51ff68: `stw r4, 0x20c(r31)` — field is initialized
  by the ctor (this also resolves decode.md's "who initializes it" question).
* `cTSWinMgrW95::DoModalWin` (0x51ca60) 0x51ca88/0x51cab8/0x51cd9c: at modal
  entry, if `hl && hl->GetFlag(1)` → remember it and `hl->HideWindow()`; after
  the modal loop exits → `hl->ShowWindow()`. **The highlighter auto-hides for
  the duration of every modal dialog and is restored after.**
* `cTSWinMgrW95::CleanUpWindowReferences(win)` (0x51dd60) 0x51dde8/0x51ddf8:
  if `+0x20c == win` → `+0x20c = 0` (raw null-out, NO Release) — same idiom as
  its other tracked fields (+0x30, +0x194, +0x1ac, +0x214). A destroyed
  highlighter leaves a clean slot.
* `cTSWinMgrW95::Shutdown` (0x51fac0) 0x51fb54/0x51fb74: if `+0x20c` →
  Release (vtable slot byte 0x10) then `+0x20c = 0`.

Port consequence: mirror the full lifecycle (hide/restore around modal loops,
clear on window destroy, release at winmgr shutdown) or highlights will bleed
through dialogs and the slot can dangle.

## C3 (HIGH — port safety): poll re-entrancy is real; Simulate does not guard it

* `cDDDSimsView::Simulate` (0x216e90) is bl-called only from
  `cSimsApp::Simulate` (0x252b30) at 0x252e14; `cSimsApp::Simulate` sits at
  **vtable slot byte 0xe0** (TVector data 0xf4a8; primary vptr = TOC−0x6bd0 →
  data 0x530e8), and `cTSFrameWork::OnIdle` (0x4b6d00) dispatches slot 0xe0 on
  `*(this+0x10)` at 0x4b6d1c–28 — the app Simulate runs from the framework
  idle path.
* Tree primitives can pump a nested event loop: `cXObject::TryDialog`
  (0xf1770) sub-ops 0xc/0xf call `cSimsApp::DoSimsModalDialog` (0x253910),
  which wraps `cTSWinMgrW95::DoModalWin` (0x51ca60) — a full nested message
  loop (GetNextMessage 0x4b4e20, MacYieldIfTime 0x329d0 → LThread::Yield,
  per-iteration virtual dispatch on winmgr+0x60 slot 0x78 at 0x51cc70).
* The ONLY re-entrancy defenses gate the VM, not the poll: view+0x110 latch
  set around `cSimulator::Simulate` (0x21717c–0x217190) and SetBlockSimulator's
  view+0x111 (`SetBlockSimulator__12cDDDSimsViewFb` 0x212970, set true/false
  around the modal loop at 0x2539bc/0x2539e8). `bl Tut_CheckForEvents` at
  0x21715c runs BEFORE those checks — a nested Simulate runs a nested poll.
* The named-tree worker (0x153ec0, reached from RunTree 0xefd30) adds an
  object-level latch decode.md does not mention: it refuses to run while
  `owner+0x24 != 0` (0x153eec) and stores the finishing tree id into
  `owner+0x24` on completion (0x15402c; cleared by `TreeSim::Reset` 0x154370,
  whose callers were not chased). This can also make the poll's RunTree return
  0 for exe-side reasons; it does NOT block re-entrant (still-in-progress)
  tree runs.

∴ YES — a lesson tree that executes a dialog/UI primitive (the
"notifyOutOfIdle"-adjacent machinery; no symbol of that name exists in the
exe) can re-enter `cDDDSimsView::Simulate` → nested `Tut_CheckForEvents` →
nested RunTree on the same owner. Port must add an explicit in-flight guard
around the poll and keep 'query wait event'/'got wait event' free of
modal/pumping primitives.

## C4 (MEDIUM — behavior): FindRelButton does NOT match by person id

decode.md §3: "matching the relationship-panel row button whose handle/person
id is id" — falsified. In `FindRelButton` (0x156a90) the id argument (r29) is
only ever forwarded into the recursive call (0x156c50); it is never compared.
The walk returns the FIRST window that downcasts via `__dynamic_cast`
(0x591230) to the RTTI pair (TOC−0x6fd0, TOC−0x6fd4). Resolved type_info names
(data 0x4ab70/0x460b8/0x4ab38; this binary's dispatch convention is
r5 = target type, r6 = static source type):

* TOC−0x6fd0 → **cWinRelationship** (the relationship PANEL window)
* TOC−0x6fd8 → **cTSWinBtn** (used by both FindButton variants)
* TOC−0x6fd4 → **cTSWin** (the nodes' static type)

Correct law: `Tut_FlashRelButton(id, on)` = if `FindNeighborByID(id)` exists
(the id gates existence only; GetGUID/GetHandle results are dead), flash the
first **cWinRelationship** window in the main-window tree, else no-op. Do not
implement row-button matching by person id — the original does not do it.

## C5 (MEDIUM — behavior): primitive 34 opcode mask is `& 0x7fff`, not `& 0xfffe`

TryElement 0xf0438–0xf045c: `lha` (sign-extend), `rlwinm r0,r0,0,0x11,0xf`
(clears bit 16 of the 32-bit word = 0x8000 of the 16-bit value), `extsh`,
`cmplwi r0, 0x33; bgt default`. So index = (s16)opcode & 0x7fff; odd opcodes
are NOT folded down (table[0x23] → 0xf0c6c, a different handler; folding with
& 0xfffe would misroute it). decode.md §6's "& 0xfffe" is wrong; table base
TOC−0x5998, sub-op byte0 mapping (0=image-id, 1=person-panel, 2=rel; ≥3 or
negative → no call, still returns 1), InterpValue(byte1, s16@+2, 0) → extsh,
bool = byte4 & 1 — all confirmed.

## C6 (LOW/MED — verify.py fixture bug): fixture (e) hilite_center is off by one for odd negative width deltas

The binary's centering idiom at 0x503538–0x50354c is
`srwi r0, d, 31; add r0, d, r0; srawi r0, r0, 1` = **trunc(d/2)**
(round toward zero, sign-bit added then floor-shifted). verify.py encodes
`srawi(d + (-1 if d < 0 else 0), 1)` = trunc((d−1)/2) for d<0, which diverges
whenever (targetW − hlW) is odd and negative (d=−3: binary −1, fixture −2).
The asserted cases use even d, so it passes. Portable C# law: plain integer
`d / 2` (C# truncates toward zero) is exact.

## C7 (LOW — precision notes)

* Two TOC holder slots for the winmgr singleton: −0x735c (BSS 0x9743c) is used
  by Tut_CheckForEvents case 8 and all three Flash helpers; −0x71fc (BSS
  0x97734) by HiliteForTutorial base/override, hl Show/Hide/OnTimer/TSPaint,
  hl Init. decode.md lists only −0x71fc. (Runtime values presumably identical.)
* CheckForEvents case 8 tests `lwz +0xd8` (full word); `cTSWinBtn::SetState`
  (0x50b350) stores a word (`stw r4, 0xd8`) — state is an int, not a byte.
* The change-detection latch TOC−0x25a0 is SHARED by cases 6 and 7; a case-7
  first-sight cache poisons a subsequent case-6 comparison (and vice versa)
  unless the reaction path re-arms it. Not stated in decode.md.
* HideWindow (hl) unsubscribes the timer only when `GetFlag(1) && +0xe0`
  (decode.md omits the visible-flag gate).
* TSOnTimerMsg up-flip requires `dir < 0` strictly (0x538db0 `bge` skips when
  dir == 0); decode.md/fixture say `dir <= 0`. Divergence only in an
  unreachable dir==0 state (ShowWindow sets dir=1 before arming).
* The base HiliteForTutorial TVector (data 0x143c8) is referenced from **98**
  vtable slots, not 97 (conclusion unchanged: only cWinLotBtn overrides — its
  TVector 0x10168 appears exactly once).
* hl Shutdown deletes the buffer but, as decode.md says, does not unsubscribe
  the timer (HideWindow does) — confirmed.

## verify.py audit

Re-ran: PASS (99 instruction pins, 11 data pins, 6 fixture groups). 14
instruction pins independently spot-checked against raw bytes — all match; all
11 data pins re-derived from an independent PEF unpack — all match; jump
tables, strings, float table, vtable columns, data 0x5c908 = 0x10168 — all
independently reproduced. No pin addresses overlap with r244's verify.py
(72 pins), and no claim contradicts r244's instruction evidence. Fixture
findings: (a)/(c)/(d) sound; (b) minor dir==0 wording; (e) genuine off-by-one
(see C6); (f) is a restatement, not evidence. Structural gap that let C1
through: nothing cross-checks jump-table entries against the strings they
select.
