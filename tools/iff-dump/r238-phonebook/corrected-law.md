# R238 — Phonebook recovery, independently checked

The R192/R195 prose was not a reliable specification. This recovery uses
Capstone 5.0.7 on the owner's original PPC executable, with full symbol names
from `r141/symbol-index.txt` cross-checked against `r153/r153-symbols.txt`.
The new disassemblies beside this file retain raw addresses and words.

## Contradictions that caused visible defects

- `0x537680` is `cTSWinTextList::__ct`, not `cTSWinText` (whose constructor
  is `0x538a50`). Phonebook fields +108/+10c are real scrolling text lists.
- `SetFont` at `0x535e90` stores font height **plus three** in +e8.
  +e0 is **visible row count**, not pitch. Phonebook assigns ten.
- `cTSWinTextList::TSPaint` at `0x5371b0` passes +64 to buffer fill.
  `RGB(0,0,57)` is the **background**, not dark text.
- The CtrlMgr factory +30 is `DefaultLabel`, not a string-set list.
  Phonebook +118 displays `<caller> doesn't know anyone.` or, after
  selection, `Call <member> <family>.` It has no dropdown, popup or ListBack.
- The (270,275) static point is an initial **size**, not a far corner.
  Both columns start at (34,77)/(351,77), initially width280/height275.
  Crucially, `ChildAdd -> child_add_absolute` invokes child Init at
  `0x508990`, and list Init `0x537534..0x53755c` replaces height by
  `10*(fontHeight+3)`. Font caller `0xe5f98..0xe5fa8` supplies slot12.
- The (50,50) static point is a provisional window origin, subsequently
  centered. The label anchor is (75,16), with width540/height25.
- Phonebook TSPaint blits the board at **(17,8)**, not (16,35).
- Call and Cancel are `DefaultPushBtn`: minimum100x33, at (20,377) and
  (532,377). The prior pair of 65px buttons at the right was invented.
- The phone icon is at ((652-23)/2,377+5) = (314,382). It is not the
  portrait. A separate 45x45 caller/selected-neighbor portrait is at (10,10).

## Text-list behavior

The constructor loads CtrlMgr image slot4 (`WinScrol.BMP`) into +dc.
`Init` constructs a vertical `cTSWinScrollbar`; its x is listWidth minus
sheet height (20), leaving a 20px gutter. Width is17px. It hides when item
count does not exceed page count. `insert` at `0x5369c0` allocates an
imageless cTSWinBtn with SetImage(NULL,4,1); row width=listWidth-20,
height=fontHeight+3. No ListBack bitmap is attached to these rows.

`TileItems` places rows at (0,(index-topRow)*rowHeight+animatedOffset).
Selection calls ScrollTo when outside the page. Up/down/Home/End/PageUp/
PageDown are native keyboard paths. Row buttons enable double-clicks.
Scrollbar vertical sheet group: cells6/7 up normal/pressed, cell8 thumb,
cell9 track, cells10/11 down normal/pressed (raw painter in
`scrollbar-vertical.txt`).

Original Init selects first family then first member. Family selection
refills the member list and selects its first member. Enter activates Call,
Escape Cancel. The selected neighbor changes the header and portrait.
`IsFamilyAvailable` excludes the caller's own family, nonhuman genders,
family zero, and ordinary families without an occupied house. Family-member
and family-list census use neighborhood/family enumeration, filtered by
known relationship entries.

## Implementation and explicit remaining limitations

`UIOriginalTextList` restores the class and geometry, clipped glyph rows,
selection, keyboard navigation, wheel scrolling, page clicks, scrollbar
thumb dragging and double-click activation. It shares these mechanics with
Help. `UIOriginalPhoneBookDialog` restores the board, label, buttons,
initial selection, original text-list fonts, and excludes the caller's own
family. Its complete items remain accessible instead of silently clamping.

The foreground palette is now proven: InitSimsColors `0x25d938..0x25d964`
passes a six-color array to SetDefaultColors: normal #C3CDCD, selected
#00FFFF, hover #FFFFFF, selected+hover #00FFFF, disabled #405D5F,
disabled+hover #405D5F. See `default-button-colors.txt`. The button
CaptionDraw indexes it by state. Precise native scroll animation and
arrow autorepeat remain unported. Portraits use live avatar NeighborId lookup, or the existing temporary
preview-avatar pattern with cleanup for off-lot neighbors.
Family enumeration remains the existing relationship-backed neighborhood
census; its ordering is stable by family ID, and exact original occupied-lot
eligibility remains outside this visual correction.
# Final package visual review

Independently inspected populated and ten-row overflow captures in
`build/ui-audit/r238/800x600`, `1280x800` and `800x600@2x`. PNG dimensions
are respectively 800x600, 1280x800 and **1600x1022** (the last folder name
overstates its available logical height). The 652x426 Phonebook fits all
three actual viewports. Both lists, native scrollbar, cyan selection,
portrait/status, phone icon and Call/Cancel captions are readable and
unclipped. The final normalized font draw introduces no observed clipping
regression. The @2x result cannot establish a full 800x600 logical fit for
taller dialogs; see the Scrapbook review for the concrete height failure.

## Fit correction recheck

Earlier captures were retained under `before-fit-*`. Fresh captures in the
three original directory names were reviewed. The requested 2x run now
uses a 939x600 logical viewport at effective scale 1.7033333 within the
same 1600x1022 physical image. Phonebook's populated/overflow lists and
footer still fit, and the 1x captures remain clear. However, this fractional
scale reveals an obvious grid of seams in the tiled outer chrome, chiefly
the footer and top/side strips. This is a separate visual rejection from
the resolved fit problem. Root was notified to investigate tiled drawing
rounding before claiming the fitted high-DPI composition is clean.

## Final seam correction accepted

The seam-bearing captures above are now retained under `before-tile-*`.
Independently re-opened the final original-named capture directories after
the tile correction and preference-persistence fix. At effective scale
1.7033333 the populated and overflow Phonebook now have continuous top,
side and footer chrome: the previously visible grid is gone. Lists, native
scrollbar, selected ink, portrait/status and Call/Cancel remain readable,
unclipped and within the available viewport. The 800x600 overflow and
1280x800 compositions remain clean. PNG dimensions were checked again:
800x600, 1280x800 and 1600x1022 physical. Both the fit and fractional-tile
visual rejections are resolved in the final package; no additional visual
blocker was found in these assigned surfaces.
