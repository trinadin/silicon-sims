# UI Gap Survey — original canon with NO port surface

**Method.** Manifest = all 794 UIGraphics.far members (`tools/iff-dump/r142/manifest.txt`). Symbol map = `tools/iff-dump/r142/rt-inventory.txt` (637 rows parsed from the 4 RT/.h pairs: SMCtrlMgrRes 17–31, Res_Other 9000–9005, Res_Nbhd 5000–6338, Res_CPanel 48–4995). Port surface = scan of every original-path literal + `GetString("id")` + `EnsureResolved(...)` in `Client/` (612 art paths, 23 direct resolvers; `ContentStrings.LoadTS1` loads **only UIText.iff**; Live.iff tables are consumed only via `OriginalLiveStrings.Wanted` = {130,131,133,135,136,137,138,140,141,142}) cross-checked against PARITY.md rows 83–149. No files were extracted or modified; all scratch work in /tmp/uigap/. Scratch artifacts: `/tmp/uigap/{unreferenced.txt, families.txt, uitext_clean.tsv, live_clean.tsv, client_allpaths.txt}`.

**Already mounted (do not re-plan):** boot/logo + 17 locales, all GO loadscreens (incl. 1024x768), cursors 51, English .ffn 45 (+ ladder rendered r119+), neighborhood screens/waves/cars/clouds/balloon/nessie/top_layer, UCP plate + mode cascade + camera diamond + speeds + Lev1/2/Roof + wall row + patches + PanelBack, category tabs 4500–4507, Green/Redbars, LiveGadget gauge, ratings bars (House/Job/Rel sheets + Off backdrops + FameSubBars 4822/23), TrackingTarget, Webcam strip (PeopleTemplate/UnknownFace), build tools + terrain + roof families, buy/build/expansion plaques + subsorts + BackButtons + ThumbTemplate, PopupInfo(+Tiles), options panel kit (Opt* + opt* + 18 PopupOpt*), interest plaques + InterestBars + 3 inventory filters, dialog chrome GenDlg+WinBtn, pie disc (ViewMenuBackground+PieButt), QueueCancel, CAS screens (PickBkg/CreateACharBack/DsgnFamBkg + buttons + PAF + Smiley + NbhdTileBtn + PersLED + points bar), logo stamps.

---

## A. UIGraphics.far art families with NO port surface — BASE GAME

