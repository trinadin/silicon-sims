# Mac clipboard integration

The original family-name edit path filters typed filename characters but its
TSOnPaste52f920..fa18 inserts clipboard text independently of that flag. R240
CAS isolated tests exposed that Simitone's cross-platform desktop entrypoint
had never installed a ClipboardHandler: the shared base implementation always
returned an empty string and discarded writes. The earlier Scrapbook test
installed a private fake, so it could verify editing logic while missing this
host integration gap.

The Mac DesktopGL entrypoint now installs MacSDLClipboard. It uses the existing
shipped `libSDL2-2.0.0.dylib`, which MonoGame already initializes; it does not
initialize/uninitialize SDL or load a second dependency. SDL clipboard text is
UTF8. The pointer returned by SDL_GetClipboardText is always released by
SDL_free in finally. Native errors are propagated without printing clipboard
contents. Other platform entrypoints and clipboard handlers are unchanged.

Shared InputManager already supports Ctrl+C/X/V/A. A Mac-only branch recognizes
left/right Command for those same four keys. It does not set CtrlDown for
Command globally, so arrows/deletion and unrelated commands keep their
existing behavior. If SDL also emits a handled Command shortcut letter as
FrameTextInput, at most one corresponding ASCII letter is consumed for that
key; other input, including Unicode composition, is preserved and the source
input list is never mutated. Tests cover all four paired letters, input-list
preservation, and a mixed paste-letter plus accented/CJK frame. The Scrapbook's existing local Command adaptation remains
compatible. The C/X nonempty-selection check now accepts an endpoint at zero, allowing
reverse selections to reach the existing range-normalization code. Both
directions are tested for every modifier. No field geometry, fonts, text
sizing or hit testing changed.

`AutotestClipboard240.Check` requires the real installed Mac handler and tests
Unicode/newline native text roundtrip and each Ctrl/leftCommand/rightCommand
copy, cut, paste, and select-all path through InputManager. A Command+Backspace
fixture guards against global shortcut remapping. The separate CAS240 helper
uses its own MemoryClipboard and restores the previous handler object, so its
filename filtering test does not touch the owner clipboard.

The runtime test snapshots every NSPasteboardItem and every advertised type's
NSData into private NSPasteboardItems before writing. Missing/lazy data that
cannot be materialized aborts the test before mutation. Finally it clears the
temporary test text and restores the full original item array, including
images and rich-text formats. Merely restoring SDL text would destroy those
formats and is deliberately insufficient. Objective-C declarations were
checked against the installed macOS SDK AppKit NSPasteboard.h and
NSPasteboardItem.h. Native allocations owned by this test are released. Only
pass/failure labels are emitted, never owner clipboard contents. After restoration
the test re-reads the pasteboard and verifies exact item counts, per-item type
counts, and isEqualToData for each saved flavor. The diagnostic says
`nativePasteboardRestore=verified-all-items-types-data` only after those checks.

No build or launch was performed by this agent. Root owns integrated runtime
verification and final package results.

## Integrated verification

Root's focused package run logged13 passed,0 failed at23:47:39, with
`uiclip` reporting `nativePasteboardRestore=verified-all-items-types-data pass`.
This proves the real SDL UTF8 roundtrip, keyboard clipboard routes, and
post-restore item/type/NSData equivalence on the active Mac host. The newest
paired-Command FrameTextInput guard and its added assertions require the
final rebuild planned by root; they are not inferred from this earlier pass.
