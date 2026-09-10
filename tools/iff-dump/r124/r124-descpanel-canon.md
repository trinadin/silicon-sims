# R124 — the ORIGINAL catalog item description panel (uidesc)

The catalog surface R122 opened is completed: clicking a catalog item now pops
the ORIGINAL description panel (kCatalogPopupBack plaque + kCatalogPopupBackTiles
fill + STR# 160 'CatalogRatings' verbatim + original .ffn glyphs), replacing the
FSO-authored modern UIQueryPanel chrome wholesale. Gate: **71/71** (r124p2).

## Surveys (Hermes x2, concurrent)

- **Canon survey**: STR# 159 'ObjectTTs' decoded (9 English entries: live-mode
  object hover tooltips — "There are no actions available" etc., chunk sha
  4c02d15de516…, archived in r124-descpanel-strs.txt) — it is NOT catalog; the
  prior round's guess that it paired with 160 was wrong and is corrected here.
  STR# 160 'CatalogRatings' confirmed (400 raw = 20 English x 20 langs,
  full-chunk sha 3a8571a9b02d…). Art: kCatalogPopupBack -> cpanel\Backgrounds\
  PopupInfo.BMP (559x127 RLE8) and kCatalogPopupBackTiles -> PopupInfoTiles.bmp
  (36x36, byte-identical file to Nbhd\PersonInfoBkg.bmp). NEGATIVE SPACE: zero
  rating/price/name art exists anywhere in the 794-member UIGraphics.far —
  ratings are TEXT (the table's own '%d' formats are the proof).
- **Port survey**: UIQueryPanel (the only consumer of the catalog click flow)
  showed name/desc/ratings but in fully modern composition, MANGLED the canon
  strings (stripped ': %d', re-appended '{0}'), and the six restriction flags
  were decoded nowhere in the codebase.

## Canon (make_r124_descpanel_canon.py — byte-verbatim, regenerable)

- **STR# 160**: all 20 English entries pinned in the gate by script
  (regen_gate_shas.py; never hand-typed). [0..6] 'Hunger: %d'..'Room: %d',
  [7..13] '+ Cooking'..'+ Study', [14..19] usage flags (adults/kids/Group
  Activity/pets/dogs/cats).
- **Art pins**: PopupInfo.BMP len 4210 559x127 sha b21cd477…; PopupInfoTiles.bmp
  len 1968 36x36 sha ee9548b9…. Composition DERIVED FROM THE ART (RLE8 pixel
  decode, r124-art-canon.txt): the plaque is one complete rounded bordered panel
  with a uniform interior (1px side borders, 551-run clean bottom rows); the
  tile carries its OWN left/right side borders — it is the vertical BODY FILL
  the description grows through. ASCII structure maps on file.

## The OBJD derivation (r124-objdflags-scan.txt — 1203 IFFs, 5125 OBJDs, 1571 masters)

- **Ratings**: fields 80..86 are SIGNED values in exactly the STR# 160 [0..6]
  caption order (Hunger..Room). Coffee's Bladder = 0xFFF8 = -8 is the signed
  proof. The port reads them as `short` and formats the DISK string verbatim
  by substituting %d.
- **Skills**: field 87 bits 0..6 = STR# 160 [7..13], data-validated by corpus:
  chess = 0x0004 (Logic), guitar/easel = 0x0010 (Creativity), mirrors = 0x0020
  (Charisma), computers = 0x0040 (Study).
- **Price**: OBJD field 16 only. CTSS carries exactly 2 English entries
  (name + description; the 36 raw entries of toilets.iff = 2 x 18 languages) —
  no price string, no rating tags; CTSS comments are Maxis authoring notes
  ('Buy Mode: Toilet: Cheap: Name'); per-object STR# tables are
  behavior/animation strings.
- **NEGATIVE SPACE (the round's headline finding)**: STR# 160 [14..19] have NO
  encoding in the corpus. MiscFlags (89) is all-zero corpus-wide except 2 FX
  objects (Fire.iff, NPC-FX.iff). The exhaustive sweep (every field 0..103 x
  16 bits vs an unambiguous pets-only set: pet beds/bowls/posts/litter/cages)
  finds only TWO pet-clustered bits — field 92 bit 5 = the PETS SUBSORT
  (catalog PLACEMENT: the doghouse carries it without being pets-only) and
  field 98 = pet DREAM flags (doghouse 0, chew toys 0) — neither is a usage
  restriction and neither distinguishes dogs/cats/kids/adults/group. The port
  therefore renders [0..13] ONLY and invents nothing. All 20 entries stay
  pinned so any future canon source (e.g. original binary disassembly) can be
  diffed against the table.
- **SCAN FIX (lesson recorded)**: ExpansionPack5.FAR (Unleashed — ALL pet
  objects) has an UPPERCASE extension; the first glob ('*.far', case-sensitive
  APFS) silently missed it and the first scan's "no pet data" was an artifact.
  Corpus scans now match archives case-insensitively (fixed in the tool; the
  571-master delta is all Unleashed).

## Port

- **UIQueryPanel.cs** (full rewrite, same external API: SetInfo x2 / Active /
  SetShown / Mode): PopupInfo plaque at top; PopupInfoTiles tiled 36x36 across
  the 559px width and downward to the wrapped description's depth; the object's
  own sprite (UI3DThumb / GetObjectThumb — the port's existing machinery)
  fitted into the plaque's left side; name = CTSS[0] in variablesans_08,
  price = '\u00A7'+OBJD price in variablesans_11 ('\u00A7' is IN the original
  font tables — probed), ratings block right-aligned in variablesans_11 with
  each line the DISK STR# 160 string formatted verbatim, description word-
  wrapped in variablesans_09 via UIOriginalParagraph over the tile fill.
  InternalBefore=true puts chrome under text (UICachedContainer contract).
- **UIOriginalText.cs**: +TintLines(Color) so the paragraph's wrapped lines
  fade with the panel opacity tween (Children is protected).
- **UIBuyBrowsePanel.cs**: SelectItemInCurrentCategory made public (the REAL
  eyedropper-path transition that collapses the subsort row; the gate drives
  it before selecting items).

## Gate ('uidesc', 70 -> 71)

(1) STR# 160 pins label/400-raw/full-chunk sha/all 20 entries (script pins);
(2) both art members pin name+len+dims+sha and mount at 559x127 / 36x36;
(3) LIVE through the REAL UIBuyBrowsePanel.Selected flow: a Seating item with
a Comfort rating yields name == the catalog's own DisplayName (CTSS[0]), price
== '\u00A7'+catalog price, a line == e160[1] with %d substituted verbatim, a
non-empty description, FullHeight > 127 (grows into the tiles), and NONE of
[14..19] appears; a second REAL selection of an object with RatingSkillFlags
set yields its '+ Skill' pin line.

## Runs

- r124p1: HONEST FAIL — canon=True art=True live=False: the gate read
  mp2.SubPanel.FilterCategory while the panel was still on its subsort row
  (ctor ends ChoosingSub=true, FilterCategory null) -> liveFails no-panel +
  skill ArgumentNullException. Fixed by driving the real transition.
- r124p2: **AUTOTEST RESULT PASS passed=71 failed=0** (uidesc canon=True
  (160/20) art=True (2 pins) live=True strLoads=2), clean exit.

## Residuals (disclosed)

- Panel placement, preview box geometry, text positions inside the plaque and
  the tile tiling geometry are ENGINE CHOICES (STR# 160 carries no (x;y)
  directives — unlike STR# 145; disclosed, as in R121/R122).
- STR# 160 [14..19] await a canon source; not rendered (see negative space).
- STR# 159 'ObjectTTs' decoded + archived — candidate for a future LIVE-mode
  object hover-tooltip round.
- BuyBack.bmp / OptionsBack.bmp / DisposeBack.bmp / IconTemp.bmp still have no
  members on this disk (standing gap class).