| # | Family (RT ids · const → member, dims/bytes) | Round doc | Confidence |
|---|---|---|---|
| 1 | **People pie face** — 800 kPieFaceBkg → `cpanel\PieFace1.bmp` 201x201, 121,460 B. **No PieFace2 exists anywhere in the manifest** (only pieFace1). r150 radius law already decoded | r142 residual ("people pie PieFace1 r150 unported"); r131 (cWinPeople Init loads it) | HIGH |
| 2 | **Speech balloon backdrops** — 9000 kSpeechMediumBmp → `Other\SpeechMedium.bmp` 16x16; 9001 kSpeechLargeBmp → `Other\SpeechLarge.bmp` 32x32. Engine consumer: `Product::GenerateSpeechIcons`/`GetSpeechImage` (symbol-index 3063/3064) | r142 inventory only ("speech balloons" header) | HIGH |
| 3 | **Tragedy/event dialog icon** — 9005 kTragedyUnhappyMask → `Other\TragedyMask.bmp` 133x103 (the cWinPictureDialog bad-event icon; same class r142 decoded for Sim messages). 9002 kMaxisLogo ABSENT from disk | none — never mentioned in any round | HIGH |
| 4 | **kHelpBtn** — 4993 → `CPanel\Buttons\HelpButton.BMP` 40x15, engine anchor (5,164) | r142 explicit residual | HIGH |
| 5 | **Camera panel family** — 2034 kCameraBtn `camera.bmp` 116x29 (anchor 175,95); 1101–1104 CamFrameSmall/Med/Large/Cust; 1105 CamViewAlbum; 1106–1108 CamQualLow/Med/High; 1109/1110 CamPopupDetail/QualDetail; 4914/4915 CamToothPicks; 2035 CameraPatch; 1100 kCamBack ABSENT from disk. Strings: UIText **140 cammodestrs** (20 Eng) unconsumed | r142 survey §3.7 ("camera tool has no port surface") | HIGH |
| 6 | **Tutorial UI** — 4980 kTutorialHighlightBMP → `cpanel\TutHigh.bmp` 150x50; strings UIText **166 Help** (94 Eng — the biggest unconsumed table); engine Tutorial fn family decoded (TryTutorial 0xf7082, ShowTutorialInfo, SetTutorialObject, CancelTutorial, TutorialCompleted); `UserData\Tutorial.FAM` staged. No "arrow/overlay" art exists in UIGraphics beyond TutHigh | r142 survey §3.8; r121 wired only the Reset Tutorial row | HIGH |
| 7 | **Budget window** — 4965 kBudgetBkg `BudgetBack.bmp` 800x600 (483,842 B); 4966 kBudgetOK 868x52; strings UIText **146 budgetstrs** (36 Eng); engine cWinBudgetDlg + cWinBudgetRow fully symbol-mapped (r141 symbol-index 3677–3695) | none — never mentioned | HIGH |
| 8 | **Scrapbook / photo album** — 1200 kScrapBack 700x560 (384,746 B); 1201 kScrapDone 520x41; 1202 kScrapDelete; 1203–1206 ScrapLeft/Right/Home/End; strings UIText **141 scrapbookstrs** (17) + **144 ScrapStrs** (23); engine cWinScrapbook mapped (4025–4032) | none — never mentioned | HIGH |
| 9 | **Phonebook dialog** — 80 kPhoneBookBkg `phonebookbkg.bmp` 620x356; 81 kPhoneBookIcon 33x23. Port call dialog = mobile chrome; only STR 180[0] consumed | r142 survey P1-10 | HIGH |
| 10 | **Job subpanel composites** — 4902 kJobIconGeneric `JobIconMultiButton.BMP` 112x484 (all career icons); 4905 kJobPopupComposite `JobIconMultiPopup.bmp` 84x1386; 4981 kSalaryBtnBMP 52x11; 4982 kPerformanceBtnBMP 52x12; 4983 kJobFriendSmileyBMP 40x11 | r132 mapped, never mounted; r142 survey P2-21 (career button mobile) | HIGH |
| 11 | **Relationship subpanel family** — 4105 kRelSmiley `smiley.bmp` 9x9; 4106 kRelHeart; 4107 kRelHeartDeep; 4108–4111 kRelFamilySort/kRelFriendSort/kRelAllSort/kRelFamousSort 80x20; 1049 kUnknownRel 200x40; 1050 kUnknownThumbnail 25x25; 108 kPeopleThumbnailBackground `ThumbBack.BMP` 25x25; 3750 kSocialPopupIcon `SocialInfoPopup.BMP` 133x103 (r131: loaded ×2 by cWinPeople Init). Strings: Live **132 Relationships** (15 Eng, shipped as Hot-Date placeholders) + UIText **240** sort tooltips (4). `UIRelationshipSubpanel.cs` has zero original art | r142 residual; r145 mounted only the 3 inventory filters | HIGH |
| 12 | **System chrome beyond GenDlg/WinBtn** — 18 WinChk 168x28; 19 WinSlH 48x12; 20 WinSlV 12x48; 21 WinScrol 204x20; 24 DropDown 16x16; 26/27 ListBack 136x22 (list-row bg — CAS/phone lists); 30 CloseBox 48x11; 31 MinimizeBox 48x11 | r142 catalogued ("system buttons" table), mounted only 17/22 | HIGH |
| 13 | **ViewMenu zoom/rotate sheets** — 820 kItemZoomIn `ViewMenuZoomIn.bmp` 378x84; 821 ZoomOut; 822/823 RotateLeft/Right 420x42 (the click-hold view-menu label sheets; only 825 background mounted) | r142 pie law (background only) | MEDIUM-HIGH |
| 14 | **Live-subpanel backdrop** — 4918 kTallSubpanel `TallSubPanel.TGA` 600x150, 360,044 B; also 4903 kPersBkg referenced only in palette comments (not mounted) | none — never mentioned in any round doc | HIGH |
| 15 | **Skill pip** — 4511 kSkillHilite `SkillsHilite.bmp` 4x11 (skill pips are `skill.png` mobile) | r142 survey P2-22 | HIGH |
| 16 | **Neighborhood navbar (NghUI family)** — 5200 MoveIn, 5201 Bulldoze, 5202 Exit, 5204 Previous, 5205 Next, 5206 Current 23x30, 5207 Import, 5215 Rezone, 5420 kNghBarBkg `banner_neighborhood.bmp` 800x52, 5306 kNghInet, 5307+5312–5316 kNghCredits (+locale), 5350 InetDialogIcon, 5300 PersonInfoBkg 36x36, 5305 kNoOneLivesHereIcon `MustPickFam.BMP` 133x103, 5018 kPickScrollbar `ScrollBar.bmp` 300x30, 5001 kLargeMask (1024x768 surround). Port panel = mobile `ngbh_*.png` | r143 decoded the full 14-button law, port never done; r142 survey P0-3 | HIGH |
| 17 | **CAS-family dialog stubs** — 4991 kDialogEditBioDisplay 337x164; 4992 kPictureDialogStub `SimStub.BMP` 133x103; 4994 kDialogEditDTDescription `LotDescription.bmp` 337x164 | none | HIGH |
| 18 | **Zodiac sign names** — UIText **164 signs** (14 Eng, [0..11] names) — zodiac computed r143 but the NAME strings are never read | r143 decoded, port shows only "(Sign)" caption | HIGH |
| 19 | **Webcam logo** — 4400/4401 kWebcamLogo(+Alpha) `SimsLogo.bmp` 75x43 (+locale) — the plumbob stamp on the webcam strip | none | MEDIUM |
| 20 | **kExploreAnchor** — 4000 `ExploreAnchor.bmp` 15x15 | none | MEDIUM |

