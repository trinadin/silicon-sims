# Original picture-in-picture: read-only R239 assessment

## Existing production entry point

`FreeSO/TSOClient/tso.simantics/VMContext.cs` registers opcode 35
(`special_effect`) as `VMSpecialEffect`. Its operand already carries Timeout
(u16), Size, Zoom, Flags and StrIndex (signed bytes). The primitive's comment
identifies string table 305 as the screenshot-caption source.

The handler presently uses only Flags bit 3 to center the main world camera.
It otherwise returns true. No PIP event reaches the UI: `VMEventType` has no
PIP variant, and `TS1GameScreen.Vm_OnGenericVMEvent` only handles build/buy
mode changes. `TS1LivePIP` is a stored options checkbox without a corresponding
PIP window implementation. The unrelated TSO `UIGizmoPIP` is an avatar preview,
not this TS1 event camera.

## Recovered native window behavior

`pip-window.txt` is a fresh disassembly of the owner's executable from
`0x298c90` through `0x29a5a0`, generated with the existing Capstone helper.
It contains the complete PIP class lifecycle and static dimension initializer.

- `cDDDSimsView::DoPictureInPicture` ends at **0x213594**. The existing
  `pip-dispatch.txt` ending at 0x2135a8 contains the complete function;
  bytes after the return are symbol metadata, not missing implementation.
- Dispatch calls PIP `Open` at 0x213574, or `Close` at 0x213580. Another branch
  performs optional auto-centering and event snapshots instead. Those paths
  must not be collapsed into a single unconditional main-camera capture.
- Native dimension triples are **100x100, 200x200, 300x300**. The static
  initializer writes these at 0x29a568..0x29a57c. `Open` selects them from its
  size argument at 0x299c40..0x299cd8.
- The English live-mode location is `(parentWidth-width-5,
  parentHeight-height-105)` at 0x299e88..0x299f18. The extra-double-byte branch
  reserves 155px instead; another mode uses only the 5px lower margin.
- The close button loads resource **30**, four horizontal states, in Init
  0x29a2c8..0x29a32c. `Open` positions it at `(width-buttonWidth-4, 4)`.
  Init loads STR#156 and uses its first string for window tooltip text.
- An open timed PIP rejects a new request for a different object; a request
  for the same object replaces its timer (0x299aa0..0x299ad4). Border/outside
  targets are rejected at 0x299ad8..0x299b50. An optional request flag skips
  opening when the object's bounds are already fully visible in the main view
  (0x299b98..0x299c3c).
- Positive duration subscribes a window timer at 0x299f1c..0x299f34. Timer
  expiry unsubscribes, clears duration, and hides via `Fade(false)`.
- Escape hides the PIP (0x299580..0x2995a0). Left-click stops Sim tracking,
  scrolls the main camera to the PIP target/level, and hides the PIP
  (0x299730..0x2997f4). Close commands also unsubscribe the timer.
- LivePIP controls whether the image rebuilds during paint or is captured
  when shown. `Fade` also respects BoboVision; it is not unconditionally a
  timed alpha tween despite the function's name.
- `BuildImage` renders an independently centered view, optional caption, and
  a one-shot auto-snapshot into the family scrapbook. The caption/snapshot
  request is cleared after fulfillment (0x2993d4..0x299484).

## Scope assessment

The event entry point and much of the window contract are available, so a
future restoration can be concrete. A faithful implementation still needs:

1. Decode `special_effect` flags and argument conversion at the original
   primitive caller, including open/close, live/static, zoom, size, timeout,
   skip-if-visible, auto-centering and automatic screenshot caption.
2. Introduce a typed simulation-to-UI event without changing the simulation's
   synchronous true-return behavior or mutating the main camera by default.
3. Render a second target-centered world view with its own size, zoom and
   level; isolate temporary renderer state and verify restoration after every
   render. A crop of the existing main viewport is not equivalent for offscreen
   events, the primary purpose of this window.
4. Implement timer/replacement/target-deletion/close/click-through lifecycle,
   native resources and option behavior; route automatic photo persistence
   through an isolated test album.
5. Exercise actual opcode-35 events and offscreen targets in runtime tests.

A small cosmetic window or repurposed main-camera snapshot would leave the
essential offscreen-event behavior missing. The current round should therefore
report PIP as an explicit remaining workflow unless it undertakes the renderer
and event integration above. This assessment made no source edits, launched
no game, and wrote no owner game/album/neighborhood data.
