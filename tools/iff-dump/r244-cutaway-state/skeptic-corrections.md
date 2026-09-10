# R244 skeptic verification (independent re-derivation)

Adversarial re-check of `decode.md` against the executable (SHA256
`33c76da2…c06a5f`). All claims were re-derived from raw disassembly and
byte-level scans performed independently of the r244 excerpts. `verify.py`
passes (72 pins, 13 chain links, 6 fixtures) and 20 of its pins were
re-checked as raw words. **Verdict: SAFE TO IMPLEMENT.** No headline law was
falsified; the corrections below are refinements.

## Confirmed by independent re-derivation

* insert `cCutawaySet::insert` (0x1aa030) has exactly **2** direct `bl` call
  sites in the whole code section (full opcode-level scan): 0x1d0628/0x1d063c,
  both in `HouseViewer::MouseTrack` 0x1d0310 (function-record attribution).
* `clear__11cCutawaySetFv` (0x1a9e60) has exactly **16** call sites, at
  exactly the addresses decode.md lists. Function-record attribution matches
  all 16: GenerateSnapshot ×3 (0x1c0020 copy-ctor 0x1aa630 then clear,
  0x1c01ac, 0x1c0814), SetGraphicsContext 0x1c0a7c, DrawPictureInPicture
  0x1c1068/0x1c161c, MakeThumbnail 0x1c1dd8/0x1c2094/0x1c24c8, DoCommand
  0x1d66c0 (cmd 0x105: BuildMaxAltsTable + memset(+0x204,0,0x200) + clear +
  dirty + GetRotation/SetRotation), 0x1d68b8+0x1d6904 (cmd 0xf6, sets
  +0x58/+0x5c/+0x60/+0x64 = 0x10/8/8/4<<zoom, clears twice, full recompute),
  0x1d6b78 (cmd 0xf0: `stb r0=1, 0x200(r28)` then clear), SetDynamicCutaway
  0x1d6fbc, SetLevel 0x1d7158, ScrollToTile 0x1d7cf0. cmd 0xe4 (0x1d6b80)
  confirmed NOT clearing (only DoDynamicCutaway(viewer+0x1f8)).
* MouseTrack phase gate (0x1d03d0 `clrlwi. r0,r31,0x18` / bne), dynamic
  gate (+0x49), viewport box [+0x28,+0x30]×[+0x2c,+0x34], mode-4 bookkeeping
  (+0x4bc/+0x4c0/+0x4c4), off-lot ±1-inset test against `*(TOC-0x7350)`
  +0x70..0x7c → ResetDynamicCutaway (0x1d0650), probe walk step
  `(viewer+0x60)/2` signed (srwi/add/srawi at 0x1d04c4..0x1d04d4), loop bound
  `viewer+0x124+4*(3-*(zoomCfg+8))` halved, out-of-world probes skipped
  (r27 kept), both inserts gated only by `cmplwi 0xfffb`.
* `XViewer::MouseDown` (0x157a10) calls `MouseTrack(..., phase=1)` at
  0x157bc8/0x157bcc → mouse-down never inserts. The phase=0 drivers were
  found by exhaustive scan (direct bl, branch islands, vtables):
  **`EdgeDetectScroller::OnMouseMove` 0x21c678** builds a stack EventRecord
  and calls MouseTrack with `phase = (int16)0x37380(1) < 0` (button-down
  tracking vs move), suppressed when scroller+0x93 is set; and
  **GenerateSnapshot 0x1c03d8** (`li r5, 0`) — its synthetic re-track can
  insert into the freshly cleared history. MouseTrack is in NO vtable.
* Second insert is **not** conditional on the rooms differing — only
  `< 0xfffb`; the duplicate no-op makes the equal case benign.
* Set constructor capacity: HouseViewer ctor `li r4, 3; bl 0x1aa770`
  (0x1d9640/0x1d9644), heap set pointer stored at viewer+0x1fc.
* Strategy chain: GetScrollingStrategy reads view+0x16c, lazily calls
  CreateEdgeDetectStrategy 0x21cb70 (`li r3, 0x94` alloc); EdgeDetectScroller
  ctor stores TOC-0x6c94 → **0x50910** primary vptr and zeroes +0x93 at
  0x21cb1c; vtable walk confirms slot +0x5c → TVector 0xf198 → 0x21a7e0
  (`lbz r3, 0x93(r3)`); base impl 0x1d02c0 = `li r3,0; blr`.
