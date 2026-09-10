# R145 — INTEREST & GIFT (INVENTORY) SUB-PANEL LAW (decoded from PPC binary)

Round target: the complete composition law of the Interest tab (res 4505) and
Gift/Inventory tab (res 4507) sub-panels of the live-mode toolbar, continuing
r144 (`r144/toolbar-law.md` — read first). Binary: `game-data/The Sims/The Sims
Complete` (PPC PEF). All addresses are FILE offsets (== code-virtual;
data-stored code pointer P → file P+0x8E90; TOC base = 0x8000 in sec1
addressing — revalidated this round via the font-array slot 0x8000-0x7178 →
BSS 0x92900 and the BuildInterestWindow jump table).

DECODED = read directly from instruction immediates / static tables.
INTERPRETED = semantic reading; immediates cited but behavior inferred.

Coordinate conventions (r144): **people-local** = cWinPeople window pixels
(people = cpanel rect (220,0,W,150); y=50 is the toolbar band top);
**host-local** = sub-panel host window pixels. Screen = people + (220, SH-150).

BIG CORRECTION vs the r144 §1.7 wording: the interests/gifts do NOT live in
host people+0xfc. Host **people+0xfc is the PET-MOTIVES host** (see §6).
The Interest and Gift panels are two SEPARATE cTSWin hosts, **people+0x250**
and **people+0x328**, both direct children of cWinPeople, built lazily by
`cWinPeople::BuildInterestWindow` @0x287010 and `BuildGiftPanel` @0x286820
(called once from `cWinPeople::Init` at 0x28fea0/0x28feb8).

Symbols (r141 index, index addr = file addr + 2):

| function | file addr |
|---|---|
| cWinPeople::BuildGiftPanel | 0x286820 |
| cWinPeople::BuildInterestWindow | 0x287010 |
| cWinPeople::BuildGifts | 0x2887d0 |
| cWinGift_LessThan (comparator) | 0x2891b0 |
| cWinPeople::BuildInterests | 0x289210 |
| cWinPeople::SetPanel | 0x28a510 |
| cWinPeople::TSOnCommand | 0x28c090 |
| cWinPeople::InitInventorySorts | 0x28e2b0 |
| cWinInterest::{SetInterest,TSPaint,SetupClient,Shutdown,Init,ctor} | 0x405b10, 0x405e80, 0x406070, 0x406110, 0x4061a0, 0x4063a0 |
| cWinGift::{TSOnMouseDownL,TSPaint,SetupClient,Shutdown,Init,ctor} | 0x407e30, 0x407e90, 0x4081f0, 0x408270, 0x4082c0, 0x408460 |

---

## 1. The two hosts — DECODED (identical law for both)

`BuildInterestWindow` @0x287010 (interest) and `BuildGiftPanel` @0x286820
(gift) are line-for-line twins:

1. `new cTSWin` (0xcc bytes, ctor 0x509610) → people+**0x250** (interest) /
   people+**0x328** (gift); `Init` (vt+0x14); on failure the whole build aborts.
2. `SetArea#1` with rect from BSS (**IsDoubleByteUI** @0x20c670 selects):
   - SB: BSS 0x934c8 = **(0, 50, 280, 150)**; DB: BSS 0x934b8 = **(0, 0, 280, 150)**
     (values written by `__sinit_:WinPeople_cpp` @0x292c70: stores at
     0x292de0/0x292d5c/0x292dd4/0x292de4 = 0,50,280,150).
   - If `AnimDevice::GetBuffDims()==0x400` (1024-wide screen): right edge += 0xe0
     (224) → **(0,50,504,150)** (0x2870c0-0x2870dc).
3. `SetArea#2` re-anchor to BSS 0x93490 = **{300, 50}** (SB) / 0x93488 =
   **{300, 0}** (DB), preserving current w/h → final host rect
   **(300,50,580,150)** at 800-wide, **(300,50,804,150)** at 1024-wide
   people-local — exactly r144's "+224-right law" band. Host-local size:
   **504x100** (1024) / 280x100 (800).
4. `AddChild(people, host)` (vt+0x28 at 0x2871a0) — a DIRECT child of
   cWinPeople, not of +0xfc.

Title text (cTSWinText, 0x108 bytes, ctor 0x538a50):
- interest → people+**0x254**, gift → people+**0x32c**.
- `SetFont(font[6])` — font array BSS 0x92900+0x18 (0x2871d8-0x2871e8).
  (NOTE: r144 §1.7's four Init-built host titles use font[11]; these two
  panel-specific titles use font[6].) The resolved font has **LineHeight =
  13 px**.
