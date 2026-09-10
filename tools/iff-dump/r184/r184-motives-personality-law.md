# R184 — desktop Live Motives + Personality native law

Read-only audit of the bottom-right `cWinPeople` panels in the owned **The Sims
Complete Collection** Macintosh executable.  This note is an implementation target,
not a claim that the current port already satisfies it.

## Provenance

| input | size | SHA-256 |
|---|---:|---|
| `game-data/The Sims/The Sims Complete` | 6,486,820 | `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f` |
| `game-data/The Sims/UIGraphics/UIGraphics.far` | — | `f0afa06b24888bd4201a3e619cf2c8d3f5ea1c9b1de6e11572981dbc871e5c8b` |
| `game-data/The Sims/GameData/Live.iff` | — | `4bbfdfdf50d6cc8f221a09227d04b6edba3cdbdf72cdffa9e5120888c5cbe0ba` |
| `game-data/The Sims/GameData/UIText.iff` | — | `b229da197eb9678370844c4da7c88640c3d1ce9a5e528654bfd71df6ef12a9ad` |
| `Fonts\\variablesans_08.ffn` | 16,208 | `9a9b707e34248ce1a3963893961dc23608b810e7745554c79b7cded8b5ef3afb` |
| `cpanel\\HouseSubBars.bmp` (RT 4900 / `0x1324`) | 1,976; 40x16 | `83a6513e05b431493f6678fbbc47e6399e0212cf932a31f914bb8045750057c9` |
| `cpanel\\Backgrounds\\PersBkg.bmp` (RT 4903 / `0x1327`) | 1,694; 40x14 | `fbada305fa488b30ce5c18ee26550a7e1b210ef06dd0925fb51f5a69fe363843` |
| `UIText.iff STR#164` (`signs`) raw chunk | 23,064 | `39de169478222c44ad23942738562fc8b434620c76b6be9384b3ac5fa69ff182` |
| `UIText.iff STR#165` (`SignPopups`) raw chunk | 80,648 | `ec3b7d715a6aef0f0e5c4cf6ec823bcf36b2e1ecf87b182d51516cf39c826604` |

The raw `Live.iff` `STR# 130` chunk hashes to
`72f1dcd92fa90ce56cc114ae143e57b200699a67e0b16c54b962908e2fbc3c5b`;
raw `STR# 131` hashes to
`4e960920e8326379ed7e1b752a43497695940fc5a6ce6d5334c073b8386faa6b`.
The static-symbol cross-reference is `r141/symbol-index.txt`; target PPC ranges
were decoded with `r145/cdis.py`.

## Shared host law

`cWinCPanel` is screen `(0, screenHeight-150, screenWidth, screenHeight)`.
`cWinPeople` is cpanel-local `(220,0,screenWidth,150)`.  Both subject hosts are
ordinary `cTSWin` children at people-local `(300,50,580,150)`, therefore:

* main-panel local `(300,0)`;
* screen `(520,screenHeight-100,800,screenHeight)` at every resolution;
* native logical size **280x100**, even when the port's container continues to the
  physical right edge.

Each host has a `cTSWinText` title, `font[11]`, at host-local `(5,0)`.  The text is
`Live.iff STR#130[0] = "Needs"` or `STR#131[0] = "Personality"`.  The former
chunk's label is `Motives`; the rendered title is not “Motives”.  People init
does not call `cTSWinText::SetTextColor` on these titles: English uses the font's
native/default colour.  (Languages 4 and 7 replace font11 with font9/font10; the
double-byte layout also uses a different title anchor.)

`cWinPeople::SetPanel` (`0x28a510`) hides all category hosts/buttons first, then
shows exactly the selected one.  Panel 1 selects human or pet motives by species;
panel 2 shows Personality.  There is no cross-fade or simultaneous old/new host.

The species discriminator used throughout these two panels is the signed word at
selected instance `+0x60e`: native's `0/1` class takes the human path; other
classes take the pet path.  Panel 1 shows host `cWinPeople+0xf8` plus human
motives (`+0x128`) for the former, or host `+0xfc` plus pet motives (`+0x134`)
for the latter.  Both arrays are pumped even when their host is hidden.  Panel 2
uses the same discriminator for human versus pet strings and zodiac visibility.

