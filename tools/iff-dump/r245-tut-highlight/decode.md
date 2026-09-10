# R245 — tutorial lesson-highlight / UI event-poll machinery

Resolves the tutorial UI-poll system left open by r239/r241 (their "remaining
gaps": `Tut_CheckForEvents`, control lookup, `HiliteForTutorial`, the TutHigh
highlighter). All addresses are executable FILE OFFSETS; r141 symbol-index
addresses are +2 (index addr − 2 = instruction-stream start). Container facts
as in r244: code section file 0x8e90..0x5c22e8, stored code words are
section-relative (+0x8e90 = file), TOC base = data + 0x8000, data section
unpacked 0x7bf80 with zero-fill BSS to 0x989a4 (singleton pointers live at
0x8xxxx–0x9xxxx, below the BSS end). Nothing outside this directory was
written; no game asset copied.

Container note for two float constants: the TOC slots −0x4348 and −0x5398
store values (0x59c3e0 / 0x59b360) that are code-section-relative, not
data-relative — +0x8e90 lands in the read-only float pool at the end of the
code section (file 0x5a5270 / 0x5a41f0). verify.py pins both readings.

Function boundaries were confirmed against the executable's own function
records (`00092041`; empirically `start = marker+8 − size − 12`, validated on
5763 records against the symbol index).

## 0. Cast of characters

| Function | Start (file) | Size |
| --- | --- | --- |
| `Tut_FlashRelButton__Fsb` | 0x156370 | 152 |
| `Tut_FlashPersonPanelButton__Fsb` | 0x156440 | 216 |
| `Tut_CheckForEvents__Fv` | 0x156550 | 1124 |
| `Tut_FlashButtonByImageID__Fib` | 0x1569e0 | 116 |
| `FindRelButton__FP6cTSWini` | 0x156a90 | ~700 |
| `FindButton__FP6cTSWini` | 0x157540 | ~220 |
| `FindButton__FP6cTSWinP9cTSBuffer` | 0x157670 | ~190 |
| `HiliteForTutorial__10cWinLotBtnFb` | 0x2d2ae0 | 144 |
| `HiliteForTutorial__6cTSWinFb` | 0x5034c0 | 332 |
| `SetTutorialHighlighter__12cTSWinMgrW95FP6cTSWin` | 0x51b660 | 108 |
| `cTSWinTutorialHighlight::HideWindow` | 0x538b90 | 112 |
| `cTSWinTutorialHighlight::ShowWindow` | 0x538c40 | 192 |
| `cTSWinTutorialHighlight::TSOnTimerMsg` | 0x538d40 | 284 |
| `cTSWinTutorialHighlight::SetBuffer` | 0x538ea0 | 304 |
| `cTSWinTutorialHighlight::TSPaint` | 0x539020 | 316 |
| `cTSWinTutorialHighlight::Shutdown` | 0x5391a0 | 112 |
| `cTSWinTutorialHighlight::Init` | 0x539250 | 136 |
| `cTSWinTutorialHighlight::__dt__` | 0x539310 | 112 |
| `cTSWinTutorialHighlight::__ct__` | 0x5393c0 | 92 |
| `GetTutorialObject__12ObjectModuleFv` | 0xe2d80 | 8 |
| `cTSWinMgrW95::SubscribeTimerMsg` | 0x51c500 | — |
| `cTSWinMgrW95::UnsubscribeTimerMsg` | 0x51c160 | — |
| `cTSWinMgrW95::GetMainWindow` | 0x51dc00 | — |
| `cTSWinMgrW95::PumpMouseMoveMsg` | 0x51bf10 | — |
| `CPState::GetCPMode` | 0x20c9f0 | 8 (`return +0x220`) |
| `cXObject::TryElement` | 0xf0410 | 2220 |
| `cDDDSimsView::Init` | 0x218b70 | 2278+ |
| `cDDDSimsView::Simulate` | 0x216e90 | — |

Globals (TOC slot → content, pinned in verify.py):

* `TOC−0x778c` → BSS 0x8510c: app holder; `+0x1c` = cSimulator, `+0x24` =
  Neighborhood.
* `TOC−0x7790` → BSS 0x92660: holder whose `+0xa4` = the CPState object (same
  chain r244 used for `GetMode`).
* `TOC−0x720c` → BSS 0x934e8: **CPState** singleton pointer (readers include
  `SetSelectedPerson__7CPStateFi`, `UpdateViewFromCPState__10cWinPeopleFP7CPState`,
  `ObjectDialog::SetParams`).