- anchored at host-local **{5,0}** SB (BSS 0x934b0) / {10,5} DB (0x934a8)
  via the same SetArea-preserve-size re-anchor.
- text = `LoadUIStrings(id, "live", …)` (0x25f440; table-name string "live"
  at DATA 0x5818c, TOC slot 0x3074):
  - interest: id **0x8c = 140** → element [0] = **"Interests"**
  - gift: id **0x8d = 141** → element [0] = **"Inventory"**
  (both from `GameData/Live.iff` STR# 140 "Interest Strings" / STR# 141 "Gifts"
  — extracted and verified this round, EN lang code 1.)
- child of the host; people+**0x264** (interest) / +**0x33c** (gift) =
  anchor.y + titleTextHeight + 5 = the CONTENT TOP (title bottom + 5 px),
  hence **18 px SB / 23 px DB** with font[6]'s 13-px line height.

Page arrows (one pair per panel; res 100 kCatalogPrevPage / 101
kCatalogNextPage, 36x49 sheets, `SetImage(_, 4, 1)` → 9x49 cells, loaded via
`UIGraphics::GetBuffer` 0x3b6190):
- interest: left = people+**0x258**, right = people+**0x25c** (0x2873f4-0x287510).
- gift: left = people+**0x330**, right = people+**0x334** (0x286bd4-0x286cc0).
- Both get flag +0x163 = 1, +0xec = 1, then HideWindow (vt+0xa0), AddChild to
  their host. QuickTips = `LoadUIStrings(0x9a=154, <lang-table>, …)` =
  MiscStrings [0] "Previous Page" / [1] "Next Page" (0x287658-0x286f04).
- Vertical law (DECODED, both panels): top = contentTop + (hostH − contentTop
  − btnH)/2, bottom = top + 49 → **vertically centered in the content band**
  (0x287514-0x287570 / 0x286d24-0x286d80).
- Horizontal law (DECODED arithmetic):
  - interest left arrow: SetArea(**2**, top, 2+9, bottom) → host-local x 2..11.
  - interest right arrow: SetArea(**hostW−btnW−2**, top,
    **hostW−2**, bottom) (0x2875a4-0x2875f4). The former residual read the
    value in argument register r6 as the left edge; r4 is the left edge and r6
    is the right edge. Exact host-local x is therefore **493..502** at the
    504px host and **269..278** at the 280px host. It is wholly inside the host,
    leaves a 2px right gutter, and requires no clipping at either screen width.
  - gift left arrow: SetArea(**0x22 = 34**, top, 34+9, bottom) (0x286d7c).
  - gift right arrow: `r4 = hostW−btnW−2`; at **0x286dfc**, `add r6,r4,r8`
    makes `r6 = r4+btnW`, so SetArea receives
    (**hostW−btnW−2**, top, **hostW−2**, bottom). Exact host-local x is
    **493..502** at the 504px host and **269..278** at the 280px host. As for
    Interest, the arrow is wholly in-host with a 2px right gutter; the port's
    `Size.X - 11` left-edge law is exact.
- Content bounds (DECODED):
  - interest: people+**0x260** = leftArrowW + 4 (content left ≈ 13),
    people+**0x268** = hostW − 4 − rightArrowW, people+**0x26c** = hostH
    (0x2875fc-0x287654).
  - gift: people+**0x338** = leftArrowW + **0x24 (36)** (content left ≈ 45 —
    leaves room for the sort-button column, §4), people+**0x340** =
    hostW − 4 − rightArrowW (+0x344 = hostH) (0x286e0c-0x286e64). Only +0x338
    is used by the item layout.

---

## 2. THE INTEREST GRID — DECODED

Populated by `cWinPeople::BuildInterests` @0x289210 (called from Init
0x2910d8, from UpdateViewFromCPState 0x28b1cc, and from the page-arrow clicks
0x28cacc/0x28cb1c).

### 2.1 The 19 widgets
Built once in BuildInterestWindow loop 0x287794-0x287908: **19 x cWinInterest**
(0x19c bytes each; ctor @0x4063a0 takes (client = people+0x270, index i)),
stored in array **people+0x274[19]** and AddChild'd (hidden) to host +0x250.
A jump table at DATA 0x5806c (TOC −0x4f90) additionally aliases each widget
into named fields (all DECODED):

