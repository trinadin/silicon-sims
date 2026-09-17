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
    /// R251: the PEOPLE PIE — the family Sim selector popped from the
    /// live-mode gauge, on the fully decoded cTSPieMenu law (evidence
    /// r251/, r191/, r159 scout §4).
    ///
    /// cTSPieMenu::Layout (0x5290c0) positions every item box from an
    /// 8-case anchor table around the pie center: cardinals at the
    /// +372 pad (60 here, cWinPeople::Init 0x28f0e4), diagonals at the
    /// +376 pad (30 here, 0x28f0f4), box = measured label ±3px per side
    /// (the +6 law), clamped into the background rect expanded by the
    /// spoke radius (+348, default 8). The per-item angles (record
    /// +12) ride the same 45-degree grid with first direction 0
    /// (SetFirstDirection/+332 never called); MaxRadius 150 and
    /// InactiveRadius 35 (+356/+336) bound only CalcItem's polar
    /// hit-fallback, never the layout. Labels draw through
    /// DrawFramedLabel: text inset (3,3) top-left, frame tinted +100
    /// (the pie base color 0,40,140), text color by TSPaint's state
    /// law — tracked item (+204) → +208 lowlight (cyan), other items →
    /// +212 highlight (165,195,214), other items while a press is held
    /// → +216 selected (white). GetCurItemCenterX/Y (0x529950 /
    /// 0x529890) return the selected box center — the portrait slot
    /// buttons ride there. CalcItem (0x5275b0) hit-tests by item-box
    /// rect first; a click on no box cancels the pie.
    /// PieFace1.bmp is byte-verified ALL magenta (40401/40401 px) — a
    /// pure chroma-key/mask buffer, so the disc itself is engine-drawn;
    /// the port draws the r=100 disc in the decoded base color with the
    /// r=35 inactive hub (disclosed, unchanged from r191).
    /// Disclosed residuals (r251): the jump-table case→index order is
    /// assumed code order (corroborated by the interaction pie's
    /// dirConfig model, same Layout + table), and the SE case's raw
    /// left edge is 0 (asymmetric with NE's +30).
    /// </summary>
    public class UIOriginalPeoplePie : UIContainer
    {
        // ---- decoded constants (pinned by the uipie gate) ----
        public const int DiscSize = 201;             // PieFace1.bmp
        public const int MaxRadius = 150;            // SetMaxRadius (+356); hit-fallback bound only
        public const int InactiveRadius = 35;        // SetInactiveRadius (+336); hit-fallback bound only
        public const int SlotCount = 8;              // cmpwi 8 @0x28f340
        public const int CardinalPad = 60;           // +372 — cardinal anchor gap
        public const int DiagonalPad = 30;           // +376 — diagonal anchor gap
        public const int ClampMargin = 8;            // SetSpokeRadius default (+348, ctor 0x52a5c8)
        public static readonly Color BaseColor = new Color(0, 40, 140);        // +100 frame tint
        public static readonly Color SelectedColor = new Color(255, 255, 255); // +216 tracked item while press held
        public static readonly Color HighlightColor = new Color(165, 195, 214);// +212 tracked item idle
        public static readonly Color LowlightColor = new Color(0, 255, 255);   // +208 untracked items

        public const int WindowSize = 2 * (MaxRadius + CardinalPad);

        // ---- gate probes ----
        public static int Pops, Closes, Selections, SlotsMounted;
        public static Vector2 LastCenter;
        public static Vector2[] LastSlotCenters = new Vector2[SlotCount];
        public static Rectangle[] LastSlotBoxes = new Rectangle[SlotCount];
        public static int LastSelectedIndex = -1;

        public TS1GameScreen Game;
        public VMAvatar[] Family;
        public UIOriginalWebcamButton[] SlotButtons = new UIOriginalWebcamButton[SlotCount];
        private UIOriginalWebcamButton CenterPortrait;
        private Vector2 Center;                      // window-local disc center
        private readonly Rectangle[] SlotBoxes = new Rectangle[SlotCount];
        private bool PressHeld;                      // TSPaint's press flag (0x37380 probes)
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

            // Layout (0x5290c0): one item box per family member — measured
            // label + 6px at the decoded anchor case; the portrait slot
            // button rides at the box center (GetCurItemCenterX/Y law).
            var font = OriginalGlyphFont.LoadDefault(GameFacade.GraphicsDevice);
            for (int k = 0; k < SlotCount; k++)
            {
                var name = (Family != null && k < Family.Length) ? (Family[k].Name ?? "") : "";
                var w = font.Measure(name) + 6;
                var h = font.LineHeight + 6;
                var box = SlotBox(k, w, h); // pie-center-relative
                var abs = new Rectangle(box.X + (int)Center.X, box.Y + (int)Center.Y, box.Width, box.Height);
                SlotBoxes[k] = abs;
                LastSlotBoxes[k] = abs;
                var center = new Vector2(abs.X + abs.Width / 2f, abs.Y + abs.Height / 2f);
                LastSlotCenters[k] = center;
                var b = new UIOriginalWebcamButton(k)
                {
                    Position = center - new Vector2(22.5f),
                    Tooltip = (Family != null && k < Family.Length) ? Family[k].Name : null
                };
                b.SetAvatar(Family != null && k < Family.Length ? Family[k] : null);
                Add(b);
                SlotButtons[k] = b;
                if (Family != null && k < Family.Length) SlotsMounted++;
            }
            LastSelectedIndex = CurrentIndex();

            ListenForMouse(new Rectangle(0, 0, WindowSize, WindowSize), OnMouse);
        }

        /// <summary>Layout's 8-case anchor table (0x529540–0x5295ec),
        /// pie-center-relative, with the raw SE decode. w/h are the +6
        /// item extents; the result is clamped into the background rect
        /// expanded by the spoke radius (0x5295f0–0x52963c).</summary>
        public static Rectangle SlotBox(int k, int w, int h)
        {
            int l, t;
            switch (((k % 8) + 8) % 8)
            {
                case 0: l = -w / 2; t = -(CardinalPad + h); break;       // N
                case 1: l = DiagonalPad; t = -(DiagonalPad + h); break;  // NE
                case 2: l = CardinalPad; t = -h / 2; break;              // E
                case 3: l = 0; t = DiagonalPad; break;                   // SE (raw: left edge at center-x)
                case 4: l = -w / 2; t = CardinalPad; break;              // S
                case 5: l = -(DiagonalPad + w); t = DiagonalPad; break;  // SW
                case 6: l = -(CardinalPad + w); t = -h / 2; break;       // W
                default: l = -(DiagonalPad + w); t = -(DiagonalPad + h); break; // NW
            }
            var half = DiscSize / 2 + ClampMargin;
            l = Math.Max(-half, Math.Min(half, l));
            t = Math.Max(-half, Math.Min(half, t));
            return new Rectangle(l, t, w, h);
        }

        /// <summary>The item-angle convention (record +12): 45-degree
        /// compass steps from 12 o'clock, clockwise. Positions come from
        /// SlotBox; angles only matter to the engine's polar fallback.</summary>
        public static Vector2 SlotDirection(int k)
        {
            var a = -Math.PI / 2 + ((k % 8) + 8) % 8 * (Math.PI / 4);
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

        private bool Occupied(int k)
        {
            return Family != null && k < Family.Length;
        }

        /// <summary>CalcItem (0x5275b0) law: a click inside an occupied
        /// item box selects it (TrackPerson 0x28bf00); a click on no box
        /// cancels the pie.</summary>
        private void OnMouse(UIMouseEventType type, UpdateState state)
        {
            if (type != UIMouseEventType.MouseDown) return;
            var p = new Vector2(state.MouseState.X, state.MouseState.Y) - Position;
            for (int k = 0; k < SlotCount; k++)
            {
                if (!Occupied(k)) continue;
                if (SlotBoxes[k].Contains((int)p.X, (int)p.Y))
                {
                    SelectSlot(k);
                    return;
                }
            }
            Close();
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
            PressHeld = state.MouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
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

            // TSPaint (0x5288b0): each item draws its framed label at the
            // stored box — frame tinted +100 (base blue), text at (+3,+3)
            // top-left. Color law (raw-verified 0x528a74-0x528aa0: word
            // 0x4082001c = BNE): untracked items draw +208; the tracked
            // item draws +212 idle and +216 while a press is held.
            var font = OriginalGlyphFont.LoadDefault(GameFacade.GraphicsDevice);
            for (int k = 0; k < SlotCount; k++)
            {
                if (!Occupied(k)) continue;
                var box = SlotBoxes[k];
                DrawFrame(batch, box, BaseColor);
                var name = Family[k].Name ?? "";
                var color = (k == LastSelectedIndex)
                    ? (PressHeld ? SelectedColor : HighlightColor)
                    : LowlightColor;
                var label = LabelFor(k, name, font, new Vector2(box.X + 3, box.Y + 3));
                label.Color = color;
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

        /// <summary>DrawLabelFrame footprint: a 2px outline in the decoded
        /// tint (the engine draws a 9-patch tile frame; the port has no
        /// decoded tile for it — disclosed).</summary>
        private void DrawFrame(UISpriteBatch batch, Rectangle r, Color col)
        {
            DrawRect(batch, new Rectangle(r.X, r.Y, r.Width, 2), col);
            DrawRect(batch, new Rectangle(r.X, r.Y + r.Height - 2, r.Width, 2), col);
            DrawRect(batch, new Rectangle(r.X, r.Y, 2, r.Height), col);
            DrawRect(batch, new Rectangle(r.X + r.Width - 2, r.Y, 2, r.Height), col);
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
