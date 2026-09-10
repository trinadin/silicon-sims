# R114 — Live-mode subpanel names: original strings + original glyphs (and a provenance fix)

**Slice**: the live-mode person subpanels' name labels — 8 motive names and 5
personality-trait names, on screen continuously during live play.

## The provenance defect this round fixes

- The port read motive names from `GameFacade.Strings` table **"f102"** — which
  resolves to FSO's own `Content/UI/uitext/english.dir/_f102_motivestrings.cst`,
  a file whose header comment says *"for some reason these are not included in
  the game strings."* They ARE in the game: **Live.iff STR# 130 `Motives`**.
- The personality trait names were **hardcoded English arrays** in
  `UIPersonalitySubpanel`. The original corpus is **Live.iff STR# 131
  `Personality`**.
- Bonus catch (disclosed): `UIMotiveSubpanel` laid its bars on a `(i/2)` row
  grid while their labels use `(i/4)` — bars would not sit under their names.
  The twin mount made the inconsistency visible; both now use the label grid
  (4×2).

## Canon (raw — GameData/Live.iff, same 76-byte-header chunk walk as r111–r113)

- **STR# `Motives`, chunkID 130**: file 0xcca99, chunk size 53,766, format −3,
  289 entries, sha256 `72f1dcd92fa90ce56cc114ae143e57b200699a67e0b16c54b962908e2fbc3c5b`.
  Layout: `[0] 'Needs'` (subpanel title), then name+popup-description pairs:
  `[1] Hunger  [3] Energy  [5] Comfort  [7] Fun  [9] Hygiene  [11] Social
  [13] Bladder  [15] Room` — the ORIGINAL needs-subpanel order (the port's f102
  order differs: Hunger/Comfort/Hygiene/Bladder/Energy/Fun/Social/Room).
- **STR# `Personality`, chunkID 131**: file 0x2381c, chunk size 130,948,
  format −3, 714 entries, sha256 `4e960920e8326379ed7e1b752a43497695940fc5a6ce6d5334c073b8386faa6b`.
  Layout: `[0] 'Personality'` (title), then per-trait blocks of 7 — trait NAME,
  3 tier names (e.g. Neat: Slob / Occasional Cleaner / Neat-Freak), 3 tier
  descriptions. Trait names at `[1] Neat  [8] Outgoing  [15] Active
  [22] Playful  [29] Nice`.

## Port

1. **New `OriginalLiveStrings`** (Simitone.Client/UI/Model): walks the Live.iff
   IFF container itself (the same 76-byte-header rule as the gate and the
   r111–r113 parsers), matches chunks by id AND label ('Motives'/'Personality' —
   duplicate-id guard), parses format −3 and keeps the English set (lang 1);
   load failure leaves callers' existing fallback strings. (The first
   implementation delegated to the FSO `IffFile` parser — it throws on
   unrelated chunk types in Live.iff and the twins never mounted through that
   path; r114p1.)
2. **`UIMotiveSubpanel`**: 8 `UIOriginalText` twins in the caption table **_10**
   at the label positions; text = `OriginalLiveStrings.Motive` mapped per bar
   (the port KEEPS its bar order and maps each bar's motive to the original
   name: index map 1,5,9,13,3,7,11,15 — Hunger,Comfort,Hygiene,Bladder,
   Energy,Fun,Social,Room bars carry the original names for those motives).
   Modern labels stay as the pre-IFF fallback, hidden once twins mount.
3. **`UIPersonalitySubpanel`**: labels now read
   `OriginalLiveStrings.Trait(1,8,15,22,29)` (hardcoded array demoted to
   load-failure fallback) + _10 twins, same mount pattern.
- Gate-readable: `MotiveNamesTwinned` / `TraitNamesTwinned` statics, public
  `NameTwins` arrays.

## Gate

- New check **`uilive`** (62 → 63): disk-pins Live.iff chunks 130/131 (labels/
  counts/sha256 + verbatim names at the mapped indices), then (a) reads the
  LIVE motive subpanel from the running tree (the same reach uichrome uses) and
  requires its 8 twins to equal the disk canon per bar mapping, and (b)
  constructs a personality subpanel + one Update tick and requires its 5 twins
  to equal the disk trait names.