| widget i | topic (STR# 140[i+1]) | icon res 1600+i | alias fields (people+) |
|---|---|---|---|
| 0 | Travel | 1600 | 0x2c0 |
| 1 | Money | 1601 | 0x2c4 |
| 2 | Politics | 1602 | 0x2c8 |
| 3 | The 60's | 1603 | 0x2cc |
| 4 | Weather | 1604 | 0x2d0 **and 0x30c** |
| 5 | Sports | 1605 | 0x2d4 **and 0x310** |
| 6 | Music | 1606 | 0x2d8 **and 0x314** |
| 7 | Outdoors | 1607 | 0x2dc **and 0x318** |
| 8 | Toys | 1608 | 0x2fc |
| 9 | Aliens | 1609 | 0x300 |
| 10 | Pets | 1610 | 0x304 |
| 11 | School | 1611 | 0x308 |
| 12 | Exercise | 1612 | 0x2e0 |
| 13 | Food | 1613 | 0x2e4 |
| 14 | Parties | 1614 | 0x2e8 |
| 15 | Style | 1615 | 0x2ec |
| 16 | Hollywood | 1616 | 0x2f0 |
| 17 | Technology | 1617 | 0x2f4 |
| 18 | Romance | 1618 | 0x2f8 |

Because the alias fields are laid out contiguously, two ready-made display
sequences exist (this is the whole point of the duplicates):
- **human array = people+0x2c0[15]** = w0,w1,…,w7, w12,…,w18 →
  Travel, Money, Politics, The 60's, Weather, Sports, Music, Outdoors,
  Exercise, Food, Parties, Style, Hollywood, Technology, Romance (15 topics).
- **pet array = people+0x2fc[8]** = w8,w9,w10,w11, w4,w5,w6,w7 →
  Toys, Aliens, Pets, School, Weather, Sports, Music, Outdoors (8 topics).
The topic-count float table at FILE 0x5a45ec (ptr 0x59b75c) holds exactly
these totals: {…, 8.0 (pet), 15.0 (human), …} (0x289278-0x2892c4).

Art-name mismatch (INTERPRETED): the Res_CPanel.h enum names 1601
"InterestViolence" and 1616/1617 "Charisma"/"Health", but the shipped strings
(the authoritative display text) are Money / Hollywood / Technology. The
Superstar-era strings renamed three topics without renaming the art enum.

### 2.2 Cell geometry (cWinInterest::Init @0x4061a0)
- `SetImage(NULL, 0, 4, 1)` then **SetArea(l, t, l+0x7d, t+0x19)** → each
  widget is **125 x 25** (0x2872f8 in BuildInterests re-reads w/h from the
  rect; constants 0x7d/0x19 at 0x406230/0x40622c).
- Cell composition = icon 75 + gap 5 + bar 40 + margin 5 (see §2.4).

### 2.3 Grid + paging (BuildInterests)
- species gate: `lha r3, 0x600(person)`; pet iff 0 < value < 0x12 (18)
  (0x289250-0x28926c). Pets use the pet array and max index 8; humans the
  human array and max index 15 (0x289458-0x289488).
- rows = 3 SB / 4 DB (0x289244-0x28924c); **columns = people+0x31c**, set in
  cWinPeople::Init 0x28fdb8-0x28fde0: **3 columns at 1024-wide, 2 at
  800-wide** (1024 also sets webcam-cols people+0x180=10 and gift max
  people+0x368=10; 800 sets 5 and 6).
- per-page = rows × columns → 9 (1024 SB) / 12 (1024 DB) / 6 (800 SB) / 8 (800 DB).
- pages = people+0x324 = ceil(total / perPage) (ceil helper 0x5a1bd8;
  0x289278-0x2892c0); current page people+0x320, clamped to pages−1.
- Arrow visibility (0x28932c-0x289400): page 0 ∧ pages 1 → hide both;
  page 0 → hide left/show right; page == pages−1 → show left/hide right;
  else show both.
- All 19 widgets hidden, then the visible ones placed (loop 0x28948c-0x28955c):
  - linear index = page·(cols·rows) + **col·rows + row** (column-major within
    a page; outer loop = column, inner = row).
  - **x = contentLeft(people+0x260) + col · (w+3)** → pitch **128** (125+3).
  - **y = contentTop(people+0x264) + row · (h+2)** → pitch **27** (25+2).
  - per widget: `cWinInterest::SetInterest(person)` then SetArea then
    ShowWindow (0x2894c4-0x289530).
- Concrete SB/1024 host-local grid (contentTop ≈ titleH+5):
  columns at x ≈ 13, 141, 269; rows at y ≈ contentTop, +27, +54; page 0 shows
  9 (cols×rows) of the 15 human topics, page 1 the remaining 6.
- Page-arrow clicks (TSOnCommand 0x28ca9c-0x28cb34): left → page = max(page−1,0);
  right → page = min(page+1, pages); then BuildInterests + repaint (vt+0x170).

### 2.4 Cell rendering (cWinInterest::TSPaint @0x405e80) — DECODED
Private surface = widget+0x5c; cleared via vt+0x28(16), blitted to parent by
vt+0x2c(16) at the end. Three blits (dest rects built at 0x405eb4-0x406004):
1. **topic icon** (buffer widget+0x198, res 1600+i, loaded in Init via
   0x3b6190(0x640+i, &w+0x198, 4) at 0x40626c) — drawn FULL SIZE at
   host-local (0,0)-(75,25). (Icons 1616-1618 are 75x26 — 1 px taller; they
   overflow the 25-px cell by 1 px as shipped.)
2. **bar background** — shared static buffer (res **1620**
   kInterestBarsBackground, 40x16, loaded once into the TOC−0x6510 static at
   0x406290-0x4062a8) at **(iconW+5, 5)** = (80,5)-(120,21).
3. **bar fill** — only if value (widget+0x190) ≠ 0: shared static res **1619**
   kInterestBars (40x16, TOC−0x6514 static) with src rect (0,0,**value·4**,16)
   drawn over the background's left edge — **fill width = value × 4 px**,
   0..40 px for value 0..10 (0x405fa8-0x406004).

### 2.5 Value source (cWinInterest::SetInterest @0x405b10) — DECODED
Jump table at DATA 0x69e10 (TOC −0x491c) on widget+0x18c (index); each case
reads a SIGNED HALFWORD from the selected person (cXPerson from
PersonFinder 0x244690):

| widget | person offset | widget | person offset |
|---|---|---|---|
| 0 Travel | **+0x5e8** | 10 Pets | +0x5ec (case of w2) |
| 1 Money | **+0x5ea** | 11 School | +0x5ee (case of w3) |
| 2 Politics | **+0x5ec** | 12 Exercise | **+0x5a6** |
| 3 The 60's | **+0x5ee** | 13 Food | **+0x5a8** |
| 4 Weather | **+0x5f0** | 14 Parties | **+0x5ac** |
| 5 Sports | **+0x5f2** | 15 Style | **+0x5b4** |
| 6 Music | **+0x5f4** | 16 Hollywood | **+0x5c0** |
| 7 Outdoors | **+0x5f6** | 17 Technology | **+0x5f8** |
| 8 Toys | +0x5e8 (case of w0) | 18 Romance | **+0x5fa** |
| 9 Aliens | +0x5ea (case of w1) | | |

Widgets 8-11 deliberately share the cases of 0-3: **for a pet, the same
person halfwords +0x5e8..+0x5f6 back the 8 pet topics** (Toys..School read
+0x5e8..+0x5ee, Weather..Outdoors read +0x5f0..+0x5f6) — i.e. the first eight
interest slots are species-polymorphic (INTERPRETED semantics; the field
sharing is DECODED).

Value mapping (every case, e.g. 0x405b44-0x405b64): `value = raw / 100`
(signed, magic 0x51EC851F mulhw then srawi 5 + sign fix), then clamp
**value ≤ 10** (0x405d5c-0x405d6c; no lower clamp). So raw interests are
0..1000-scale → bar 0..10 → 0..40 px. If the widget is currently hovered
(client+0x14 == widget and widget visible), SetInterest also pushes
`SetName(names[i])` / `SetDesc(descs[i])` into the cWinLivePopup client
(0x279a60/0x2799c0, 0x405d70-0x405dc4).

### 2.6 Strings (verified by extracting GameData/Live.iff, format STR# v−3)
- **STR# 140 "Interest Strings"** (live): [0] "Interests" (panel title), [1..19]
  topic names (list above), [20] "".
- **STR# 142 "Interest Descriptions"** (live): [0] intro ("Interests are what
  your sim like to talk about."), [1..19] per-topic popup text.
- The ctor (@0x4063e8-0x406530, one-time guard at r2+0x1b30) iterates both
  sets 19× into static pointer lists: names → BSS 0x95d7c region (TOC −0x6508),
  descs → BSS 0x95d24 region (TOC −0x650c). list[i] = STR#[i+1] (the iterator
  advances before the first read — the title path at 0x2872d4 takes element
  [0] directly). SetInterest's popup uses names[i]/descs[i].
