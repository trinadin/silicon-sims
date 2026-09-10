# R145 Interest right pager — closed placement/state residual

Target: close the remaining right-page-arrow residual in
`cWinPeople::BuildInterestWindow` @0x287010 without substituting a visually
plausible edge anchor. Evidence is the shipped PPC Complete executable plus
the byte-verbatim `ScrollRight.BMP` member in the owner's `UIGraphics.far`.

## Exact destination rectangle

`SetArea` is virtual slot +0x68 and takes `(this, left, top, right, bottom)` in
`r3..r7`; `cTSWin::SetArea` stores those at +0x74/+0x78/+0x7c/+0x80. At
0x287578..0x2875f4 the right-arrow call derives:

```
buttonW = button.right - button.left           // r8
buttonH = button.bottom - button.top           // r0
hostW   = host.right - host.left               // r4
top     = contentTop + (hostH-contentTop-buttonH)/2
left    = hostW - buttonW - 2                  // r4
right   = left + buttonW = hostW - 2           // r6
bottom  = top + buttonH                        // r7
SetArea(button, left, top, right, bottom)
```

The old residual mistook final `r6 = hostW-2` for `left`; it is the `right`
argument. Resource 101 is 36x49 and `SetImage(buffer,4,1)` makes a 9x49
button, so the exact host-local rectangles are:

| screen | host | arrow rect | right gutter | clipping |
|---|---:|---:|---:|---|
| 1024 | 504x100 | (493,34,502,83) | 2px | none |
| 800 | 280x100 | (269,34,278,83) | 2px | none |

The host itself ends at the physical right screen edge in both layouts, but
the arrow does not: its exclusive right is two pixels inside that edge.

## Exact source-cell orientation and interaction state

`cTSWinBtn::SetImage` @0x50c0f0 stores rows=1 at +0xe4, columns=4 at +0xe8,
and cell dimensions 9x49. `ComputeSrcRect` @0x50ad00 uses horizontal source
`[col*9,(col+1)*9)` and row zero. `CalcRowCol` @0x50d270 reduces the enabled
four-column case to:

```
col = (entered ? 2 : 0) + (state != 0 ? 1 : 0)
```

Thus `ScrollRight.BMP` is never mirrored or vertically sliced. Its four
left-to-right cells are 0 normal, 1 selected/down while not entered, 2 hover,
and 3 entered+selected/down (the pressed cell for this one-shot pager).
`BuildInterestWindow` constructs the right control from resource 101, so it
uses the right-pointing sheet directly; resource 100 supplies the left pager.

Port consequence: `Size.X - 11` was already the exact 9px button left edge.
The correction is to the evidence and regression pins, not that coordinate.
The shared original-sheet draw selector now preserves programmatic state 1
while composing native entered/down combinations into cells 2/3; its existing
disabled presentation remains cell 3.
