# R169: original `cWinPictureDialog` layout, paint, and color law

## 1. Scope and evidence boundary

This note decodes the original PowerPC `cWinPictureDialog` used by the desktop
picture-modal path, including the automatic-object dialog that the current
`UIMobileAlert` path is intended to reproduce.

The decode uses only the owner's local original executable, its recovered PEF
symbols/data, and locally inventoried original resources. No external source or
replacement-engine behavior is used to derive the law.

Source executable:

```text
game-data/The Sims/The Sims Complete
SHA-256 33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f
```

PEF addressing used below:

```text
code virtual 0 corresponds to file offset 0x8e90
packed data container: file+0x5c22f0, length 0x6d834
unpacked data length: 0x7bf80
TOC base within unpacked data: 0x8000
```

Unless explicitly labelled otherwise, code addresses are executable **file
offsets**. “Proved” means directly recoverable from PPC instructions, relocated
PEF data, or the original resource bytes. “Disclosed” marks an interpretation
that is useful to the port but is not encoded by the instruction sequence
itself.

## 2. Relevant symbols and object fields

```text
0x295050  cWinPictureDialog::TSPaint(bool)
0x295550  cWinPictureDialog::TryLayout()
0x295d50  cWinPictureDialog::DoLayout()
0x2962b0  cWinPictureDialog::TSBeginModal()
0x296ab0  cWinPictureDialog::AddText(char const *)
0x296b60  cWinPictureDialog::SetImage(cTSBuffer *, cTSRect const &, bool)
0x296c20  cWinPictureDialog::SetImage(cTSBuffer *, bool)
0x296f20  cWinPictureDialog::AddCloseBox(...)
0x297030  cWinPictureDialog::AddCloseBox(...)
0x297090  cWinPictureDialog::AddEditBox(...)
0x297230  cWinPictureDialog::AddButton(...)
0x2974b0  cWinPictureDialog::SetTitle(char const *)
0x297620  cWinPictureDialog::SetPositioning(...)
0x2977c0  cWinPictureDialog::Init()
0x297a20  cWinPictureDialog constructor

0x2aeb30  cWinTextWrap::DoLayout()
0x2aed50  cWinTextWrap::SetTopLeftImageSize(...)
0x2af4c0  cWinTextWrap constructor
```

The relevant `cWinPictureDialog` fields are:

```text
+0xdc  title cTSWinText *
+0xe0  body cWinTextWrap *
+0xe4  close box *
+0xe8  edit box *
+0xec  button vector
+0xf8  image buffer handle
+0xfc  image crop left
+0x100 image crop top
+0x104 image crop right
+0x108 image crop bottom
+0x10c frame/tile buffer
+0x110 45x45 special-case stub buffer
+0x124 positioning mode
+0x128..+0x134 positioning target rect
+0x138 target width/height aspect ratio
```

## 3. Frame resource and the exact client inset

This dialog is not framed by the generic 30x30 `GenDlg.bmp` tile.

`cWinPictureDialog::Init` calls the control manager's `SystemBMP(5)` and stores
the result at `this+0x10c`:

```text
0x29785c  get control manager
0x297864  li r4, 5
0x297868  load virtual +0x18
0x29786c  call
0x297874  stw r3, 0x10c(r31)
```

`cSimsApp::SetupWinCtrlMgr` loads resource type 4, ID 3008 (`0x0bc0`) and
installs it in slot 5:

```text
0x259b98  resource-load setup
0x259b9c  li r3, 0x0bc0             ; 3008
0x259ba0  li r5, 4
0x259bbc  li r4, 5                  ; system slot
0x259bc8  install
```

The local original-resource inventory identifies ID 3008 as
`kCatalogPopupBackTiles`, `CPanel/Backgrounds/PopupInfoTiles.bmp`, 36x36. The
instruction sequence independently divides both tile dimensions by 3:

```text
0x295d6c..0x295d90  tile height / 3 -> by
0x295d94..0x295da8  tile width  / 3 -> bx
```

Therefore:

