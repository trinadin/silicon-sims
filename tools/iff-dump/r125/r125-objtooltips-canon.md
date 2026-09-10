# R125 — the ORIGINAL live-mode object hover tooltips (uitt)

The surface R124 named as its successor: hovering an entity over the lot now
shows the ORIGINAL tooltip — the entity's NAME when interactions are available,
else the unavailability REASON from UIText.iff STR# 159 'ObjectTTs' — and a
zero-interaction click shows the same reason instead of the port's dead click.
Gate: **72/72** (r125p1, first full run, clean exit).

## Surveys (Hermes x2, concurrent)

- **Port survey**: the hover MACHINERY already existed — `UILotControl.
  LiveModeUpdate` tracks `ObjectHover` (hit-test via `World.
  GetObjectIDAtScreenPos`, the eyedropper's own picker) and `InteractionsAvailable`
  (via `GetPieMenu`), and the cursor already switches through the R87 original
  Live* cursors. But objects showed NO tooltip, avatars showed `ToString()` only,
  and Simitone's zero-interaction click was a dead click (FreeSO upstream showed
  string id 0 through `ShowErrorTooltip`, which ALREADY reads STR# 159 — a TSO
  inheritance that validated reason-only feedback). The tooltip channel
  (`state.UIState.Tooltip` + keepalive) and the R119 original-glyph renderer are
  in place.
- **Data survey**: TTAB interaction entries carry the FULL permission set in one
  u32 — `TS1NoChild` 0x10, `TS1NoAdult` 0x40, `TS1AllowCats` 0x200,
  `TS1AllowDogs` 0x400 — and `VMThread.CheckTS1Action` (the pie-menu filter)
  already rejects on exactly those bits. UIText.iff has NO pie-menu or
  object-name table (names live in CTSS; STR# 159 is the sole reason table).

## Canon (make_r125_objtts_canon.py — regenerable)

- **STR# 159** (r125-objtts-strs.txt): label 'ObjectTTs', 180 raw = 9 English x
  20 langs, full-chunk sha 4c02d15de516… — pinned in the gate by
  regen_gate_shas.py (never hand-typed).
- **TTAB classification scan** (r125-ttab-scan.txt): every catalog master on
  disk (1571, all FARs case-insensitive — the R124 EP5 lesson), each master's
  TreeTableID TTAB parsed by a python mirror of TTAB.Read (v8 plain LE + v9
  bit-packed, a mirror of FreeSO's IffFieldEncode, MSB-first) and classified
  the way the port does. Self-verified anchors:
  - 'Persian Plush Pet Bed' (petbed.iff) — entries 0x770/0x671, all
    AllowCats|AllowDogs → **pets-only** (12 masters)
  - 'Critter Condo' (doghouse.iff) — 0x570/0x450/0x470, AllowDogs only →
    **dogs-only** (7 masters, with the fire hydrant)
  - 'Scratcheriffic Scratching Post' (petpost.iff) — 0x271, AllowCats only →
    **cats-only** (5 masters)
  - **kids-only: ZERO masters corpus-wide** — the survey's 'ToyBox.iff 0x41'
    row did not reproduce (no such file, no such TTAB label; re-derived from
  scratch here). 'Can only be used by kids' has no TTAB anchor on this disk.
  - adults-only 128 (all base-game appliances etc.), none/no-TTAB 668, mixed 751.
  - NOTE pet FOOD BOWLS are 'mixed' (the human-fills entry is adults-only
    0x10, the pet-eats entry pets-only 0x771) — the classifier is over the
    WHOLE entry set, matching what a click would see.

## Port (Client/Simitone UILotControl.cs)

- `ObjectTooltipReason(vm, obj, viewer)` — public static, THE classifier:
  [3] for the active character itself; then over the entity's RAW TTAB
  (own + global tree tables — `GetPieMenu` DROPS flag-rejected entries, so the
  classifier walks the tables): pets-only → [6]/[4]/[5] by which species bits
  are present; pet viewer over people-only → [7], over the other species →
  [4]/[5]; child over all-NoChild → [1]; adult over all-NoAdult → [2];
  empty tables → [0]; entries exist but none user-runnable → [8].
  Every string is `GetString("159", i)` verbatim.
- `ObjectHoverName(obj)` = `obj.ToString()` (VMEntity.ToString already resolves
  MultitileGroup.Name → CTSS[0] → OBJD label — the original's own name source).
- Hover branch: leader-izes (as the pie path does — also fixed the availability
  probe at hover-change to leader-ize, matching the click path) and shows
  name-when-available / reason-when-not for BOTH objects and avatars (the
  avatar name path is unchanged in content — `ToString()` — now shared).
- Zero-interaction click: `ShowReasonTooltip` (the shared error channel/sound)
  with the classified reason — the port's dead click is gone.
- The TSO-era for-sale hover/click branches stay (TS1 lots never set them).

## Disclosed interpretations (STR# 159 gives strings, not branch conditions)

- [3] applies to hovering/clicking the selected sim itself.
- [0] 'no actions' = the tree tables carry no entries at all;
  [8] 'no user-directed actions' = entries exist but none runnable by user
  direction right now (guard failures). The split is the round's one
  interpretive choice; a time-boxed PPC scan for the original's STR# 159
  consumer (one probe: no STR#+0000 comparison sites found with the guessed
  pattern) did not settle it — the R90 scan infra remains the path for a
  future engine round.
- [2] 'kids' can never fire from TTAB data on this disk (zero kids-only sets);
  it stays wired in the classifier for any future corpus.

## Gate ('uitt', 71 -> 72)

(1) STR# 159 label/180/sha + all 9 entries (script pins); (2) LIVE through the
ENGINE's own TTAB decoder: created out-of-world entities of the three corpus
anchors classify to [6]/[4]/[5] verbatim, the active avatar hovering itself →
[3], and the name path returns the anchor's CTSS name; entities deleted after.

## Run

- r125p1: **AUTOTEST RESULT PASS passed=72 failed=0** (uitt canon=True (159/9)
  live=True strLoads=4), clean exit — no honest-FAIL chain this round; the
  survey-correction chain (ToyBox re-derivation) is the evidence discipline
  equivalent, on file in the scan.

## Residuals

- The [0]/[8] and [3] interpretations (above) pending an engine-binary round.
- The tooltip LOOK is the port's transient channel (white/black box via
  UILayer.DrawTooltip) rendered in original glyphs (R119); the original's
  hover-tooltip background/composition, if any, is not established.
- Pet selection exists in this port (family subset unfiltered); the [7]
  'people' branch is wired but untested live (no pet in the gate's family).
