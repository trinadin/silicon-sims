# R193 — Small mounts: kTallSubpanel + the ViewMenu label sheets

Date: 2026-08-31

## Evidence boundary

Owner's local `game-data/The Sims/The Sims Complete` PPC executable
(SHA-256 `33c76da2…c06a5f`) and `UIGraphics.far`. Constants and behavior
only.

## Decode

### kTallSubpanel 4918 — cWinCPanel::Init 0x270170

The load at 0x270240 (`li r3, 0x1336; factory(4918, &cpanel+0x104, 4)`)
is guarded by `cWinCPanel` calling **0x20c670 = CPState::IsDoubleByteUI**
(first at 0x270230): the tall backdrop loads only for non-double-byte
(non-CJK) UI. The buffer is then used through vt+0x9c and a rect built
from buffer fields +0x14/+0x18 with `+0x118`/`+0x32` offsets.

Member: `cpanel\Backgrounds\TallSubPanel.TGA` — **600x150** (360,044 B).
The people window is 150 tall (`(220, SH-150, W, SH)`, r144), so the tall
backdrop is that window's full-height background behind the 100px toolbar
band.

Port: mounted at people-local (0,0) = MainPanel-local **(0,-50)** at
natural size, visible only for desktop LIVE. The buffer-rect arithmetic
(`+0x118`/`+0x32`) was not fully resolved — natural size at the window
origin is the disclosed model.

### The ViewMenu label sheets — cDDDSimsView::Init 0x218b70

Four `factory(id, &view+N, 4)` loads at 0x218c64-0x218dd4, each followed
by a `new cTSWinBtn` (alloc 0x18c) with `SetImage(buf, cols, 1)`:

| id | member | dims | SetImage | cell |
|---|---|---|---|---|
| 820 kItemZoomIn | `cpanel\ViewMenuZoomIn.bmp` | 378x84 | **(9, 1)** | 42x84 |
| 821 kItemZoomOut | `cpanel\ViewMenuZoomOut.bmp` | 378x84 | **(9, 1)** | 42x84 |
| 822 | `cpanel\ViewMenuRotateLeft.bmp` | 420x42 | **(5, 1)** | 84x42 |
| 823 | `cpanel\ViewMenuRotateRight.bmp` | 420x42 | (5, 1) | 84x42 |

These are the click-hold view-menu state labels next to the small
zoom/rotate diamond buttons (`cpanel\Buttons\zoomin/zoomout.bmp` 108x27 =
4x27x27 and `rotleft/rotright.bmp`, already mounted on the UCP).

Port: mounted on the UCP zoom cluster as `UIOriginalNavbarButton` cells
(9x1 / 5x1), visible only while the matching small button is held, with
the cell quantized from the port's zoom/rotation state (mapping
disclosed — the original ladder geometry was not decoded).

## Gate

`uismall` pins: the five art members at their byte dims; the live tall
backdrop (mounted at (0,-50), 600x150, LIVE-only visibility); the four
label cells (cols, cell sizes, original mount, hold-gated invisibility).
Targeted soak `uismall,corpus`: PASS 6/0. Full default gate: PARITY.md
R193 row.

## Residuals (disclosed)

- The TallSubpanel buffer-rect arithmetic and the ViewMenu label
  positions are modeled (natural size at window origin / adjacent to the
  small buttons), not instruction-exact.
- The zoom/rotation cell quantization maps the port's continuous camera
  state onto the 9/5-state ladders.
