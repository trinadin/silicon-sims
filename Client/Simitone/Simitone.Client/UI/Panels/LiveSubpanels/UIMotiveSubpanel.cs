using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.SimAntics.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSO.Common.Rendering.Framework.Model;

namespace Simitone.Client.UI.Panels.LiveSubpanels
{
    public class UIMotiveSubpanel : UISubpanel
    {
        public const int NATIVE_HOST_W = 280;
        public const int NATIVE_HOST_H = 100;
        public UIMotiveBar[] MotiveDisplays;
        public UIOriginalLiveGauge Gauge;
        public UILabel GaugeCaption;
        public UIOriginalTrackButton TrackButton;

        // R132: is the camera currently following the selected Sim (the
        // engine's TrackPerson state)? The engine mounts the kTrackingTarget
        // crosshair over the tracked person's portrait; the port's equivalent
        // anchor is WorldState.ScrollAnchor (re-centered every frame by
        // World.Update, cleared by manual camera input — the same
        // stop-on-drag as the original).
        public bool IsTracking
        {
            get
            {
                var world = Game.LotControl != null ? Game.LotControl.World : null;
                var av = Game.SelectedAvatar;
                if (world == null || av == null || av.WorldUI == null) return false;
                return world.State.ScrollAnchor == (av.WorldUI as FSO.LotView.Components.AvatarComponent);
            }
        }

        // ROUND-114: motive names in ORIGINAL strings + glyphs. The port read these from an
        // FSO-authored .cst (f102); the ORIGINAL corpus is Live.iff STR# 130 'Motives' —
        // name indices per motive: Hunger 1, Comfort 5, Hygiene 9, Bladder 13, Energy 3,
        // Fun 7, Social 11, Room 15 (the table pairs each name with its popup description;
        // the port keeps ITS bar order and maps each bar's motive to the original name).
        // Twins render in the caption table _10; modern labels stay as pre-IFF fallback.
        public static int MotiveNamesTwinned = 0;
        private UILabel[] NameLabels;
        public UIOriginalText[] NameTwins;
        private static readonly int[] MotiveStringIndex = new int[] { 1, 5, 9, 13, 3, 7, 11, 15 };
        // R144 desktop: Live.iff STR# 130 name indices in GAUGE GRID order
        // (Hunger,Energy,Comfort,Fun,Hygiene,Social,Bladder,Room — pairs are
        // name@2k+1: Hunger 1, Energy 3, Comfort 5, Fun 7, Hygiene 9, Social 11,
        // Bladder 13, Room 15).
        public static readonly int[] GaugeStringIndex = new int[] { 1, 3, 5, 7, 9, 11, 13, 15 };