* `TOC−0x71fc` → holder of the **cTSWinMgrW95** singleton (used with
  GetMainWindow / GetCursorManager / UnsubscribeTimerMsg / etc.).
* `TOC−0x6fc8` → BSS 0x9777c: **last-activated cTSWinBtn\*** — written by
  `cTSWinBtn::TSOnMouseDownL` (0x50bf40 stores `this`) and cleared by the
  button dtor (0x50d68c–0x50d69c).
* `TOC−0x6fcc` → BSS 0x85c08: holder of a second singleton whose first word is
  change-polled (identity UNRESOLVED, not needed for the port).
* `TOC−0x7350` → data 0x85c48: the world singleton (r244); `+0x84` =
  altitude-level word.
* `TOC−0x57b4/−0x57b8/−0x57bc` → data 0x4abf8/0x4abb8/0x4ab78: tree-name
  string table and the two 16-entry jump tables (decoded in §2).
* `TOC−0x25a0` → data 0x5a60: a TOC-resident s32 cache used as the
  change-detection latch (statically −1).
* `TOC−0x4348` → (code-rel 0x59c3e0 → file 0x5a5270) float table
  **P = {0.0, 0.5, 1000.0, 1.0, 6.0}**.
* `TOC−0x5398` → (code-rel 0x59b360 → file 0x5a41f0) float **6.0**.
* `TOC−0x7208` → BSS 0x92968: the magenta color key (r241), passed to the
  loaded TutHigh buffer.
* `TOC−0x60b0` → data 0x76c30: the `cTSWinTutorialHighlight` vptr.

## 1. The poll driver (task A)

**`Tut_CheckForEvents` (0x156550) has exactly ONE `bl` call site in the whole
executable: 0x21715c, inside `cDDDSimsView::Simulate` (0x216e90).** Every path
through Simulate converges on that address (`viewsim-full.txt`: the only
branches targeting the region land ON 0x21715c, none skip past it). The
cadence is therefore once per simulation tick of the 3D house view; the
guards live inside Tut_CheckForEvents itself (no tutorial owner → return;
'query wait event' tree falsy → return). Immediately after the poll,
Simulate may call `cSimulator::Simulate` (0x217188) — the VM tick.

**Re-entrancy (skeptic repair, see skeptic-response.md C3).** The tick chain
is `cTSFrameWork::OnIdle` (0x4b6d00) → vtable slot byte 0xe0 dispatch at
0x4b6d1c–0x4b6d28 = **cSimsApp::Simulate**. The viewer +0x110/+0x111 byte
gates AFTER the poll (0x217160/0x21716c) guard ONLY the cSimulator::Simulate
call — the poll at 0x21715c runs BEFORE them. Since `TryDialog`
(0xf1770) can enter `DoSimsModalDialog` (a nested message loop), a nested
`Tut_CheckForEvents` poll and re-entrant `RunTree` on the SAME owner while
one is already running is reachable. The exe-side latch participating in the
protocol is TreeSim's: the named-tree worker inside
`RunOneTickTree__7TreeSimFP8BehaviorssPs` (0x153e60; worker head 0x153ec0)
REFUSES to run while `TreeSim+0x24 != 0` (`lha r0, 0x24(r3)` at 0x153eec,
return 0 otherwise) and stores the FINISHING tree id there (`sth r4,
0x24(r25)` at 0x15402c, from the finished stack elem); `TreeSim::Reset`
(0x154320) clears it (`sth r0, 0x24(r29)` at 0x154370). A port must model
this single-slot "one named tree at a time" latch.

The three `Tut_Flash*` helpers are NOT called from Tut_CheckForEvents. Their
only call sites are at 0xf04a8 / 0xf04c8 / 0xf04e8, all inside
**`cXObject::TryElement(StackElem*, BehaviorNode*)`** (0xf0410) — they are
behavior-tree PRIMITIVE implementations invoked by the tutorial lesson script
(see §6). `HiliteForTutorial__6cTSWinFb` has exactly one `bl` site:
0x2d2afc — the super-call from the `cWinLotBtn` override (both are vtable
slot byte 0x168; see §5). `SetTutorialHighlighter` has exactly one `bl` site:
0x219444 in `cDDDSimsView::Init`, which also calls
`cTSWinTutorialHighlight::SetBuffer` at 0x21941c (construction region in
`viewinit-region.txt`). All other `cTSWinTutorialHighlight` methods have zero
direct `bl` sites — they are reached only virtually.

## 2. Tut_CheckForEvents decision tree (task B)

`checkforevents.txt`. Per poll:

