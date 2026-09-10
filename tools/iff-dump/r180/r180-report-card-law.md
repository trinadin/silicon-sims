# R180 — child Report Card law

## Failure in the port

The child branch of the Job subpanel was a port-authored three-block layout:
`Report Card`, `Grades: <number>`, and a long school description were drawn in
the 100px toolbar band. It used adult `JobPerformance`, left adult skill/job
controls mounted behind it, and blanked their state strings when switching
modes. None of those behaviors exists in the original desktop client.

## Executable evidence

### One centered control

`cWinSubpanelReportCard::Init` (`0x2a6780-0x2a6af1`) constructs one 420-byte
`cFlashyBtn`. It selects font-array entry 18 at `0x2a6900`, gives the control
the complete report-card child width, gives it `TSCharHeight(font[18])`, sets
`x=0`, and centers it vertically with
`y=(parentHeight-controlHeight)/2`. No separate title, grade caption, or school
description child is attached to the toolbar surface.

`cWinSubpanelReportCard::TSPaint` obtains the active selected Sim, calls
`PersonFinder::GetJobPerformance`, passes that result to `cJob::GetGrade`, and
paints the returned string on this one control.

### Child value source and grade table

`PersonFinder::GetJobPerformance` (`0x23fd30`) branches on age `<18`. The child
branch returns person offset `+0x72`, PersonData word 57
(`JobPromotionLevel`); the adult branch returns offset `+0x7e`, word 63
(`JobPerformance`). The former port read the adult word for children.

`cJob::GetGrade` (`0x5a7c0`) adds one for the original 1-based string-set API
and looks up the value in the grade string set loaded by `LoadCareers`.
`LoadCareers` (`0x59300`) loads work.iff STR#4097, label `Grade Strings`.

Source pin:

- `ExpansionShared.far!work.iff`: 4,308,150 bytes,
  SHA-256 `4f9d3dcfd49b89f068e5ead328920f4cb293584def7597e684f33719f87b2545`
- STR#4097 serialized chunk: 41,006 bytes,
  SHA-256 `ecfe7806425851e8ca2e189eafde966c886c1dcfab1bdcd43dfdb6106a9524f4`
- English-US values, indices 0 through 15:
  `A+`, `A`, `A-`, `B+`, `B`, `B-`, `C+`, `C`, `C-`, `D+`, `D`, `D-`,
  `F`, `F`, `F (danger of military school)`,
  `F (off to military school)`.

The selected font is the shipped `Fonts\\variablesans_18.ffn`: 44,112 bytes,
SHA-256 `2ff1f4dd93343fcdca8f0c7d28102f5d1e8dda01bbf3731cc9934ce1ee058fd0`.

### Click popup

The grade control creates the shared `cWinLivePopupClient` with Live.iff
STR#139 entry 1 (`Grades`) as its title and entry 2 as its body. It borrows
Job popup composite index 11. `JobIconMultiPopup.bmp` is a 22-row sheet with
84x63 cells, so the Report Card artwork is rectangle `(0, 11*63, 84, 63)`.
This is the same special row selected by the original `job -1` dialog-icon
route. Entry 0 (`Report Card`) is the grade control's quick-tip.

## Port correction

The desktop Job subpanel now mounts one `UIReportGradeControl` using the exact
font[18] table. Its hit rectangle spans the complete subpanel width, its height
is the font character height, and its grade text is centered. The displayed
value comes from PersonData word 57 through work.iff STR#4097. All adult job
controls are hidden while the child surface is active and restored without
destroying their captions when an adult is reselected.

The first production survey exposed a separate renderer defect that a UI-tree
visibility assertion could not catch: custom `UIOriginalText`,
`UISkillDisplay`, and `UIOriginalRatingBar` draw overrides did not honor the
framework's `Visible` field, so their cached pixels still painted after the
controls were hidden. Those overrides now return before drawing when hidden,
and the gate renders representative hidden controls in isolation and requires
zero emitted alpha pixels.

Clicking the grade mounts the existing exact shared live-popup implementation
in the uncropped dynamic overlay, anchored to the report-card child's right and
top edges, with the 84x63 row-11 artwork and Live.iff STR#139 strings.

The established mobile touch layout is not geometrically changed; its numeric
placeholder now resolves through the same original grade table.

## Gate

The focused `uijob` gate now requires:

- all 16 exact STR#4097 values and out-of-range empty behavior;
- the original `<18` child boundary and PersonData word-57 display path;
- font[18] to resolve to the shipped `variablesans_18.ffn` object;
- one full-width, font-height, vertically and horizontally centered desktop
  control, with no three legacy text blocks;
- every adult career/plaque/skill control hidden in child mode;
- the popup to be an uncropped dynamic-overlay child with exact STR#139 text,
  right/top anchor, and 84x63 row-11 artwork;
- all adult controls restored after child-to-adult transition.

The visual survey also emits `uisurvey-reportcard.png` from the production
panel after temporarily selecting grade index 4 (`B`) and restores the Sim's
age/grade words immediately afterward.

## Packaged validation

Exact current source was published self-contained for `osx-arm64`, packaged
with `packmac.sh arm64`, and exercised through
`dist/The Sims-arm64.app/Contents/MacOS/TheSims` with
`lot,corpus,uijob,uirel,uisurvey`.

The sealed focused run passed **9/9**, with the Report Card line reporting:

```text
gradeCanon=True font18=True reportSurface=True reportPopup=True
adultRestored=True text=B geo=0,33,504x34 hidden=True/paintTrue
```

The run exited cleanly after both `Run` and `Dispose`. Evidence pins:

- focused log: 736 lines, SHA-256
  `94f6b4bab528349653f752da5404df93e95d8448c6a6c8d0db7673353f60042b`;
- publish and packaged `Simitone.Client.dll`: byte-identical, SHA-256
  `622935ad971f1833dc75dd0ac6554f3602dc72b228d9767a601e4ca3d0410e54`;
- final `uisurvey-reportcard.png`: SHA-256
  `862b15e0f774873a56a8fe1d2f3b987223e529ed4d38212f1db6096baa9900f`.