The port already exposes the correct practical predicate as `VMAvatar.IsPet`:
`FreeSO/TSOClient/tso.simantics/Entities/VMAvatar.cs:208-214` tests
`Gender & (8|16)`.  `IsDog` and `IsCat` separately test bits 8 and 16 at lines
217-232.  Do **not** substitute `Gender > 1`: child encodings 2/3 are human.
The zodiac age gate described below is intentionally a separate raw-Gender
comparison, not this species test and not the port's age-derived `IsChild`.

## Motives / Needs

### Geometry and ordering

Relevant code: `cWinMotive::Init 0x27f620`, `TSPaint 0x27ea50`, constructor
`0x27fa50`, `SetVal 0x27e200`, static initializer `0x27fe20`.

The executable table at file address `0x5a45b8` is:

```
{ 7, 5, 6, 15, 8, 14, 9, 13 }
  Hunger Energy Comfort Fun Hygiene Social Bladder Room
```

The corresponding `STR#130` name indices are `{1,3,5,7,9,11,13,15}`.  Eight
100x20 controls are host-relative:

| grid index | motive | rect `(l,t,r,b)` |
|---:|---|---|
| 0 | Hunger | `(0,21,100,41)` |
| 1 | Energy | `(100,21,200,41)` |
| 2 | Comfort | `(0,40,100,60)` |
| 3 | Fun | `(100,40,200,60)` |
| 4 | Hygiene | `(0,59,100,79)` |
| 5 | Social | `(100,59,200,79)` |
| 6 | Bladder | `(0,78,100,98)` |
| 7 | Room | `(100,78,200,98)` |

The static pair written by `0x27fe20` is `{20,12}`.  It is the bar origin, not a
whole-widget fill size.  Within each 100x20 control the only bar rectangle is:

```
bar = (20,12,80,17)             // 60 x 5
fill = clamp(trunc(0.5 + 60*(raw+100)/200), 0, 60)
```

The arithmetic at `0x27eb64..0x27ebc8` is floating-point add-0.5 followed by
PPC `fctiwz`, i.e. nonnegative round-half-up after the `raw+100`
normalisation.  It is not C#'s default `Math.Round` (ties-to-even): for example,
`raw=-85` gives 4.5 pixels, hence native **5** versus `Math.Round(4.5)` **4**.

After base-button text painting, `TSPaint` fills `[20,20+fill) x [12,17)` with
the current positive ramp colour and `[20+fill,80) x [12,17)` with the current
remainder colour.  If `fill > 1`, the final active column at `x=20+fill-1` is
painted **black** for all five pixels.  There is no 100x20 dark backing plate and
no horizontal top highlight.

The two colours are animated state, not a stateless red-to-green function of the
current motive.  `TSPaint 0x27ec14..0x27f204` consumes two `RampGenerator`
histories and vector interpolation; the hard-coded colour endpoints include the
`(93,64,95)` / `(0,209,0)` paths at `0x27ed30..0x27ee18`.  Porting only a simple
raw-value lerp cannot reproduce native transitions.

### Caption and delta meter

`Init` calls `SetImage(NULL,4,1)` and uses `font[8]`.  `TSPaint
0x27ea7c..0x27eb44` replaces the button text rectangle with:

```
(0, (10-fontCharacterHeight)/2, 100, 20)
```

For the shipped English font8, character height is 16, hence the glyph origin is
`y=-3`.  The caption is not parked at x=62; it overlays the control's full text
rect exactly as the native button painter clips it.

The caption colour does not come from pixels in `variablesans_08.ffn` and is not
an arbitrary UI-style white.  `InitSimsColors` creates RGB `#C3CDCD` at
`0x25d680..0x25d6a0` (BSS colour handle `0x92988`), and registers the four
handles as control palette **4** at `0x25d96c..0x25d9a0`.  Both widgets select
that palette with `SetImage(NULL,4,1)`.  Its state order is:

```
normal #C3CDCD, pressed #00FFFF, hover #FFFFFF, disabled #405D5F
```