1. `owner = ObjectModule::GetTutorialObject()` (0xe2d80 = `return +0xac`).
2. `btn = *(TOC−0x6fc8)` (last-activated button). If nonnull and its
   `+0xd0` (image buffer) is nonnull: **`cSimulator::SetGlobal(0xc,
   (s16)btn->+0xd0->+0x30)`** — the simulator GLOBAL 12 always mirrors the
   image-id of the last-activated button (0 when none). This write happens
   before every other check.
3. If `owner == null` → return.
4. `result = owner->RunTree(owner->+0x11c->+0xc /*Behaviors*/, 0,
   "query wait event", 0, 0)` (RunTree 0xefd30; name from string table
   r31+0x00). `(u8)result == 0` → cache `*(TOC−0x25a0) = −1` and return.
5. `ev = (s16)owner->+0x3a`; `param = (s16)owner->+0x3c`; `found = false`.
6. `ev <= 0xf` → jump table `*(TOC−0x57b8)[ev]` (pinned):

| ev | handler | condition that sets `found` |
| --- | --- | --- |
| 0, 3, 10, 12 | 0x156974 | never (idle) |
| 1 | 0x15661c | CPState object exists AND `CPState::GetCPMode() == 0` (GetCPMode 0x20c9f0 = `return +0x220`) |
| 2, 9, 11, 13, 15 | 0x156644 | person panel open AND its current page caption equals a name (see below): 2="motives", 9="rel", 11="job", 13="skill", 15="per.ity" |
| 4 | 0x156820 | last-activated button exists AND `btn->+0xd0->+0x30 == param` (its image id) |
| 5 | 0x15684c | `Neighborhood::FindNeighborByID(*(app)->+0x24, param)` → GUID → `PersonFinder::GetHandle` → `GetPictureBuffer(...)` succeeds AND `AsBuffer(handle) == *(TOC−0x6fc8)->+0xd0` — the person-panel portrait of neighbor `param` is the shown buffer |
| 6 | 0x1568c4 | `world->+0x84` differs from the cached `*(TOC−0x25a0)`; on first sight (cache == −1) it just caches the value (latch). On change: `found`, cache reset to −1 by the reaction path |
| 7 | 0x1568f4 | same latch law against `*(*(TOC−0x6fcc))` first word |
| 8 | 0x156920 | `cTSWinMgrW95::GetMainWindow()` → `FindButton(mainwin, param)` finds a button whose buffer id is `param` AND `button->+0xd8 != 0` (its state; `cTSWinBtn::SetState` writes +0xd8 — MouseEnter/Exit/Down/Up, so "mouse is over/activating it") |
| 14 | 0x1567e0 | inverse of case 2: the person-panel page button is ABSENT (`CPState->+0x150` null, its `+0x104` null, or `+0xd8 == 0`) |

7. Case 2 detail: chain `CPState->+0x150` (person panel window), byte
   `+0xd8` nonzero, `r24 = panel->+0x104` (current page button), then a
   `StringBuffer` built on the stack is compared with the caption StringBuffer
   at `page->+0x18` (`compare__12StringBufferCFRC12StringBuffer`, 0x141e90).
   Which name is appended is picked by the SECOND jump table
   `*(TOC−0x57bc)[ev]` (pinned): ev 2 → "motives" (r31+0x11), 9 → "rel"
   (r31+0x1d), 11 → "job" (r31+0x19), 13 → "skill" (r31+0x21),
   15 → "per.ity" (r31+0x27); other values compare against the empty buffer
   (never equal for real captions). (An initial draft of this decode swapped
   ev 9 and ev 11; the raw handlers are 9 → 0x156720 = "rel" and
   11 → 0x1566e8 = "job".)
8. If `found`: `owner->RunTree(..., "got wait event", 0, 0)` (0x156994) and
   `*(TOC−0x25a0) = −1` (cache reset — the latch fires exactly once per
   change). If not found: nothing (re-polled next tick).

**Throttle/idempotence law:** every poll re-evaluates from scratch; the ONLY
memory is the `*(TOC−0x25a0)` latch used by cases 6/7 (edge-triggered) —
cases 1–5/8/14 are level-triggered, but the reaction ('got wait event') runs
on EVERY poll while the level holds. The 'query wait event' tree returning 0
is what stops the cycle.

PORTABLE PSEUDOCODE:

```
every view tick:
    g12 = (lastClickedBtn and lastClickedBtn.buffer) ? lastClickedBtn.buffer.id : 0
    Simulator.global[12] = g12
    owner = ObjectModule.tutorialObject
    if owner is None: return
    if not owner.runTree("query wait event"): cached = -1; return
    ev, param = owner.local[+0x3a], owner.local[+0x3c]
    found = CASES[ev](param)          # table above
    if found:
        owner.runTree("got wait event")
        cached = -1
```

