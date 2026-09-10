# R141 — Main-game UI/UX round: the control panel on original engine art

Round mandate (user): "focus heavy on the overall UI/UX... Duplicate icons. Things in
the wrong spot. Existing Simitone artifacts... the game is unplayable."

## Canon recovered this round

### 1. Res_CPanel.RT / Res_CPanel.h — the ORIGINAL gadget id → art map
`UIGraphics.far` members `Res_CPanel.RT` (27,327 B) + `Res_CPanel.h` (15,800 B) are the
engine's own resource templates: every `{RLEBMP,kXxx,"CPanel/..."}` line maps a gadget id
to its bitmap. Key rows (full copies: `art/Res_CPanel.RT`, `art/Res_CPanel.h`):

| id | constant | art | sheet |
|----|----------|-----|-------|
| 2037 | kUniversalBkgTGA | Backgrounds/UniversalBack.tga | 220x183 (32-bit TGA) |
| 2010 | kPeople | Buttons/people.bmp | 200x50 = 4x 50x50 |
| 2008 | kObjects | Buttons/objects.bmp | 180x47 = 4x 45x47 |
| 2003 | kArch | Buttons/arch.bmp | 156x39 = 4x 39x39 |
| 2009 | kOptions | Buttons/options.bmp | 92x23 = 4x 23x23 |
| 2013/2014 | kZoomIn/kZoomOut | Buttons/zoomin/zoomout.bmp | 108x27 = 4x 27x27 |
| 2012/2011 | kRotLeft/kRotRight | Buttons/rotleft/rotright.bmp | 108x27 = 4x 27x27 |
| 2026 | kPause | Buttons/pause.bmp | 60x30 = 2x 30x30 |
| 4705/4706/4707 | kSpeed1/2/3 | Buttons/Speed1/2/3.bmp | 44/80/128x15 = 4x 11/20/32x15 |
| 2025 | kLevelRoof | Buttons/LevRoof.bmp | 88x19 = 4x 22x19 |
| 2006 | kNoCut | Buttons/nocut.bmp | 84x21 = 4x 21x21 |
| 2004 | kDynaCut | Buttons/dynacut.bmp | 84x21 = 4x 21x21 |
| 2007 | kNoWall | Buttons/nowall.bmp | 88x15 = 4x 22x15 |
| 2024/2023 | kLevel2/kLevel1 | Buttons/Lev2/Lev1.bmp | 88x15 / 84x11 = 4x 22x15 / 21x11 |
| 4803/4804/4805/4806 | kLiveModePatch/... | Backgrounds/Live/Buy/Build/OptionsPatch.bmp | 213x153 / 156x148 / 107x132 / 37x100 |
| 4910 | kUniversalCPBkg | Backgrounds/PanelBack.bmp | 804x100 (all-mode toolbar back) |
| 4502 | kHouseBtn | Buttons/House.bmp | 108x30 — the HOUSE TAB, not the live-mode button |

