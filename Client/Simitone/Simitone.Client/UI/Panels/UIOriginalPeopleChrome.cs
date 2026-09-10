using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Screens;
using System;
using System.Linq;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// R144: the cWinPeople TOOLBAR CHROME (engine law decoded in
    /// tools/iff-dump/r144/toolbar-law.md section 1) — the pieces that stay
    /// on screen for EVERY live-mode category: the 8-webcam family strip,
    /// the LiveGadget plaque behind the category tabs, the 7 category tabs
    /// and the mood trend arrows. Coordinates are MainPanel-local: the
    /// original people window is (220, SH-150, W, SH) 150 tall and the
    /// toolbar band is its lower 100px, so people-local y - 50 == local y
    /// here (MainPanel sits at (220, SH-100)).
    ///
    /// Engine anchors (people-local -> local):
    /// - webcams: origin (9,55) 45x45, pitch 45, wrap x+45>189, 4 cols x 2 rows
    /// - LiveGadget 4911 (108x100) at (193,50)
    /// - tabs: Mood 4500 32x39 at (202,81); Relationship 4504 (269,54);
    ///   Job 4503 (269,85); House 4502 (269,116); Personality 4501 (237,85);
    ///   Interest 4505 (237,54); Gift 4507 (237,116) — 27x30 cells of 108x30
    ///   4x1 sheets
    /// - trend arrows: kGreenbars 4506 (205,53) / kRedbars 4510 (205,123),
    ///   27x25, 5px vertical reveal per trend unit (trend==0: both full,
    ///   2px-inset source).
    /// </summary>
    public class UIOriginalPeopleChrome : UIContainer
    {
        public TS1GameScreen Game;
        public UIOriginalWebcamButton[] Webcams = new UIOriginalWebcamButton[8];
        public UIImage LiveGadget;
        public UIOriginalTabButton[] Tabs = new UIOriginalTabButton[7];
        /// category ids in TAB ORDER (engine res-id order 0x5a4598): Mood,
        /// Relationship, Job, House, Personality, Interest, Gift — mapped to
        /// the port's subpanel ids; 6/7 = the R145 Interest/Gift band panels
        /// (engine people panels 6/7, r145/interest-gift-law.md).
        public static readonly int[] TabCategory = { 0, 3, 1, 5, 2, 6, 7 };
        public int ActiveTab = 0;
        public Action<int> OnCategorySelect;

        public Texture2D GreenBars, RedBars;
        private int Trend;                     // clamped native [-5,5] mood band

        public int TrendForProbe => Trend;

        // ==== R200: cWinPeople::TrackPerson / StopTracking (0x28bf00 / 0x288740) ====
        // The engine's tracked person is ONE global (TOC-0x70b8) — clicking a
        // webcam portrait (cWinPeople::TSOnCommand +0x224 select-when-different,
        // then the +0xfcc sender loop) or pressing RETURN in build mode (the
        // cDDDSimsView hotkey dispatcher 0x21761c -> 0x217a3c, gated
        // GetMode()==2) calls TrackPerson(person): a click on the ALREADY
        // tracked person takes the same-person branch and UNTRACKS; otherwise
        // the tracked portrait is marked — the webcam button is reparented
        // into the tracking container (this+0x240) at {0,0}, composing the
        // kTrackingTarget 4601 crosshair (cpanel\People\TrackingTarget.bmp
        // 45x45) over the 45x45 portrait — and the camera follows (global
        // 0x47cb10). StopTracking fires from CPState::SetSelectedPerson
        // (selection change), the UCP LEVEL buttons (cWinViewControl::
        // TSOnCommand +0x64/+0x9c, skipped when the level is unchanged),
        // manual camera input (EdgeDetectScroller), and the picker/PiP
        // surfaces. NOT from mode switches: tracking outlives the people
        // window's visibility.
        public static VMAvatar TrackedAvatar;
        public static Texture2D TrackingTarget;        // the 45x45 crosshair
        public static int TrackAttaches, TrackDetaches;   // gate instrumentation

        public void TrackPerson(VMAvatar av)
        {
            if (av == null || TrackedAvatar == av) { StopTracking(); return; }   // same-person branch
            StopTracking();                                                        // pre-clear (0x28bf20)
            TrackedAvatar = av;
            TrackAttaches++;
            // 0x47cb10: the camera follows the tracked person (the port's
            // ScrollAnchor — re-centered every frame, cleared by manual input).
            var world = Game != null && Game.LotControl != null ? Game.LotControl.World : null;
            var comp = av.WorldUI as FSO.LotView.Components.AvatarComponent;
            if (world != null && world.State != null && comp != null)
                world.State.ScrollAnchor = comp;
        }

        public static void StopTracking()
        {
            if (TrackedAvatar != null) TrackDetaches++;
            TrackedAvatar = null;
            var game = FSO.Client.GameFacade.Screens != null
                ? FSO.Client.GameFacade.Screens.CurrentUIScreen as TS1GameScreen : null;
            var world = game != null && game.LotControl != null ? game.LotControl.World : null;
            if (world != null && world.State != null) world.State.ScrollAnchor = null;
        }

        /// <summary>
        /// R200 sync laws, polled every frame from the always-mounted frontend
        /// (the engine's stops are event-driven; this host is alive in every
        /// mode, unlike the live-only people chrome): the crosshair cannot
        /// outlive the follow it marks — CPState::SetSelectedPerson stops
        /// tracking on a selection change by any other surface, and manual
        /// camera input (EdgeDetectScroller) / the UCP level buttons clear
        /// WorldState.ScrollAnchor, taking the tracking state with them.
        /// </summary>
        public static void SyncTracking(TS1GameScreen game)
        {
            if (TrackedAvatar == null) return;
            var world = game != null && game.LotControl != null ? game.LotControl.World : null;
            var anchor = world != null && world.State != null ? world.State.ScrollAnchor : null;
            bool selectionLost = game == null || game.vm == null || game.vm.MyUID != TrackedAvatar.PersistID;
            if (selectionLost || anchor == null) StopTracking();
        }

        private static int NativeRoundAway(float value)
        {
            return value < 0f ? (int)(value - 0.5f) : (int)(value + 0.5f);
        }

        /// <summary>
        /// cWinPeople::TSPaint derives the arrow directly from the selected
        /// person's current overall-mood float. There is no trend history or
        /// sampling cadence in this path.
        /// </summary>
        public static int NativeMoodTrendForProbe(float overallMood)
        {
            var roundedMood = NativeRoundAway(overallMood);
            var band = NativeRoundAway(((100f + roundedMood) * 11f / 201f) - 5f);
            return Math.Max(-5, Math.Min(5, band));
        }

        public UIOriginalPeopleChrome(TS1GameScreen game)
        {
            Game = game;
            var gd = GameFacade.GraphicsDevice;
            if (TrackingTarget == null)
            {
                try { TrackingTarget = UIOriginal.EnsureResolved("cpanel\\People\\TrackingTarget.bmp")?.Get(gd); } catch { }
            }

            // webcams (1.1): 8 buttons, custom portrait draw
            for (int i = 0; i < 8; i++)
            {
                var b = new UIOriginalWebcamButton(i);
                b.Position = new Vector2(9 + (i % 4) * 45, 5 + (i / 4) * 45);
                b.OnButtonClick += Webcam_OnButtonClick;
                Add(b);
                Webcams[i] = b;
            }

            // LiveGadget plaque (1.2) behind the tabs
            var lg = UIOriginal.EnsureResolved("cpanel\\Backgrounds\\LiveGadget.bmp")?.Get(gd);
            if (lg != null)
            {
                LiveGadget = new UIImage(lg) { Position = new Vector2(193, 0) };
                Add(LiveGadget);
            }

            // R191: the live-mode gauge (kLiveModeGauge 4911) is the PEOPLE
            // PIE's pop target — cWinPeople::Init builds the pie right after
            // the gauge factory call; the port pops the family selector at
            // the gauge's center on click.
            ListenForMouse(new Rectangle(193, 0, 108, 100), Gauge_Click);

            // tabs (1.3) — art member, cell size, anchor, and the matching
            // Live.iff STR#134 ModeTips entry in native button order.
            var tabDefs = new[]
            {
                new { Art = "cpanel\\Buttons\\Mood.bmp",          W = 32, H = 39, X = 202, Y = 31 },
                new { Art = "cpanel\\Buttons\\Relationship.BMP", W = 27, H = 30, X = 269, Y = 4  },
                new { Art = "cpanel\\Buttons\\Job.bmp",          W = 27, H = 30, X = 269, Y = 35 },
                new { Art = "cpanel\\Buttons\\House.bmp",        W = 27, H = 30, X = 269, Y = 66 },
                new { Art = "cpanel\\Buttons\\Personality.bmp",  W = 27, H = 30, X = 237, Y = 35 },
                new { Art = "cpanel\\Buttons\\Interest.bmp",     W = 27, H = 30, X = 237, Y = 4  },
                new { Art = "cpanel\\Buttons\\Gift.bmp",         W = 27, H = 30, X = 237, Y = 66 },
            };
            for (int i = 0; i < 7; i++)
            {
                var d = tabDefs[i];
                var t = new UIOriginalTabButton(d.Art, d.W, d.H)
                {
                    Tooltip = OriginalLiveStrings.Entry(134, i) ?? ""
                };
                t.Position = new Vector2(d.X, d.Y);
                var idx = i;
                t.OnButtonClick += (btn) => { SelectTab(idx); };
                Add(t);
                Tabs[i] = t;
            }
            SetTabSelected(0);

            // R245: the category tabs carry ORIGINAL ids (Res_CPanel.RT
            // kMoodBtn 4500 / kPersonalityBtn 4501 / kHouseBtn 4502 /
            // kJobBtn 4503 / kRelationshipBtn 4504 / kInterestBtn 4505 /
            // kGiftBtn 4507 — r144/toolbar-law.md section 1, in TAB ORDER).
            int[] tabIds = { 4500, 4504, 4503, 4502, 4501, 4505, 4507 };
            for (int i = 0; i < 7; i++)
                TutorialControlMap.Register(Tabs[i], tabIds[i], false,
                    "people tab " + tabDefs[i].Art + " id " + tabIds[i] + " (r144/toolbar-law.md)");

            // trend arrows (1.4)
            GreenBars = UIOriginal.EnsureResolved("cpanel\\Greenbars.BMP")?.Get(gd);
            RedBars = UIOriginal.EnsureResolved("cpanel\\Redbars.bmp")?.Get(gd);
        }

        public void SelectTab(int i)
        {
            // Native cWinPeople ignores category clicks when it has no tracked
            // person and leaves every category unhighlighted. UIMainPanel also
            // enforces this at the route/host boundary so a previously mounted
            // panel cannot survive control loss.
            bool hasSelectedPerson = false;
            try { hasSelectedPerson = Game?.vm != null && Game.SelectedAvatar != null; }
            catch { }
            if (!hasSelectedPerson)
            {
                SetTabSelected(-1);
                // Route the rejected request so the owner can synchronously
                // retire a host mounted before selection was lost. It retains
                // its previous ActiveCategory and creates no replacement.
                if (i >= 0 && i < TabCategory.Length)
                    OnCategorySelect?.Invoke(TabCategory[i]);
                return;
            }
            SetTabSelected(i);
            if (TabCategory[i] >= 0) OnCategorySelect?.Invoke(TabCategory[i]);
        }

        public void SetTabSelected(int i)
        {
            ActiveTab = i;
            for (int k = 0; k < 7; k++) Tabs[k].SelectedState = (k == i) ? (byte)1 : (byte)0;
        }

        public void SelectWebcam(int i)   // public: the uirate gate drives the exact click route
        {
            var av = Webcams[i]?.Avatar;
            if (av == null || Game.vm == null)
            {
                // an empty slot cannot select or track
                return;
            }
            // R200 engine order (cWinPeople::TSOnCommand): the +0x224 branch
            // selects ONLY when the clicked person differs from the current
            // selection (SetSelectedPerson — which itself stops tracking);
            // the +0xfcc sender loop then calls TrackPerson on EVERY webcam
            // click. Net: click unselected = select + track; click selected
            // = toggle-track (same person again = untrack).
            if (Game.vm.MyUID != av.PersistID) Game.vm.MyUID = av.PersistID;
            TrackPerson(av);
        }

        private void Webcam_OnButtonClick(UIElement button)
        {
            var webcam = button as UIOriginalWebcamButton;
            if (webcam != null) SelectWebcam(webcam.Index);
        }

        // R191: pop the people pie at the gauge's global center. The chrome
        // is MainPanel-local; the gauge occupies (193,0,108,100) here and its
        // people-local center is (247,50).
        private void Gauge_Click(FSO.Common.Rendering.Framework.IO.UIMouseEventType type,
            FSO.Common.Rendering.Framework.Model.UpdateState state)
        {
            if (type != FSO.Common.Rendering.Framework.IO.UIMouseEventType.MouseDown) return;
            if (Game == null || Game.vm == null) return;
            var center = GlobalPoint(new Vector2(247f, 50f));
            var pie = new UIOriginalPeoplePie(Game, center);
            Game.Add(pie);
        }

        public void RefreshWebcams()
        {
            // family webcams: the strip shows the ACTIVE FAMILY's members
            // (the strip's engine title is the family; visitors are not
            // listed) — match avatar object GUIDs against FAMI GUIDs.
            VMAvatar[] avatars = null;
            try
            {
                var fam = Game.ActiveFamily;
                var guids = fam != null ? fam.FamilyGUIDs : null;
                avatars = Game.vm.Entities.OfType<VMAvatar>()
                    .Where(a => a.PersistID != 0 && (guids == null || guids.Contains((uint)a.Object.GUID)))
                    .OrderBy(a => Array.IndexOf(guids, (uint)a.Object.GUID))
                    .Take(8).ToArray();
            }
            catch { }
            for (int i = 0; i < 8; i++)
                Webcams[i].SetAvatar(avatars != null && i < avatars.Length ? avatars[i] : null);
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (GameFrames++ % 30 == 0) RefreshWebcams();

            // R200 sync laws (the authoritative poll also runs from the
            // always-mounted frontend — see SyncTracking).
            SyncTracking(Game);

            // TSPaint maps current overall mood directly to a signed eleven-
            // band indicator and clamps it to the five available reveal steps.
            // It owns no queue, previous sample, or person-transition baseline.
            var av = Game.SelectedAvatar;
            Trend = av == null ? 0
                : NativeMoodTrendForProbe(av.GetMotiveData(VMMotive.Mood));
        }
        private int GameFrames;

        public override void Draw(UISpriteBatch batch)
        {
            // UIContainer.Draw returns for an invisible container, but this
            // override paints the trend textures after that call. Guard the
            // whole override so LIVE-only Greenbars/Redbars cannot leak into
            // cWinArch/cWinCatalog modes.
            if (!Visible) return;
            base.Draw(batch);
            // trend arrows (people (205,53)/(205,123) -> local (205,3)/(205,73)):
            // |trend| units visible of 5 stacked 5px rows; trend 0 -> both full
            // with the engine's 2px-inset source.
            if (GreenBars != null && RedBars != null)
            {
                var greenRect = Trend > 0 ? new Rectangle(0, 0, 27, 5 * Trend)
                    : (Trend == 0 ? new Rectangle(2, 2, 23, 21) : new Rectangle(0, 0, 0, 0));
                var redRect = Trend < 0 ? new Rectangle(0, 0, 27, 5 * -Trend)
                    : (Trend == 0 ? new Rectangle(2, 2, 23, 21) : new Rectangle(0, 0, 0, 0));
                if (greenRect.Height > 0)
                    DrawLocalTexture(batch, GreenBars, greenRect, new Vector2(205, 3), Vector2.One);
                if (redRect.Height > 0)
                    DrawLocalTexture(batch, RedBars, redRect, new Vector2(205, 73), Vector2.One);
            }
        }
    }

    /// <summary>
    /// R144: one cWinPeople webcam (1.1) — 45x45 custom-drawn family-member
    /// thumbnail. The engine shares one PeopleTemplate.bmp (180x45 = four
    /// 45px cells) as the button template and paints the sim's portrait
    /// into it (type-2 custom draw, internals not disassembled — the port
    /// composes template cell + UIIconCache head, disclosed interpretation).
    /// Empty slots retain the blank PeopleTemplate frame.
    /// </summary>
    public class UIOriginalWebcamButton : UIButton
    {
        public readonly int Index;
        public VMAvatar Avatar;
        private Texture2D Frame;        // template cell (selected/normal)
        private Texture2D FrameSel;
        private Texture2D Portrait;
        private static Texture2D Template, TemplateSel;
        private Vector2 ExactSize;

        // UIButton.Size only applies its X component; these custom-drawn buttons
        // therefore need to own both reported geometry and the mouse region.
        public override Vector2 Size
        {
            get { return ExactSize; }
            set
            {
                ExactSize = value;
                Width = value.X;
                if (ClickHandler != null)
                {
                    ClickHandler.Region.Width = (int)value.X;
                    ClickHandler.Region.Height = (int)value.Y;
                }
            }
        }

        public UIOriginalWebcamButton(int index)
            : base(FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice))
        {
            Index = index;
            ImageStates = 1;
            var gd = GameFacade.GraphicsDevice;
            if (Template == null)
            {
                Template = UIOriginal.Rect("cpanel\\People\\PeopleTemplate.bmp", 0, 0, 45, 45);
                TemplateSel = UIOriginal.Rect("cpanel\\People\\PeopleTemplate.bmp", 90, 0, 45, 45);
            }
            Frame = Template; FrameSel = TemplateSel;
            Size = new Vector2(45, 45);
        }

        public void SetAvatar(VMAvatar av)
        {
            // Native webcam buttons use quick-tip type 2, whose payload is
            // the tracked person's display name. Empty family slots have no
            // quick tip.
            Tooltip = av?.Name;
            if (Avatar == av) return;
            Avatar = av;
            Portrait = null;
            if (av != null)
            {
                try { Portrait = UIIconCache.GetObject(av); } catch { }
            }
        }

        // Production-path probes: Draw uses this same resolver, so the visual
        // regression gate can prove that an unoccupied slot selects the blank
        // PeopleTemplate cell rather than an anonymous-person portrait.
        public Texture2D CurrentBaseTextureForProbe { get { return ResolveBaseTexture(); } }
        public Texture2D NormalFrameForProbe { get { return Frame; } }

        private Texture2D ResolveBaseTexture()
        {
            if (Avatar == null) return Frame;
            var game = FSO.Client.GameFacade.Screens?.CurrentUIScreen as TS1GameScreen;
            var selected = game != null && game.vm != null && game.vm.MyUID == Avatar.PersistID;
            return selected ? FrameSel : Frame;
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            // The anonymous-person portrait sheet is not an empty-family-slot
            // texture. Empty slots retain the blank PeopleTemplate frame.
            var tex = ResolveBaseTexture();
            if (tex != null) DrawLocalTexture(batch, tex, null, Vector2.Zero, Vector2.One);
            if (Portrait != null)
            {
                // head centered in the 45x45 cell (webcam draw internals are
                // an r144 residual; cell-fit is the visual reading)
                var s = Math.Min(45f / Portrait.Width, 45f / Portrait.Height);
                var w = Portrait.Width * s; var h = Portrait.Height * s;
                DrawLocalTexture(batch, Portrait, null, new Vector2((45 - w) / 2, (45 - h) / 2), new Vector2(s), Color.White * 0.92f);
            }
            // R200: the kTrackingTarget crosshair over the TRACKED portrait —
            // TrackPerson reparents the tracked webcam into the tracking
            // container at {0,0}, composing the 45x45 reticle exactly over
            // the 45x45 cell.
            if (CrosshairVisibleForProbe)
                DrawLocalTexture(batch, UIOriginalPeopleChrome.TrackingTarget, null, Vector2.Zero, Vector2.One);
        }

        /// <summary>
        /// R200 production-path probe: this webcam carries the tracking
        /// crosshair when its avatar is the tracked person and the reticle
        /// art is live (the same resolver Draw uses).
        /// </summary>
        public bool CrosshairVisibleForProbe
        {
            get
            {
                return Avatar != null
                    && UIOriginalPeopleChrome.TrackedAvatar == Avatar
                    && UIOriginalPeopleChrome.TrackingTarget != null;
            }
        }
    }

    /// <summary>
    /// R144: one cWinPeople category tab (1.3) — a 4x1 SetImage sheet cell
    /// (Mood.bmp 128x39 -> 32x39; the rest 108x30 -> 27x30). The shared
    /// cTSWinBtn state law selects normal/selected-or-down/hover/
    /// hover+selected-or-down from cells 0/1/2/3.
    /// </summary>
    public class UIOriginalTabButton : UIButton
    {
        public readonly string Member;
        public readonly int CellWidth;
        public readonly int CellHeight;
        public byte SelectedState;
        private Texture2D[] States;
        private Vector2 ExactSize;

        // The art is painted by Draw rather than UIButton.Draw, so its sheet
        // cannot establish the base class's hit height. Preserve the decoded
        // cell geometry explicitly (Mood 32x39; all other tabs 27x30).
        public override Vector2 Size
        {
            get { return ExactSize; }
            set
            {
                ExactSize = value;
                Width = value.X;
                if (ClickHandler != null)
                {
                    ClickHandler.Region.Width = (int)value.X;
                    ClickHandler.Region.Height = (int)value.Y;
                }
            }
        }

        public UIOriginalTabButton(string member, int cellWidth, int cellHeight)
            : base(FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice))
        {
            Member = member;
            CellWidth = cellWidth;
            CellHeight = cellHeight;
            ImageStates = 1;
            Size = new Vector2(cellWidth, cellHeight);
        }

        internal int CurrentNativeFrameForProbe
        {
            get
            {
                return UIOriginalSheetButton.NativeFrameForProbe(
                    false, base.Hovered, SelectedState != 0 || IsDown);
            }
        }

        private Texture2D FrameTex(int i)
        {
            if (States == null)
            {
                var tx = UIOriginal.EnsureResolved(Member)?.Get(GameFacade.GraphicsDevice);
                if (tx == null) { States = new Texture2D[1]; return null; }
                var n = tx.Width / CellWidth;
                States = new Texture2D[n];
                for (int k = 0; k < n; k++)
                    States[k] = UIOriginal.Rect(Member, k * CellWidth, 0, CellWidth, tx.Height);
            }
            if (i >= States.Length) i = 0;
            return States[i];
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            var tex = FrameTex(CurrentNativeFrameForProbe);
            if (tex != null) DrawLocalTexture(batch, tex, null, Vector2.Zero, Vector2.One);
        }
    }
}
