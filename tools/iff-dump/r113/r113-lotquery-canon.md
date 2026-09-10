# R113 — Lot-query panel in original .ffn type (street / lot name / description / secondary)

**Slice**: `UIHouseSelectPanel` — the original lot-query popup on the neighborhood
screen (shown when a house is clicked). Its strings were ALWAYS original (street
names from the per-neighborhood STR tables 2000/2001; body/secondary from
UIText.iff); the rendering was modern MSDF labels. This round renders them in
original .ffn glyphs.

## Canon (raw, UIText.iff chunks — same 76-byte-header walk as r111/r112)

- STR# **`MoveInModeStrs`, chunkID 132**: format −3, 480 entries, chunk sha256
  `888ee3da3ae26fff096718f21629dc373682a99ee5f867707fc2a7dea51be552`.
  English [15] (used for the occupied-house secondary line):
  `This house is occupied by another family.`
- STR# **`NghRollover`, chunkID 134**: format −3, 780 entries, chunk sha256
  `70a5d2a201722be18c7566b7c6286360bc75e99b0431a8deb6c519971ca1a4af`.
  English [0] (family description template — note the EMBEDDED `\r\n`, which
  the paragraph twins must honour):
  `The %s Family\r\nNet Worth: %s\r\nFriends: %d`
  English [17]/[18] (community-lot lines), [1]/[4]/[5]/[29]/[36]/[37] (price /
  move-in / magicoins branches) — all already flowing through
  `GameFacade.Strings` in the panel's ctor branches.

## Port

1. **New `UIOriginalParagraph`** (UIOriginalText.cs, additive): a UIContainer of
   `UIOriginalText` lines rebuilt lazily when Text/MaxWidth change — captions
   mutate after ctor (family/community/price branches), so lazy re-wrap is the
   norm. Space-wrap with hard-break for over-long words; honours embedded
   `\r\n`; `RightAlign`/`BottomAnchor` for the bottom-right secondary field.
   Line height is the caller's disclosed choice.
2. **`UIHouseSelectPanel.SyncOriginalTwins()`** (Update-driven): street title +
   description + secondary in the caption table **_10** (line height 13px), the
   lot NAME in the title table **_14** (matching the neighborhood-title role).
   Twins sync from the modern labels each Update (caption mutations picked up
   live) and mirror the MoreTween visibility. Modern labels stay as the
   pre-IFF-mount fallback, hidden once twins mount. The panel's UIBigButtons
   already render original captions via R112's base-class conversion.
   Font-role mapping and placement are DISCLOSED interpretations (original
   dialog geometry is engine-drawn).
- Gate-readable: `UIHouseSelectPanel.LotQueryTwinsMounted`,
  `UIOriginalText.StreetTwin/LotTwin/DescTwin/SecondaryTwin` (public, sync
  asserts read them), `UIOriginalParagraph.ParagraphsMounted`.

## Gate

- New check **`uilotq`** (61 → 62): disk-pins chunks 132/134 (labels/counts/
  sha256 + the verbatim [15] and [0] template), then constructs the panel for
  house 1 through the game's own ctor path (street-name lookup + family/price
  branches all run), ticks one Update by hand (twins mount in Update), and
  requires all four twins mounted AND text-synced to their modern labels, plus
  at least one paragraph wrap exercised.
