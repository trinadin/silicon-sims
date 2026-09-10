# R184 — desktop Live Job-family native law

Read-only audit of the five mutually exclusive surfaces behind the desktop Live
**Job** tab in the owned **The Sims Complete Collection** Macintosh executable.
The decoded rectangles below are the production target; they are not inferred
from the port's pre-patch UI tree.

## Provenance and decode anchors

| input | SHA-256 |
|---|---|
| `game-data/The Sims/The Sims Complete` | `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f` |
| `game-data/The Sims/GameData/Live.iff` | `4bbfdfdf50d6cc8f221a09227d04b6edba3cdbdf72cdffa9e5120888c5cbe0ba` |
| `game-data/The Sims/GameData/fame.iff` | `8e0267b18fe2fc3a25457ca4669163fac36889b21f982035296827bdd30bc815` |

The Report Card additionally resolves `ExpansionShared.far!work.iff`
(SHA-256 `4f9d3dcfd49b89f068e5ead328920f4cb293584def7597e684f33719f87b2545`),
whose serialized STR#4097 chunk hashes to
`ecfe7806425851e8ca2e189eafde966c886c1dcfab1bdcd43dfdb6106a9524f4`.

The symbol cross-reference is `r141/symbol-index.txt`; PPC ranges were decoded
with `r145/cdis.py`. Principal anchors are `cWinPeople::SetPanel
0x28a510..0x28aac8`, Job `TSPaint 0x2a3200..0x2a3a64`,
`cPerformanceBtn::CaptionDraw 0x2a5880`, Report Card Init
`0x2a6780..0x2a6af1`, Cat Init `0x45cad0`, Dog Init `0x45dd50`, and Fame
Init/TSPaint/PaintFameBar `0x460620` / `0x45f820` / `0x45fe40`.

## Router law

`cWinPeople::SetPanel` hides the complete old Job-family surface first, then
selects exactly one child in this order:

1. **Report Card** when `PersonsAge > 0 && PersonsAge < 18`;
2. **Dog Skills** for a dog;
3. **Cat Skills** for a cat;
4. **Fame** when the Superstar route is enabled, there is no resolved ordinary
   career, and person word 80 (`TS1FameScore`) is positive;
5. ordinary **Job** otherwise.

The word-80 test is direct executable evidence, not a semantic guess:
`CPState::GetShowFameUIPanel 0x20c410` calls `PersonFinder::GetData`, reads
`lha 0xa0(r3)` at `0x20c47c` (`0xa0 / 2 = 80`), and separately requires
`PersonFinder::GetCareer` to return null.

The practical port predicates for steps 2 and 3 are `VMAvatar.IsDog` and
`VMAvatar.IsCat`; raw `Gender > 1` is invalid because human child encodings also
exceed one. Fame's routing word is intentionally not the word that paints the
visible star bar.

## Ordinary Job

The native 427x100 child keeps all painted content in its first 200 pixels.
`Live.iff STR#136[0] = "Job"` is copied into internal state at `+0x138` but is
**never painted**. There is no desktop Job heading.

Six single-line `font[7]` skill captions use rectangles
`(93,6+15*i)-(155,22+15*i)`, are right-aligned, and are deliberately not
clipped. Their adjacent 40x11 bars begin at `(157,7+15*i)`. The rows and
person-data sources are:

```
i  STR#136  caption      person word
0     1     Cooking          10
1     2     Mechanical       12
2     3     Charisma         11
3     4     Body             17
4     5     Logic            18
5     6     Creativity       15
```

Each value is integer `raw/100`, clamped to 0..10, with four fill pixels per
point over the 40x11 `JobSubBarsOff` / `JobSubBars` art.

The summary controls are exact, independent controls:

| item | icon/control anchor | caption rectangle or anchor | font |
|---|---|---|---:|
| career | `(3,4)`, 28x22 state from `JobIconMultiButton` | `(3,26)-(98,49)` | 7 |
| salary | `(3,45)` | `(20,45)` | 8 |
| performance | `(3,65)` | `(20,66)` | 7 |
| friends needed | `(3,85)` | `(20,85)` | 7 |

