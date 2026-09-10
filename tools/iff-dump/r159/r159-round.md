# R159 — The people-panel completion (UI-100% campaign, round 1)

User directive (2026-08-28): base game first; **UI 100%**. This round executes
backlog Tier-A items 1/7/8 + the Tier-B string wirings from the campaign plan
(PARITY.md), on the three-scout research phase committed earlier the same day
(`r159-scout-*.md`, engine decode `r159-scout-engine-decode.md`).

## What was ported

### 1. The relationship subpanel on ORIGINAL art + the decoded engine law
`UIRelationshipSubpanel.cs` rewritten (was: mobile `rel_*.png` buttons +
`inv_item.png` cards + two 0.5-scale UIMotiveBars):
- **Sort plaques** kRelFamilySort/kRelFriendSort/kRelAllSort/kRelFamousSort
  (4108-4111, `CPanel/Buttons/Rel*Sort.bmp` 80x20 — cWinPeople::
  InitRelationshipSorts 0x28e910 cTSWinBtn toggles) with the original STR# 240
  tooltips ('Family Only'/'Friends Only'/'Famous Only'/'All'). Selected state
  = a cool tint (the engine toggles without art frames — disclosed reading).
- **Marker priority chain** (TSPaint 0x29b918): deep-heart > heart > smiley,
  drawn beside each row. Fired from the relationship record's FLAG word.
- **The flag-word discovery** (fixture NBRS probe, `r159-nbrs-probe` below):
  NBRS relationship value lists are `[score, flags]` — the married pair
  carries `[100, 1]`; every other record carries one value or `[n, 0]`. So
  value[1] IS the engine's marker source (the rel record's +68/+69/+70 flag
  bytes packed): bit1 → deep-heart, bit0 → heart, else smiley.
- **One score per row** (value[0], 0-100): the OLD UI's second bar read
  `values[2]` — an index that exists in NO record in the fixture (histogram:
  lengths 1-2 only). Dead code removed, not "fixed".
- **Family sort filters by FAMI membership** (the webcam strip's own test).
  Friends/Famous filters behave as All pending the engine's friend/fame flag
  semantics (disclosed — those flags are BSS-gated expansion systems).
- Rows carry the kPeopleThumbnailBackground cell (`ThumbBack.BMP` 25x25,
  res 108) behind the portrait; names/scores in the original caption font.

### 2. The job subpanel composites
- The **performance gauge** is now the ORIGINAL Job bar family
  (kJobSubpanelBars 4901 over kJobSubBarsOff 4920, via the R132
  `UIOriginalRatingBar.JobBar()`) — was a Greenbars-motive-bar reuse.
- The **caption plaques** kPerformanceBtnBMP 4982 (52x12) and kSalaryBtnBMP
  4981 (52x11) mounted beside their readouts.

### 3. The child ReportCard branch
cWinSubpanelReportCard (0x2a6180) decoded as a live-popup overlay on the Job
subpanel reusing its popup composite — no art of its own; strings Live.iff
STR# 139 ('Report Card' / 'Grades' / the school description). Children are
gated by **PersonsAge** (fixture law: adults carry 27, children 9 — proven
across all 49 NBRS records). The branch hides the career block and shows the
report-card texts. The grade LETTERS are string-set-indirected in the engine
(helper 0x5a7c0 builds them through 0x146710) — shown as the numeric school
performance until that set decodes (disclosed residual).

### 4. Skill pips
`UISkillDisplay` now draws **kSkillHilite** (`CPanel/SkillsHilite.bmp` 4x11,
res 4511) — was the mobile `skill.png` + UIStyle tints. Needed-tier alpha is
our disclosed reading (only the hilite bitmap ships).

### 5. String wirings
- **Zodiac names** from UIText.iff STR# 164 'signs' ([0..11] Aries..Pisces) —
  computed since R143 but never read from the table (hardcoded literals demoted
  to load-failure fallback).
- **Clock AM/PM** from STR# 138 [21]/[22] — the r117 'mapped-UNUSED' entries.
- Live.iff 139 added to `OriginalLiveStrings.Wanted`.

## The gate

NEW check **'uirel'** (95 → 96): disk-pins STR# 240 (sha ac8b98b6… + all 4
values), STR# 164 (sha 39de1694… + Aries/Aquarius/Pisces), Live.iff 139 (sha
f110a54a… + all 3 values); art dims for the 4 plaques / 3 markers /
SkillsHilite / ThumbBack / salary+performance plaques; LIVE construction of
the subpanel through its real ctor with the 4 original sorts mounted + tooltips
== disk canon; the marker priority law on synthetic flag words (0→smiley,
1→heart, 2/3→deep); the child law (27→false, 9→true, 0→false); zodiac
table-read; AM/PM table; SkillsHilite pips.

- r159-targeted-run1.log: the harness arg form discovery (`-autotest-opts`,
  not `-autotest-checks`) + uibrand failing on the unpacked publish dir
  (expected — it asserts the packed bundle).
- r159-targeted-run2.log: honest FAIL — the gate's own marker assertion used
  flag 5 (bits 0+2) where deep-heart requires bit 1; the PORT law was correct.
- r159-targeted-run3.log: **PASS 5/5**.

## Residuals (disclosed)

- Friends/Famous sort filters = All until the friend/fame flag semantics
  decode (BSS-gated expansion systems).
- Grade letters string-set-indirected (numeric shown).
- The relationship DeltaMeter (this+416, 20-wide trend meter) not ported —
  its anchor is BSS ([TOC-20288]); per-row trend deferred.
- SocialInfoPopup 3750 + kJobIconGeneric 4902/kJobPopupComposite 4905 crop
  laws undecoded — next people-panel round.
- Sort-plaque positions are disclosed interpretations (engine reads them
  from the RT buffers).

## Evidence

- `r159-nbrs-probe.txt` — the NBRS record dump that established the
  [score, flags] value layout + the PersonsAge 27/9 child law (49 records).
- `r159-relstrings-canon.{py,txt}` — the 240/164/139 canon generator + dump.
- `r159-scout-*` (committed earlier this round-phase), `r159-*.txt` raw
  disassemblies.

## Full gate

- r159-gate-run1.log: honest artifact — 94/0 with `glob-fix`/`chunk-reg`
  logging `probe-missing` (macOS cleaned `/tmp/npc_glob_probe.iff` +
  `/tmp/posi_probe.iff`; regenerated with `tools/iff-dump/make_probes.py`).
- r159-gate-run2.log: **FULL GATE 96/96 PASS** (95 + uirel), clean exit chain
  (`after Run`/`after Dispose` + `AUTOTEST_WRAPPER_EXIT=0`), carseek
  minute=615 spawnedCars=1 attr0Max=9.
- Dist byte-match: Simitone.Client.dll 4a35868c…, FSO.SimAntics.dll
  903db62a… (publish == dist/The Sims-arm64.app).
