using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Model;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.HIT;
using FSO.HIT.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Simitone.Client.UI.Panels.LiveSubpanels
{
    // R121/R174 'uiopts': the ORIGINAL options screen, IFF-first. Every string is read
    // verbatim from UIText.iff STR# 145 'optionstrs' (84 English entries, chunk
    // sha256-pinned by the gate) and every control texture is the ORIGINAL
    // UIGraphics.far member (sha-pinned by the gate). The original game stored its
    // own main-button layout in the string table as '(x;y)' directives: the six
    // main buttons sit EXACTLY at those parsed coordinates (Save (29,6),
    // Neighborhood (88,6), Quit (159,6), Graphics Options (29,58), Sound Options
    // (89,58), Play Options (151,58)). The apparent later coordinate-like strings
    // are row pitches/widths, not popup anchors (R174 executable trace).
    // R174 closes the main-screen residual against cWinOptions::BuildButtons and
    // TSPaint: the six captions are tooltips (never visible labels), the buttons
    // use their absolute options-local coordinates, the main grid remains mounted
    // while a section is active, and the two original toothpick strips paint at
    // (16,51)/(222,6). There is no synthetic category plaque or back button.
    //
    // Live engine effect exists for: Free Will (VM.FreeWillEnabled), Edge
    // Scrolling (UILotControl TestScroll gate), the three volume sliders (HITVM
    // master volumes, applied live exactly like boot does) and Anti-alias (new
    // surfaces). UI-26 wire round (r260-options-readiness WIRE rows) adds:
    // Lighting (WorldConfig.LightingMode 0/1 + World.ChangedWorldConfig, boot
    // + toggle), Shadows (WorldConfig.ObjShadows gating LMapBatch object-shadow
    // generation), Character Detail (FSO.Vitaboy.Avatar.DefaultTechnique, read
    // every Avatar.Draw) and Sim In Background (VM.ApplyFocus focus-suspend of
    // SpeedMultiplier via SimitoneGame.RelayFocus). The remaining canon options
    // persist the player's choice to config.ini verbatim pending engine work
    // (disclosed residual): Terrain Detail (grass system = BUILD tranche),
    // Quick Tips (presenter = BUILD tranche) and Export HTML (writer = BUILD
    // tranche). Interface Effects is a DISCLOSED PARTIAL: natively the row is
    // cOptionsMgr "BoboVision" (decode §3 — UI window effects gate), and this
    // port binds it to the PIP fade only; the port has no decoded window-
    // transition effect sites to widen onto.
    public class UIOriginalOptionsPanel : UISubpanel
    {
        public static string Table = "145";

        // ---- canon helpers -------------------------------------------------
        public static string S(int i) { return GameFacade.Strings.GetString(Table, i.ToString()); }

        public static Point? ParseCanonPos(string s)
        {
            if (s == null) return null;
            var m = Regex.Match(s.Trim(), @"^\((\d+);(\d+)\)$");
            if (!m.Success) return null;
            return new Point(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
        }

        // ---- UI-26 engine wires (r260-options-readiness WIRE rows) ----------
        // WIRE (b): 'Character Detail' Low/Med/High → Vitaboy.fx technique
        // index — 0 NoSSAA, 2 AdvancedLighting, 3 SSAA (1/4/5 are the ObjID/
        // shadow/directional variants the native row never selected; TSO
        // precedent UISim.cs sets 0/3). Avatar.Draw reads DefaultTechnique
        // every frame, so the technique swap is live on the next draw.
        public static int CharacterDetailTechnique(int detail)
        {
            detail = Math.Max(0, Math.Min(2, detail));
            return detail == 1 ? 2 : (detail == 2 ? 3 : 0);
        }

        public static void ApplyCharacterDetail(int detail)
        {
            FSO.Vitaboy.Avatar.DefaultTechnique = CharacterDetailTechnique(detail);
        }

        // ORIG-01 D-2: 'Terrain Detail' was persistence-only — it now scales
        // the grass renderer (GrassEffect.DetailScale; disclosed 0.45/0.725/
        // 1.0 ladder, native one undecoded). Also applied at boot
        // (SimitoneGame first update).
        public static void ApplyTerrainDetail(int detail)
        {
            FSO.LotView.Effects.GrassEffect.DetailScale = 0.45f + 0.275f * Math.Max(0, Math.Min(2, detail));
        }

        // WIRE (a): 'Lighting' — AdvancedLighting is LightingMode > 0, so the
        // toggle maps straight to 0/1 and re-applies through the live-proven
        // World.ChangedWorldConfig path (light batches rebuild from the
        // ROOM/OUTDOORS changed flags). ForceAdvLight lots clamp the mode to 1
        // inside ChangedWorldConfig (VMContext law); the choice still persists.
        public static void ApplyLighting(FSO.SimAntics.VM vm, bool on)
        {
            FSO.LotView.WorldConfig.Current.LightingMode = on ? 1 : 0;
            vm?.Context.World.ChangedWorldConfig(GameFacade.GraphicsDevice);
        }

        // WIRE (d): 'Shadows' — the shadows-only gate the native cOptionsMgr
        // always had; LMapBatch.DrawObjShadows refuses to generate while the
        // flag is off (wall shadows and the light itself are untouched).
        public static void ApplyShadows(FSO.SimAntics.VM vm, bool on)
        {
            FSO.LotView.WorldConfig.Current.ObjShadows = on;
            vm?.Context.World.ChangedWorldConfig(GameFacade.GraphicsDevice);
        }

        // ---- gate introspection -------------------------------------------
        public class MainButtonInfo
        {
            public int CaptionIndex; public string Caption;
            public int PosIndex; public Point CanonPos;
            public string ArtMember; public UIButton Btn; public Texture2D Tex;
        }
        public class OptRow
        {
            public int CaptionIndex; public string Caption;   // canon string, verbatim
            public int AboutTitleIndex; public string AboutTitle;
            public int AboutBodyIndex; public string AboutBody;
            public string Kind;                               // check | radio | slider | button
            public string ArtMember; public string PopupMember;
            public Func<bool> IsOn; public Func<int> IntValue; public Action Toggle;
            public string Group;                              // radio group id
            public UIElement Widget;
        }
        public List<MainButtonInfo> Mains = new List<MainButtonInfo>();
        public List<OptRow> Rows = new List<OptRow>();
        public readonly List<UIOriginalText> Labels = new List<UIOriginalText>(); // font[9] row captions
        public readonly List<UIOriginalText> RadioValueLabels = new List<UIOriginalText>(); // font[8] Low/Med/High
        public string ActiveScreen = "main";                  // main | graphics | sound | play
        public UIOptionAboutPopup CurrentPopup;
        public static int CanonDirectivesParsed;              // '(x;y)' strings parsed this mount
        public static int PopupsOpened;

        private Texture2D _optSave, _optNbhd, _optExit, _optGraphics, _optSound, _optPlay;
        private Texture2D _checkbox, _radio, _sliderOn, _sliderOff, _tutReset;
        public Texture2D ToothpickHorizontal, ToothpickVertical;
        private readonly List<UIElement> _mainContent = new List<UIElement>();
        private readonly List<UIElement> _screenContent = new List<UIElement>();

        private class MainDef
        {
            public int Cap, Pos; public string Member; public Texture2D Tex; public Action Act;
            public Func<bool> Disabled; // ORIG-02: native save-disable law
        }

        // ORIG-02 community-buildbuy law (cDDDSimsView/Neighborhood::GetZoningType
        // 0xaaa20): Save is disabled on community lots (zoning == 2) and lots
        // 93-99 (downtown/visitor).
        private bool CommunityNoSave()
        {
            try
            {
                var lot = Game?.vm?.GetGlobalValue(10) ?? 0;
                if (lot >= 93 && lot <= 99) return true;
                return FSO.Content.Content.Get().Neighborhood.GetZoningType((short)lot) == 2;
            }
            catch { return false; }
        }

        public UIOriginalOptionsPanel(TS1GameScreen game) : base(game)
        {
            Game = game;
            var gd = GameFacade.GraphicsDevice;
            // NOTE: member names are the manifest's EXACT case (the FAR lookup is
            // case-sensitive; the manifest is chaotic: 'OptSave.BMP' but
            // 'optgraphics.bmp'). UIOriginal also carries a case-insensitive fallback.
            _optSave = UIOriginal.ResolveOrPng("cpanel\\Buttons\\OptSave.BMP", "opt_save.png", null);
            _optNbhd = UIOriginal.ResolveOrPng("cpanel\\Buttons\\OptNbhd.bmp", "opt_neigh.png", null);
            _optExit = UIOriginal.ResolveOrPng("cpanel\\Buttons\\OptExit.bmp", "opt_quit.png", null);
            _optGraphics = UIOriginal.ResolveOrPng("cpanel\\Buttons\\optgraphics.bmp", null, null);
            _optSound = UIOriginal.ResolveOrPng("cpanel\\Buttons\\optsound.bmp", null, null);
            _optPlay = UIOriginal.ResolveOrPng("cpanel\\Buttons\\optplay.bmp", null, null);
            _checkbox = UIOriginal.ResolveOrPng("cpanel\\Buttons\\optcheckbox.bmp", null, null);
            _radio = UIOriginal.ResolveOrPng("cpanel\\Buttons\\optradio.bmp", null, null);
            _sliderOn = UIOriginal.ResolveOrPng("cpanel\\Buttons\\optslideron.bmp", null, null);
            _sliderOff = UIOriginal.ResolveOrPng("cpanel\\Buttons\\optslideroff.bmp", null, null);
            _tutReset = UIOriginal.ResolveOrPng("cpanel\\Buttons\\opttutreset.BMP", null, null);
            ToothpickHorizontal = UIOriginal.EnsureResolved("cpanel\\Backgrounds\\OptionsToothpkHoriz.bmp")?.Get(gd);
            ToothpickVertical = UIOriginal.EnsureResolved("cpanel\\Backgrounds\\OptionsToothpkVert.bmp")?.Get(gd);
            ShowMain();
        }

        public override void GameResized()
        {
            base.GameResized();
            // cWinOptions always owns the complete 804x100 PanelBack child. Its
            // coordinates are absolute and therefore do not reflow on resize.
            if (Game.Desktop) Size = new Vector2(Math.Max(0, Game.ScreenWidth - 220), 100);
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (CurrentPopup == null) return;
            // cWinOptions::TSOnMouseExit{Child} dismisses the informational
            // popup once the new hit window is outside the Options hierarchy.
            // The popup is a separate root overlay, so entering it also closes
            // it; the plaque itself is deliberately noninteractive.
            var mouse = GetMousePosition(state.MouseState);
            if (mouse.X < 0 || mouse.Y < 0 || mouse.X >= Size.X || mouse.Y >= Size.Y)
                ClosePopup();
        }

        private void ClearScreenContent()
        {
            ClosePopup();
            foreach (var e in _screenContent) Remove(e);
            _screenContent.Clear();
            Rows.Clear();
            Labels.Clear();
            RadioValueLabels.Clear();
        }

        private void Track(UIElement e)
        {
            _screenContent.Add(e);
            Add(e);
        }

        private void TrackMain(UIElement e)
        {
            _mainContent.Add(e);
            Add(e);
        }

        private void ClearMainContent()
        {
            foreach (var e in _mainContent) Remove(e);
            _mainContent.Clear();
            Mains.Clear();
        }

        // ---- MAIN: the six canon buttons -----------------------------------
        public void ShowMain()
        {
            ActiveScreen = "main";
            ClearScreenContent();
            if (Mains.Count == 0) BuildMainButtons();
            SetSelectedSection(null);
            Invalidate();
        }

        private void BuildMainButtons()
        {
            ClearMainContent();

            var defs = new MainDef[]
            {
                // canon pairing: even string i is the '(x;y)' position, odd i+1 its caption
                new MainDef { Cap = 3,  Pos = 2,  Member = "cpanel\\Buttons\\OptSave.BMP",     Tex = _optSave,
                              // ORIG-02 community-matrix law: the native disables
                              // Save on community lots (zoning==2) and lots 93-99
                              // (downtown/visitor) — the Save button greys out and
                              // the click is inert. Vacant-house save-disable is
                              // unreachable here (you cannot enter a vacant house).
                              Act = () => { if (!CommunityNoSave()) Game.Save(); },
                              Disabled = CommunityNoSave },
                new MainDef { Cap = 1,  Pos = 0,  Member = "cpanel\\Buttons\\OptNbhd.bmp",     Tex = _optNbhd,
                              Act = () => Game.ReturnToNeighbourhood() },
                new MainDef { Cap = 5,  Pos = 4,  Member = "cpanel\\Buttons\\OptExit.bmp",     Tex = _optExit,
                              Act = () => Game.CloseAttempt() },
                new MainDef { Cap = 7,  Pos = 6,  Member = "cpanel\\Buttons\\optgraphics.bmp", Tex = _optGraphics,
                              Act = () => ToggleScreen("graphics") },
                new MainDef { Cap = 9,  Pos = 8,  Member = "cpanel\\Buttons\\optsound.bmp",    Tex = _optSound,
                              Act = () => ToggleScreen("sound") },
                new MainDef { Cap = 11, Pos = 10, Member = "cpanel\\Buttons\\optplay.bmp",     Tex = _optPlay,
                              Act = () => ToggleScreen("play") },
            };

            foreach (var d in defs)
            {
                var pos = ParseCanonPos(S(d.Pos));
                if (pos == null) continue; // canon string unparsable: row cannot mount
                CanonDirectivesParsed++;
                var info = new MainButtonInfo
                {
                    CaptionIndex = d.Cap,
                    Caption = S(d.Cap),
                    PosIndex = d.Pos,
                    CanonPos = pos.Value,
                    ArtMember = d.Member,
                    Tex = d.Tex,
                };
                if (d.Tex != null)
                {
                    var btn = new UIButton(d.Tex)
                    {
                        Position = pos.Value.ToVector2(),
                        Tooltip = info.Caption,
                    };
                    var act = d.Act;
                    if (d.Disabled != null) btn.Disabled = d.Disabled(); // ORIG-02: native save-disable
                    btn.OnButtonClick += (b) => { act(); };
                    info.Btn = btn;
                    TrackMain(btn);
                }
                Mains.Add(info);
            }
        }

        private void SetSelectedSection(string name)
        {
            foreach (var main in Mains)
            {
                if (main.Btn == null) continue;
                bool section = main.CaptionIndex == 7 || main.CaptionIndex == 9 || main.CaptionIndex == 11;
                bool selected = (name == "graphics" && main.CaptionIndex == 7)
                    || (name == "sound" && main.CaptionIndex == 9)
                    || (name == "play" && main.CaptionIndex == 11);
                // cWinOptions behavior-2 selectors latch in state 1 (the cyan
                // down cell). Stock UIButton frame 2 is merely hover.
                main.Btn.ForceState = section && selected ? 1 : -1;
            }
        }

        private void ToggleScreen(string name)
        {
            if (ActiveScreen == name) ShowMain();
            else ShowScreen(name);
        }

        // ---- sub-screens ----------------------------------------------------
        public void ShowScreen(string name)
        {
            if (name != "graphics" && name != "sound" && name != "play") { ShowMain(); return; }
            if (Mains.Count == 0) BuildMainButtons();
            ActiveScreen = name;
            ClearScreenContent();
            SetSelectedSection(name);

            if (name == "graphics") BuildGraphics();
            else if (name == "sound") BuildSound();
            else BuildPlay();
            // Child replacement and ForceState updates must invalidate the
            // cached bitmap even when no pointer hover state changes.
            Invalidate();
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            // cWinOptions::TSPaint: these are one-to-one blits into PanelBack,
            // before child controls are composed.
            if (ToothpickHorizontal != null)
                DrawLocalTexture(batch, ToothpickHorizontal, null, new Vector2(16, 51), Vector2.One);
            if (ToothpickVertical != null)
                DrawLocalTexture(batch, ToothpickVertical, null, new Vector2(222, 6), Vector2.One);
            base.Draw(batch);
        }

        private UIOriginalText CanonLabel(int idx, Vector2 pos)
        {
            var font = OriginalGlyphFont.LoadByIndex(9, GameFacade.GraphicsDevice);
            var caption = S(idx);
            var l = new UIOriginalText(caption, font)
            {
                Position = pos,
                Size = new Vector2(font != null ? font.Measure(caption) : Math.Max(1, caption.Length * 7),
                    font != null ? font.LineHeight : 17),
                Color = new Color(0xC3, 0xCD, 0xCD, 0xFF),
            };
            Track(l);
            Labels.Add(l);
            return l;
        }

        private void AddRadioValueLabels(Vector2 radioPos)
        {
            // R215 decode: the engine builds ONE cWinTriText for the whole
            // screen (the binary has a single ctor call site, 0x285c64) — a
            // shared Low/Med/High column header above the FIRST tri-radio,
            // not one label row per radio. Its SetArea (0x285d98 tail):
            // left = radio.left − 8, right = radio.right + 5, top =
            // radio.top − fontHeight − 7, height = fontHeight − 2 — three
            // 31px centered cells (cWinTriText::SetArea divides by 3).
            // The old per-radio second row landed y=33..47 ON TOP of the
            // first radio's cells (30..50) — the overlap defect.
            var font = OriginalGlyphFont.LoadByIndex(8, GameFacade.GraphicsDevice);
            int height = font != null ? font.LineHeight : 16;
            float left = radioPos.X - 8;
            float top = radioPos.Y - height - 7;
            for (int i = 0; i < 3; i++)
            {
                var caption = S(39 + i);
                int width = font != null ? font.Measure(caption) : caption.Length * 7;
                var label = new UIOriginalText(caption, font)
                {
                    Position = new Vector2(left + i * 31 + (31 - width) / 2f, top),
                    Size = new Vector2(width, height),
                    Color = new Color(0xC3, 0xCD, 0xCD, 0xFF),
                };
                Track(label);
                RadioValueLabels.Add(label);
            }
        }

        private OptRow AddCheckbox(int capIdx, int titleIdx, int bodyIdx, string popupMember,
                                   Func<bool> get, Action<bool> set, Vector2 pos)
        {
            var row = new OptRow
            {
                CaptionIndex = capIdx, Caption = S(capIdx),
                AboutTitleIndex = titleIdx, AboutTitle = S(titleIdx),
                AboutBodyIndex = bodyIdx, AboutBody = S(bodyIdx),
                Kind = "check", ArtMember = "cpanel\\Buttons\\optcheckbox.bmp",
                PopupMember = popupMember,
                IsOn = get,
                Toggle = () => { set(!get()); GlobalSettings.Default.Save(); Invalidate(); },
            };
            if (_checkbox != null)
            {
                // cTSWinCheck owns a persistent boolean independently from the
                // mouse state. A plain UIButton ForceState=2 mistakes the hover
                // cell for the selected-state protocol.
                var btn = new UIOriginalOptionCheck(_checkbox, get, row.Toggle) { Position = pos };
                row.Widget = btn;
                Track(btn);
            }
            var label = CanonLabel(capIdx, pos + new Vector2(30, 2));
            MakeCaptionOpenPopup(label, row);
            Rows.Add(row);
            return row;
        }

        private void AddRadioGroup(int capIdx, int titleIdx, int bodyIdx, string popupMember,
                                   string group, Func<int> get, Action<int> set,
                                   Vector2 rightEdgeAndY, Vector2 radioPos)
        {
            var row = new OptRow
            {
                CaptionIndex = capIdx, Caption = S(capIdx),
                AboutTitleIndex = titleIdx, AboutTitle = S(titleIdx),
                AboutBodyIndex = bodyIdx, AboutBody = S(bodyIdx),
                Kind = "radio", ArtMember = "cpanel\\Buttons\\optradio.bmp",
                PopupMember = popupMember, Group = group,
                IntValue = get,
            };
            var caption = S(capIdx);
            var labelWidth = MeasureCaption(caption);
            var label = CanonLabel(capIdx,
                new Vector2(rightEdgeAndY.X - labelWidth, rightEdgeAndY.Y));
            MakeCaptionOpenPopup(label, row);
            Rows.Add(row);

            if (_radio != null)
            {
                var tri = new UIOriginalOptionTriRadio(_radio, get, v =>
                {
                    set(v);
                    GlobalSettings.Default.Save();
                    Invalidate();
                }) { Position = radioPos };
                row.Widget = tri;
                Track(tri);
                if (RadioValueLabels.Count == 0)
                    AddRadioValueLabels(radioPos); // R215: ONE shared header, on the first radio only
            }
        }

        private OptRow AddSlider(int capIdx, int visibleIdx, int titleIdx, int bodyIdx, string popupMember,
                                 Func<byte> get, Action<byte> set, Vector2 labelPos, Vector2 sliderPos)
        {
            var row = new OptRow
            {
                CaptionIndex = capIdx, Caption = S(capIdx),
                AboutTitleIndex = titleIdx, AboutTitle = S(titleIdx),
                AboutBodyIndex = bodyIdx, AboutBody = S(bodyIdx),
                Kind = "slider", ArtMember = "cpanel\\Buttons\\optslideron.bmp",
                PopupMember = popupMember,
                IntValue = () => get(),
            };
            var slider = new UIOptSlider(_sliderOff, _sliderOn) { Position = sliderPos };
            slider.Value = get();
            slider.OnChange += (v) => { set((byte)v); GlobalSettings.Default.Save(); };
            row.Widget = slider;
            Track(slider);
            var label = CanonLabel(visibleIdx, labelPos);
            MakeCaptionOpenPopup(label, row);
            Rows.Add(row);
            return row;
        }

        private static int MeasureCaption(string caption)
        {
            var font = OriginalGlyphFont.LoadByIndex(9, GameFacade.GraphicsDevice);
            return Math.Max(1, font != null ? font.Measure(caption ?? "") : (caption ?? "").Length * 7);
        }

        private void MakeCaptionOpenPopup(UIOriginalText label, OptRow row)
        {
            label.ListenForMouse(new Rectangle(0, 0, (int)label.Size.X, (int)label.Size.Y),
                (type, state) =>
                {
                    if (type == UIMouseEventType.MouseDown) OpenPopup(row);
                });
        }

        // ---- About popups at the canon section directive --------------------
        public void OpenPopup(OptRow row)
        {
            if (row == null || row.PopupMember == null) return;
            if (CurrentPopup != null && ReferenceEquals(CurrentPopup.Row, row))
            {
                ClosePopup();
                return;
            }
            ClosePopup();
            var tex = UIOriginal.EnsureResolved(row.PopupMember);
            var popup = new UIOptionAboutPopup(this, row,
                tex != null ? tex.Get(GameFacade.GraphicsDevice) : null);
            // cWinLivePopup::RebuildBuffer anchors to the options parent's
            // absolute right/top edges. For the native 804x100 options child
            // that is panel-local (parentWidth-W,-H), independent of the row.
            // (The executable's separate 150px-parent branch adds 50 to y;
            // cWinOptions is 100px tall and never takes that branch.)
            popup.Position = new Vector2(Size.X - popup.Size.X, -popup.Size.Y);
            popup.CanonPos = new Point((int)popup.X, (int)popup.Y);
            CurrentPopup = popup;
            PopupsOpened++;
            // UISubpanel is a 804x100 cached surface. The native popup lives
            // above that rectangle at negative local Y; adding it to the
            // cached child list clips it completely. DynamicOverlay retains
            // the panel-local coordinate system but draws after/outside the
            // cache, matching the executable's separate root popup window.
            DynamicOverlay.Add(popup);
        }

        public void ClosePopup()
        {
            if (CurrentPopup != null)
            {
                DynamicOverlay.Remove(CurrentPopup);
                CurrentPopup = null;
            }
        }

        // ---- Graphics Options (145 [12..41]) --------------------------------
        private void BuildGraphics()
        {
            var set = GlobalSettings.Default;
            AddCheckbox(14, 15, 16, "cpanel\\PopupOptAntiAlias.bmp",
                () => set.AntiAlias > 0, (v) => set.AntiAlias = v ? 1 : 0, new Vector2(245, 7));
            AddCheckbox(17, 18, 19, "cpanel\\PopupOptShadows.bmp",
                () => set.TS1Shadows, (v) => { set.TS1Shadows = v; ApplyShadows(Game?.vm, v); }, new Vector2(245, 27));
            AddCheckbox(20, 21, 22, "cpanel\\PopupOptLighting.bmp",
                () => set.Lighting, (v) => { set.Lighting = v; ApplyLighting(Game?.vm, v); }, new Vector2(245, 47));
            AddCheckbox(23, 24, 25, "cpanel\\PopupOptTransUI.bmp",
                () => set.TS1InterfaceFX, (v) => set.TS1InterfaceFX = v, new Vector2(245, 67));
            // Executable anchors: right-aligned labels to x480; the two logical
            // 80x20 tri-radios begin at x485 with a 26px row pitch.
            AddRadioGroup(28, 29, 30, "cpanel\\PopupOptTerrainDetail.bmp", "terrain",
                () => set.TS1TerrainDetail, (v) => { set.TS1TerrainDetail = v; ApplyTerrainDetail(v); },
                new Vector2(480, 30), new Vector2(485, 30));
            AddRadioGroup(31, 32, 33, "cpanel\\PopupOptCharDetail.bmp", "character",
                () => set.TS1CharacterDetail, (v) => { set.TS1CharacterDetail = v; ApplyCharacterDetail(v); },
                new Vector2(480, 56), new Vector2(485, 56));
        }

        // ---- Sound Options (145 [42..52]) ------------------------------------
        private void BuildSound()
        {
            var hit = HITVM.Get();
            var set = GlobalSettings.Default;
            int labelX = 305;
            int labelMax = Math.Max(MeasureCaption(S(44)), Math.Max(MeasureCaption(S(47)), MeasureCaption(S(50))));
            int sliderX = labelX + labelMax + 15;
            AddSlider(45, 44, 45, 46, "cpanel\\PopupOptSFX.bmp",
                () => set.FXVolume,
                (v) => { set.FXVolume = v; hit.SetMasterVolume(HITVolumeGroup.FX, v / 10f); },
                new Vector2(labelX, 13), new Vector2(sliderX, 13));
            AddSlider(48, 47, 47, 49, "cpanel\\PopupOptMusic.bmp",
                () => set.MusicVolume,
                (v) => { set.MusicVolume = v; hit.SetMasterVolume(HITVolumeGroup.MUSIC, v / 10f); },
                new Vector2(labelX, 41), new Vector2(sliderX, 41));
            AddSlider(51, 50, 50, 52, "cpanel\\PopupOptVox.bmp",
                () => set.VoxVolume,
                (v) => { set.VoxVolume = v; hit.SetMasterVolume(HITVolumeGroup.VOX, v / 10f); },
                new Vector2(labelX, 69), new Vector2(sliderX, 69));
        }

        // ---- Play Options (145 [53..83]) -------------------------------------
        private void BuildPlay()
        {
            var set = GlobalSettings.Default;
            AddCheckbox(56, 57, 58, "cpanel\\PopupOptAutoCenter.bmp",
                () => set.TS1AutoCenter, (v) => set.TS1AutoCenter = v, new Vector2(245, 7));
            AddCheckbox(59, 60, 61, "cpanel\\PopupOptFreeWill.bmp",
                () => FSO.SimAntics.VM.FreeWillEnabled,
                (v) => { FSO.SimAntics.VM.FreeWillEnabled = v; set.TS1FreeWill = v; }, new Vector2(245, 27));
            AddCheckbox(62, 63, 64, "cpanel\\PopupOptEdgeScroll.bmp",
                () => set.EdgeScroll, (v) => set.EdgeScroll = v, new Vector2(245, 47));
            // UI-26 WIRE (c): the law consumes TS1SimInBackground at the next
            // focus transition (SimitoneGame.RelayFocus → VM.ApplyFocus — the
            // native cSimulator +52 signed-speed law); 'on' keeps the simulator
            // running while the window lacks focus.
            AddCheckbox(65, 66, 67, "cpanel\\PopupOptSimInBack.bmp",
                () => set.TS1SimInBackground, (v) => set.TS1SimInBackground = v, new Vector2(245, 67));

            int firstMax = Math.Max(MeasureCaption(S(56)), Math.Max(MeasureCaption(S(59)),
                Math.Max(MeasureCaption(S(62)), MeasureCaption(S(65)))));
            int secondLabelX = 245 + firstMax + 70;
            int secondCheckX = secondLabelX - 30;
            AddCheckbox(72, 73, 74, "cpanel\\PopupOptQuickTips.bmp",
                () => set.TS1QuickTips, (v) => set.TS1QuickTips = v, new Vector2(secondCheckX, 7));
            AddCheckbox(75, 76, 77, "cpanel\\PopupOptAutoSnap.bmp",
                () => set.TS1AutoSnapshot, (v) => set.TS1AutoSnapshot = v, new Vector2(secondCheckX, 27));
            AddCheckbox(78, 79, 80, "cpanel\\PopupOptLivePIP.bmp",
                () => set.TS1LivePIP, (v) => set.TS1LivePIP = v, new Vector2(secondCheckX, 47));
            AddCheckbox(81, 82, 83, "cpanel\\PopupExportHTML.bmp",
                () => set.TS1ExportHTML, (v) => set.TS1ExportHTML = v, new Vector2(secondCheckX, 67));

            // Reset Tutorial (145 [69]) — the canon OptTutReset bitmap button.
            // R247 (r247-tut-lifecycle decode §B + skeptic correction 3): the
            // button now runs the native reset flow — click → confirm dialog
            // (native style 1: proceed only on the Yes/result-3 button) → the
            // DoSave gate (not-in-a-house proceeds; in a house the port's
            // existing save machinery runs, the STR# 153 [3]/[4] YesNoCancel
            // prompt: Yes = native result 4 (save then proceed), No = native
            // result 2 (proceed without saving), Cancel = the native abort
            // path) → engine StageTutorialReset (the UserData/Tutorial.FAM →
            // UserData/Import/Tutorial.FAM copy; fail-if-exists per the native
            // CopyFileA flag 1) → success lands on the neighborhood screen
            // (native DoNbhdScreen(true) via the port's ExitLot production
            // teardown); any failure raises the native style-0 error dialog.
            // STRING GAP (disclosed): the native confirm/failure literals are
            // runtime-localized cTSStrings (decode §B: not statically
            // recoverable), so this port surfaces the row's own canon
            // STR# 145 [69]/[71] pair on both dialogs.
            //
            // The canon About popup (art PopupResetTutorial.bmp, strings
            // [70]/[71]) is NO LONGER mouse-reachable, honestly disclosed: its
            // only opener was this button (pre-R247 the button opened the
            // popup because the destructive reset had no port equivalent), and
            // the button now runs the native reset. The caption→popup law of
            // the other rows (MakeCaptionOpenPopup) does not apply — the canon
            // row is a caption-less bitmap button at (panelWidth-25,7) with no
            // label to hang it on (r174 §3 Play). The popup's content still
            // surfaces: S(71) is the confirm and error dialog body below, and
            // OpenPopup(row) remains available for any future canon anchor.
            var row = new OptRow
            {
                CaptionIndex = 69, Caption = S(69),
                AboutTitleIndex = 70, AboutTitle = S(70),
                AboutBodyIndex = 71, AboutBody = S(71),
                Kind = "button", ArtMember = "cpanel\\Buttons\\opttutreset.BMP",
                PopupMember = "cpanel\\PopupResetTutorial.bmp",
            };
            if (_tutReset != null)
            {
                var btn = new UIButton(_tutReset)
                {
                    Position = new Vector2(Math.Max(0, Size.X - 25), 7),
                    ImageStates = 4,
                    Tooltip = row.Caption,
                };
                btn.OnButtonClick += (b) => { BeginTutorialReset(); };
                row.Widget = btn;
                Track(btn);
            }
            Rows.Add(row);
        }

        // ---- R247: the native ResetTutorial flow (client half) ---------------
        // Probe surface for AutotestTutorialLifecycle247: the flow asserts the
        // real dialogs through these, and the engine dispatch count through
        // TutorialEngine247.StageResetCallCount.
        internal UIMobileAlert ResetConfirmDialogForProbe { get; private set; }
        internal UIMobileAlert ResetSavePromptForProbe { get; private set; }
        internal UIMobileAlert ResetErrorDialogForProbe { get; private set; }

        private void BeginTutorialReset()
        {
            // Native step 1: confirm (style 1) — proceed only on the Yes
            // button (the Quit convention's result 3).
            UIMobileAlert confirm = null;
            confirm = new UIMobileAlert(new UIAlertOptions()
            {
                Title = S(69),
                Message = S(71),
                Buttons = UIAlertButton.YesNo(
                    (b) => { confirm.Close(); ResetConfirmDialogForProbe = null; TutorialResetSaveGate(); },
                    (b) => { confirm.Close(); ResetConfirmDialogForProbe = null; })
            });
            ResetConfirmDialogForProbe = confirm;
            UIScreen.GlobalShowDialog(confirm, true);
        }

        private void TutorialResetSaveGate()
        {
            // Native step 2: the DoSave gate (skeptic correction 3) — the gate
            // PROCEEDS when no house is loaded; the port's options panel only
            // exists in-lot, so the in-lot branch follows the port's existing
            // save machinery (the ReturnToNeighbourhood prompt). With the
            // options panel unmounted Game.InLot is re-read live: a reset
            // dispatched while not in a lot proceeds without a prompt.
            var game = Game;
            if (game == null || !game.InLot)
            {
                RunTutorialReset();
                return;
            }
            UIMobileAlert savePrompt = null;
            savePrompt = new UIMobileAlert(new UIAlertOptions()
            {
                Title = GameFacade.Strings.GetString("153", "3"), //save
                Message = GameFacade.Strings.GetString("153", "4"), //Do you want to save the game?
                Buttons = UIAlertButton.YesNoCancel(
                    (b) => { savePrompt.Close(); ResetSavePromptForProbe = null; game.Save(); RunTutorialReset(); },
                    (b) => { savePrompt.Close(); ResetSavePromptForProbe = null; RunTutorialReset(); },
                    (b) => { savePrompt.Close(); ResetSavePromptForProbe = null; }) // abort
            });
            ResetSavePromptForProbe = savePrompt;
            UIScreen.GlobalShowDialog(savePrompt, true);
        }

        private void RunTutorialReset()
        {
            // Native steps 3-5: copy (engine StageTutorialReset), then land on
            // the neighborhood screen on success or raise the style-0 error.
            if (Simitone.Client.Utils.TutorialEngine247.StageTutorialReset())
            {
                var game = Game;
                if (game != null) game.ExitLot(); // native DoNbhdScreen(true)
            }
            else
            {
                UIMobileAlert error = null;
                error = new UIMobileAlert(new UIAlertOptions()
                {
                    Title = S(69),
                    Message = S(71),
                    Buttons = UIAlertButton.Ok((b) => { error.Close(); ResetErrorDialogForProbe = null; })
                });
                ResetErrorDialogForProbe = error;
                UIScreen.GlobalShowDialog(error, true);
            }
        }
    }

    /// <summary>
    /// Stateful cTSWinCheck rendering for the original six-cell option sheet:
    /// off/on, off/on entered, off/on disabled. Each cell is exactly 20px.
    /// </summary>
    public class UIOriginalOptionCheck : UIElement
    {
        private readonly Texture2D Art;
        private readonly Func<bool> Get;
        private readonly Action Toggle;
        private bool Hover, Down;
        public bool Disabled;
        public int FrameWidth { get { return Art != null ? Art.Width / 6 : 20; } }
        private Vector2 _size;
        public override Vector2 Size { get { return _size; } set { _size = value; } }

        public UIOriginalOptionCheck(Texture2D art, Func<bool> get, Action toggle)
        {
            Art = art;
            Get = get;
            Toggle = toggle;
            Size = new Vector2(art != null ? art.Width / 6 : 20, art != null ? art.Height : 21);
            ListenForMouse(new Rectangle(0, 0, (int)Size.X, (int)Size.Y), Mouse);
        }

        private void Mouse(UIMouseEventType type, UpdateState state)
        {
            if (Disabled) return;
            if (type == UIMouseEventType.MouseOver) Hover = true;
            else if (type == UIMouseEventType.MouseOut) { Hover = false; Down = false; }
            else if (type == UIMouseEventType.MouseDown) Down = true;
            else if (type == UIMouseEventType.MouseUp && Down)
            {
                Down = false;
                Toggle?.Invoke();
                HITVM.Get().PlaySoundEvent(UISounds.Click);
            }
            Invalidate();
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || Art == null) return;
            int fw = Art.Width / 6;
            bool on = Get != null && Get();
            int frame = Disabled ? 4 + (on ? 1 : 0) : (Hover ? 2 : 0) + (on ? 1 : 0);
            DrawLocalTexture(batch, Art, new Rectangle(frame * fw, 0, fw, Art.Height), Vector2.Zero, Vector2.One);
        }
    }

    /// <summary>
    /// cWinTriRadio's one 80x20 logical control. It composes three 20px
    /// cTSWinBtn children at x=0/30/60. cWinTriText is mounted separately.
    /// </summary>
    public class UIOriginalOptionTriRadio : UIElement
    {
        private readonly Texture2D Art;
        private readonly Func<int> Get;
        private readonly Action<int> Set;
        private int Hover = -1;
        private int Down = -1;
        public bool Disabled;
        public int FrameWidth { get { return Art != null ? Art.Width / 6 : 20; } }
        private Vector2 _size;
        public override Vector2 Size { get { return _size; } set { _size = value; } }

        public UIOriginalOptionTriRadio(Texture2D art, Func<int> get, Action<int> set)
        {
            Art = art; Get = get; Set = set;
            Size = new Vector2(80, 20);
            ListenForMouse(new Rectangle(0, 0, 80, 20), Mouse);
        }

        private int Hit(UpdateState state)
        {
            var p = GetMousePosition(state.MouseState);
            if (p.X >= 0 && p.X < 20) return 0;
            if (p.X >= 30 && p.X < 50) return 1;
            if (p.X >= 60 && p.X < 80) return 2;
            return -1;
        }

        private void Mouse(UIMouseEventType type, UpdateState state)
        {
            if (Disabled) return;
            if (type == UIMouseEventType.MouseOver) Hover = Hit(state);
            else if (type == UIMouseEventType.MouseOut) { Hover = -1; Down = -1; }
            else if (type == UIMouseEventType.MouseDown) { Down = Hit(state); Hover = Down; }
            else if (type == UIMouseEventType.MouseUp && Down >= 0)
            {
                int hit = Hit(state);
                if (hit == Down)
                {
                    Set?.Invoke(hit);
                    HITVM.Get().PlaySoundEvent(UISounds.Click);
                }
                Down = -1;
            }
            Invalidate();
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (Hover >= 0) Hover = Hit(state);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || Art == null) return;
            int fw = Art.Width / 6;
            int selected = Math.Max(0, Math.Min(2, Get != null ? Get() : 0));
            int[] x = { 0, 30, 60 };
            for (int i = 0; i < 3; i++)
            {
                bool on = selected == i;
                int frame = Disabled ? 4 + (on ? 1 : 0) : (Hover == i ? 2 : 0) + (on ? 1 : 0);
                DrawLocalTexture(batch, Art, new Rectangle(frame * fw, 0, fw, Art.Height),
                    new Vector2(x[i], 0), Vector2.One);
            }
        }
    }

    // ---- the original volume slider: Off track + On fill, 0..10 -------------
    public class UIOptSlider : UIElement
    {
        private Texture2D _off, _on;
        private bool _down;
        public float Value; // 0..10
        public event Action<float> OnChange;
        public const float LogicalWidth = 130f;
        private Vector2 _size;
        public override Vector2 Size { get { return _size; } set { _size = value; } }

        public UIOptSlider(Texture2D off, Texture2D on)
        {
            _off = off; _on = on;
            Size = new Vector2(LogicalWidth, off != null ? off.Height : 14);
            ListenForMouse(new Rectangle(0, 0, (int)Size.X, (int)Size.Y), SliderMouse);
        }

        // R215 gate probe: the engine's piece width (sheet width / 4 = 11) —
        // caps and thumb paint at this natural size, only the middle stretches.
        public int PieceWidthForProbe { get { return _off != null ? Math.Max(1, _off.Width / 4) : 0; } }

        // R216 gate probe: the middle piece's XNA scale MULTIPLIER. DrawLocalTexture's
        // scale multiplies the source rect (R215 passed the pixel width as the
        // multiplier — an 11px sample at scale 108 spanned the screen). The
        // correct multiplier stretches the sample to exactly w − 2·pw.
        public float MiddleScaleXForProbe
        {
            get
            {
                var pw = PieceWidthForProbe;
                var w = (int)Size.X;
                return (pw < 1 || w < 2 * pw) ? 0f : (w - 2 * pw) / (float)pw;
            }
        }

        private void SliderMouse(UIMouseEventType type, UpdateState state)
        {
            if (type == UIMouseEventType.MouseDown) { _down = true; ApplyMouse(state); }
            else if (type == UIMouseEventType.MouseUp) _down = false;
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (_down) ApplyMouse(state); // drag (UISlider idiom: apply while held)
        }

        private void ApplyMouse(UpdateState state)
        {
            var p = GetMousePosition(state.MouseState);
            var frac = Math.Max(0f, Math.Min(1f, p.X / Size.X));
            var v = (float)Math.Round(frac * 10.0);
            if (v != Value) { Value = v; OnChange?.Invoke(v); Invalidate(); }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || _off == null) return;
            // R215 engine law (cTSWinSlider::TSPaint @0x52c7c0 + SetImage
            // @0x52bbf0): the 44x14 sheet is a 4-piece strip (piece = w/4 =
            // 11px) — [left cap][middle sample][right cap][thumb]. The caps
            // and thumb paint at natural size; only the middle stretches
            // across the logical width (the 130 directive). There is NO
            // fractional fill: the thumb piece marks the value (bright in
            // the on-sheet, a dim slot in the off-sheet — the two sheets
            // differ only there).
            int pw = Math.Max(1, _off.Width / 4);
            int w = (int)Size.X;
            DrawLocalTexture(batch, _off, new Rectangle(0, 0, pw, _off.Height), Vector2.Zero, Vector2.One);
            // R216: DrawLocalTexture's scale is a SpriteBatch MULTIPLIER, not a
            // pixel width — divide the trough's pixel width by the piece width
            // (R215 passed 108 as the multiplier: an 11px sample stretched to
            // 1188px, the whole-screen slider).
            DrawLocalTexture(batch, _off, new Rectangle(pw, 0, pw, _off.Height),
                new Vector2(pw, 0), new Vector2((w - 2 * pw) / (float)pw, 1f));
            DrawLocalTexture(batch, _off, new Rectangle(2 * pw, 0, pw, _off.Height),
                new Vector2(w - pw, 0), Vector2.One);
            if (_on != null)
            {
                // Thumb travel: linear across the free width between the
                // caps (CalculateThumbPosition @0x52bff0 not fully decoded —
                // the linear mapping is the disclosed model).
                float travel = Math.Max(0f, w - 2 * pw - pw);
                float tx = pw + (Math.Max(0f, Math.Min(10f, Value)) / 10f) * travel;
                DrawLocalTexture(batch, _on, new Rectangle(3 * pw, 0, pw, _on.Height),
                    new Vector2((float)Math.Round(tx), 0), Vector2.One);
            }
        }
    }

    /// <summary>
    /// Product::DrawIcon's type-1 analogue for native live-popup ObjSelectors.
    /// A temporary out-of-world group supplies the authored DGRP/multitile
    /// components to the same compositor used by the original buy popup.
    /// The returned texture is standalone and owned by the caller.
    /// </summary>
    public static class UIOriginalPopupObjectRenderer
    {
        public static Texture2D Render(TS1GameScreen game, uint guid)
        {
            if (game?.LotControl?.vm == null || game.LotControl.World == null
                || guid == 0 || guid == uint.MaxValue) return null;
            FSO.SimAntics.Entities.VMMultitileGroup ghost = null;
            var vm = game.LotControl.vm;
            try
            {
                ghost = vm.Context.CreateObjectInstance(guid,
                    FSO.LotView.Model.LotTilePos.OUT_OF_WORLD,
                    FSO.LotView.Model.Direction.NORTH, true);
                if (ghost == null || ghost.Objects.Count == 0) return null;
                var components = new FSO.LotView.Components.ObjectComponent[ghost.Objects.Count];
                for (int i = 0; i < ghost.Objects.Count; i++)
                {
                    components[i] = ghost.Objects[i].WorldUI as FSO.LotView.Components.ObjectComponent;
                    if (components[i] == null) return null;
                }
                return game.LotControl.World.GetObjectThumb(components,
                    ghost.GetBasePositions(), GameFacade.GraphicsDevice);
            }
            catch { return null; }
            finally
            {
                if (ghost != null)
                {
                    try { ghost.Delete(vm.Context); }
                    catch { }
                }
            }
        }
    }

    // ---- the original cWinLivePopup: tiled chrome + pane art + canon text ---
    public class UIOptionAboutPopup : UIContainer
    {
        public Texture2D Art; public Texture2D Art2;
        public string Title; public string Body; public string Member;
        public UIOriginalOptionsPanel.OptRow Row;
        public Rectangle? ArtSource;
        public Rectangle? Art2Source;
        public Rectangle? ArtDrawSource;
        public Rectangle? Art2DrawSource;
        public bool UsesArtPane;
        public bool UsesLeftArtPane;
        public bool UsesRightArtPane;
        public bool SplitArtPanes;
        public readonly uint[] ObjectGuids = new uint[2];
        public readonly int[] ObjectScales = new int[2];
        public Point CanonPos;
        public const int WindowWidth = 559;
        public const int MinimumHeight = 127;
        public const int ArtPaneWidth = 168;
        public const int RightArtPaneLeft = 372;
        public const int RightArtPaneRight = 540;
        public const int TextLeft = 178;
        public const int TextRight = 545;
        public const int TextWidth = TextRight - TextLeft;
        public const int NoPaneTextLeft = 15;
        public const int RightPaneTextRight = 382;
        public const int TitleTop = 12;
        public const int BodyTop = 31;
        public const int BodyLineHeight = 17;
        public const int ArtScale = 1;
        public int WrappedBodyLines;
        public Point ArtPosition;
        public Point Art2Position;
        public Vector2 ArtDrawScale = Vector2.One;
        public Vector2 Art2DrawScale = Vector2.One;
        public UIOriginalText TitleText;
        public UIOriginalParagraph BodyParagraph;
        private Vector2 _size;
        public override Vector2 Size { get { return _size; } set { _size = value; } }

        public UIOptionAboutPopup(UIOriginalOptionsPanel owner, UIOriginalOptionsPanel.OptRow row, Texture2D art)
        {
            Art = art; Title = row.AboutTitle; Body = row.AboutBody; Member = row.PopupMember; Row = row;
            ConfigurePanes(art != null, false);
            var titleFont = OriginalGlyphFont.LoadByIndex(10, GameFacade.GraphicsDevice);
            var bodyFont = OriginalGlyphFont.LoadByIndex(9, GameFacade.GraphicsDevice);
            var textLeft = CurrentTextLeft;
            var textWidth = CurrentTextRight - textLeft;
            WrappedBodyLines = CountWrappedLines(Body, bodyFont, textWidth);
            int titleHeight = titleFont != null ? titleFont.LineHeight : 19;
            int height = Math.Max(MinimumHeight,
                24 + titleHeight + WrappedBodyLines * BodyLineHeight);
            Size = new Vector2(WindowWidth, height);

            UpdateArtLayout(height);

            TitleText = new UIOriginalText(Title, titleFont)
            {
                Position = new Vector2(textLeft, TitleTop),
                Size = new Vector2(textWidth, titleHeight),
                Color = new Color(0xC3, 0xCD, 0xCD, 0xFF),
            };
            Add(TitleText);

            if (bodyFont != null)
            {
                BodyParagraph = new UIOriginalParagraph(bodyFont)
                {
                    Text = Body,
                    MaxWidth = textWidth,
                    LineHeight = BodyLineHeight,
                    Position = new Vector2(textLeft, BodyTop),
                    TextColor = new Color(0xC3, 0xCD, 0xCD, 0xFF),
                };
                Add(BodyParagraph);
            }
        }

        /// <summary>
        /// Retarget the native shared live-popup client without replacing the
        /// popup window. cWinPeople uses this path when a second relationship
        /// card is selected while the first card's popup is still visible.
        /// </summary>
        public void SetContent(string title, string body)
        {
            Title = title ?? "";
            Body = body ?? "";
            if (Row != null)
            {
                Row.AboutTitle = Title;
                Row.AboutBody = Body;
            }
            var titleFont = TitleText?.Font;
            var bodyFont = BodyParagraph?.Font;
            var textLeft = CurrentTextLeft;
            var textWidth = CurrentTextRight - textLeft;
            WrappedBodyLines = CountWrappedLines(Body, bodyFont, textWidth);
            int titleHeight = titleFont != null ? titleFont.LineHeight : 19;
            int height = Math.Max(MinimumHeight,
                24 + titleHeight + WrappedBodyLines * BodyLineHeight);
            Size = new Vector2(WindowWidth, height);

            UpdateArtLayout(height);
            if (TitleText != null)
            {
                TitleText.Text = Title;
                TitleText.Position = new Vector2(textLeft, TitleTop);
                TitleText.Size = new Vector2(textWidth, titleHeight);
            }
            if (BodyParagraph != null)
            {
                BodyParagraph.Text = Body;
                BodyParagraph.Position = new Vector2(textLeft, BodyTop);
                BodyParagraph.MaxWidth = textWidth;
            }
            Invalidate();
        }

        /// <summary>
        /// Retarget a bitmap-backed live-popup client. Null deliberately
        /// clears the previous bitmap when Job switches from Career to one of
        /// its text-only summary clients.
        /// </summary>
        public void SetContent(string title, string body, Texture2D art, string member)
        {
            Art = art;
            Art2 = null;
            ArtSource = null;
            Art2Source = null;
            ConfigurePanes(art != null, false);
            ObjectGuids[0] = 0;
            ObjectGuids[1] = 0;
            ObjectScales[0] = 0;
            ObjectScales[1] = 0;
            Member = member ?? "";
            if (Row != null) Row.PopupMember = Member;
            SetContent(title, body);
        }

        /// <summary>
        /// Job and motive clients clear the popup bitmap and install up to two
        /// ObjSelectors. Native pane 0 is x=0..168 and pane 1 is x=372..540,
        /// flanking (not subdividing) the text column. Keep exact GUID and
        /// special-scale identity while the port uses authored catalog
        /// thumbnails in place of Product::DrawIcon's world-sprite renderer.
        /// </summary>
        public void SetObjectContent(string title, string body,
            Texture2D art, uint guid, Texture2D art2, uint guid2,
            int scale = 0, int scale2 = 0,
            bool artIsPairedBitmap = false, bool art2IsPairedBitmap = false)
        {
            Art = art;
            Art2 = art2;
            ArtSource = ObjectArtSource(art, artIsPairedBitmap);
            Art2Source = ObjectArtSource(art2, art2IsPairedBitmap);
            ConfigurePanes(guid != 0, guid2 != 0);
            ObjectGuids[0] = guid;
            ObjectGuids[1] = guid2;
            ObjectScales[0] = scale;
            ObjectScales[1] = scale2;
            Member = "";
            if (Row != null) Row.PopupMember = "";
            SetContent(title, body);
        }

        /// <summary>
        /// Switch the current native live-popup client between an art-pane
        /// client (options, relationship, personality traits) and a text-only
        /// client (for example the zodiac label) without replacing the popup.
        /// </summary>
        public void SetArt(Texture2D art, Rectangle? source = null, bool reservePane = true)
        {
            Art = art;
            Art2 = null;
            ArtSource = source;
            Art2Source = null;
            ConfigurePanes(reservePane, false);
            ObjectGuids[0] = 0;
            ObjectGuids[1] = 0;
            ObjectScales[0] = 0;
            ObjectScales[1] = 0;
            SetContent(Title, Body);
        }

        private static Rectangle? ObjectArtSource(Texture2D art, bool pairedBitmap)
        {
            if (art == null) return null;
            // TS1 catalog product BMPs have two equal horizontal states. An
            // ObjSelector shows the normal state, never both states at once.
            // The flag is explicit: a rendered world thumb may coincidentally
            // have a 2:1 aspect ratio and must not be mistaken for a sheet.
            if (pairedBitmap && art.Width == art.Height * 2)
                return new Rectangle(0, 0, art.Width / 2, art.Height);
            return new Rectangle(0, 0, art.Width, art.Height);
        }

        private static int CountWrappedLines(string text, OriginalGlyphFont font, int maxWidth)
        {
            if (font == null || string.IsNullOrEmpty(text)) return 0;
            int count = 0;
            foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = "";
                foreach (var word in paragraph.Split(' '))
                {
                    var candidate = line.Length == 0 ? word : line + " " + word;
                    if (font.Measure(candidate) <= maxWidth || line.Length == 0) line = candidate;
                    else { count++; line = word; }
                }
                count++;
            }
            return count;
        }

        private int CurrentTextLeft => UsesLeftArtPane ? TextLeft : NoPaneTextLeft;
        private int CurrentTextRight => UsesRightArtPane ? RightPaneTextRight : TextRight;

        private void ConfigurePanes(bool left, bool right)
        {
            UsesLeftArtPane = left;
            UsesRightArtPane = right;
            UsesArtPane = left || right;
            SplitArtPanes = left && right;
        }

        private static Rectangle ComputePaneRectangle(int windowHeight, bool rightPane)
        {
            // RebuildBuffer's two picture panes flank the copy; they are not
            // adjacent half-panes. English rects begin as (0,0,168,127) and
            // (372,0,540,127). DrawPane centers natural-size art within them.
            int available = windowHeight;
            var language = FSO.Client.GlobalSettings.Default.LanguageCode;
            var doubleByte = language == 15 || language == 17
                || language == 18 || language == 20;
            if ((!rightPane && GameFacade.GraphicsDevice.Viewport.Width == 1024)
                || (rightPane && doubleByte)) available -= 50;
            int paneTop = 0;
            int paneBottom = MinimumHeight;
            if (available > MinimumHeight)
            {
                paneTop = 5;
                paneBottom = 122 + 2 * ((available - MinimumHeight) / 2);
            }
            var paneLeft = rightPane ? RightArtPaneLeft : 0;
            return new Rectangle(paneLeft, paneTop, ArtPaneWidth, paneBottom - paneTop);
        }

        private static float ObjectScaleFactor(int scale)
        {
            // RenderParam scale 3/2/1 is TS1 near/medium/far. The port's
            // world thumbnail is rendered at near, so the other authored
            // discrete levels correspond to half and quarter dimensions.
            if (scale >= 3) return 1f;
            if (scale == 2) return 0.5f;
            return 0.25f;
        }

        private static bool ObjectScaleFits(Rectangle source, Rectangle pane, int scale)
        {
            var factor = ObjectScaleFactor(scale);
            // DrawIconic's automatic path requires three free pixels on each
            // half-axis before accepting the current 3 -> 2 -> 1 scale.
            return source.Width * factor <= pane.Width - 6
                && source.Height * factor <= pane.Height - 6;
        }

        private static void ComputeArtLayout(Texture2D art, Rectangle? source,
            int windowHeight, bool rightPane, bool objectSelector, int specialScale,
            out Point position, out Vector2 drawScale, out Rectangle? drawSource)
        {
            var pane = ComputePaneRectangle(windowHeight, rightPane);
            if (art == null)
            {
                position = new Point(pane.X + pane.Width / 2, pane.Y + pane.Height / 2);
                drawScale = Vector2.One;
                drawSource = null;
                return;
            }

            var src = source ?? new Rectangle(0, 0, art.Width, art.Height);
            if (objectSelector)
            {
                var scale = specialScale;
                if (scale == 0)
                {
                    scale = 3;
                    while (scale > 1 && !ObjectScaleFits(src, pane, scale)) scale--;
                }
                var factor = ObjectScaleFactor(scale);
                drawScale = new Vector2(factor, factor);
            }
            else
            {
                // Bitmap panes shrink independently to the pane, but never
                // enlarge their authored source rectangle.
                drawScale = new Vector2(
                    Math.Min(1f, pane.Width / (float)Math.Max(1, src.Width)),
                    Math.Min(1f, pane.Height / (float)Math.Max(1, src.Height)));
            }

            var drawnWidth = Math.Max(1, (int)Math.Round(src.Width * drawScale.X));
            var drawnHeight = Math.Max(1, (int)Math.Round(src.Height * drawScale.Y));
            position = new Point(pane.X + (pane.Width - drawnWidth) / 2,
                pane.Y + (pane.Height - drawnHeight) / 2);
            drawSource = src;

            // A fixed object scale is allowed to overflow, but DrawPane clips
            // it to the 168px viewport. Crop the source equivalently before
            // handing it to the UI sprite batch.
            var left = Math.Max(0, (int)Math.Ceiling((pane.Left - position.X) / drawScale.X));
            var top = Math.Max(0, (int)Math.Ceiling((pane.Top - position.Y) / drawScale.Y));
            var right = Math.Max(0, (int)Math.Ceiling(
                (position.X + drawnWidth - pane.Right) / drawScale.X));
            var bottom = Math.Max(0, (int)Math.Ceiling(
                (position.Y + drawnHeight - pane.Bottom) / drawScale.Y));
            if (left != 0 || top != 0 || right != 0 || bottom != 0)
            {
                var clippedWidth = Math.Max(0, src.Width - left - right);
                var clippedHeight = Math.Max(0, src.Height - top - bottom);
                drawSource = clippedWidth == 0 || clippedHeight == 0 ? (Rectangle?)null
                    : new Rectangle(src.X + left, src.Y + top, clippedWidth, clippedHeight);
                position = new Point(position.X + (int)Math.Round(left * drawScale.X),
                    position.Y + (int)Math.Round(top * drawScale.Y));
            }
        }

        private void UpdateArtLayout(int windowHeight)
        {
            ComputeArtLayout(Art, ArtSource, windowHeight, false,
                ObjectGuids[0] != 0, ObjectScales[0],
                out ArtPosition, out ArtDrawScale, out ArtDrawSource);
            ComputeArtLayout(Art2, Art2Source, windowHeight, true,
                ObjectGuids[1] != 0, ObjectScales[1],
                out Art2Position, out Art2DrawScale, out Art2DrawSource);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            // cWinLivePopup obtains SystemBMP(5), which maps to the 36x36
            // PopupInfoTiles sheet. RebuildBuffer divides it into 12px thirds
            // and tiles all nine cells over the complete dynamic window.
            UIOriginalDialogChrome.DrawPictureWindow(this, batch, 0, 0,
                (int)Size.X, (int)Size.Y);
            if (Art != null && ArtDrawSource.HasValue)
                DrawLocalTexture(batch, Art, ArtDrawSource,
                    ArtPosition.ToVector2(), ArtDrawScale);
            if (Art2 != null && Art2DrawSource.HasValue)
                DrawLocalTexture(batch, Art2, Art2DrawSource,
                    Art2Position.ToVector2(), Art2DrawScale);
            base.Draw(batch); // title label + wrapped canon body
        }
    }
}
