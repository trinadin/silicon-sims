# R143 — the ORIGINAL Create-A-Sim flow (Pick-A-Family → Create-A-Family → Create-A-Character)

Round mandate (user): "Begin the next recommended round, covering as much ground
as efficiently possible utilizing Hermes." — R142's recommendation was the CAS
screen; three concurrent Hermes agents ran (A: CAS/CAF layout decode, B:
neighborhood+PickAFamily decode, C: client survey + art state decomposition),
plus an in-main-thread zodiac-table decode.

## Law docs (the round's canon)

- `cas-layout-law.md` — Create-A-Character + Create-A-Family: every anchor,
  SetImage(4,1) sheet, the LED law, the points bar, the Vita viewport, text
  field rects/capacities, colors, fonts (agent A).
- `nbhd-layout-law.md` — Pick-A-Family + the entire neighborhood screen
  (toolbar 14-button 50px grid, banner, lots, popups) (agent B).
- `client-cas-survey.md` — client-side inventory, the RLE8 load-path answer,
  autotest instantiation law, string tables (agent C).
- `cas-art-states.txt` — programmatic sheet decomposition of the whole family.

## The RLE8 unlock (FreeSO submodule, commit 8beea8e3)

237 of the 282 .bmp members the TS1 UIGraphics.far mount are BI_RLE8 — the
entire nbhd/CAS art family. Findings chain:

1. Simitone.Desktop's `BitmapReader` (ImageSharp 3.1.11) DOES decode RLE8 —
   but renders every pixel OPAQUE (agent C, verified in a dotnet harness).
2. The sheets' transparency is keyed by literal MAGIC-PINK palette ENTRIES
   (AddPersonBtn idx1 = 19,048 px, ScrollBar idx2 = 317 px), exactly the
   `ManualTextureMaskData` law (R>=248 && B>=248 && G<=4 → alpha 0) the loader
   already applies to the BitmapFunction path — which never ran for these
   files' alpha.
3. Palette index 0 is an ORDINARY NAVY FILL in this family (PickBkg is 66%
   idx0 and the original screen is opaque there) — my first idx0-transparency
   reading was WRONG and was corrected before commit (zero-overlap proof:
   magenta and idx0 never coincide; every "diff vs PIL" was exactly the idx0
   count).
4. `BmpRLE8.TryDecode` (new, FreeSO/TSOClient/tso.files/BmpRLE8.cs) runs
   FIRST in `WinDataFromStreamP`, decodes the RLE spec exactly (validated
   bit-identical to PIL on the corpus incl. delta-heavy PickBkg), and applies
   the magic-pink law at palette level. stb_image and GDI+ both reject RLE8
   outright (the no-BitmapFunction fallback path could never load these).

## The port (Client/Simitone/Simitone.Client/UI/Panels/CAS/UIOriginalCAS.cs)

All three screens rebuilt on the decoded tables, gated by
`!FSOEnvironment.SoftwareKeyboard` (mobile keeps the FreeSO panels unchanged):

- **UIOriginalPickFamily**: opaque PickBkg 800x600 CENTERED (nbhd law §1:
  offX = max(0,(winW-800))/2); family cards = FamilyPickItemBkg rows=3 states
  (690x68) 6 visible at (41,76); card internals name (20,0)/funds (526,21)/
  count (620,21)/member strip 45x68 at (110+i*48,14) wrap 494/smiley (657,19);
  text colors cyan (0,255,255) selected / white hover / (195,205,205) normal;
  PAF buttons Add 92x62 (322,530) / Delete 92x62 (414,530) / MoveIn 164x62
  (506,530) 4-state sheets; Cancel cTSSystemButton 121x62
  (139,529,260,591), caption STR#128[5], tooltip STR#128[15]; scroll arrow
  hotspots 26x23 at (586,42)/(662,42). PAFCancel art 5104 is a dead slot;
  the live Cancel button uses RT 5301.
