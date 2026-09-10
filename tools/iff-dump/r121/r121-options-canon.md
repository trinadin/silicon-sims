# R121 — the ORIGINAL options screen (STR# 145 'optionstrs' + CPanel options art)

Round protocol: decode/port original data → pin in the autotest gate → full gate green →
evidence (this file) → PARITY.md/AUTOTEST.md rows → commit.

## What the original has

`UIText.iff` STR# 145 `optionstrs` (84 English entries, format -3 language pairs,
lang-1 block first; full-chunk sha256 **b4f0ecd775cb614758f005fea4f218df5dbc12f481208edcc7be0159da503ea6**,
header included — gate convention; decoded by `make_r121_optionstrs_canon.py`,
output `r121-optionstrs.txt`):

- **The original's own layout directives live in the string table** as `'(x;y)'` entries.
  Main screen buttons pair even index (position) with odd i+1 (caption):
  `[2](29;6)/[3]Save`, `[0](88;6)/[1]Neighborhood`, `[4](159;6)/[5]Quit`,
  `[6](29;58)/[7]Graphics Options`, `[8](89;58)/[9]Sound Options`, `[10](151;58)/[11]Play Options`
  — a 3×2 grid. Section anchors: popups `[13](200;20)` graphics / `[43](200;28)` sound /
  `[55](200;20)` play; right-side radio anchors `[26](480;30)`, `[37](485,30)`;
  `[68](521;24)` near the Reset Tutorial zone.
- **Graphics Options** `[14..41]`: Anti-alias, Shadows, Lighting, Interface Effects
  (each with `About X` title + full help body), then Terrain Detail + Character Detail
  with the shared Low/Med/High labels `[39][40][41]`.
- **Sound Options** `[42..52]`: F/X (Sound Effects), Music, Vox — each short label,
  long label and a volume help body. The original volume range is 0..10 (the port's
  GlobalSettings bytes 0..10 / `HITVM.SetMasterVolume(v/10f)` match exactly).
- **Play Options** `[53..83]`: Auto-Centering, Free Will (394-byte canon help text),
  Edge Scrolling, Sim In Background, then Reset Tutorial (button), Quick Tips,
  Auto Snapshot, Live PIP, Export HTML.

**Art** (UIGraphics.far, `Res_CPanel.h` symbol map + `res_cpanel.rt` template rows —
same idiom as R86/R88/R89): six main buttons `Opt{Save,Nbhd,Exit,Graphics,Sound,Play}.bmp`
(4-frame RLE8 state sheets: up/down/hover/disabled), widget sheets `optcheckbox.bmp`
(4 frames: `[unchecked, unchecked-hi, checked, checked-hi]` — verified by palette/ASCII
decode, the checkmark lives in frames 2/3), `optradio.bmp` (same convention, white-dot
selected in frame 2), `optslideron/off.bmp` (44×14 track + fill), `opttutreset.bmp`
(single-frame caption button), `options.bmp` (the CP-mode bitmap, reused as the
sub-screen back affordance), and **18 About-popup panels** `PopupOpt*.bmp` (133×103)
including `PopupExportHTML.bmp` / `PopupResetTutorial.bmp` — the RT-mapped names for
`kPopupOptHtml`/`kPopupOptReset` (there is no `PopupOptHtml.bmp`/`PopupOptReset.bmp`
member; the RT template is authoritative). All 33 members pinned name+len+dims+sha256
in `r121-art-canon.txt` (12 controls + options.bmp + 18 popups + the 2 corrected
members); byte-verbatim extracts in `art/`.

`OptionsBack.bmp` (`kBkgOptions` 103) is declared by the RT template but has NO member
in this disk's FAR manifest (first-occurrence scan over the manifest + the r85 idiom) —
the original composes panel chrome from `PanelBack` + `OptionsPatch` (37×100) +
`OptionsToothpkHoriz/Vert`; the port keeps its existing subpanel background this round
(disclosed residual, see below).

## What the port had

`UIMainPanel` OPTIONS mode built a `UIButtonSubpanel` with three modern `UICatButton`s
(Save/Neighborhood/Quit from 145 [3]/[1]/[5]) plus a port-authored "Free Will: ON/OFF"
toggle. Graphics/Sound/Play Options did not exist at all.

## What R121 ships

`UIOriginalOptionsPanel` (new, `LiveSubpanels/UIOriginalOptionsPanel.cs`) replaces the
subpanel for OPTIONS mode:

- **Main screen**: the six original buttons at the canon `(x;y)` coordinates (parsed
  from the LIVE string table at mount, not hardcoded), captions verbatim from 145,
  original 4-frame art via `UIOriginal.ResolveOrPng` (IFF-first, png fallback for the
  three that have one). Captions render in the R119 original-glyph default renderer.
- **Graphics/Sound/Play sub-screens**: original checkbox/radio/slider art, canon
  captions/labels (Low/Med/High from [39]-[41]), each caption click opens its About
  popup (`PopupOpt*.bmp` ×2 scale, `About X` title, body word-wrapped through
  `UIOriginalParagraph` in the dialog .ffn) at the canon section directive.
- **Live wiring** (real engine effect): Free Will ↔ `VM.FreeWillEnabled`+`TS1FreeWill`;
  Edge Scrolling ↔ `GlobalSettings.EdgeScroll` (read live by `UILotControl.TestScroll`);
  the three volume sliders ↔ `FXVolume`/`MusicVolume`/`VoxVolume` +
  `HITVM.SetMasterVolume` live (the exact application boot performs); Anti-alias ↔
  `AntiAlias` (affects subsequently created surfaces — pie menus/head scenes read it live).
