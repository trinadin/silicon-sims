# R190 — Scrapbook / photo-album + snapshot capture law

Date: 2026-08-31

## Evidence boundary

All decode facts below come from the owner's local `game-data/The Sims/The
Sims Complete` PowerPC executable (SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`) and the
shipped `UIText.iff` / `UIGraphics.far`. Addresses are file offsets
(symbol address − 2 = function start, r141 convention). This note records
behavior, constants, and hashes only; it does not copy or redistribute
proprietary art.

## 1. Window law — cWinScrapbook (template 1200)

`cWinScrapbook::__ct` 0x29ff50, `Init` 0x29f5a0:

- Init loads template `0x4b0` (1200 = kScrapBack) through the shared
  template loader 0x3b6190 (the same loader family as budget's 3008).
- Init registers string-set `0x90` (144) via LoadUIStringIntoLabel 0x25f440
  (packed-string getter 0x87f60 index 9) and parses `(x;y)` anchors with
  StringPosToPoint 0x25f110; two anchors compose a rectangle, one composes a
  control position (dlg+0x104/0x108 = a point; dlg+0x1c..0x28 = the photo
  draw rect).
- `TSPaint` 0x29f1a0: draws the current page's photo via `cScrapbook::GetPhoto`
  0x2493a0 CENTERED in the photo rect at natural size.
- `TSOnCommand` 0x29ef02 routes: Done → close; First/Prev/Next/Last →
  `SetCurPage` 0x249870; Delete → `cSimsApp::MessageDialog` 0x253f00
  confirm, then `DeleteCurrentPage` 0x247d60 + `SaveScrapbook` 0x248400;
  caption edits go through `SetDescription` 0x249450.
- `Update` 0x29ec40 computes the enable/disable state per page position.
- Opening/closing coordinates with `cWinCPanel::SetScrapbookOpen` 0x26ec30 /
  `cWinCamPanel::SetScrapbookOpen` 0x265ce0 (the CamViewAlbum button).

### STR# 144 'ScrapStrs' (414 = 23 langs × 18; English [0..17])

sha256(chunk) = `d5b8d8c97bcec1a92e6629401e8cd87d16d9945ccec88892045a52a49c4c54f6`

| i | value | use |
|---|---|---|
| 0 | `(129;512)` | Done anchor |
| 1 | `Done` | label |
| 2 | `Close Photo Album` | tooltip |
| 3 | `(442;511)` | Delete anchor |
| 4 | `Delete` | label |
| 5 | `Delete this photo` | tooltip |
| 6 | `(58;442)` | Prev anchor |
| 7 | `Previous Photo` | tooltip |
| 8 | `(614;442)` | Next anchor |
| 9 | `Next Photo` | tooltip |
| 10 | `(19;442)` | First anchor |
| 11 | `First Photo` | tooltip |
| 12 | `(643;442)` | Last anchor |
| 13 | `Last Photo` | tooltip |
| 14 | `(520;72)` | page filename label anchor |
| 15 | `(90;435)` | caption anchor |
| 16 | `(350;220)` | photo center |
| 17 | `(100;415)` | second anchor (rect composition) |

(Continuing entries: [18] `(450;20)`, [19] `File name, minus extension`
— the filename label format, [20] `Delete Photo?`, [21] the delete-confirm
message, [22] `Empty Photo Album`.)

STR# 141 'scrapbookstrs' (17 entries, single-language legacy table)
sha256 = `191c6df052f153f0dc25aba7f68936590b5f24e22bfec82ce62b6ecdd0eb6568`;
same family with `&`/`*` prefix markers (e.g. [4] `&Done`, [7] `*Delete`).

### Art (UIGraphics.far, byte-verified)

| member | full sheet | cell (4 states) | anchor |
|---|---|---|---|
| `cpanel\backgrounds\scrapback.bmp` | 700x560 (RLE8) | — | board |
| `cpanel\buttons\scrapdone.bmp` | 520x41 | 130x41 | (129,512) |
| `cpanel\buttons\scrapdelete.bmp` | 520x41 | 130x41 | (442,511) |
| `cpanel\buttons\scraphome.bmp` | 152x47 | 38x47 | (19,442) |
| `cpanel\buttons\scrapleft.bmp` | 116x47 | 29x47 | (58,442) |
| `cpanel\buttons\scrapright.bmp` | 112x46 | 28x46 | (614,442) |
| `cpanel\buttons\scrapend.bmp` | 148x46 | 37x46 | (643,442) |

## 2. Capture law

`cDDDSimsView::TSOnMouseDownL` 0x2165b0 (offset 0xd8..0x138):

1. While `this->0x114` view mode getter (0x211150) returns **4** (camera):
2. `HouseViewer::ComputeCameraFrame` 0x1c4f90 builds the frame: center
   (mouse ± CamGetWidth()/2, mouse ± CamGetHeight()/2), where the dims are
   CPState+0x218/+0x21c; the rect is clamped to the view bounds
   (0x37c70 rect-clamp calls).
3. The click handler insets the rect **1px per edge** (left+1, top+1,
   right−1, bottom−1 — the stw adjustment block at 0x21665c..0x216680).
4. `HouseViewer::TakeAsyncSnapshot` 0x1c4e10 (rect, TRUE, NULL); the click
   returns 1 = consumed.

### The dimension tables (CPState)

`CamSetSize(enum)` 0x20cd60 reads three BSS tables through TOC slots
r2−0x6cb8/−0x6cb4/−0x6cb0. The tables are written by the static
initializer at the end of `CPState::__ct` 0x212100 (0x2124a8..0x2124bc):

| enum | table | dims |
|---|---|---|
| 0 Small | [r2−0x6cb0] → BSS 0x911f8 | **200 x 150** |
| 1 Medium | [r2−0x6cb8] → BSS 0x911f0 | **400 x 300** |
| 2 Large | [r2−0x6cb4] → BSS 0x911e8 | **600 x 400** |
| 3 Custom | keeps CPState+0x218/+0x21c | clamped |

`CamSetSize(int,int)` 0x20cbe0 clamps a custom size into
[small.w..large.w] × [small.h..large.h] and re-classifies the enum on an
exact table match. `CamGetWidth/CamGetHeight` 0x20cb10/0x20cad0 are plain
CPState+0x218/+0x21c loads.

Decode route for the tables: the PEF data section (container file offset
0x5c22f0, packed 0x6d834, unpacked 0x7bf80) was re-unpacked with the r142
opcode-corrected decoder; the TOC slots resolve at image offset 0x1348+
(r2 = data+0x8000, r142 law), but their targets 0x911e8..0x911f8 lie beyond
the initialized image (0x7bf80) — i.e. BSS — so the values were recovered
from the constructor's `stw` writes instead (`li r6,0xc8 / li r5,0x96` =
200/150; `li r4,0x190 / li r3,0x12c` = 400/300; `li r0,0x258 / li r4,0x190`
= 600/400).