- **UIOriginalDesignChar**: CreateACharBack 800x600 at WINDOW TOP-LEFT with
  the margin UNPAINTED (cas law §2 quirk — the engine anchors
  max(0,(art-win)/2) = 0, unlike PickFamily's centering); the 13-button table
  §3.1 (Adult 57x100 (497,101), Child 92x90 (406,111), Female 57x121
  (499,274), Male 98x111 (400,274), Dark 57x71 (499,202), Medium 46x71
  (452,202), Light 55x71 (396,202), body arrows 18x34 (580/729,291), head
  arrows 18x34 (597/714,160)); Done/Cancel = cTSSystemButtons on NbhdTileBtn
  provisionally centered at (111,529)/(400,529), then caption-sized to final
  rects (56,529,165,591)/(339,529,460,591) — the wide Done/Cancel plates
  5033/5034 are NEVER loaded by the engine (dead art, canon); name field
  (275,52)-(530,77) capacity 25; bio (22,425)-(783,521) capacity 2048; white
  text; labels 'CREATE A SIM' strip / 'PERSONALITY' centered x=198 y=114 /
  'Enter First Name:' right-anchored x=255 / 'BIO'.
- **THE PERSONALITY PANEL**: 5 trait rows at (228,131+32i), caption
  right-aligned at LED.x-14; each trait = the 59x25 PersLED strip with
  LIT WIDTH = value*6 (clamped 59; the 10th pip is 1px short) while the CLICK
  ZONES split at value*(59/10)=5 px (draw/click pitch mismatch = engine
  quirk, kept); values 0..10; POOL 25 DOWN-counter; plus-zone denied (deny
  sound) at pool 0; sounds UI_CAC_personpts / UI_CAC_CycleParts /
  UI_CAC_Cyclehead via the string-based HIT API; remaining-points bar =
  CACPointRemainingBars 149x25 at (117,342), width = pool*6.
- **THE ZODIAC READOUT** (points button (129,290)-(249,310), tooltip
  'Astrological Sign'): caption "(Sign)" whenever pool < 25. The engine's
  ComputeZodiacSign @0x172170 decoded this round in-main: nearest archetype
  by squared Euclidean distance over the five traits; the 12-row table read
  straight from data 0x4c144 (Aries 5,4,8,5,3 … Aquarius 3,7,3,5,7; Pisces =
  all-zero row → an unassigned sim is a Pisces); names from UIText STR#
  'signs' (catalog 7, indices 0..11).
- **UIOriginalDesignFamily**: DsgnFamBkg top-left; Add 139x103 (593,103) /
  Delete 149x99 (594,208) / Edit 137x99 (594,311); Done/Cancel system buttons
  with final rects (145,529,254,591)/(539,529,660,591); family name
  (274,50)-(529,75) capacity 24
  (engine filters file chars); 8 member slots 85x105 vertical-first grid
  (111,121)-(551,481) pitch (120,140), portrait at (15,10), name at the slot
  bottom; enabling laws (Add iff count<8 && name non-empty; Delete/Edit iff
  selected). The engine parks the 'Enter Last Name:' label OFFSCREEN-LEFT
  (quirk) — kept canon.
- **TS1CASScreen wiring**: desktop constructs the three original screens and
  never the mobile Back/Accept buttons; PrepareEdit/BuildMember/SaveFamily/
  ClearFamily/Update/SetFamilies all branch on Original; the Vita window
  ((618,145) 100x220, engine rect) is cut as an alpha hole in the background
  with ONE full sim (chosen head + chosen body on a single avatar — the
  original's independent head/body cycling) standing in the view; the mobile
  ring carousel is hidden on desktop.

## Gate

New check **uicasorig** (85 total, all PASS): RLE8 art dims (8 members), the
TRANSPARENCY LAW pinned by pixel probes (PickBkg (0,0) alpha 255 — idx0
opaque; AddPersonBtn (0,0) alpha 0 — magic-pink key), the full DesignChar
anchor+sheet-width table, field rects/capacities, system-button positions,
LED rows + AdjustTrait pool accounting + lit widths (3→18px, clamp 10→59) +
points bar width (22→132px) + deny-at-zero, the zodiac table (Aries/Taurus/
Pisces probes), DesignFamily anchors + the 8-slot grid arithmetic, PickFamily
anchors. Evidence dumps: `dumps/uisurvey-casorig-{cac,fam,paf}.png` (1024x768,
art top-left per engine law, margins showing the lot = the unpainted-margin
quirk visible, hole cut verified by pixel variance).

## Disclosed interpretations / residuals

- Vita camera constants (VitaCenterTile/VitaZoom/VitaWorldPos/VitaFacing) are
  CALIBRATED FIRST-CUT, not engine-decoded — the engine's own camera
  keyframes live in cWinVitaBtn/cDDDSimsView internals (residual). The
  engine RECT is canon; the sim placement inside it needs a live look.
- **Later correction (R183):** `cTSSystemButton` is now decoded through its
  native paint and interaction paths. Its states are default/pressed/hover/
  disabled; caption colors are #C3CDCD/#00FFFF/#FFFFFF/#405D5F, pressed text
  moves (+2,+2), the art uses 45/1/45 cap/stretch/cap painting, and activation
  occurs only on mouse-up inside. See `../r183/r183-cas-system-button-law.md`.
- Member portraits on PAF cards/family slots use the client's 3D headshot
  renders (UIIconCache); the original uses GetPictureBuffer person sheets
  (4x3, offset (15,10)) — sourcing those buffers is a future round.
- Fonts: engine font indices 8/10/11/12/14/16 map to our .ffn tables by role
  (caption/bold/small); the cITSFontSys index→resource mapping is decoded as
  a TABLE (cas-layout-law.md §6) but our assignment per element is disclosed.
- Text fields are UITextBox/UITextEdit with white text on the engine rects;
  the original glyph fonts don't drive editable layout (disclosed).
- The hidden "body strings cheat" bio field (flag 0x945b1, parked (618,-55))
  is canon-decoded but not ported (flag writer unknown).
- PAFCancel art 5104 and the 5033/5034-adjacent wide plates, HotspotPopup
  5101/5102, SimEstates 5015, Marquee 5016: dead slots in The Sims Complete
  (no code references). The live PAF Cancel control is the RT 5301
  cTSSystemButton decoded in R183.
- Neighborhood screen itself (toolbar law fully decoded in nbhd-layout-law.md
  §3) untouched this round — R144 candidate.