Normal caption/guide colour is Sims palette 4 normal, `#C3CDCD`. These captions
also use the native no-clip glyph path. The unemployed values are STR#136[7]
`Unemployed` and [8] `n/a`; the desktop salary is the section-sign amount, while
STR#137[14]/[15] and [16]/[17] are the Salary and Performance popup pairs.
STR#137[0..11] supplies the six skill popup title/body pairs; [23] is the
Hours/Carpool popup format. RT 4905 `JobIconMultiPopup.bmp` is the 84x63-cell
Job popup composite.

`cPerformanceBtn::CaptionDraw` has a unique two-line rule. A one-line career
name follows the ordinary caption path. For two lines, line one is placed at
`x = width(line2)-width(line1)` and line two at `x=0` one font height below, so
their right edges align. If line one is wider than line two, native falls back
to the generic unwrapped caption. It neither clips nor ellipsizes; this law is
what prevents `Subway Musician` from colliding with the summary below.

### Promotion indication

`PersonFinder::GetSkillNeededToAdvance 0x2401f0` returns a required point count
only while `rawSkill < required*100`; otherwise it returns zero. For every unmet
row, native underlines the skill caption and paints a `#C3CDCD` L-guide around
the 40x11 bar:

```
vertical:   x = 4*required-1, y = 0..10
horizontal: y = 10, x = -16 through the vertical marker
```

Fame uses the same rule through `cFameTrack::GetSkillNeededToAdvance 0x57e00`.

## Child Report Card

The child surface is one `cFlashyBtn`, not a toolbar title plus three text
blocks. It spans the full child width at `x=0`, has exactly
`TSCharHeight(font[18])` height, is vertically centered, and centers its one
grade string horizontally. The grade index is child person word 57
(`JobPromotionLevel`), resolved through work.iff STR#4097 (`A+` through the
military-school failure strings); adult word 63 is not used.

The grade control's quick tip is Live.iff STR#139[0]. Clicking it opens the
shared Live popup with [1] as title and [2] as body and uses row 11 of RT 4905:
`JobIconMultiPopup.bmp` crop `(0,11*63,84,63)`. All adult Job controls are
hidden while this one surface is active.

## Dog and Cat Skills

Both pet surfaces paint `Live.iff STR#171[0] = "Pet Skills"` at `(13,8)` in
`font[11]`. Three right-aligned, single-line, no-clip `font[7]` labels occupy
`(103,25+15*i)-(205,41+15*i)`; 40x11 Job bars begin at
`(207,27+15*i)` and again show `raw/100` points.

| row | dog caption | cat caption | person word |
|---:|---|---|---:|
| 0 | STR#171[1] House Breaking | STR#171[4] Hunting | Cooking 10 |
| 1 | STR#171[2] Tricks | STR#171[2] Tricks | Mechanical 12 |
| 2 | STR#171[3] Obedience | STR#171[3] Obedience | Charisma 11 |

Live.iff STR#172 supplies popup title/body pairs. Dog rows map to `0/1`, `2/3`,
`4/5`; Cat rows map to `6/7`, `2/3`, `4/5`. The shared Tricks and Obedience
pairs are intentional.

## Fame

English paints fame.iff STR#1[0] at `(5,0)` in `font[11]`; the language-3
double-byte branch instead uses `(10,5)` and `font[10]`. Six right-aligned,
single-line, no-clip `font[7]` skill captions occupy
`(145,6+15*i)-(207,22+15*i)`, with 40x11 Job bars at `(210,7+15*i)` and the
same person-word order as ordinary Job.

The main fame bar is 80x16 at `(3,30)` and fills exactly `8*level` pixels. Its
visible level is word 81 (`TS1FameStarPower`), clamped to 0..10. The `font[7]`
level caption occupies `(3,45)-(105,61)` and uses STR#1 indices 1..11
(`Nobody` through `Superstar`). A separate decimal word-80 score text is
constructed in `font[7]` at `(250,3)`, updated, and then explicitly hidden at
`0x461294..0x4612a0`; it must never become a second visible fame label. This is
the critical **PD81 visible bar versus hidden PD80 diagnostic** correction.