        // R144: the DESKTOP motive host per r144/toolbar-law.md section 1.5 —
        // engine host rect people (300,50,580,150) = MainPanel-local (300,0);
        // 8 gauges 100x20 flow from host-local (0,21), x-pitch 100, row pitch
        // 19, wrap at 200 -> 2 COLUMNS x 4 ROWS (motive ids 0x5a45b8 order
        // {Hunger,Energy,Comfort,Fun,Hygiene,Social,Bladder,Room}). Fill law:
        // clamp(round(raw*60/200), 0, 60) px of the 100px width.
        public const int GAUGE_HOST_X = 300;
        public const int GAUGE_X0 = 0, GAUGE_Y0 = 21, GAUGE_PITCH_X = 100, GAUGE_PITCH_Y = 19;
        public static readonly VMMotive[] GaugeMotives = {
            VMMotive.Hunger, VMMotive.Energy, VMMotive.Comfort, VMMotive.Fun,
            VMMotive.Hygiene, VMMotive.Social, VMMotive.Bladder, VMMotive.Room };
        public UIOriginalMotiveGauge[] GaugeGrid;
        public UIOriginalText HostTitle;
        public UIOptionAboutPopup CurrentPopup;
        public int PopupMotive = -1;
        // R187: cWinPeople's static constructor writes these exact selector
        // pairs to BSS 0x93390 (human) and 0x93310 (pet). cWinMotive passes
        // each pair to its cWinLivePopupClient in gauge-grid order. A zero pair
        // is significant: Social is the native text-only popup client.
        public static readonly uint[][] HumanPopupObjectGuids =
        {
            new uint[] { 0xB94755DFu, 0xFF9F18E0u },
            new uint[] { 0x82E04C5Bu, 0xBF137195u },
            new uint[] { 0xD97681DBu, 0x7CB11019u },
            new uint[] { 0x5CDC712Fu, 0x481A74ECu },
            new uint[] { 0x85E00942u, 0x85E02A40u },
            new uint[] { 0u, 0u },
            new uint[] { 0x84E0774Cu, 0u },
            new uint[] { 0x7F907075u, 0xBC3BA088u },
        };
        public static readonly uint[][] PetPopupObjectGuids =
        {
            new uint[] { 0x8EEC19F7u, 0xFFC0EA58u },
            new uint[] { 0xBC537C52u, 0x96CB67AEu },
            new uint[] { 0xE23F9020u, 0x2B4502A2u },
            new uint[] { 0x914E5A74u, 0xB26AB256u },
            new uint[] { 0x2ECDB068u, 0u },
            new uint[] { 0u, 0u },
            new uint[] { 0x39CCF441u, 0xBEDD7B26u },
            new uint[] { 0x7F907075u, 0xBC3BA088u },
        };
        // BSS 0x93350/0x932d0: only the second selector in row zero uses
        // cWinLivePopupClient::SetSpecialScale(..., 2); every other slot is 0.
        public static readonly int[][] PopupObjectScales =
        {
            new int[] { 0, 2 },
            new int[] { 0, 0 }, new int[] { 0, 0 }, new int[] { 0, 0 },
            new int[] { 0, 0 }, new int[] { 0, 0 }, new int[] { 0, 0 },
            new int[] { 0, 0 },
        };
        public readonly uint[] PopupObjectGuids = new uint[2];
        public readonly int[] PopupObjectScale = new int[2];
        private readonly Dictionary<uint, Texture2D> PopupObjectThumbs =
            new Dictionary<uint, Texture2D>();
        private bool? LastPetCorpus;

        private void InitDesktop(TS1GameScreen game)
        {
            var gd = FSO.Client.GameFacade.GraphicsDevice;

            // keep the mobile-era controls alive (hidden) so the uigauge/uirate
            // checks' constructed-panel contracts hold; per the r144 law the
            // VERTICAL mood gauge visual does not exist on the toolbar — the
            // LiveGadget plaque is the tab-column backdrop (UIOriginalPeopleChrome)
            // and the mood trend shows as Greenbars/Redbars arrows.
            Gauge = new UIOriginalLiveGauge();
            Gauge.Visible = false;
            Add(Gauge);
            GaugeCaption = new UILabel();
            GaugeCaption.Caption = GameFacade.Strings.GetString("154", "15"); // 'Mood Rating'
            GaugeCaption.Visible = false;
            Add(GaugeCaption);

            // R200: the follow-Sim crosshair is NOT motive-panel chrome. The
            // engine (cWinPeople::TrackPerson 0x28bf00) mounts kTrackingTarget
            // 4601 over the TRACKED sim's webcam portrait in the people
            // window (UIOriginalPeopleChrome), toggled by webcam clicks and
            // RETURN-in-build — the old hidden TrackButton mount here is
            // retired (the mobile/touch ctor below keeps its button by
            // design). See r200/r200-trackperson-law.md.

            // sub-panel title: cTSWinText font[11] anchored host-local {5,0}
            // ("Motives" = Live.iff STR# 130 [0]).
            var titleFont = OriginalGlyphFont.LoadByIndex(11, gd);
            var titleStr = Simitone.Client.UI.Model.OriginalLiveStrings.Motive(0);
            if (titleFont != null && titleStr != null)
            {
                HostTitle = new UIOriginalText(titleStr, titleFont) { Position = new Vector2(5, 0) };
                Add(HostTitle);
            }

            MotiveDisplays = new UIMotiveBar[8];   // kept null-safe for mobile-era callers
            GaugeGrid = new UIOriginalMotiveGauge[8];
            for (int i = 0; i < 8; i++)
            {
                var index = i;
                var g = new UIOriginalMotiveGauge(GaugeMotives[i],
                    gauge => ToggleMotivePopup(index));
                // flow: (0,21),(100,21),(0,40),(100,40)... — 2 cols x 4 rows
                g.Position = new Vector2(GAUGE_X0 + (i % 2) * GAUGE_PITCH_X, GAUGE_Y0 + (i / 2) * GAUGE_PITCH_Y);
                Add(g);
                GaugeGrid[i] = g;
            }

            // mount the gauge name twins EAGERLY (font[8] + Live.iff are both
            // ready once a lot is live; the lazy Update mount only fires on a
            // game-loop tick, which panels freshly swapped in never get in the
            // autotest harness).
            var efont = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadByIndex(8, gd);
                var em0 = Simitone.Client.UI.Model.OriginalLiveStrings.Motive(1);
                if (efont != null && efont.Atlas != null && em0 != null)
                {
                    NameTwins = new UIOriginalText[8];
                    for (int i = 0; i < 8; i++)
                    {
                    var orig = Simitone.Client.UI.Model.OriginalLiveStrings.Motive(GaugeStringIndex[i]);
                    GaugeGrid[i].SetName(orig, efont);
                        NameTwins[i] = GaugeGrid[i].Label;
                    }
                    // Leave the corpus sentinel unset. The first Update must
                    // install both the visible names and the matching native
                    // tooltip descriptions for whichever species is selected;
                    // eagerly declaring "human" here skipped that description
                    // pass for the normal startup case.
                    LastPetCorpus = null;
                    MotiveNamesTwinned++;
                    Invalidate();
                }
        }

