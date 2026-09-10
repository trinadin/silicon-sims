# R122 — the ORIGINAL buy-mode catalog (STR# 150 sorts + cpanel\Catalog art)

Round goal: convert the last major modern-UI surface in the bottom bar — the
buy-mode catalog — to the original's own data: sort names/order from UIText.iff,
plaques/sheets/frames from UIGraphics.far, room sorting on the original
RoomFlags bits.

## 1. String canon (UIText.iff, decoded by `make_r122_buycat_canon.py` → `r122-buycatalog-strs.txt`)

### STR# 150 'BuyModeCatalogSortTips' — 960 raw = 48 English × 20 langs, format -3
full-chunk sha256 `d99134a4b8d6b2b58bad808f6a33e3b79c32a67cbfb484187990e473e0e5de6e`
(header-inclusive, gate convention). Six fixed blocks of 8 sort names:

| block | idx | names |
|---|---|---|
| home ROOMS | 0–7 | Living Room, Dining Room, Bedroom, Study, Kitchen, Bathroom, Outside, Miscellaneous |
| home FUNCTIONS | 8–15 | Seating, Surfaces, Decorative, Electronics, Appliances, Plumbing, Lighting, Miscellaneous |
| Downtown (Hot Date) | 16–23 | Dining, Shops, Outdoors, Street, Entertainment, Bathroom, Decorative, Miscellaneous |
| Vacation (On Vacation) | 24–31 | Lodging, Shops, Recreation, Amenities, Vacation Five(Unused), Six(Unused), Seven(Unused), Miscellaneous |
| Old Town (Unleashed) | 32–39 | Food, Shops, Outdoors, Street, Unleashed Five(Unused), Six(Unused), Seven(Unused), Miscellaneous |
| Studio Town (Superstar) | 40–47 | Food, Shops, Studio, Spa, MagiCo, Superstar Six (Unused), Seven (Unused), Miscellaneous |

Despite the label "...SortTips" the table carries ONLY sort-button names (the
"tips" are the button tooltips) — zero long-form sentences, zero `(x;y)` layout
directives (unlike 145 'optionstrs' in R121). The kBuyF* (150–157) and kBuyR*
(158–165) plaque symbol orders in Res_CPanel.h are EXACTLY the [8..15] and
[0..7] orders.

### Subsort tables
- STR# 200–207 '<Name> Function Sub Sorts' — each 102 raw = 6 English × 17 langs;
  entries [0..3] named, [4]/[5] EMPTY reserved slots (pinned as empty):
  200 Seating: Dining Chairs, Lounge Chairs, Sofas, Beds · 201 Surfaces:
  Counters, Tables, End Tables, Desks · 202 Decorative: Paintings, Sculptures,
  Rugs, Plants · 203 Electronics: Entertainment, Video, Audio, Phones ·
  204 Appliances: Stoves, Refrigerators, Small Appliances, Large Appliances ·
  205 Plumbing: Toilets, Showers/Tubs, Sinks, Hot Tubs · 206 Lighting:
  Table Lamps, Standing Lamps, Wall Lamps, Hanging Lamps · 207 Miscellaneous:
  Recreation, Knowledge, Creativity, Wardrobe. (Per-table shas pinned in the gate.)
- STR# 210 'Function Sub Sort Extra Strings' — 100 raw = 5 English × 20:
  Back, Other, All, Pets, Magic. sha `7f3df62e...` (full value gate-pinned).
- STR# 154 'MiscStrings' [0]/[1] = 'Previous Page' / 'Next Page' — the paging
  captions (full table sha-pinned; the two entries additionally value-pinned).