- **Persistence-only** (disclosed): Shadows, Lighting, Interface Effects, Terrain/
  Character Detail, Auto-Centering, Sim In Background, Quick Tips, Auto Snapshot,
  Live PIP, Export HTML persist canon state to config.ini via new GlobalSettings keys
  (`TS1Shadows`, `TS1InterfaceFX`, `TS1TerrainDetail`, `TS1CharacterDetail`,
  `TS1AutoCenter`, `TS1SimInBackground`, `TS1QuickTips`, `TS1AutoSnapshot`,
  `TS1LivePIP`, `TS1ExportHTML` — FreeSO submodule commit) pending engine work.
  Reset Tutorial mounts canon art + its canon About text; the destructive tutorial-house
  reset has no port equivalent (no tutorial state).
- The port-authored free-will toggle and its `FreewillOriginal` twin are DELETED
  (Free Will is now the canon Play Options row).

Engine-side choices disclosed (canon gives no data): caption placement around the canon
button positions (under, font-8/10), sub-screen widget rows, the back button (canon
`options.bmp` bitmap), popup ×2 scale + text metrics, slider Off-track/On-fill
composition, sub-panel background (no `OptionsBack.bmp` member exists on this disk).

## Gate (CheckUIOptions 'uiopts', 69th check)

1. **Disk canon**: STR# 145 chunk sha256 + label + 84 English entries + 28 spot pins +
   the Free-Will-body prefix + Export-HTML double-space body pin + all 12 layout
   directives parse from the DISK table.
2. **Art canon**: all 30 pinned members present in the TS1Global mount, byte-identical
   (len + BMP dims + sha256, case-insensitive manifest match — manifest casing is mixed:
   `OptSave.BMP`, `optgraphics.bmp`...).
3. **Live**: OPTIONS mode mounts the panel; 6 mains with captions == disk table verbatim,
   canon positions == parsed directives, live relative deltas == canon deltas, textures
   at pinned frame dims; graphics screen 4 checks + 2 radios + Low/Med/High ×2;
   sound sliders bound to the REAL settings (FXVolume mutation reflects instantly);
   play screen 8 checks + tutreset; Free Will row == VM.FreeWillEnabled and toggling it
   flips the VM + setting (restored); Edge Scrolling toggle flips the live setting
   (restored); About popup opens at the canon (200,20) with title/body verbatim;
   LIVE mode restored after.

## Runs

- **r121p1: honest FAIL (gate-side, both bugs mine)** — `canon=True art=False live=False`.
  (a) 4 of the 30 hand-typed art sha256 constants had mid-string transcription errors
  (identical 10-char prefixes, diverging tails — the R119 lesson again: never hand-copy
  64-hex strings). Fixed by REGENERATING the whole pins table from the FAR bytes
  (`regen_gate_shas.py`; `fix_gate_shas.py` now diffs gate-vs-truth: bad=0).
  (b) the mains dims assert compared frame width against SHEET width (`156/4 == 156` —
  always false). Fixed to sheet-vs-sheet + `%4==0`.
- **r121p2: honest FAIL (port-side mount bug)** — `canon=True art=True live=False
  liveFails=mains`. Root cause: the FAR provider's `EntriesByName` lookup is
  CASE-SENSITIVE and the manifest casing is chaotic (`OptSave.BMP`, `optgraphics.bmp`,
  `opttutreset.BMP`); the panel requested `OptSave.bmp`/`OptGraphics.bmp`/etc. → exact
  miss → png/null fallback → texture dims wrong at i=0. The sub-screens passed because
  their member casing happened to match. Fix: (1) the panel now uses the manifest's
  EXACT case; (2) `UIOriginal.EnsureResolved` grew an OrdinalIgnoreCase fallback through
  `GetFarEntries` (defense for every future round — RT templates declare 'CPanel/...'
  while the manifest stores 'cpanel\...'); (3) the gate's mains loop now logs the exact
  failing index+cause.
- **r121p3: PASS — suite 69/69** (full soak, clean exit; `uiopts canon=True
  (optionstrs/84 sha=b4f0ecd7) art=True live=True directivesParsed=7`;
  `AUTOTEST RESULT PASS passed=69 failed=0`).

## Residuals (disclosed)

- Persistence-only canon options (Shadows, Lighting, Interface Effects, Terrain/
  Character Detail, Auto-Centering, Sim In Background, Quick Tips, Auto Snapshot,
  Live PIP, Export HTML): state persists to config.ini verbatim; the engine does not
  yet read these keys. Free Will, Edge Scrolling, the three volumes and Anti-alias
  have live engine effect (Anti-alias on newly created surfaces).
- Reset Tutorial mounts canon art + canon About text; the tutorial-house reset itself
  has no port equivalent (no tutorial state to reset).
- `OptionsBack.bmp` (kBkgOptions) has NO member in this disk's FAR manifest — the
  options panel background remains the port's existing subpanel chrome (the original's
  composition would be PanelBack + OptionsPatch + toothpicks; those three members all
  exist and are future work).
- Engine-side placements (canon gives no data): caption text around the canon button
  positions, sub-screen widget rows, popup scale/text metrics, slider fill
  composition, the sub-screen back button (canon `options.bmp` art).
- The category-switcher still mounts its single dummy category in OPTIONS mode
  (pre-existing; the original has no category column on the options screen).