- QuickTips (arrows): **STR# 154 MiscStrings** [0] "Previous Page" / [1] "Next
  Page" (in UIText.iff).
- Tooltips on each widget = its topic name (Init 0x406240-0x40625c passes
  listA[i] to 0x504ad0).
- The hover-popup client object at people+**0x270** (0x84 bytes, ctor
  0x27a770) carries a StringSet at +0x18 loaded from table name **"int"**
  (DATA 0x58195 — "live"+9, built at 0x28774c-0x287780).

### 2.7 Other cWinInterest internals
- TSOnMouseDownL @0x405e20 (32 B): forwards to the popup client (hover pin).
- Shutdown @0x406110, dtor @0x406300; cWinInterestBufferCleanup dtor
  @0x4066a0 frees the static string lists.
- vtable from TOC −0x6518.

---

## 3. THE GIFT / INVENTORY PANEL — DECODED

Populated by `cWinPeople::BuildGifts` @0x2887d0 (Init 0x2910e0, tab/click
paths 0x28cc28-0x28cd48, UpdateViewFromCPState 0x28b1e0).

### 3.1 Items
- r31 = people+**0x368** − 1 = max items per page − 1 → per-page =
  **9 (1024) / 5 (800)** (people+0x368 = 10/6, set in Init alongside 0x31c).