* Tool chain: IssueToolSelection 0x158138 stores its arg into TOC-0x6fb8
  (`stw r29, 0(r31)` at 0x158174); cTool dtor stores TOC-0x6f48 → **0x4d280**
  at this+0x28; full slot walks of 0x4d280 and cMoveTool's 0x4c550 confirm
  slot 18 (+0x48) = AdjustCutawayForTool base 0x191910 / override 0x174100
  and slot 19 (+0x4c) = ModifyCursorPos 0x1579d0 (`blr`-only, 4 bytes).
  Base impl behavior verified: +0x24 gate, [x0+1..x1-1]×[y0+1..y1-1] gate,
  GetWall, set bit(tile), wall&1 → clear tile+{+6,+7}, wall&2 → clear
  tile+{+0,+1}. cMoveTool impl: +0x24 gate, GetCurrentLevel, picked list
  (+0xf4/+0xf8>>4, floor +0x110; +0x77/+0x79 drag override via
  +0xfc/+0xfd/+0xfe), [1..size-2] gate, marks into the matrix arg.
* Cursor vicinity: buffer-bounds test vs GetBuffDims words 0/4;
  `mouse.y += (8 << GetScale()) << 2` — **ADD at 0x1cfb28 confirmed
  positive**; ModifyCursorPos called before PointToTile; viewer vptr = 0x4ea40
  (HouseViewer ctor stores TOC-0x6de4 → 0x4ea40 at +4; 0x4ea40+0xc → 0xeaa0 →
  PointToTile 0x1d3b00, +0x1c → GetScale 0x1d17a0); dir globals +0xc =
  (-1,-1,0), +0xf = (+1,+1,0) from __sinit__ 0x610d0; corner = tile + 4*dir1
  (operator* 0x19abf0 with li r4,4); both walk-in loops; CTileRect::end
  returns begin() unless a.x<b.x && a.y<b.y; iteration half-open
  [a.x,b.x)×[a.y,b.y), x innermost; per-tile world bounds check.
* **r26 gate confirmed**: r26 is written only at 0x1cf440 (init 0),
  0x1cf5a8 (history room IsOutside), 0x1cf75c (mode-2 person room IsOutside)
  before the gate at 0x1cfdc4 (`clrlwi. r3,r26,0x18; beq 0x1cffcc`); it is
  reused as a loop counter only later (0x1d01c4, after the gate). The
  surprising reading is correct: the cursor rectangle is marked only when at
  least one ORed room was outside. The person rectangle itself is NOT gated
  by r26.
* Outside-person rectangle: tile bytes +0xfc/+0xfd/+0xfe (extsb x,y), gate
  1<=x,y<=size-2 (size = (*TOC-0x7354)->+0x10), altitude remap
  `TOC-0x73e4 + ((*world->+0x84)<<13) + (x<<7) + (y<<1) - 0x2000`, byte[0]=new
  x, byte[1]=new y, probe k=4,3,2,1,0 (ctr=5) storing the candidate into the
  rect's corner each iteration and exiting at the first inside candidate, so
  exhaustion leaves corner=tile; CTileRect{tile, corner} then marks
  [x,x+k)×[y,y+k) per-tile bounds-checked; k=0 → degenerate → nothing.
* CPState modes: SetMode(2) → EnterLiveMode__5HouseFv (0x210800) + hook
  string; all five hook strings re-read from unpacked data at strtab 0x50258:
  +0x17 "Entering Build Mode", +0x2b "Entering Buy Mode", +0x3d "Entering
  Live Mode", +0x50 "Entering Camera Mode", +0x65 "Entering Options Mode".
  TSOnCommand passes 2/1/0/3/4; EnteringHouse sets 2; SetDynamicCutaway
  clears only when +0x49 actually changes (cmp/beq 0x1d6f8c-0x1d6f90).
