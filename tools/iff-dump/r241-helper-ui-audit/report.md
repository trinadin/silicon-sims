# R241 helper audit — non-tutorial, non-PIP base-game UI (Assignment C)

Scope: broad base-game UI evidence audit over the retained r240 captures
(`build/ui-audit/r240/{default,800x600,800x600@2x,1280x800}` plus logs), traced
into maintained source and the owner-local original corpus. Excluded per
assignment: tutorial UI, guided tutorial, PIP composition, the corrected Needs
strip, and the options "survey" fixture's content. All analysis scripts referenced
below live in this directory and are read-only on captures/game data.

Capture-to-log map (which run shows what):
- `default/` = 1024x768 logical; full survey set + `uisurvey-dialog*.png`
  (dialog fixtures only exist here) and no `uisurvey-audit-*`.
- `800x600/`, `800x600@2x/`, `1280x800/` = size runs; add `uisurvey-audit-*`
  (options, phonebook, scrapbook, budget, help) and have no `uisurvey-dialog*`.
- The `casorig-*` dumps are constructed in isolation by `CheckUICASOrig` step 8
  (`AutotestRunner.cs:21802-21823`): the static panels are attached to whatever
  screen is current (the live lot), drawn via `SurveyDump`
  (`AutotestRunner.cs:18984-19043`, gray 0x72 clear + world + full UI tree), and
  detached. The live lot/HUD visible around them is a fixture artifact, NOT
  product flow. The real-flow CAS screen is never captured in any retained set.

---

## Findings (ranked by confidence x user impact)

### 1. VERIFIED (compositing defect) — CAS "Vita" preview window washes the scene with the background gradient (CutHole emits invalid premultiplied pixels)

- Observation: in ALL FOUR capture sets, `uisurvey-casorig-cac.png` shows a flat
  mauve rectangle in the Create-A-Character preview window (art rect
  (618,145) 100x220): mean RGB ≈ (109,91,117), reproducing identically at
  1024x768, 800x600 and 1280x800. The original art
  (`UIGraphics.far :: cpanel\Backgrounds\CreateACharBack.BMP`, 800x600 BI_RLE8;
  faithful extraction `tools/iff-dump/uigr-png/0106_CreateACharBack.png`) paints
  that window blue, mean (33,39,100).
- Mechanism (proved offline, `palette_test.py`, `diff_art.py`, `rle_sim.py`):
  the capture hole pixels group by the art's own palette indices with
  intra-index std 5.6, and capture ≈ art RGB + live-world RGB (art navy
  (33,41,99) + terrain (76,51,17) = (109,92,116) = capture). That is exactly
  MonoGame premultiplied `AlphaBlend` (src=1, dst=1−α) applied to pixels whose
  alpha was zeroed but whose RGB was left intact: result = artRGB + world.
  The BmpRLE8 decoder itself is innocent — a byte-exact simulation of
  `FreeSO/TSOClient/tso.files/BmpRLE8.cs` on this member matches PIL 0.0
  (`rle_sim.py`).
- Source: `/Users/nathannoom/Developer/Games/The Sims/simitone-fork/Client/Simitone/Simitone.Client/UI/Panels/CAS/UIOriginalCAS.cs`
  - `CutHole` lines 85-94: `data[y*Width+x].A = 0;` keeps RGB.
  - Used only at line 619 (`UIOriginalDesignChar` ctor) to punch `VITA_RECT`
    (line 554, `Rectangle(618, 145, 100, 220)`), the cWinVitaBtn preview window.
- Impact: in the real Create-A-Sim flow (`TS1CASScreen.cs:914-945` renders
  `VitaPreview` behind the punched window), the 3D preview Sim is seen through an
  additive superimposition of the painted blue placeholder — a mauve/blue wash
  over the Sim instead of the original clean view. This is a maintenance-code
  compositing bug, independent of the fixture (the fixture merely made it
  measurable by putting terrain behind the panel).
- Reproduction for the primary chat:
  1. Existing evidence: open `build/ui-audit/r240/default/uisurvey-casorig-cac.png`
     and the same file in the other three sets; the window region (628..708,
     155..355) is mauve in all. Compare with `tools/iff-dump/uigr-png/0106_CreateACharBack.png`
     (blue) and with the same coordinates in `default/uisurvey-buy.png` (terrain).
  2. Live repro: launch the desktop build, enter the neighborhood, choose
     Create-A-Family, add/edit a Sim; the preview window at art (618,145)
     shows the washed/tinted preview instead of a clean Sim.
  3. After any fix, rerun the r240 default run (`run-final-validation.py`) and
     re-check the hole pixels: they should equal the underlying scene exactly
     (in the fixture dump: identical to the world pixels at the same coordinates
     in `uisurvey-buy.png`).
- Proposed minimal fix (description only): make `CutHole` write fully
  premultiplied-transparent pixels — set RGB to 0 together with alpha
  (`data[..] = new Color(0,0,0,0)`) — so the punched region contributes nothing
  under premultiplied blending. No other callers of `CutHole` exist.