- Gets the selected person's inventory selector (person+0x118 → +0x1c →
  0xb4f10 → 0xac680).
- Three filter buttons (see §4) gate the categories; BuildGifts queries counts
  via 0x400760(selector, cat) for cats **0, 4, 5** (gift filter), **7**
  (magic), **8** (other), then walks each category with
  0x400d40(sel, cat, &idOut) / 0x400c70(sel, idx, &item) (0x288930-0x288f0c).
- For each item: reuse an existing widget (match on widget+0x18c == item id)
  else `new cWinGift` (0x1a4 bytes; ctor @0x408460 takes (client =
  people+0x348, long itemId)); appended to the master array people+0x360
  (count +0x35c) AND the display array people+0x354 (count +0x350); AddChild
  to host +0x328; hidden. A temporary inventory token is initialized with
  count 1, but every category path immediately fills it through
  `GetTokenAtIndex`, then copies the token's signed halfword **+0x60** to
  widget+0x190 (cat 0: 0x288a38-0x288a44; cat 4: 0x288b68-0x288b74;
  cat 5: 0x288c98-0x288ca4; cat 7: 0x288dc8-0x288dd4; cat 8:
  0x288ef8-0x288f04). The label therefore shows the **actual token stack
  count**, not a constant 1.
- The display array is sorted by item id ascending via 0x292610 with
  comparator `cWinGift_LessThan` @0x2891b0 (0x288f10-0x288f24).
- people+0x364 = total = sum of the five category counts; pages = people+0x370
  = ceil(total / r31); current page people+0x36c (clamped). Arrow visibility
  law identical to §2.3 (0x28f90-0x289098). Zero items → hide both arrows and
  return **before changing the current page** (0x288f90-0x288fc4). Thus an
  empty filter preserves people+0x36c while displaying no pager controls.

### 3.2 Layout — single row
Loop 0x2890a0-0x28916c: for index = page·9 … min(count, (page+1)·9):
- **row = index / 9; col = index % 9** — a single horizontal row per page.
- **x = contentLeft(people+0x338) + col · w** (w = widget width; no x-pitch
  beyond the widget width — cells abut).
- **y = contentTop(people+0x33c) + (hostH − contentTop − h)/2** — the row is
  vertically centered in the content band (0x2890e4-0x289128).
- SetArea + ShowWindow.

### 3.3 The cWinGift cell (ctor @0x408460, Init @0x4082c0) — DECODED
- cTSWinBtn subclass (size 0x1a4); +0x18c = item id; +0x194 = item NAME
  cTSString, +0x198 = description (from the object definition via catalog
  0xda0e0; fallback strings from a static block at TOC −0x4914 when the
  catalog misses); +0x19c = the popup client; +0x190 = count (hword).
- Init: SetImage(NULL,0,4,1); **SetArea(l, t, l+0x2a, t+0x3c) → 42 x 60**
  (0x408330); SetFont(**font[7]**, BSS 0x92900+0x1c); flags 0xf0/0xec = 1;
  tooltip = the item NAME string (0x408350-0x408360).
