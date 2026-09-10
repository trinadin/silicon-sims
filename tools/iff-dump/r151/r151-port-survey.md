# R151 SURVEY — the seven expansion-era Interest topics: port storage, panel, gate, strings

Round target: make Exercise/Food/Parties/Style/Hollywood/Technology/Romance stop
rendering empty. Everything below is READ-ONLY survey (no code changed); paths
relative to the repo root unless the FreeSO submodule is named. Style follows
`tools/iff-dump/r150/init-traits-interest-law.md` sections 2-3.

## 1. PANEL — `UIOriginalInterestGiftSubpanels.cs` post-R150

File: `Client/Simitone/Simitone.Client/UI/Panels/LiveSubpanels/UIOriginalInterestGiftSubpanels.cs`
(all cites this file unless noted).

- **Cell inventory**: `Cells` is declared 19 wide (`UIOriginalInterestCell[19]`,
  :41) but only **`TopicsShown = 15` cells are ever constructed** (:44, loop
  :95-106). `Cells[15..18]` stay **null** — Style/Hollywood/Technology/Romance
  are not "rendering 0", they DO NOT EXIST as widgets in the port panel. Paging:
  3x3 column-major, 9/page (:99-102, `SetPage` :125-139, `maxPage = (15-1)/9 = 1`)
  — page 2 shows cells 9-14, i.e. the three dead cells live on page 2.