        public UIMotiveSubpanel(TS1GameScreen game) : base (game)
        {
            if (game.Desktop)
            {
                InitDesktop(game);
                return;
            }

            // R131: the ORIGINAL live-tab mood gauge on the LEFT of the needs
            // (engine composition: LiveGadget backdrop + Green/Redbar fill —
            // see UIOriginalLiveGauge). The original panel is portrait+gauge
            // left / needs right (LivePatch.bmp 213x153 covers that column).
            // The caption is STR# 154 'MiscStrings' [15] 'Mood Rating' — the
            // ratings family House/Friend/Job/Mood = [12]/[13]/[14]/[15].
            Gauge = new UIOriginalLiveGauge();
            Gauge.Position = new Vector2(10, 36);
            Add(Gauge);

            GaugeCaption = new UILabel();
            GaugeCaption.CaptionStyle = GaugeCaption.CaptionStyle.Clone();
            GaugeCaption.CaptionStyle.Size = 15;
            GaugeCaption.CaptionStyle.Color = UIStyle.Current.Text;
            GaugeCaption.Alignment = FSO.Client.UI.Framework.TextAlignment.Bottom;
            GaugeCaption.Size = new Vector2(1);
            GaugeCaption.Position = new Vector2(63, 10);
            GaugeCaption.Caption = GameFacade.Strings.GetString("154", "15"); // 'Mood Rating'
            Add(GaugeCaption);

            // R132: the follow-Sim toggle (kTrackingTarget 4601 crosshair,
            // cpanel\People\TrackingTarget.bmp 45x45). Engine: TrackPerson
            // attaches the crosshair + the camera layer follows; StopTracking
            // detaches. Port: WorldState.ScrollAnchor on the selected Sim —
            // click toggles, drag/scroll cancels (engine-faithful). The
            // button centers its texture on Position (UIElasticButton).
            try
            {
                var crossTex = Simitone.Client.UI.Model.UIOriginal.EnsureResolved("cpanel\\People\\TrackingTarget.bmp")?.Get(GameFacade.GraphicsDevice);
                if (crossTex != null)
                {
                    TrackButton = new UIOriginalTrackButton(crossTex);
                    TrackButton.Position = new Vector2(10 + 22, 8 + 22);
                    TrackButton.OnButtonClick += (b) => ToggleFollow();
                    Add(TrackButton);
                }
            }
            catch { }

            MotiveDisplays = new UIMotiveBar[8];
            NameLabels = new UILabel[8];
            for (int i=0;i<8;i++)
            {
                var d = new UIMotiveBar();
                // R141: the old grid (150px bars at 180px pitch) placed columns 3-4
                // at x=1068/1248 — OFF-SCREEN on a 1024-wide desktop, so half the
                // needs were invisible (survey uisurvey-live evidence). The original
                // needs block is 4x2; 100px bars at 130px pitch fit the ~582px
                // subpanel: 130 + 3*130 + 100 = 620 <= panel width at 1024.
                // R142: rows pitch 44 (labels 12/56, bars 28/72) so both rows fit
                // the original 100px toolbar bar (PanelBack.bmp height); the old
                // 60px pitch spilled row 2 past the bar's bottom edge.
                var vPitch = Game.Desktop ? 44 : 60;
                var vBase = Game.Desktop ? 28 : 30;
                d.Width = 80;
                d.Position = new Vector2(100 + (i%4)*105, vBase+(i/4) * vPitch);
                Add(d);
                MotiveDisplays[i] = d;

                var l = new UILabel();
                l.CaptionStyle = l.CaptionStyle.Clone();
                l.CaptionStyle.Size = 15;
                l.CaptionStyle.Color = UIStyle.Current.Text;
                l.Alignment = FSO.Client.UI.Framework.TextAlignment.Bottom;
                l.Size = new Vector2(1);
                l.Position = new Vector2(100 + (i % 4) * 105, (Game.Desktop ? 12 : 24) + (i / 4) * (Game.Desktop ? 44 : 60));
                l.Caption = GameFacade.Strings.GetString("f102", (i+1).ToString());
                Add(l);
                NameLabels[i] = l;
            }

        }

