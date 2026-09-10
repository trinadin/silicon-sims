# R238 — Scrapbook editable caption and hidden filename

Evidence is freshly generated Capstone disassembly from the owner's original
PPC executable (`scrapbook.txt`), with exact full symbols. R190 had treated
sequential `(x;y)` strings as anchors without tracking whether each use
consumed a size or a position.

## Caption

`cWinScrapbook::Init` allocates `cTSWinTextEdit2` at 0x29f948 and stores
it in +f0. SetFont takes font-array +28 = slot10. The text color comes from
the standard #C3CDCD global. STR144[14]=(520;72) is first put in the
right/bottom fields of a zero rectangle. STR144[15]=(90;435) is then added
to all four edges. Thus caption rect is (90,435)-(610,507), **520x72**.
The prior read-only caption and its 430px width were not faithful.

Settings: SetLinesAllowed(-1), SetCapacity(4096), SetFlashFrameOnEmpty(false),
SetDrawFrameOnFocus(false), SetTransparent(true). SetColors gets background
native37 (5 for depth mode1), caret/frame the standard text color. The native
source does not request any new edit-border chrome.

`SetColors` at 0x530090 stores the three arguments into +138/+13c/+140.
Paint uses +138 for the background fill (0x534614), +13c for caret lines
(0x534ac0), and +140 for the frame (0x534b74). None is a selection color.
Selection is passed as a range to cTSFont::TSDrawText (0x5347d0..0x534808),
which marks each selected character for TSDrawChar (0x4ade80..0x4adeb4).
The selected-character branch (0x4b379c..0x4b3d18) fills cell padding with
the font ink and reverses the glyph blend against the underlying page.
It does not put blue behind ordinary light text. The port uses the inverse
of the owned glyph alpha mask in that branch, preserving page pixels through
the glyph strokes. See `font-selection.txt` and `font-selected-char.txt`.

`Update` at0x29ec74 reads GetDescription and SetText(...,false,true), then
FlushUndoBuffers. If no description is present, it supplies an empty string.
This is an editable caption, not a static summary, filename or help text.

## Caption persistence

Command index0 Done: current edit buffer -> SetDescription0x249450,
then SaveScrapbook0x248400, then close. Commands2/3 Prev/Next and4/5 First/
Last also SetDescription before SetCurPage and Update, but do not save to
disk in those command branches. Delete uses a confirmation; on Yes it
DeleteCurrentPage+SaveScrapbook and closes if no pages remain. On No it
calls Update, restoring the stored caption. Native caption edits do not
write a sidecar on every keystroke.

Delete No deliberately discards pending caption edits: MessageDialog's
non-Yes result branches at 0x29f010 to 0x29f058, which calls Update at
0x29f05c. There is no SetDescription before the confirmation. The port
retains this decoded behavior rather than introducing draft preservation.
Its asynchronous confirmation explicitly blurs/disables the underlying
caption until the response, to reproduce the native modal input boundary.
No refresh, save or caption mutation occurs while the confirmation is open.
Both callbacks share ResolveDeleteConfirmation, which is also available to
input tests; No refreshes the stored caption without saving to disk.

## Photo center and filename

The next point, STR144[16]=(350;220), is stored in dialog+104/+108 for the
photo center. It is separate from both text controls.

The filename child+f4 is `cTSWinText` (0x29fb40), font-array+20 = slot8.
STR144[17]=(100;415) is its origin; [18]=(450;20) is its size, so rect is
(100,415)-(550,435). Its horizontal alignment is2, background opaque=false,
word-wrap flag2=false. STR144[19] is sent to SetToolTip0x504ad0; it is NOT a
filename format string. Update GetFilename passes the current page's name
to the label caption. Most importantly, Init calls HideWindow immediately
after adding the label at0x29fcd4..0x29fce0. Update does not show it in the
recovered body. Displaying an unrequested filename permanently was wrong.

## Port

The isolated UIOriginalTextEdit uses original glyph metrics for wrap,
selection, caret hit tests and clipped drawing, with transparent background,
4096-character capacity and multiline input. Existing InputManager handles
text, clipboard and delete/select-all; the new control handles glyph-based
navigation and undo/redo. Scrapbook commits the buffer before navigation,
and persists on Done at the native points. Existing model callers retain
immediate-save behavior through an optional `persist` argument.

The editor isolates three shared-input compatibility defects: it normalizes
backwards copy/cut ranges that end at zero, caps incoming text before the
preexisting suffix rather than truncating that suffix, and supplies explicit
word-delete ranges to avoid the shared helper's zero-range/end-of-buffer
sentinel collision. CRLF paste cursor positions are mapped into normalized
LF text before applying the capacity adjustment. These fixes affect only
the restored Scrapbook editor, not the shared InputManager.

Exact native selection scrolling animation, cursor blink timing, and every
legacy text-edit shortcut have not been runtime compared to the original.
The port stores caret position as a character index with no separate
soft-wrap affinity: the index shared by the end of one wrapped line and
the start of the next resolves to the next line. Clicking the preceding
line's far right or pressing End there can therefore display the caret at
the next line's start. Text remains intact, but this interaction needs
comparison with the original before claiming complete editing parity.
Mouse word selection by double-click, drag acceleration, undo coalescing,
and the complete native shortcut table are likewise not established by
the recovered dialog call sites. The restored core is editable captions,
original dimensions/ink/font/selection paint, basic selection/clipboard/
undo/navigation, and the proven caption commit/save command points.
Use the input gate and populated album captures to verify the restored
composition and persistence, and retain these limits in parity claims.

## Final package visual review

Independently inspected the final package's populated and selected-caption
captures in `build/ui-audit/r238/800x600` and `1280x800`. PNG IHDR dimensions
are exactly 800x600 and 1280x800. The fixed 700x560 board fits both; caption
lines, inverse selection, navigation buttons, Done/Delete captions and the
frame are legible and unclipped. The normalized font draw adds no observed
text clipping regression.

The folder named `800x600@2x` actually contains **1600x1022** captures,
equivalent to only 800x511 logical pixels. In that capture the 700x560 board
clips at the top and its Done/Delete footer extends below the image. This
is a failed fit check at the available logical height, not a passing
800x600 high-DPI check. Root was notified; a supported taller logical
viewport or an explicit limitation is needed. No speculative shrinking of
the original board was introduced during review.

### Fit correction recheck

The failed captures above were retained under `before-fit-*`. Fresh captures
in the original three directory names were independently reviewed after
the viewport-fit correction. Physical PNG dimensions remain 800x600,
1280x800 and 1600x1022; the requested 2x run now uses effective scale
1.7033333 and a 939x600 logical viewport. Scrapbook's full board, top frame,
caption selection and Done/Delete footer are now visible at the fitted
scale. Both 1x sizes remain clean. The prior Scrapbook clipping rejection
is resolved by fitting the overall UI scale, without changing the original
700x560 dialog geometry.

### Final package recheck

Re-opened the final original-named captures after the fractional tile and
configured-resolution persistence fixes. The selected two-line caption,
complete board/frame, navigation buttons and Done/Delete footer remain
visible and clean at the fitted high-DPI scale. The 1280x800 capture remains
clean as well. No additional Scrapbook clipping or selection-paint blocker
was found. Physical PNG dimensions were reconfirmed as 800x600, 1280x800
and 1600x1022; the final fitted run retains 939x600 logical space. The input
parity residuals recorded above remain explicit and are not erased by this
visual acceptance.