## 3. The Flash helpers (task C)

All three end the same way: locate a `cTSWin` target and dispatch its vtable
slot byte **0x168 = `HiliteForTutorial(bool)`** with the bool argument
(`lwz r12, 0x168(r12)` at 0x1563e4 / 0x1564e8 / 0x156a30). On failure they
just return (no highlight change). All obtain the main window via the winmgr
virtual at slot byte 0x20 + `GetMainWindow` (0x51dc00).

* **`Tut_FlashRelButton(sbyte id, bool on)`** (0x156370): `id` is a NEIGHBOR
  (person) id, but only as an EXISTENCE GATE:
  `Neighborhood::FindNeighborByID(sim->+0x24, extsh(id))` → `GetGUID` →
  `PersonFinder::GetHandle(guid)`; if the neighbor does not exist the helper
  returns without flashing. The window search itself,
  `FindRelButton(mainwin, handle)` (0x156a90), NEVER compares the id — the
  argument is dead there (only forwarded into the recursion at 0x156c50). It
  walks the window tree (child list at win+0x3c, sentinel +0x40;
  `GetChildList` 0x157290) and returns the FIRST child that downcasts via
  `__dynamic_cast` (0x591230) to **cWinRelationship** (RTTI: TOC−0x6fd0 =
  cWinRelationship, TOC−0x6fd4 = cTSWin, TOC−0x6fd8 = cTSWinBtn): cast
  success exits through the 0x156d38 epilogue with the cast result as the
  return value; 0x156d34 is the not-found `li r3,0`. So the rel flash simply
  highlights the relationship window (first one found), gated on the
  neighbor existing. (Skeptic repair C4 — the original draft claimed a
  per-row person-id match.)
* **`Tut_FlashPersonPanelButton(sbyte id, bool on)`** (0x156440): `id` is a
  neighbor id: FindNeighborByID → GUID → `ImageHandle(null)` ctor →
  `PersonFinder::GetPictureBuffer(handle, ImageHandle&)`; if it succeeds,
  `AsBuffer(ImageHandle)` and `FindButton(mainwin, cTSBuffer*)` (0x157670)
  — a recursive child search (`__dynamic_cast` to **cTSWinBtn**, RTTI pair
  TOC−0x6fd8/−0x6fd4) matching the button whose `+0xd0` (image buffer)
  pointer IS the person's portrait buffer. The person-panel button
  is thus identified by portrait identity, not by index.
* **`Tut_FlashButtonByImageID(int id, bool on)`** (0x1569e0): `id` is an
  IMAGE/BMP resource id: `FindButton(mainwin, id)` (0x157540) — recursive
  child walk with `__dynamic_cast` to **cTSWinBtn** (RTTI pair
  TOC−0x6fd8/−0x6fd4); a child matches when `child->+0xd0 != null &&
  child->+0xd0->+0x30 == id` (findbutton-tail.txt; the matching cast result
  returns via the 0x157618 epilogue, 0x157614 is the `li r3,0` path). So
  image IDs map to controls by comparing each candidate button's buffer-id
  field across the ENTIRE main-window tree (depth-first, parent before
  children, recursion into non-matching children).

## 4. cTSWinTutorialHighlight lifecycle (task D)

**Construction/attachment** (`viewinit-region.txt`, cDDDSimsView::Init):
1. `new cTSWinTutorialHighlight` (ctor 0x5393c0: cTSWin ctor; vptr from
   TOC−0x60b0 → data 0x76c30; +0xcc=+0xd0=+0xd8=+0xdc=0; +0xd4 = P[0x10] =
   6.0; +0xe0=0).
2. `r31->GetParentWin()` (slot byte 0x20) then `->ChildAdd(hl)` (slot 0x28) —
   the highlighter is attached as a child of the view's parent window.
3. `SetOverlapsScrollArea(hl, true)` (0x503820).
4. `hl->HideWindow()` (slot 0xa0) — starts hidden.
5. `LoadBuffer(nsSMResFac, 0x1374, &buf, 4, eColorType4)` — resource
   **0x1374 = 4980 = kTutorialHighlightBMP = cpanel\TutHigh.bmp 150x50**
   (canon-pinned in earlier rounds; usage pinned here, asset NOT copied).
