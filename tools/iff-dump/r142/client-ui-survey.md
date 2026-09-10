# r142 — Client UI survey: remaining Simitone/mobile artifacts

Read-only survey of `/Users/nathannoom/Developer/Games/The Sims/simitone-fork/Client/Simitone/Simitone.Client/`
(UI/Screens, UI/Panels/**, UI/Controls, UI/Model, GameController.cs, AutotestRunner.cs).
Goal: prioritized defect list for the next original-art port round.

Context used:
- Original art reaches the UI through `UIOriginal.` (UIGraphics.far resolver) and the
  `OriginalGlyphFont`/`UIOriginalText` twins. The mobile-era art pack lives in
  `Content/uigraphics/` (referenced via `Content.Get().CustomUI`) — **any
  `CustomUI.Get("*.png")` that is not an `orig_*`/`rating_*` png twin is Simitone/mobile art**.
- Desktop is the primary target: `TS1GameScreen.Desktop = !FSOEnvironment.SoftwareKeyboard`
  is true on desktop builds, so panels created only in `!Desktop` branches are listed as
  "mobile-path" (invisible on desktop, still non-original code).

Already-known-done (NOT re-flagged): UIDesktopUCP plate, UIMainPanel toolbar
(PanelBack/tabs/speed cluster/twins), UIMotiveSubpanel grid, UILiveButton, UIOriginal resolver.

---

## Section 1 — UI surface inventory

### Screens (UI/Screens/)

| Class | Surface | Art status |
|---|---|---|
| `LoadingScreen` (+`UISimitoneBg`, `UIOriginalLoadBar`) | boot/load screen | ORIGINAL — Other\setup.bmp from frame one, original splash strings (.ffn), engine-drawn bar (R88/R111/R118). Modern fallback gradient deleted. |
| `TS1GameScreen` | game host (neighborhood + lot) | Mixed — lot HUD via `UISimitoneFrontend`; **all confirm dialogs are mobile `UIMobileAlert`**; `Bg = new UISimitoneBg()` (default `loadModernFallback=true`) leaves the mobile `load_static_bg.png` gradient as the neighborhood letterbox backdrop (TS1GameScreen.cs:199). |
| `TS1CASScreen` | Create-A-Sim / family select | **MOBILE** — btn_back/btn_accept/btn_movein pngs, CAS panels all mobile art (see CAS rows). Strings are original (uicas pins UIText). |

### Panels (UI/Panels/**)

| Class | Surface | Art status |
|---|---|---|
| `Desktop/UIDesktopUCP` | control-panel island (bottom-left) | ORIGINAL (R141) — UniversalBack plate, 4-state mode sheets, camera diamond, wall row, patches. Fallbacks to mobile pngs only if IFF mount fails. |
| `UISimitoneFrontend` | in-lot HUD container | Wiring only; mounts desktop UCP + MainPanel (desktop) or clock/money/mode switcher (mobile-path). |
| `UIMainPanel` | toolbar across bottom | ORIGINAL on desktop (PanelBack, pause/speed cluster, .ffn time/money twins). Mobile-path diagonal chrome + floor buttons + hide button + search box are mobile-only branches. |
| `UIMotiveSubpanel` | Live/needs tab | ORIGINAL (gauge, Green/Redbars, TrackingTarget, .ffn name twins). |
| `UIJobSubpanel` | Live/job tab | Mostly original (JobSubBars rating bar R132, .ffn twins). Mobile remnants: `blank_blue.png` CareerButton (line 97), `skill.png` pips. |
| `UIPersonalitySubpanel` | Live/personality tab | Original strings + twins; mobile `skill.png` pips. |
| `UIRelationshipSubpanel` (+`UIRelationshipDisplay`) | Live/relationship tab | **MOBILE** — rel_friend/fam/all/fame.png buttons, `inv_item.png` cell bg tinted Simitone blue, modern labels. |
| `UIInventorySubpanel` (+`UIInventoryDisplay`) | Live/inventory tab | **MOBILE** — inv_mag/ing/other_btn.png, `inv_item.png` bg, modern labels. |
| `UIHouseSubpanel` | Live/house tab | ORIGINAL bars (HouseSubBars R132) + original strings; labels still modern UILabel (no twins). |
| `UIOriginalOptionsPanel` | Options screen | ORIGINAL art/strings/positions (R121); captions are modern-font UILabels. |
| `UIBuyBrowsePanel` | Buy/Build catalog | Mostly ORIGINAL (buy plaques R122, build tool plaques R126, roof pager R130/131, DT sorts). Mobile-era remnants: modern caption labels under original plaques, touch-scroll behavior, `NoResultsLabel`. |
| `Catalog/UICatalogItem` | catalog item cell | ORIGINAL frames (ThumbTemplate/ThumbTemplate1Frame R122, RoofPatternTemplate R130); mobile fallback path (`pswitch_icon_bg.png` tinted) only if frames missing; price label modern font. |
| `Catalog/UIOriginalRoofResProvider`, `UIOriginalTerrainResProvider`, `UIRoofPitcher` | build res providers | ORIGINAL members. |
| `UIButtonSubpanel` | generic button row | MOBILE (`cat_*.png`); **dead code** — no instantiation remains. |
| `UIClockPanel`, `UIMoneyPanel`, `UIModeSwitcher`, `UICutawayPanel` | clock/speed cluster (mobile), funds (mobile), mode dial (mobile), wall-cutaway pop (mobile) | **MOBILE-path only** (created in `!Desktop` branches; clock/money text use .ffn twins). |
| `UIMobileAlert` (+ base `UIMobileDialog` in Controls) | every modal dialog | **MOBILE** — full-screen diagonal stripe wipe, `dialog_title_grad.png` title band, `button.png`/`greenbutton.png` big buttons. Titles/bodies are original glyphs (R112). |
| `UITransDialog` | screen transition | **MOBILE** — stripe wipe + `trans_*.png` splash image (CAS entry/exit). |
| `UIHouseSelectPanel` | neighborhood lot query | Text on original glyphs (R113) over **MOBILE chrome** (half-screen stripes, UIBigButton row incl. hardcoded English captions). |
| `UINeighborhoodSelectionPanel` | neighborhood screen | Screens + layers ORIGINAL (R89–R96, cars R91, clouds/balloon R92/R104, nessie R93); **mobile `ngbh_outline.png` 9-slice frame** around the 800x600 art (line 253). |
| `UINeighbourhoodSwitcher` | neighborhood toolbar | **MOBILE** — `ngbh_cas/back/downt/vacat/studio/magic.png` elastic edge buttons. |
| `UIPieMenu` | click-a-Sim radial menu | FSO-STYLE — `TextureGenerator.GetPieBG/GetPieButtonImg` generated art + modern font (`GameFacade.MainFont`); Sim head render is faithful in spirit. |
| `UIQueryPanel` | catalog/object description popup | ORIGINAL (PopupInfo plaque + tiles, R124). |
| `UIPickupPanel` | object-in-hand bar (buy mode) | **MOBILE** — `cat_cancel.png`, modern title/subtext labels. |
| `UIInteractionQueue` (+ `UIInteraction` control) | top-left action queue | **MOBILE** — `int_big_bg/int_small_bg/int_big_sel/int_cancel.png` tinted Simitone blue + yellow rim. |
| `UIObjectHolder` | placement/drag logic | No chrome (logic only). |
| `UICheatTextbox` | in-lot cheat bar | Plain modern UITextBox, flat teal fill (67,93,90). |
| `UISwitchAvatarPanel` (+`UIAvatarSelectButton`) | Sim selector (People mode) | **MOBILE** — `pswitch_bg/icon_bg/icon_sel.png`, blue tint (104,164,184). Desktop-visible. |
| `UIRotationAnimation` | rotation arrows overlay | MOBILE-path only (touch helper). |
| `LotControls/UICallNeighborAlert` | phone "Call Neighbor" dialog | **MOBILE** dialog base + touch lists + UIBigButtons (original button strings only). |
| `LotControls/UISelectSkinAlert` | clothes/pet selection dialog | **MOBILE** dialog base (original title strings R115). |
| `LotControls/UIArchTouchHelper`, `UILotControlTouchHelper` | touch build tools / touch lot control | MOBILE-path only (`touch_*.png`). |
| `WorldUI/UIHeadlineRenderer` | thought/speech balloons | ORIGINAL Sprites.iff frames (skill strings built but never drawn — dead code). |
| `WorldUI/UIMoneyHeadline` | money balloon over Sims | **MOBILE** — `money_bg.png` 3-slice + modern vector font text. |
| `CAS/UIFamiliesCASPanel` | family select in CAS | MOBILE (btn_deletefam/btn_createfam, diagonal stripes, cas_new_plus). |
| `CAS/UIFamilyCASPanel` | family edit in CAS | MOBILE (diagonal stripes, UIAvatarSelectButton). |
| `CAS/UIFamiliesCASPanel`/`UIFamilyCASItem` | family list/cards | MOBILE. |
| `CAS/UISimCASPanel` | Sim tabs (look/personality/bio) | MOBILE (cas_sim/per/bio/male/female/... pngs, `bar.png` personality bars). |

### Controls (UI/Controls/)

| Class | Status |
|---|---|
| `UIOriginalText`, `OriginalVectorFont` (+`OriginalGlyphFont` in FSO.Client) | ORIGINAL glyph rendering. |
| `UIOriginalLiveGauge`, `UIOriginalRatingBar`, `UIOriginalTrackButton` | ORIGINAL art + engine laws. |
| `UIStencilButton`, `UITwoStateButton` | Texture passthrough (used with original art in the ported surfaces). |
| `UIMotiveBar` (in UIValueBar.cs) | ORIGINAL fills (Green/Redbars R85/R123); mobile `motive_arrow.png` arrow; mobile `motive_bg.png` only as fallback base. |
| `UIValueBar` | Procedural green/red (used only via UIMotiveBar subclass today). |
| `UISkillDisplay` | MOBILE `skill.png` pips with Simitone palette tints. |
| `UICatButton` | Original art passthrough + **mobile `cat_btn_base.png` round plaque drawn behind it whenever `OriginalStyle == false`** (LIVE/BUILD/OPTIONS main plaque). |
| `UICategorySwitcher` (+`UIVertGrad`) | Original plaque art IFF-first, but the expanding column chrome is **mobile**: `UIDiagonalStripe` + rotated `dialog_title_grad.png`. Desktop-visible. |
| `UIDiagonalStripe` | Mobile chrome primitive (stripe wipe). |
| `UIMobileDialog` | Mobile dialog base (see P0-1). |
| `UIBigButton` | Mobile `button.png`/`greenbutton.png` texture; captions already original glyphs (R112). |
| `UIElasticButton` | Mobile bouncy-scale behavior + mobile textures at call sites. |
| `UIInteraction` | MOBILE queue icons (see P1-5). |
| `UITouchScroll`, `UITouchStringList` | Mobile touch-scroll machinery; `UITouchStringList` draws `cat_btn_base.png` 9-slice. |

### Model (UI/Model/)
`UIOriginal` (resolver), `UIStyle` (palette already aligned to uipal navy/cyan),
`OriginalHouseStats`/`OriginalLiveStrings`/`OriginalRouteHistory` (original data),
`UIIconCache` — no chrome.

---

## Section 2 — Prioritized defect list

### P0 — most visible / breaks the original look everywhere

**P0-1. Every modal dialog is the mobile full-screen stripe wipe.**
`UI/Controls/UIMobileDialog.cs:94-118` — `BackStripe` = full-screen `UIDiagonalStripe`
in `new Color(0,70,140)*0.33f` with the `diag.png` mobile texture; `FrontStripe` =
black 80% wipe; `TitleBg` = `dialog_title_grad.png` stretched screen-wide; buttons are
`UIBigButton` (`button.png`/`greenbutton.png` mobile rounded pngs, UIBigButton.cs:38).
TS1 dialogs are windowed dialogs on original UIGraphics bitmaps.
Visible: quit (TS1GameScreen.cs:975), save/return-to-neighborhood (:1002), move-in
confirm (:242), evict confirm (UIHouseSelectPanel.cs:417), **every VM dialog — Sim
messages, event popups, yes/no choices** (UILotControl.cs:302 and :1091 —
`vm.SignalDialog` → `UIMobileAlert`), load errors (TS1GameScreen.cs:735), CAS confirms
(TS1CASScreen.cs:566/596/777), phone/clothes/pet dialogs (P1-10/11), debug F8.
This is the single most pervasive non-original surface in the client.

**P0-2. The whole CAS screen is Simitone mobile art.**
`UI/Screens/TS1CASScreen.cs:455-459,637-638` (`btn_back/btn_accept/btn_movein.png`);
`UI/Panels/CAS/UISimCASPanel.cs:67-154,398` (cas_sim/per/bio/male/female/adult/child/
skin/rand pngs, `bar.png` personality bars); `UIFamilyCASPanel.cs:76,100,216`
(diagonal stripes, `cas_new_plus.png`); `UIFamiliesCASPanel.cs:65-70`
(btn_deletefam/btn_createfam). UIGraphics.far ships the original CAS set
(CreateACharBack.bmp + CAS button family); none of it is mounted. Strings are already
original (uicas gate) — the art layer is entirely missing.
Visible: the entire Create-A-Sim / family select flow.

**P0-3. Neighborhood chrome: mobile edge buttons + mobile frame + mobile gradient.**
- `UI/Panels/UINeighbourhoodSwitcher.cs:35-47,59-84` — the neighborhood "toolbar" is
  six 96x96 mobile `ngbh_*.png` elastic buttons glued to the left/right screen edges
  (CAS, back, Downtown, Vacation, Studiotown, Magictown). TS1 has its own neighborhood
  toolbar art in UIGraphics (ngbh family).
- `UI/Panels/UINeighbourhoodSelectionPanel.cs:253-257` — `ngbh_outline.png` 9-slice
  mobile frame drawn around the 800x600 original screen.
- `UI/Screens/TS1GameScreen.cs:199` + `UI/Screens/LoadingGameScreen.cs:363-373` —
  `new UISimitoneBg()` with default `loadModernFallback=true` leaves the mobile
  `load_static_bg.png` gradient as the persistent letterbox behind the 4:3 original
  screens (visible on any non-4:3 desktop window; also flashes during lot load).
Visible: every visit to the neighborhood screen (i.e. every game session start).

### P1 — clearly wrong look on desktop-visible surfaces

**P1-4. Pie menu is generated art + modern font.**
`UI/Panels/UIPieMenu.cs:70` (`TextureGenerator.GetPieBG`), :222/:265/:294
(`GetPieButtonImg`), :56-64 (modern `GameFacade.MainFont`, colors A5C3D6/00FFFF).
The original uses its pie sprite family and original glyphs. Desktop-visible every
time the player clicks an object/Sim in live mode.

**P1-5. Interaction queue icons are mobile pngs.**
`UI/Controls/UIInteraction.cs:55,63-64,78` — `int_cancel/int_big_sel/int_big_bg/
int_small_bg.png` tinted Simitone blue `(104,164,184)`; :137 draws a `Color.Yellow`
"rim". `UI/Panels/UIInteractionQueue.cs:243-244` port-authored positions.
Visible: top-left queue whenever a Sim has queued interactions (all live-mode play).

**P1-6. Relationship tab fully mobile.**
`UI/Panels/LiveSubpanels/UIRelationshipSubpanel.cs:43-58` (rel_friend/fam/all/fame.png),
`UIRelationshipDisplay` :149/:184 (`inv_item.png` tinted blue, modern name labels).
Visible: Live mode → Relationships tab.

**P1-7. Inventory tab fully mobile.**
`UI/Panels/LiveSubpanels/UIInventorySubpanel.cs:48-58` (inv_mag/ing/other_btn.png),
`UIInventoryDisplay` :145/:158. Visible: Live mode → Inventory tab.

**P1-8. Sim-switch (avatar select) panel mobile.**
`UI/Panels/UISwitchAvatarPanel.cs:28,65-66,79-91` — `pswitch_bg/icon_bg/icon_sel.png`,
blue tint, modern name text. Visible: desktop, clicking the People mode button when a
Sim is selected (MainPanel.ShowSelect).

**P1-9. Pickup panel mobile.**
`UI/Panels/UIPickupPanel.cs:52` (`cat_cancel.png`), :38-50 modern title/subtext.
Visible: buy/build mode whenever an object is picked up (SetSubpanelPickup swap-in).

**P1-10. Phone dialog on mobile chrome.**
`UI/Panels/LotControls/UICallNeighborAlert.cs:17-41` — `UIMobileDialog` base (full
stripe wipe) + `UITouchStringList` lists (cat_btn_base 9-slice) + UIBigButtons.
TS1's phone book is an original dialog. Visible: Sim uses phone → Call.

**P1-11. Clothes/pet selection dialog on mobile chrome.**
`UI/Panels/LotControls/UISelectSkinAlert.cs:29-62` — same mobile dialog base,
SetHeight(600) full-screen. Visible: "Change to Formal/Swimwear", pet adoption, etc.

**P1-12. Category switcher expand chrome + round plaque behind original art.**
- `UI/Controls/UICategorySwitcher.cs:59-65` — the expanding category column is a
  `UIDiagonalStripe` (navy, diagonal-cut edge) with `UIVertGrad`
  (`dialog_title_grad.png` rotated, :167-181) — mobile chrome, desktop-visible every
  time the main plaque opens the tab column.
- `UI/Controls/UICatButton.cs:74-75` — when `OriginalStyle == false` (LIVE, BUILD,
  OPTIONS main plaque — it is only true in BUY, UIMainPanel.cs:464) the mobile
  `cat_btn_base.png` round plaque is drawn behind/around the original art at a -5px
  offset — overlapping chrome that the original art does not have.

**P1-13. Buy/build subsort rows duplicate plaque text in the modern font.**
`UI/Panels/LiveSubpanels/UIBuyBrowsePanel.cs:1084-1094` — every subsort slot draws a
wrapped UILabel caption (modern font, y=106) under the original plaque, but the R122
canon states the plaque art already carries the visible text (the strings are
tooltips). Result: doubled, wrong-font labels under every original plaque.
Visible: buy mode (function + room pages), build mode, DT subtowns — any subsort row.

**P1-14. Money balloon mobile.**
`UI/Panels/WorldUI/UIMoneyHeadline.cs:42,55-64` — `money_bg.png` mobile 3-slice under
modern vector-font text. Visible: any § change balloon over a Sim (buy, wages, bills).

**P1-15. Lot-query panel chrome mobile.**
`UI/Panels/UIHouseSelectPanel.cs:102-110` (two `UIDiagonalStripe` half-screen shapes),
:315-357 UIBigButton row (`Enter Lot`/`Move In` :316, `More` :324, and the hardcoded
English `Bulldoze/Rezone/Export/Back` set :337-343 — not read from UIText).
Text is original glyphs (R113) but all container/button chrome is mobile.
Visible: clicking any lot on the neighborhood screen.

**P1-16. Screen transition is the mobile stripe + splash image.**
`UI/Panels/UITransDialog.cs:62-74` — `trans_{type}.png` mobile art + diagonal stripe
wipe (TransColor). Mounted from `UINeighbourhoodSwitcher.cs:37` (neighborhood → CAS)
and `TS1CASScreen.cs:623`. TS1 has no such wipe (it cross-fades/cuts).

### P2 — minor / font-level / mobile-path / port-authored

**P2-17. Options panel captions in the modern font.** `UI/Panels/LiveSubpanels/
UIOriginalOptionsPanel.cs:198-209,271,290,313,333` — original art + strings, but
UILabel captions (button labels, row labels, Low/Med/High) are modern font; no
`UIOriginalText` twins here.

**P2-18. Catalog price labels modern font.** `Catalog/UICatalogItem.cs:138-147` —
"§n" price under each cell in modern font (original strips carry price in .ffn glyphs).

**P2-19. In-lot cheat bar is a flat teal modern textbox.** `UI/Panels/UICheatTextbox.cs:61-65`
— fill `(67,93,90)` is not in the uipal set; no original styling.

**P2-20. Neighborhood cheat bar is a bare UITextBox at (0,0).**
`UINeighbourhoodSelectionPanel.cs:446-453` — default position, no styling
(original cheat bar has its own bar art).

**P2-21. Job tab CareerButton mobile texture.** `UIJobSubpanel.cs:97` (`blank_blue.png`).

**P2-22. Skill pips mobile.** `UI/Controls/UISkillDisplay.cs:22` (`skill.png`) with
Simitone palette tints (UIStyle.cs:47-49) — used by Job + Personality tabs.

**P2-23. Motive trend arrow mobile.** `UI/Controls/UIValueBar.cs:77` (`motive_arrow.png`).

**P2-24. House tab labels modern font (no twins).** `UIHouseSubpanel.cs:46-86`.

**P2-25. "No items found" port string, modern font.** `UIBuyBrowsePanel.cs:473-481`
(catalog search — itself a mobile-only feature on desktop since the field is
desktop-dropped; label still exists for the panel API).

**P2-26. Port-authored "Custom Content Warning" dialog.** `GameController.cs:130-188`
— a UIMobileAlert surface with English hardcoded text that the original game does not
have (on mobile chrome on top of that).

**P2-27. Skill headline strings are dead code.** `WorldUI/UIHeadlineRenderer.cs:115-136`
— `SkillString`/`SpeedString` computed every frame, never drawn (the balloon renders
original sprite frames only). Cleanup + verify the original skill balloon contents.

**P2-28. Mobile-path leftovers (invisible on desktop, still non-original).**
`UIClockPanel` (clockbg/clockinbg/speedbtn pngs), `UIMoneyPanel` (money_bg),
`UIModeSwitcher` (mode_build/buy pngs), `UICutawayPanel`/CutBtn (cut_* pngs),
MainPanel mobile branch (level_up/down, divider, hide button, search field + solid
TitleBg fill behind it), `UILotControlTouchHelper` + `UIArchTouchHelper` (touch_* pngs)
+ `UIRotationAnimation` (rot_* pngs), and dead `UIButtonSubpanel`. These only render
when `FSOEnvironment.SoftwareKeyboard` is true.

**P2-29. Catalog item mobile fallback tint.** `Catalog/UICatalogItem.cs:109` —
fallback path draws `pswitch_icon_bg.png` tinted `(104,164,184)` if the original
frames fail to mount (fallback only, but non-original).

### Placement / duplication notes (no new P-entries; folded into the above)

- P1-13 is the live duplication defect (modern text under original text).
- P1-12(b) is the live overlap defect (mobile plaque behind original plaque).
- P0-3(a) is the live placement defect (edge buttons vs the original toolbar position).
- Previously-fixed placements confirmed fixed in code and NOT re-flagged: motive grid
  off-screen columns (R141 reposition in UIMotiveSubpanel.cs:98-105), duplicate speed
  clusters (R141 singularity, UIMainPanel.cs:47-53), neighborhood switcher bleeding
  into the lot (TS1GameScreen.cs:270-278 teardown), loading bar carried into the game
  screen (GameController.cs:59-66 filter), main-panel/UCP overlap (R120/R141
  MainPanel.X anchoring, UISimitoneFrontend.cs:98-99).

---

## Section 3 — Missing TS1 surfaces

1. **Original dialog/message bitmaps** — TS1's windowed dialogs (MsgBack family in
   UIGraphics) are absent; everything renders on the mobile stripe-wipe (P0-1). This
   also covers Sim message popups and event dialogs (they exist functionally via VM
   dialogs, but as mobile chrome).
2. **Original pie menu sprites + font** — surface exists (UIPieMenu) but on generated
   art; the original pie family is not mounted (P1-4).
3. **Original CAS art** — CreateACharBack.bmp + CAS buttons; the whole screen runs on
   the mobile pack (P0-2). The `Content/uigraphics/cas/` pngs are the mobile set, not
   UIGraphics.far members.
4. **Original neighborhood toolbar buttons** — switcher exists on mobile pngs (P0-3).
5. **Original phone dialog art** — phone book exists functionally (UICallNeighborAlert)
   on mobile chrome (P1-10).
6. **Interests live tab** — engine tab order is Mood/Personality/House/Job/Relationship/
   Interest (UIMainPanel.cs:116-121 comment); the port has no Interests subpanel at all.
7. **Camera/snapshot control** — the original control panel's photo/camera tool has no
   port surface (autocenter/PiP options exist, the camera button does not).
8. **Tutorial UI** — original tutorial tips/house system has no surface (the options
   panel's Reset Tutorial button mounts canon art + About text only, disclosed).
9. **Original cheat bar styling** — both in-lot (P2-19) and neighborhood (P2-20).
10. **Skill balloon progress contents** — headline renders the sprite frames, but the
    port's skill/speed text path is dead (P2-27); original contents need decode/pin.
11. **Sim/object pickup presentation** — the original composes the object in-hand with
    the query panel; the port adds a mobile pickup bar (P1-9) — surface exists, art
    wrong, and the original has no such bar.

---

## Section 4 — Autotest gate coverage vs this defect list

Gates read from `AutotestRunner.cs` (check list line 51; dispatch 679-774).

### Covered / pinned (a defect here would fail a gate)

- `uicp` — pins the UCP plate/patches/button frame dims, the ONE speed cluster, and the
  REMOVED mobile chrome on desktop (no hide button, no search box, no friend/eyedropper
  buttons). Guards UIDesktopUCP + UIMainPanel speed singularity.
- `uimpanel` — bottom-bar composition overlap-free/on-screen (guards the R120/R141
  placement fixes).
- `uitoolbar` / `uichrome` / `uidump` — original toolbar chrome members + PanelBack
  mount + pixel evidence (uidump-lot.png).
- `uibuy`, `uibuild`, `uibldt`, `uiroof`, `uidesc`, `uitt`, `uidtips` — catalog
  plaques, build tools, roof pager, description plaque, tooltips.
- `uilive`, `uijob`, `uihouse`, `uirate`, `uigauge`, `uivalue` — subpanel art/strings/
  bars (motive twins, job rating bar, house stats, ratings family).
- `uidialog`, `uilotq`, `uicas` — pin the STRINGS + glyph twins for dialogs, the lot
  query, and CAS. **Strings only — none of the chrome.**
- `uinbhd`, `uianim` — neighborhood screen bitmaps + animation families (not the
  switcher/outline/letterbox).
- `uiboot`, `uisplash`, `uipal`, `uitext`, `uivfont`, `uiglyph`, `uicur` — boot art,
  splash strings, palette, .ffn rendering.
- `uisurvey` — tree dumps of live/ucp/buy states (evidence logger; always passes — it
  makes the bottom-bar tree visible to humans, not a pin).

### NOT pinned (defects a gate cannot see today)

- **P0-1** dialog chrome (stripes/title band/UIBigButton pngs) — uidialog checks
  strings/twins only.
- **P0-2** CAS art (uicas = strings only).
- **P0-3** neighborhood switcher buttons, ngbh_outline, letterbox gradient.
- **P1-4..P1-16** pie menu art, queue icons, relationship/inventory/switch-avatar/
  pickup panels, phone/clothes dialog chrome, switcher column chrome + cat_btn_base
  plaque, subsort caption duplication, money headline, house-select chrome, UITransDialog.
- **P2-17..P2-29** residual modern-font labels, cheat bars, skill pips, dead code,
  mobile-path branches, content-warning dialog.

Recommendation for the next round: add a `uidlgchrome`-style gate when P0-1 is ported
(pin the original dialog bitmap family like uicp pins the UCP), a `uicasart` gate for
P0-2, and extend `uinbhd` to pin the neighborhood toolbar members + absence of
`ngbh_*.png`/`ngbh_outline.png`/`load_static_bg.png` in the rendered tree (P0-3).

---

## Quick counts

- P0: 3 (dialogs, CAS, neighborhood chrome)
- P1: 13 (pie menu, queue icons, relationship, inventory, switch-avatar, pickup,
  phone, clothes, switcher chrome + plaque, subsort duplication, money balloon,
  lot-query chrome, transition)
- P2: 13 grouped entries (17-29)
- Missing/partial TS1 surfaces: 11