STR# 140 [17] documents the interaction: "Move the frame unto the game
screen and click the mouse to capture the highlighted scene"; custom size
via CTRL + arrow keys. [19] gives the original JPEG file-size ladder
(Small low 4K … Large high 89K).

## 3. Port

- `UI/Panels/UIOriginalScrapbookDialog.cs` — the 700x560 board modal: six
  4-state sheet buttons at the STR# 144 anchors with labels/tooltips, the
  caption paragraph at (90,435), the filename label at (520,72) formatted
  per [19], the empty state [22], the delete confirm [20]/[21], photo drawn
  centered on (350,220) at natural size.
- `UI/Model/OriginalSnapshotAlbum.cs` — the cScrapbook model: pages
  (photo + description + filename), navigation, delete, and PNG persistence
  under `UserDir/PhotoAlbum/`.
- `UI/Panels/UIOriginalCameraOverlay.cs` — the camera-mode world surface:
  consumes world clicks exactly in camera mode (the mode-4 consume), draws
  the highlighted frame, computes the capture rect on the decoded law,
  supports CTRL+arrows custom sizing with the engine clamp.
- `UI/Screens/OriginalSnapshotCaptureScene.cs` — the TakeAsyncSnapshot
  equivalent: a no-draw scene appended directly after the lot World so the
  backbuffer readback (`GetBackBufferData` region overload, implemented in
  FSOMonoGame's OpenGL backend) sees the finished world with no UI.
- Wiring: `TS1GameScreen.InitializeLot` mounts the overlay above the lot
  control and below the frontend; `MainPanel.ModeChanged` gates it;
  `CameraChrome.OnViewAlbum` opens the album (STR# 140:9 'View Photo Album').

## 4. Residuals (disclosed)

- The original persists photos as JPEG at three qualities under a
  per-family album directory with generated thumbnails; the port persists
  PNG (lossless) under `UserDir/PhotoAlbum/` with `photo-N` names. The
  dims/anchor/click laws are exact; the container format is not.
- The caption is displayed read-only; the original's in-album text edit
  (SetDescription path) is not yet an editable control.
- The in-world frame highlight is a 1px border (the original's frame
  presentation is view-drawn and was not pixel-decoded).
- PIP (DoPictureInPicture 0x2130c0) also calls TakeAsyncSnapshot; the
  picture-in-picture feature itself is not ported.

## 5. Gate

`uiscrap` pins: STR# 141/144 chunk shas + the English blocks; all seven
art dims; the dimension tables and clamp bounds; the frame-rect law on
three probe points (center, top-left clamp, bottom-right clamp) + the 1px
inset; the live dialog (anchors, cells, labels, tooltips, empty state);
the album add/navigate/delete round-trip; the overlay's mode gating.
Targeted soak `uiscrap,corpus`: PASS 6/0. Full default gate: see
PARITY.md R190 row.
