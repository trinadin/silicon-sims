# R238 recovered Help composition

Independent source audit and owned-engine decode overturn the R162/R195 pane
interpretations. `recovered-help.txt` contains the fresh raw-file-address
instruction evidence and the original executable's SHA-256.

## Measured geometry

`sinit` at `0x278530` writes these globals through their TOC slots:

| TOC | BSS | Value | Role |
|---|---|---|---|
| -0x5054 | 0x932b0 | (20,20) | Topic-list origin |
| -0x5050 | 0x932a8 | (250,250) | Preliminary topic width; first coordinate used |
| -0x504c | 0x932a0 | (270,20) | Preliminary body origin |
| -0x5048 | 0x93298 | (340,210) | Preliminary body dimensions; final width stays340 |
| -0x5044 | 0x932c0 | (50,50) | Preliminary dialog origin |
| -0x5040 | 0x932b8 | (625,300) | Preliminary dialog size |

The only direct UCP caller at `0x2b5370–78` passes **font-table slot12**.
This is the topic-list font, stored in Help `+0x10c`.

Help constructs the actual **cTSWinTextList at +0x100**, through explicit
constructor `0x537680`. It sets visible rows `+0xe0=12`, applies the caller
font, and establishes a preliminary height `12*font.TSCharHeight`.
`ChildAdd` calls `Init` before mounting the child (see independent shared
list evidence in `r238-phonebook/child-add.txt`); that list initialization
sets its final height to **12*(TSCharHeight+3)**. For font12 this is312px.

Init measures all topic titles through the same font `TSStringWidth` and
sets list width to **widest title+25** at `0x278144–74`. English's widest
title is `Build Mode - Delete`, 154px, so width=179px. The preliminary250px
width is only used if the measured maximum is zero.

`+0x108` is the **body label**, not another list: CtrlMgr vtable+0x30
resolves to `DefaultLabel` `0x50fa30`, which constructs `cTSWinText`.
That factory uses system font0 (font-table11 per R175), alignment0, and the
constructor's2px horizontal/vertical gutters. The body text is white/gray
system ink, with natural font11 line height21px and word wrapping.

After the list is mounted, `0x278208–34` sets body rect to
`(list.Right+20, list.Top, list.Right+20+340, list.Bottom)`. This final
SetArea supersedes both its initial210px height and the earlier Thai-only
10px width reduction. `0x27823c–84` sizes the dialog from the body right
edge plus20px and the list bottom plus twice the close-button height.

## Button and title corrections

The button created by `DefaultPushBtn` is WinBtn with a **100px minimum
width** (factory `0x50fc2c–58`) and33px art height. `GetButtonLabel(3)`
returns **OK**, not Close: the third global string's initializer at
`0x524f4c–58` reads DATA`0x7594c+0xc2`, the literal `OK`; the game can
localize that system label. The port uses STR152[0], the system OK label.
This mapping is directly proved at `0x256cdc` (load STR152), `0x256cf4`
(GetString1, the engine's one-based first entry), and `0x256d10–14`
(SetButtonLabel3).

Final placement at `0x27828c–310` is horizontally centered,
`y=list.Bottom+floor(button.Height/2)`. Escape and Enter both dispatch the
same button at `0x277910–980`.

There is no separate Help title window or title-paint path. The former
port's title at(20,14) was invented and would overlap the recovered list
origin. Removing it is part of restoring the original composition.

## English fixture and implementation boundaries

| Surface | Native rectangle |
|---|---|
| Dialog | 579×398, centered in the current viewport, scale1 |
| Topic list | (20,20,179,312), 12rows at26px |
| Body | (219,20,340,312), text inset2px |
| OK | (239,348,100,33) |

The list background is sampled from the center of the original frame tile
as the original does at `0x277fbc–278088`; it must not be confused with
the list rows' foreground state colors. The shared TextList recovers the
scrollbar/row implementation separately. Body clipping is local to this Help
class, so long/localized text cannot cross the neighboring pane or footer.
All47 owned English bodies fit comfortably in the recovered body rectangle.

The existing47-topic English census and selected-topic persistence remain.
The old ScrollBy implementation immediately forced the viewport back to the
selected row, preventing independent scrolling; the shared list corrects
that and supplies pointer scrolling, scrollbar controls, and keyboard
navigation. Native wrapping edge cases and non-English runtime behavior need
original-runtime comparison; this restoration does not implement tutorials.
