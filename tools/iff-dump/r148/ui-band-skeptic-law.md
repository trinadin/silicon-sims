# R148 — UI "SOMETHING IS MISSING" DEEP-DIVE (skeptic audit + buy-band interaction chrome)

User report: "We still haven't nailed the UI. Something is missing. I'm not sure
what, you might recruit a skeptic and dive deep here." + a 386x194 crop of the
community-lot BUY band (plaque row + first grid column over PanelBack).

## 1. What the crop actually shows (identification chain)

1. Pixel census of the crop: essentially ZERO ThumbTemplate cell colors
   (64+24+280 of 74884 px) in the PLAQUE area, but the background palette
   matches **PanelBack.BMP** exactly — (0,0,74) top-left, (82,82,132)/(123,123,165)
   light zones, (0,0,82) navy right.
2. The band background is NOT kCatalogBck: `Res_CPanel.RT` maps kCatalogBck(48)
   -> `cpanel/Backgrounds/BuyBack.bmp`, but that file DOES NOT EXIST anywhere in
   game data (all 794 UIGraphics.far entries enumerated; re-extracted with full
   paths under `far-full/`). The PPC binary never loads res 48 — the visible
   band = **kUniversalCPBkg(4910) = PanelBack.BMP 804x100**, loaded by
   cWinCPanel::Init at 0x2701fc (`li r3, 0x132e`), and the port already mounts
   it (UIMainPanel.OriginalPanelBack). kCatalogBck is a vestigial RT entry.
3. Template-matching the crop's blobs against the actual plaque art tiles
   (`art-tiles/`) hits the CommunitySubsort family across all 8 functions —
   the crop spans the SUBSORT plaque block + the first grid column.
4. The plaque ART-CLASS LAW (magenta-key census, `orig-art/` + far-full):
   - MAIN plaques (BuyC*/BuyD*/BuyF*/BuyR*, ~124-128x38-42, 4 frames):
     **icon-only tiles on magenta** (~65% keyed). The original mains are BARE
     ICONS on PanelBack by design — the 4 frames are TINT STATES
     (0 gray default, 1 pressed, 2 cyan hover, 3 dim), not different art.
   - SUBSORT plaques (SubSortIcons/*, 144x36, 4 frames of 36x36):
     **fully opaque baked tiles** whose backing (107,107,153) deliberately
     blends into PanelBack's light zone.
   - Item GRID cells: ThumbTemplate.BMP 180x45 = 4 states of 45x45 +
     ThumbTemplate1Frame.BMP 45x45 (single) — navy border, light interior.
5. Conclusion: the composition the user cropped is structurally the ORIGINAL
   one (icons-on-PanelBack is the law, not a bug). What was actually wrong is
   the INTERACTION layer — below.

## 2. Skeptic audit (general-purpose agent, no images; code+law citations)

Ranked findings (all verified against the r145/r146 law docs):

| id | severity | finding | fix |
|---|---|---|---|
| P0-1 | user-visible | catalog popup never dismisses on mouse-out and LEAKS across BUY->LIVE/BUILD (mounted on MainPanel, never removed on Kill) | hover enable + exit disable + Kill detach |
| P0-2 | user-visible | **UIButton never grows the click Region's HEIGHT** — every band sheet button + catalog cell (built on a 1x1 white texture) had a **1px-tall hit strip**: the band was nearly unclickable by mouse | own the full rect in ctor/Remount |
| P1-2 | law | toggle plaque y=32; law = (100-artH)/2 with Back art 132x26 -> **37** | recompute after every mount |
| P1-3 | law | port upscaled every icon to 41px; engine ProductButton::FGBlt 0x20b960-0x20bb54 blits at NATURAL size (src==dest), halving pair-sheet widths (cols>1: ((w>>31)+w)>>1) | natural size, never upscale, fit down only past 41 |
| P1-4 | robustness | one-shot frame loader + swallowed exception = every grid cell invisible for the whole session; norm==null drew NOTHING | retry until mounted; navy fallback cell |
| P1-5 | law | popup anchor +45 right of the law (btnX-559, btnY-127) | drop the +45, clamp x>=0 (disclosed) |
| P2-6 | polish | tooltip text black; engine color RGB(31,124,31) at cWinCatalog Init 0x26b458 | TS1 green in UITooltipHandler (FreeSO submodule 1a977e88) |
| P2-7 | polish | no hover feedback anywhere on the band (art carries a distinct hover cell — mains go CYAN) | frame 2 on hover (sheet buttons + grid cells) |

Verified-correct (no action): plaque anchor tables (all 16 pairs), grid
geometry (13x2 @1024, (195,5) 45px pitch), page-arrow art/positions (36x49
sheets ARE 4x9x49 cells — engine slices the same), band background law,
expansion lock, tooltip strings wiring, STR#150 bases.

Also decoded: the popup does NOT use PopupInfo.bmp in the PPC Complete binary
(res 3006/3008 never LoadBuffer'd; BuildMyBuffer clears a fresh cTSBuffer) —
the port's flat panel stays; the exact clear color is a future decode.

## 3. Port changes

- `UIOriginalBandControls.cs` — UIOriginalSheetButton: full click rect +
  Hovered (frame 2); UIOriginalCatalogCell: full click rect + Hovered (frame 2
  of ThumbTemplate) + FitSize law (natural, no upscale) + navy fallback cell.
- `Catalog/UICatalogItem.cs` — OriginalFrameHover (ThumbTemplate frame 2);
  loader retries until all three frames mount.
- `UIOriginalBuyChrome.cs` — toggle Position re-derived from mounted art
  ((100-h)/2 -> 37 for the 132x26 Back sheets) after every remount.
- `UIBuyBrowsePanel.cs` — popup on cell HOVER (engine 0x13/EnablePopup law),
  hidden on exit/re-page, Kill() detaches it from MainPanel; anchor law
  abs.X-559 (clamp 0) / abs.Y-127.
- `FreeSO` submodule `1a977e88` — tooltip text color RGB(31,124,31).

## 4. Gate (89/89 PASS — r148-gate.txt)

New check **uibandlaw** (88 -> 89): FitSize law pins, frame mounts (45x45
normal/sel/hover), toggle (-2,37) with 132x26 art, full click regions for
plaques/toggle/arrows/first cell (reflection into protected ClickHandler),
hover wiring driven through UIButton's own MouseOver/MouseOut dispatcher
(cell.Hovered + popup enable/disable + anchor btnX-559), popup detach on Kill.

## 5. Residuals (disclosed)

- Engine hover law for cTSWinBtn frame mapping is art-inferred (0 up / 1 down /
  2 hover / 3 disabled per UIButton convention + art brightness census);
  SetState(1)-on-selection sites are decoded, the hover redraw site is not.
- Pair-sheet width halving (FGBlt cols>1 rule) not applied — our icons are
  single BMPs, not mirrored-pair sheets; disclosed.
- Popup slide-in ramp (250ms), can't-afford red tint, name—" "—price +4px
  advance, popup icon 1/3 scale: future polish (P2-8).
- Main-state expansion plaque highlight (State=1 on CurrentMain) is a port
  addition; decoded SetState(1) sites are subsort/build only (P2-9, ambiguous).
- Plaque enable/gray matrices (r146 §7) still unimplemented.