```text
bx = 36 / 3 = 12
by = 36 / 3 = 12
client top-left = (12, 12)
content inset from outer window = (bx + 9, by + 9) = (21, 21)
```

`cWinPictureDialog::GetClientTopLeft` at `0x294600..0x29463c` confirms the same
one-third computation. The 12px frame inset and 21px content origin are proved
constants for this original resource.

### 3.1 The 36x36 resource is tiled as a 3x3 frame

`cWinPictureDialog::TSPaint` does more than use the resource to derive an inset.
At `0x295094..0x2950e0` it constructs a source rectangle equal to the first
one-third cell of the resource, then calls destination-buffer virtual `+0xc8`:

```text
source cell = [resource.left,
               resource.top,
               resource.right / 3,
               resource.bottom / 3]
            = [0,0,12,12]

destination = this+0x1c outer-window rectangle
final bool argument = true
```

Resolving the `cTSBuffer` vtable proves that `+0xc8` is
`cTSBuffer::BltEdge(cTSBuffer *, cTSRect const &, cTSRect const &, bool)` at
file `0x4852b0`. The vtable is reached through `TOC[-0x63a0]` to data
`0x6e388`; its `+0xc8` entry points through descriptor `data+0x12100` to code
virtual `0x47c420`, file `0x4852b0`.

`BltEdge` treats the supplied source rectangle as the top-left cell of a 3x3
source grid. Its implementation at `0x4852b0..0x4856ac` paints:

```text
[ TL ][  T ][ TR ]
[  L ][  C ][  R ]
[ BL ][  B ][ BR ]
```

The exact paint semantics are:

- four 12x12 corners are copied at native size with `cTSBuffer::Blt`;
- the top/bottom cells are tiled horizontally;
- the left/right cells are tiled vertically; and
- because `TSPaint` passes `true`, the center 12x12 cell is tiled in both axes.

The edge/center loops call vtable `+0xc4`, resolved to
`cTSBuffer::BltTiled` at file `0x485700`, with tile offsets `(0,0)`. `BltTiled`
clips the last repeated cell to the destination rectangle
(`0x485700..0x4858ac`). Therefore the edges and center are **tiled, not
stretched**, and the interior is **the repeated center cell, not a separately
painted solid fill**. `cWinPictureDialog::TSPaint` does not read the window's
main-fill-color field on this path.

## 4. Definitions used by the geometry formulas

The following names make the PPC formulas easier to read:

```text
bx, by = frame tile width/3 and height/3; both are 12 here
I        = displayed image rectangle
Iw, Ih   = displayed image width and height
T        = fitted title rectangle
Tw, Th   = fitted title width and one-line height
B        = body cWinTextWrap rectangle
Bw, Bh   = body rectangle width and laid-out height
Ew, Eh   = top-left exclusion width and rounded exclusion height
n        = number of buttons
Wi, Hi   = actual width/height of button i
S        = button-to-gap spacing
```

All positions below are local to the outer dialog unless noted otherwise.

## 5. Image dimensions, placement, and scaling

### 5.1 Stored crop is exact

`SetImage(buffer, rect, bool)` at `0x296b60` copies the caller's rectangle
unchanged to `this+0xfc..+0x108`. `SetImage(buffer, bool)` at `0x296c20` copies
the buffer's own bounds. Neither method computes a scale or substitute size.

### 5.2 Normal image path: native size, no scaling

The normal `TSPaint` image path is `0x29530c..0x295400`. It normalizes a copy of
the source crop to the origin, then translates the destination by:

```text
outer window top-left + GetClientTopLeft() + (9, 9)
```

The source and destination extents are the same crop width and height. Thus:

```text
I.left = 21
I.top  = 21
Iw = crop.right  - crop.left
Ih = crop.bottom - crop.top
scale = 1
```

There is no “fit to canvas,” up-to-3x scale, or aspect-fitting step in
`cWinPictureDialog`.

### 5.3 Exact 45x45 special case