- Icon: the object's catalog thumbnail sprite id (objdef+0x60 → +0x52 hword →
  0x3b6430) into widget+**0x1a0**; fallback = static res **598
  kItemUnknown**, `cpanel\CatalogUnknown.bmp` (90x45; TOC −0x6500,
  0x4084ac-0x4084c4; identity from shipped `Res_CPanel.h/.RT`).
- TSPaint @0x407e90: (1) one-time label color from two globals (0x407eb0-0x407f28);
  (2) icon blit with **src = left half of the icon buffer** (src right =
  bufferW/2, 0x407f30-0x407f4c) — the catalog icon sheets hold two frames
  side by side; the LARGE (left) frame is used — dest horizontally centered
  in the 42-px cell (0x407fa4-0x407fc8). The branch at 0x407fd0 tests the icon
  handle itself: non-null draws that buffer; null calls `AsBuffer` @0x571d80
  on the static kItemUnknown handle and draws the shipped placeholder
  (0x407ff8-0x408024). It is not a selected/greyed-state branch;
  (3) count label: `sprintf(str, fmt, widget+0x190)` (0x37000 with the static
  format at TOC −0x4914), label rect = (0, iconH+3, 42, iconH+3+textH)
  (0x408080-0x4080dc), background fill from a global color (vt+0x28) then
  font->vt+0x88 draw (0x408108-0x408154).

### 3.4 Strings
- Panel title = STR# 141 "Gifts" [0] = **"Inventory"** (live table).
- Item names/descriptions come from the object catalog, not STR#.
- Arrow quicktips = MiscStrings 154 [0]/[1] (same as interests).

### 3.5 Sort/filter buttons — DECODED (`InitInventorySorts` @0x28e2b0)
Three cTSWinBtn children of the gift host, all `SetImage(_, 4, 1)` (80x20
sheets → **20x20 cells**), flags set, shown, and state-toggled per filter:

| member | res | art | filter categories |
|---|---|---|---|
| people+0x1a8 | **4112** | cpanel\Buttons\InventoryGiftSort.bmp | cats 0, 4, 5 (default on) |
| people+0x1a4 | **4113** | cpanel\Buttons\InventoryMagicSort.bmp | cat 7 |
| people+0x1ac | **4114** | cpanel\Buttons\InventoryOtherSort.bmp | cat 8 |

BuildGifts reads each button's +0xd8 state to pick the category set
(0x288834-0x2888c0): Gift (+0x1a8) selects {0,4,5}; otherwise Other
(+0x1ac) selects {8}; otherwise Magic (+0x1a4) selects {7}. TSOnCommand
rebuilds, clears both other buttons, and leaves exactly the clicked button
active (SetState 0x50b350 + BuildGifts, 0x28cc90-0x28cd58). Each sort button
was initialized with cTSWinBtn mode +0xf0 = 1; its base mouse handler
@0x50bd60 sets (rather than toggles off) state 1, so clicking the current
filter preserves the one-active radio behavior.

The three filter-command paths @0x28cc90-0x28cd88 never write current page
people+0x36c; they call BuildGifts at the existing page. For a non-empty new
result, BuildGifts clamps only when page >= pages (0x288fc8-0x288fdc). For an
empty result it retains the page as described in §3.1. Selecting a filter is
therefore **not** a reset-to-page-zero action.

Placement (0x28e640-0x28e8a4): a **vertical stack at host-local x = 2**
(width = cellW+2 ≈ 22), tops derived from font[6]'s measured title height
**M = 13 px**
(vt+0x60 on font at TOC −0x7178 area) plus button heights:
- SB: magic at M+0x14(20), other at M+H(gift)+0x15(21), gift at M+H(gift)+H(magic)+0x16(22)
- DB: M+0x28(40), M+H+0x29(41), M+H+H'+0x2a(42)
i.e. a ~21-px-pitch stack down the host's left edge below the title — which is
why the gift content-left is +0x24 (36) instead of the interest panel's +4.

R188 integration correction: the single-byte branch is literally `M+20`,
`M+H(gift)+21`, and `M+H(gift)+H(magic)+22` at
`0x28e794..0x28e8a0`. The raw conditional at `0x28e640` is
`0x41820140`, PowerPC `beq`: `IsDoubleByteUI == 0` therefore selects this
branch. The older r131/r159 text decoder mislabeled that opcode as `bne` and
temporarily inverted the SB/DB prose. An earlier port transcription subtracted 30 from all
three tops (`M-10`, `M+11`, `M+32`), which put the Magic button over the
leading `Inv` of the title. The shipped 20px cells make the exact host-local
tops **33, 54, and 75** in Magic/Other/Gift order for SB. The DB branch gives
**53, 74, and 95**.

