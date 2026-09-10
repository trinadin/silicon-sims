using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Screens;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FSO.SimAntics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// R191: the PEOPLE PIE — the family Sim selector popped from the
    /// live-mode gauge, on the decoded cTSPieMenu law (evidence
    /// r191/ + r159/r159-scout-engine-decode.md §4).
    ///
    /// cWinPeople::Init (0x28eee0-0x28f244):
    ///   factory(kLiveModeGauge 4911, this+548)          the pop target
    ///   factory(kPieFaceBkg 800, this+208)               PieFace1.bmp 201x201
    ///   0x48e380(buf,255,0,255)                          the magenta key
    ///   pie/sub = new cTSPieMenu (ctor 0x52a4f0)
    ///   SetFont(type 1); field+100 = {0,40,140}
    ///   SetSelectedColor {255,255,255}  (vt+580)
    ///   SetHighlightColor {165,195,214}  (vt+576)
    ///   SetLowlightColor {0,255,255}     (vt+584)
    ///   SetMaxRadius(150)               (vt+488, @0x28f0ac)
    ///   SetInactiveRadius(35)           (vt+476, @0x28f0d4)
    ///   pie+372 = 60; pie+376 = 30       (literal slots)
    ///   SetPopupBackgroundBuffer(this+208, 0)
    ///   8 portrait buttons (cTSWinBtn) bound `cmpwi 8` @0x28f340
    ///
    /// PieFace1.bmp is byte-verified ALL magenta (40401/40401 px) — it is a
    /// pure chroma-key/mask buffer, so the disc itself is engine-drawn; the
    /// port draws the r=100 disc in the decoded base color (disclosed).
    /// cTSPieMenu::Layout (0x5290c0) distributes items over the 8 compass
    /// directions at 45-degree steps; the exact TSPaint label-frame
    /// placement is modeled at the same angles (radius 150 label ring,
    /// radius 75 portrait ring) and disclosed.
    /// </summary>
    public class UIOriginalPeoplePie : UIContainer
    {
        // ---- decoded constants (pinned by the uipie gate) ----
        public const int DiscSize = 201;             // PieFace1.bmp
        public const int MaxRadius = 150;            // SetMaxRadius
        public const int InactiveRadius = 35;        // SetInactiveRadius
        public const int SlotCount = 8;              // cmpwi 8 @0x28f340
        public const int LiteralSlotA = 60;          // pie+372
        public const int LiteralSlotB = 30;          // pie+376
        public static readonly Color BaseColor = new Color(0, 40, 140);        // pie+100
        public static readonly Color SelectedColor = new Color(255, 255, 255); // vt+580
        public static readonly Color HighlightColor = new Color(165, 195, 214);// vt+576
        public static readonly Color LowlightColor = new Color(0, 255, 255);   // vt+584

        public const int PortraitRadius = 75;        // modeled (disclosed)
        public const int WindowSize = 2 * (MaxRadius + 60);

        // ---- gate probes ----
        public static int Pops, Closes, Selections, SlotsMounted;
        public static Vector2 LastCenter;
        public static Vector2[] LastSlotCenters = new Vector2[SlotCount];
        public static int LastSelectedIndex = -1;

        public TS1GameScreen Game;
        public VMAvatar[] Family;
        public UIOriginalWebcamButton[] SlotButtons = new UIOriginalWebcamButton[SlotCount];
        private UIOriginalWebcamButton CenterPortrait;
        private Vector2 Center;                      // window-local disc center
        private int HoverSlot = -1;
        private Texture2D White;
        private static readonly Rectangle PxRect = new Rectangle(0, 0, 1, 1);

        public UIOriginalPeoplePie(TS1GameScreen game, Vector2 globalCenter)
        {
            Game = game;
            White = TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
            Pops++;
            LastCenter = globalCenter;

            var sw = GameFacade.Screens.CurrentUIScreen.ScreenWidth;
            var sh = GameFacade.Screens.CurrentUIScreen.ScreenHeight;
            // window centered on the pop point, clamped on screen
            Position = new Vector2(
                Math.Max(0, Math.Min(sw - WindowSize, globalCenter.X - WindowSize / 2f)),
                Math.Max(0, Math.Min(sh - WindowSize, globalCenter.Y - WindowSize / 2f)));
            Size = new Vector2(WindowSize, WindowSize);
            Center = new Vector2(globalCenter.X - Position.X, globalCenter.Y - Position.Y);

            // the family census shared with the webcam strip (engine title =
            // the family; visitors are not listed)
            try
            {
                var fam = game.ActiveFamily;
                var guids = fam != null ? fam.FamilyGUIDs : null;
                Family = game.vm.Entities.OfType<VMAvatar>()
                    .Where(a => a.PersistID != 0 && (guids == null || guids.Contains((uint)a.Object.GUID)))
                    .OrderBy(a => Array.IndexOf(guids, (uint)a.Object.GUID))
                    .Take(SlotCount).ToArray();
            }
            catch { Family = new VMAvatar[0]; }

            // the inactive center disc holds the current portrait
            CenterPortrait = new UIOriginalWebcamButton(-1);
            CenterPortrait.Position = Center - new Vector2(22.5f);
            CenterPortrait.SetAvatar(CurrentAvatar());
            Add(CenterPortrait);

            // 8 compass slots: k = 0 at 12 o'clock, clockwise, 45 degrees
            for (int k = 0; k < SlotCount; k++)
            {
                var dir = SlotDirection(k);
                var pos = Center + dir * PortraitRadius - new Vector2(22.5f);
                var b = new UIOriginalWebcamButton(k)
                {
                    Position = pos,
                    Tooltip = (Family != null && k < Family.Length) ? Family[k].Name : null
                };
                b.SetAvatar(Family != null && k < Family.Length ? Family[k] : null);
                var idx = k;
                b.OnButtonClick += (btn) => SelectSlot(idx);
                Add(b);
                SlotButtons[k] = b;
                LastSlotCenters[k] = Center + dir * PortraitRadius;
                if (Family != null && k < Family.Length) SlotsMounted++;
            }
            LastSelectedIndex = CurrentIndex();

            ListenForMouse(new Rectangle(0, 0, WindowSize, WindowSize), OnMouse);
        }

        /// <summary>Layout slot direction: 45-degree compass steps starting
        /// at 12 o'clock, clockwise.</summary>
        public static Vector2 SlotDirection(int k)
        {
            var a = -Math.PI / 2 + k * (Math.PI / 4);
            return new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
        }

        private VMAvatar CurrentAvatar()
        {
            try
            {
                var uid = Game.vm.MyUID;
                return Game.vm.Entities.OfType<VMAvatar>()
                    .FirstOrDefault(a => a.PersistID == uid);
            }
            catch { return null; }
        }

        private int CurrentIndex()
        {
            var cur = CurrentAvatar();
            if (cur == null || Family == null) return -1;
            for (int i = 0; i < Family.Length; i++) if (Family[i] == cur) return i;
            return -1;
        }

        private void OnMouse(UIMouseEventType type, UpdateState state)
        {
            if (type == UIMouseEventType.MouseDown)
            {
                // click outside every occupied slot closes the pie
                var p = new Vector2(state.MouseState.X, state.MouseState.Y) - Position - Center;
                for (int k = 0; k < SlotCount; k++)
                {
                    if (Family == null || k >= Family.Length) continue;
                    if (Vector2.Distance(p, SlotDirection(k) * PortraitRadius) <= 30f) return;
                }
                Close();
            }
        }

        /// <summary>TrackPerson (0x28bf00) equivalent: the slice click makes
        /// that Sim the tracked/selected person, then the popup ends.</summary>
        public void SelectSlot(int k)
        {
            if (Family == null || k >= Family.Length) return;
            var av = Family[k];
            if (av != null && Game.vm != null)
            {
                Game.vm.MyUID = av.PersistID;
                Selections++;
                LastSelectedIndex = k;
            }
            Close();
        }

        public void Close()
        {
            Closes++;
            Parent?.Remove(this);
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            HoverSlot = -1;
            var p = new Vector2(state.MouseState.X, state.MouseState.Y) - Position - Center;
            for (int k = 0; k < SlotCount; k++)
            {
                if (Family == null || k >= Family.Length) continue;
                if (Vector2.Distance(p, SlotDirection(k) * PortraitRadius) <= 30f) { HoverSlot = k; break; }
            }
            if (state.KeyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)) Close();
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;

            // the engine-drawn disc (PieFace1 is the pure key mask): r=100
            // base-color fill with a lowlight rim, and the inactive r=35
            // center disc.
            DrawDisc(batch, Center, 100f, BaseColor);
            DrawDisc(batch, Center, 100f, LowlightColor, rimOnly: true);
            DrawDisc(batch, Center, InactiveRadius, BaseColor * 0.55f);

            // name labels on the max-radius ring, framed in the decoded
            // palette: selected = white frame, hover = highlight, else base.
            var font = OriginalGlyphFont.LoadDefault(GameFacade.GraphicsDevice);
            for (int k = 0; k < SlotCount; k++)
            {
                if (Family == null || k >= Family.Length) continue;
                var name = Family[k].Name ?? "";
                var lp = Center + SlotDirection(k) * MaxRadius;
                var w = font.Measure(name);
                var frame = new Rectangle((int)(lp.X - w / 2) - 4, (int)(lp.Y - 7) - 2, w + 8, 18);
                var fill = (k == LastSelectedIndex) ? SelectedColor
                    : (k == HoverSlot) ? HighlightColor : BaseColor;
                DrawRect(batch, frame, fill * 0.85f);
                var label = LabelFor(k, name, font, new Vector2(frame.X + 4, frame.Y + 2));
                label.Color = (k == LastSelectedIndex) ? BaseColor : SelectedColor;
                label.Draw(batch);
            }

            base.Draw(batch);
        }

        private readonly UIOriginalText[] Labels = new UIOriginalText[SlotCount];
        private UIOriginalText LabelFor(int k, string name, OriginalGlyphFont font, Vector2 pos)
        {
            if (Labels[k] == null)
            {
                Labels[k] = new UIOriginalText(name, font);
                Add(Labels[k]);
            }
            Labels[k].Position = pos;
            Labels[k].Text = name;
            return Labels[k];
        }

        private void DrawDisc(UISpriteBatch batch, Vector2 c, float r, Color col, bool rimOnly = false)
        {
            var white = TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
            if (rimOnly)
            {
                for (int a = 0; a < 360; a += 6)
                {
                    var d = new Vector2((float)Math.Cos(a * Math.PI / 180), (float)Math.Sin(a * Math.PI / 180));
                    DrawLocalTexture(batch, White, PxRect, c + d * (r - 1), new Vector2(2, 2), col);
                }
            }
            else
            {
                for (float rr = r; rr > 0; rr -= 1.5f)
                    DrawLocalTexture(batch, White, PxRect, c - new Vector2(rr), new Vector2(rr * 2, 1.5f), col);
            }
        }

        private void DrawRect(UISpriteBatch batch, Rectangle r, Color col)
        {
            DrawLocalTexture(batch, White, PxRect, new Vector2(r.X, r.Y), new Vector2(r.Width, r.Height), col);
        }
    }
}