`TSPaint` tests for a crop of exactly 45x45 at `0x295108..0x29512c`. For that
case, `Init` has loaded original resource 4992 (`0x1380`) at
`0x2978b8..0x2978dc`. The local inventory identifies it as
`kPictureDialogStub`, `CPanel/SimStub.BMP`, 133x103.

The special paint path:

1. paints the 133x103 stub at `(21,21)`; and
2. centers the unscaled 45x45 crop on the stub at `0x295244..0x2952dc`.

For layout and title avoidance, this case uses the stub dimensions:

```text
Iw = 133
Ih = 103
```

The actual 45x45 image itself is still not scaled.

### 5.4 Automatic-object modal is exactly 120x120

The original automatic-object caller is also exact:

```text
0x0cd354..0x0cd36c  initialize crop (0,0,120,120)
0x0cd440..0x0cd450  Product::DrawIcon(buffer, crop)
0x0cd454..0x0cd468  retain dialog crop (0,0,120,120)
0x0cc67c..0x0cc6dc  pass that crop to cWinPictureDialog::SetImage
```

Consequently the automatic-object picture presented by this dialog is a native
120x120 image at `(21,21)`. A port that first composes the icon into an exact
120x120 buffer matches the final dimensions; a generic scale-to-canvas rule is
not part of the original dialog.

## 6. Title construction, font, color, and placement

### 6.1 Title font and one-line sizing

`SetTitle` loads `font_table[14]`:

```text
0x2974b8  load font-table base from TOC[-0x7178]
0x2974dc  lwz r31, 0x38(r5)         ; 14 * 4
0x297514..0x297528                  ; SetFont(font[14])
```

`TryLayout` obtains the font's virtual line height and forces the title bottom
to `top + lineHeight` at `0x295740..0x295790`. The title is not word wrapped;
it is a fitted, single-line `cTSWinText`.

The binary proves table index 14. The caller that populates the font table uses
font type 1, size equal to the table index, flags 0, language 0 at
`0x25d9cc..0x25d9e8`. `cTSFontSys::TSAcquireFont` at `0x4b0100..0x4b05c4`
builds the regular `VariableSans_%02d.ffn` path; its literal pool reached through
`TOC[-0x45dc]` contains `VariableSans_`, `%02d`, `B`, `_`, and `.ffn`, and the
zero flags do not append `B`. Thus slot 14 is the regular shipped
`Fonts\variablesans_14.ffn`, not merely a point-size naming hypothesis.

### 6.2 Title color is the default font color, RGB(195,205,205)

The title is **not** explicitly set to black. This required resolving the
`cTSWinText` vtable rather than inferring method names from argument counts.

`cTSWinText`'s vtable base is resolved by `TOC[-0x6b9c]` to unpacked data
`0x769fc`. Relevant entries are:

| Vtable slot | Descriptor | Code virtual | File offset | Method |
|---:|---:|---:|---:|---|
| `+0x1e8` | `data+0x14cf0` | `0x52ecc0` | `0x537b50` | `SetTextColor(unsigned long)` |
| `+0x1ec` | `data+0x14cf8` | `0x52ec00` | `0x537a90` | `SetBackgroundOpaque(bool)` |
| `+0x1f0` | `data+0x14d00` | `0x52eb90` | `0x537a20` | `SetFont(cITSFont *)` |
| `+0x1f4` | `data+0x14d08` | `0x52f2b0` | `0x538140` | `SetAlignment(unsigned long)` |
| `+0x1f8` | `data+0x14d10` | `0x52eb50` | `0x5379e0` | `SetGutters(long,long)` |

The complete `SetTitle` setup at `0x297514..0x2975a0` calls:

```text
SetFont(font[14])
SetAlignment(0)
SetBackgroundOpaque(false)
SetWinTextFlag(2, false)
FitWindowToText()
SetGutters(0, 0)
```

It never calls vtable `+0x1e8`, `SetTextColor`.

The `cTSWinText` constructor leaves the color-override flag at `this+0xfc`
false at `0x538abc`. `cTSWinText::TSPaint` checks that flag at
`0x5385b0..0x5385d8` and changes the font color only when it is true. The title
therefore uses the shared font's initialized default color.

