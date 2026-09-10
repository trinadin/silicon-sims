# R179 — Job title is data, not a visible child

## Reported failure

The packaged Job survey still drew `Job` over `Cooking` in the first skill
row. R171 pinned the recovered anchors, but its gate did not reject extra text
mounted at those anchors.

## Original executable evidence

`cWinSubpanelJob::Init` in the shipped executable distinguishes the string
table data from the controls that are actually attached:

- `0x2a42a4-0x2a42e4` resolves Live.iff STR# 136 entry 0 (`Job`) and assigns it
  to the internal `cTSString` at `this+0x138`.
- That sequence contains no window construction, bounds call, or child attach.
- `0x2a42fc` begins the six-iteration control loop. Each iteration constructs a
  420-byte `cFlashyBtn`, gives it the recovered 62x16 bounds, attaches it, and
  advances through the six native point-table entries.
- The skill anchors remain `(93, 6 + 15*i)` and the pip anchors remain
  `(157, 7 + 15*i)`.

Therefore STR# 136[0] is loaded state, not a visible title at `(35,2)`. The
port-authored title was the object overlapping the native `Cooking` row.

The remaining oversize text had a separate cause. The original does not use
the caption face for these controls:

- `cWinSubpanelJob::Init` loads the font pointer array through
  `TOC[-0x7178]` (BSS `0x92900`). `0x2a4090` saves font[8] at stack `0x68`
  and `0x2a4098` saves font[7] at stack `0x6c`.
- Every one of the six skill `cFlashyBtn`s calls `cTSWinBtn::SetFont` at
  `0x2a4430-0x2a4434` with stack `0x6c`: all six use font[7].
- The four summary controls select from those two stack slots at
  `0x2a4ad8-0x2a4aec`. Their selector base is `TOC[-0x4ee0]`.
- Unpacking PEF section 1 resolves that TOC entry to section-relative
  `0x59308`, whose first four big-endian words are `{1,0,1,1}`. With
  stack `0x68` as index 0 (font[8]) and stack `0x6c` as index 1 (font[7]),
  the exact summary sequence is `{font[7], font[8], font[7], font[7]}` for
  career, salary, performance, and friends.

## Port correction

The desktop branch no longer constructs the extra `UIOriginalText("Job")`.
It now mounts the career/performance/friend text and all six skill labels in
font[7], and the salary in font[8]. Each skill label also carries the decoded
62x16 native control bounds. The career name, salary, summary art, six skill
anchors, and six pip rows otherwise retain their decoded geometry. The mobile
branch is unchanged.

## Gate extension

`uijob` now requires:

- no visible original-glyph child equal to STR# 136[0];
- the four summary twins use `{7,8,7,7}` and every skill twin uses font[7];
- each skill twin retains a 62x16 native control rectangle;
- the career and salary strings to end at least two pixels before the native
  skill-label column (`x=93`);
- every skill string to fit its 62px control before the pip column.

This closes the false-positive path where all anchors were correct but an
extra control still made the composed panel overlap.

The republished arm64 app passed the focused packaged survey:

```text
AUTOTEST uijob ... desktopGeometry=True host=504x100 pips=6 bars=1
modernLabels=0 parts=True/True/True/True/True titleAbsent=True
fontLaw=True contentFits=True valuesWired=True floaterTwinned=True
AUTOTEST SUMMARY passed=12 failed=0 skipped=0
AUTOTEST RESULT PASS
```
