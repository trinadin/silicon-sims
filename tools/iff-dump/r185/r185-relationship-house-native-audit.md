# R185 — desktop Live Relationships + House native law

Read-only parity audit of the bottom-right Relationships and House surfaces in
the owned **The Sims Complete Collection** Macintosh executable.  Coordinates
below are native logical pixels.  This note is an implementation target; it does
not assert that every data source in the current port already produces the
native value.

This note supersedes the relationship interpretations in R159 which described
two cards per person, a single NBRS bitfield / mutually-exclusive marker, an
80x20 sort control, or one visible score.  The executable proves the opposite:
there is one 45x90 English card per target, two scores, three independent marker
bytes, and each 80x20 sort sheet is four horizontal 20x20 states.

## Provenance and decoded ranges

Primary executable: `game-data/The Sims/The Sims Complete`, SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`.

| subject | executable range / evidence |
|---|---|
| Relationship population, filters, paging, placement | `cWinPeople::BuildRelationships` `0x289880..0x28a4e1`; raw `r159/r159-rel-build.txt` |
| Relationship sort construction | `cWinPeople::InitRelationshipSorts` `0x28e910..0x28ee15`; `r159/r159-rel-sorts.txt` |
| Relationship card values | `cWinRelationship::SetRelationship` `0x29a5c0..0x29a759`; `r159/r159-rel-setrel.txt` |
| Relationship card paint / popup client | `0x29a790..0x29bb31`; `r159/r159-rel-tspaint.txt`, `r159-rel-setupclient.txt` |
| Relationship click dispatch | `cWinRelationship::TSOnMouseDownR/L` `0x29aad0/0x29ab50`; `cWinPeople::TSOnCommand` relationship branch `0x28c6f4..0x28c7c0`; fresh Capstone decode in R185 |
| Relationship card construction / init / constants | `0x29bbb0..0x29c2e5`; `r159-rel-init.txt`, `r159-rel-ctor.txt`, static initializer |
| Native candidate filter | helper `0x242360..0x242c59`; Capstone decode `/tmp/rel-audit-getrelated.txt` during this round |
| House init / paint / commands | `cWinSubpanelHouse` cluster `0x2a0000..0x2a16f1`; `r132/r132-decode-houseinit.txt`, `r132-decode-housepaint.txt`, Capstone command decode |
| House data law and strings | `r133/r133-housestats.md`, `r133/r133-house-canon.txt` |

The shipped English `Fonts\\variablesans_07.ffn` has SHA-256
`aaa09e228e3bd37667bda0239d28bdc159559f6d31cd3c77062ed54efc20b905`
and measured `TSCharHeight == 13` (maximum glyph `height + topOffset` 11,
minimum top offset -2).  That measured height is used in the formulae below;
it is not a guessed modern-font line height.

## Relationships

### Host, title, sort controls, and pager

The English relationship host is native logical width 280 at 800-wide and is
expanded by 224 pixels at exactly 1024-wide, to 504.  It occupies the native
People-panel vertical band used by the relationship builder.  Its title is
`Live.iff STR#132[0]` (`Relationships`), at `(5,0)`, font slot 6.  The DBCS title
point is `(10,5)`.

Let `W` be the relationship-host width and `H6` be the font-6 character height.
The four sort controls are 20x20 and are stacked as follows:

| mode passed to native finder | control | resource | host-local origin |
|---:|---|---:|---|
| 0 | Family | RT 4108, `RelFamilySort.bmp` | `(3, H6+1)` |
| 1 | Friends | RT 4109, `RelFriendSort.bmp` | `(2, H6+22)` |
| 2 | Famous | RT 4111, `RelStarSort.bmp` | `(2, H6+43)` |
| 3 | All | RT 4110, `RelAllSort.bmp` | `(2, H6+64)` |

For shipped English font 6, `H6 == 13`, hence the origins are `(3,14)`,
`(2,35)`, `(2,56)`, `(2,77)`.  All is selected initially.  Each source bitmap
is 80x20 and contains four horizontal **20x20 state cells**.  Native state is a
normal cTSWinBtn state selection; scaling the full 80x20 sheet or tinting a
single sheet is not equivalent.

Tooltips are `UIText.iff STR#240[0..3]` in this semantic order:
`Family Only`, `Friends Only`, `Famous Only`, `All`.

