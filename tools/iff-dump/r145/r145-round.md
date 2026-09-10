# R145 — BUY/BUILD toolbars + Interest/Gift panels (round evidence)

Round target (from R144's recommendation): the buy/build desktop toolbars on
the cWinArch/cWinCatalog law + the Interest/Gift tab subpanels — the last
interpreted pieces of the in-lot chrome.

## Decode (3 Hermes agents, this directory)

- `build-toolbar-law.md` — cWinArch/cWinCatalogPopup/cWinRoofPanel. THREE
  corrections to r144 §4: (1) the 12 tool buttons NEVER flow — static 12-pair
  anchor table BSS 0x92a90, two hand-tuned rows; the (338,5) pitch-45 flow is
  the SUBTOOL pattern row. (2) undo (11,21)/redo (11,53) and page arrows
  (326,26)/(archW−14,26) are placed in TSPaint, not the popup. (3) the
  "(201,0,W,100) popup area" is a mouse-guard rect. Also: tooltips STR# 139
  [0..15] (tools 0-11 then undo/redo/pageL/pageR — confirming the port's
  StrInd mapping 0..10 and adding Hand=11); the hover popup floats ABOVE-LEFT
  of the hovered button over the 3D view (559x127, icon pane 168); the roof
  panel swaps the pattern row at (314,5,W,100) with pitch icons
  steep/medium/shallow/flat at (6,1)/(6,30)/(6,50)/(6,71) (4-state sheets —
  correcting R130's natural-size reading) and the pattern grid at (70,1)
  45px pitch, 16/page @1024; toothpick strips at x 49/319, y 6.
- `buy-catalog-law.md` — cWinCatalog. 8 plaques 2x4 at irregular anchor
  tables (room/function mains) or the regular 38/48 sub grid (31,10);
  BuyBack* = the LEFT toggle plaque only (flush at −2, vertically centered,
  hidden in main state); the Objects (buy) button TOGGLES room<->function on
  every activation, first entry = ROOM main Living; the item grid = 45x45
  thumbnail-only cells at (195,5), 13 cols x 2 rows = 26/page @1024, paged by
  res 102/101 arrows at (184,26)/right-edge; name/price live in the hover
  popup (STR# 160 = CatalogRatings); kCatalogBck 48 is never loaded (Mac).
- `interest-gift-law.md` — cWinPeople::BuildInterestWindow/BuildGiftPanel.
  Interest: title font[6] 'Interests' (Live.iff STR# 140 [0]); 15 human
  topics (STR# 140 [1..15]); cells 125x25 (icon plaque 75x25 + bar bg 40x16
  at (80,5) + fill revealed value*4 px, value = raw/100 clamp 10); grid
  3x3 COLUMN-MAJOR pitch 128x27; res 100/101 arrows. Gift: title 'Inventory'
  (STR# 141 [0]); row of 9 cells 42x60 (left half of the catalog icon + font[7]
  count); 3 filter buttons res 4112/4113/4114 (cats {0,4,5}/{8}/{7}) at host
  x=2. CORRECTION: people+0xfc is the pet-motives host; interest/gift are
  direct people children (panels 6/7).
- `survey-notes.md` — port-side survey: res→art map (Res_CPanel.RT verbatim),
  BMP dims (tools variable-width 4-state sheets 25..43 x 28..41; PopupInfo
  559x127; interest icons 75x25 single plaques), engine tool order → port
  subcat wiring, VM interest words 46-53.

## Port

- NEW `UIOriginalBandControls.cs`: `UIOriginalSheetButton` (SetImage(_,4,1)
  law — cell = sheet/4 at natural sizes, state 0/1, Remount for re-imaging),
  `UIOriginalCatalogCell` (engine ProductButton: 45x45 thumbnail-only, the
  R122 ThumbTemplate frames, roof swatch + natural-plaque variants),
  `UIOriginalCatalogPopup` (the hover/click info popup: icon pane 168px +
  name—price font[10] at text+(10,12) + desc font[8]; rating lines disclosed
  residual).
- NEW `UIOriginalArchChrome.cs`: 12 tools at the static anchors, undo/redo
  (disabled — no VM undo machinery, disclosed), toothpicks in Draw, and
  `UIOriginalRoofPanel` (pitch icons at engine anchors sending
  VMNetSetRoofCmd with the R130 pitch map; pattern grid (70,1) 16/page on the
  RoofPatternTemplate frames; panel page arrows (58,20)/(456,20)).
- NEW `UIOriginalBuyChrome.cs`: the 8-plaque row (room/function main anchor
  tables + the sub grid), the BuyBack toggle plaque, the room<->function
  toggle law, engine entry default (room main, Living, subsort 0 applied).
- NEW `UIOriginalInterestGiftSubpanels.cs`: both band panels on the law;
  Live.iff tables 140/141/142 added to OriginalLiveStrings; font[6]/[7]
  mapped in LoadByIndex (6 falls to _07 — no _06.ffn ships, disclosed).
- `UIBuyBrowsePanel`: BandMode desktop branch — the mobile touch scroll +
  subsort row are replaced by the engine grid (BUILD: (338,5) 1 row of 10,
  res 100/101 arrows at (326,26)/(W−14,26), next shown iff count > cols;
  BUY: (195,5) 13x2 26/page, res 102/101 arrows at (184,26)/right-edge) with
  cell clicks driving the existing Selected()/QueryPanel machinery; the
  click-popup mounts on the MAIN panel at above-left of the cell.
- `UIMainPanel`: chrome creation + SetMode visibility; BUILD entry = no tool
  selected (empty pattern row); BUY entry = the engine composition;
  SelectBuildTool/SelectBuyMain/SelectBuySub/ToggleBuySortEngine; subpanel
  mounts at (0,0) covering the band in BUILD/BUY desktop; Interest/Gift ids
  6/7 wired into TabCategory.
- `UISimitoneFrontend`: the Objects-button toggle law (buy re-activation
  flips the sort).
- Autotest: uibuy/uibuild live sections REWRITTEN to the engine band law
  (the mobile switcher/subsort-row pins were the pre-decode composition —
  disclosed correction, art/STR pins untouched); NEW check 'uiinterest'
  (Live.iff STR# 140/141/142 + 24 art pins + live geometry/fill/paging/filters;
  gate 85 → 86); survey states D (build band), D2 (roof panel), E (interest).

## Visual verification (dumps/, pixel-math)

- build: all 12 tools at anchors (ink 391-1208/cell), undo/redo painted,
  10-cell pattern row (Wall tool), pageR only, toothpicks at x 269/539.
- buy: all 8 plaques painted (124/80/85/102/129/85/124/73 colors after the
  RelayoutMain visibility fix), 26-cell grid 13x2, prev hidden at page 0.
- interest: title glyphs, 9 plaques column-major, bar backgrounds, arrows.
- roof: 4 pitch icons (71-115 colors), pattern grid cells rich, arrows.

## Honest residuals

- Undo/Redo mount DISABLED (the port VM has no architecture-undo machinery).
- Hand tool is selection-only (no port object-grab tool wired to it).
- Subtool row order keeps the port's price sort; the engine's per-tool sort
  key table (DATA 0x55490: {0,9,8,0,5,7,6,0,3,4,0,0} via GetSortVal) is
  decoded but the port catalog lacks the sort-val field — disclosed.
- The hover popup shows on CLICK (engine: hover AND click); hover-trigger is
  a future pass. Rating lines (STR# 160) not ported (no ratings data).
- Interest VALUES read VM person words 46-53 (TSO packing; nothing writes
  them yet — bars render at stored values, 0 today); Exercise/Food/Parties
  have no port word. Gift cells display-only (give/select unwired).
- Buy next-arrow exact x (right-aligned formula ambiguous in the decode) =
  width−24−9; interest arrow x placements are the sensible reading of the
  odd decoded formulas (disclosed in the law file).
- kCatalogPopupBack 3006/3008 unreferenced by the popup class (belong to
  cWinCatalog per agent C; not mounted).
- Expansion (DT/vacation/community/studio/magic) buy bands keep the mobile
  path (BandMode is Normal/Build only).

## Gate

`r145-gate.txt`: AUTOTEST RESULT PASS passed=86 failed=0 (full run 20:13).

## R145a addendum (crash-report follow-up, 0e61037)

User report: SIGABRT ~31s after launch + "duplicate buttons bottom-right,
overlapping; background fine, buttons broken and all over the place."
Root cause: the lot-entry path enters BUY directly on community/downtown
visits (frontend ctor, global-32) — GetLotType returns Community there, the
browse panel is NOT BandMode, and the new band chrome mounted ON TOP of the
mobile subsort row + touch-scroll catalog (the duplicates). A caught NRE at
entry ('select-buymain EXC' in game.log 20:53:33.852) left the mixed state;
an unhandled follow-on aborted the process 18s later (exact site unrecovered
— the crash log next to the binary was destroyed by a deploy rsync --delete;
CrashLog now also writes ~/Documents/Simitone/simitone-crash.log, and
SelectBuyMain logs stacks).

Fixes: BUY band only on home lots (GetLotType == Normal; expansion lots keep
the R122 composition until their decode round); BUILD stays banded (its law
is lot-independent); the mobile BuyPage arrows hide when the band owns BUY
(they were duplicate arrows on normal lots too); the Objects-toggle law is
gated to band lots. Full gate 86/86 PASS after the fix.
