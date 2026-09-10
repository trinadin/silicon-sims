# R246: real-flow CAS audit (`ucasflow`) — family creation driven end-to-end and the personality-save crash repaired

For the first time, the port's Create-A-Sim is exercised the way a player runs
it: from the real neighborhood screen, through the real MoveIn entry, real
button and typing interaction on the original Pick-a-Family / Create-a-Family /
Create-a-Character panels, through the real unspent-points and save-confirm
dialogs, into the real persistence write — and the written files are then
re-parsed from disk and pinned against the original's own created-record canon.
The audit's headline finding was a true product bug: the first real family
save threw `IndexOutOfRangeException`, because the CAS UI's 5-slider
personality vector was assigned straight into the 6-slot generator field.
That crash is repaired with the original's own slot mapping, and `ucasflow`
now pins the mapping regression forever. The default suite is untouched
(`ucasflow` is opt-in; default count stays 142).

## The repaired bug (found independently by two pre-work audits, confirmed in source)

`TS1CASScreen.BuildMember` stored the UI vector (display order Neat /
Outgoing / Active / Playful / Nice, ×100) as `short[5]`;
`CASToNeighGen` assigned it to `SimTemplateCreateInfo.PersonalityPoints`
(`short[6]`); `MakePersonData` reads index 5 → throw on `SaveFamily`. The fix
maps at the CAS→generator boundary into the generator/IFF person-data order
`[Nice, Active, Generous, Playful, Outgoing, Neat]` with **Generous = 0** —
matching the canon established from the staged Maxis bytes: every original
created record stores pd[2..7] = Nice/Active/Generous/Playful/Outgoing/Neat on
a 0..1000 scale, and no original CAS record ever writes Generous (the original
UI has only five sliders). `VMTS1MakeNewCharacter` (the other caller) writes
all six slots uniformly and needed no change. The adversarial review
triple-verified the mapping direction against three independent sources
(UIText.iff label pins, the zodiac archetype table summation, mobile-panel
trait twins) and confirmed the ×100 scale is applied exactly once.

## The canon (established read-only from the owner's staged data)

* Created family = one `FAMI` + one `FAMs` chunk in `UserData/Neighborhood.iff`
  plus one NBRS record per member plus one `Characters/User#####.iff`
  (original-style IFF: OBJD 128 `user##### - Name`, CTSS 2000 [name, bio],
  STR# 200 bodystring table). Person data (personality/skills/interests/
  gender/age/skin/family id) lives only in NBRS. `Tutorial.FAM` is the
  tutorial's Newbie move-in snapshot — format reference, not a typical
  created family.
* Zodiac is **never persisted** in the original (pd[70]=0 in every record);
  the port's CAS derives it in UI only — both sides agree by absence.
* Original NBRS: chunk 0x3E/0x3F, record Version 4 (80 shorts), PersonMode 5,
  Name = character-file stem; skin pd[60] is lgt=1/med=3/drk=2. The port
  writes chunk 0x49, record 0xA (256 shorts, 88 real), PersonMode 9, Name
  "iffname", skin 0/1/2 — asserted as current behavior and documented as
  divergences, not silently accepted as canon.

## The harness (`AutotestCASFlow.cs`, opt-in)

* Dedicated autotest state entered from the boot neighborhood screen (never
  touches the TryNextHouse house pipeline); both `ucasflow` and `uicasflow`
  accepted as gate flags; the default `Checks` string is byte-identical and
  the default suite cannot go red from this check.
* Isolation: when the check is enabled, `FSOEnvironment.UserDir` is redirected
  to a fresh temp directory inside `AutotestRunner.Begin` — strictly before
  `Content.Init`/`InitSpecific`, so the provider clones pristine UserData from
  game-data into the temp dir and every CAS write lands there. The harness
  pins `game-data/The Sims/UserData/Neighborhood.iff` byte-identical (SHA-256,
  not just mtime) across the run.
