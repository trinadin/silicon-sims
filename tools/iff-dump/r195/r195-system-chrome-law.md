# R195 — System chrome engine-wide (campaign item 9): the decode

Evidence: `r195-disasm-cwinhelp.txt`, `r195-disasm-syschrome.txt`,
`callers.py` (the corrected PPC bl caller-scanner). All addresses are raw
file offsets (capdis convention); symbol-index entries are normalized with
`& ~3`.

## 0. Tooling fix (affects all future caller-scans)

The bl displacement is `w & 0x03FFFFFC` sign-extended over 26 bits — **no
shifts** (LI already carries the <<2 alignment in place). Verified against
capstone on two known words. The earlier "shift-then-mask" mental model
silently returns zero hits. `callers.py` in this folder is the reusable
scanner. With it: `cTSWinGenDlg::AddCheck/Scrollbar/AddSlider` and every
`cTSWinCtrlMgr::Default*` factory have **zero direct bl callers** — all
dialog widget creation goes through the `cITSWinGenDlg` interface vtable
(`lwz r12, slot(r12); mtctr; bctrl`), so consumers must be found at the
game-side Init level instead.

## 1. The standard modal window is template 3008, NOT GenDlg

`cWinHelp::Init` 0x277d0c: `LoadBuffer(0xbc0, &this[+0x110], 4)` —
rt 3008 = kCatalogPopupBackTiles = `cpanel\Backgrounds\PopupInfoTiles.bmp`
(36x36; the R174 12px nine-slice). `CWinPhoneBook::Init` 0x45944 loads the
same 0xbc0 into +0x120, then 80 (kPhoneBookBkg, +0x124) and 81
(kPhoneBookIcon, +0x128). The R162/R192 ports drew these windows on the
**GenDlg** tile — corrected this round to the PopupInfoTiles 12px slice
(`UIOriginalDialogChrome.DrawPictureWindow`).

`cWinHelp::TSPaint` 0x277b20 — the R162 "scroll thumb" reading is
CORRECTED: it draws one 12x12 cell of the 3008 sheet at the window origin
(src rect = buffer rect with right/bottom replaced by `w/3`/`h/3` via the
0x55555556 reciprocal — the corner cell of the 36x36 sheet). It is the
frame's own top-left corner tile. The topic list is a separate string-set
list widget (see §2); its internal row/scroll painting is not statically
decoded.

## 2. cWinHelp layout (the string-set list consumer)

- `cTSFrameWork_CtrlMgr()` → **vtable+0x30(0, &StringSetIter)** creates the
  topic list widget (stored +0x108); string set = STR# 166 through the
  `0x87f60(9)` / `0x25f440(0xa6, ...)` chain. It is SetArea'd (+0x68),
  shrunk `width-0xa` under a version-0x13 condition, moved
  (`cTSWin::TSWinMoveTo`), given SetID(0x2000, 1) (+0x98), and added as a
  child (+0x28).
- Ctrl-mgr **vtable+0x2c(0, label)** creates the Close push button
  (+0x104), caption `GetButtonLabel(3)`; `TSOnKeyDown` ESC/Enter →
  `0x51d930(...,3,...)` = simulate button-3 click.
- `SetSelected`/`GetSelected`: the selected page persists across opens.
- Command 0x15 from the list: index = `param*2+2` into the STR# 166
  iterator → the list widget's +0x1d4/+0x1f4 virtuals (top-index family).

## 3. CWinPhoneBook::Init 0x4592f0 — the full widget census

| field | widget | decode |
|---|---|---|
| +0x120 | 3008 tiles | `LoadBuffer(0xbc0)` — the window frame |
| +0x124 | 80 kPhoneBookBkg | `cpanel\backgrounds\phonebookbkg.bmp` 620x356 |
| +0x128 | 81 kPhoneBookIcon | `cpanel\backgrounds\phoneicon.bmp` 33x23 |
| +0x110 | push button | ctrl-mgr vtable+0x2c, caption `GetButtonLabel(2)` (Call) |
| +0x114 | push button | ctrl-mgr vtable+0x2c, caption `GetButtonLabel(1)` (Cancel) — resolves the R192 "SetImage(8192)" residual |
| +0x118 | string-set LIST | ctrl-mgr **vtable+0x30**, `SetArea(0, 0, 0x21c, 0x19)` = 540x25 at the (50,50) anchor — the family header strip; the slot-11 DropDown / slot-13/14 ListBack consumer family |
| +0x108 | cTSWinText | 0x110-byte alloc, ctor 0x537680; row pitch **+0xe0 = 10**; text color `MakeColor(0,0,0x39)` → **RGB(0,0,57)** at +0x64; height = count x 10; filled by `FillFamilyList` 0x458830 |
| +0x10c | cTSWinText | same law; filled by `FillFamilyMembers` 0x4584c0 |

The two columns are PLAIN text widgets on the board — no ListBack row
chrome (the R192 per-row ListBack tiling on the columns is retired;
ListBack belongs to the string-set strip family). Both window buttons are
WinBtn push buttons (GetButtonLabel captions), as the port already drew.