* Person fields: UpdateCurrentRoom 0x109450 (`lha +0x84` vs `lhz +0xaac`,
  `sth +0xac... sth r29, 0xaac(r31)` at 0x1094b4, same 0xfffb constant,
  GetPeopleCount + SetOverheadLights, dispatch 0xf7 when +0x114 bit29 or
  tracked); Room::GetPeopleCount scans people's +0xaac (0x124a44);
  CenterHouseViewOnMe word-compares `lwz r27, 0x110(r26)` with GetLevel
  (0xc49e8-0xc49f0) and DoDynamicCutaway compares +0x110 with viewer+0x18
  (0x1cf610-0x1cf618).

## Corrections (refinements; none invalidate a headline law)

1. **Insert container mechanics** (decode.md §1.3 says "erase the FRONT
   element (0x1aa2e0…)"): 0x1aa2e0 is `std::vector<int>::pop_back` (record
   name; body decrements vector+4), and the subsequent insert (0x1aa150 →
   0x36320) inserts at **begin()** (0x1aa270 = begin, 0x1aa210 = iterator
   load, r4 = position). The vector is therefore stored newest-first:
   duplicates keep their old slot (no recency refresh), eviction drops the
   least-recently-inserted from the back, and a new id is pushed to the
   front. The observable membership law (FIFO eviction of the oldest,
   capacity 3, duplicate no-op) is exactly as decoded; only the in-vector
   order description was inverted. Order is unobservable through the set's
   use (OR + clear), so the port should model membership only.
2. **Bit addressing wording** (decode.md §3): with helper 0x591b10 the word
   at matrix offset **0 holds bits 32-63 and offset 4 holds bits 0-31**
   (`stw r0,0(r5)` stores the high half, `stw r3,4(r5)` the low half).
   decode.md's "word0 = bits 0-31, word1 = bits 32-63" has the halves
   swapped. Only matters if the port mirrors the raw 512-byte layout.
3. **verify.py chain gap**: `'cdddsimsview_vptr_slot': (0x1354, 0x50450)` is
   a true TOC-slot/word pair but 0x50450 is a cTSWin/cDDDSimsView interface
   table, not the vptr used by the viewer dispatches. The dispatched vptr is
   **0x4ea40**, stored by the HouseViewer ctor from TOC-0x6de4 (word at
   unpacked data 0x121c) — verified there. The PointToTile/GetScale slot
   words themselves (0x4ea40+0xc → 0x1d3b00, +0x1c → 0x1d17a0) are correct.
4. **Phase-0 driver named**: decode.md says "phase==0 is the tracking/move
   call" without naming the caller. It is EdgeDetectScroller::OnMouseMove
   (0x21c678, phase from button state, suppressed by the same +0x93 flag the
   +0x5c predicate reads) — which also shows IsWaitingForClick suppresses
   the insert driver, not just the cursor-marking section. Additionally
   GenerateSnapshot's synthetic MouseTrack(phase=0) at 0x1c03d8 can insert
   right after its clear.
5. **Cosmetic naming/tooling**: decode.md writes `DoCommand__11HouseViewerFs`
   — record is `…Fsl`; `DrawPictureInPicture…RC7FTilePtii` — record has
   `iii`. decode.md's function-record law "function start = size_addr -
   size - 8" should be **- 0xC** (marker+8 = size word; verified against
   `__ml__Q29CTileRect8iteratorFv` and GenerateSnapshot).
6. **Unproven presentational detail**: "typically 4x4 block below-left of
   the cursor" — the 4×4 size, half-open extent, and cursor-tile exclusion
   are proven; the screen-space "below-left" orientation is rotation- and
   camera-dependent interpretation, not a binary fact. Y += 32<<zoom moves
   the sample point down-screen (Mac v grows downward), toward the camera.

## verify.py audit

Runs clean. 20 pins re-checked as raw words (all match); all 13 data-chain
links reproduced independently, plus the missing viewer-vptr link (TOC-0x6de4
→ 0x4ea40, see correction 3). Fixtures are meaningful: direction constants
are re-derived from `li/stb` pins, both rectangles from the decoded loop
laws, eviction from the insert law; `mouse_y_adjust` is arithmetic-only and
could not catch a sign error on its own, but pin 0x1cfb28 (`add r0,r5,r4`)
does. 72 pins disjoint from r243's set; no tautological pin found (each pin
was hand-checked at its address in context).