### 2. VERIFIED (observation) / INFERENCE (original >1024 law) — bottom band chrome is a fixed 1024px composition; at window widths >1024 the band stops mid-screen with unpainted world to its right

- Observation: `1280x800/uisurvey-live.png`, `1280x800/uisurvey-audit-options-graphics.png`
  and the band background in `1280x800/uisurvey-build.png` all end at x ≈ 1018-1024
  (`band_edge.py`: rightmost navy band pixel x=1018 at BOTH 1024x768 and
  1280x800), leaving a hard vertical edge with live terrain visible in the
  bottom-right corner (x≈1024..1280). In BUILD/BUY the band's CONTENT (tool/item
  cells) extends to the window edge and floats over terrain beyond the backdrop.
  At 1024x768 and 800x600 the band reaches/clips at the viewport edge, so the
  default-size captures look correct and this only shows in the 1280 run.
- Source: `/Users/nathannoom/Developer/Games/The Sims/simitone-fork/Client/Simitone/Simitone.Client/UI/Panels/UIMainPanel.cs`
  - lines 270-282 (`OriginalPanelBack` mount; `SetSize(804, 100)` fixed in the
    `Game.Desktop` branch at line 279, comment lines 275-277) and the same fixed
    size re-asserted at line 1479 in `GameResized()`. 220 (UCP) + 804 = 1024.
  - Related width law: `UISubpanel.cs:39-48` `NativeDesktopPeopleWidth` expands
    People panels 280→504 only when `screenWidth == 1024`.
- Original evidence: `tools/iff-dump/r144/toolbar-law.md` §0 (from
  `cWinCPanel::Init` 0x270170 disassembly): every child area is
  `SetArea#1(child, l, t, l+(W-220), t+H0)` with W = cpanel width, and the child
  screen rects are tabulated as formulas "(220, SH-100, W, SH)" — i.e. the band
  spans x 220..W at the window width; 1024 is only the example. Counterweight:
  the same doc records a `GetBuffDims()==0x400` (1024) surface check at 0x28f9f4,
  so the original may cap its people surface at 1024; the exact >1024 blt
  semantics (stretch vs tile vs cap) are not pinned by prior rounds.
- Impact: any macOS window wider than 1024 (common on modern displays) shows the
  band breaking off mid-screen in live/build/buy/options modes.
- Reproduction: rerun a size audit with `width=1280` (see
  `build/ui-audit/r240/config-before-size-audit.ini` for the size-run config
  method), enter live mode, and inspect the bottom-right corner; or simply open
  `build/ui-audit/r240/1280x800/uisurvey-live.png`.
- Proposed minimal fix (description only, AFTER executable confirmation):
  extend the band chrome to `Game.ScreenWidth` (stretch the PanelBack center or
  tile it), matching the §0 child-area law; keep the UCP plate and landmarks
  1:1. If the executable investigation shows the original caps at 1024 instead,
  record that law and close this finding.
- Label rationale: the maintained-build behavior is VERIFIED from captures; the
  parity claim is INFERENCE pending the original's >1024 behavior being read
  from the executable (cWinCPanel::TSPaint 0x26f510 / child Init 0x2704fc et al.).

### 3. MEDIUM / evidence-quality — a stray production "Magic Town" picture dialog with an EMPTY body appears mid-survey in all three size runs and contaminates several retained artifacts

- Observation: `800x600/uisurvey-build.png`, `800x600/uisurvey-magic-main.png`,
  `800x600/uisurvey-roof.png` and their 1280x800 twins show a centered modal
  picture dialog: wizard-hat image, title "Magic Town", OK button, NO body text.
  In the default run no retained capture shows it. Log timeline
  (`size-1280x800.log`): absent through `buy-query-hidden` (00:19:36.964),
  present at `build` (00:19:38.252) — it appears between fixture steps with no
  log marker; the next fixture (`uiaudit`) reports `tracked=2` dialogs versus
  `tracked=1` at uisurvey start (line 802), confirming a real dialog object was
  created mid-survey. The dialog then persists through build/wall/roof/
  expband/studio-main/magic-main dumps.
- Source: trigger not identified from captures. Candidate production surfaces:
  the neighborhood destination routing (`UINeighbourhoodSwitcher.cs:386`,
  `UINeighbourhoodSelectionPanel.cs:607`, art `NghUI\MagicLand.bmp` mounted in
  the size logs), or a VM/object-initiated dialog on the survey lot. The
  build-mode fixture itself only calls `ArchChrome.SelectTool(...)`
  (`AutotestRunner.cs:19807-19852`).
- Impact: (a) several retained size-run artifacts are not clean single-state
  observations; (b) IF this dialog is reachable in play, an empty body may be a
  real string/plumbing gap versus the original (original picture dialogs carry
  body text; cf. the IFF-literal `uidialog` gate's 6-line body law in the logs).
  Neither can be confirmed from the retained set.
- Reproduction: rerun any size run (`run-final-validation.py` size mode) and
  watch the frame between the `buy-query-hidden` and `build` survey dumps; or
  click through build tools on the survey lot in a 800x600/1280x800 window until
  the dialog appears, then inspect its text source.
- Proposed follow-up (description only): identify the dialog's creator (log
  dialog construction with a stack tag), then either fix the trigger or give the
  survey fixture an answering/suppression step for it so future artifacts are
  clean; separately verify the original dialog's body string for "Magic Town"
  travel/visit confirmations before treating the empty body as a product gap.

