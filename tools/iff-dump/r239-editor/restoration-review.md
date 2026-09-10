# R239 Scrapbook editor interaction restoration

The owner-approved restoration changes the maintained
`UI/Controls/UIOriginalTextEdit.cs` caption editor. It does not change the
Scrapbook's rectangle, native font, capacity or button placement.

Fresh original PPC evidence is reproducible with `recover-editor.py`; its
address-labelled output is `editor-contracts.txt`. The executable SHA-256 is
pinned by the script. Original data remains owner-local.

- Native MoveCursor advances from the active selection endpoint before
  clearing an unshifted range. Left/Right no longer collapse to a range edge.
- Native Ctrl+Up/Down and Ctrl+PageUp/PageDown scroll the viewport while
  preserving the caret and selection. Ctrl+Shift with those keys is a no-op.
- Vertical arrows preserve character column across differently sized glyphs.
  Page movement also restores the independently calculated viewport offset.
- MoveWord uses space and LF boundaries, consumes trailing separators to the
  right, and retains the original unusual initial-space behavior. Tabs are
  not treated as word separators by this navigation path.
- The double-click handler selects using native MoveWord(-1), then
  MoveWord(+1) with selection. Its selected range includes trailing spaces.
  The desktop event adapter currently recognizes two clicks within 500ms and
  four logical pixels; that threshold is not claimed to reproduce the
  original system's configurable double-click timing.
- End backs over a soft line's terminal character, keeping the caret on the
  visible row. Far-right mouse hits use the same soft-row terminal rule.
  Native Rebuild includes LF in hard-line counts; the port excludes it from
  End already, so hard newlines and final EOF need no extra decrement.
  Independent evidence is in `end-hit-skeptic.md`.

`AutotestEditor239` exercises the production keyboard and mouse handlers,
including reverse selections, word boundaries, viewport-only scrolling,
hard line End and double-click word selection. The existing `uiscrap` gate
continues to test clipboard, undo/redo, capacity, confirmation and persisted
caption workflows. Integrated results are in the parent round report.

This is not a complete port of cTSWinTextEdit2. Multibyte-language movement,
OS click timing and the other legacy edit controls
remain separate parity concerns until independently verified.