**kPeople (people.bmp) is the live-MODE button art.** The port had used House.bmp there
since R86 — this round corrects it on both desktop (UIDesktopUCP) and mobile
(UILiveButton), and the uitoolbar gate pin is updated to the corrected law (200x50
sheet, self-cropped by UIButton's 4-state convention) — disclosed correction, not a
weakening: House.bmp remains pinned as the House tab (kHouseBtn).

### 2. Symbol index — 7,717 functions (`symbol-index.txt`)
CW trailer format cracked this round: `[u32 0][u32 unitId 0x00092040/1][u32 flags]
[u32 size][u16 len]['.'+name]` follows each function body directly; function start =
nameAddr − 18 − size (+2 slop; verified against R140's InitBitmapped 0x4b3f00).
Indexed to `symbol-index.txt` — the whole UI class family is now addressable:
`cWinCPanel` (Init 0x270170), `CPState` (0x20c643+), and the control-panel builder:

### 3. cWinViewControl::Init (0x2b5f30, 8,288 B) = THE UCP builder
Decoded (`r141-viewcontrol-init.txt`): loads exactly kUniversalBkgTGA, kPeople,
kObjects, kArch, kOptions, kZoomIn/Out, kRotLeft/Right, kLevel1/2, kLevelRoof, kNoCut,
kDynaCut, kNoWall, kPause, kSpeed1/2/3, the four mode patches, and panel templates
100/380/480 (via `bl 0x591440`). Gadget rects are read from TSUI TEMPLATE objects
(resource ids 100/380/480) — **that database is not yet decoded**; the in-plate button
positions in the port are therefore a DISCLOSED art-anchored interpretation (plate
220x183 flush bottom-left; camera diamond; wall/floor row in the plate's measured dark
bottom recess 45..195 x 125..182) pending the template decode. Art is byte-canon.

## Survey (the user's view, made measurable)
New `uisurvey` check: stabilizes the HUD in 3 states (live panel open / UCP island /
buy catalog), renders the CURRENT screen explicitly (the old uidump only drew the
screen-manager stack — the loading screen — a measurement bug found and fixed this
round; UICachedContainer children additionally need the PreDraw pass), dumps PNGs +
an absolute-position visible-tree.

Findings fixed (see dumps/ for before/after):
1. **Motive bars 3-8 OFF-SCREEN** — the 4x2 grid at 180px pitch put columns 3-4 at
   x=1068/1248 on a 1024 screen. Fixed: 80px bars at 105px pitch (4x2 within the
   533px usable subpanel width).
2. **Two speed clusters at once** — UCP pills + a static, non-clickable
   OriginalSpeedStrip image. Fixed: ONE cluster at the toolbar's right end with the
   original kPause/kSpeed1-3 art (pause 30x30; speeds 11/20/32x15).
3. **Double category icon** — `>= 720px` clause re-added the ACTIVE category stencil
   over the main plaque on every desktop screen. Fixed (condition removed).
4. **Neighborhood switcher leak (autotest path)** — five 96x96 edge buttons stayed
   mounted in-lot when PlayHouse got a null switcher. Fixed (tracked field + teardown).
5. **Loading-screen chrome permanent in-lot** — GameController.EnterGameMode
   transferred the loading screen's PROGRESS BAR + SPLASH TIP into the game screen
   forever (UIOriginalLoadBar at (195,708) drawn over the lot). Fixed (transfer
   skips the bar + tip classes).
6. **The desktop UCP was entirely port chrome** — d_live_bg.png 292x281 plate, ~20
   d_live_* stencil buttons, speed pills, friend-count icon+label, eyedropper button,
   panel-hide X, catalog search box, diagonal-cut stripe. ALL removed/replaced: the
   UCP is now UniversalBack.TGA (via the byte-faithful png twin `orig_ucp_back.png`
   — the FAR texture resolver covers .bmp only) + per-mode patches + original art
   buttons; clock/funds .ffn twins moved beside the speed cluster at the toolbar's
   right end (original composition); the eyedropper tool survives key-only (E).
7. **UIButton frame convention** — UIButton self-crops 4-state sheets
   (m_Width = Width/ImageStates): mode buttons mount the FULL sheets; UIStencilButton
   (ImageStates=1) mounts Frame/FrameW crops. FrameW added to UIOriginal for sheets
   that don't divide Width%Height==0 (pause 60x30, Speed1-3, Lev1/Lev2/LevRoof,
   nowall, nocut/dynacut 84x21).

## Gate
- NEW `uicp`: plate 220x183 @ (0,SH-183); 4 patches canon dims + exactly-one-visible
  per mode; every button's original frame dims; speed singularity (pause+3 at toolbar
  right end, UCP pills compile-gone); artifact absence (no hide button, no search
  box); motive bars on-screen.
- `uisurvey`: evidence harness (3 dumps + tree), PASS on completion.
- CORRECTED `uitoolbar`: live-tab 200x50 people.bmp sheet (was 108x30 House.bmp —
  the codified misuse). `uidtips`: speeds asserted at the toolbar cluster; eyedropper
  button absence is compile-level.
- Everything else untouched; full-gate run r141p1 (see r141p1-gate.txt).

## Residuals (disclosed)
- TSUI template database (panel ids 100/380/480) undecoded → UCP-internal button
  positions are art-anchored interpretation, not engine rects. Next-round target:
  decode 0x591440's template store.
- The patches mount over the plate's dial area; the engine may compose them
  differently (e.g. right of the plate). Same disclosure class.
- Toolbar right-end cluster placement (pause/speeds/clock/funds) is interpretation.
- Mobile HUD untouched this round (only the live-button art corrected).