6. if loaded: `buf->vt+0x70(*(TOC−0x7208))` — apply the magenta color key.
7. `hl->SetBuffer(buf, 3, *(TOC−0x5398) = 6.0f)`.
8. `hl->SetFlag(0x10000, true)` (slot 0x98).
9. `cTSWinMgrW95::SetTutorialHighlighter(winmgr, hl)`.

**SetTutorialHighlighter** (0x51b660): `if (new) new->AddRef()` (slot 0xc);
`if (mgr->+0x20c) old->Release()` (slot 0x10); `mgr->+0x20c = new`. Storage =
**cTSWinMgrW95 field +0x20c**, refcounted, single slot (old released, not
hidden here — the base HiliteForTutorial hides it).

**Init** (0x539250): `if (get_init_flag()) { cTSWin::Init(); return its ret; }
else { if (!cTSWin::Init()) return 0; SetCursor(this,
GetStandardCursor(GetCursorManager(winmgr), 0)); return 1; }`.

**Shutdown** (0x5391a0): if initialized: `delete buffer` (buffer vtable slot
byte 8 with r4=1) and `+0xcc = 0`; then `cTSWin::Shutdown` (0x509330).
Does NOT unsubscribe the timer (HideWindow does).

**dtor** (0x539310): restore base vptr, virtual slot 0x18 (= Shutdown),
`cTSWin::__dt__` (0x509530), free if flag > 0.

**ShowWindow/HideWindow laws.** Both first call `GetFlag(1)` (slot 0x94,
arg r4=1 = the visible flag).
* ShowWindow (0x538c40): if already flag-visible → just `cTSWin::ShowWindow`
  (0x5029d0). Else reset `+0xdc = 0` (phase), `+0xd8 = 1` (direction), and —
  if the timer is not yet subscribed (`+0xe0 == 0`) and `period (+0xd4) >
  P[0] (= 0.0)` — subscribe:
  `interval = fctiwz(1000.0 * (1.0/period) + 0.5)` = round(1000/period) ms
  (`fdivs` P[3]/period, `fmadds` ×P[2] +P[1], `fctiwz` truncate),
  `SubscribeTimerMsg(winmgr, this, interval, 0)` (0x51c500), `+0xe0 = 1`.
  With the shipped period 6.0 s: **167 ms per phase step**.
* HideWindow (0x538b90): if `+0xe0` (subscribed):
  `UnsubscribeTimerMsg(winmgr, this)` (0x51c160), `+0xe0 = 0`; then
  `cTSWin::HideWindow` (0x5028a0).

**TSOnTimerMsg — the FLASH animation law** (0x538d40):
1. If subscribed: unsubscribe (`+0xe0 = 0`) — each tick re-arms explicitly.
2. `dir = (s32)+0xd8`. If `dir > 0 and phase(+0xdc) == N−1` → `dir = −1`
   (N = the int arg of SetBuffer at +0xd0). Else if `dir <= 0 and
   phase == 0` → `dir = +1`. (Literal decode; N=1 degenerates — shipped N=3.)
3. `phase += dir` (`+0xdc`).
4. `InvalidateSelf()` (slot 0x170 → 0x5a29e0 glue).
5. If now unsubscribed and `period > 0`: re-subscribe with the same
   round(1000/period) ms math; `+0xe0 = 1`.
The animation NEVER auto-hides: the phase ping-pongs 0→N−1→0 forever until
HideWindow/SetTutorialHighlighter(release) stops it.

**SetBuffer(cTSBuffer* buf, int n, float period)** (0x538ea0):
`if (+0xcc and +0xcc != buf) delete +0xcc` (buffer vtable byte 8, r4=1).
Stores `+0xcc = buf`, `+0xd0 = n`, `+0xd4 = period`. If buf: resize the
window via `SetArea(long,long,long,long)` (slot 0x68) with the cTSWin area
fields 0x74/0x78/0x7c/0x80 = {left, top, right, bottom} and the cTSBuffer
rect {left=0x14, top=0x18, right=0x1c, bottom=0x20}:
* always second call `SetArea(l, t, r, t + (buf.bottom − buf.top))`;
* first call sets the width: `n != 0` → `SetArea(l, t, l + (R−L)/n, b)`
  (`divw`, trunc); `n == 0` → `l + (R−L)`.
Net effect for the 150x50 art with n=3: window becomes 50 wide × 50 tall
(three 50x50 animation frames in the art strip).

**TSPaint(bool)** (0x539020): returns 1 immediately if no buffer. Builds two
cTSRects on the stack; `step = right/N − left` (`divw` right/N then `subf`):
* src rect (r1+0x40) = {left + phase*step, top, right/N + phase*step,
  bottom} — the phase-th horizontal slice of the art (`mullw` phase*step).