* Real drives: reflection-event button presses that respect the production
  Disabled laws (a press on a disabled button degenerates to a no-op → phase
  timeout → red, never false green), real focus + `FrameTextInput` typing
  through the production forbidden-character filter and caps (family 24,
  person 25; a `:` in the typed stream is asserted dropped), the real
  unspent-points `UIMobileAlert` asserted first and the R239 seam used for
  determinism after, the real save-confirm dialog, the real `SaveFamily`, the
  `NeighborhoodTransition` seam for the move-in boundary (asserts the new
  family's ChunkID without loading a lot).
* Disk read-back with fresh `IffFile` parses: exactly one new FAMI/FAMs
  (HouseNumber 0, FamilyNumber old-max+1, Budget 20000, Unknown 24, unique
  GUIDs, lang-1 name == typed name); NBRS member record (lowest-free
  NeighbourID, GUID ∈ FamilyGUIDs, **pd[2..7] = 600/300/0/500/200/800 for the
  chosen slider vector — the regression pin for the repaired crash**; pd[58]
  age, pd[61] family id, pd[65] gender, pd[56]/pd[57]/pd[36]/pd[33]/pd[29]/
  pd[32] writer defaults, pd[70]=0); character file (OBJD/GUID/label, CTSS
  name+bio, STR# 200 CAS-controlled slots and outfit name-pairs), with the
  generator's extra slots [30]–[34] logged as documented residuals.
  Written `Neighborhood.iff` + `User00024.iff` bytes + SHA-256 are published
  as evidence.
* Six GPU captures (pick-a-family, family-edit typed, character preview,
  suit-memory switch, the real unspent-points alert, post-save card) plus the
  written files live under `ui-audit/r246` and `tools/iff-dump/r246-casflow/`.

## Review record

The adversarial review returned SHIP with no P0/P1: mapping direction and
scale correct; the pre-fix crash confirmed real; isolation timing airtight
(including the config.ini binding order); the game-data pin byte-level; the
default suite provably untouched; no false-green tautologies (a swapped
personality pair would fail the pd pins; zodiac derives through an
independent reimplementation). P2 notes: log spelling split uicasflow/ucasflow
(both accepted by design), temp dirs accumulate in `/tmp` (evidence lives
there by design), the pre-existing process-exit-code contract (log-level
verdicts) and the `simitone_debug.log` CWD leftover (`TS1CASScreen.SetFamilies`)
are unchanged pre-existing behavior documented for a future cleanup round.

## Validation

* Baseline before work: 142/0.
* Focused packaged run `-autotest-opts uicasflow`:
  **AUTOTEST RESULT PASS — ucasflow passed=91 failed=0** (`focused-final.log`),
  clean exit; publish and package carry the same `Simitone.Client.dll`
  SHA-256 `6ef937667a3a7e92f9d0d41e06ba3ae9e85da061edddb2af607f864297fcf555`.
* Full default suite: **AUTOTEST RESULT PASS passed=142 failed=0** at
  2026-09-06 (default-final.log), clean exit-probe chain — the opt-in wiring
  provably does not touch the default path.
* Isolation proof: pristine-clone + write verification inside the redirected
  temp dir; `gamedata-neighborhood-byte-identical` and `-mtime-untouched`
  PASS.

## Documented divergences and residuals (audit findings, not silently accepted)

* Skin encoding pd[60]: port 0/1/2 vs original 1/2/3 (med=3) — self-consistent
  in the port; appearance actually flows through the bodystrings, which match
  canon.
* NBRS `Name="iffname"` vs original file-stem convention; PersonMode 9 vs
  original 5; FAMI Version 9 + 16 trailing zeros vs original Version 7 —
  port-chosen, read-compatible by the port's own codecs, unproven against the
  original engine.
* `SaveNeighbourhood` drops the derived `rsmp` chunk (readers walk chunks
  sequentially; harmless) and `NBRS.Write` declares the decoded-entry count
  while writing the valid-record set (pre-existing lossiness, pinned by the
  harness's NeighbourByID baseline).
* STR# 200: the written character file carries 27 slots like its
  TemplatePerson source; slots [30]–[34] the generator once targeted are
  absent in both — logged per slot.
* Skills/interests are randomized at creation (original CAS values unknown);
  asserted presence/range only.
* Outside the redirect: `autotest.log` (also /tmp) and the pre-existing
  `simitone_debug.log` CWD leftover. game-data is never written.