- **TopicWord map** (:57-74, the R150 single-word map): topics 0-7 → words
  46-53 (Travel, Money[Violence icon], Politics, The 60's, Weather, Sports,
  Music, Outdoors); topics 8-11 → 46-49 (Toys/Aliens/Pets/School DELIBERATELY
  share 0-3's words — engine widget law, r145 interest-gift-law.md §2.5);
  topics **12/13/14 = -1** ("Exercise (expansion-era field, no port word)", Food,
  Parties).
- **Display scale law** (:76-80): `FillWidthForValue(raw) =
  max(0, min(10, raw/100)) * 4` px — the pinned cWinInterest::TSPaint law
  (0..1000 raw → 0..10 units → 0..40 px). `Update` (:141-160) reads the FULL
  word and x100s it into that scale (:157, the disclosed slot→runtime bridge);
  for `TopicWord[i] < 0` it pins `Cells[i].RawValue = 0` (:150).
- **Which cells render 0 today**: exactly **12 Exercise, 13 Food, 14 Parties**
  (wordless → RawValue 0 → `FillWidthForValue(0)` = 0 px fill). The prompt's
  "seven render 0" needs re-scoping: the other four (Style 15, Hollywood 16,
  Technology 17, Romance 18 in STR# order) have no cell at all.
- **Captions are BITMAPS, not strings**: each cell draws a 75x25 pictorial
  plaque from `cpanel\Buttons\Interest*.bmp` (:183-197 — the members array
  lists exactly 15: Travel..Parties) + `InterestBarsBackground.bmp`/`InterestBars.bmp`
  (:201-202). No text caption, no tooltip on cells. There is NO 'expansion'
  hide/disable handling — cells 12-14 are full first-class cells whose bar
  simply never fills.

## 2. PERSON DATA STORAGE — words 56..100 ARE addressable; 54/55 are free AND already written

- **The array**: `private short[] PersonData = new short[101];` —
  `FreeSO/TSOClient/tso.simantics/Entities/VMAvatar.cs:66`. **The VM can
  address words 0..100.** Bounds check: both accessors throw on
  `(ushort)variable > 100` — `GetPersonData` :783-785, `SetPersonData` :874-876
  (`throw new Exception("Person Data out of bounds!")`).
- **The write primitive**: the 'init traits' tree writes through op 2
  VMExpression → `VMMemory.SetBigVariable`
  (`FreeSO/TSOClient/tso.simantics/Primitives/VMExpression.cs:65-68`) → scope
  `MyPersonData` (18) / `MyPersonDataByTemp` (30) / StackObject variants
  (19/31) (`FreeSO/TSOClient/tso.simantics/Engine/VMMemory.cs:531-574`) →
  `VMAvatar.SetPersonData`. Reads symmetric via `GetBigVariable`
  (VMMemory.cs:79-83, :126-130).
- **Engine-side constants**: `VMPersonDataVariable`
  (`FreeSO/TSOClient/tso.simantics/Model/VMPersonDataVariable.cs`) names ONLY
  46-53 as interests (:52-59 `Interest_TravelToys`..`Interest_Outdoors`);
  **54/55 are named `UnusedAndDoNotUse2`/`UnusedAndDoNotUse3`** (:60-61) —
  no expansion-interest constant exists anywhere in the engine. Words 56+ are
  BUSY with TS1-meaningful fields: JobType 56, JobPromotionLevel 57,
  PersonsAge 58 (:62-64), SkinColor 60, TS1FamilyNumber 61, JobPerformance 63,
  Gender 65 (:70-72), and the TS1 alias block :112-124 (TS1FameScore 80,
  TS1FameStarPower 81, TS1MagicTransgressions 83, TS1Zodiac 70 …) — several of
  which ALIAS TSO fields (81 = SkillLockBase :90), so any new storage must not
  trample 56+.
- **DECISIVE for R151 (decoded, from the engine's own jump table)**:
  r145 interest-gift-law.md §2.5 (:217-228) — widget read offsets: base 0-7 =
  **+0x5e8..+0x5f6** (2-byte stride), 8-11 share 0-3's cases, and
  **17 Technology = +0x5f8, 18 Romance = +0x5fa** — the NEXT two halfwords
  after the base run — while 12 Exercise +0x5a6, 13 Food +0x5a8, 14 Parties
  +0x5ac, 15 Style +0x5b4, 16 Hollywood +0x5c0 sit SCATTERED BELOW the run.
  Continuing the (word-46)*2+0x5e8 map: **+0x5f8 = person word 54,
  +0x5fa = person word 55** — i.e. Technology/Romance are the very two words
  the init-traits tree writes (46..55) and the port already stores and gates.
  HIGH-confidence inference (offset continuity + the tree's 10-write bucket
  law); Exercise/Food/Parties/Style/Hollywood have NO person-word equivalent
  anywhere in the 0..100 space (they live in a raw struct block the IFF
  variable space never exposed) — those five need new disclosed storage.
- **Save path serializes person data WHOLE**:
  `FreeSO/TSOClient/tso.simantics/Marshals/VMAvatarMarshal.cs:21`
  (`short[101]` default), `SerializeInto` :112-113 writes
  `PersonData.Length` + the entire array, `Deserialize` :63-65 reads `pdats`
  and grows: `PersonData = new short[Math.Max(101,pdats)]`. `VMAvatar.Load`
  adopts it wholesale (`VMAvatar.cs:1268`). Trigger:
  `Client/Simitone/Simitone.Client/UI/Screens/TS1GameScreen.cs` `Save()`
  wraps `vm.Save()` (`VM.cs:699`) into an **FSOV** chunk (:1023-1035) saved
  with the lot IFF (:1056); also `cas.fsov` local house (:955-962) and the
  downtown snapshot (:442). Restore: `VMBlueprintRestoreCmd.cs:43-50` reads
  the FSOV → `VMMarshal.Deserialize` → `vm.Load`. **Words ≥56 persist
  automatically** — no save-side work for R151.
- (TSO-only aside: `VMNetAvatarPersistState.cs:33` keeps a separate
  `short[27]` selection for the TSO server persist path — irrelevant to the
  TS1 house save.)

## 3. TOWNIE GENERATOR + VMInterestTraits (FreeSO ee612e89)

- **`SimitoneNeighbourGenerator.MakePersonData`**
  (`FreeSO/TSOClient/tso.simantics/Utils/SimitoneNeighbourGenerator.cs:252-347`):
  builds `short[88]` (:254); the interest loop `for (i=46; i<56; i++)`
  (:314-339) rolls 0..10 with the bucket law and writes the RAW roll (R150
  correction :333-337). So townies carry 46..55 = ten raw rolls — **words
  54/55 are initialized here too**, exactly like a tree-written human.
- **`VMInterestTraits`** (`FreeSO/TSOClient/tso.simantics/Utils/VMInterestTraits.cs`,
  new in ee612e89): `InterestWords = {46..55}` (:32), `ApplyInitTraits`
  (:34-48) mirrors the BHAV 4100 bucket law (need {high3, med3, low3, zero1}),
  `IsZeroed` (:51-56) probes all ten. Called from `VMTS1Activator.cs:310-317`
  at lot load for any avatar whose block is all-zero. NOTE: `IsZeroed`
  returning true is the ONLY safety-net trigger — once R151 gives 54/55 a
  display, a zeroed-but-genuinely-zero Technology/Romance pair is
  indistinguishable from never-initialized (same standing model as R150).
- **Nothing anywhere initializes interest words ≥56** — words 56+ are only
  written with job/age/skin/family/gender values (generator :276-278, :341-344;
  the IFF 'init person' canon comments :264-278). The only ≥56 risk in R151 is
  picking storage for Exercise/Food/Parties/Style/Hollywood; every free
 -looking slot in 0..100 must be checked against VMPersonDataVariable + the
  TS1 alias block first.

## 4. GATE — 'uiintvals' (and 'uiinterest') in AutotestRunner.cs

- Registration: `Config.Checks` (:52) contains `...,uiinterest,uiintvals,...`
  (note: `uiinterest` appears twice in the list — pre-existing wobble).
  Dispatch: `if (CheckEnabled("uiinterest")) CheckInterestGift();` and
  `if (CheckEnabled("uiintvals")) CheckInterestValues();` (:751-754).
- **`CheckInterestValues`** (:6639-6693), comment block :6620-6638. Reaches
  the panel WITHOUT instantiating it: pins the STATIC
  `UIOriginalInterestSubpanel.TopicWord` (:6645-6652 — `map.Length == 15`,
  0-7→46-53, 8-11→46-49, 12-14→-1), then reads live avatars from
  `screen.vm.Entities` (:6655-6657) and pins per avatar:
  words 46..55 each in {0}∪[1..10]; bucket shape exactly 1×0 / 3×[1..3] /
  3×[4..6] / 3×[7..10] (:6661-6677); at least one avatar nonzero (:6674,
  :6679). Skips (Pass) when no in-lot screen (:6644).
- **`CheckInterestGift`** (:7236-7442) is the canon/art/live envelope: STR#
  140/141/142 label+count+sha pins (:7292-7295), the runtime OriginalLiveStrings
  pins incl. **Entry(140,19)=="Romance"** (:7320-7326), 24 art pins
  `R145InterestArt` (:7251-7277 — includes
  `intereststyle.bmp`/`interestcharisma.bmp`/`interesthealth.bmp`/
  `interestromance.bmp` 75x25/26, byte+len+dims+sha, :7268-7271), and the
  LIVE panel build (fill law :7382-7386; constructs a real
  `UIOriginalInterestSubpanel` :7388; grid/paging :7392-7405 — **hard-pins 15
  cells and page-2 = 6 visible**, which R151 must extend when TopicsShown
  grows).
- **How a new pin extends** (AUTOTEST.md "Adding or extending a check",
  `AUTOTEST.md` repo root): add `CheckXxx()` + dispatch in the check loop +
  register the name in `Config.Checks`; save evidence to tools/iff-dump/;
  "**Never weaken an existing pin — extend it and keep the suite green**."
  For R151: the uiintvals map pin (:6647-6651) will need to EXTEND from
  `map.Length == 15`/three -1s to the new 19-entry map (old 15 entries keep
  their values; only the -1 tail changes), the live grid pin (:7392-7404)
  extends to `TopicsShown` 19 / page-2 = 9+1 (19 = 9+9+1 → maxPage 2), and
  words 54/55 are ALREADY gated by the existing 10-word bucket loop — no new
  value pin needed if Technology/Romance map to them.

## 5. STRINGS — captions

- The panel's only TEXT is the title: `OriginalLiveStrings.Entry(140, 0)`
  (:88). Loader: `Client/Simitone/Simitone.Client/UI/Model/OriginalLiveStrings.cs`
  — raw walk of **`GameData/Live.iff`** from 0x40 (:60-82), Wanted table
  includes **STR# 140 'Interest Strings'**, 141 'Gifts', 142 'Interest
  Descriptions' (:42-49), format -3 English (lang 1) parse (:92-107).
- **All seven expansion captions ALREADY EXIST in the port's staged data** and
  are loaded at runtime. Verified by re-parsing STR# 140 from
  `game-data/The Sims/GameData/Live.iff` with the loader's own documented
  format (357 lang-entries; English set = entries [0..20]):
  `[0] Interests, [1] Travel, [2] Money, [3] Politics, [4] The 60's,
  [5] Weather, [6] Sports, [7] Music, [8] Outdoors, [9] Toys, [10] Aliens,
  [11] Pets, [12] School, [13] Exercise, [14] Food, [15] Parties,
  [16] Style, [17] Hollywood, [18] Technology, [19] Romance, [20] ''`.
  uiinterest already pins [1]/[2]/[19] at runtime (:7323-7325) and the chunk
  sha (:7293). STR# 142 carries the matching 20 per-topic descriptions.
- The CELLS don't show these strings (pictorial plaques, §1). Art for cells
  15-18 exists in `UIGraphics.far` and is already pinned byte-verbatim
  (`intereststyle/charisma/health/romance.bmp` — the engine's internal art
  names for the Style/Hollywood/Technology/Romance plaques; the 15-member
  `LoadArt` array :187-197 just stops at Parties).
- Other candidates NOT needed: `game-data/The Sims/ExpansionShared/` holds
  only `ExpansionShared.far` + SkinsBuy/Sound (no UIText tree); the base
  `GameData/Live.iff` carries the full 19-topic corpus (the original shipped
  the captions in the base game's Live.iff even though widgets 12-18 were
  expansion-fed).

## 6. ENGINE-SIDE Superstar/fame prior art

- `VMPersonDataVariable` TS1 alias block: `TS1FameScore = 80`,
  `TS1FameStarPower = 81`, `TS1FameStarHighWatermark = 82`,
  `TS1MagicTransgressions = 83` (Makin' Magic), `TS1LivingGhost = 84`,
  `TS1AtWorkSchool = 87` (VMPersonDataVariable.cs:112-124). **No reader/writer
  anywhere** — grep for TS1FameScore/TS1FameStarPower outside the enum: zero
  hits. The fame fields are NAMED but DEAD.
- `VMGenericTS1Call` (`FreeSO/TSOClient/tso.simantics/Primitives/VMGenericTS1Call.cs:298-303`):
  modes **34 PromoteFameIfNeeded / 36 DemoteFameIfNeeded are comment-only
  stubs** (fall through, GOTO_FALSE); mode 35 TakeTaxiHook also stubbed with
  "seems to have been added for studiotown". Declared in
  `VMGenericTS1CallMode.cs:39-41`. No Superstar interest logic exists — R151
  has NO engine precedent to copy for the scattered five; the FameScore enum
  names are precedent only for "name the words, store them, disclose".

## 7. R151 synthesis (survey conclusion, no code)

1. **Free wins first**: Technology + Romance = person words 54/55 (r145 §2.5
   offset continuity +0x5f8/+0x5fa; the tree, the generator, VMInterestTraits,
   the gate bucket law, and the whole-array FSOV save ALREADY cover them).
   Panel change: TopicWord[17]=54, [18]=55, TopicsShown 15→19, extend LoadArt
   members (style/charisma/health/romance plaques — already FAR-pinned),
   extend uiintvals map pin + uiinterest grid/paging pin (never weaken:
   15-entry values stay, tail extends).
2. **The scattered five** (Exercise +0x5a6, Food +0x5a8, Parties +0x5ac,
   Style +0x5b4, Hollywood +0x5c0) have no person-word equivalent; they need
   NEW port storage — safest is spare person words verified free against
   VMPersonDataVariable (0..100) AND the TS1 alias block — plus a disclosed
   mapping note; OR keep them wordless with a pinned 'dead cell' state.
   Words 56+ are the WRONG default (jobs 56/57/63, age 58, gender 65, fame
   80-82 alias SkillLock 81/82).
3. Whatever backs cells 12-14 should still surface the tree-written words
   54/55 model honestly: init traits rolls TEN topics, the base panel showed
   EIGHT — the original's own Tech/Romance bars read the last two rolls.
