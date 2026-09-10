# R145 survey notes (port-side, pre-Hermes)

Round target: desktop BUY/BUILD toolbars on the cWinArch/cWinCatalog law
(r144/toolbar-law.md section 4) + the Interest/Gift tab subpanels (people
host +0xfc).

## Res-id -> art map (Res_CPanel.RT verbatim)

| res | symbol | member | BMP dims | cell (sheet/4) |
|---|---|---|---|---|
| 64 | kToolBtnUndo | cpanel\Buttons\Undo.bmp | 120x24 | 30x24 |
| 65 | kToolBtnRedo | cpanel\Buttons\Redo.bmp | 120x24 | 30x24 |
| 66 | kToolBtnTerrain | cpanel\Buttons\ToolBtnTerrain.bmp | 156x39 | 39x39 |
| 67 | kToolBtnTree | cpanel\Buttons\ToolBtnTree.bmp | 140x39 | 35x39 |
| 68 | kToolBtnPool | cpanel\Buttons\ToolBtnWater.bmp | 100x38 | 25x38 |
| 69 | kToolBtnFloor | cpanel\Buttons\ToolBtnFloor.bmp | 172x28 | 43x28 |
| 70 | kToolBtnWall | cpanel\Buttons\ToolBtnWall.bmp | 124x40 | 31x40 |
| 71 | kToolBtnDoor | cpanel\Buttons\ToolBtnDoor.bmp | 108x41 | 27x41 |
| 72 | kToolBtnWindow | cpanel\Buttons\ToolBtnWindow.bmp | 116x39 | 29x39 |
| 73 | kToolBtnWallpaper | cpanel\Buttons\ToolBtnWallpaper.bmp | 128x40 | 32x40 |
| 74 | kToolBtnStairs | cpanel\Buttons\ToolBtnStairs.bmp | 128x38 | 32x38 |
| 75 | kToolBtnFireplace | cpanel\Buttons\ToolBtnFireplace.bmp | 116x35 | 29x35 |
| 76 | kToolBtnRoof | cpanel\Buttons\ToolBtnRoof.bmp | 156x33 | 39x33 |
| 77 | kToolBtnHand | cpanel\Buttons\ToolBtnHand.bmp | 120x35 | 30x35 |
| 100 | kCatalogPrevPage | CPanel/Buttons/ScrollLeft.bmp | 36x49 | 9x49 |
| 101 | kCatalogNextPage | CPanel/Buttons/ScrollRight.bmp | 36x49 | 9x49 |
| 102 | kCatalogBuyModePrevPage | CPanel/Buttons/ScrollLeftBuy.bmp | 36x49 | 9x49 |
| 48 | kCatalogBck | CPanel/Backgrounds/BuyBack.bmp | ? | |
| 3006 | kCatalogPopupBack | CPanel/Backgrounds/PopupInfo.bmp | 559x127 | |
| 3008 | kCatalogPopupBackTiles | CPanel/Backgrounds/PopupInfoTiles.bmp | 36x36 | |
| 4912/4913 | kBuildToothPick1/2 | BuildToothpkLeft/Right.BMP | 2x89 | |
| 1600-1618 | kInterestTravel..kInterestRomance | cpanel\Buttons\Interest*.bmp | 75x25 (3x 25x25) | |
| 1619 | kInterestBars | cpanel\InterestBars.bmp | 40x16 | |
| 1620 | kInterestBarsBackground | cpanel\Backgrounds\InterestBarsBackground.bmp | 40x16 | |
| 4112 | kInventoryGiftSort | cpanel\Buttons\InventoryGiftSort.bmp | 80x20 | 4x 20x20 |

KEY CORRECTION vs R126: the toolBtn* plaques are **4-frame state sheets at
natural per-tool sizes** (variable width 25..43, height 28..41) — the engine
does SetImage(res, 4, 1) so each state cell = sheet/4. R126's "single-frame,
text baked in" reading was wrong (it mounted the full 156x39 sheet as one
plaque). The 45x45 flow cell (r144) holds the ~39px art.

PopupInfo.bmp is 559x127 — TALLER than the 100px band, so the catalog popup
rises ABOVE the toolbar line (window area fields (201,0,W,100); exact art
placement = Hermes A).

## Engine tool order (DATA 0x55290) -> port subcategory wiring

| i | res | tool | port group | port subcat (StrInd 139) | catalog MaskBit |
|---|---|---|---|---|---|
| 0 | 66 | Terrain | outdoors (1) | 0 | 18 |
| 1 | 68 | Pool | outdoors (1) | 1 | 14 |
| 2 | 70 | Wall | architecture (0) | 2 | 13 |
| 3 | 73 | Wallpaper | architecture (0) | 3 | 15 |
| 4 | 74 | Stairs | objects (2) | 4 | 10 |
| 5 | 75 | Fireplace | objects (2) | 5 | 12 |
| 6 | 67 | Tree | outdoors (1) | 6 | 11 |
| 7 | 69 | Floor | architecture (0) | 7 | 16 |
| 8 | 71 | Door | objects (2) | 8 | 8 |
| 9 | 72 | Window | objects (2) | 9 | 9 |
| 10 | 76 | Roof | architecture (0) | 10 | 17 (paged: R131) |
| 11 | 77 | Hand | — (no catalog; the move-objects tool) | — | — |

Port flow today: BUILD SetMode -> Switcher.InitCategories(3 groups) ->
UIBuyBrowsePanel(group, Build) -> ChoosingSub subsort row (the R126 wide
plaques). New desktop flow: flat 12-tool row at arch-local (338,5) 45px pitch
(r144 section 4); tool click == clicking the corresponding subsort plaque
(InitSubcategory re-inits FullCategory to the tool's OWN catalog category via
MaskBit — the group distinction evaporates once a tool is chosen).

## Interest data in the port VM (VMPersonDataVariable)

46 Interest_TravelToys, 47 Interest_ViolenceAliens, 48 Interest_PoliticsPets,
49 Interest_SixtiesSchool (packed 2-topic words — low/high bytes?), 50
Interest_Weather, 51 Interest_Sports, 52 Interest_Music, 53 Interest_Outdoors
(single-topic words). No writer found (imported NBRS PersonData arrays carry
them if present; otherwise 0). UIText.iff carries NO interest topic names —
the panel is pictorial (icons + bars), same precedent as r127's tool plaques.

## STR# tables of note (UIText.iff walk, label ids)

240 'Relationship Panel Sort Button Tooltip Text', 241 'Inventory Panel Sort
Button Tooltip Text' (the Gift/inventory sort tooltips), 139 'BldTips' (tool
captions [0..10] + [11..15] spare), 154 'MiscStrings', 147 roofpanelstrs.
