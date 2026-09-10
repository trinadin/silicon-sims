# Census: remaining non-original UI surfaces in the macOS client

Scope walked: `Client/Simitone/Simitone.Client/UI/**` (Panels, Controls, CAS, LiveSubpanels, LotControls, WorldUI, Screens, Model), `Client/Simitone/Simitone.Client/GameController.cs`, `FreeSO/TSOClient/FSO.UI` (UILayer tooltip box, UIUtils, UITextBox), PARITY.md rows 114–158. "Desktop" = `!FSOEnvironment.SoftwareKeyboard` (the macOS default); "touch" = mobile composition. Canon references are the PARITY/round docs.

## A. Screens / backdrops

1. **In-game base gradient (load_static_bg.png) under the neighborhood view** — `UI/Screens/TS1GameScreen.cs:199` creates `UISimitoneBg()` with `loadModernFallback=true`; the texture loads at `UI/Screens/LoadingGameScreen.cs:369`. `Bg.Visible=false` only inside a lot (`TS1GameScreen.cs:102`), so at neighborhood zoom the modern green gradient is the backdrop behind the neighborhood panel. Canon: none needed (original paints the screen art full-bleed; the R118 residual named exactly this as "post-loader scope"). **Both paths.** Base.
2. **Neighborhood screen surround** — `UI/Panels/UINeighbourhoodSelectionPanel.cs:253-257` draws `ngbh_outline.png` (port-authored 9-slice border, 24px slices) around every neighborhood screen. Canon exists on disk: Res_Nbhd navbar buttons (212x52, R142 art extract) + the R143-decoded 14-button toolbar law / banner-is-button / lot popups / kLargeMask surround — parked as "R144 material", never landed. **Both.** Base (see #3 for expansion buttons).
3. **Neighborhood side navigation buttons** — `UI/Panels/UINeighbourhoodSwitcher.cs:35-47`: `ngbh_cas.png`, `ngbh_back.png` (base) plus `ngbh_downt.png` (Hot Date), `ngbh_vacat.png` (Vacation), `ngbh_studio.png` (Superstar), `ngbh_magic.png` (Makin' Magic) — all port-authored PNGs on UIElasticButton. Canon: Res_Nbhd navbar members (212x52) extracted in R142, unmounted. **Both.** Base + EXPANSION (the four expansion buttons).
4. **Neighborhood→CAS/downtown transition** — `UI/Panels/UITransDialog.cs:55-56` uses `trans_{type}.png` + UIDiagonalStripe wipe; the Simitone transition effect. Canon: none named in docs (engine transition effect undecoded). **Both.** Base.
5. **Neighborhood title / hover caption placement** — title at (12,8) (`UINeighbourhoodSelectionPanel.cs:328`), hover lot caption at (14,566) with port fallback "Lot N" (`:402-405`). Strings/fonts original; **placement is disclosed interpretation** (engine code, no data). **Both.** Base.
6. **Neighborhood cheat bar** — `UINeighbourhoodSelectionPanel.cs:446` builds a bare FSO `UITextBox` (TSO `dialog_textboxbackground`, which resolves to nothing on TS1 data → white box). Original: engine Ctrl+Shift+C bar, no art member named. **Both.** Base.

## B. CAS

7. **Vita viewport camera** — `UI/Panels/CAS/UIOriginalCAS.cs:478-481` cuts the hole and shows ONE full sim; original cycles head/body independently with engine keyframes. Canon: keyframes undecoded (R143 "first-cut" residual). **Desktop.** Base.
8. **CAS member portraits / webcams** — 3D headshots via UIIconCache (`UIOriginalPeopleChrome.cs:199-260` webcam; family slots use 3D thumbs) instead of the engine's GetPictureBuffer 4x3 sheets; "webcam draw internals an r144 residual". Canon: sheets exist in the original save pipeline, not on disk as UI members. **Desktop.** Base.
9. **CAS system-button hover + height 62 interpreted; per-element .ffn font assignment disclosed; 4-state frame order = standing R122 convention; hidden "body strings cheat" field decoded not ported** (R143 residuals; `UIOriginalCAS.cs` `Sheet()`/`UIOriginalSystemButton`). **Desktop.** Base.
10. **Mobile CAS panels** — `UISimCASPanel.cs` (14 modern PNGs incl. `cas_*.png`, `plumb_*.png` personality +/-), `UIFamilyCASPanel`, `UIFamiliesCASPanel`, `TS1CASScreen.cs:517-523` `btn_back.png`/`btn_accept.png`/`btn_movein.png` — reachable only when `TS1CASScreen.Original == false` (ctor returns before building them on desktop, `TS1CASScreen.cs:468-505`). Strings where original exist (R116); art/layout mobile. **Touch.** Base.

## C. Lot HUD — desktop control panel / toolbar

11. **kHelpBtn + kCameraBtn unported** — `UI/Panels/Desktop/UIDesktopUCP.cs` mounts the full engine anchor set except these two (canon anchors (5,164)/(175,95); strings 138[23] "Help", [19] "Camera Mode - F4" pinned-but-unused since R117). **Desktop.** Base.
12. **Per-mode patch compose position** — patches mounted at `(0,0)` full-plate (`UIDesktopUCP.cs:109-112, 259-268`); R142 residual "mode-patch compose positions (paint code)" — engine paint law undecoded. **Desktop.** Base.
13. **Floor readout ordinals + offset** — `FloorNames = {"1st".."5th"}` hardcoded (`UIDesktopUCP.cs:86-93`, touch twin at `UIMainPanel.cs:973-984`); R115's NOT-FOUND list says no corpus strings exist ("floor ordinals"); X offset beside Lev pair disclosed. **Desktop (UCP) + touch (panel).** Base.
14. **Clock AM/PM suffix** — hardcoded `"AM"/"PM"` (`UIMainPanel.cs:1004-1006`); canon 138[21]/[22] carries them (R117 "mapped-UNUSED"). **Desktop+touch.** Base.
15. **Modern select-Sim overlay reachable on desktop** — `UISwitchAvatarPanel.cs:34` (`pswitch_bg.png` mobile chrome, elastic round buttons) is mounted by `MainPanel.ShowSelect()` (`UIMainPanel.cs:1194`), which the desktop UCP Live button still reaches when the panel is closed in live mode (`UISimitoneFrontend.cs:193-214` falls to `StartSelect()`). Canon: none (original has no such overlay; selection is the webcam strip). **Desktop (edge) + touch.** Base.
16. **In-lot cheat textbox** — `UICheatTextbox.cs:61-65`: flat teal `Texture2D` background box, FSO UITextBox; port-only cheats incl. `weather`, `motherlode`. Original bar is engine-drawn; no member. **Both.** Base.

## D. Live-mode people panels

17. **Relationship subpanel — mobile art on the DESKTOP tab column** — tab 3 of `UIOriginalPeopleChrome.TabCategory` (`UIOriginalPeopleChrome.cs:49`) dispatches to `UIRelationshipSubpanel.cs`: `rel_friend/rel_fam/rel_all/rel_fame.png` buttons (`:46-58`), `UITouchScroll` with `scroll_edge_l/r.png` (`:37-41`, `UITouchScroll.cs:41-42`), rows = `inv_item.png` tinted mobile card + two 0.5-scale UIMotiveBars + UILabel (`UIRelationshipDisplay`, `:129-186`). Canon exists: kRelBars 4802 63x26 + Off backdrops (R132-pinned), cWinRelationship::TSPaint mapped; sort tooltips STR# 240/241 on disk, unwired. **Desktop + touch.** Base; the **Fame sort button is Superstar — EXPANSION**.
18. **ReportCard / CatSkills / DogSkills / Fame subpanels not ported at all** — no files; engine subpanels mapped at R131 (cWinSubpanelHouse/Job/ReportCard cluster 0x2a0000+). Kids get the adult Job panel (no child branch, `UIJobSubpanel.cs` — confirmed). ReportCard = base game; CatSkills/DogSkills = Unleashed (EXPANSION); Fame = Superstar (EXPANSION, FameSubBars 4822/4823 pinned-but-unused). **Desktop+touch.**
19. **Job subpanel chrome** — `blank_blue.png` CareerButton (`UIJobSubpanel.cs:97`); Performance gauge is a UIMotiveBar (Greenbars 3-slice reused, `:82-84`) rather than JobSubBars 4901/4920 (canon pinned R132); skill pips are `skill.png` mobile texture with UIStyle tints (`UISkillDisplay.cs:23, 49-60`) — no canon skill-pip member identified in docs. Strings/twins/rating bar original. **Desktop+touch.** Base.
20. **Motive trend arrows** — `motive_arrow.png` port chevrons (`UIValueBar.cs:153, 224-236`); canon: engine cWinDeltaMeter arrows (kGreenbars/kRedbars reveal law, R144 "not ported this round"). **Desktop+touch.** Base.
21. **Follow-Sim button hidden** — `UIMotiveSubpanel.cs:92-95` mounts kTrackingTarget then `Visible=false` ("r144: cWinLivePopup backdrop, not a toolbar button"), superseding R132's live port; follow-sim now has no visible toggle. Conflicting canon reading between R131 (follow button) and R144 — unresolved. **Desktop+touch.** Base.
22. **Relationship/inventory sort-button tooltips unwired** — canon 240/241 exists; port buttons assign none (R117 "nothing to swap — wiring them = new UI"). **Desktop+touch.** Base.
23. **Gift (Inventory) panel behavior** — `UIOriginalInterestGiftSubpanels.cs:265` "gift-cell click behavior (give/select) is not [ported] — display-only". Art/strings original. **Desktop.** Base (magic/ingredient gift cats are MM — EXPANSION, touch side).

## E. Buy / Build catalog

24. **Catalog hover popup chrome** — `UIOriginalBandControls.cs:291-301` (`UIOriginalCatalogPopup.Draw`): flat `0x10,0x10,0x2c` white-px rectangle + 1px divider; `Slide` field exists but unused; no can't-afford red tint; icon at fit-scale not engine 1/3; name-price at +10 not +4 (R148 residuals). R148 verdict: engine res 3006/3008 never loaded — flat panel is the lawful approximation, exact clear color future decode. **Desktop.** Base.
25. **STR# 160 [14..19] never rendered** — "Can only be used by adults/kids/pets/dogs/cats", "Group Activity" have no corpus encoding (R124 negative-space proof); `UIQueryPanel` renders [0..13] only. **Desktop.** Base (pets/dogs/cats entries are Unleashed-flavored — EXPANSION once a source appears).
26. **Build undo/redo disabled** — `UIOriginalArchChrome.cs:87-93` mounts both on canon art, Disabled (no VM architecture-undo machinery; engine ArchUndo/ArchRedo @0x20ee60/0x2edc0). Hand tool selection-only (R145). **Desktop.** Base.
27. **Build subtool row keeps price sort; buy next-arrow x formula ambiguous; pair-sheet width halving not applied; main-state expansion plaque highlight a port addition; hover frame mapping art-inferred** (R145/R148 residuals, `UIOriginalBuyChrome.cs`/`UIBuyBrowsePanel.cs`). **Desktop.** Base.
28. **"No items found" label** — `UIBuyBrowsePanel.cs:495` hardcoded English, modern UILabel; not in corpus (R115 NOT-FOUND "search strings" class). **Desktop+touch.** Base.
29. **Absent-from-disk members backing port chrome** — BuyBack (kCatalogBck 48), OptionsBack (kBkgOptions), DisposeBack (kDisposeBack), IconTemp, LiveBack (kLiveBack) have no members on this disk (R121/122/124/131/148 enumerations) — buy/options backgrounds stay PanelBack/port chrome, pickup strip unpainted. "No canon on disk" per docs. **Desktop+touch.** Base.

## F. Dialogs / shared controls

30. **Dialog chrome residuals** — GenDlg nine-slice border widths 10/10/10 measured not recovered (`UIOriginalDialogChrome.cs:34-38`); body wrap 420px, title inset (16,8), 16px line height = disclosed interpretations (`UIMobileAlert.cs:326-331, 348-349, 380`). **Desktop.** Base.
31. **UIBigButton mobile textures inside dialog-family panels** — `UIBigButton.cs:38` still binds `button.png`/`greenbutton.png`; used on desktop by `UIHouseSelectPanel` (EnterLot/More/option rows, `:315-349`), `UICallNeighborAlert.cs:96-104`, `UISelectSkinAlert.cs:138-147`. Canon: WinBtn 260x33 system button (mounted for UIMobileAlert's own buttons only). **Desktop+touch.** Base.
32. **Lot-query panel chrome + captions** — `UIHouseSelectPanel.cs:92-99` two UIDiagonalStripes (Simitone chrome, no gate); hardcoded captions "Enter Lot"/"Move In"/"More"/"Bulldoze"/"Evict"/"Rezone"/"Export"/"Back" (`:315-345`); family strip = round `pswitch_icon_*` buttons (`UIHouseFamilyList`, `UIAvatarSelectButton`). Canon: 132 MoveInModeStrs (480 entries) is the unverified candidate for the captions; member-strip law 45x68 (R143) unmounted. Note bulldoze/rezone/evict are themselves a [GAP] feature. **Both.** Base.
33. **"Call Neighbour" heading** — port-authored, no verbatim original (R115 disclosed) — `UICallNeighborAlert.cs:35`. **Both.** Base.
34. **Custom Content Warning dialog text** — `GameController.cs:160-190` fully port-authored English ("Custom Content Warning", "Common causes:…", "…and N more"). No canon named. **Both.** Base.
35. **Tooltip BOX** — `FreeSO/TSOClient/FSO.UI/UILayer.cs:346-393`: white rectangle + 1px border, port layout (290 wrap, 13px lines). Glyphs original since R119; text color: green (31,124,31) via `UIUtils.cs:205/225`, but `UILotControl.ShowErrorTooltip/ShowReasonTooltip` force `Color.Black` (`UILotControl.cs:542, 632`) — object-hover/error tooltip color is not the pinned catalog-green. R125 residual "original hover background not established". **Both.** Base.
36. **STR# 159 error ids beyond canon** — `UILotControl.cs:490-499` passes ids 16/21/22/24/27 (TSO-era disable flags) to the 9-entry TS1 table; most paths unreachable in TS1, ObjectLimitExceeded (24) can fire and will miss. **Both.** Base.
37. **Dead mobile control** — `UITouchListbox.cs` (`cat_btn_base.png` outline) has no callers. Not reachable. **Dead code.**

## G. In-lot overlays

38. **People pie (PieFace1) unported** — `UIPieMenu.cs:148-172` renders a 3D sim head in the pie center (`initSimHead`); PieFace1.bmp 201x201 r150 is on disk (RT member, R142) and never mounted (only `pieface1` TSO id in FSO.UI/`UIFileIDs.cs:200`). Interaction pie itself is original art/palette/radius. **Both.** Base.
39. **Money headline** — `UIMoneyHeadline.cs:44` `money_bg.png` 3-slice mobile pill behind §-text. Canon: original headline chrome = speech-balloon family in Res_Other (R142 extract); note `UIHeadlineRenderer.cs:48-66` already mounts ORIGINAL Sprites.iff balloon sprites for normal headlines — only the money variant is port-styled. **Both.** Base.
40. **Interaction queue** — desktop = bare icons + original QueueCancel (R142, `UIInteraction.cs`), but queue strip position/composition and tweens are port-authored; original queue geometry not decoded in docs. Touch keeps `int_big_bg/int_small_bg/int_big_sel.png` pills (`UIInteraction.cs:71-92`). **Both (touch = mobile art).** Base.
41. **Pickup strip** — desktop never paints (`UIPickupPanel.cs:155-165`, R144 gate) — original pickup chrome (DisposeBack family) absent from disk; touch keeps mobile strip + `cat_cancel.png`. **Touch paints; desktop = feature-quiet.** Base.

## H. Touch-path-only modern surfaces (not reachable on desktop — census for completeness)

42. Frontend chrome: `cut_btn_*.png`/`cut_bg.png` cutaway button+panel (`UISimitoneFrontend.cs:46-63`, `UICutawayPanel`), `UIClockPanel` (`clockbg/clockinbg.png`), `UIMoneyPanel` (`money_bg.png`), `panel_expand.png`, `UIModeSwitcher`/`UILiveButton` (`mode_live/buy/build/options.png`, `speedbtn_*.png` mobile pills) — `UISimitoneFrontend.cs:44-96`.
43. MainPanel touch controls: `level_up/down.png`, `divider.png`, `panel_div.png`, `panel_hide.png`, catalog search box + "Search selected category…" placeholder, mobile white-px bg (`UIMainPanel.cs:368-478, 947-964`).
44. Touch helpers: `UILotControlTouchHelper` (`touch_*.png`), `UIArchTouchHelper`, `UIRotationAnimation` (`rot_seg/rot_arrow_*.png`).
45. Touch HUD composition laws declared "by design" mobile: R122 catalog switcher/subsort row and R146 "mobile (touch) keeps the R122 composition"; R141 "mobile HUD otherwise untouched"; R142 toolbar speed cluster stays on touch (`UIMainPanel.cs:325-366`).
All **touch**, base unless noted.

## I. Interactions/strings with no corpus (port-authored, disclosed)

46. "Eyedropper Tool (E)" tooltip (port tool, `UIDesktopUCP.cs:389` area); "Gender"/"Age"/"Skin Color" CAS group labels (R116 — touch panels now); rotate CW/CCW + floor-arrow pairings ours (R117); salary "Salary: §n (h-h)" composition (R115); house score values 0 on proven-absent ladders (R134/135); interests re-roll per session; personality-allocation mobile +/- plumb buttons (touch).

## Stale PARITY clauses (closed by later rounds but still readable as open)

- **Current gaps line 20** "[GAP] Personality-allocation UI ('design a person')" — closed on desktop by R143 (LED strips, pool-25 law live in `UIOriginalCAS.cs:409-470, 629-643`); only the pet naming/skill-string half (Unleashed, EXPANSION) is still open, and the touch CAS remains mobile.
- R118 residuals "MSDF remains the engine-wide default" (closed R119) and "macOS app-menu name stays Simitone" (closed R156 uibrand).
- R122 residual "build-mode subcategory buttons still port icons" (closed R126).
- R131 residuals "exact easing constants / TOC-20400 offsets" (closed R135), "level-view toolbar" (mounted on plate R142), "TrackingTarget surface" (ported R132 — then hidden by R144; live state is "hidden", see #21).
- R142 residual "toolbar's own row (cWinPeople/cWinArch children, undecoded)" (decoded+ported R144/R145).
- R130 residual "STR# 147 page titles have no port panel-title surface" (closed R131 pager pages).
- R145 residuals "expansion buy bands stay mobile" (closed R146), "interest values TSO-packed, bars at 0" (closed R150-152), "popup click-not-hover + rating lines" (closed R148/R124).
- R146 residuals "plaque enable matrices" (closed R149), far-zoom black terrain (closed R147).
- R125 "[0]/[8]/[3] interpretations pending engine round" (settled R128/R129).
- R142 "CAS screen + neighborhood chrome = next-round targets": CAS half closed (R143); only the neighborhood chrome half is still open (#2/#3).

Genuinely-open engine-decode residuals I can confirm in code or docs: GenDlg border widths, dialog wrap/title inset, Vita keyframes, webcam draw internals, cWinDeltaMeter arrows, catalog popup clear color, sway-amplitude model (R110 — neighborhood balloon), SubWorlds/RestoreSurroundings dormant (R147), camera-cage exact law (R147).