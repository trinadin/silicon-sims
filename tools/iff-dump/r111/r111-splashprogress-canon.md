# R111 — UIText.iff STR# `splashprogress`: the ORIGINAL splash/title-screen status strings

**Slice**: the loading screen's rotating status text. The port showed modern
UILabel strings (`LoadText`, "Reticulating Splines..." etc. — Simitone-authored,
NOT original data). The original's rotating boot/title-screen strings live
IFF-literally in `GameData/UIText.iff`, STR# chunk **label `splashprogress`**.
This round mounts the original corpus byte-verbatim and renders it with an
original .ffn glyph table on the R88 original boot screen.

## Source (raw canon)

- File: `game-data/The Sims/GameData/UIText.iff` (2,300,704 bytes)
- IFF container: 60-byte Maxis header (`IFF FILE 2.5:TYPE FOLLOWED BY SIZE...`),
  u32 BE offset-to-rsmp (0x40), then flat chunks:
  `4CC | u32 BE chunkSize | u16 BE chunkID | u16 BE flags | 64-byte null-padded
  label | (chunkSize-76) data` (exact walk per FSO `tso.files` IffFile.AddChunk,
  verified byte-for-byte against the file).
- 74 STR# tables total.
- **`splashprogress`: chunkID=155, flags=0x10, file span 0x1e267c..0x1ec9f3**
  - whole chunk (4CC..end) = **41,847 bytes**,
    sha256 `39fdf8af203d6639ca2f423dcc0c9a60318b7aa18591bc0bfb97fa3d9c2fcc2a`
  - data (post-76-byte header) = **41,771 bytes**,
    sha256 `bf1daae4d8b9a392a1f1db975d184b67c4bb292e4d8f20e546954e0670001746`
- STR data format: LITTLE-endian i16 formatCode = **−3 (0xFFFD)** = language
  string-pairs: u16 count (340), then per entry `{u8 langCode, cstring value,
  cstring comment}`.
- 340 entries = **20 languages × 17**. The English set (lang=1) is the canon
  scroll; every entry carries a translator comment classifying it.

## The English set (byte-verbatim, mac_roman)

| idx | value | comment class |
|---|---|---|
| 0 | `Starting App init` | `!Title Screen: Scrolling Text` |
| 1 | `Registering resources` | `!Title Screen: Scrolling Text` |
| 2 | `Setting up controls` | `!Title Screen: Scrolling Text` |
| 3 | `Initializing graphics` | `!Title Screen: Scrolling Text` |
| 4 | `Locating Misplaced Calculations` | `##SPELLBOUND - Translated` |
| 5 | `Eliminating Would-be Chicanery` | `##SPELLBOUND - Translated` |
| 6 | `Tabulating Spell Effectors` | `##SPELLBOUND - Translated` |
| 7 | `Reticulating Unreticulated Splines` | `##SPELLBOUND - Translated` |
| 8 | `Recycling Hex Decimals` | `###SPELLBOUND Misc2 - Needs Translation` |
| 9 | `Binding Trace Enchantments` | `##SPELLBOUND - Translated` |
| 10 | `Fabricating Imaginary Infrastructure` | `##SPELLBOUND - Translated` |
| 11 | `Optimizing Baking Temperature` | `##SPELLBOUND - Translated` |
| 12 | `Setting up personfinder` | `!Title Screen: Scrolling Text` |
| 13 | `Finished app init` | `!Title Screen: Scrolling Text` |
| 14 | `Ensuring Transplanar Synergy` | `##SPELLBOUND - Translated` |
| 15 | `Loading Characters` | `!Title Screen: Scrolling Text` |
| 16 | `Simulating Program Execution` | `##SPELLBOUND - Translated` |

Notes:
- The original strings carry **no trailing dots** (the port's modern LoadText
  invented "..." suffixes).
- The comment taxonomy is itself canon: `!Title Screen: Scrolling Text` marks
  the seven boot-stage anchors (0,1,2,3,12,13,15); the `##SPELLBOUND` marks are
  Makin' Magic-era humor additions interleaved into the same scroll.
- No dedicated loading-tip table exists elsewhere: the only loading-adjacent
  STR# labels in UIText.iff are `splashprogress` (155) and
  `Celebrity Strings for Loading Dialog` (271, the Superstar narrative dialog —
  a different, expansion loading screen; out of this slice's scope).
- Full 340-entry decode (all languages + comments): `r111-splashprogress-dump.txt`.
- Parser: `parse_uitsplash.py` (this dir).

## Port mapping (DISCLOSED interpretations — positions/fonts are ours, data is canon)

1. **Strings**: mounted through the existing `GameFacade.Strings` table "155"
   (built by `ContentStrings.LoadTS1()` from the same UIText.iff at
   SimitoneGame.Initialize, before the loading screen exists). The gate
   additionally re-reads the chunk from disk and pins sha256 + all 17 English
   values verbatim, and requires the mounted tips to equal the disk corpus —
   locale drift would FAIL loudly, not render silently wrong.
2. **Font**: `variablesans_10.ffn` (the caption table) — an original .ffn glyph
   table, chosen for the status-caption role. The original engine's exact
   title-screen font selection is NOT recovered (engine code, not IFF data);
   this is a disclosed interpretation using 100% original glyphs.
3. **Placement**: centered above the R88 engine-drawn original load bar
   (bar top at ScreenHeight−60; text baseline ScreenHeight−96). Exact original
   placement is engine-drawn and not recovered — disclosed.
4. **Rotation**: sequential walk over all 17 strings, advancing every ~1.5s of
   frames AND on every loading-progress change (mirrors the original's mix of
   stage anchors + continuous scroll). Starts at index 0 (`Starting App init`).
5. Modern `LoadText`/UILabel remains as the pre-IFF-mount fallback (the ~1.5s
   window before TS1Global resolves, same as R88 boot-art fallback) and is
   hidden once the original tips mount.

## Gate

- New autotest check **`uisplash`** (gate 59 → 60): disk-reads UIText.iff,
  walks chunks, pins chunkID/entry-count/sha256/17 verbatim values, and asserts
  the live loading screen mounted the same corpus with the caption font.