`InitSimsColors` constructs the default font color from direct RGB inputs:

```text
0x25d67c  load color-creation vcall
0x25d680  li r4, 0xc3              ; 195
0x25d684  li r5, 0xcd              ; 205
0x25d68c  li r6, 0xcd              ; 205
0x25d690  call color creation
0x25d698..0x25d6a0 store result through pointer to BSS 0x92988
0x25d9fc..0x25da30 apply that color to each successfully indexed font
```

Thus the intended title RGB is:

```text
RGB(195, 205, 205) = #C3CDCD
```

The engine stores the result in its native packed color format, so the final
frame-buffer value can undergo format quantization. The RGB inputs and their use
as the title font's default are proved. Black is not supported by this path.

### 6.3 Exact title placement

After body layout has established its narrow first-line width, define:

```text
Ew = Iw + 18
narrowWidth = Bw - Ew
```

With an image, `TryLayout` at `0x2957a4..0x295850` places the title at:

```text
T.left = bx + Iw + 18 + (narrowWidth - Tw) / 2
T.top  = by + 9
```

The PPC signed-half sequence supplies integer truncation consistent with its
rectangle arithmetic. For the normal automatic-object image (`Iw=120`):

```text
T.left = 150 + (narrowWidth - Tw) / 2
T.top  = 21
```

Notice that the title's base uses `bx`, not `bx+9`; this puts the title's
centering region 9px left of the body's narrow text start.

Without an image, `0x295854..0x2958bc` centers the title in the outer window:

```text
T.left = (outerWidth - Tw) / 2
T.top  = 21
```

## 7. Body position, top-left image exclusion, and wrapping

### 7.1 Body rectangle position

`DoLayout` establishes the same positions later reproduced by `TryLayout`:

```text
body left = bx + 9 = 21

with nonempty title:
    body top = T.bottom + 18
             = by + Th + 27

without title:
    body top = by + 9 = 21
```

Evidence is at `0x295ddc..0x295e8c` and `0x2958c0..0x295950`.

### 7.2 Image-exclusion rectangle

At `0x295e90..0x295f40`, `DoLayout` passes the following top-left exclusion to
`cWinTextWrap::SetTopLeftImageSize`:

```text
Ew = Iw + 18
rawEh = Ih + by + 27 - bodyTop
```

Therefore:

```text
without title: rawEh = Ih + 18
with title:    rawEh = Ih - Th
```

For the normal 120x120 automatic-object image:

```text
Ew = 138
without title: rawEh = 138
with title:    rawEh = 120 - Th
```

`cWinTextWrap::SetTopLeftImageSize` at `0x2aed90..0x2aedcc` rounds `rawEh` to the
nearest whole body-font line height. An exact halfway remainder stays at the
lower multiple because the increment occurs only when distance-to-lower is
strictly greater than distance-to-upper.

### 7.3 Body font and wrapping algorithm

If no font has been explicitly installed, `cWinTextWrap::DoLayout` loads
`font_table[12]`:

```text
0x2aeb44..0x2aeb58  font-table base, then +0x30 = 12 * 4
```

The binary proves index 12. The same zero-flag factory path proved in section
6.1 makes this the regular shipped `Fonts\variablesans_12.ffn`. It is **not**
`variablesans_12_bs.ffn`; the `_bs` resource is a distinct bold table and is not
selected by the `InitSimsColors` call here.

`cWinTextWrap::DoLayout` at `0x2aeb30..0x2aed04` does not split a Unicode string
on spaces in client code and does not use a hard-coded 16px pitch. It:

1. obtains the font's line height through virtual `+0x60`
   (`0x2aeb6c..0x2aeb80`);
2. computes `narrowWidth = Bw - Ew` and
   `narrowLineCapacity = roundedEh / fontLineHeight`
   (`0x2aeb94..0x2aebac`);
3. asks the font's native wrap/count entry points (`+0x6c` and `+0x68`) to wrap
   the byte string at `narrowWidth` for at most that many lines
   (`0x2aebbc..0x2aec78`);
