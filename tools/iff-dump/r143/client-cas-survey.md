# R143 — client CAS survey (`client-cas-survey.md`)

Client-side inventory for a faithful port of the original Create-A-Sim screens
(Pick-A-Family → Create-A-Family → Create-A-Sim) onto the original Res_Nbhd
art + UIText strings. Companion artifact: `cas-art-states.txt` (programmatic
state-count analysis of every Nbhd sheet) and full string dumps
`uitext-128-PickFamilyStrs.txt` / `uitext-129-DesignFamilyStrs.txt` /
`uitext-130-DesignCharStrs.txt` (fresh this round, with the Maxis comments
that name each string's UI element).

All file:line citations are current as of this round.

---

## 1. Current CAS flow — who renders what

### 1.1 Entry / exit

- **Entry**: the neighborhood screen's left-side switcher builds a CAS button
  (`ngbh_cas.png`) that opens a `UITransDialog("cas")` transition and calls
  `GameController.EnterCAS()` — `Client/Simitone/Simitone.Client/UI/Panels/UINeighbourhoodSwitcher.cs:35-41`.
- `EnterCAS` (`Client/Simitone/Simitone.Client/GameController.cs:190-200`)
  news up a `TS1CASScreen`, cleans up a prior `TS1GameScreen` world, and
  `GameFacade.Screens.AddScreen(screen)`. There is no other caller.
- **Exit**: `TS1CASScreen.SetMode(UICASMode.ToNeighborhood)`
  (`UI/Screens/TS1CASScreen.cs:616-629`) marks the screen dead, runs a
  `UITransDialog("normal")`, then `CleanupLastWorld()` +
  `GameController.EnterGameMode("", false)` — or, when a family was selected
  for move-in, `EnterGameMode("!" + ('m' if NeighTypeFrom==7 else 'n') + familyId, false)`.

### 1.2 Modes — `UICASMode` (`TS1CASScreen.cs:1089-1095`)

```
ToNeighborhood = -1   (exit tween)
FamilySelect   = 0    (Pick-A-Family;  UIFamiliesCASPanel shown)
FamilyEdit     = 1    (Create-A-Family; UIFamilyCASPanel shown)
SimEdit        = 2    (Create-A-Sim;    UISimCASPanel shown)
```

Mode transitions ride ONE tweened property, `FamilySimInterp`
(`TS1CASScreen.cs:57-73`): value = mode−1 in [−1..1] (initial −2); it moves
`CASPanel.Position` (`(ScreenWidth−500)/2`, `10 − 282*(1−v)`), fades
`FamilyPanel.ShowI = 1−|v|` and `FamiliesPanel.TitleI = 1−|v+1|`, and drives
the 3D camera through `CameraInterp` (`TS1CASScreen.cs:134-174`).
`SetMode` (`TS1CASScreen.cs:616-642`) also swaps the accept button art
(`btn_movein.png` in FamilySelect, `btn_accept.png` otherwise) and starts the
1 s quad tween.

Back/Accept advance modes: `GoBack` (`:590-614`) pops toward FamilySelect
with a confirm dialog in FamilyEdit mode; `Accept` (`:553-588`) commits the
sim/family (confirm dialog 129[13]/[14] in FamilyEdit), or arms
`MoveInFamily` in FamilySelect, then `SetMode(Mode−1)`.

### 1.3 The screen (`UI/Screens/TS1CASScreen.cs`, 1097 lines)

Owns the 3D side entirely:

- `InitializeLot()` (`:1004-1068`): creates `FSO.LotView.World`, adds it to
  `GameFacade.Scenes` (`:1010-1011`), boots a local-server `VM` and loads the
  **staged CAS lot** `Content/cas.fsov` (`:1020-1027`), connects a fake admin
  client, then instantiates **18 head avatars + 18 body avatars**
  (`vm.Context.CreateObjectInstance(0x7FD96B54, ...)` — the Sim object GUID,
  `:1055-1065`) and `PopulateSimType("ma")`.
- `Update()` (`:662-766`): first tick sets wall cut/level per mode
  (FamilySelect sees Level 2 + roofs; others Level 1 + room cutaway), pins
  the clock to 12:00, gates the Accept button per mode, and when
  `Mode == SimEdit` runs `UpdateCarousel`.
- **Carousel**: 18 avatars of each kind are placed on a circle at big-tile
  (28.5, 21.5), radius 4.5, angle = (relPos/24)*2π with relPos wrapped so the
  front 3 slots are dense (`:734-754`). `UpdateCarousel` (`:253-365`)
  implements drag-to-spin (mouse-Y > 282 = body ring, else head ring),
  damped snap to integer positions, per-step click sound, and reclothes
  ring slots through `SetHead`/`SetBody` (`:367-401`) building TS1 `Outfit`s
  (`.apr` appearance + texture + literal handgroups).
- **Collections**: `PopulateSimType` (`:181-222`) reads
  `Content.Get().BCFGlobal` collections "c" (heads) / "b" (bodies) per avatar
  code (`ma/fa/mc/fc`), matches textures by skin suffix (lgt/med/drk) through
  `TS1AvatarTextureProvider.GetAllNames()`, and derives handgroups
  (`huao<skincolor>`).
- **Family staging**: `WIPFamily` (CASFamilyMember list) + `RepresentFamily`
  (live VMAvatars posed in the world); `SetFamilyMember` (`:829-880`) places
  each at staggered offsets around tile (34,31) and re-equips them;
  `SaveFamily`/`CASToNeighGen` (`:882-905`, `:915-920`) feed
  `SimitoneNeighbourGenerator.CreateFamily`.
- **World lifecycle**: `CleanupLastWorld` (`:977-1002`) kills ambience,
  sounds, net, removes the World from Scenes and disposes.

### 1.4 UI elements created today, with their art/strings

**TS1CASScreen ctor (`:435-465`)** — all `Content.Get().CustomUI` pngs
(= `Client/Simitone/Simitone.Client/Content/uigraphics/**`, port-made art):

| element | art | position |
|---|---|---|
| `CASPanel` (UISimCASPanel) | — | (0, −400) then tweened |
| `FamilyPanel` (UIFamilyCASPanel) | — | stripes at screen edges |
| `FamiliesPanel` (UIFamiliesCASPanel) | — | centered list |
| `BackButton` UITwoStateButton | `btn_back.png` (common) | (25, SH−140) |
| `AcceptButton` UITwoStateButton | `btn_accept.png` / `btn_movein.png` | (SW−140, SH−140) |

**UISimCASPanel (`UI/Panels/CAS/UISimCASPanel.cs:62-228`)** — the tabbed
right-side editor; *everything except five labels is mobile-styled*:

- Tabs: `UIStencilButton` ×3 — `cas_sim.png` (85x85), `cas_per.png`,
  `cas_bio.png` at y 0/93/186; tab backgrounds drawn as flat
  `UIStyle.Current.Bg` rects in `Draw` (`:286-296`).
- First-name label `GetString("130","26")` + `UITextBox` (401x48) (`:82-96`).
- SIM tab: port-authored labels "Gender"/"Age"/"Skin Color" (**no original —
  disclosed R116**), buttons `cas_male.png`/`cas_female.png` (64x64),
  `cas_adult.png`/`cas_child.png` (64x64), `cas_skinlgt/med/drk.png`
  (90x45), random die `cas_rand.png` (80x80) (`:100-157`).
- PERSONALITY tab: five labels `GetString("130","17".."21")` right-aligned,
  and five `UICASPersonalityBar`s (`:388-436`) = 10 two-segment pips drawn
  from `bar.png` tinted `SkillActive/SkillInactive`; remaining-points gauge
  is 25 rotated `skill.png` pips in `Draw` (`:299-307`). AllowedPoints=25.
- BIO tab: `GetString("130","24")` label, 9-slice `cas_bio_bg.png` (31x31
  source), `UITextEdit` 371x133 (`:206-223`).
- Glyph twins (R116): `FirstNameTwin`, `TraitTwins[5]`, `BioTwin`
  (`UIOriginalText` on `OriginalGlyphFont.LoadCaption`), mounted on first
  `Update` (`:359-385`).

**UIFamilyCASPanel (`UI/Panels/CAS/UIFamilyCASPanel.cs:66-111`)** — the
Create-Family chrome; *fully mobile-styled*:

- `UIDiagonalStripe` name bar + list bar (`:76-101`), full-screen-width
  `UITextBox` last-name (37 pt text) with label `GetString("129","12")`.
- `UIAvatarListPanel` (`:180-240`): `UIAvatarSelectButton` per member
  (icon via `UIIconCache.GetObject(avatar)` = 3D headshot render) + a
  `cas_new_plus.png` (46x46) slot; selection opens a `UICategorySwitcher`
  with `cas_cat_edit.png` / `cas_cat_del.png` (65x65) categories
  (`:56-61`, `:126-139`).
- `LastNameTwin` glyph twin (`:161-177`).

**UIFamiliesCASPanel (`UI/Panels/CAS/UIFamiliesCASPanel.cs:40-77`)** —
Pick-A-Family list; *fully mobile-styled*:

- `UITouchScroll` 810-wide centered, 180px items, no draw bounds; each item
  is a `UIFamilyCASItem`.
- Title `GetString("128","8")` ('SELECT A FAMILY') in a flat Bg bar;
  `btn_deletefam.png` / `btn_createfam.png` (230x115) right-edge buttons;
  `TitleTwin` glyph twin (`:138-154`).

**UIFamilyCASItem (`UI/Panels/CAS/UIFamilyCASItem.cs:36-70`)** — per-family
row: 9-slice `circle10px.png` capsule sized 100·members+10 wide,
`dialog_title_grad.png` selection wipe, FAMs[0] name label, and a
`UIAvatarSelectButton` row with live one-tick avatars
(`vm.Context.CreateObjectInstance(guid...)` per member, `:82-108`).

**Dialogs**: `UIMobileAlert` confirmations for delete-family
(`GetString("128","6"/"7")`, `TS1CASScreen.cs:776-804`), cancel-family
(129[7]/[8], `:596-605`) and finish-family (129[13]/[14], `:566-575`) —
desktop branch renders the R142 `UIOriginalDialogChrome` already.

**Mobile-styled inventory (what a desktop port must replace)**: the three
stripes/tab-pip/9-slice capsule system, `UITouchScroll`, stencil buttons,
port png buttons (btn_back/accept/movein/createfam/deletefam, cas_*),
`UICategorySwitcher`, and the "Gender/Age/Skin Color" authored labels.
Original-side replacements exist for all of it in Res_Nbhd (backgrounds
PickBkg/DsgnFamBkg/CreateACharBack, PAF*/DesignFam*/DesignChar* plates,
FamilyPickItemBkg 3-row items, Smiley, PersonInfoBkg/pers bkg, ScrollBar,
PersLED, CACPointRemainingBars — see `cas-art-states.txt`).

---

## 2. Far-member BMP load path — and the RLE8 answer

Trace of `UIOriginal.EnsureResolved` → texture (exact code path):

1. `UIOriginal.EnsureResolved(memberName)`
   `Client/Simitone/Simitone.Client/UI/Model/UIOriginal.cs:16-52` — resolves
   through `Content.Get().TS1Global.Get(name)`, with a case-insensitive retry
   over `ts1.GetFarEntries(".bmp")` (R121 law). Any exception → null → caller
   keeps its png fallback.
2. `TS1Provider.Get` `FreeSO/TSOClient/tso.content/Framework/TS1Provider.cs:91-93`
   → `FAR1Provider<object>.Get(item)` (codec **null** for TS1Global,
   `TS1Provider.cs:29`).
3. `FAR1Provider<T>.Get(entry)` `Framework/FAR1Provider.cs:132-146` — pulls
   the raw FAR bytes and, since Codec==null, calls
   `SmartCodec.Decode(stream, ".bmp")` (`:140`).
4. `SmartCodec` `Codecs/SmartCodec.cs:19` maps ".bmp" → `TextureCodec`.
5. `TextureCodec.GenDecode` `Codecs/TextureCodec.cs:49-63` wraps the RAW
   bytes in `InMemoryTextureRef` — **no decode yet** (decode deferred to GPU
   thread via `Get(device)`).
6. `AbstractTextureRef.Get(device)` `Model/TextureRef.cs:135-173` →
   `Process(device, stream)` (`:212-215`) =
   `ImageLoader.FromStream(device, stream)`.
7. `ImageLoader.FromStream` `FreeSO/TSOClient/tso.files/ImageLoader.cs:172-175`
   → `WinFromStream` → `WinDataFromStreamP` (`:296`). Magic `0x4D42` ("BM")
   branch (`:302-334`): if `ImageLoaderHelpers.BitmapFunction != null` it is
   used (byte[] BGRA + dims), else falls back to
   `Texture2D.FromStream(gd, str)` (`:321-327`) — MonoGame's stb_image.
   The BitmapFunction branch also applies the magenta key:
   `ManualTextureMaskData` (`:315`, law at `:405-419` — B≥248 && R≥248 &&
   G≤4 → alpha 0), which is exactly the key used by the Nbhd sheets.
8. Host wiring of `BitmapFunction`:
   - **Simitone.Desktop** (macOS/Linux/cross-platform):
     `Client/Simitone/Simitone.Desktop/Program.cs:355` → `BitmapReader`
     (`:504-518`) = **ImageSharp** `Image.Load<Rgba32>`; package pinned at
     `SixLabors.ImageSharp 3.1.11` (`Simitone.Desktop.csproj`).
   - **Simitone.Windows**: `Client/Simitone/Simitone.Windows/Program.cs:294`
     → `BitmapReader` (`:375-398`) = **System.Drawing GDI+**
     `Bitmap.FromStream` + `LockBits`.

### CAN THE PATH DECODE RLE8? — YES on every shipped host (empirically).

- The Res_Nbhd art **are** standard BMPs with `BI_RLE8` (compression=1,
  8bpp, 40-byte info header) — verified by raw header parse of all 62
  `Nbhd_*.bmp` files this round (see `cas-art-states.txt` §F; every CAS/nbhd
  member reads `bpp=8 comp=1`, palette at offset 1078).
- **Empirical test** (this host, dotnet 10 console harness replicating the
  exact `BitmapReader`): `SixLabors.ImageSharp 3.1.11`
  `Image.Load<Rgba32>` decoded `Nbhd_AddPersonBtn.bmp` (556x103),
  `Nbhd_CreateACharBack.BMP` (800x600), `Nbhd_ScrollBar.BMP` (300x30),
  `Nbhd_Smiley.bmp` (20x23), `Nbhd_PAFAddFamily.bmp` (368x62) — all OK at
  full dimensions. **The Simitone.Desktop path decodes RLE8 today.**
- **GDI+** (Simitone.Windows): GDI+'s BMP codec decodes BI_RLE8/BI_RLE4 —
  this is a documented GDI+ capability; not re-testable on this macOS host,
  so flagged as the one unverified leg. Given every Windows host sets
  `BitmapFunction`, stb_image never runs there.
- **stb_image fallback (`Texture2D.FromStream`) does NOT support RLE8.**
  MonoGame 3.8.4 DesktopGL uses stb_image_sharp, whose BMP loader only
  accepts uncompressed 24/32bpp (RLE → "not supported"). This path only runs
  when `ImageLoaderHelpers.BitmapFunction == null` — i.e. a host that never
  installed a reader (neither Simitone host; `FSO.Windows/Program.cs:33`
  installs GDI+ too). In that case `WinDataFromStreamP` throws → caught at
  `UIOriginal.cs:47-51` → `EnsureResolved` returns null →
  `ResolveOrPng`/`Frame` fall back to png twins. **Conclusion for the port:
  png twins are NOT required for RLE8 on the two shipped hosts — the far
  members themselves are loadable via `UIOriginal.EnsureResolved`/`FrameW`/
  `Rect` — but keep the png-twin fallback for exotic hosts exactly as the
  existing `ResolveOrPng` pattern does (UIOriginal.cs:99-114).**

One caveat: `Frame()`'s auto 2–4 state split only fires when
`Width % Height == 0` (UIOriginal.cs:74-77); CAS sheets don't satisfy that
(use `FrameW` with the explicit per-state widths from `cas-art-states.txt`,
or `Rect`).

---

## 3. Autotest: instantiating new desktop CAS panels standalone

The `-autotest` harness (hosted by Simitone.Desktop:
`Simitone.Desktop/Program.cs:116-136, 393-398` → `AutotestRunner.Begin`,
which hooks `GameThread.EveryUpdate(Tick)`, `AutotestRunner.cs:143-162`)
runs all UI checks in ONE batch once the neighborhood screen has loaded a
lot (`StateWaitLot` → LOT-READY, `AutotestRunner.cs:262-309`; the batch
dispatch site is the big `if (CheckEnabled(...))` chain around
`AutotestRunner.cs:690-789`, e.g. `:727 if (CheckEnabled("uicas")) CheckUICAS();`).

What precedent shows a CAS-panel check can rely on:

- **Panels construct standalone** — `CheckUICAS` (`AutotestRunner.cs:4778-4843`)
  already does `new UISimCASPanel()`, `new UIFamiliesCASPanel()`,
  `new UIFamilyCASPanel(new List<VMAvatar>())` and ticks each with
  `sim.Update(new UpdateState())`. Needed: only `Content.Get().CustomUI`
  (mounted) and `GameFacade.GraphicsDevice` (non-null at that point —
  `CheckUIDialogChrome` uses it directly and null-guards for headless:
  `AutotestRunner.cs:8508-8509`).
- **Far art resolves inside checks** — `CheckUIDialogChrome` pattern:
  `UIOriginal.EnsureResolved("shared\\sys\\PieButt.bmp")?.Get(gd)` then assert
  dims (`AutotestRunner.cs:8584-8587`). The same one-liner works for
  `Nbhd\*` members; RLE8 decode included (§2).
- **Live-screen access** — `CheckUCP` (`AutotestRunner.cs:8343-8353`) reads
  `GameFacade.Screens.CurrentUIScreen as TS1GameScreen` → `.Frontend`.
  For a desktop CAS screen the equivalent would be to drive
  `GameController.EnterCAS()` via `GameThread.NextUpdate`, wait for
  `CurrentUIScreen is TS1CASScreen`, assert, then re-enter the game screen.
- **Strings** — `GameFacade.Strings.GetString("128"/"129"/"130", idx)` works
  at check time (`CheckUICAS` `:4832-4833`); disk-pin precedent walks
  UIText.iff with `FindStrChunkByID`/`ParseLang1`
  (`AutotestRunner.cs:4497-4541`).
- **Evidence dumps** — attach the constructed element to the current screen,
  force `InterpolatedAnimation = 1f`, `SurveyDump("name", tree)`, detach
  (`AutotestRunner.cs:8569-8576`).

A new `uicas2`-style check for desktop CAS panels therefore needs: nothing
beyond what `CheckUICAS`/`CheckUIDialogChrome` already use. If the new
panels keep parameterless ctors (or take the avatar list), the check can
construct them, tick once, and assert art ids/dims (`FrameW` widths from
`cas-art-states.txt`), label strings (§4), and twin mounts.

---

## 4. String tables — catalogs + exact indices

Full dumps written this round (disk walk = the autotest's own
`FindStrChunkByID`/`ParseLang1` law): `uitext-128-PickFamilyStrs.txt`,
`uitext-129-DesignFamilyStrs.txt`, `uitext-130-DesignCharStrs.txt` in this
dir — each line `[index] 'value' | 'comment'`, where the **comments are
Maxis's own UI-element names** (the map from string → screen element).

### STR# 128 `PickFamilyStrs` (400 entries; 20 English) — Pick-A-Family
| idx | value | element (Maxis comment) |
|---|---|---|
| [0]/[9] | Net Worth | column labels (dup at [9] for dialog use, '!') |
| [1] | Friends Score | pick-family label |
| [2] | Create New Family | Button: Tooltip (PAFAddFamily plate) |
| [3] | Move In Family | Button: Tooltip (PAFMoveIn plate) |
| [4] | Delete Family | Button: Delete Family: Tooltip |
| [5]/[9] | Cancel | Button: Cancel: Label |
| [6] | Delete Family? | Delete dialog TITLE (ported) |
| [7] | Are you sure you want to delete the "%s" family? | Delete dialog TEXT (ported; %s built-in) |
| [8] | SELECT A FAMILY | Screen: Title (ported; uppercase in canon) |
| [10] | Net Worth | Icon: Tooltip |
| [11] | Family Friends | Icon: Tooltip |
| [12] | Personality for %s | Character: Popup: Title (PersonInfoBkg 5300 popup) |
| [13] | Create a Family? | first-visit dialog title |
| [14] | You can create a new family by clicking on the Create a Family button at the bottom of the screen | first-visit dialog text |
| [15] | Back to Neighborhood | Cancel: Tooltip |
| [16] | Click to create a new family | empty-slot tooltip |
| [17]-[19] | Family MagiCoins / MagiCoins? / notice | Spellbound (Creepy Hollow) columns |

### STR# 129 `DesignFamilyStrs` (270 entries; 15 English) — Create-A-Family
| idx | value | element |
|---|---|---|
| [0] | Add a New Sim | Button: Tooltip (AddPersonBtn plate) |
| [1] | Delete Selected Sim | Delete Sim: Tooltip |
| [2] | Edit Selected Sim | Button: Tooltip |
| [3] | Done | Button: Done: Label |
| [4] | Cancel | Button: Cancel: Label |
| [5] | Delete Sim? | delete dialog title (NOT yet ported — port deletes immediately) |
| [6] | Are you sure you would like to delete this Sim? | delete dialog text |
| [7] | Cancel? | cancel dialog title (ported) |
| [8] | Are you sure you want to cancel and lose this family? | cancel dialog text (ported) |
| [9] | CREATE A FAMILY | Screen: Title (NOT yet ported — screen has no title today) |
| [10]/[11] | Done / Cancel | ('!' duplicates) |
| [12] | Enter Last Name: | Screen: Label (ported) |
| [13] | Finished? | done dialog title (ported) |
| [14] | Are you sure you are finished? You cannot return... | done dialog text (ported) |

### STR# 130 `DesignCharStrs` (504 entries; 28 English) — Create-A-Sim
| idx | value | element |
|---|---|---|
| [0]/[1] | Adult / Child | Button02/01: Tooltips (age buttons) |
| [2]/[3] | Female / Male | Button07/06: Tooltips (gender buttons) |
| [4]/[5]/[6] | Dark / Medium / Light | Button05/04/03: Tooltips (skin buttons) |
| [7]/[8] | Previous/Next Body | Button10/11 (skins scroll) |
| [9]/[10] | Previous/Next Head | Button08/09 |
| [11]/[12] | Done / Cancel | Button: Label (wide plates' captions ARE text) |
| [13] | More Personality! | unassigned-points dialog TITLE (NOT yet ported — port allows Done with points left) |
| [14] | You haven't used all the personality points. Are you really done? | same dialog TEXT |
| [15] | CREATE A SIM | Screen: Title (NOT yet ported) |
| [16] | PERSONALITY | Screen: Label02 ("Will appear above personality traits. Not currently displaying." — Maxis's own note!) |
| [17]-[21] | Neat / Outgoing / Active / Playful / Nice | trait labels (ported; no colons in canon) |
| [22]/[23] | DONE / CANCEL | uppercase ('!') variants |
| [24] | BIO | Screen: Label08 (ported) |
| [25] | Bio for %s | Label09 — the bio title AFTER naming (NOT yet ported; port shows static 'BIO') |
| [26] | Enter First Name: | Screen: Label01 (ported) |
| [27] | Astrological Sign | Button: Tooltip (the zodiac readout under the personality panel) |

Related but out-of-slice: 131 EvictModeStrs, 132 MoveInModeStrs (move-in /
bulldoze lot dialogs), 220 Clothing Dialog Text (Buy outfit). Full tables
dumped in this round's scratch notes; only [128]/[129]/[130] are CAS.

**Points-remaining is NOT a string.** No UIText entry says "N points left";
the original UI is the art plate `Nbhd/CACPointRemainingBars.bmp`
(kPeoplePtsBuffer RT 5203, 149x25, single-state) + `DesignCharPersLED`
(59x25) with the count engine-drawn; the only text is the [13]/[14] warning
dialog.

**Button captions vs baked art**: Done/Cancel labels are text ([11]/[12] in
130, [3]/[4] in 129, [5] in 128); the PAF plates' titles are tooltips
([2]/[3]/[4] in 128); the type/skin/arrows are icon plates with tooltips
([0]-[10] in 130). The 800x600 backgrounds bake their screen titles
("SELECT A FAMILY" etc. appear in BOTH art and strings — the strings are the
tooltip/dialog/title layer the engine draws or shows on hover; the painted
headings are in the background art).

Engine-side corroboration (r143 disasm, already on disk):
`nbhd-disasm-winpickfamily-init.txt:80` loads StringSet `0x80` (=128); its
GetString ids {17, 9, 11, 12, 16, 18} (1-based) = [16],[8],[10],[11],[15],[17]
0-based — the empty-slot tooltip, title, and column tooltips above.
`cas-disasm-buildpersonalitybtns.txt` loops r27=0..4 with
`GetString(r27+0x12)` = ids 18..22 = traits [17]-[21].

---

## 5. 3D carousel rendering + where a 2D background layers

- The CAS 3D scene is a normal `FSO.LotView.World` living in
  `GameFacade.Scenes` (`TS1CASScreen.cs:1008-1011`). Camera: in Full3D
  graphics mode the screen grabs the World's first-person camera
  (`TS1CASScreen.cs:673-679`) and `CameraInterp` lerps
  `ModePositions[4]`/`ModeTargets[4]` keyframes (`:102-116`, one per mode,
  with sinusoidal x-offsets `SinTransitions`); otherwise (2D cutaway mode)
  it pans `World.State.CenterTile`/`PreciseZoom` through the `Mode2D` table
  (`:118-156`). The rings themselves are 18+18 VMAvatars positioned on a
  circle every frame (`:734-754`).
- **Draw order law**: `SimitoneGame.Initialize` adds the layers as
  `base.Screen.Layers.Add(SceneMgr); base.Screen.Layers.Add(uiLayer);`
  (`Client/Simitone/Simitone.Client/SimitoneGame.cs:267-268`) — the 3D layer
  (`GameFacade.Scenes`, `_3DLayer`) draws ALL its scenes in list order
  (`FreeSO/TSOClient/tso.common/Rendering/Framework/3DLayer.cs:47-53`),
  and the UI layer (`GameFacade.Screens` → `UILayer.Draw` → current
  UIScreen children, `FSO.UI/UILayer.cs:337-339`) draws AFTER.
- Consequence for the port: a full-screen 800x600 background
  (PickBkg/DsgnFamBkg/CreateACharBack) added as a child of `TS1CASScreen`
  would paint OVER the World and hide the carousels/family avatars. To get
  the original composition (background behind the 3D sims, panels on top)
  the background must be drawn in the 3D layer BEFORE the World — i.e. a
  `_3DAbstract` backdrop scene inserted into `GameFacade.Scenes` at a lower
  index than the World (list order = z order per 3DLayer.cs:47-53), or
  drawn by the World itself. Everything else (plates, plates' text, item
  rows) belongs in the UIScreen tree above the World, which is the existing
  layering for free.
- Screen-size note: the original screens are 800x600 compositions; the
  panels today are resolution-fluid (ScreenHeight-relative). Desktop
  fidelity will need the 800x600 anchor law (centered or letterboxed)
  decided the same way r142 did for the 1024-wide bottom bar.

---

## 6. Work log / dead ends

Checked:
- TS1CASScreen.cs (full), all four CAS panel files (full), CAS entry/exit
  call graph (UINeighbourhoodSwitcher → GameController.EnterCAS; exit tween
  → EnterGameMode), AutotestRunner CAS/UCP/dialog-chrome checks + harness
  lifecycle, SimitoneGame layer wiring, _3DLayer/UILayer draw order.
- Full BMP load chain UIOriginal→TS1Provider→FAR1Provider→SmartCodec→
  TextureCodec→TextureRef→ImageLoader→host BitmapReader; both host wirings.
- BMP headers of all 62 Nbhd_* files (comp codes); PIL decode; state-sheet
  decomposition (key-seam + mask-correlation + bbox; column-profile
  repetition for opaque plates); key-color census.
- UIText.iff full catalog list (74 STR# chunks) + complete English dumps of
  128/129/130 with Maxis comments; targeted dumps of 131/132/220;
  all-catalog search for points-remaining text (none exists).
- Empirical ImageSharp 3.1.11 RLE8 decode test (all OK, full dims).
- Cross-checked the r143 disasm artifacts already on disk (parallel effort):
  extracted every `SetImage` (vtable+0x1a4) call with cols/rows args, the
  StringSet 0x80 load, and the personality-trait GetString loop; resolved
  the pick-family-item 1x3 law.

Dead ends / caveats:
- TOC-chasing the nbhd createbutton art-id table (TOC[-0x4cf4] → 0x59b990)
  fails: the pointer is beyond the unpacked data section (sec1 len 0x7bf80);
  raw-binary offsets don't yield a clean 5000-range table. Left to the
  disasm agent.
- `Texture2D.FromStream` (stb_image) RLE8 rejection is asserted from the
  stb_image format matrix, not run — it only matters for hypothetical hosts
  that leave `BitmapFunction` null, which neither shipped host does.
- GDI+ RLE8 (Simitone.Windows leg) is documented-capability, not
  machine-verified (no Windows host here).
- `Nbhd_PAFCancel.bmp` 744x49: 3-state read is LOW-MED confidence (profile
  corr .81 vs .75 at k=2); needs the engine constructor before pinning.
- `Nbhd_Current.bmp` (29x46), `Nbhd_Import.bmp` (372x61), `Nbhd_MoveIn.bmp`
  (372x122), `Nbhd_PersonInfoPersBkg.bmp` (40x14) are far members NOT in
  the RT template (the RT lists NghUI variants at other dims) — proto/
  unused art; don't wire them to RT ids blindly.
- Frame ORDER inside every multi-state sheet (which cell is up/down/hover/
  disabled) remains the port's standing disclosure (UIOriginal.cs:54-59);
  the engine state mapping is not yet decoded for these screens.