A full-size 100x20 `cWinDeltaMeter` child is also created.  `SetTopLefts` places
its two indicator lanes at `(0,12)` and `(81,12)` (the `{20,12}` origin minus
20, and origin plus 61).  The constructor's argument is meter width 10.  In
`TSPaint 0x275450..0x2754f8`, a negative sample starts at first-lane
`x+(10-4)=6`; a nonnegative sample starts at second-lane `x=81`.

There is **no motive-arrow bitmap or sprite resource to identify**.
`cWinDeltaMeter::DrawArrow 0x274970..0x274a64` writes a procedural 3x5 mask
directly to the locked 16-bit surface.  Negative uses the left-pointing form and
nonnegative the mirrored right-pointing form:

```
left       right
..#        #..
.##        ##.
###        ###
.##        ##.
..#        #..
```

`TSPaint 0x275570..0x2758b4` iterates **five** delta-history entries, not one
static indicator.  Each is ramp-coloured; later arrows advance by four pixels
toward/through their lane according to sign and animation state.  `SetVal`
drives this meter and both bar ramp histories; it does not simply replace one
integer used by the bar painter.

### Popup / input

Each human motive control owns a `cWinLivePopupClient`, constructed from the
`STR#130` name/description pair and optional selector GUIDs, with client tag
`"motives"`.  The control uses the normal button hit area, while
`TSOnMouseDownL` delegates to the base button and performs no direct motive
mutation.  `cWinPeople::SetQuickTips` also toggles the native quick-tip pointer.
The current port exposes neither this live popup nor the delta indicator/history.

## Personality

### Exact five-row layout

Relevant code: `cWinPeople::BuildPersonalityButtons 0x2895b0..0x289818`, people
init `0x290aac..0x290e74`; `cWinPersonality` mouse `0x2930e0`, hit test
`0x293140`, `UpdateClient 0x293220`, `SetPerson 0x293440`, `SetTextRect
0x293520`, `ComputeTextRect 0x2936a0`, `TSPaint 0x293760`, `Init 0x293e00`,
constructor `0x294070`.

The executable data-index table at file address `0x5a45d8` is
`{7,6,3,5,2}` = Neat, Outgoing, Active, Playful, Nice.  English font8 widths are
28, 48, 36, 36, 25 pixels.  `ComputeTextRect` adds 3 to the widest caption, so
the common text width is 51.  `SetTextRect` adds a further 3-pixel gap before the
40-pixel bar.  Each final control is therefore **97x16**, laid out rightward from
`x = 202-97 = 105`, with an 18-pixel vertical step:

| trait | person-data index | host-relative rect |
|---|---:|---|
| Neat | 7 | `(105,8,202,24)` |
| Outgoing | 6 | `(105,26,202,42)` |
| Active | 3 | `(105,44,202,60)` |
| Playful | 5 | `(105,62,202,78)` |
| Nice | 2 | `(105,80,202,96)` |

There is one locale-specific single-byte branch for language byte `0x13`: people
init shifts the last row's left edge by +25, recomputes its private text rect and
control width, then moves it +3x; that locale's text paint also shifts the text
rectangle three pixels upward.  It is not active in English.

### Art, points, caption colour

`Init` loads RT 4900 `HouseSubBars` and RT 4903 `PersBkg`, calls
`SetImage(NULL,4,1)`, and uses `font[8]`.  It does **not** use
`SkillsHilite.bmp` or draw ten separated skill pips.

For each selected person:

```
points = clamp(personData[dataIndex] / 100, 0, 10) // integer division
text rect = (0,0,51,16)
bar x = 54
PersBkg destination = (54,1,94,15)                // full 40x14 backdrop
HouseSubBars source crop width = 4*points          // 40x16 at 10 points
HouseSubBars destination origin = (54,0)
```

The one-pixel vertical difference follows centering the 14-pixel backdrop inside
the 16-pixel font/control height.  Caption state colours are the same type-4
palette above; normal is `#C3CDCD`.

### Human/pet strings, popup, and hit law

`BuildPersonalityButtons` loads human `Live.iff STR#131` and pet `STR#170`.
Each trait consumes seven strings: trait name, low/middle/high tier names, then
low/middle/high descriptions.  Human trait blocks begin at indices
`1,8,15,22,29`.  `SetPerson` selects the human corpus when the selected instance
`+0x60e` class is `0/1`; otherwise it selects the pet caption and popup corpus.
There is no separate child layout or child trait-order branch.

