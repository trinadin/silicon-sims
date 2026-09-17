# cTSPieMenu label paint law — TSPaint + DrawFramedLabel decoded (UI-05 fix D)

2026-09-16 investigation window (user report: "action bubbles look wrong — too
small / font too big / color off"). Binary: `game-data/The Sims/The Sims Complete`
(PPC, addresses = file offsets). Companion disasms regenerable with
`python3 tools/iff-dump/ppc_decode.py "<binary>" 0xSTART 0xEND`:

- TSPaint 0x5288b0–0x528b80 (symbol trailer `TSPaint__10cTSPieMenuFv`)
- DrawFramedLabel 0x528bb0–0x528cbc (`DrawFramedLabel__10cTSPieMenuFPCciP8cITSFontUlUliii`)
- DrawLabelFrame 0x528d10 (banked r142 as piemenu-drawlabelframe.txt)
- Layout 0x5290c0 (banked r142), SetSelection 0x527a70, cTSPieMenu ctor 0x52a4f0,
  lazy font fetch 0x52a040–0x52a098, interaction-pie init in
  cDDDSimsView::Init 0x218df0–0x218efc.

## Item rect law (the bubble size)

Layout measures EVERY item label through the font measure vtable+88 and builds
the item rect as **label bbox + 3px per side** (±3 insets at 0x529228–0x529258
and 0x5293a0–0x5293c0: `w = (right+3)−(left−3)`, `h = (bottom+3)−(top−3)`).
DrawFramedLabel draws the text inset (left+3, top+3) inside that rect
(0x528c78/0x528c80) and the frame (tile 9-patch) around it. **Bubble height
grows with the label; padding is 3px on all four sides.** There is no fixed
bubble height anywhere in the native pie.

## Color law (TSPaint 0x528a74–0x528a9c) — CORRECTED in UI-15

Per item, one state color is selected and passed with +100 to DrawFramedLabel.
Raw words (0x528a74 `lwz r0,204(r31)`; 0x528a7c `cmp r0,r23`; 0x528a80
`0x4082001c` = **BNE** (BO=4), not beq; 0x528a88 `0x4182000c` = BEQ):

- `item index != field+204` (**untracked**) → **+208 = RGB(185,185,208)**
  ("lowlight")
- `item index == +204` (tracked) && press flag (0x37380(1)/(2)) **clear** →
  **+212 = RGB(0,255,255)** (highlight cyan)
- tracked && press flag **set** → **+216 = RGB(255,255,255)** (selected/white)

So **untracked labels draw the muted (185,185,208)**; the tracked (hovered)
item draws cyan idle and flips WHITE while the press is held on it. (The
first version of this section and the r251 "corrected" law both read the
0x528a80 branch as beq — the raw BO=4 word is bne, so the roles invert: +208
belongs to the untracked majority, and the cyan/white pair belongs to the
tracked item.)
The decoded +100 label color RGB(187,187,187) is **not the text color** —
DrawFramedLabel passes it to DrawLabelFrame (0x528bd4: `r4 = r8`, vtable+420)
as the frame/tile tint. There is no hover recolor beyond the +204 tracking
itself.

## Font law

The pie never sets a font. cTSPieMenu field +220 lazily fetches the app
default at first use (0x52a050: global → vtable+24 → vtable+28 → +220); the
interaction pie's init (0x218df0–0x218efc) only sets: bg buffer RT 825
(`cpanel\ViewMenuBackground.bmp`), +100 = RGB(187,187,187), vtable+476(6),
**vtable+488(90)** (max radius), vtable+596(0), +372=6, +376=40, +80=owner,
title string via vtable+432. **The default font's size is not statically
pinned** — the fetch goes through the TOC-indirected framework global (same
disclosed limit as the GenDlg border widths). Font metrics measured from
UIGraphics.far `Fonts\variablesans_{10,11,12,14}.ffn` (r140 file law): 'Walk
Here' = 59/66/75/87 px wide, max ink height 12/14/15/18 px.

## Port deltas found (Simitone.Client/UI/Panels/UIPieMenu.cs + engine UIButton)

1. Bubble height hard-fixed at 17px (UIButton 3-slice uses the 17x17 PieButt
   tile's height as the control height); Size-12 label ink (up to 15–17px)
   is centered in a 15px box → text touches/overflows the caps. Native is
   label+6px with 3px padding all round.
2. Normal text color (187,187,187) where native draws WHITE; hover=white /
   pressed=cyan mapping has no native counterpart (native recolors via +204
   during press-tracking); ColorMod items draw (185,185,208) with no native
   per-item branch found in TSPaint (item flag +8 hides instead).
3. PieButt tile drawn untinted; native tints the frame with +100 (187-gray).
4. Overflow stack (items >8) still uses `TextureGenerator.GetPieButtonImg`
   (the pre-R142 generated pill) instead of PieButt.
5. Background disc grows to 200x200 in code; R142's own law text says the
   disc grows to 180 (radius 90).
6. "Back" title button caption forced to cyan; native title draws in the
   selected/white family.
