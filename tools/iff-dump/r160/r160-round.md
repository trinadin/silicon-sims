# R160 — the neighborhood NAVBAR on the engine law (UI-100% round 2)

Round mandate: replace the mobile neighborhood switcher (floating
`ngbh_*.png` elastic buttons + `ngbh_outline.png` 9-slice frame + the
Simitone gradient backdrop) with the ORIGINAL navbar, on the R143-decoded
cWinNeighborhoodVC/UL::Init layout law.

## What shipped

1. **UINeighbourhoodSwitcher REWRITTEN** as the engine navbar
   (Client/Simitone/Simitone.Client/UI/Panels/UINeighbourhoodSwitcher.cs):
   - kNghBarBkg banner (800x52) mounted as a clickable 1x1 sheet button at
     (0,0); banner_downtown on mode 2.
   - The 14-slot toolbar law (r143 §3.1/§3.2, anchor table BSS 0x94698 +
     resource table img 0x6ce40): MoveIn(200,0) Bulldoze(300,0) Exit(600,0)
     Inet(762,0) Credits(41,543 — the English language-switch anchor,
     CreditBtnEn 232x58) Previous(106,0) Next(141,0) Import(250,0)
     Downtown(400,0) Logo(5,0 SetImage(1,1)) Vacation(450,0) Rezone(350,0)
     Studio(500,0) Magic(550,0) — 50px main-row pitch, per-state cells
     W/cols x H/rows (53x52 / 39x52 / 26x52 / 58x58 / 70x52).
   - kNghCurrent indicator (23x30) at (124,3) on home modes; screen title
     (UIOriginalText) at (152,19), right of the indicator (the engine's own
     banner name slot kMarquee 5016 is DEAD in The Sims Complete, r143 §3.5 —
     placement disclosed).
   - Tooltips from STR# 151 (English block 0..13; [13]='' = the logo, no
     tooltip). Destination modes swap in their own tables: 169/170/173/174,
     entry 0 'Return to Neighborhood View'.
   - Destination modes mount the home grid minus the 5 home-only slots minus
     the current destination, plus a Return button (per-screen Return.bmp:
     Downtown 5209 / Studiotown 5217 / Magicland; NghUI\Return.bmp for
     Vacation — no VIsland Return exists in the FAR, disclosed) at the
     MoveIn slot (200,0). Per-screen Import/Bulldoze art swap in
     (Downtown\Import.bmp etc.; no VIsland Bulldoze — NghUI art, disclosed).
   - Wiring: MoveIn = the port CAS entry ('Select or Create Family', the
     old ngbh_cas behavior via UITransDialog+EnterCAS); Exit = the R121
     options Quit flow (TS1GameScreen.CloseAttempt → STR# 153 save/quit
     dialog); destination buttons PopMode; moveIn mode hides MoveIn +
     destinations (Quit stays reachable). Bulldoze/Import/Inet/Previous/
     Next/Rezone/Credits/logo/banner mount engine-exact with tooltips and
     no-op clicks (evict/bulldoze already lives on the lot popup
     UIHouseSelectPanel.cs:332-339; no multi-hood/import/web/rezone/credits
     systems in the port yet) — every no-op logs `uinav:`.
2. **UIOriginalNavbarButton** (same file): UIButton subclass drawing one
   cell of a cols x rows sheet whole (engine SetImage law). NOTE: the
   toolbar sheets are 212x52 — W%H != 0 — so UIOriginal.Frame() cannot crop
   them; cells crop by cols directly. Reuses UIButton's hover/press state
   machinery; ForceState=0 for the single-state logo.
3. **ngbh_outline.png RETIRED** from UINeighborhoodSelectionPanel
   (PopulateScreen no longer mounts the 9-slice shadow frame — the original
   screens are the raw 800x600 artboard). The panel's R96 title block moved
   to the navbar.
4. **Modern gradient KILLED** behind the neighborhood: TS1GameScreen now
   constructs `UISimitoneBg(loadModernFallback: false)` and mounts the
   engine surround — kLargeMask 5001 = Downtown/LargeBack.bmp (1024x768),
   the r143 §1 law (windows wider than the artboard load the surround;
   cover-fit, disclosed). UISimitoneBg.Draw now scales from the art's own
   dims (was hardcoded 1136x640 for the Simitone gradient — behavior
   identical for every existing caller).

## New decode facts this round (all in this directory / r143 tools)

- **STR# 151 'NghbBtnTips'** = the navbar tooltip table, 280 entries =
  20 languages x 14; English block = engine toolbar strings verbatim
  (r160-navstrings-canon.txt, chunk sha pinned in the gate).
- **STR# 169/170/173/174** = the destination screens' own tooltip tables:
  [0] Return, [1] Exchange (web), [2] Credits, [3] Import, [4] Bulldoze
  (Magicland adds [5] 'Select or Create Family') — this IS the destination
  toolbar button set, matching the per-screen Return/Import/Bulldoze/
  TheSimsLogo art in UIGraphics.far.