* dst rect (r1+0x50) = src shifted by the window position
  (+0x1c = x, +0x20 = y): {src.x + x, top + y, src.right + x, bottom + y}.
Then, on the drawing surface at `this->+0x5c`: `vt+0x28(0x10)` (begin, must
return nonzero), `vt+0xa0(buffer, &src, &dst, 0)` (draw), `vt+0x2c(0x10)`
(end). Finally `GetMainWindow()->vt+0x178(&this->+0xc)`
(InvalAgainstScreenRect(cTSRect&) — the window's own rect at +0xc feeds the
screen invalidation).
So the TutHigh strip animates by BLITTING slice[phase] (marching frames,
ping-ponged by the timer); there is no alpha blending — the magenta key
applied at load handles transparency.

**Who creates it:** only `cDDDSimsView::Init` (the 3D house view) constructs
and registers it — one instance, child of the view's parent, pulled to front
on each show (§5). Z-order: `PullToFront` (slot 0x4c) every HiliteForTutorial
show.

## 5. The window-manager hook (task E)

* Storage: **cTSWinMgrW95+0x20c** (SetTutorialHighlighter 0x51b660). The
  winmgr ctor (`__ct__12cTSWinMgrW95Fv`, 0x51fe80) initializes it to null
  (`stw r4, 0x20c(r31)` at 0x51ff68).
* ALL accessors of the field (skeptic repair C2 — an earlier draft claimed
  the two HiliteForTutorial impls were the only readers):
  * `HiliteForTutorial__6cTSWinFb` (0x5034e8) and
    `HiliteForTutorial__10cWinLotBtnFb` (0x2d2b0c) — the flash/read side.
  * `DoModalWin__12cTSWinMgrW95FP6cTSWinP6cTSWin` (0x51ca60): when a modal
    dialog opens, if the current highlighter is flag-visible (GetFlag(1),
    0x51ca88..0x51cab0) it is HIDDEN (slot 0xa0 at 0x51cac4, re-reading
    +0x20c at 0x51cab8); when the modal ends it is SHOWN again (slot 0x9c
    at 0x51cda4, re-reading +0x20c at 0x51cd9c). So modal dialogs suppress
    and restore the highlight without clearing the field.
  * `CleanUpWindowReferences__12cTSWinMgrW95FP6cTSWin` (0x51dd60): when the
    highlighted window is destroyed (`lwz r0, 0x20c(r30); cmplw r0, r31` at
    0x51dde8/0x51ddec) the field is nulled (`stw r0, 0x20c(r30)` with
    r0 = 0 at 0x51ddf4/0x51ddf8) **WITHOUT Release** — an ownership quirk a
    port must reproduce (the field borrow is not a +1 ref).
  * `Shutdown__12cTSWinMgrW95Fv` (0x51fac0): Releases the highlighter
    (slot 0x10 at 0x51fb64) and nulls the field (0x51fb74).
* The winmgr singleton is reached through TWO TOC holders: **TOC−0x735c** on
  the poll/flash side (CheckForEvents case 8 at 0x156920, all three
  Tut_Flash* helpers at 0x1563ac/0x1564a4/0x1569e8) and **TOC−0x71fc** on
  the Hilite/timer side (HiliteForTutorial 0x5034c8, highlight Show/Timer/
  TSPaint/Init, and cDDDSimsView::Init r25). Both slots hold the same
  singleton pointer; a port can use one accessor.
* Consultation is event-driven, not polled in a draw path: the highlighter
  is shown/moved by HiliteForTutorial (invoked by the Tut_Flash* primitives'
  slot-0x168 dispatch), hidden/shown around modality by DoModalWin, and
  nulled on target destruction by CleanUpWindowReferences.
* Base-impl TVector references: the cTSWin::HiliteForTutorial TVector
  (data 0x143c8) appears in **98** vtable slots across derived classes
  (pure inheritance; recount confirms 98, not the 97 an earlier draft
  printed).
