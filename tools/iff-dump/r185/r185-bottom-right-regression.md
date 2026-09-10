# R185 — bottom-right People-panel regression audit

Read-only follow-up over the seven standard desktop People panels at a 1024-pixel
logical width.  Production and `AutotestRunner` were not changed.

## Native route and host matrix

The seven visible tabs are, in native order, Mood, Relationship, Job, House,
Personality, Interest, Gift.  The port's category map `{0,3,1,5,2,6,7}` maps
that order correctly to its internal switch.  Native `cWinPeople::SetPanel`
still has two route guards which are not optional presentation details:

* with no selected person, a tab click is ignored/unhighlighted and no host is
  shown;
* House is accepted only when `Neighborhood::GetZoningType` returns native type
  1, otherwise the request is rerouted to panel 0 (none).

At exactly 1024 pixels, native host sizes are:

| surface | native host |
|---|---:|
| Mood / pet motives | 280x100 |
| Personality | 280x100 |
| Relationship | 504x100 |
| Job / Report Card / Dog / Cat / Fame | 504x100 |
| House | 504x100 |
| Interest | 504x100 |
| Gift | 504x100 |

The port currently assigns every mounted desktop Live `UISubpanel` 504x100.
That is exact for five panel families but gives Mood and Personality a 224-pixel
larger cache/clipping rectangle than native.  The shared band behind them still
extends to the screen edge; only these two content hosts stop at x=800.

## Decoded relationship comparator

The comparator passed by native `GetRelatedPeople` was previously left unnamed
in `r185-relationship-house-native-audit.md`.  Its transition vector resolves
to code `0x242040..0x242328` in the owned Complete Collection executable
(SHA-256 `33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`).
It orders candidates by the following keys, in order:

1. member of the active `Family` first (`Family::TestMember`,
   `0x242060..0x242110`);
2. resolved/on-lot person object first (`0x242114..0x242234`);
3. selected-to-candidate relationship matrix slot 0 descending
   (`GetRelation` plus compare, `0x242238..0x242300`);
4. candidate integer/index ascending (`0x242304..0x242314`).

`PersonFinder::GetRelation` writes relation slot 0 to output offset 0 at
`0x241d34..0x241d47`, proving the third key.  Therefore preserving NBRS/dictionary
insertion order is not native and can visibly move cards and page boundaries.

## Concrete current residuals

1. `UIRelationshipSubpanel.UpdateRelView` enumerates
   `source.Relationships.Keys` without the native comparator and explicitly
   claims insertion order is native.
2. Native tab clicks call `Switcher_OnCategorySelect` directly, but that method
   does not update the hidden mobile switcher's `ActiveCategory`.  The family
   selector completion and reopen paths restore from that stale value, usually
   returning a non-Mood panel to Mood.  The hidden `LiveCategories` list also
   omits Interest/Gift and retains legacy inventory id 4, so it cannot safely be
   the canonical native-panel state.
3. `UIOriginalInterestSubpanel` hard-codes cells at y=17 and paging arrows at
   y=25.  Native font[6] height is 13, hence `contentTop=18`; cells start at y=18
   and the 9x49 arrows center at y=34.
4. `UIHouseSubpanel.RecomputeStats` explicitly sets Size, Furnishings, and Yard
   to zero.  Native recomputes and displays all five scores each paint.  The
   exact runtime ladders for those three remain unresolved, so zero is a known
   data-parity placeholder rather than a valid native result.
5. The native no-selection and House-zoning visibility guards are absent from
   the direct People-tab path.  Several panels also retain their last painted
   values when `SelectedAvatar` becomes null, amplifying the stale-host result.

The Report Card width is **not** a residual: People establishes the 504x100
Report Card host before its `Init`, and the grade button intentionally spans
that complete host at 1024.

## Implementation closure

The parity pass following this audit closed the evidence-backed items without
inventing the two unresolved mappings:

* `UIRelationshipSubpanel` now applies the decoded four-key comparator: active
  family, resolved runtime person, relation slot 0 descending, candidate index.
* desktop People routing now persists the accepted category, exposes the exact
  seven-category census `{0,3,1,5,2,6,7}`, rejects legacy id 4, and clears the
  host/highlight when there is no selected person;
* the host matrix is 280x100 for Mood/Personality and 504x100 for the other five
  families at the original 1024 logical width;
* Interest cells now start at y=18 and both paging arrows at y=34;
* Interest and Gift recompute their native 1024/800 page laws when the host is
  resized: Interest uses 3x3 / 2x3 cells (9/6 per page), while Gift uses 9/5;
* mood-trend history is now scoped to the selected person, so webcam changes
  establish a fresh zero-delta baseline instead of comparing two Sims;
* House now applies the native residential-only route guard through an explicit
  semantic translation: native zoning 1 (`residential`) maps to the provider's
  local value 0, while native 2 (`community`) maps to local 1;
* every standard People content host is invalidated on its update tick. This is
  required because the port uses `UICachedContainer` while native repaints live
  values; without it, motives, skills, relationship values, and Job-family mode
  swaps can remain frozen on the previous cached surface.

The zoning-number mismatch is resolved without changing provider storage. The
owned executable's `SetZoningType` string branches prove native 1 writes
`residential` and native 2 writes `community`; the provider's parser proves its
0/1 encoding of those same words. R186 closes Size with the exact Global.iff
STR#505 curve and native square-feet-per-family-member input. Furnishings and
Yard remain explicit data-law residuals until their native operands have exact
Simitone equivalents.