The famous-friend summary is a 120x15 control at `(3,75)`, `font[7]`. Its icon
is the first 22x20 state of the 88x20 `FameFriendSmiley` sheet; caption glyphs
start at host x=28 (control-local x=25). Native
`Neighborhood::GetFamousFriendCount 0xb2030`:

1. iterates valid other people;
2. rejects an other person whose family word 61 is zero or equals the selected
   person's family word 61;
3. requires relationship slot 0 in **both directions** to meet the global
   friend threshold (50 in the owned executable);
4. sums each accepted person's word 81 star power.

The display multiplies that raw sum by 0.5 and prints exactly `.0` or `.5`.
The per-level friend requirements are
`{0,0,0,0,2,4,7,11,14,18,0}` for levels 0..10.

The six skill requirements by fame level are:

```
0  {0,0,0,0,0,0}       6  {0,0,6,5,0,6}
1  {0,0,1,0,0,1}       7  {0,0,7,6,0,7}
2  {0,0,2,1,0,2}       8  {0,0,8,7,0,8}
3  {0,0,3,2,0,3}       9  {0,0,10,8,0,9}
4  {0,0,4,3,0,4}      10  {0,0,0,0,0,0}
5  {0,0,6,4,0,4}
```

fame.iff STR#1 is mapped as follows: [0] title; [1..11] level labels; [12]
friends-needed format; [13..34] eleven level-popup title/body pairs; [35]/[36]
unused here; [37]/[38] famous-friends popup pair. Fame skill popups deliberately
reuse Live.iff STR#137 pairs `0/1` through `10/11`. The level and friends
popups use RT 4906 `FameIconMultiPopup.bmp`, one 84x63 image.

## Pre-patch visual evidence and gate correction

The r184 pre-patch production captures are:

| capture | SHA-256 | observation |
|---|---|---|
| `/Users/nathannoom/Documents/Simitone/uisurvey-job.png` | `57ab225ed6c9a5d8d2b982a750a8ea9528baa10df3a78b81468e2b38a81cea01` | painted the non-native `Job` heading over the skill area and collided on `Subway Musician` |
| `/Users/nathannoom/Documents/Simitone/uisurvey-reportcard.png` | `be5d42762aa5e3c8a3c831de9afe0da4ec37338c933e321c6957d11d12974c75` | clean single centered `B` Report Card reference |

The earlier `uijob` check nevertheless passed because it asserted the port's
`HostTitle` and old structural shape rather than native paint output. That green
result was a false positive for desktop fidelity; the new law must assert the
mutually exclusive route, absence of a painted Job title, exact rectangles,
no-clip caption behavior, promotion guides, PD81/PD80 split, and popup maps.

## Implementation mapping

The production mapping is contained in
`Client/Simitone/Simitone.Client/UI/Panels/LiveSubpanels/UIJobSubpanel.cs`:

- `UIDesktopJobMode`, `ResolveDesktopMode`, and `UpdateDesktop` implement the
  ordered, mutually exclusive router;
- `UIOriginalCaptionControl` supplies original-font alignment, `#C3CDCD`,
  underline, click, and no-clip painting; `UIJobCareerCaption` supplies the
  two-line career exception;
- `UIJobSkillBar` and `UIFameRatingBar` implement the exact fills and unmet
  promotion guide;
- the Job initialization deliberately creates no desktop `HostTitle`;
- `UIReportGradeControl`, the pet/fame control builders, raw format-3 STR#
  loader, and popup toggles map the remaining surfaces above;
- `FamousFriendStarPower` applies family exclusion, mutual relationship slot 0,
  and word-81 accumulation; `FameScoreLabel` remains explicitly hidden.

The decoded implementation branch is desktop-only and returns before the
established mobile Job/Report Card path, so this correction does not change the
mobile layout.

## Verification

`dotnet build Client/Simitone/Simitone.Client/Simitone.Client.csproj
--no-restore -v:q -p:WarningLevel=0` completed successfully on 2026-08-31:
**0 errors**, with the repository's existing 42 `NU1701` compatibility
warnings. `git diff --check` passes for the production source and this note.