`UpdateClient` computes `points=floor(value/100)` and chooses:

```
0..2 -> low tier
3..7 -> middle tier
8..10 -> high tier
```

It updates a `cWinLivePopupClient` with that tier's name/description and a cropped
45x45 selected-person portrait.  `IsPointInWindowWindowCoordinates 0x293140`
accepts **only the common caption text rect**, not the bar or entire 97x16
control.  Mouse-down merely delegates to the base class; this panel is read-only.

### Zodiac control (correction to R144)

The `cTSWinBtn*` stored at `cWinPeople+0x1bc` is the **zodiac-sign label**, not a
selected-Sim-name button.  It is created at `0x290e74..0x290f08`, uses
`font[12]`, type-4 state styling, and is parented to the Personality host.

Its displayed sign code is one-based.  `UIText.iff STR#164` has 14 English
entries and supplies the exact mapping `name = STR164[code-1]`:

| code | string index | label |
|---:|---:|---|
| 1 | 0 | Aries |
| 2 | 1 | Taurus |
| 3 | 2 | Gemini |
| 4 | 3 | Cancer |
| 5 | 4 | Leo |
| 6 | 5 | Virgo |
| 7 | 6 | Libra |
| 8 | 7 | Scorpio |
| 9 | 8 | Sagittarius |
| 10 | 9 | Capricorn |
| 11 | 10 | Aquarius |
| 12 | 11 | Pisces |

`cWinPeople::SetPerson 0x28bcf0..0x28be78` reads PersonData byte offset `0x82`
(index 65, Gender/age encoding).  Values 0/1 are adult male/female; values 2/3
are male/female children in the port's matching enum.  Only `Gender <= 1` enters
the native zodiac branch.  It computes/caches zodiac when PersonData offset
`0x8c` (index 70, TS1Zodiac) is zero, sets the zodiac name, autosizes to font12
text width/height, and anchors bottom-left:

```
(5, hostHeight-textHeight-5, 5+textWidth, hostHeight-5)
```

It is hidden when `Gender > 1`; thus children do not get zodiac text.  The panel
visibility pass at `SetPanel 0x28a828..0x28a894` and the selected-person update at
`0x28b12c..0x28b188` additionally hide it for non-human instance classes and only
allow it with Personality.  A missing selected person causes panel 0 and no
Personality host.  `SetQuickTips 0x288100..0x288134` assigns the zodiac quick-tip
when quick tips are enabled.  That quick-tip is `UIText.iff STR#130[27] =
"Astrological Sign"` (`r143/uitext-130-DesignCharStrs.txt:30`; its resource
comment explicitly identifies the readout below Personality).

The label is also a popup button.  People init `0x290f0c..0x2910b8` loads
`UIText.iff STR#165` and prebuilds 12 title/description pairs:

```
title(code)       = STR165[2*(code-1)]
description(code) = STR165[2*(code-1)+1]
```

Thus English starts `STR165[0] = "About Aries"`, `STR165[1] = <Aries body>`,
and continues pairwise through Pisces.  Init appends compatibility lines to each
description using `STR164[12] = "Most compatible with:  "` plus
`GetCompatibleSigns(code)` (`0x171e00`), then `STR164[13] = "Least compatible
with:  "` plus `GetIncompatibleSigns(code)` (`0x171ce0`).

Follow-up decode (R187 correction): the two helpers already return the complete
prefixed lines. `InitZodiac` loads the one-based UIText `STR#154` entries 7/8
(English indices 6/7, `"Most compatible with: "` / `"Least compatible with: "`),
then each helper appends the two sign names with the literal `", "` at data
`0x4c2e0`. The exact one-based pair tables at data `0x4c220` / `0x4c280` are:

```
sign:          1    2    3     4    5     6    7     8      9      10     11    12
compatible:   3,2  1,7  12,6  2,8  9,4  11,9  6,4  12,5  12,10  11,2  10,9  8,3
incompatible: 4,7  6,4  10,1  3,1  10,3  5,2  12,8  7,11  7,8   5,3   8,6   5,1
```

