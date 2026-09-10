# R143 — CREATE-A-CHARACTER / CREATE-A-FAMILY layout law (decoded from PPC binary)

Round target: the exact UI layout law of the Create-A-Character screen
(kDesignCharBkg 5042) and Create-A-Family screen (kDesignFamilyBkg 5022) of
The Sims 1 (PowerPC Mac port), continuing the r142 UCP methodology.

Binary: `game-data/The Sims/The Sims Complete` (PPC PEF, 6,486,820 bytes).
**All "addresses" in this document are raw FILE offsets** (== symbol-index
addresses). Code-virtual pointers stored in the data section map to file via
`file = value + 0x8E90` (see memory-map section).

DECODED = read directly from instruction immediates.
INTERPRETED = my reading of the semantics; the immediates are cited but the
behavioral conclusion involves inference.

---

## 0. Memory map (foundational, validated this round)

PEF container header (file 0x00..0x28), 24-byte section headers at 0x28:

| section | defaultAddress | totalLength | raw(container) | file offset |
|---|---|---|---|---|
| 0 (code) | 0x0 | 0x5B9458 | 0x5B9458 (unpacked) | **0x8E90** |
| 1 (data) | 0x0 | 0x989A4 (incl. BSS) | 0x7BF80 (PEF-packed) | **0x5C22F0** |

- Code: `file = code_virtual + 0x8E90`; code range in file = [0x8E90, 0x5C22E8).
- Data: r142's `tsui/data-sec1-unpacked.bin` IS the initialized image at
  data-virtual 0; BSS = [0x7BF80, 0x989A4).
- **TOC**: r2 = data + 0x8000. A `lwz rX, off(r2)` reads data word
  0x8000+off. That word is a pointer whose KIND is given by r142's
  `reloc-kinds.json`: kind **C** = code pointer (value is code-virtual,
  file = value+0x8E90), kind **D** = data/BSS pointer (value is the data-
  section offset directly).
- vtables: arrays of 4-byte data-relative pointers, each pointing at an
  8-byte TVector {code, TOC} pair. (Mixed 8/12-byte entries exist per the
  reloc stream; dereference two levels to resolve a virtual.)

## 1. Class inventory (symbol index = ground truth)

| class | ctor | Init | PrepareButtons | TSPaint | TSOnCommand | anchor writer |
|---|---|---|---|---|---|---|
| cWinDesignCharacter | 0x2cfc00 | **0x2ce610** (5252B) | 0x2cc9b0 | 0x2ce060 | 0x2cd3a0 | **0x2cfe54** (static-init) |
| cWinDesignFamily | 0x2d2150 | **0x2d1350** (3116B) | 0x2d0140 | 0x2d0ff0 | 0x2d05e0 | **0x2d2400** (static-init) |
| cWinPersonalityWidget | 0x2d8bd0 | 0x2d89e0 | — | 0x2d8580 | (mouse 0x2d7d10/0x2d7eb0) | — |
| cWinDesignFamPeopleBtn | (inlined) | — | — | (base cTSWinBtn) | — | — |
| cWinPickFamily (creator) | 0x2da310 | 0x2d99f0 | — | 0x2d96c0 | 0x2d9270 | 0x2d8bd0-adjacent |