The list layout rectangle begins at `(35,18)` in English and ends at
`(W-2,150)` in the builder's relationship coordinate band.  English cards are
laid out horizontally at:

```
card[i] origin = (35 + 45*i, 18)
card[i] size   = 45 x 90
```

There are five visible slots at ordinary widths and ten **only when the screen
width equals 1024**.  The DBCS branch uses a 45x110 card and the alternate list
top/bottom constants `(35,140)`.

The previous/next controls are RT 100/101 (`ScrollLeft.bmp` /
`ScrollRight.bmp`).  Each source is 36x49, four horizontal states, and each
destination control is 9x49.  Their host-local placement is:

```
previous = (24,     H6 + floor((hostHeight-H6-49)/2), 9,49)
next     = (W - 11, H6 + floor((hostHeight-H6-49)/2), 9,49)
```

For the English 100-pixel band this is y=32: previous `(24,32,9,49)`, next
`(269,32,9,49)` at W=280 and `(493,32,9,49)` at W=504.  Previous is visible iff
`page > 0`; next is visible iff `(page+1)*capacity < filteredCount`.  Changing a
filter resets the page to zero.  Rebuilding also decrements a stale nonzero page
until it contains an item, rather than leaving an empty page.

### Exact 45x90 English card compositor

One card represents one target person.  It contains no visible name caption;
the target's composed full name is installed as the card quick tip.

| layer | card-local destination / rule |
|---|---|
| portrait-state frame | `(2,0,40,40)`; one 40x40 cell from RT 1049 `UnknownRel.bmp` (200x40 / five horizontal states) |
| score at object field `+408` | font 7 text rectangle `(0,38,45,13)`, i.e. y `38..51` |
| `+408` relationship bar | origin x=2, y `51..53` (2 px high) |
| score at object field `+404` bar | origin x=2, y `55..59` (4 px high) |
| score at object field `+404` text | font 7 rectangle `(2,59,40,13)`, i.e. y `59..72` |
| smiley | RT 4105, 9x9 at `(2,70)` when byte `+412 != 0` |
| heart | RT 4106, 9x9 at `(14,70)` when `+413 != 0` and `+414 == 0` |
| deep heart | RT 4107, 9x9 at `(14,70)` when byte `+414 != 0`; it replaces heart only |

The score-text / marker overlap at y70..71 is native.  Do not separate those
elements merely to make the layout look tidier.  Smiley is independent and can
coexist with either heart.  Only deep-heart supersedes ordinary heart.

For either relationship score `s` in `[-100,100]`, the bar endpoint is:

```
endX = 2 + trunc((s + 100) * 40 / 200)
```

The native colour path interpolates red `(255,0,0)` to green `(0,255,0)` and
uses its direct fill / one-pixel edge paint.  The portrait-state selection uses
field `+404`:

```
state = trunc((score404 + 100) / 40)
if state == 5: state = 4
```

The two fields come from `PersonFinder::GetRelation` output words `+72` and
`+76`.  Their exact user-facing daily-versus-lifetime names are deliberately
left unresolved here: the executable proves placement and two distinct values,
but this round did not prove which C# NBRS slot is which.  Implementations must
not collapse them to one value or fabricate a nonexistent list index.

### Delta-meter caveat

The relationship card owns a real `cWinDeltaMeter`; it is not just the two
static score bars.  `__sinit_:WinRelationship_cpp` writes the base point `(2,52)`.
`cWinRelationship::Init 0x29bd30..0x29bd58` adds six to y and calls
`SetTopLefts` with:

```
lane A top-left = (2,  58)
lane B top-left = (22, 58)
meter width     = 20
```

`SetRelationship` pumps the relation sample into this child at `0x29a700..14`.
The shared `cWinDeltaMeter` decode in R184 now proves more than the original
version of this note stated: it draws five procedural 3x5 history arrows.  A
negative sample uses the left-pointing mask and begins in lane A at
`x + (20-4) = 18`; a nonnegative sample uses the mirrored right-pointing mask
and begins in lane B at x=22.  Later history records step horizontally by four
pixels.

What remains unresolved is not the bitmap or sign lane.  It is the exact
`cAverageHistory` record/aging law, ramp colour selection, and animation
displacement/timing for the relationship configuration, plus how that history
survives native card/page rebuilds.  The current port has no equivalent
persistent history source.  Substituting a generic modern arrow, deriving a
fake delta from the two static scores, or pretending the meter is a third value
bar is therefore still not proven parity.