        public override void Update(UpdateState state)
        {
            // mount FIRST: UpdateMotives' no-avatar early-return would otherwise skip the
            // twin mount entirely on frames without a selection (r114p2/p3 honest FAIL).
            if (NameTwins == null)
            {
                var gd = FSO.Client.GameFacade.GraphicsDevice;
                var font = GaugeGrid != null
                    ? Simitone.Client.UI.Controls.OriginalGlyphFont.LoadByIndex(8, gd)   // engine label font[8]
                    : Simitone.Client.UI.Controls.OriginalGlyphFont.LoadCaption(gd);
                var m0 = Simitone.Client.UI.Model.OriginalLiveStrings.Motive(1);
                if (font != null && font.Atlas != null && m0 != null)
                {
                    NameTwins = new UIOriginalText[8];
                    if (GaugeGrid != null)
                    {
                        // desktop: the gauge labels ARE the twins (font[8] in-gauge)
                        for (int i = 0; i < 8; i++)
                        {
                            var orig = Simitone.Client.UI.Model.OriginalLiveStrings.Motive(GaugeStringIndex[i]);
                            GaugeGrid[i].SetName(orig, font);
                            NameTwins[i] = GaugeGrid[i].Label;
                        }
                        // the gauge labels are children of a CACHED container —
                        // mounting them after the fade-in cache build needs an
                        // explicit invalidate or they never appear.
                        Invalidate();
                    }
                    else
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            var orig = Simitone.Client.UI.Model.OriginalLiveStrings.Motive(MotiveStringIndex[i]);
                            NameTwins[i] = new UIOriginalText(orig ?? NameLabels[i].Caption, font) { Color = UIStyle.Current.Text };
                            NameTwins[i].Position = NameLabels[i].Position;
                            Add(NameTwins[i]);
                            NameLabels[i].Visible = false;
                        }
                    }
                    MotiveNamesTwinned++;
                }
            }
            UpdateMotives();
            if (TrackButton != null) TrackButton.TrackActive = IsTracking;
            base.Update(state);
            if (GaugeGrid == null && Opacity < 1)
            {
                if (DynamicOverlay.GetChildren().Count > 0)
                {
                    foreach (var m in MotiveDisplays)
                    {
                        DynamicOverlay.Remove(m);
                        Add(m);
                    }
                }
                Invalidate();
            } else if (GaugeGrid == null)
            {
                if (DynamicOverlay.GetChildren().Count == 0)
                {
                    foreach (var m in MotiveDisplays)
                    {
                        Remove(m);
                        DynamicOverlay.Add(m);
                        Invalidate();
                    }
                }
            }
        }

        private void UpdateMotives()
        {
            if (Game.SelectedAvatar == null) return;
            if (GaugeGrid != null)
            {
                // R184: native swaps the complete human/pet string corpus by
                // selected instance class while retaining the same grid/data
                // order. Keep captions and popup descriptions synchronized
                // when the player changes selection.
                var pet = Game.SelectedAvatar.IsPet;
                if (LastPetCorpus != pet)
                {
                    var font = OriginalGlyphFont.LoadByIndex(8, GameFacade.GraphicsDevice);
                    for (int i = 0; i < 8; i++)
                    {
                        var idx = GaugeStringIndex[i];
                        var name = pet ? OriginalLiveStrings.PetMotive(idx) : OriginalLiveStrings.Motive(idx);
                        var desc = pet ? OriginalLiveStrings.PetMotive(idx + 1) : OriginalLiveStrings.Motive(idx + 1);
                        GaugeGrid[i].SetName(name, font);
                        GaugeGrid[i].Tooltip = desc;
                        NameTwins[i] = GaugeGrid[i].Label;
                    }
                    LastPetCorpus = pet;
                    RetargetOpenPopup();
                    Invalidate();
                }

                // Raw [-100,100] -> the native 60x5 gauge strip.
                for (int i = 0; i < 8; i++)
                    GaugeGrid[i].RawValue = Game.SelectedAvatar.GetMotiveData(GaugeMotives[i]);
                Gauge.MoodValue = Game.SelectedAvatar.GetMotiveData(VMMotive.Mood);
                return;
            }
            MotiveDisplays[0].MotiveValue = Game.SelectedAvatar.GetMotiveData(VMMotive.Hunger);
            MotiveDisplays[1].MotiveValue = Game.SelectedAvatar.GetMotiveData(VMMotive.Comfort);
            MotiveDisplays[2].MotiveValue = Game.SelectedAvatar.GetMotiveData(VMMotive.Hygiene);
            MotiveDisplays[3].MotiveValue = Game.SelectedAvatar.GetMotiveData(VMMotive.Bladder);
            MotiveDisplays[4].MotiveValue = Game.SelectedAvatar.GetMotiveData(VMMotive.Energy);
            MotiveDisplays[5].MotiveValue = Game.SelectedAvatar.GetMotiveData(VMMotive.Fun);
            MotiveDisplays[6].MotiveValue = Game.SelectedAvatar.GetMotiveData(VMMotive.Social);
            MotiveDisplays[7].MotiveValue = Game.SelectedAvatar.GetMotiveData(VMMotive.Room);
            // R131: the gauge reads the SAME mood the engine's people panel does
            // (GetMotiveData(VMMotive.Mood), short [-100,100]).
            Gauge.MoodValue = Game.SelectedAvatar.GetMotiveData(VMMotive.Mood);
        }

        /// <summary>
        /// Native cWinPeople routes every motive button to one shared
        /// cWinLivePopup. Selecting another motive retargets that same window;
        /// selecting the active motive toggles it closed.
        /// </summary>
        private void ToggleMotivePopup(int index)
        {
            if (!Game.Desktop || GaugeGrid == null || index < 0 || index >= GaugeGrid.Length)
                return;
            if (CurrentPopup != null && PopupMotive == index)
            {
                CloseMotivePopup();
                return;
            }

            var gauge = GaugeGrid[index];
            var title = gauge?.Label?.Text ?? "";
            var body = gauge?.Tooltip ?? "";
            if (CurrentPopup == null)
            {
                var row = new UIOriginalOptionsPanel.OptRow
                {
                    AboutTitle = title,
                    AboutBody = body
                };
                CurrentPopup = new UIOptionAboutPopup(null, row, null);
                DynamicOverlay.Add(CurrentPopup);
            }

            PopupMotive = index;
            ApplyMotivePopupContent(index, title, body);
            PositionPopup();
        }

        internal void ToggleMotivePopupForProbe(int index) { ToggleMotivePopup(index); }

        private void RetargetOpenPopup()
        {
            if (CurrentPopup == null || GaugeGrid == null
                || PopupMotive < 0 || PopupMotive >= GaugeGrid.Length) return;
            var gauge = GaugeGrid[PopupMotive];
            ApplyMotivePopupContent(PopupMotive,
                gauge?.Label?.Text ?? "", gauge?.Tooltip ?? "");
            PositionPopup();
        }

        private void ApplyMotivePopupContent(int index, string title, string body)
        {
            if (CurrentPopup == null || index < 0 || index >= GaugeGrid.Length) return;
            var pet = Game.SelectedAvatar?.IsPet == true;
            var guids = (pet ? PetPopupObjectGuids : HumanPopupObjectGuids)[index];
            var scales = PopupObjectScales[index];
            PopupObjectGuids[0] = guids[0];
            PopupObjectGuids[1] = guids[1];
            PopupObjectScale[0] = scales[0];
            PopupObjectScale[1] = scales[1];

            if (guids[0] == 0 && guids[1] == 0)
            {
                // Social's client has no ObjSelectors. Let its copy use the
                // complete popup width instead of leaving an empty 168px pane.
                CurrentPopup.SetContent(title, body, null, "");
                return;
            }
            bool paired0;
            bool paired1;
            var art0 = PopupObjectArt(guids[0], out paired0);
            var art1 = PopupObjectArt(guids[1], out paired1);
            CurrentPopup.SetObjectContent(title, body,
                art0, guids[0], art1, guids[1], scales[0], scales[1],
                paired0, paired1);
        }

        private Texture2D PopupObjectArt(uint guid, out bool pairedBitmap)
        {
            pairedBitmap = false;
            if (guid == 0) return null;
            Texture2D thumb;
            if (PopupObjectThumbs.TryGetValue(guid, out thumb)) return thumb;
            thumb = UIOriginalPopupObjectRenderer.Render(Game, guid);
            if (thumb != null)
            {
                PopupObjectThumbs[guid] = thumb;
                return thumb;
            }
            try
            {
                var obj = FSO.Content.Content.Get()?.WorldObjects?.Get(guid);
                var fallback = obj?.Resource?.Get<FSO.Files.Formats.IFF.Chunks.BMP>(obj.OBJ.CatalogStringsID)
                    ?.GetTexture(GameFacade.GraphicsDevice);
                pairedBitmap = fallback != null && fallback.Width == fallback.Height * 2;
                return fallback;
            }
            catch { return null; }
        }

        private void PositionPopup()
        {
            if (CurrentPopup == null) return;
            CurrentPopup.Position = new Vector2(Size.X - CurrentPopup.Size.X,
                -CurrentPopup.Size.Y);
            CurrentPopup.CanonPos = new Point((int)CurrentPopup.X, (int)CurrentPopup.Y);
        }

        public void CloseMotivePopup()
        {
            if (CurrentPopup != null) DynamicOverlay.Remove(CurrentPopup);
            CurrentPopup = null;
            PopupMotive = -1;
            PopupObjectGuids[0] = 0;
            PopupObjectGuids[1] = 0;
            PopupObjectScale[0] = 0;
            PopupObjectScale[1] = 0;
        }

        public override void GameResized()
        {
            base.GameResized();
            PositionPopup();
        }

        public override void Kill()
        {
            CloseMotivePopup();
            foreach (var thumb in PopupObjectThumbs.Values)
            {
                try { thumb?.Dispose(); }
                catch { }
            }
            PopupObjectThumbs.Clear();
            base.Kill();
        }

        // R132: the engine's TrackPerson/StopTracking pair as one toggle —
        // anchor the camera on the selected Sim (follow), or clear the anchor
        // (stop). Manual camera input clears the anchor on its own (the
        // original's stop-on-drag); this is the explicit off switch.
        public void ToggleFollow()
        {
            var world = Game.LotControl != null ? Game.LotControl.World : null;
            var av = Game.SelectedAvatar;
            if (world == null || av == null || av.WorldUI == null) return;
            var anchor = av.WorldUI as FSO.LotView.Components.AvatarComponent;
            if (world.State.ScrollAnchor == anchor) world.State.ScrollAnchor = null;
            else world.State.ScrollAnchor = anchor;
        }
    }
}
