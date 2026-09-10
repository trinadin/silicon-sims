# R240 CAS outfit memory and family-name character filtering

This bounded change affects the maintained desktop CAS controller and its
family-name field. It changes no layout, art, font, margins, hit rectangles,
preview geometry, mobile controls, shared FreeSO editor implementation or game
data. Root retains integrated build/runtime/visual validation ownership.

`recover.py` reproduces all evidence from the owner's local PPC PEF, SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`.
The R153 complete symbol map confirms named functions. Earlier prose was
checked against fresh instructions rather than used as the behavioral oracle.

## Four-type outfit memory

- `cWinDesignCharacter` constructor 0x2cfc94..fcc4 zeroes 32 bytes at0x1b8.
  These are four pairs of body/head indices indexed by gender and age.
- The Adult/Child branches at0x2cd7ec..d928 write the current body0x16c and
  head0x168 into the outgoing row, change age, and restore the target row.
  Female/Male at0x2cd93c..da70 perform the same operation for gender.
- Skin branches beginning0x2cda84 similarly save and restore the **same** row.
  There is no independent three-skin memory dimension.
- `PrepareButtons` calls `SanifySuits` 0x2cc840. A missing collection yields
  index-1; negative indices with choices become0; indices beyond the collection
  clamp to count-1. The selected indices are written back to PersonFinder.
- Initialization0x2cf974..9a4 selects indices0/0 for a new person and the
  person's existing head/body for an edit. Other type rows remain zero.

The port previously retained a single unbounded carousel offset across every
age/gender/skin catalog, losing earlier type choices and applying an unrelated
index to the new type. It now stores the currently visible normalized head/body
indices in four rows, restores and clamps after catalog changes, and clears
those rows whenever PrepareEdit starts another native-equivalent dialog session.
Existing draft outfits are loaded by identity and passed through the native
clamp, so a missing outfit no longer accidentally resolves to the last entry.

The existing desktop arrow callbacks and preview selection path remain in use;
the change affects which existing outfit they select. Catalog contents/order
and asset lookup are unchanged. The mobile carousel path is unchanged.

## Family-name filter: exact typed-input rule

Family Init0x2d1628..1630 enables `SetFilterFileChars(true)`. The setter0x52ee60
writes flag0x185. `TSOnCharacter` tests that flag at0x533808 and scans exactly
20 forbidden ASCII bytes before insertion or replacement. Their TOC slot is
-0x4358, pointer is CODE0x59c3a8, and the PEF code section's file start0x8e90
places the table at file0x5a5238. The metadata file records each byte explicitly.

The rejected characters are backslash, slash, vertical bar, asterisk, question
mark, colon, less-than, greater-than, double quote, apostrophe, percent, both
parentheses, ampersand, semicolon, at sign, exclamation mark, number sign,
comma and period. Space, hyphen, underscore and plus are not in the table.

**Paste is different in the original.** `TSOnPaste`0x52f920..fa18 calls
InsertText directly and does not inspect flag0x185. Programmatic SetText also
bypasses this typed-character check. The restoration preserves that distinction;
it does not silently sanitize existing names or clipboard contents.

A CAS-local `UIOriginalFamilyNameBox : UITextBox` filters the pending typed
characters before the existing InputManager mutates the text. This preserves
an active selection when an invalid character is rejected. The fallback
key-to-character path is filtered using the existing key translator. Shared
UpdateState input lists are restored in `finally`, so other controls see the
original frame input. Every inherited visual property remains identical.

## Isolated regression coverage

`AutotestCAS240.Check(out diagnostics)` constructs an unattached desktop CAS
screen with a synthetic catalog callback. It never initializes a VM/lot, loads
owner families, renders a scene, writes persistence or navigates screens. A
private tween manager is restored and stopped without completing fixture
targets. A private focus/input manager exercises the field; the previous shared
update state and clipboard handler are restored. The fixture installs a private memory
clipboard; it does not read or write the owner's system clipboard.

The helper invokes actual age/gender/skin/arrow button callbacks and checks:

- first-visit zero choices for all four types;
- independent restored choices after a full four-type cycle;
- skin-driven clamping and absence of a separate skin cache;
- reset for new and existing editing sessions;
- edited draft's original outfit identities and missing-outfit clamp;
- native empty/negative/out-of-range index laws;
- all20 rejected typed characters preserve selected text;
- accepted punctuation/letters, mixed accepted/rejected input, native paste and
  SetText bypass, and restoration of shared input list references.

Source whitespace validation passes. Integrated build/runtime results are to
be appended by root after execution.

## Remaining scope

- Existing CAS text controls retain their inherited caret/clipping/keyboard
  behavior; this is not a wholesale cTSWinTextEdit2 replacement.
- Native invalid-character denial sound is not newly reproduced here.
- Multibyte input/font-specific character assembly remains the existing port's
  Unicode input path; the recovered forbidden ASCII table is exact.
- Empty catalogs return native -1 in the index law, but the existing preview
  pipeline still expects installed base-game head/body collections to exist.
- Original catalog ordering and fallback assets were not changed or claimed
  newly verified in this round.

## Independent follow-up review

The first integrated run passed the catalog memory checks but failed paste:
the application's default ClipboardHandler is a no-op unless a host installs
one. The helper now temporarily installs its own MemoryClipboard and restores
the exact previous handler. This corrects the test setup without weakening
the native paste-bypass assertion or changing production character filtering.
A second agent independently reviewed all four-type/reset/clamp and filter
paths and found no concrete regression. The separately authorized Mac SDL clipboard restoration now closes the discovered
host gap; see r240-clipboard/restoration-law.md and its real runtime fixture.

Root's focused package run ending23:47:39 passed13/0, including CAS240 with
the isolated MemoryClipboard correction. This confirms the four-type outfit
callback and family filename-filter fixtures in the integrated application.
