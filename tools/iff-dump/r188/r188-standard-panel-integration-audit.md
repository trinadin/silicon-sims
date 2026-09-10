# R188 standard People-panel integration audit

This closes the shared-chrome and integration pass across the seven standard
desktop People panels in the user's owned **The Sims Complete Collection**
data: Needs, Relationships, Job, House, Personality, Interests, and Inventory.
The specialized Job surfaces (adult career, child report card, Fame, dog
skills, and cat skills) were audited in the same routed host.

## Proven corrections

- Every `SetImage(_,4,1)` People tab and Interest pager now uses the native
  `col = (entered ? 2 : 0) + (active ? 1 : 0)` selector. Real mouse dispatch
  probes cover normal, hover, down, dragged-out down, selected, and selected
  hover. `UIOriginalSheetButton` also uses inherited `UIButton.Disabled`, so a
  disabled control cannot accept hover/down/click input.
- `Live.iff STR#134` (`ModeTips`) is loaded and pinned byte-for-byte. Its ten
  English entries are Mood, Relationships, Job, House, Personality, Interests,
  Inventory, Skills, Grades, and Not Available to Pets. The seven standard
  tabs use entries 0 through 6 as their native quick tips.
- Every tooltip-bearing custom control in the routed panels installs the
  engine tooltip handler: motive gauges, relationship cards, House rows,
  personality rows and zodiac, Job captions/report grade, and Gift cells.
  Occupied webcam buttons expose the person's name; empty webcam slots now use
  a null payload so the engine cannot paint a blank 6x15 tooltip box.
- The Inventory filter column now uses the literal single-byte
  `InitInventorySorts` tops `M+20`, `M+41`, and `M+62` (Magic, Other, Gift).
  The prior transcription was 30px too high and covered the leading `Inv` in
  the panel title. This branch is selected by raw opcode `0x41820140`
  (`beq`) when `IsDoubleByteUI == 0`; the legacy disassembly text's `bne`
  label is a decoder error, not evidence for swapping the English/DB layouts.
- The Needs bar colour and overall People mood-band laws are the exact R188
  executable-derived laws documented in `r188-motive-ramp-law.md`.
- House Furnishings now halves the aggregate wall-style value once, matching
  native odd-price rounding. Yard, label order, score order, and fact rows are
  pinned as documented in `r188-house-furnishings-yard-audit.md`.
- Shared live popups remain above the 100px host and clip/wrap within their
  native panel-relative bounds. Hidden standard-panel painters emit no stale
  pixels after a category change.

## Final verification snapshot

The final integration binary was rebuilt after all tab, tooltip, webcam,
Inventory-filter, and House changes. `Simitone.Client.dll` SHA-256:

```text
ec0442abb5d2993673e409295c317d2bbbbf848482015630c548b49d0aa6e384
```

The matching `FSO.SimAntics.dll` SHA-256 is:

```text
c19e91570cad3ee6f6f41ea1a1f5d93711de05872bbfea0a3e7f732e9edd1869
```

Verification results from that synchronized runtime:

- focused corpus + Live + Job + Interest/Gift + visual survey: **9 passed,
  0 failed**;
- complete seven-panel route + hidden-paint checks: **11 passed, 0 failed**;
- extended UI text/value/interpolation suite: **19 passed, 0 failed**;
- focused bar/webcam hit geometry and empty-tooltip law: **6 passed,
  0 failed**.

Fresh 1024x768 captures were generated for all seven panels, all standard
popups, report card, Fame, dog skills, and cat skills. They show one 100px host
with no repeated panel, overlap, clipping, or stale background controls. The
Gift fixture owns no inventory objects, so the survey records both that real
empty state and a test-only populated fallback/count fixture driven by the same
synthetic token as the `uiinterest` regression. Its full-width Gift popup is
therefore covered at pixel level too.

## Explicit residuals

This pass does not guess at the two already documented House data residuals:
native stats snapshot timing at four lifecycle triggers, and the roof-tile
contribution for which the port has no per-tile roof-layer operand. Neither is
a panel geometry or hidden-paint defect.
