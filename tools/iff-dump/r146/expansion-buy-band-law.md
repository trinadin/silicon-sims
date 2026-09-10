# R146 — EXPANSION BUY-BAND LAW (cWinCatalog sort cases 2-6, decoded from PPC binary)

> **R176 correction:** the Studio and Magic Init rows are non-sequential
> (`[273,274,276,275,...277]` and `[1705,1706,1708,1707,...1709]`), so their
> native slots 2/3 are Studio/Spa and MagiCo/Outdoors. Magic also overrides
> tooltips 2/3 to STR#150 [44]/[34]. See
> `../r176/r176-expansion-category-law.md`. The contrary Studio/Magic quirk
> claims below are retained only as the historical R146 inference.

Follow-up to r145/buy-catalog-law.md (the home-lot room/function sorts). This
round decodes the five EXPANSION lot sorts and ports them — replacing the R122
mobile composition that R145a had left serving community/DT lots on desktop
(the "duplicate overlapping buttons" report).

Binary: `game-data/The Sims/The Sims Complete` (PPC PEF, file offsets).
Sources: LoadBooks 0x2690a0 (disasm-catalog-loadbooks.txt), the mask getters
0x2686f0-0x268b10, Res_CPanel.h/RT, UIText.iff STR# 150.

## 1. Plaque sets (DECODED — LoadBooks case → member row)

| sort | mode | main art row (+0x…) | back/toggle row | subsort row | tip base |
|---|---|---|---|---|---|
| 2 | Downtown | +0x208 res 200-204 kBuyDDining/Shops/Outdoor/Street/Misc | 190-194 | +0x2a8 res 220-227 kBuyDSubSort* | 0x10 (16) |
| 3 | Vacation | +0x228 res 230-234 kBuyVOne..Four/Misc | 195-199 | +0x2c8 res 240-247 kBuyVSubSort* | 0x18 (24) |
| 4 | Community | +0x248 res 250-254 kBuyCOne..Four/Misc | 255-259 | +0x2e8 res 260-267 kBuyCSubSort* | 0x20 (32) |
| 5 | Studio | +0x268 res 273-277 kBuySTfood/shop/spa/studio/Misc | 268-272 | **+0x308 = DSubSort copy** | 0x28 (40) |
| 6 | Magic | +0x288 res 1705-1709 kBuyMTfood/shop/outdoor/magic/Misc | 1700-1704 | **+0x328 = DSubSort copy** | 0x28 (40) |

Each 8-slot row carries art at slots 0-3 and **slot 7 (Misc)**; slots 4-6 are
zero and LoadBooks HIDES them in the main state (0x269cd8: `sortState==0 &&
i∈{4,5,6} → Hide`, vt+0xa0).