The visible result appended to each STR#165 body is a newline, the compatible
line, another newline, and the incompatible line. STR#164[12]/[13] happen to
carry visibly equivalent prefixes but are not the globals read by these two
helpers in this executable.

`cWinPeople::TSOnCommand 0x28c648..0x28c6ec` recognizes the zodiac button,
converts its visible localized sign name back to code via `0x171f20`, installs
the corresponding pair in the live popup and presents it.  The R187 port pass
now mounts that autosized label/quick-tip and retargets the same live-popup
object between portrait-backed trait clients and the text-only zodiac client.

## Current-port mismatch map (audited 2026-08-31)

1. **Personality composition is structurally wrong.**
   `UIPersonalitySubpanel.cs:48-64` constructs `UISkillDisplay` controls in a
   3-column x 2-row grid at x=334/474/614 and y=35/95, even though the subpanel
   itself is already mounted at x=300.  Native is the five-row table above at
   host x=105.  This explains off-band clipping and overlap.
2. **Personality uses the wrong art and visual grammar.**
   `UISkillDisplay.cs:17-23,61-74` explicitly uses `SkillsHilite.bmp`, ten pips,
   8-pixel pitch, and invented alpha/dark tints.  Native uses the two RT resources
   above as a continuous 40-pixel strip cropped in four-pixel increments.
3. **Personality typography is wrong/incomplete.**
   `UIPersonalitySubpanel.cs:67-83,95-100` loads `LoadCaption`, applies
   `UIStyle.Current.Text`, and retains size-15 fallback labels.  Native uses
   font8/type-4 state colours.  There is no font11 `(5,0)` title, pet caption
   swap, zodiac label, state hit rect, or popup.
4. **Motive outer grid/order/title are mostly correct.**
   `UIMotiveSubpanel.cs:55,63-68,101-119,126-136` has the native desktop order,
   anchors and fonts.  The comment at line 102 is misleading: the rendered string
   is `Needs`, although the chunk label is `Motives`.
5. **Motive interior paint is fundamentally wrong.**
   `UIValueBar.cs:62-71,93-103,114-133` describes and paints a full 100x20 dark
   track, starts fill at `(0,0)`, makes it 20 pixels high, invents a stateless RGB
   lerp and a top highlight, and moves the caption to x=62.  Native bar is only
   `(20,12,80,17)`, has dynamic ramp histories and a black vertical endpoint, and
   gives base text the full clipped rect.  The line-106 comment also omits the
   `+100` normalization even though the method implementation includes it.
6. **Motive behavior is incomplete.**  No delta meter, animated SetVal history,
   live popup, or quick-tip/input parity exists in `UIOriginalMotiveGauge`.
7. **Mounting is correct but masks bad child coordinates.**
   `UIMainPanel.cs:947-960` correctly mounts Live subpanels at `(300,0)`; the
   Personality child x=334 values must not be treated as global coordinates.

## Existing-test blind spots

* `AutotestRunner.cs:6662-6865` pins the two string chunks and checks only that
  motive/personality glyph twins contain canonical words plus atomic switching.
  It does not assert Personality title, row rectangles, art, pet strings, zodiac,
  hit testing, tier popup, portrait, or state colours.
* `AutotestRunner.cs:14594-14635` checks the Motives host/outer grid and the three
  scalar fill widths only.  It cannot detect the wrong fill origin/height,
  full-widget backing rectangle, wrong caption clip, wrong colour/history,
  missing endpoint, missing delta meter, or missing popup.

A parity gate should render/probe the Motive bar pixels at raw `-100/0/+100`,
including the black endpoint and absence of pixels outside `(20,12,80,17)` except
native text/delta content.  Personality should pin all five rects, resources and
crop widths at `0/100/300/800/1000`, title/zodiac visibility, human/pet table
selection, and the caption-only popup hit region.

## Corrections to prior notes

`r144/toolbar-law.md` remains useful for the outer people-host geometry, but two
interpretations are superseded here by the focused class disassemblies:

1. a Motive's active/remainder track is 60x5 at `(20,12)`, not the full 100x20;
2. `cWinPeople+0x1bc` is zodiac text, not the selected Sim's name.