4. advances through the actual byte string; and
5. wraps the remainder at full width `Bw`
   (`0x2aec80..0x2aecd8`).

If all text fits within the narrow lines, body height is the rounded exclusion
height (`0x2aebe8..0x2aec20`). Otherwise:

```text
Bh = roundedEh + remainderLineCount * fontLineHeight
```

at `0x2aece0..0x2aed04`.

The body does not install a text-color override. It therefore uses the same
initialized font default `RGB(195,205,205)` described above, now through font
slot 12.

### 7.4 Exact runtime line heights: slot 12 is 23px; slot 14 is 27px

The four FFN header bytes at `+0x10..+0x13` are all zero in both shipped tables,
so `ascent + descent` cannot reproduce the original line height. The runtime
height comes from `cTSFont::InitBitmapped` and can be reproduced exactly from
the glyph records.

The final `cTSFont` vtable is reached through `TOC[-0x631c]` to unpacked data
`0x6f5b8`. Its virtual `+0x60` points through descriptor `data+0x12880` to code
virtual `0x4a4120`, file `0x4acfb0`. That function is:

```text
0x4acfb0  lwz r3, 0x18(r3)
0x4acfb4  blr
```

and is symbolized `cTSFont::TSCharHeight()`.

The exact construction of `cTSFont+0x18` is:

1. `MacGIMEX_info` reads FFN `t1` and `t2` at entry bytes `+9` and `+10`,
   sign-extends and negates them at `0x00bf5c..0x00bf78`.
2. `cTSFont::InitBitmapped` negates those GIMEX values again at
   `0x4b411c..0x4b4134`, so runtime glyph-record `+0x14` is the original signed
   FFN `t2`.
3. For every nonempty glyph except character 160, `0x4b4554..0x4b45d0` takes
   `max(glyphHeight + t2)` into `cTSFont+0x18` and separately tracks
   `min(t2)`.
4. If `min(t2) < 0`, `0x4b4640..0x4b4660` subtracts that negative minimum from
   `cTSFont+0x18`.

The proved formula for these FNTF tables is therefore:

```text
TSCharHeight = max(glyphHeight + t2) - min(0, min(t2))
```

including the degenerate but nonempty space glyph, exactly as the engine loop
does. Applying that formula to the byte-verbatim English tables in
`UIGraphics.far` gives:

| Font-table slot | Exact resource | FFN SHA-256 | `max(h+t2)` | `min(t2)` | Virtual `+0x60` |
|---:|---|---|---:|---:|---:|
| 12 | `Fonts\variablesans_12.ffn` | `f584b9de66c9e588f541d7d1643acf1f2e91586fdc8636fc408fbfe603117a1c` | 19 | -4 | **23px** |
| 14 | `Fonts\variablesans_14.ffn` | `9075d4b9422e804b9f8614601843f1e8f94fffb05566ee811b299c5dc991efcc` | 22 | -5 | **27px** |

These numerical line heights are proved by the engine formula and original
resource bytes; they are not inferred from filename point sizes.

For the common 120x120 automatic-object dialog, the title/body consequences are
now fully numeric:

```text
title height Th = 27
title top/bottom = 21/48
body top with title = title.bottom + 18 = 66
body line height = 23

without title:
    raw exclusion height = 120 + 18 = 138
    rounded exclusion height = 138 = 6 body lines

with title:
    raw exclusion height = 120 - 27 = 93
    rounded exclusion height = 92 = 4 body lines
```

The 93px exclusion rounds to 92 because `SetTopLeftImageSize` rounds to the
nearest multiple of 23 (`4 * 23`), as proved in section 7.2.

## 8. Width lower bounds and adaptive window sizing

### 8.1 `TryLayout` width lower bounds

Before positioning children, `TryLayout` computes a minimum body width.

With a nonempty title:

```text
minimum Bw >= Tw + Iw + 18
```

at `0x295564..0x2955c8`.

Without a title it starts from `Iw`; only when that is zero does it substitute
100 at `0x2955cc..0x29560c`.

