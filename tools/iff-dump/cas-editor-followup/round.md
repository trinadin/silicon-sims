# CAS biography and preview follow-up

This pass restores desktop biography editing and repairs two preview position
errors. It does not certify complete CAS parity. The user approved restoration
and requested a skeptic; independent findings and review are in `skeptic.md`.

## Biography

R143 decodes cTSWinTextEdit2, font slot 10, white transparent text, capacity
2048, unlimited paragraphs and rectangle (22,425)-(783,521). Fresh native
SetArea/Init/Rebuild evidence supplies the missing five-pixel row inset and
751-pixel wrap width. `recover.py` reproduces `native-editor.txt` from the
owner's hash-pinned PPC executable. Navigation and selection evidence comes
from R239's editor recovery.

The biography now uses UIOriginalTextEdit with the owned font-10 glyph metrics,
original selection inversion, caret and word/line/page navigation, scrolling,
clipboard and undo. The optional horizontalInset preserves existing Scrapbook
callers at default 0; CAS alone opts into 5. Wrapping, glyph paint, selection,
caret and mouse hit testing use that same offset. The outer hit rectangle,
other controls, art and persistence mapping are preserved.

Mode changes and confirmations blur the biography. The final tween visibility
boundary also blurs it, closing the skeptic's reported interval where the
outgoing visible field could otherwise be clicked again before being hidden.

## Preview defects

TS1CASScreen.Update positioned the dedicated desktop preview and then ran the
mobile body-carousel loop, overwriting all 18 body avatars. The head loop was
already mobile-only. The body loop now has the same guard. Real-flow checks
verify that the preview position survives Update and all other carousel
avatars stay hidden.

The preview's world-space position was also assigned to an API accepting tile
XY and height Z. WorldSpace.GetWorldFromTile proves the conversion
(tileX,tileY,height) -> (3X,3height,3Y). The existing world (90,0,58) calibration
now maps to tile (30,58/3,0), instead of rendering at world (270,174,0).
The coordinate contract is source-proven; the intended calibration constant
is an inference from the existing name and camera vector, not newly decoded
PPC camera geometry.

Visual inspection at 800x600 and 1024x768 confirms that the extra carousel
people disappear, but the dedicated Sim remains cropped at different preview
edges. **Preview framing is still an open defect.** The existing fixed camera
calibration does not reproduce the original dedicated preview at both sizes.
Do not close this gap based on the passing pose assertions.

## Validation

- Publish and package completed. All 393 published DLLs byte-match their
  packaged copies (`package-dlls.json`). Final Simitone.Client.dll SHA-256:
  `7d86599e4eccd04c7d5b5b5b38df0d07b0a53b8bd920f85109651e3be2ff5bfe`.
- Focused CAS/Scrapbook: 8 passed, 0 failed (`focused.log`). Existing shared
  editor keyboard/mouse checks and CAS character filtering remain green.
- Final real CAS flow: 93 assertions passed at both 1024x768
  (`preview-flow.log`) and 800x600 (`flow800-validated.log`). Six paragraphs
  survive acceptance, save and fresh disk reparse; original game data remains
  byte-identical according to the existing isolation assertion.
- CAS239 checks the actual biography's font, six paragraphs, inset hit testing,
  wrap width, active-endpoint selection navigation, replacement at capacity
  preserving its suffix, mode blur, tween refocus blur and confirmation blur.
- Inspected both final size captures. Five biography rows fit and the sixth
  paragraph scrolls into view without crossing the field boundary. Comparison
  against the prior R246 capture finds **0 changed pixels out of 384,944** in
  the 800x600 UI outside biography and animated preview rectangles. This bounds
  this change's visual effects; it is not original-game screenshot equivalence.
- All completed runs reached both after-Run and after-Dispose exit probes.
  `validate.py` restores owner settings byte-for-byte, including on failure.
- Final full suite: **142 passed, 0 failed** (`default-validated.log`),
  with clean Run/Dispose and byte-identical settings restoration.

The first expanded flow fixture incorrectly sent LF inside printable
FrameTextInput; the shared adapter rejects control characters there. It was
corrected to send actual Return key events between paragraph text events.
The three corresponding failures remain in `flow.log`; no expected content
was weakened. The subsequent runs passed.

The initial packaged baseline had one `carseek` failure (missing worker and
no return/pay event), with 141 other checks passing. A subsequent full suite
passed 142/0 before the preview corrections (`default-final.log`). Process
exit 0 alone is not treated as a passing test result.

## Remaining work and source ownership

Prioritize preview framing/scene composition and the single-line name editors.
Names require horizontal scrolling, native Enter command routing, pasted-LF
semantics, initial focus and empty/focused frame behavior. Exact focused frame
presentation, native undo coalescing, OS double-click timing, scrollbar
presentation and multibyte behavior are not certified by this biography pass.

Maintained edits are in the parent Simitone client. The FreeSO tree contains
preexisting uncommitted tutorial work; the integration package includes that
working tree, but this CAS commit does not include or modify it. Recorded
source state is in `source-state.json`. No upstream mirrors, original assets
or package caches were edited as maintained source.