Dev-note remnants live in the padding of some tables (e.g. 210's trailer
contains "##SPELLBOUND -- Needs Tra..."); they are inside the hashed chunks and
pin byte-verbatim. Trailer length always equals the declared count (writer
flushes one pad byte per entry — same behavior as r121's 145 at 1512).

## 2. Art canon (`make_r122_art_canon.py` → `r122-art-canon.txt`)

80 members, FULL lowercase path keys (the subsort sheets repeat basenames
across 8 family directories — a basename key would be ambiguous):
- 8 BuyF* + 8 BuyR* main plaques (single images, 84–152 × 29–43)
- 8 RoomSubsort sheets (144×36 = 4 frames of 36×36)
- 32 function subsort sheets (Seating..Miscellaneous × One/Two/Three/Four)
  + BuySubSortPets/Magic + BuySubSortOther/All singles (124×29)
- 16 expansion plaques: Downtown/Community/Vacation sets + Studiotown/Magictown
  per the res_cpanel.RT ALIASES (kBuySTfood→BuyDDining.BMP, kBuySTspa→
  BuySTSpa.bmp, kBuyMTmagic→BuyMTmagic.bmp etc. — the RT is authoritative over
  the unreferenced BuySTDining/BuySTMisc/BuySTShops alternates on disk)
- ScrollLeft.BMP / ScrollRight.BMP paging arrows (36×49, kCatalogPrevPage/
  kCatalogNextPage) + ThumbTemplate.BMP (180×45 = 4×45×45) and
  ThumbTemplate1Frame.BMP (45×45).

Zero members missing. The one RT symbol with NO member on disk remains
kCatalogBck → 'CPanel/Backgrounds/BuyBack.bmp' (same missing-class as R121's
OptionsBack.bmp; the buy panel background stays port chrome — disclosed).

## 3. Port

- `UIMainPanel`: BuyCategories (8 port-ordered PNG icons) REPLACED by
  canon-ordered builders — `GetBuyFunctionCategories()` (STR# 150 [8..15]
  order, BuyF* plaques, IDs stay the catalog function IDs {0,1,5,3,2,4,7,6})
  and `GetBuyRoomCategories()` (STR# 150 [0..7] order, BuyR* plaques, IDs
  100+display-index). Captions go to the button TOOLTIPS (the table's own
  label: sort tips). BUY mode pages between the ROOM page and the FUNCTION page
  via two original arrows (ScrollLeft/Right.BMP, tooltips 154 [0]/[1]);
  16 plaques cannot fit the expanding column at once (engine composition,
  disclosed). MainButton.OriginalStyle suppresses the modern round chrome.
- `UIBuyBrowsePanel`: new `roomMode` ctor param — the room main sort shows
  every item whose ORIGINAL RoomFlags bit carries the room, across all 8
  function categories; the room subsort row is the canon kBuyRSubSort* set
  (the 8 FUNCTION sorts, captions 150 [8..15], RoomSubsort sheet art), each
  filtering on the item's catalog function category. Function mode subsorts
  now mount the ORIGINAL per-family sheets (slot j ↔ STR# 200+j entry j —
  the empty reserved slots [4]/[5] are exactly why only 4 slots have art);
  Other/All/Pets/Magic from 210 + their art. Expansion-lot (Downtown/
  Community/Vacation/Studiotown/Magictown) subsort rows mount the RT-mapped
  plaques. All subsort captions become tooltips; InitSubcategory's switcher
  image replacement uses the original frame.
- `UICategorySwitcher`/`UICatButton`: UICategory.Caption; tooltips wired on
  both the main button and the expanding stencil buttons; OriginalStyle skips
  the modern cat_btn_base chrome.
- `UICatalogItem`: the ORIGINAL ThumbTemplate1Frame (45×45) behind every
  catalog item at native size, selected state = frame 1 of the 4-frame
  ThumbTemplate sheet (frame order read with the UIButton convention — our
  disclosed choice); icons scale to 41px inside the frame.
- `UIOriginal.Frame(member, frame)`: crops one frame out of the 2–4-frame
  original sheets (144×36→36×36 etc.), cached; single art passes through.
- Engine fixes this round (both latent bugs exposed by the room view, which is
  the first code to aggregate items across all function categories):
  1. `IffFile.InitHash` (FreeSO submodule): `hash.Update(chunk.ChunkData ??
     chunk.OriginalData, chunk.ChunkData.Length)` crashed on lazily-loaded
     chunks (null length arg) and some registered chunks carry NEITHER buffer —
     now hashes the buffer actually passed and skips dataless chunks.
  2. `UISubpanel.Kill`: deferred `Parent.Remove(this)` NREs for panels never
     Add()ed to a parent (the gate's introspection constructions) — now `?.`.

## 4. The RoomFlags bit order — DATA-DERIVED, not assumed

The original's internal room-bit order differs from the 150 [0..7] display
order, exactly like function order vs `RemapString`. Derived from the live
catalog (r122p6 diagnostic matrix + anchors):

| bit | room | evidence |
|---|---|---|
| 0x01 | Kitchen | 'Stove - Black Iron' r=0x01; 25 counters + 26 appliances |
| 0x02 | Bedroom | 'Bed - Single - SciFi' r=0x02; 15 beds |
| 0x04 | Bathroom | 'Toilet - Aluminum' r=0x04; 29 plumbing |
| 0x08 | Living Room | 61 seating (sofas/armchairs) |
| 0x10 | Miscellaneous | 100 mixed, heavy decorative+misc |
| 0x20 | Dining Room | 25 dining chairs + 29 tables |
| 0x40 | Outside | 128 plants + 65 outdoor lights |
| 0x80 | Study | 'Bookshelf - Retro' r=0x80 |

Port map `CanonRoomToFlagBit = {3,5,1,7,0,2,6,4}` (display index → bit).
The gate pins it empirically: Kitchen contains appliances, Bathroom plumbing,
Living-room panel items all carry 0x08.

## 5. Gate ('uibuy', 69→70 checks)

1. Canon: STR# 150 label/count/sha + ALL 48 entries; 200–207 shas + entries
   0–3 (+ empties 4/5); 210 sha + 5 entries; 154 sha + [0]/[1] values.
2. Art: all 80 members (length + raw bytes + BMP dims + sha256; pins
   REGENERATED by `regen_gate_shas.py` — the r119/r121 no-hand-typed-shas
   discipline).
3. Live (post-lot-load, restores prior mode): function page = 8 canon captions
   as tooltips + plaque dims vs pins; paging arrows visible with 154 tooltips;
   subsort row = STR# 200 names + Other/All with 36×36 sheet frames and
   124×29 singles; room page toggle → 150 [0..7] captions, room panel filters
   on the flag bits, 8 function subsorts; Kitchen↔appliances + Bathroom↔
   plumbing empirical bit pins; Downtown row = 150 [16..19] plaques;
   ThumbTemplate frames mounted 45×45.

## 6. Runs (honest log)

| run | result | cause |
|---|---|---|
| r122p1 | FAIL uibuy EXC NRE | no stack trace yet — turned out to be the InitHash engine bug |
| r122p2 | FAIL uibuy EXC NRE | stack added: IffFile.InitHash crash (first engine fix) |
| r122p3 | FAIL uibuy EXC NRE | InitHash still crashing: some chunks have NEITHER buffer (second half of the fix) |
| r122p4 | FAIL live | gate-side: my art loop consumed the pin-lookup dict (fixed with an `unseen` set); roomBits failing |
| r122p5 | FAIL live roomBits | diagnostics: Kitchen had appliances but "Bathroom" (bit 5) had zero plumbing → bit-order suspicion |
| r122p6 | FAIL live roomBits | matrix + anchors dumped → the TRUE RoomFlags order (§4) → port map fixed |
| r122p7 | uibuy PASS, then Abort trap 6 | UISubpanel.Kill deferred `Parent.Remove` NRE on unparented gate panels (port fix) |
| r122p8 | **PASS 70/70** | full soak, clean exit (AUTOTEST_WRAPPER_EXIT=0) |

## 7. Residuals (disclosed)

- kCatalogBck/BuyBack.bmp missing from this disk — buy panel background stays
  port chrome (same class as R121 OptionsBack; PanelBack/BuyPatch exist for a
  future composition round).
- Paging arrow placement (right of the switcher), the 2-page rooms/functions
  model, subsort row layout, and the item-cell composition (frame + 41px icon
  + modern price label) are engine choices — canon supplies the art and the
  caption strings, not positions.
- Build-mode subcategory buttons still use port icons (BldTips strings already
  canon since before; the build tool art is a separate family).
- The three unreferenced BuyST* alternates on disk stay unwired (the RT itself
  aliases the D* files — followed).
- STR# 160 'CatalogRatings' decoded and archived (motive ratings 'Hunger: %d'
  + skill tags + 'Can only be used by...' — 20 entries, sha in the canon txt);
  mounting those in the object description UI is future work.