Each button also contributes its actual width plus 30:

```text
minimum Bw >= sum(Wi + 30)
```

at `0x295620..0x2956a4`. If the current body width is below the resulting
minimum, `TryLayout` widens the body at `0x2956f0..0x295724`.

### 8.2 Final outer dimensions

After laying out all children, `TryLayout` makes the outer window:

```text
outerWidth = Bw + 2 * (bx + 9)
           = Bw + 42

outerHeight = verticalCursor + by
            = verticalCursor + 12
```

at `0x295b70..0x295bb0`. It returns `outerWidth / outerHeight` as a float at
`0x295bb4..0x295c14`.

### 8.3 `DoLayout` aspect search

`DoLayout` first seeds the outer rectangle, preserving its current left/top,
with:

```text
width  = 300
height = 185
```

at `0x295db8..0x295dd8`. These are search seeds, not fixed final dimensions.

The constructor initializes `this+0x138` to float 2.0 at
`0x297af0..0x297b04`. The comparison tolerance used by `DoLayout` is float 0.05
(`0x3d4ccccd`, reached through its TOC literal). At
`0x295f44..0x2961c4`, the dialog:

1. calls `TryLayout` and compares `outerWidth/outerHeight` with target 2.0;
2. if outside target ±0.05, repeatedly doubles the body width until the ratio
   brackets the target;
3. binary-searches the lower/upper body-width candidates;
4. stops after at most 10 midpoint iterations or once the width gap is no more
   than 20; and
5. keeps the candidate whose aspect ratio is closer to 2.0, then performs a
   final `TryLayout`.

There is no 420px text-width cap, no `screenWidth - 80` clamp, and no 200px
outer-width minimum in `cWinPictureDialog`. Those values belong to other dialog
logic or to the port, not this class.

## 9. Edit-box and button row geometry

### 9.1 Edit box

When present, `TryLayout` forces the edit box height to 36 at
`0x29596c..0x295988`, then positions it:

```text
edit.left  = body.left
edit.top   = body.bottom + 9
edit.width = Bw
edit.height = 36
```

The vertical cursor then advances to `edit.bottom + 18`
(`0x2959c4..0x295a28`). Without an edit box, the button-row cursor begins at
`body.bottom + 18` (`0x295954..0x295968`).

### 9.2 Per-button dimensions

`AddButton` at `0x297274..0x2972a4` widens each individual button to a minimum
of 100. It does not normalize every button to the widest button. Native control
height and each label-derived width remain attached to that particular button.

### 9.3 Button spacing and placement

At `0x295a58..0x295b10`:

```text
sumWidth = sum(Wi)
S = (Bw - sumWidth) / (n + 1)

button[i].left = bx + 9 + S * (i + 1) + sum(Wj for j < i)
               = 21 + S * (i + 1) + priorWidths

button[i].top = verticalCursor
```

The PPC instruction is unsigned `divwu.` at `0x295a9c`. A CR-negative branch
at `0x295aa0..0x295aa4` substitutes 9; it is not the source-level law
`if (S < 9) S = 9`. In valid layout, the earlier
`Bw >= sum(Wi + 30)` lower bound makes row space positive.

After placing the row, the vertical cursor advances by the first button's
actual height plus 9 at `0x295b4c..0x295b6c`.

### 9.4 Close box

After final sizing, `DoLayout` puts a present close box at:

```text
close.left = outerWidth - closeWidth - 5
close.top  = 5
```

at `0x2961c8..0x29622c`.

## 10. Default modal position

The constructor initializes positioning mode `this+0x124` to 0 at
`0x297ab4`. After `DoLayout`, `TSBeginModal` mode 0 gets the application's main
window and calls the dialog's center-in-rect virtual with the main-window rect:

```text
0x296354  call DoLayout
0x29635c..0x296380  dispatch positioning mode
0x296384  GetMainWindow
0x296394  address mainWindow+0x74 rect
0x29639c  load dialog virtual +0x70
0x2963a0  call center-in-rect
```