## 4. cTSWinScrollbar — the engine scrollbar (slot 4 = WinScrol)

`cTSWinCtrlMgr::DefaultScrollbarHorizontal` 0x50f530 allocates 0x148 bytes
and binds ctrl-mgr system slot **4** (WinScrol 204x20); the vertical
factory 0x50f670 is byte-similar (slot presumed 4, disclosed).

`cTSWinScrollbar::SetImage` 0x525350 — the sheet law:

```
cellW = mulhwu(0xAAAAAAAB, w) >> 3      // = w/12  (204 -> 17 exact)
src   = (0, 0, cellW, h)                // the first cell
if transparent: chroma-key RGB(255,0,255) via buffer vtable+0x70
orientation +0xcc:
  == 1 (horizontal): SetArea keeps width, height = h (20)
  == 2 (vertical):   SetArea width = cellW; src.left += 6*cellW
                     (src = (6*cellW, 0, 7*cellW, h))
```

So the sheet is **12 cells of 17x20 in two 6-cell groups**: cells 0-5 the
horizontal scrollbar's pieces, cells 6-11 the vertical's. The vertical
widget is one cell (17px) wide and its cells come from the second group.
`CalculateAllMetrics` 0x5255b0: `+0xdc = vcall(+0x214)` and
`+0xe4 = span − vcall(+0x214) − vcall(+0x1d4/+0x1d8)` (the prev/next
button extent pair; +0x1d4 height / +0x1d8 width). The bmp painter
0x526420 composes cells 0/2/3/4-or-5 (state-dependent) along the axis.

**No base-game consumer found**: the help and phonebook windows use
string-set list widgets whose scroll painting is internal to the ITS list
class (not statically reachable), and cWinOptions uses dedicated CPanel
art (optcheckbox 120x21 / optradio 120x20 / optslideron/off 44x14 — dims
verified this round). WinChk 18 / WinSlH 19 / WinSlV 20 therefore stay
mounted-but-unconsumed in the base game; they are pinned by the new
`uisyschrome` census together with the RT-only CloseBox 30 / MinimizeBox
31 (never bound by `SetupWinCtrlMgr` — r142 §7.5).

## 5. Sys-sheet cell census (FAR-verified dims + decoded math)

| member | dims | cell law |
|---|---|---|
| WinBtn.BMP | 260x33 | 4 states of 65x33 |
| WinChk.BMP | 168x28 | 6 cells of 28x28 (2 check x 3 widget states) |
| WINSLH.bmp | 48x12 | slider thumb (36x12) + track tile (12x12) |
| WINSLV.bmp | 12x48 | track tile (12x12) + thumb (12x36) |
| WinScrol.BMP | 204x20 | 12 cells of 17x20 (6 horizontal + 6 vertical) |
| dropdown.bmp | 16x16 | combobox arrow (slot 11) |
| ListBack.BMP | 136x22 | list row chrome (slots 13/14) |
| CloseBox.bmp | 48x11 | 4 cells of 12x11 (RT-only) |
| MinimizeBox.bmp | 48x11 | 4 cells of 12x11 (RT-only) |
| PopupInfoTiles.bmp | 36x36 | 3x3 cells of 12 (template 3008) |

(WINSLH/WINSLV cell splits are pixel-structure observations from the
36px/48px sheets — the slider painter itself was not decoded.)

## 6. Port (this round)

- `UIOriginalHelpDialog`: frame → PopupInfoTiles (corrects the R162
  GenDlg reading); comments updated (corner-tile law).
- `UIOriginalPhoneBookDialog`: frame → PopupInfoTiles; the (50,50) 540x25
  string-set strip ported (ListBack row stretched 22→25 + the slot-11
  DropDown arrow at its right edge + a modeled family picker popup);
  both columns re-pitched to the engine's 10px cTSWinText rows in
  RGB(0,0,57) on the board (19/22 row clamps); ListBack retired from the
  columns.
- Gate: `uiphone`/`uihelp` strengthened (strip/pitch/ink/frame pins,
  live strip + picker probes); NEW `uisyschrome` census check (114 total).

## 7. Residuals (disclosed)

- The strip's popped-picker interaction is modeled on string-set list
  semantics; the engine's popped-list geometry/behavior is not decoded.
- The columns use the variablesans_10 caption table for the 10px pitch
  (the engine's ctrl-mgr system font slot is undecoded).
- Columns remain non-scrolling (rows beyond the decoded rects are
  clipped; the engine sizes to count x 10 and clips at the window).
- cTSWinScrollbar is decoded but has no identified live base-game
  consumer to mount on; WinChk/WinSlH/WinSlV likewise stay
  mounted-but-unconsumed. DefaultScrollbarVertical's slot binding is
  presumed 4 from byte-similarity.
- cWinHelp pane rects (window size 460x400, 160px column, 18px pitch)
  remain the port's.
