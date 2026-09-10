# R239 independent End and mouse-hit review

Read-only review against the owner's PPC binary with SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`.
Fresh decodes use `r145/capdis2.py` and the complete R153 symbol map:

| Evidence file | Code interval |
| --- | --- |
| line-layout-skeptic.txt | 0x5301a0–0x531180 |
| mouse-hit-skeptic.txt | 0x5339e0–0x5344d0 |
| move-adjust-skeptic.txt | 0x532990–0x532ff0 |
| vertical-hit-skeptic.txt | 0x531ca0–0x531f70 |

No editor source edits, builds or game launches occurred in this review.

## Line representation

- `IsSoftEnd` at0x5303c0 returns byte0x20; `IsCREnd` at0x530400 returns byte0x21.
- `Rebuild` initially sets soft-end for a nonempty string (0x530a90..aa8).
  Reaching NUL clears soft-end (0x530b40..b50).
- Encountering LF clears soft-end, sets CR-end, and appends the previous
  cumulative advance again for that LF (0x530b58..ba0). The line's count
  therefore **includes LF**, even though it has no added visual width.
- The final advance-vector size is stored as line.count and added to the next
  line's start at0x530c68..c74. On an ordinary soft wrap, `start+count` is the
  next line's start.
- `IndexToLineNum` 0x5301e4..250 treats that boundary as belonging to the next
  line; equality belongs to the current line only for the last line/EOF.

The port stores End before LF and Next after LF, whereas on soft wraps it stores
End==Next. Account for this difference when translating native arithmetic.

## End key: current correction supported

`TSOnKeyDown` 0x533560..5bc sets the movement target to line.start+line.count.
If either CR-end or soft-end is set, it calls `AdjustDelta(target,-1)` and adds
that character stride before MoveCursor. For the single-byte English fonts,
AdjustDelta returns -1 directly (0x532dfc..2e20).

Thus End goes to before LF on a hard-break row and before the final wrap
character on a soft-break row. On the port's representation, returning End for
hard breaks and subtracting one for an ordinary soft-ended row is correct. A
row containing `word ` with End==Next==5 should produce caret4, not caret5.
No separate visual caret-affinity model is needed to explain this behavior.

## Right-edge mouse hit: mismatch proven

For the single-byte path, `TSOnMouseDownL` obtains a nearest character index,
then tests `index>0 && index==line.count` at0x5342d4..2e4. It checks CR-end and
soft-end and decrements the index at0x534308 before adding line.start. The
mouse-drag path repeats this at0x533b40..b74; mouse-up repeats the same rule.
The multibyte path subtracts the final character stride instead of literal one.

The port's exhausted HitTestLine return of line.End is therefore wrong on an
ordinary soft wrap: it moves to the following row. It should return End-1 on
that soft-ended row, bounded by Start. Hard-break and final-EOF returns should
remain End, since the port already excludes LF from End. This corrects actual
native behavior rather than introducing a new affinity policy.

## Shared hit-test blast radius: vertical movement is different

The port also calls HitTestLine for Up/Down/Page movement, using pixel width
from the old row's start. Native `VertLineScroll` does **not** preserve pixel X:

1. 0x531dbc..dc4 computes the character column `caret-oldLine.start`.
2. 0x531e38..e64 clamps the requested target row.
3. 0x531e74..ea0 compares column with target.count. Only when column is
   **strictly greater** does it clamp to count-1 for CR/soft ends, or count
   for the final ordinary end.
4. 0x531ef4..1f10 requests MoveCursor to target.start+column. Multibyte fonts
   convert their column count separately.

Native MoveCursor itself crosses to the next row when the target equals a
soft/CR boundary (0x532bbc..c00), so the strict-greater comparison should not
be casually changed to greater-or-equal. Preserving a character column is a
separate proven difference from the current port's pixel-column vertical
movement. Fixing mouse HitTestLine should not be presented as proof that the
port's existing vertical policy is native. Root owns the scope decision and
any implementation/test changes.

## Authorized follow-up implementation

After this read-only report, root authorized editing only Navigate's vertical
movement block. That block now preserves `Caret-oldLine.Start`, uses
`target.Next-target.Start` as the native count (including LF), applies the
strict-greater terminated-row clamp, and selects target.Start+column. The
existing root End and mouse-hit changes were left untouched.

Independent suggested runtime examples:

- `iiiiiiii\nWWWWWWWW`, caret4 then Down → caret13, regardless of glyph widths.
- `abcdefgh\nxy\nlast`, caret6 then Down → caret11, immediately before LF.
- The same text at caret3 then Down → caret12, next row's start. This checks
  the native strict-greater comparison rather than replacing it with >=.
- `abcdef\nxy`, caret6 then Down → caret9, final EOF.

`VertPageScroll` 0x531d0c..18 invokes the same character-column routine with
pageLines*direction. It subsequently sets top-visible-line to its independently
computed page offset at0x531d1c..24. That final viewport operation was outside
the authorized block and is not claimed implemented by this change.

Root subsequently completed the separate Page viewport operation: it computes
clamp(oldTop + pageLines*direction) before moving, then assigns that top after
caret visibility handling. Two production-keyboard fixtures check ordinary
and Shift page movement, including retained selection and screen-row offset.
