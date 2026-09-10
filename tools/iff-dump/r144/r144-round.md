# R144a — Main-UI accuracy round (live toolbar per decoded engine law)

User report: "Do another pass of the UI. The main UI. Make sure we recreated
it accurately. Still seemed wonky."

## What was actually wrong (found, not guessed)

1. **Survey harness ghost** — the FIRST explicit UI draw after a render-target
   switch painted nothing (lazy caches); fixed with a warm-up pass + RT
   re-assert in `AutotestRunner.SurveyDump`. After the fix the live dump
   painted and became trustworthy evidence.
2. **White slab at screen (389,674)-(514,767)** — isolated by a temporary
   per-child bisect dump to `UIPickupPanel`: the mobile pickup strip's STALE
   held-object thumbnail paints at full brightness whenever the panel is
   `Visible`, regardless of `Opacity` (the thumb draw ignores Opacity), and
   the panel anchors its mobile-era (350,64) layout INSIDE the original
   toolbar. Fixed: desktop never paints the mobile pickup chrome (the
   original pickup UI is the cWinDisposePopup family — future round).
   Same root cause (UICachedContainer painting a stale cache at Opacity 0)
   found on `UIQueryPanel` — it tinted webcam cells 2-3; gated the same way.
3. **The whole live toolbar content was mobile-era interpretation.** Hermes
   decoded the real law from the binary (see `toolbar-law.md`): the desktop
   bar was missing the webcam strip, the category tab column, the trend
   arrows; its motive grid was 4 cols x 2 rows at 105px pitch (engine: 2
   cols x 4 rows, 100px gauges, 19px pitch, host at people (300,50)); the
   clock/money were on the toolbar's right end (engine: on the UCP PLATE);
   the LiveGadget was misused as a gauge backdrop (engine: the tab-column
   plaque); Greenbars/Redbars were misused as gauge fills (engine: the mood
   trend arrows beside the Mood tab).

## Ported this round (all per r144/toolbar-law.md, section refs in parens)

- `UIOriginalPeopleChrome` (new): 8-webcam family strip (1.1) with
  PeopleTemplate/UnknownFace art + UIIconCache portraits, click = sim
  selection (`vm.MyUID`); LiveGadget plaque (1.2); 7 category tabs (1.3)
  at engine anchors (Mood 32x39 @(202,31); 27x30 tabs at x237/269, y4/35/66
  panel-local) wired to the existing subpanels (Interest/Gift mount as art,
  no subpanel yet — disclosed); mood trend arrows (1.4) Greenbars/Redbars
  @(205,3)/(205,73), 5px vertical reveal per rating unit, R135 Rating law.
- `UIMotiveSubpanel` desktop branch: engine gauge host (1.5) — 8
  `UIOriginalMotiveGauge` 100x20, flow (0,21)+{100c,19r}, fill law
  `clamp(round(raw*60/200),0,60)`, track color + color-ramp fill + 1px
  highlight, labels font[8] right of the fill; host title "Motives"
  (Live.iff STR#130[0]) font[11] at host (5,0) (1.7). Twins mount EAGERLY
  (the lazy Update mount never fires in the autotest harness and left the
  labels off-screen). Mobile path untouched.
- Clock + money moved to the UCP plate (2): `UIDesktopUCP.MoneyOriginal`
  font[10] right-anchored to plate (178,158) sized for "$9,999,999";
  `ClockDigits` font[8] centered in plate rect (95,118,151,125); plate
  change-floaters re-anchored; MainPanel's toolbar copies removed.
- `UIMainPanel`: kBackPatch 2036 bridge at cpanel (168,50) = child
  (-52,0) (section 0); desktop LIVE subpanels mount at people host
  (300,50) = local (300,0), width to the screen right edge (1.7); the
  mobile category plaque hides in desktop LIVE (the tab column replaces
  it) and returns for buy/build/options; tab highlight syncs through
  every selection path.
- `OriginalGlyphFont.LoadByIndex(i)` — engine font[i] table indices
  resolved to the ffn corpus by point-size number (index == filename
  number; 12 -> _12_bs). Disclosed hypothesis (cITSFontSys map undecoded).

## Pins corrected to the new law (corrections disclosed)

- `uicp` section 6: gauge host/grid/fill-law probes, webcam grid, tab
  anchors, plaque dims, plate money/clock anchors, "no toolbar readouts".
- `uilive`: twin texts compared against the panel's own (engine) motive
  order on desktop (`GaugeStringIndex` = STR#130 idx {1,3,5,7,9,11,13,15}).
- `uichrome`/`uitoolbar` tree phase: kGreenbars/Greenbars + LiveGadget now
  pinned on their new engine mounts (chrome arrows/plaque) with the same
  dimension values.
- `uibargeom`: unchanged text, now passing against the (300,0) mount +
  width-to-edge law via `UISubpanel`.

## Verification

- Full gate: **85/85 PASS** (`r144-gate.txt`).
- Eyes-on: `dumps/uisurvey-live.png` — webcams (2 portraits + empty-face
  cells), plaque + tabs, red trend arrow under the Mood tab (mood
  declining), "Motives" title, 2x4 gauge grid with labels, plate clock
  digits + money. Pixel audits in the transcript (zone probes).

## Residuals (honest)

- Webcam thumbnail DRAW internals (type-2 custom draw) not disassembled —
  portrait+template compose is interpretation; slot ordering follows FAMI
  GUID order.
- Interest/Gift tabs have no subpanels yet (art mounts, clicks no-op).
- Per-motive color-ramp endpoints + track color + label x (62) are
  interpretation (engine vec3 endpoints partially traced); per-gauge delta
  arrows (cWinDeltaMeter {W-20}/{W+61}) not ported.
- font[i] -> ffn resource mapping hypothesis (point-size number).
- Buy/build/options desktop toolbars keep the mobile switcher chrome
  (cWinArch 12-tool column at (338,5) 45px pitch decoded, not ported —
  next round with the neighborhood screen).
- cWinPeople's upper 50px strip (sub-panel popups, pie menus) not ported.
- The survey still draws the screen twice (manager pass + explicit pass) —
  harmless overdraw, heavier glyphs in dumps.