### Filter predicates, ordering, and input

The helper at `0x242a44..0x242bec` proves the mode switch:

| mode | exact acceptance predicate after the native validity checks |
|---:|---|
| 0 Family | selected Neighbor halfword `+0xEE ==` candidate Neighbor halfword `+0xEE` |
| 1 Friends | call `PersonFinder::GetRelation`; returned relation byte at offset `+5 == 1` (the byte copied to card `+412`) |
| 2 Famous | resolved candidate Person-data halfword `+0xA2 != 0` |
| 3 All | accept every otherwise-valid candidate |

Negative or values >=3 follow the all-candidates path in the helper.  The
selected person is excluded before these predicates.  The completed list is
passed through the native comparator at `0x242c24..48`; preserving source-map
insertion order is not the native ordering.  This round did not assign a safe
high-level semantic name to that comparator, so a score/name ordering claim
would be speculative.

`cWinRelationship::TSOnMouseDownL` at `0x29ab50` delegates to the normal
`cTSWinBtn` path.  The resulting parent command enters the relationship branch
of `cWinPeople::TSOnCommand` at `0x28c6f4`.  That branch proves this exact
single-popup state machine:

1. popup hidden: attach the clicked card's client, call `SetupClient`, and
   `EnablePopup(client, people)`;
2. popup visible on the same card: `DisablePopup(true)` (toggle closed);
3. popup visible on another card: detach the old client, attach and set up the
   clicked client, then `SetClient(newClient)` on the **same live popup**.

Rebuilding a relationship filter/page disables the popup before it detaches the
old card clients.  The popup uses RT 3750
`cpanel\Backgrounds\SocialInfoPopup.BMP` (133x103) in the shared
`cWinLivePopup` chrome.

The owned `Live.iff STR#132` English set has 15 visible entries: title
`Relationships` followed by seven visibly identical `Relationship` / generic
daily-and-lifetime explanation pairs.  The `***New - Hot Date*` text is in the
translator-comment field, not in the displayed string; describing the visible
English descriptions as placeholders was an earlier audit error.

`cWinRelationship::SetupClient` `0x29a828..0x29aa64` then appends a
target-specific suffix to that selected generic description.  It calls
`PersonFinder::GetName`, reads/calculates person-data word 70 (zodiac), calls
`GetZodiacName`, and calls `PersonFinder::GetDescription`.  The shared literal
pointer loaded from TOC `r2-0x4f38` resolves to sec1 `0x58b40`; its byte-exact
NUL-separated strings are `" ("`, `")"`, `" -- "`, and `"\n\n"`.  Therefore
the visible body is:

```
STR#132 selected description + "\n\n"
    + name [+ " (" + zodiac + ")"] + " -- " + catalog description
```

`PersonFinder::GetName` `0x243260..0x243888` starts with the target
`ObjSelector` catalog name.  When person word 32 is neither 0 nor 4 and word
61 is nonzero, it appends one space plus that FAMs family name.  The popup is
therefore genuinely card-specific even though all seven shipped English
STR#132 pairs are visibly identical; retargeting must rerun this composition
while preserving the same popup window object.

`cWinRelationship::TSOnMouseDownR` at `0x29aad0` reads the card's target index,
resolves its live `cXObject`, and immediately calls
`CenterHouseViewOnMe(true)`.  It returns handled even when no live target is
resolved.  In the port, `InputManager.HandleMouseEvents` derives all callback
edges from `LeftButton`, so testing `RightButton` inside that callback can
never implement this law.  The lawful bridge is a hovered right-button edge
poll (the same pattern already used by `UIVMPersonButton`).

## House

### Geometry and composition

`cWinSubpanelHouse::Init` establishes a 427x100 native control.  English content
occupies only the leftmost 197 pixels:

* title `Live.iff STR#133[0] = House`, font 11, rect `(5,0,100,20)`, draw point
  `(5,0)`;
* DBCS title rect `(5,5,200,20)`, draw point `(13,8)`;
* nine text-only cTSWinBtn controls, font 8, no button bitmap;
* five 40x16 rating backdrops / fills.

The score-label and bar geometry is:

| index | label | label top; visual right edge | bar origin |
|---:|---|---|---|
| 0 | Size | y=7; x=154 | `(157,6)` |
| 1 | Furnishings | y=25; x=154 | `(157,24)` |
| 2 | Yard | y=43; x=154 | `(157,42)` |
| 3 | Upkeep | y=61; x=154 | `(157,60)` |
| 4 | Layout | y=79; x=154 | `(157,79)` |

The executable's static creation anchor for each score caption is x=85, then
the measured caption control is repositioned so its rendered right edge is
x=154.  The native hit rectangle is the measured text control, not an invented
154-pixel-wide row.  English labels use `Live.iff STR#133[1..5]`.

The four fact controls are left-aligned at x=0, 80x16, with top positions
`18,38,58,78`:

```
STR#133[6] Sq. Ft.: %d
STR#133[7] Bedrooms: %d
STR#133[8] Bathrooms: %d
STR#133[9] Lot: %s
```

The ordinary type-4 control palette is native normal `#C3CDCD`, pressed /
selected `#00FFFF`, hover `#FFFFFF`, disabled `#405D5F`.

### Rating art and data order

Each bar first paints the full 40x16 off backdrop RT 4919
`Backgrounds/HouseSubBarsOff.BMP` at the table origin.  It then crops active RT
4900 `HouseSubBars.bmp` (40x16) to:

```
points = clamp(score,0,100) / 10       // integer truncation, 0..10
active source/destination width = 4 * points
```

The native score order is exactly Size, Furnishings, Yard, Upkeep, Layout.
`TSPaint` constructs a fresh `HouseStats`, calls `House::GetHouseStats`, and
queries all five values every paint.  It does not leave the first three at zero
and it does not use a stale creation-time snapshot.

The data structure and already-decoded facts are:

| HouseStats field | meaning |
|---:|---|
| `+0` | square feet |
| `+4` | resident count (size-score input) |
| `+8` | bedrooms |
| `+12` | bathrooms |
| `+20` | lot size index |
| `+24` | layout score |
| `+28/+32` | object-module aggregates |
| `+36` | upkeep aggregate |
| `+40` | object count |

Square feet accumulates native room area helper times 9.  Room flag `+88`
marks a bedroom and `+92` a bathroom.  The lot-size ladder is `<40 Small`,
`40..49 Medium`, `>=50 Large`, with words from STR#138[0..2].  Exact native
Size/Furnishings/Yard ratings depend on runtime gamestate ladder tables; that is
a data-parity residual, not permission to change the proven geometry or display
zero as if it were native.

### Selection, help, and quick tips

The nine buttons map in visual order to nine title/body pairs in STR#135:

```
House Size, Furnishings, Yard, Upkeep, Layout,
Square Feet, Bedrooms, Bathrooms, Lot Size
```

`TSOnCommand` command 1 identifies which of the nine children fired, selects
that one, and creates or updates the existing `cWinLivePopup` with the matching
pair.  Command 19 marks/repaints the panel.  Native does not destroy and replace
the popup with a differently skinned generic options dialog on every click.
The client constructor at `0x2a1390..0x2a13bc` passes zero for both selector
slots, both bitmap slots, and both special-scale slots. `RebuildBuffer` therefore
uses the no-pane branch: width 559, title/body x=15..545 (530 px), y=12/31,
minimum height 127. House must not reserve an empty 168-pixel image pane.
`SetQuickTips` assigns the corresponding help title/quick-tip data to all nine
controls when quick tips are enabled.

## Parity gates implied by this audit

1. Relationship: assert title/font/position, four vertical 20x20 sort cells,
   All default selection, exact tooltip strings, 5/10 capacity law, exact pager
   bounds and visibility law.
2. Relationship card: assert `(35+45*i,18)`, 45x90, both score texts, both bar
   rectangles, independent smiley plus heart/deep-heart rule, no visible name,
   full-name quick tip, and left/right-click semantics.
3. Relationship filters: fixture each predicate separately, including family
   field equality and friend byte equality rather than a score threshold.
4. House: assert all nine measured text controls, right-edge x154, bar x157,
   y tables, STR#133/135/138 content, one selected help item, and fresh recompute.
5. Keep two residuals explicit: relationship delta-meter internal animation and
   the runtime House Size/Furnishings/Yard score ladders.  Neither residual
   invalidates the exact compositor geometry above.
