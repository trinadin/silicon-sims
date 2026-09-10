# R183 — native CAS `cTSSystemButton` state-sheet law

## Evidence boundary

This note isolates the native law behind the corrected Create-a-Character,
Create-a-Family, and Pick-a-Family Cancel buttons. It reads the owner's
local game in place and commits no executable or original art.

- Owner executable: `game-data/The Sims/The Sims Complete`, 6,486,820 bytes,
  SHA-256 `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`.
- `UIGraphics.far` SHA-256:
  `f0afa06b24888bd4201a3e619cf2c8d3f5ea1c9b1de6e11572981dbc871e5c8b`.
- Extracted PEF symbol index SHA-256:
  `698765465ec861b1a6dc357e32e14359752a1f63269222dadb56ad6134fae2a6`.
- Instruction addresses below are aligned executable file offsets. The PEF
  symbol index retains its `+2` tag (`cTSSystemButton` ctor `0x500882`), so
  its aligned instruction start is `0x500880`.

Run the independent gate with:

```sh
python3 tools/iff-dump/r183/r183-cas-system-button-canon.py
```

The script is standard-library-only. It hash-gates the owner inputs, walks the
FAR1 manifest, reads the BMP header, verifies the relevant PEF symbols and
machine words, and decodes the small PPC32 instruction subset used below.

## Resource identity and state width

The shipped resource declarations map
`kNghDefaultBtn = 5301` to `Nbhd/NbhdTileBtn.bmp`. The one matching FAR member
has this metadata:

| property | native value |
|---|---:|
| FAR data offset | `0x12eef1a` |
| member length | 9,842 bytes |
| member SHA-256 | `5c5470ae614f12bd9f8a1a10ecf6072a857c7b60db552ce41b40f94b9dd01d17` |
| BMP header | 364x62, 8bpp, BI_RLE8 |

`cTSSystemButton::cTSSystemButton(cTSBuffer*)` makes the inherited virtual
`SetImage` call with the source buffer in `r4`, columns in `r5`, and rows in
`r6`:

```text
0x005008b0: 389f0000  addi r4, r31, 0
0x005008b4: 38a00004  li r5, 4
0x005008bc: 38c00001  li r6, 1
0x005008c0: 819e0000  lwz r12, 0(r30)
0x005008c4: 818c01a4  lwz r12, 0x1a4(r12)
0x005008c8: 480a2119  bl 0x5a29e0
```

The symbol table identifies the inherited implementation as
`SetImage__9cTSWinBtnFP9cTSBufferii` and the constructor as
`__ct__15cTSSystemButtonFP9cTSBuffer`. Therefore the native image law is
unambiguously `SetImage(buffer, 4, 1)`, and the per-state source rectangle is:

```text
source width  = 364 / 4 = 91
source height =  62 / 1 = 62
```

The full 364px sheet is never one button frame. A system button has a 91x62
state source. `SetArea` temporarily gives it a 200x62 area; `SetCaption` later
shrinks and recenters that area to `textWidth + 70`. Conflating the full sheet,
the provisional area, and the final control width caused the earlier defects.

## Image composition: 45 / 1 / 45

`cTSSystemButton::ImageBlt` derives its cap width from the per-state source:

```text
cap = ((364 / 4) - 1) / 2 = 45
```

It then performs three blits: the 45px left cap at native size, source column
45 stretched across the variable center, and the 45px right cap from source
columns 46..90 at native size. For a final width `W`, the mapping is:

| destination | source |
|---|---|
| `0..44` | `0..44` |
| `45..W-46` | column `45` stretched |
| `W-45..W-1` | `46..90` |

Uniformly scaling a 91px state is not native: it distorts both rounded ends.
The verifier pins the cap calculation and all three native blit calls at
`0x5004c0..0x500674`.

## Create-a-Character and Create-a-Family

Both native flows load RT 5301 once, pass that exact buffer to the system-button
constructor, and then dispatch `cTSSystemButton::SetArea`. The decisive width
instruction is `right = left + 0xc8`; the bottom uses the buffer height at
offset `+0x20`.

| flow | RT 5301 load / buffer slot | ctor receives buffer | 200px area |
|---|---|---|---|
| Create-a-Character `Init` | `0x2ce7e4..0x2ce7f0`, `this+0x190` | `0x2ce9ec..0x2ce9f0` | `0x2cea14..0x2cea38` |
| Create-a-Family `Init` | `0x2d13b8..0x2d13cc`, `this+0x178` | `0x2d1bb0..0x2d1bb4` | `0x2d1bc0..0x2d1be4` |

The two area calculations are instruction-for-instruction equivalent:

```text
Create-a-Character
0x002cea20: 808f0074  lwz r4, 0x74(r15)      # left
0x002cea24: 80060020  lwz r0, 0x20(r6)       # buffer height
0x002cea28: 80af0078  lwz r5, 0x78(r15)      # top
0x002cea2c: 38c400c8  addi r6, r4, 0xc8      # right = left + 200
0x002cea30: 818c0068  lwz r12, 0x68(r12)      # SetArea vtable slot
0x002cea34: 7ce02a14  add r7, r0, r5          # bottom = top + height

Create-a-Family
0x002d1bcc: 809a0074  lwz r4, 0x74(r26)      # left
0x002d1bd0: 80060020  lwz r0, 0x20(r6)       # buffer height
0x002d1bd4: 80ba0078  lwz r5, 0x78(r26)      # top
0x002d1bd8: 38c400c8  addi r6, r4, 0xc8      # right = left + 200
0x002d1bdc: 818c0068  lwz r12, 0x68(r12)      # SetArea vtable slot
0x002d1be0: 7ce02a14  add r7, r0, r5          # bottom = top + height
```

This is only the pre-caption area. After each button receives its caption, the
final English rectangles are:

| flow | caption | font[12] width | final width | final left/top |
|---|---|---:|---:|---|
| Create-a-Character | Done | 39 | 109 | `(56,529)` |
| Create-a-Character | Cancel | 51 | 121 | `(339,529)` |
| Create-a-Family | Done | 39 | 109 | `(145,529)` |
| Create-a-Family | Cancel | 51 | 121 | `(539,529)` |

The original center anchors are preserved; odd-width recentering follows the
engine's integer division. All controls remain 62px high.

## Pick-a-Family `CreateButton(3)`

`cWinPickFamily::Init` independently loads RT 5301 into `this+0x128` at
`0x2d9a40..0x2d9a54`. `cWinPickFamily::CreateButton` selects the system-button
branch only for index 3, constructs the same `cTSSystemButton`, and applies
the same provisional 200px area law:

```text
0x002d9490: 2c1b0003  cmpwi r27, 3
0x002d94a4: 40820108  bne 0x2d95ac
0x002d94b8: 809d0128  lwz r4, 0x128(r29)      # RT 5301 buffer
0x002d94bc: 482273c5  bl 0x500880             # cTSSystemButton ctor
0x002d94d4: 809c0074  lwz r4, 0x74(r28)      # left
0x002d94d8: 80060020  lwz r0, 0x20(r6)       # buffer height
0x002d94dc: 80bc0078  lwz r5, 0x78(r28)      # top
0x002d94e0: 38c400c8  addi r6, r4, 0xc8      # right = left + 200
0x002d94e4: 818c0068  lwz r12, 0x68(r12)      # SetArea vtable slot
0x002d94e8: 7ce02a14  add r7, r0, r5          # bottom = top + height
```

The button loop consumes English STR#128 entries [2..5]. Index 3 therefore
receives [5] `Cancel`, not a selected-family name. Later initialization assigns
the same button STR#128[15] `Back to Neighborhood` as its tooltip and marks it
one-shot (`0x2da0ec..0x2da128`). Its provisional area is centered at x=200;
font[12] measures `Cancel` at 51px, so its final rectangle is
`(139,529)-(260,591)`. Its command path returns to the neighborhood.

`PAFCancel.bmp` (RT 5104) remains dead art in this executable. The visible
Cancel button is text over the four-state RT 5301 system-button art.

## Caption geometry and state law

`SetCaption` writes font index 12 once. It does not search smaller fonts. It
measures the caption, sets the caption origin to x=35, vertically centers the
font, and changes the button width to:

```text
final width = textWidth - 20 + 2*45 = textWidth + 70
```

The owner `variablesans_12.ffn` metrics are `Done=39`, `Cancel=51`, and a
23px character height, placing the English caption at local `(35,19)`.

The four image states are default, pressed, hover, disabled. Caption colors
from the native palette are `#C3CDCD`, `#00FFFF`, `#FFFFFF`, and `#405D5F`.
Only the pressed state offsets its caption, by `(+2,+2)`; the ctor's other
three offset pairs are zero.

Mouse-down captures and presses but does not issue the default command.
Mouse-up issues it only if the pointer is inside. Pointer exit clears the
visual press while retaining capture; re-entry restores the press, so a
drag-out cancels and a drag-back-in rearms before release.

## Verified conclusion

The native invariant for all three paths is:

```text
RT 5301 -> 364x62 H4 sheet -> SetImage(4,1) -> 91x62 source state
                                             -> 45/1/45 ImageBlt
          SetArea(..., left+200, top+62)      -> provisional centering
          SetCaption(font[12])                -> width=textWidth+70, recentered
```

For the shipped English labels the final widths are 109px (`Done`) and 121px
(`Cancel`). Pick-a-Family button 3 is the active Cancel/back control, not a
family-name display.