**Engine-dead canon — do NOT port** (proven r143/r148): wide plates 5033/5034, 5026/5027 (DesignFamily Done/Cancel), HotspotPopup 5101/5102/5107, SimEstates 5015, Marquee 5016, PAFCancel 5104; absent-from-disk backgrounds BuyBack(48)/OptionsBack(103)/BuildBack(166)/LiveBack(4702)/CamBack(1100)/DisposeBack(3007)/IconTemp(4201)/MaxisLogo(9002)/dlgframe(5423). Unreferenced strays (no RT id, low priority): cpanel root Rotate/Zoom 31,808 B copies, BuySTDining/STMisc/STShops, InterestsExcercise.bmp, Personality.bmp, ButtonTileDialog.BMP, AMPM.BMP (clock AM/PM — engine draws digits via .ffn; likely dead), littleStar.bmp, Shared extras (LBoxTile, CheckBtnNew, FLDRBTNS, ListBack136, ListToggle, SPINBTNS, TogglePushBtn, WinClose, WinRadioBtn, WinlbEdg), `Community\_` (1 B junk).

## B. UIGraphics.far — EXPANSION-DEFERRED (flagged)

| Family | Members | Round doc |
|---|---|---|
| Magic book (Makin' Magic) | 82 magicbookbkg 620x400; 90–94 SPELLBOOK_BakersOven/CharmMakerAdult/WandCharger/CharmMakerKid/NectarPress 130x90; 5020 kPickSpellItemBkg; STR 273 | none |
| Superstar | 4906 FamePopupComposite 84x63; 4907 FameIconGeneric 112x22; 4984 FameFriendSmiley; 278–280 kFameRunway/MovieSet/MusicVideo 128x110; 5071 CelebPoolPic, 5073 FanPoolPic; 5068/5069/5072 FilterMovie/Model/Music buttons; littleStar.bmp; STR 173/271; Live 165–168 name tables; Studiotown DScreen_theatrelights (cars themselves ARE mounted r91) | none (r142 vtable list names cPickSpellItem) |
| Unleashed / OldTown | 5045 kFilterToolbar `Filter_Toolbar_Unleashed.bmp` 800x72; 5060–5067 FilterLodging/Food/Gardening/Shopping/DogCat/SmallAnimal/Recreation/Spa buttons 200x52; 5070 ngh.bmp 119x80; 5075/5076 FilterRides/Magic; 4930–4942 cpanel filter* 150x50; STR 172/230/270; Live 145, 170/171/172/180 pet tables | none |
| Hot Date / Downtown | 5426 kDTPhone 70x135; 5432 kDTDlgBkg2; 5422 banner_downtown; Downtown Screen Tooltips **169** (5); Live 143; STR 212; GoDTButton/GoNghButton/OptDowntown/dscreensign strays | r89 mounted DT screen/water only |
| Vacation | 5431 kVIPhone; 5510 kVacationNoVacancy 50x50; 4995 kDialogVILogo; STR 170/213; Live 144; vacationlogo.bmp stray | loadscreen/waves mounted r83/r89 |
| Magicland | 5435 mtphone; 5436 kMagictownHole 101x138; 5074 MagicPoolPic; **Nhood_balloon_shadow.tga** 224x154 (the balloon/fog themselves ARE mounted r89/r92 — the shadow is NOT); STR 174/272; Live 147/181/182; Magicland Bulldoze/Return/TheSimsLogo/largeback strays | shadow never mentioned |
| 1024x768 surrounds | 5001 kLargeMask, 5002 kLargeMaskSS, magicland/studiotown largeback + dlgframe_1024x768 duplicates | none |

## C. UIText.iff STR# — consumed vs gap (74 tables)

**Consumed in Client** (code-verified): 128, 129, 130, 131, 132, 134, 136, 137, 138, 139, 142, 145, 147, 150, 153, 154, 155, 159, 160, 180, 200–207, 210, 220, 221. (Roughly: 145=options, 150+200-207+210=catalog, 138/139=tooltips, 155=splash, 159/160=tooltips/ratings, 128-130/134/220/221=CAS/lot-query, 142/152… see below.)

**Gaps — base game** (id · label · Eng entries · consumer):

| id | label | Eng | Belongs to |
|---|---|---|---|
| 140 | cammodestrs | 20 | camera panel (A5) |
| 141/144 | scrapbookstrs / ScrapStrs | 17 / 23 | scrapbook (A8) |
| 146 | budgetstrs | 36 | budget window (A7) |
| 148 | Pause | 2 | game-screen pause label |
| 149 | DirtToolErrs | 7 | terrain-tool error tooltips (r127 tools mounted without them) |
| 151 | NghbBtnTips | 14 | **neighborhood navbar tooltips** (A16) |
| 152 | DefaultDialogButtons | 4 | OK/Cancel/Yes/No for save/photo-alarm/nbhd dialogs (port uses 142) |
| 156 | PIPStrings | 1 | PIP window tooltip |
| 157 | ConfigStrings | 14 | in-game config UI labels |
| 158 | WebPageStrings | 13 | HTML family-web export (r121 Export HTML option exists; exporter absent) |
| 161 | LocalKeys | 31 | locale key names |
| 162 | FriendCountDlg | 3 | Family Friend Count dialog (UCP [20] tooltip also unused, r117) |
| 163 | credits | 65 | credits screen (CreditBtn art A16) |
| 164 | signs | 14 | zodiac names (A18) |
| 165 | SignPopups | 24 | zodiac popup descriptions |
| 166 | Help | 94 | tutorial help system (A6) |
| 167 | HTMLUpdate | 3 | new-families HTML dialog |
| 168 | TranslateImport | 7 | import-flow strings |
| 133 | RegularModeStrs | 4 | vacant-house hand-cursor dialog |
| 135 | Ranger | 1 | ("!" placeholder — dead) |
| 225 | Nbhd screen change failure tooltips | 5 | navbar failure states (A16) |
| 240 / 241 | Relationship / Inventory sort-button tooltips | 4 / 3 | r117 documented "wiring = new UI"; still unwired |

**Gaps — EXPANSION-DEFERRED**: 169 (Downtown), 170 (Vacation Is.), 171 (Filter Bar — UL/ST), 172 (UL Import), 173 (Studio Town), 174 (Magic land), 211 (Townies — Hot Date), 212 (Go Downtown), 213 (Vacation), 230 (Tourists), 250 (Rezone — UL), 251 (Zoning Types — UL; note FreeSO's tso.client reads "251" but from TSO tables, not this file), 260/261 (Visit Mode — UL/MM), 270 (UL Welcome), 271 (Celebrity loading), 272 (Magic pool), 273 (Transform Me — MM).

## D. Live.iff STR# — 10 of 35 consumed

**Consumed** (OriginalLiveStrings.Wanted): 130 Motives, 131 Personality, 133/135/138 House tables, 136/137 Job tables, 140/141/142 Interest/Gift tables.

**Gaps — base game**: **132 Relationships** (15 Eng — shipped as `***New - Hot Date*` placeholders, so the real port source is NBRS data; flag for content not art), **134 ModeTips** (10 — live-panel tab tooltips incl. [5] Interest/[6] Inventory icons Hot Date), **139 ReportCard** (3 — "Subpanel: School: Title/Popup" = the kids' school report; r131 proved the cWinSubpanelReportCard class exists; art likely reuses the Job family — no dedicated RT member).

**Gaps — EXPANSION-DEFERRED**: 143 Downtown, 144 Vacation leave, 145 Community leave (UL), 146 Studiotown leave, 147 Magictown leave, 150/151 Datable NPC names (HD), 160–164 Tourist names (Vacation), 165–168 Celeb/Fan names (Superstar), 170 Pet Personality, 171 Pet Skills, 172 Pet Skill Popups, 180 Pet Motives (UL), 181/182 Magic Town first names (MM).

## E. Answers to the named residuals

- **People pie**: `cpanel\PieFace1.bmp` only (121,460 B, 201x201, RT 800 kPieFaceBkg). **PieFace2 does not exist** in this FAR — r142's "PieFace2?" guess is settled negative. Unported, HIGH.
- **Relationship subpanel art**: gap (row A11) — sort plaques, heart/smiley states, UnknownRel, SocialInfoPopup, ThumbBack all unmounted; only the tab button + RelBars rating strip are original.
- **Inventory subpanel**: only the 3 filter buttons (4112–4114) + Scroll arrows original; cells are modern; STR 241 tooltips unwired; kIconFrame 4200 unmounted, kIconTemp absent from disk. Partial gap.
- **ReportCard**: no dedicated art family in any RT template; the gap is Live 139 strings + the decoded cWinSubpanelReportCard behavior (reuses Job bar art per the language-conditional decode in r132).
- **Speech balloons**: Res_Other 9000/9001, engine Product::GenerateSpeechIcons — unported. The port's balloon *frames* come from Sprites.iff (headline renderer), not UIGraphics.
- **kHelpBtn / kCameraBtn**: both canon-but-unported (r142 residuals, still zero code references); kCameraBtn drags the whole 11-member camera family + STR 140 with it.
- **Tutorial UI**: canon = TutHigh.bmp (4980) + STR 166 Help (94) + engine Tutorial fn family + Tutorial.FAM; no arrow/overlay art exists elsewhere in UIGraphics (verified by manifest scan).
- **In-game notification/event art**: TragedyMask (9005) + FriendCountDlg (162) + cWinPictureDialog icon path — no port surface; also the RichEdith "blocking help dialog" latch (r157) shows the engine's help dialog already fires in the port's VM, so the art/strings gap is live-reachable.
- **Never-mentioned-in-any-round families**: TallSubPanel.TGA (4918), BudgetBack/BudgetOK (4965/4966), Scrap* (1200–1206), PhoneBook (80/81), JobIconMulti pair (4902/4905), SpeechMedium/Large, TragedyMask, ViewMenu item sheets (820–823), Nhood_balloon_shadow.tga, webcam logo stamps (4400/4401), ExploreAnchor — these are the highest-value "no doc ever names it" finds.