### Checked and NOT gaps (so the primary chat does not re-chase them)

- Buy catalog "blank tile": disproven. The suspicious light tile in
  `default/uisurvey-buy.png` row 0 has textured content (std ≥ 8 in every 45px
  cell; `find_blank.py` found zero uniform cells). No empty catalog cell exists.
- Catalog popup caption "Name$153" run-in: the code draws the price 4px after
  the measured name per the documented engine law
  (`UIOriginalBandControls.cs:359-366`, "BuildMyBuffer advances exactly four
  pixels"); consistent with the original single-line composition. Not a gap on
  capture evidence.
- "Paused" top-left label: intentional original behavior
  (`UI/Controls/UIOriginalPauseLabel.cs`).
- Empty `uisurvey-ucp.png`: intentional "State B: UCP island alone (panel
  closed)" dump (`AutotestRunner.cs:19602-19611`).
- `casorig-paf` empty family list and the lot/HUD visible around `casorig-*`
  panels: fixture artifacts (panels constructed and dumped in isolation,
  `AutotestRunner.cs:21802-21823`; `UpdateFamilies` never called on the dumped
  `UIOriginalPickFamily`).
- BmpRLE8 decoder on `CreateACharBack.BMP`: byte-faithful (simulation matches
  PIL 0.0), despite the unusual absolute-run padding law in
  `BmpRLE8.cs:118` (`if ((p & 1) == 1) p++;` pads on stream-position parity
  rather than odd `n`). Worth a unit check on other members one day, but no
  observed artifact here.

---

## Original addresses / resource IDs relied on

- `UIGraphics.far :: cpanel\Backgrounds\CreateACharBack.BMP` (= uigr 0106,
  `Nbhd\CreateACharBack.BMP` = uigr 0543, identical): 800x600, 8bpp BI_RLE8,
  `bfOffBits=1078`, clrUsed=256; Vita window inner (628..708, 155..355) mean
  (33,39,100). Extraction: `tools/iff-dump/uigr-png/0106_CreateACharBack.png`.
- `cWinVitaBtn` window rect law (618,145,100,220) — maintained at
  `UIOriginalCAS.cs:554`, attributed to cas-layout-law §3 (r143).
- `cWinCPanel::Init` 0x270170 / `__sinit_:WinCPanel_cpp` 0x270e10 (BSS 0x93230 =
  {220,50}), `cWinCPanel::TSPaint` 0x26f510 (kBackPatch at cpanel-local
  (168,50)), child vtable slots; band child screen rects as formulas in W:
  `tools/iff-dump/r144/toolbar-law.md` §0. cWinPeople surface check
  `GetBuffDims()==0x400` at 0x28f9f4.
- STR tables cited by the code under test: 128 (PAF), 129/130 (family/character),
  134 (neighborhood text), 147/150/151 (build/buy tooltips), 154 (band buttons),
  164 (zodiac).

## Non-assessable areas (blind spots of the current captures)

1. Real-flow original CAS (`TS1CASScreen` with `Original=true`): no retained
   capture exercises the production screen — the `casorig-*` artifacts are
   isolated panels. Finding 1's real-flow visuals follow from the blend math but
   were not photographed. A dedicated capture entering CAS via the
   neighborhood→family→add-Sim flow is needed.
2. Real-flow Pick-A-Family with a populated family list (cards, portraits,
   funds/count text): the fixture dumps an empty `UIOriginalPickFamily`.
3. Two-button (OK/Cancel) dialogs and notification (0x10c) dialogs: the four
   `uisurvey-dialog*` captures are single-OK; the `uidialog` gate validates
   OK/Cancel captions at IFF/string level only (`size-800x600.log` line 194).
   No visual artifact of a Cancel dialog is retained. (0x10c semantics already
   listed as unrecovered in r240.)
4. The original's exact >1024-wide band composition (Finding 2's parity half).
5. Options dialog CONTENT (explicitly out of scope per assignment); only the
   band-width observation in Finding 2 touches the options captures.
6. Phonebook, scrapbook, budget, help, house, live-popup and skills dialogs were
   visually reviewed at all sizes and showed no reproducible anomaly, but no
   pixel/IFF-level diff against original dialog art was performed in this round;
   absence of evidence there is not certification.

## Analysis scripts (this directory; read-only elsewhere)

`pixel_probe.py`, `hole_id.py`, `diff_art.py`, `rle_sim.py` (byte-exact BmpRLE8
simulation), `palette_test.py` (proof of Finding 1), `far_extract.py`,
`scan_all_fars.py`, `find_mauve.py`, `blank_tile.py`, `find_blank.py`,
`band_edge.py`, `bmp_header.py`.