VERBATIM QUIRKS KEPT (engine's own behavior):
- Studio plaque 2 = kBuyST**spa** art with tooltip [42] "Studio"; plaque 3 =
  kBuyST**studio** art with tooltip [43] "Spa" — the tip order and art order
  are crossed in the engine.
- **Magic shares the Superstar tooltip block [40..47]** (addi r0,i,0x28 in
  case 6) and the DOWNTOWN subsort art — Magic mains read
  Food/Shops/"Studio"/"Spa"/Misc.
- Res_CPanel.RT aliases kBuySTfood/kBuyMTfood → `BuyDDining.BMP`,
  kBuyST{shop,Misc}/kBuyMT{shop,Misc} → `BuyDShops/BuyDMisc.bmp` — Maxis art
  reuse. Byte-identical: BuyCOne==BuyDDining (sha 0b00cdaf…),
  Buy{C,D,V}Misc==BuyFMisc (2a345953…), all three family SubSortSeating icons
  share one bitmap (2cbd2d26…).

## 2. Anchors (DECODED — sinit writer 0x26c7d0, catalog-local = anchor − 220)

- Expansion main rows are the "downtown pattern" top row + the standard
  bottom row: top (22,8)(64,12)(98,12)(149,14); bottom slot 7 (144,64).
- **Studio's 4th main sits at (138,14)**, not 149 (its own table 0x93080).
- Subsort state = the shared sub grid (31,10)/(31,58) 38/48 pitch (same as
  home lots).
- Toggle plaque law unchanged from r145: flush −2, (100−h)/2, BuyBack<family>
  art of the current main, hidden in main state.

## 3. Tooltips (DECODED — STR# 150 of UIText.iff, 48 entries)

[0..7] rooms · [8..15] functions · [16..23] downtown mains
(Dining/Shops/Outdoors/Street/Entertainment/Bathroom/Decorative/Misc — slots
4-6 unused plaques still own strings) · [24..31] vacation (Lodging/Shops/
Recreation/Amenities/…(Unused)×3/Misc) · [32..39] community (Food/Shops/
Outdoors/Street/Unleashed×3-unused/Misc) · [40..47] superstar+magic.
**Subsort state uses [8+i] for every expansion** (addi r0,r16,8).

## 4. Filtering (DECODED — GetMagictownMask 0x268ab0 et al.)

```
sub-catalog i → mask 1<<i      (jump table: 0→1 … 7→0x80)
```
So mains = bits 0x01-0x08 and the **Misc plaque = slot 7 = bit 0x80** of the
item's expansion sort byte (DowntownSort/CommunitySort/… in the port, from
OBJD DTSubsort/CommunitySubsort). Subsort state = main-mask AND function
category (STR#150 [8..15] = the 8 functions). The sort is LOCKED per lot type
(cWinViewControl 0x2b4b24: sorts 2+ are kept — the Objects button never
toggles room↔function on expansion lots). First entry: main state, main 0,
subcatalog 0 applied to the grid.

Live data (gate uiexpband): community main0 (bit 1) = 364 items,
Misc (bit 0x80) = 428, full mode catalog = 659, downtown main0 = 386 —
bit 0x80 is heavily populated, confirming Misc as a real catalog page.

## 5. Grid/paging/popup

Identical to the r145 home-lot law: (195,5) 45x45 cells, 13×2 = 26/page
@1024, res 102/101 arrows at (184,26)/right-edge, ProductButton-style
thumbnail cells, popup 559x127. The catalog window itself is lot-type
independent (220, SH-100, W, SH).

## 6. Port map

- `UIOriginalBuyChrome`: LotMode + Exp{Main,Back,Sub}Art tables, ExpMain
  anchors (studio variant), ExpTipBase, expansion branches in
  RelayoutMain/RelayoutSub; ToggleSort stays home-lot-only.
- `UIBuyBrowsePanel`: BandMode on EVERY desktop lot; expansion branch in
  InitCategory (whole-mode catalog, BandMainSlot mask after the price sort);
  band subs = 8 function cats with the family art; InitSubcategory
  FuncId-filter generalized (main-bit AND function) and made RE-ENTRANT
  (the ChoosingSub gate used to no-op every sub click after the first).
- `UIMainPanel`: SetMode BUY band on all lots (SetLotMode by GetLotType);
  SelectExpMain(mainSlot, applyDefaultSub) — entry keeps the MAIN plaque row,
  main click enters the subsort row with subcatalog 0 applied; mobile
  BuyPage arrows hidden on every desktop BUY.
- Gate: new check **uiexpband** — 19 art pins (R146ExpArt), chrome law for
  all five modes (anchors incl. studio 138, hidden 4-6, tooltips, toggle),
  live community/downtown mask + sub-filter counts.

## 7. Residuals (disclosed)

- Plaque ENABLE matrices (engine Init 0x26bd3c builds 8x8 availability per
  expansion; the port does not gray empty plaques — also true of the r145
  home band).
- The R122 mobile Other/All subsort plaques (STR#210) were a MOBILE-only
  composition; the engine expansion band has no Other/All — removed with the
  mobile row on desktop, still served on touch builds.