- **STR# 163 'credits'** dumped (65 entries, the credits roll) and **STR#
  225** (the expansion-missing failure tooltips) confirmed — neither is
  needed with Complete data; 225's engine consumer is the 0x25f440
  LoadUIString mechanism (see below).
- **0x87f60 identified** (jump-table `get packed string by id` returning
  pointers into a TOC-0x5be8 blob; ids 0..0x5f) and **0x25f440
  identified** (LoadUIStringIntoLabel: builds a CTGString from the blob
  string + registers it under a string-set id — VC::Init calls it with
  sets 225 AND 151 for the banner button labels @0x474424-0x474464). The
  exact banner tooltip text source remains unresolved (r143 work-log
  residual kept); the port mounts no banner tooltip.
- **cWinDowntown::Init partially scanned** (r160-dt-init-notes.md): the
  function builds the FILTER toolbar (5-button loop @0x3ffd78-0x3ffee8,
  this+0x164 stride 4, anchor pairs at r30 stride 8, language-law
  LoadUIString 0xa9=169) + two winW>800 adornment buttons (this+0x180/
  +0x17c from the kDTDlgBkg2/kFilterToolbar buffers). Its main-screen
  return control is the PAYPHONE (kDTPhone 5426) — decode deferred to the
  expansion rounds per the campaign scope.
- **FAR inventory vs RT template gaps** (r142/art re-extraction, pruned
  after inspection): NghUI_Return.bmp, Magicland_largeback.bmp,
  Magicland_Bulldoze.bmp, VIsland Import-only, Magicland_GoNghButton/
  GoDTButton/OptDowntown families exist in UIGraphics.far but not in the
  Res_Nbhd.RT template rows r142 pinned — the FAR is the ground truth for
  the mount (the gate pins name+dims through TS1Global, which serves the
  FAR).
- **Banners are text-free** (pixel-variance scan of NghUI_banner_*
  .bmp — clean diagonal gradients, no baked wordmark), confirming the
  name text is engine-drawn (dead kMarquee slot).

## Gate (uinav, the 97th check)

- STR# 151 chunk sha256 4ff94459…daf9 + English block 0..13 verbatim;
  169/170/173/174 [0] == 'Return to Neighborhood View'.
- 24 art members pin name+dims (14 toolbar sheets, 2 banners, Current,
  CreditBtnEn, 4 Return sheets, Downtown Import/Bulldoze, LargeBack).
- EngineLaw table == the r143 constants (member/x/y/cols/tip/idx x14).
- LIVE: headless strips (Panel=null) — home mode 4 (14 buttons at law
  anchors, engine cell sizes, disk tooltips, banner + Current + title
  'Old Town'), mode 2 (banner_downtown, 9 buttons, per-screen art,
  Return at (200,0) w/ 169[0]), moveIn lock (MoveIn + destinations
  hidden, Exit reachable). strips=3 buttons=32.
- Runs inside RunCorpus (the corpus-family method) — targeted runs must
  pass `-autotest-opts "uinav,corpus"` (learned this round: opts=uinav
  alone never enters RunCorpus; same applies to every UI check in that
  block).

## Honest log

- r160-targeted-run1.log artifact kept: opts=uinav alone → SUMMARY 0/0
  (check never reached) — the RunCorpus discovery above.
- Build iteration artifacts (not kept): a `head -10` on the publish pipe
  SIGPIPE'd dotnet mid-build → stale binary ran as run2-era gate (0/0);
  caught by strings-checking the DLL for the check name before rerunning.
- Full gate: 97/97 PASS, clean exit chain (after Run / after Dispose /
  AUTOTEST_WRAPPER_EXIT=0), carseek intact (minute=6:41 natural clock),
  uibrand intact. Dist DLL byte-match: Simitone.Client 64586f92…, FSO.
  SimAntics 903db62a… (unchanged), FSO.Client d8d1f8d2….

## Residuals (disclosed, not pinned)

- Banner click = no-op (engine opens the credits/picker; no credits
  screen in the port — campaign item 4 territory). Banner tooltip text
  source (0x87f60 blob) unresolved.
- Destination screens mount the SHARED neighborhood grid (banner + 50px
  row) rather than their own engine toolbars — the DT/Vacation/Studio/
  Magic native strips (filter toolbar + payphone etc.) are expansion
  systems, deferred with their screens per the campaign scope.
- Previous/Next mount engine-exact but no-op (single neighborhood in the
  port). Import/Inet/Rezone no-op (systems absent). Credits button no-op.
- Vacation (mode 3): NghUI Return glyph + NghUI Bulldoze (no VIsland
  members in the FAR); banner_neighborhood on 3/5/7 (no dedicated banners
  exist in Res_Nbhd).
- The surround cover-fits (the original only ever ran <=1024x768 with a
  centered artboard; the port scales the artboard to window height).