### 3.6 Cell click and text-only live popup — DECODED

The cell click is informational only; it does **not** select, give, consume,
or otherwise mutate the inventory item:

- `cWinGift::TSOnMouseDownL` @0x407e30 is only an eight-instruction wrapper
  around `cTSWinBtn::TSOnMouseDownL` @0x50bd60. There is no cWinGift-specific
  mutation or transfer call. The base handler plays its normal button sound
  and sends the parent command during **mouse-down** (0x50bf20-0x50bf44), not
  on a later mouse-up.
- For panel 7, `cWinPeople::TSOnCommand` @0x28cb40-0x28cbe4 scans the current
  display array people+0x354. If the clicked cell is already the active
  `cWinLivePopup` client, it calls `DisablePopup(true)`; otherwise it sets the
  current client window, calls `cWinGift::SetupClient` @0x4081f0, then
  `SetClient` / `EnablePopup(client, people)`. A second click on the same cell
  therefore closes the popup; a different cell replaces its contents.
- `cWinGift::SetupClient` sends widget+0x194 (catalog name) to `SetName` and
  widget+0x198 (catalog description) to `SetDesc`. The cell tooltip is the
  same catalog name (§3.3).
- `BuildGiftPanel` @0x286f14-0x286f90 constructs the gift popup client with
  both bitmap/object picture sources null (tag `"gift"`). Consequently
  `cWinLivePopup::RebuildBuffer` @0x278630 takes the no-pane branch: fixed
  width **559**, minimum height **127**, title/body text from **x=15** to
  **x=545** (530 px wide), title y=12, body y=31, body pitch 17. It does not
  reserve the 168-px picture pane used by options/about popups. As with the
  other people-panel live popups, the result is right-aligned to the people
  panel and rises above the 100px band.

---

## 4. VISIBILITY LAW — DECODED

`cWinPeople::SetPanel` @0x28a510 (current panel in people+0xf4):
1. Hides the live popup if switching (0x28a540-0x28a550).
2. A "previous panel" switch (jump table DATA 0x580d8) unhilights the old tab
   (0x50b350(_, 0) on people+0x154/+0x164/+0x15c/+0x160/+0x158/+0x168/+0x16c).
3. Special case: panel 4 (House) requires a condition (0xaaa20 on the person)
   else it reroutes to panel 0 = none (0x28a5e0-0x28a610).
4. **Hides ALL candidate hosts** (0x28a618-0x28a714): +0xf8, +0xfc, +0x100,
   +0x1bc (name btn), +0x108, +0x114, +0x10c, +0x110, +0x104, +0x118, +0x11c,
   **+0x250 (interest)**, **+0x328 (gift)**.
5. Show-side jump table (DATA 0x580b8, 1-based):

| panel | shows | tab highlighted (people+) |
|---|---|---|
| 0 | (nothing) | — |
| 1 Motives | +0xf8 (human) or +0xfc (pet, see §5) + motive refresh | 0x154 (Mood) |
| 2 Personality | +0x100 + name btn 0x1bc | 0x164 |
| 3 Job | +0x11c ReportCard (pets) / +0x10c DogSkills / +0x110 CatSkills / +0x114 Fame / +0x108 Job | 0x15c |
| 4 House | +0x118 | 0x160 |
| 5 Relationships | +0x104 | 0x158 |
| **6 Interest** | **+0x250** (tip mode 0x200 via 0x210740) | 0x168 |
| **7 Gift** | **+0x328** (tip mode 0x400) | 0x16c |

6. Tab clicks (`cWinPeople::TSOnCommand` @0x28c090, window-pointer compares):
   Mood(+0x154)→1, Personality(+0x164)→2, Job(+0x15c)→3, House(+0x160)→4,
   Relationship(+0x158)→5, **Interest(+0x168)→SetPanel(6)** (0x28c444-0x28c464),
   **Gift(+0x16c)→SetPanel(7)** (0x28c47c-0x28c49c). Clicks are ignored when
   no person is selected (people+0xe0 == −1 → just unhilight).
7. After switching: UpdateViewFromCPState (0x28ad00) + full repaint (vt+0x170).

So the Interest TAB and the Gift TAB are two independent panels over the same
band; there is no mode where both hosts are visible.

---

## 5. Pets and host +0xfc (correction + pet path)