* **HiliteForTutorial__6cTSWinFb(bool on)** (0x5034c0), the base:
  1. `hl = winmgr->+0x20c`; if null → the code would still take the hide
     branch at 0x5035dc (a null deref) — in practice unreachable because Init
     always installs the highlighter before windows exist. Honest note: the
     original has no null guard.
  2. If NOT `this->GetFlag(1)` (target invisible): `hl->HideWindow()`
     (slot 0xa0); return.
  3. Move the highlighter onto the target: `TSWinMoveTo(hl, x, y)`
     (0x260ba0) with
     `x = target->+0xc + srawi(d + (d >> 31 & 1), 1)` where `d = targetW −
     hlW` and `srwi(d, 31)` is a LOGICAL shift giving 1 for negative d —
     i.e. trunc((d + 1)/2) for negative d, NOT trunc((d − 1)/2)
     (skeptic repair C6), `y = hl->+0x10 − hlH − 3`,
     where `targetW = target->+0x7c − target->+0x74`, `hlW = hl->+0x7c −
     hl->+0x74`, `hlH = hl->+0x80 − hl->+0x78` (area fields 0x74..0x80;
     +0xc/+0x10 = anchor x/y).
  4. `hl->ShowWindow()` (slot 0x9c) then `hl->PullToFront()` (slot 0x4c).
  5. `cursor = GetStandardCursor(GetCursorManager(winmgr), 0)`; if
     `hl->+0x68 != cursor`: store it and — if `hl->GetFlag(1)` —
     `PumpMouseMoveMsg(winmgr)` (0x51bf10) so the cursor state re-evaluates.
* **cWinLotBtn override** (0x2d2ae0, vtable word at data 0x5c908 = TVector
  0x10168; exactly ONE vtable references the override): super-call first,
  then if `on`: `hl->SetArea(hl.l, hl.t + 0x32, hl.r, hl.b + 0x32)` — the
  highlight is shifted DOWN 50 px (0x32) for lot buttons — and
  `hl->SetCursor(GetStandardCursor(mgr, 4))` (cursor 4, not 0).
* **Other overrides: none found.** The base implementation's TVector
  (0x143c8) is referenced from 98 vtable slots across derived classes (pure
  inheritance; see §5 header notes); a symbol-index scan for
  `*HiliteForTutorial*` yields exactly the two implementations above.
* Related flash plumbing: `cTSWinBtn::SetState(int)` writes the button state
  byte `+0xd8` used by CheckForEvents case 8; button `+0xd0` = its image
  buffer with the image id at `+0x30`.

## 6. The request interface (task F) — read-side contract for the IFF agent

Writers of the state Tut_CheckForEvents consumes:

1. **The tutorial owner object** (`ObjectModule::GetTutorialObject` =
   `ObjectModule+0xac`; acquired/released by primitive 10, r239). The poll
   runs two NAMED trees on it: **"query wait event"** (must return nonzero
   while waiting) and **"got wait event"** (fired when the condition holds).
   Both run via `RunTree(owner, owner->+0x11c->+0xc /*Behaviors*/, name, 0, 0)`
   — the IFF side must provide these named trees on the tutorial object
   (Tutorial.iff main 4096's driver should set them up per lesson step).
2. **Owner object fields (s16) +0x3a (event code 0..15) and +0x3c (param).**
   These are cXObject VM variable slots read every poll. Executable-side
   writers visible in the binary:
   * the awaited-DIALOG completion path (`dialog-writers.txt`, 0xf1800ff):
     when an async dialog the object waits on completes, its RESULT is
     stored to `+0x3a` and the wait slot (+0xee/+0xf0/+0xf2/+0xf6) cleared
     (for wait subtypes 5/8/7/0xb; sites 0xf1830/0xf1870/0xf18b0/...);
   * the interpreter local-store op (0xf0c04: `sth r0, 0x3a(r3)` with the
     value from `CalcShortDistance`-style ops — generic object-variable
     writes), so script-side variable writes land here too.
   The IFF agent should treat +0x3a/+0x3c as "the lesson's wait descriptor":
   the script (or the dialog plumbing) sets ev/param, the poll detects the
   condition, then runs 'got wait event'.
3. **Simulator global 12 (0xc)** is WRITTEN by the executable every poll
   (image-id of the last clicked button, or 0) — scripts can read it but must
   not rely on writing it.
4. **CPState**: case 1 uses `GetCPMode() == 0` (CPState+0x220); cases 2/14
   read `CPState->+0x150` (person panel) `->+0xd8` (visible byte) and
   `->+0x104` (page button with caption StringBuffer at +0x18). The person
   panel captions compared are the literal strings "motives", "job", "rel",
   "skill", "per.ity".
5. **World +0x84** (case 6) and `*(*(TOC−0x6fcc))` (case 7) are
   edge-triggered change detectors with a −1-initialized latch; the IFF side
   needs no writers — any value change (e.g. entering/leaving a situation
   reflected at world+0x84) fires the event once.

Event-code → condition table for the script side (from §2): 1 = call-panel
mode 0; 2/9/11/13/15 = person panel showing motives/rel/job/skill/
personality page respectively; 4 = button with image id `param` was pressed
(last-activated); 5 = person `param`'s portrait is displayed; 6/7 = change
detectors; 8 = mouse is over the button with image id `param`; 14 = person
panel page gone; 0/3/10/12 = never.

The script FLASHES controls with **primitive 0x22 (34)** in object behaviors
(`tryelement-region.txt`): the dispatch index is `(s16)opcode & 0x7fff`
(rlwinm mask 0x11/0xf clears bit 0x8000; the initial `& 0xfffe` reading was
wrong — skeptic repair C5), bounds-checked `cmplwi 0x33`, then entry of the
table at `*(TOC−0x5998)`; index 0x22 → handler 0xf0460. Odd opcodes have
their own entries (e.g. 0x23 → 0xf0c6c = `TryUserEvent__8cXObjectF...`).
For 0x22, operand byte 0 = sub-op (0 = flash button by IMAGE ID, 1 = flash
PERSON PANEL button, 2 = flash RELATIONSHIP button); the id argument is the
s16 at operand+2 run through `InterpValue__8cXObjectFssb` (r5 = value,
r4 = byte at operand+1 = addressing mode, r6 = 0); the bool = bit 0 of
operand byte 4 (flash on/off). Returns 1.

## 7. What is safe to base a C# implementation on

Instruction-pinned and fixture-backed (verify.py passes; 99 instruction words
+ 11 data pins + 6 fixture groups):

* Poll driver: single call site in cDDDSimsView::Simulate, unconditional per
  tick, self-guarded; global 12 mirror law; 'query wait event'/'got wait
  event' named-tree protocol; the full 16-entry decision table incl. the
  literal page-caption strings.
* The three Flash helpers: neighbor-id vs portrait-buffer vs image-id
  lookups, all ending in the vtable-0x168 `HiliteForTutorial(bool)` dispatch.
* Highlighter lifecycle: creation/attachment/color-key/SetBuffer(buf, 3, 6.0)
  in cDDDSimsView::Init; cTSWinMgrW95+0x20c refcounted single slot;
  HideWindow-on-invisible-target; centering arithmetic (+0x32 lot-btn shift,
  cursor override + PumpMouseMoveMsg); PullToFront.
* Flash animation: interval = round(1000/period) ms with period = 6.0 s
  (167 ms), phase ping-pong over N = 3, never auto-hides.
* Paint law: 150x50 art strip (resource 4980, magenta key), N=3 → window
  50x50, slice[phase] blit at window offset, no alpha.
* Function-record start law used this round: `start = marker+8 − size − 12`
  (validated on 5763 records).

UNRESOLVED (with reason):
* Identity of the singleton at `*(*(TOC−0x6fcc))` (case 7 change-detector) —
  only its change-latch semantics are pinned.
* cTSWin field semantics at +0xc/+0x10 (anchor x/y) vs +0x74..0x80 (area):
  the arithmetic is pinned; the abstraction boundary between them is inferred
  from SetArea/TSWinMoveTo usage.
* `HiliteForTutorial` with a null highlighter takes the hide branch without a
  null check (0x5035dc) — the original would crash; unreachable in practice
  because Init installs the highlighter. A port should guard it.
* The runtime initializer (if any) that may rewrite TOC−0x4348/−0x5398 was
  not found (no `stw`/`addi` writers by displacement scan); the pinned
  code-relative float readings are self-consistent (P table = {0, 0.5, 1000,
  1, 6} and period = 6.0 = the ctor default), so the values are treated as
  static.

## Files

* `verify.py` — pins + fixtures; PASS (99 words, 11 data pins, 6 fixture
  groups), runs from any cwd, ruff-clean; writes
  `verified-tut-highlight.json` only.
* `checkforevents.txt`, `flashrelbutton.txt`, `flashpersonpanel.txt`,
  `flashbyimageid.txt`, `findrelbutton.txt`, `findbutton-tail.txt`,
  `case1-helper.txt`, `gettutorialobject.txt`, `tryelement-region.txt`,
  `tryelement-tail.txt`, `dialog-writers.txt`, `settutorialhighlighter.txt`,
  `hl-*.txt` (ctor/dtor/init/shutdown/show/hide/timer/setbuffer/tspaint),
  `hilite-tswin.txt`, `hilite-lotbtn.txt`, `viewinit-prologue.txt`,
  `viewinit-region.txt`, `viewsim-full.txt`, `viewsim-region.txt`,
  `tree-worker.txt`, `trydialog-head.txt` — capdis2.py excerpts (r145) at
  the addresses cited above.
* `skeptic-response.md` — decode → skeptic review → repair provenance.