The recovered symbol for that window operation is
`cTSWin::CenterWindowInRect(cTSRect const&)` at `0x508e80`. Therefore the default
law is center in the application's main-window rectangle, not explicitly
center in whichever display/screen bounds the port exposes. Those only coincide
when the main-window rect and screen rect coincide.

## 11. Proven mismatches in the audited `UIMobileAlert` picture path

The following comparisons were proved against the implementation present when
this audit began. They are findings, not authorization for speculative UI
changes.

| Area | Original law | Audited port behavior | Verdict |
|---|---|---|---|
| Frame | system slot 5, RT3008 PopupInfoTiles, 36x36 split 12/12/12 | generic GenDlg 30x30 split 10/10/10 | Proved mismatch |
| Frame paint | corners native; edges and center tiled from 12x12 cells | edges stretched; separate solid interior fill | Proved mismatch |
| Content/image origin | `(21,21)` | `(19,19)` from `Border + 9` | Proved mismatch |
| Automatic-object canvas | exact native 120x120 | final 120x120 canvas | Dimension matches |
| General image rendering | exact crop, scale 1 | `min(3, canvas/image)` scaling and centering | Proved mismatch outside already-composed 120x120 case |
| 45x45 input | 133x103 SimStub, native 45x45 centered | no original special case | Proved mismatch when exercised |
| Title font | engine font slot 14, regular `variablesans_14.ffn`, line height 27 | dialog font loader corresponding to slot 9; zero-header fallback height | Proved mismatch |
| Body font | engine font slot 12, regular `variablesans_12.ffn`, line height 23 | dialog font loader corresponding to slot 9 | Proved mismatch |
| Title color | font default RGB(195,205,205) | `UIStyle.DialogTitle = Black` | Proved mismatch |
| Body color | font default RGB(195,205,205) | `UIStyle.DialogText = White` | Proved mismatch |
| Body line pitch | font slot 12 virtual line height | hard-coded 16 | Proved law mismatch |
| Wrapping | native byte-string font wrap, narrow lines then full width | manual `Split(' ')` line builder | Proved algorithm mismatch |
| Width selection | adaptive target aspect 2.0 ±0.05 | 420 cap, screen-minus-80 clamp, 200 minimum | Proved mismatch |
| Button widths | individual label width, each minimum 100 | normalize all to the widest | Proved mismatch when widths differ |
| Button spacing | inside `Bw`, with 21px body inset | computed from full outer width | Proved mismatch for multiple buttons |
| Default centering | application main-window rect | screen bounds | Conditional mismatch unless rects coincide |

For a normal 120x120 automatic-object dialog, the most compact exact placement
summary is:

```text
image:       x=21, y=21, w=120, h=120, scale=1
body rect:   x=21
title:       x=150 + (narrowWidth-titleWidth)/2, y=21
title font:  slot 14 regular, height=27, default RGB #C3CDCD
body font:   slot 12 regular, line height=23, default RGB #C3CDCD
with title:  title y=21..48; body y=66; exclusion width=138, height=92
no title:    body y=21; exclusion width=138, height=138
window:      width=bodyWidth+42; height=cursor+12; body width chosen by
             target-aspect search rather than a fixed cap
```

## 12. Disclosed items and limits

The following distinctions prevent the evidence from being overstated:

- Font indices 12 and 14, their regular `VariableSans_%02d.ffn` resources, and
  their 23px/27px runtime heights are proved by the factory, `InitBitmapped`,
  and local resource bytes. They do not depend on the zero FFN ascent/descent
  header or on the port's former index/point-size hypothesis.
- RGB(195,205,205) is proved as the font-default RGB input. The original engine
  converts it to its native packed pixel format, so exact stored pixel bits can
  vary with the active format.
- Centering in the main-window rect is proved. Whether that produces the same
  coordinates as centering in current screen bounds depends on runtime window
  geometry and cannot be assumed from this static decode.
- The automatic-object **display crop** is proved 120x120. Pixel-for-pixel icon
  composition inside that crop is owned by `Product::DrawIcon`; matching only
  the crop dimensions does not by itself prove matching icon pixels.