- people+0xfc = the **pet-motive host**: on SetPanel(1) with
  `lha person+0x60e` ∈ {0,1} the human host +0xf8 is shown and +0xfc hidden;
  for larger values +0xfc is shown instead (0x28a744-0x28a7c4). This host
  holds the 8 pet cWinMotive (people+0x134 array) per r144 §1.5 — it has
  nothing to do with interests/gifts.
- The INTEREST panel's pet path: species = `lha person+0x600`, pet iff
  0 < species < 18 → 8 pet widgets from people+0x2fc (Toys, Aliens, Pets,
  School, Weather, Sports, Music, Outdoors) reading person+0x5e8..+0x5f6
  (shared with human widgets 0-7, §2.5).
- The GIFT panel has no pet-specific code path (BuildGifts keys only on the
  inventory selector).

---

## 6. Fonts & static singletons (r143 law: font[i] at BSS 0x92900+4i)

| element | font | evidence |
|---|---|---|
| interest/gift panel titles | **font[6]** (0x92900+0x18) | 0x2871d8, 0x2869e8 |
| cWinGift count label | **font[7]** (0x92900+0x1c) | 0x40833c |

Shared statics (created on first use):
- interest bars fill res 1619 → static at TOC −0x6514 (Init 0x406280).
- interest bars background res 1620 → static at TOC −0x6510 (Init 0x40629c).
- gift default icon res 598 → static at TOC −0x6500 (ctor 0x4084b8).
- interest name/desc pointer lists → TOC −0x6508 / −0x650c (ctor 0x4064a0+).

## 7. Art inventory (UIGraphics.far; dims verified from BMP headers)

| res | art | size |
|---|---|---|
| 1600-1615 | cpanel\Buttons\Interest*.bmp (Travel..Style) | 75x25 |
| 1616-1618 | InterestCharisma/Health/Romance.bmp (= Hollywood/Technology/Romance) | **75x26** |
| 1619 | cpanel\InterestBars.BMP | 40x16 |
| 1620 | cpanel\Backgrounds\InterestBarsBackground.BMP | 40x16 |
| 4112/4113/4114 | cpanel\Buttons\Inventory{Gift,Magic,Other}Sort.bmp | 80x20 (cell 20x20) |
| 100/101 | ScrollLeft/Right (page arrows) | 36x49 (cell 9x49) |
| 598 | cpanel\CatalogUnknown.bmp (kItemUnknown gift fallback) | 90x45 |

## 8. Port-relevant corrections vs prior rounds

1. Interests/gifts do NOT live in people+0xfc — that host is pet motives.
   Interest host = people+0x250, gift host = people+0x328, both direct
   children of cWinPeople sharing the r144 band law (300,50,804,150).
2. The interest grid is **3 columns (1024) / 2 (800) × 3 rows (SB) / 4 (DB)**,
   column-major, cell 125x25, pitch 128x27, content origin (leftArrowW+4,
   titleH+5) — NOT a fixed 4x4.
3. 19 topics exist; humans show 15 (Toys/Aliens/Pets/School excluded), pets 8.
4. Bar fill = (raw/100 clamped ≤10) × 4 px over the 40x16 background.
5. Panel ids are 1-based (6 = Interest, 7 = Gift); the r144 tab↔button array
   order {4500,4504,4503,4502,4501,4505,4507} maps to people+0x154..+0x16c.
6. Gift panel = one 42x60-cell row (9 items at 1024), vertically centered,
   sorted by item id, plus a 3-button filter stack at host x=2. Gift/Magic/
   Other select category ids {0,4,5}/{7}/{8}; every cell shows its token's
   real count and toggles a full-width, text-only live popup on click.

## Residuals (honest)
- Both RIGHT page-arrow residuals are closed: each is fully in-host at
  hostW−btnW−2, with exact x **493..502 / 269..278** at the two host widths;
  the four horizontal source cells remain normal/selected/hover/pressed.
- The font/title-height residual is closed: font[6] has a **13px LineHeight**,
  fixing the title content top and the Gift sort-stack y origins above.
- The count-label format string (TOC −0x4914 static) was not read; its value
  source is now exact (inventory-token halfword +0x60 copied to cWinGift+0x190).
- The inventory category ids {0,4,5,7,8} semantics (gift/magic/other groupings)
  are DECODED as ids only; their OBJD-level meaning was not traced.
- cWinInterest icon buffers 1616-1618 are 75x26 vs the 25-px cell — the
  shipped 1-px overflow noted, clipping behavior not verified.
- person+0x60e's full pet-type encoding (cat=1? dog=2?) only partially
  inferred from SetPanel panels 1/3.