The two anchor writers are unsymboled CW static-initializer functions (no
`bl` callers — run at load, like r142's UCP writer at 0x2b8230).

## 2. Screen/window geometry law

Chain of window rects (each screen copies its parent's rect via SetArea
vt+0x68 = cTSWin::SetArea @0x504860, storing +0x74/78/7c/80):

- cWinDesignFamily: created in `cWinPickFamily::HandleButton` @0x2d8f30;
  `SetArea(fam, pickFamily->0x74, 0x78, 0x7c, 0x80)` at 0x2d8fec-0x2d9008.
- cWinDesignCharacter: created in `cWinDesignFamily::TSOnCommand`
  (2 sites: 0x2d0658 Add, 0x2d08a4 Edit);
  `SetArea(cas, family->0x74..0x80)` at 0x2d067c-0x2d0694.

So both screens are full game-window size (800x600 in the standard mode).

**Background law** (identical in both Inits; CAS cite 0x2ce740-0x2ce7b4,
family 0x2d13f0-0x2d1468):

```
LoadBuffer(5042 /*CAS*/ or 5042->5022 family, &this->0x100, 4)
if (buf) {
    ox = clamp((bufW - winW)/2, >=0)   -> this->0x104   [see note]
    oy = clamp((bufH - winH)/2, >=0)   -> this->0x108
} else ox = oy = 0
```

- The emitted code is `subfc/subf/subfe/srwi/and`; the intended law is
  `ox = max(0, (artW-winW)/2)` (INTERPRETED — the and-mask picks the
  art-larger-than-window case). At 800x600 window: **ox = oy = 0**, so every
  anchor below equals art coordinates exactly.
- TSPaint (CAS 0x2ce14c-0x2d1d4, family 0x2d0ff0+): blits the 800x600 art
  **at native size** at (window.x + ox, window.y + oy) via parent vt+0xa0 —
  never scaled. If (ox>0 || oy>0) it also calls `PaintInsetDlgBorder`
  @0x4723d0 with a border buffer from BSS 0x92964 (dialog chrome for the
  clipped case).

## 3. THE CREATE-A-CHARACTER TABLE (screen-local coords, add (ox,oy))

### 3.1 The 13 top-level buttons

Built in `cWinDesignCharacter::Init` loop @0x2ce87c-0x2ceb14.

- Resource-ID array (13 u32) at code-virt 0x59b920 = **file 0x5a47b0**
  (TOC slot -0x4d58, kind C): `5028,5029,5035,5036,5037,5038,5039,5030,5032,5030,5032,5033,5034`
- Anchors: 13 {x,y} pairs built from immediates into stack r1+0xb0..r1+0x114
  (stores at 0x2ce688-0x2ce714).
- Per button: `LoadBuffer(res[i])`; `SetImage(buf, 4, 1)` (vt+0x1a4) →
  per-state W = sheetW/4, H = sheetH; `stb 1, 0x163` (flag);
  `SetArea(ax+ox, ay+oy, ax+ox+W, ay+oy+H)` (0x2ce928-0x2ce96c).
- Buttons 11..12 are `cTSSystemButton` (ctor @0x500880) with the buffer from
  `LoadBuffer(5301 kNghDefaultBtn)` at 0x2ce7e8 (art NbhdTileBtn.bmp 364x62),
  NOT 5033/5034: `SetArea(0,0,200,62)` then
  `CenterOn((ax+ox, ay+oy+31))` (0x2cea14-0x2cea88). That 200x62 rect is
  provisional: the later caption assignment uses font[12], changes the width
  to `textWidth+70`, and recenters it on the same anchor. The shipped English
  labels therefore finish at 109x62 (`Done`) and 121x62 (`Cancel`). The
  91x62 state source is painted with the native 45/1/45 cap/stretch/cap law.
  The kDesignCharDoneBtn/CancelBtn BMPs are never loaded by this port. See the
  later complete decoder in `../r183/r183-cas-system-button-law.md`.

| # | res id | art sheet | cols,rows | button WxH | anchor (ax,ay) | RECT (l,t,r,b) | identity |
|---|---|---|---|---|---|---|---|
| 0 | 5028 kDesignCharAdultBtn | 228x100 | 4,1 | 57x100 | (497,101) | (497,101,554,201) | Adult toggle (SetState(1) default, 0x2cf1c8) |
| 1 | 5029 kDesignCharChildBtn | 368x90 | 4,1 | 92x90 | (406,111) | (406,111,498,201) | Child toggle (SetState(0)) |
| 2 | 5035 kDesignCharFemaleBtn | 228x121 | 4,1 | 57x121 | (499,274) | (499,274,556,395) | Female |
| 3 | 5036 kDesignCharMaleBtn | 392x111 | 4,1 | 98x111 | (400,274) | (400,274,498,385) | Male |
| 4 | 5037 kDesignCharDarkBtn | 228x71 | 4,1 | 57x71 | (499,202) | (499,202,556,273) | skin Dark |
| 5 | 5038 kDesignCharMediumBtn | 184x71 | 4,1 | 46x71 | (452,202) | (452,202,498,273) | skin Medium |
| 6 | 5039 kDesignCharLightBtn | 220x71 | 4,1 | 55x71 | (396,202) | (396,202,451,273) | skin Light |
| 7 | 5030 kDesignCharSkinsLeftBtn | 72x34 | 4,1 | 18x34 | (580,291) | (580,291,598,325) | BODY prev ("UI_CAC_CycleParts") |
| 8 | 5032 kDesignCharSkinsRightBtn | 72x34 | 4,1 | 18x34 | (729,291) | (729,291,747,325) | BODY next |
| 9 | 5030 kDesignCharSkinsLeftBtn | 72x34 | 4,1 | 18x34 | (597,160) | (597,160,615,194) | HEAD prev ("UI_CAC_Cyclehead") |
| 10 | 5032 kDesignCharSkinsRightBtn | 72x34 | 4,1 | 18x34 | (714,160) | (714,160,732,194) | HEAD next |
| 11 | 5033 (unused) | 5301 system art | 4,1; 45/1/45 paint | 109x62 final | center (111,529) | (56,529,165,591) | DONE (system button) |
| 12 | 5034 (unused) | 5301 system art | 4,1; 45/1/45 paint | 121x62 final | center (400,529) | (339,529,460,591) | CANCEL (system button) |

Sound overrides at 0x2cf25c-0x2cf2b8: btn7/8 use string "UI_CAC_CycleParts"
(data 0x5c024+0x49), btn9/10 use "UI_CAC_Cyclehead" (+0x5b).
Buttons 11/12 get `SetOneShot(true)` (0x2cf238-0x2cf258). Buttons 7-10 get
`stw 0, 0xf0` (0x2cf1f8-0x2cf20c).

### 3.2 The 3D preview (cWinVitaBtn)

- `operator new(500)`; `cWinVitaBtn::ctor(5.0f)` @0x2dc700 — the float 5.0
  from code-const 0x59b954 = file 0x5a47e4 (spin/idle rate) — 0x2cf2ec-0x2cf304.
- Anchor (618,145) = BSS 0x94618 (anchor pair {618,145}); SetArea with
  anchor+size (0x2cf314-0x2cf358), then **resized:
  `SetArea(l, t, l+0x64, t+0xdc)` = 100 x 220** (0x2cf360-0x2cf37c).
- **Head/body 3D viewport RECT = (618,145)-(718,365)** (screen-local +
  (ox,oy)). The head arrows (597/714 @y160) and body arrows (580/729 @y291)
  flank it.
- HouseViewer prep at 0x2cf2bc-0x2cf2e8: `SetRotation(viewer,0,0,0,1)`,
  `SetScale(viewer,1,0,0,1)` (viewer = *(BSS 0x8510c)+4).

### 3.3 Text fields (cTSWinTextEdit2, 408 bytes, ctor 0x535470)

**Name field** (this->0x10c) — built 0x2cf3d0-0x2cf50c:
- anchor BSS 0x945f0 = {275,52} + size-quad BSS 0x945f8 = {0,0,255,25}
  → **RECT (275,52)-(530,77)**, 255x25 (0x2cf41c-0x2cf460).
- SetFont(font[10]); SetTextColor(0xFFFFFF); SetColors(4, color, color).
- `SetLinesAllowed(1)`; **`SetCapacity(16)` if language byte
  ([BSS 0x8510c]->0x64) == 15 else `SetCapacity(25)`** (0x2cf47c-0x2cf4a4);
  SetFlashFrameOnEmpty(1); SetDrawFrameOnFocus(0); SetTransparent(1);
  gets focus (TSSetFocus 0x2cf53c).
- name string key "UI_NHood_error" stored at +0x18c (0x2cf4dc-0x2cf4e8; the
  string pool at data 0x5c024, key at +0x6c) — INTERPRETED as the field's
  error-message key.

**Bio field** (this->0x110) — built 0x2cf54c-0x2cf664:
- anchor BSS 0x945d8 = {22,425} + size-quad BSS 0x945e0 = {0,0,761,96}
  → **RECT (22,425)-(783,521)**, 761x96 (0x2cf5a4-0x2cf5e8).
- `SetLinesAllowed(-1)` (multiline); **`SetCapacity(0x800 = 2048)`**
  (0x2cf62c); SetFont(font[10]); same colors; SetTransparent(1).
- Prefilled from PersonFinder::GetDescription (0x2cfa20-0x2cfa40).

**Hidden cheat-bio field** (this->0x1b0, cTSWinText 264B) — only if global
flag `byte[BSS 0x945b1] != 0` (0x2cec18-0x2cec24): SetWinTextFlag(2,1),
`SetCaption("body strings cheat")` (string at data 0x5c024+0x36, 0x2cecd4),
SetArea from anchor {618,145} with **y-200 → parked at (618,-55)**
(0x2cec90-0x2cecC8), then FitWindowToText. INTERPRETED: hidden
body-strings-cheat entry field; flag 0x945b1 is set elsewhere (not found this
round; ctor sets byte[0x945b0]=1 at 0x2cfd18).

**Labels** (cTSWinText, measured — no hardcoded rects):
- this->0x194 (title strip): SetCaption(GetString(16)) = **"CREATE A SIM"**
  (STR# 130 entry 15); RECT = (0, oy+6, winW, oy+6+measured+6)
  (0x2ceb50-0x2ceba0).
- this->0x198 (**"Bio for %s"** label, GetString(26) -> this->0x19c):
  anchored (22,425), vertically placed via two font measurements
  (y' = 425 - 2*textH, bottom = y'+textH) (0x2cedc4-0x2cee8c) — sits just
  above the bio field.
- Anonymous label (0x2ceedc): caption GetString(27) = **"Enter First Name:"**;
  right-anchored: right = 275+ox-20, top = 52+oy (0x2cef68-0x2cefb4).
- Anonymous label (0x2cefec): caption GetString(17) = **"PERSONALITY"**;
  **horizontally centered at x=198, y=114** (0x2cf078-0x2cf0e0:
  left = 198+ox - width/2).
- this->0x1a4 = GetString(25) = **"BIO"** (alternate bio label, 0x2ced54).

String text (GetString is 1-based into UIText.iff STR# 130 "DesignCharStrs";
extracted in this round's sibling files `uitext-130-DesignCharStrs.txt` /
`uitext-129-DesignFamilyStrs.txt`):
16='CREATE A SIM', 17='PERSONALITY', 18-22='Neat','Outgoing','Active',
'Playful','Nice' (the five trait captions, in widget order — matches trait
offsets {7,6,3,5,2}), 24='BIO', 25='Bio for %s', 26='Bio for %s' table,
27='Enter First Name:', 28='Astrological Sign' (points-button tooltip — the
table comment independently confirms: "Appears below the personality panel.
As personality points are assigned the different astrological signs appear
on the screen"). The Done-with-points-remaining dialog = entries 13/14
('More Personality!' / "You haven't used all the personality points...").

### 3.4 The personality panel (crown jewel)

Built by `BuildPersonalityBtns` @0x2cc430 (called from Init 0x2cf3c8) +
`cWinPersonalityWidget` (456 bytes, ctor 0x2d8bd0 takes the CAS window).

**Five widgets** (0x2cc484-0x2cc58c): `new cWinPersonalityWidget(this)`,
AddChild, append to this->0x158-array (ptrs at this->0x160, count at
this->0x15c), `SetImage(NULL, 4, 0)` (invisible base), SetFont —
**font[14] (idx 0x38/4) normally; font[12] (idx 0x30/4) if language 3..4**
(0x2cc45c-0x2cc480) — SetMinCaption(GetString(18+i)) i=0..4 =
**'Neat','Outgoing','Active','Playful','Nice'** (STR# 130 entries 17-21;
widget i therefore displays trait i, matching person-data offsets {7,6,3,5,2}
in the same order), SetMaxCaption("     " 5 spaces, data 0x5c024+0x29),
SetFlag(0x2000,1).

**Flow layout** (0x2cc590-0x2cc7b0):
- params from BSS: origin 0x94610 = {228,131}; pitch 0x94608 = {100,32}
  (column 100, row 32); bounds = window rect + (ox,oy); flag=1 selects
  vertical variant.
- Variant B (flag 1, active): for each visible widget:
  `TSWinMoveTo(w, pos)`; pos += (0, 32); if pos.y+32 > winBottom: wrap to
  next column (pos.x = 228+ox + col*100, pos.y = 131+oy).
- Then `SetLEDTopLeft(widget, &widget->0x74)` for i=0..4 (0x2cc7bc-0x2cc7d8)
  — the LED anchor point = the widget's position BEFORE ComputeLayout
  re-anchors it.
- **Law: trait row i at (228+ox, 131+oy + 32*i).**

**ComputeLayout** @0x2d8020 (called by SetMinCaption/SetMaxCaption/
SetLEDTopLeft):
- measures minCaption and maxCaption with the font (vt+0x58 of cITSFont);
- new widget rect = (led.x - minCapH - 14, top, led.x + cellW + 4 + maxCapW,
  top + max(cellH+4, minCapW+4)) where cellW/cellH = the LED sheet cell
  (art 0x945... i.e. widget->0x190 fields 0x14..0x20 = {0,0,59,25});
- pip-group offsets (relative to widget): group1 (min caption) at (2,0);
  group2 (max caption) at (cellW + minCapH + 16, 0) — stored 0x1a8/0x1ac and
  0x1b0/0x1b4 (0x2d81a4-0x2d8204); captions are drawn there in TSPaint
  (0x2d868c-0x2d8770) via font vt+0x80.

**The LED pips** — `cWinPersonalityWidget::Init` @0x2d89e0:
`LoadBuffer(5040 kDesignCharPersLED, &widget->0x190, 4)` — each widget owns
a copy of the 59x25 sheet. Cursors: `GetCursor(0x20)` -> +0x1b8 (plus),
`GetCursor(0x24)` -> +0x1bc (minus) (0x2d8a80-0x2d8b00).

`TSPaint` @0x2d8580 pip draw (0x2d8790-0x2d8898):
```
if (value > 0):
    w = value * 6                      // PIP PITCH = 6 px
    if (w > sheetW) w = sheetW         // clamp to 59
    src = (0, 0, w, 25)   // prefix of the 59x25 sheet
    dst = src + (origin.x + left - led.x, origin.y + top - led.y)
    parent->vt+0xa0(LEDbuf, &src, &dst, 0)
```
**THE LED LAW: one 59x25 strip per trait; lit width = value x 6 pixels,
clamped to 59 (so 10 pips x 6 = 60 -> 59, the last pip is 1px short).**

**Value semantics** — `SetValue` @0x2d84e0: `v<0 -> 0; v>10 -> 10; store
widget->0x18c; notify vt+0x170`. `GetValue` @0x2d84a0 returns widget->0x18c.

**Point pool** — CAS member this->0x11c, initialized to **25** in the ctor
(0x2cfcb8: `li r0, 0x19; stw r0, 0x11c`). Init's new-person loop
(0x2cf908-0x2cf950): `SetValue(widget, raw/100); pool -= value` — so the
pool counts DOWN from 25 as points are spent. The raw person-data encoding:
`raw = value * 100` (write-back `mulli r0, r3, 0x64` at 0x2ccf88 and
0x2cd55c); read-back uses magic 0x51EB851F>>5 (lis 0x51EC/addi -0x7AE1 at
0x2cf8ec-0x2cf8f4) = exact signed /100. Trait byte-offsets into person data
(code-const table 0x59b748 = file 0x5a45d8): **{7, 6, 3, 5, 2}** (x2 for the
s16 fields).

**Click behavior** — `TSOnMouseMove` @0x2d7eb0 (hover zones) and
`TSOnMouseDownL` @0x2d7d10:
- Hover: if value==0 -> plus-cursor (0x1b8), zone=+1; if value==10 ->
  minus-cursor (0x1bc), zone=0; else boundary = led.x - left +
  value*(sheetW/10) (magic 0x66666667 >> 2 = /10; 59/10 = 5):
  mouseX > boundary -> plus zone (right of lit pips), else minus zone.
  NOTE: draw pitch is 6px, click pitch is 5px (engine truth).
- Click: if plus zone: if value==10 do nothing; if pool(this->0x1c4->0x11c)
  == 0 -> PlaySoundA(data 0x5ceb0+0x11) (deny); else value=clamp(v+1),
  pool -= 1, PrepareButtons, PlaySoundA("UI_CAC_personpts" at data 0x5ceb0).
  If minus zone: if value==0 do nothing; value=clamp(v-1), pool += 1,
  PrepareButtons, same sound.

**Remaining-points bar** — kPeoplePtsBuffer 5203 (149x25) loaded ONCE into
CAS this->0x180 at 0x2cf8a4. Drawn in CAS `TSPaint` @0x2ce1d8-0x2ce298:
```
dst = (117+ox, 342+oy)  // anchor BSS 0x945d0 = {117,342}
w = pool * 6            // SAME 6px pitch, clamp to 149
src = (0, 0, w, 25)     // left prefix of the 149x25 sheet
```
**Points bar = 25 segments x 6px = 150 -> sheet 149px** (last seg 5px wide).

**Points button** (this->0x1ac, cTSWinBtn) — 0x2cf66c-0x2cf7cc:
- `SetFont(font[12])`; `SetImage(NULL, 4, 1)` (invisible);
  **RECT from quad BSS 0x945c0 = {129,290,249,310} -> (129,290)-(249,310)**;
  sub-rect (0,0,120,20) into +0x114..0x120; flags 0x114=0, 0x118=0,
  0x11c=120, 0x120=20, 0x15c=1, 0x158=1; tooltip GetString(28) via 0x504ad0;
  SetFlag(0x400,0), SetFlag(0x2000,1).
- Zodiac (PrepareButtons 0x2ccfc8-0x2cd140): if pool < 25:
  `ComputeZodiacSign(data)` -> data->0x8c; caption = "(" + GetZodiacName +
  ")" via SetCaption, SetFlag(0x400,1). If pool == 25: caption "" (string at
  data 0x5c024+0x35), SetFlag(0x400,0).

### 3.5 Carousel / suit cycling (TSOnCommand @0x2cd3a0)

- BODY prev/next (btn7/8): `this->0x16c --/++` with wraparound against
  `CountAvailable*(person)` (0x23ee10; wrap 0x2cd6c0-0x2cd728); sets
  SanifySuits flag 0x1b4, PrepareButtons, then unsymboled helper 0x2cc1c0
  (applies suit; INTERPRETED).
- HEAD prev/next (btn9/10): this->0x168 with count from 0x23e780
  (0x2cd748-0x2cd7dc).
- Age/gender switch (btn0/1 Adult/Child, 0x2cd7e0-0x2cd870): remembers
  {bodySuit, headSuit} per (age x gender) in a 4x2 table at this->0x1b8
  (row stride 16, entry stride 8), restores on switch, then SetAge.
- Done (cmd 1, btn 0x150): if pool > 0 -> MessageDialog warning (buttons=4,
  0x2cd410-0x2cd45c); on confirm: SetName (strncpy clamp 0x801),
  SetDescription, personality write-back loop (value*100), SetupNewHDSkins
  if child, `EndDesignAPerson(person, flag, 1, 1)`, notify parent {3, 0x1f,
  person}.
- Cancel (cmd 0x17, btn 0x154): EndDesignAPerson(0,1,1) + DeletePerson if
  the person was new (0x2cd654-0x2cd698).

## 4. THE CREATE-A-FAMILY TABLE

### 4.1 Buttons (Init loop 0x2d1a80-0x2d1ca4)

Res array at code-virt 0x59b958 = **file 0x5a47e8**: {5023,5024,5025,5026,5027}.
Anchor pairs at BSS 0x94658 (stride 8): (593,103),(594,208),(594,311),
(200,529),(600,529).

| # | res id | art | cols,rows | WxH | anchor | RECT | identity |
|---|---|---|---|---|---|---|---|
| 0 | 5023 kDesignFamilyAdd | 556x103 | 4,1 | 139x103 | (593,103) | (593,103,732,206) | Add person (TSWinMoveTo) |
| 1 | 5024 kDesignFamilyDelete | 596x99 | 4,1 | 149x99 | (594,208) | (594,208,743,307) | Delete person |
| 2 | 5025 kDesignFamilyEdit | 548x99 | 4,1 | 137x99 | (594,311) | (594,311,731,410) | Edit person |
| 3 | 5026 (unused) | 5301 system art | 4,1; 45/1/45 paint | 109x62 final | center (200,529) | (145,529,254,591) | DONE (cTSSystemButton) |
| 4 | 5027 (unused) | 5301 system art | 4,1; 45/1/45 paint | 121x62 final | center (600,529) | (539,529,660,591) | CANCEL (cTSSystemButton) |

Buttons 0-2: cTSWinBtn, SetImage(buf,4,1), `stb 1,0x163`, positioned by
`TSWinMoveTo(anchor + (ox,oy))` (0x2d1b0c-0x2d1b38), tooltip via 0x504ad0,
SetFlag(0x2000,1), SetOneShot(true) (0x2d1cc8-0x2d1cf4).
Buttons 3-4: cTSSystemButton (art = LoadBuffer(5301) at 0x2d13bc into
this->0x178), provisional `SetArea(0,0,200,62)` + CenterOn
(0x2d1bc0-0x2d1c34), followed by the caption-driven `textWidth+70` resize and
recenter — the same law as CAS Done/Cancel. kDesignFamilyDone/Cancel BMPs are
never loaded.

Enabling law (PrepareButtons 0x2d038c-0x2d04a4):
- Add (0x140): enabled iff personCount < 8 AND family-name field non-empty.
- Delete (0x144) & Edit (0x148): enabled iff selected member index
  (this->0x13c) != -1.
- Selected member slot: `SetState(1)` (0x2d04a8-0x2d04c4).

### 4.2 Family name field (this->0x10c, cTSWinTextEdit2) — 0x2d14d8-0x2d1648

- anchor BSS 0x94650 = **{274,50}** + size-quad BSS 0x94640 = {0,0,255,25}
  → **RECT (274,50)-(529,75)**, 255x25 (same geometry as the CAS name
  field).
- SetFont(font[10]); SetTextColor(white); SetColors(**0x25** normally,
  **5** if byte([BSS 0x8510c]->0x34) == 1 — border style variant).
- **SetCapacity(24)** (0x2d15ec); SetLinesAllowed(1);
  **SetFilterFileChars(1)** (family name becomes a file name);
  SetFlashFrameOnEmpty(1); SetTransparent(1); gets focus.

### 4.3 Member slot list — 8 x cWinDesignFamPeopleBtn

Created 0x2d176c-0x2d184c: `new cTSWinBtn` then vtable overwrite with
cWinDesignFamPeopleBtn vtable at DATA 0x5c540 (+0x14 Init = cTSWinBtn::Init,
+0x1a4 = cTSWinBtn::SetImage @0x50c0f0); backref this->0x18c = family->0x170.
- `SetArea(0,0,0x55,0x69)` → **each slot 85 x 105** (0x2d17a4-0x2d17c0);
  SetEnableDoubleClicks(1); SetFlag(0x2000/0x400/0x1000, 1).
- **Grid flow** (0x2d1850-0x2d1a5c): region rect = quad BSS 0x94628 =
  {0,0,440,360} + point BSS 0x94638 = {111,121} → region
  **(111,121)-(551,481)**, offset by (ox,oy); pitch quad {col 120, row 140}
  (immediates 0x78/0x8c at 0x2d18c0-0x2d18f0). CAS-01 correction: the flow flag at stack+0x108 is initialized to zero
  (0x2d1880), so the **horizontal** branch is active. Each visible slot
  TSWinMoveTo(pos); pos += (120,0); wrap to the next +140 row past the right edge.
- **Slot top-lefts: col x = 111/231/351/471, row y = 121/261.**

Portrait fill (PrepareButtons 0x2d170-0x2d32c), per person i < count:
- slot->0x190 = person handle; `GetPictureBuffer(person)` (0x244de0) →
  `AsBuffer` → **`SetImage(slot, portrait, 4 cols, 3 rows)`** — the portrait
  is a 12-state sheet (4x3); per-state size = portraitW/4 x portraitH/3.
- SetArea re-affirmed 85x105 (0x2d01e4-0x2d0224).
- **Portrait offset inside slot = BSS 0x94620 = {15,10}** stored to
  slot->0x13c/0x140 (0x2d022c-0x2d0248) — INTERPRETED as the portrait blit
  position within the 85x105 cell.
- Caption = PersonFinder::GetName (0x2d0268-0x2d0288); SetFont(font[10]);
  name text sub-rect stored at slot +0x114..0x120 =
  **(0, 105-(2*textH+2), 85, 105)** (0x2d02c0-0x2d02f4). CAS-01
  corrected the reversed subtraction and traced centered DrawTextPara wrapping
  with parent clipping; see the current [native contract](../../../../coordination/evidence/CAS-01/native-contract.md).
- Unused slots hidden via vt+0xa0 (0x2d0360-0x2d0388).
- cWinDesignFamPeopleBtn::GetToolTipsWindow override at 0x2d2360: routes to
  cPickFamilyPersonInfo::SetPerson for the tooltip.

### 4.4 Family labels

- this->0x17c (cTSWinText): SetFont(font[16]); strip
  (0, oy+6, winW, oy+6+measured+6) (0x2d1dd8-0x2d1e40) — same top strip as
  CAS; SetCaption(GetString(10)); SetAlignment(2); SetBackgroundOpaque(0).
- this->0x198 (cTSWinText): SetCaption(GetString(13)) = **"Enter Last
  Name:"**; FitWindowToText; SetArea at (ox-w-20, oy, ox-20, oy+h) — parked
  offscreen-left in this build (INTERPRETED: the label is laid out relative
  to the name field but positioned left of the window; the engine may re-
  place it at runtime) (0x2d1668-0x2d1764).
- Message strings: this->0x168 = GetString(14) = "Finished?",
  this->0x16c = GetString(15) = "Are you sure you are finished?..."
  (Done-confirm dialog) (0x2d1eb8-0x2d1eec).
- Button captions/tooltips from STR# 129: Add='Add a New Sim',
  Delete='Delete Selected Sim', Edit='Edit Selected Sim', Done/Cancel
  system-button captions 'Done'/'Cancel'; Delete/Cancel confirm dialogs are
  entries 5-8.
- StringSet = LoadUIStrings(129, str9) (0x2d14b4); CAS uses table 130
  (0x2ce838). Text extracted in `uitext-129/130-*.txt` (this directory).

## 5. Colors — `InitSimsColors` @0x25d600 (freestanding, symbol-indexed)

Each color: `colorMgr->vt+0x44(obj, R, G, B)` returns an index, stored
(low 16 bits) into a BSS global. Decode (call sites cited):

| global | RGB | call site |
|---|---|---|
| 0x9298c | (0xA6,0xB4,0xB4) | 0x25d644 |
| 0x92988 | (0xC3,0xCD,0xCD) | 0x25d680 |
| **0x92984** | **(0xFF,0xFF,0xFF) white** | 0x25d6b8 |
| 0x92980 | (0x00,0xFF,0xFF) cyan | 0x25d6f0 |
| 0x9297c | (0x40,0x5D,0x5F) | 0x25d728 |
| 0x92978 | (0x00,0x00,0x52) | 0x25d760 |
| 0x92974 | (0x00,0x00,0x4A) | 0x25d79c |
| 0x92970 | (0xD1,0x00,0x00) | 0x25d7d8 |
| 0x9296c | (0x00,0xCA,0x39) | 0x25d814 |
| 0x92968 | (0xFF,0x00,0xFF) magenta | 0x25d850 |
| 0x92964 | (0x00,0x32,0x00) | 0x25d88c |
| 0x92960 | (0x88,0x2E,0x2F) | 0x25d8c8 |
| 0x9295c | (0x20,0x93,0x4C) | 0x25d904 |

**The CAS/family name+bio text color = 0x92984 = WHITE** (SetTextColor at
0x2cf404/0x2cf57c/0x2d1508). `SetColors(4, white, white)` (border style 4)
in CAS; family uses SetColors(0x25|5, white, white). The LED buffer prep
call uses color 0x92968 (magenta) as its keying color (widget Init 0x2d8a44:
`buffer->vt+0x70(*(BSS 0x92968))` — INTERPRETED: transparency keying).

Color reg pairs (r1+0x50 / r1+0x40) are registered with the framework ctrl
mgr as palettes 6 and 4 (0x25d950-0x25d9a0).

## 6. Fonts — the 23-slot engine font table

`InitSimsColors` tail (0x25d9a4-0x25da78): the object at BSS 0x9743c yields
a font factory (vt+0x18); for i in 0..22 (0x17):
```
font[i] = factory->vt+0x18(1, i, 0, 0)      // stored at BSS 0x92900 + 4*i
if font && font->vt+0x20() == i: ok[i] = 1 (BSS 0x928e8+i); font->vt+0x28(color 0x92988)
else destroy + slot = 0
fallback font at 0x928e4 = factory->vt+0x18(1, 0xC, 0x10, 0)
compaction: empty slots copy the previous slot (fill-down)
```
CAS/family usage (offset into 0x92900 table → font index; every use is
`lwz r4, off(r30)` with r30 = 0x92900 followed by SetFont):
- **+0x28 = idx 10** — CAS name edit (0x2cf3f0), CAS bio edit (0x2cf56c),
  CAS points label 0x198 (0x2cedb0), family name edit (0x2d14f8),
  member-slot captions (family PrepareButtons 0x2d029c)
- **+0x30 = idx 12** — CAS points button 0x1ac (0x2cf68c), the two
  anonymous CAS labels (0x2cee fc, 0x2cf00c), personality widgets when
  language 3..4 (0x2cc480)
- **+0x38 = idx 14** — personality widgets, default (0x2cc45c)
- **+0x40 = idx 16** — CAS name-hint strip 0x194 (0x2ceb3c), family top
  strip 0x17c (0x2d1ddc)
- **+0x20 = idx 8** — CAS cheat-bio field 0x1b0 (0x2cec4c)
Font index → resource/size mapping is inside the factory (cITSFontSys) and
was NOT decoded this round (residual).

## 7. Cross-check vs art (all rects fit 800x600)

- CAS: max right = 783 (bio), max bottom = 591 (Done/Cancel) — all within
  800x600. Personality rows 131..291, points bar 342, points button 290-310
  (immediately below last trait row 259+32) — consistent stack.
- Head arrows (597/714 @160..194) and body arrows (580/729 @291..325) flank
  the 100x220 VitaBtn viewport (618..718) — tight, matches the real screen.
- Skin tone row Dark/Medium/Light spans x 396..556 contiguous; gender toggle
  Male/Female 400..556 contiguous; Adult/Child 406..554 — segmented-toggle
  look reproduced exactly by engine rects.
- Family: slots grid 111..(111+3*120+85)=556 wide x rows 121/261;
  buttons at 593..743 right side; name field (274,50)-(529,75) top-center.

## 8. Files (this round's dumps, in r143/)

- `cas-layout-law.md` — this document.
- `casdis.py`, `rdump.py` — capstone annotators (TOC-slot + reloc-kind aware).
- `cas-disasm-cawindesigncharacter-init.txt` (1313 ln) — CAS Init.
- `cas-disasm-cawindesigncharacter-ctor.txt` — CAS ctor.
- `cas-disasm-cas-anchor-writer.txt` — CAS static anchor init (0x2cfe54).
- `cas-disasm-cawindesigncharacter-preparebuttons.txt` — CAS PrepareButtons.
- `cas-disasm-cawindesigncharacter-tspaint.txt` — CAS TSPaint (bg + points bar).
- `cas-disasm-cawindesigncharacter-tsoncommand.txt` — CAS TSOnCommand.
- `cas-disasm-buildpersonalitybtns.txt` — personality panel construction.
- `cas-disasm-cwinpersonalitywidget.txt` — full widget class (all methods).
- `cas-disasm-cawindesignfamily-init.txt` — family Init.
- `cas-disasm-cawindesignfamily-preparebuttons.txt` — family PrepareButtons.
- `cas-disasm-cawindesignfamily-tspaint.txt` — family TSPaint.
- `cas-disasm-family-anchor-writer.txt` — family static anchor init (0x2d2400).
- `cas-disasm-initsimscolors.txt` — color/font initializer.

## 9. Work log (search path, for future rounds)

1. Read r142 law doc + rt-inventory + capdis; noted r142's BSS/TOC model.
2. Symbol-index grep for "Design|Personality|PickFamily" → full class maps
   for cWinDesignCharacter (0x2cc3a2-0x2cffd2), cWinDesignFamily
   (0x2cffd2-0x2d22d2), cWinPersonalityWidget (0x2d7ca2-0x2d8bd2),
   cPickFamily* (0x2c66f2-0x2cbe22). Disassembled Init functions with
   capdis (worked at symbol-2 for function starts).
3. TOC-slot values exceeded the data section → memory-map investigation:
   parsed the PEF header/section table at file 0x28 (24-byte headers) —
   code base 0 @0x8E90, packed data container @0x5C22F0. Proved TOC kind-C
   values are code-virtual: the CAS res-ID array 0x59b920 → file 0x5a47b0
   contains exactly 5028,5029,5035,... — the 13 CAS button IDs. This also
   located the family res array at 0x5a47e8. Cross-validated with r142's
   reloc-kinds.json ('C' vs 'D' words).
4. The 13 button anchors were immediates in Init's prologue (stack array),
   not BSS; the BSS anchors (0x945xx-0x946xx) had a single writer each —
   unsymboled static-init functions found by scanning all `lwz rD, off(r2)`
   referencing slots -0x4d44..-0x4d1c: CAS writer 0x2cfe54 (between ctor
   and cWinDesignFamily, hidden behind CW symbol trailers), family writer
   0x2d2400 (after cWinDesignFamPeopleBtn dtor).
5. vtable decoding: r142's "slot N at vtable+N" needed two-level
   dereference (vtable word -> TVector{code,toc}); validated against
   cTSWinBtn SetImage 0x50c0f0; recovered cTSWinText slots (SetCaption/
   SetFont/FitWindowToText/SetAlignment/SetWinTextFlag/SetBackgroundOpaque)
   and used symbol-index names for cTSWinTextEdit2 setters.
6. Personality panel: BuildPersonalityBtns loop → 5 widgets, flow grid;
   ComputeLayout → pip group offsets; TSPaint → the 6px pip law; SetValue →
   0..10 clamp; ctor → pool 25; TSOnMouseDownL/Move → plus/minus zones,
   pool accounting, sounds. Division magic initially mis-converted to hex
   by my script (0x51E4851F); corrected: 0x51EC0000-0x7AE1 = 0x51EB851F =
   exact /100 — matches write-back mulli x100.
7. Points bar: grep for `li r3, 0x1453` (5203) → single site in Init →
   this->0x180; consumed in TSPaint with the same value*6 law.
8. Window sizes: found creators via bl-scan to the ctors; both screens copy
   the parent rect (PickFamily → Family → Character).
9. Colors/fonts: color global 0x92984 → TOC slot scan → all refs →
   `InitSimsColors` @0x25d600 decoded (13 RGB constants + 23-font table +
   fallback/compaction).
10. Dead ends: no "EditField" symbols (real class = cTSWinTextEdit2);
    scanning the code gap 0x2c34d2-0x2c66f2 for the anchor writer (it's
    DirectSound code); positive-offset TOC slots (+0x4d4c0..) have no
    addis/lwz users (not real code refs); the font-identity chain stops at
    the factory virtual.
11. String text resolved via the sibling round's UIText.iff extraction
    (`uitext-130-DesignCharStrs.txt`, `uitext-129-DesignFamilyStrs.txt`):
    every GetString id used by CAS/family code maps 1-based onto the
    entries, independently confirming the label identities, the trait
    order (Neat/Outgoing/Active/Playful/Nice), the "Astrological Sign"
    tooltip on the points button, and the Done-with-unspent-points dialog.

## 10. Residuals (not recovered this round)

- Font index → font resource/point-size mapping (inside cITSFontSys
  factory vt+0x18) — CAS needs indices 8,10,11,12,14,16.
- Who sets cheat flag byte[0x945b1] (bio cheat field) — writer not found.
- `cTSSystemButton` was subsequently decoded completely in R183: 62px height,
  font[12], caption-driven final width, 45/1/45 painting, state colors, and
  release-inside interaction. See `../r183/r183-cas-system-button-law.md`.
- The unsymboled helpers 0x504ad0 (INTERPRETED: cTSWinBtn::SetToolTip) and
  0x2cc1c0 (suit-apply after carousel step).
- The ox/oy `and`-mask emits `max(0,(art-win)/2)` intent but the literal
  instruction sequence would produce a huge unsigned value in the
  art-larger case; TSPaint's `ox>0` guard then paints the inset border.
  Only matters for window sizes < 800x600.
- Family "Enter Last Name:" label computed offscreen-left in this build —
  whether it is repositioned at runtime was not traced.
- CAS-01 subsequently traced the active flow branch: four columns and two
  rows, replacing the incorrect vertical-flow interpretation above.

NOTE: this directory is shared with a concurrent sibling round (neighborhood
screens): `nbhd-*.txt`, `nbhd-layout-law.md`, `client-cas-survey.md`, and the
`uitext-*.txt` string extractions (which this document cites) are theirs.
