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
    ///
    /// UI-23 (r257): the SUB-PIE. cWinPeople owns TWO cTSPieMenu windows
    /// (+212 main / +216 sub) and a shared item-0 string (+208); the sub
    /// ring is configured byte-identical (Init 0x28f130-0x28f238: same
    /// 150/35 radii, same 60/30 pads, same command id) — a second 201px
    /// window, NOT a wider ring; SetSubMenu is dead code (zero callers).
    /// DoMenu 0x184058 caches both accessors, engages both rings and
    /// rebuilds the interaction items into the ACTIVE ring only (sub is
    /// the default active target, +156 = (+92==0)?SUB:MAIN). Item 0 of
    /// both rings is the shared back string inserted with a NULL
    /// sub-button (Init 0x28f124/0x28f22c → Layout's measured-name+6
    /// text path). Dismissal is ALWAYS both rings, main first
    /// (CancelPieMenu 0x2125e0). Keys route to the SUB ring first
    /// (TSOnCommand 0x28cd8c-28: sub vt+508() liveness tested before
    /// main); ESC = the vt+628(−1) step-out — an emptied sub ring falls
    /// through to the main ring. Port mapping (law doc §7): ring 1 =
    /// first 8 family members (existing census), ring 2 = the overflow
    /// with the back affordance as its item 0. ENGAGEMENT DISCLOSED: the
    /// native view-global predicate is BSS/relocation-walled (law §6)
    /// and pet-vs-large-family routing awaits REF-01 — the port lands
    /// the machinery on the plan's sanctioned condition (census overflow
    /// beyond the main ring's 8 slots engages ring 2; live TS1 families
    /// cap at 8, so ring 2 stays dark until the REF-01 capture names the
    /// real routing).
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

        // ---- UI-23: the sub ring (second instance, identical constants) ----
        /// <summary>Ring 2's item-0 affordance (the shared +208 string in
        /// the engine; its runtime text is not decoded — disclosed port
        /// label).</summary>
        public const string BackLabel = "Back";

        // ---- gate probes ----
        public static int Pops, Closes, Selections, SlotsMounted;
        public static Vector2 LastCenter;
        public static Vector2[] LastSlotCenters = new Vector2[SlotCount];
        public static Rectangle[] LastSlotBoxes = new Rectangle[SlotCount];
        public static int LastSelectedIndex = -1;

        // ---- UI-23 sub-ring gate probes (kept separate so the r251
        // main-ring probes keep their exact semantics) ----
        public static int SubPops, SubCloses, SubSteps, SubSelections, SubSlotsMounted;
        public static Vector2[] SubLastSlotCenters = new Vector2[SlotCount];
        /// <summary>CancelPieMenu 0x2125e0 call-order receipt: "M" appended
        /// when the main ring dismisses, "S" when the sub follows.</summary>
        public static string LastDismissal = "";

        public TS1GameScreen Game;
        public VMAvatar[] Family;
        public UIOriginalWebcamButton[] SlotButtons = new UIOriginalWebcamButton[SlotCount];

        // ---- UI-23 two-ring state ----
        /// <summary>True = this instance is ring 2 (the sub-pie).</summary>
        public readonly bool IsSub;
        /// <summary>The main ring's live sub ring (mounted on first
        /// Update; engine: both rings shown, sub is the active target).</summary>
        public UIOriginalPeoplePie Sub;
        /// <summary>Ring 2's owner (the main ring).</summary>
        public UIOriginalPeoplePie Owner;
        /// <summary>Ring 2 carries the back affordance as its item 0
        /// (engine: shared +208 string, item 0 of both rings).</summary>
        public bool HasBack { get { return IsSub; } }
        /// <summary>Back item box, window-local (Layout slot-0 anchor:
        /// measured label + 6 at the N case).</summary>
        public Rectangle BackBox;
        /// <summary>The window-local disc center (gate helper).</summary>
        public Vector2 DiscCenter { get { return Center; } }
        internal UIOriginalPeoplePie PendingSub { get { return _pendingSub; } }
        private UIOriginalPeoplePie _pendingSub;
        private int TrackedIndex = -1;               // sub-ring tracked slot (TSPaint color law)
        private bool _subSteppedFrame;               // ESC consumed by the sub this frame

        private UIOriginalWebcamButton CenterPortrait;
        private Vector2 Center;                      // window-local disc center
        private readonly Rectangle[] SlotBoxes = new Rectangle[SlotCount];
        private bool PressHeld;                      // TSPaint's press flag (0x37380 probes)
        private Texture2D White;
        private static readonly Rectangle PxRect = new Rectangle(0, 0, 1, 1);

        public UIOriginalPeoplePie(TS1GameScreen game, Vector2 globalCenter, bool sub = false)
            : this(game, globalCenter, sub, null) { }

        // UI-23 gate seam: force the census split (live families cap at
        // 8; the real routing predicate is walled pending REF-01 — the
        // gate exercises the >8 overflow machinery with this override).
        internal UIOriginalPeoplePie(TS1GameScreen game, Vector2 globalCenter, bool sub, VMAvatar[] censusOverride)
        {
            Game = game;
            IsSub = sub;
            White = TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
            if (IsSub) SubPops++; else Pops++;
            if (!IsSub) LastCenter = globalCenter;

            var sw = GameFacade.Screens.CurrentUIScreen.ScreenWidth;
            var sh = GameFacade.Screens.CurrentUIScreen.ScreenHeight;
            // window centered on the pop point, clamped on screen — the
            // identical window law: ring 2 mounts at the SAME pop point
            // with the same 201px-disc window size (Init 0x28f130 block
            // configures it byte-identical to +212's 0x28f03c block).
            Position = new Vector2(
                Math.Max(0, Math.Min(sw - WindowSize, globalCenter.X - WindowSize / 2f)),
                Math.Max(0, Math.Min(sh - WindowSize, globalCenter.Y - WindowSize / 2f)));
            Size = new Vector2(WindowSize, WindowSize);
            Center = new Vector2(globalCenter.X - Position.X, globalCenter.Y - Position.Y);

            // the family census shared with the webcam strip (engine title =
            // the family; visitors are not listed)
            VMAvatar[] census;
            try
            {
                var fam = game.ActiveFamily;
                var guids = fam != null ? fam.FamilyGUIDs : null;
                census = game.vm.Entities.OfType<VMAvatar>()
                    .Where(a => a.PersistID != 0 && (guids == null || guids.Contains((uint)a.Object.GUID)))
                    .OrderBy(a => Array.IndexOf(guids, (uint)a.Object.GUID))
                    .ToArray();
            }
            catch { census = new VMAvatar[0]; }
            if (censusOverride != null) census = censusOverride;

            if (IsSub)
            {
                // ring 2 population (law §7): the overflow members, with
                // item 0 = the back affordance (no member). Items cap at
                // the same 8 geometry slots as the main ring; overflow
                // beyond 7 is dropped (same cap law as the main ring).
                Family = new VMAvatar[SlotCount];
                for (int k = 1; k < SlotCount && k - 1 < census.Length; k++) Family[k] = census[k - 1];
            }
            else
            {
                Family = census.Take(SlotCount).ToArray();
            }

            // the inactive center disc holds the current portrait
            CenterPortrait = new UIOriginalWebcamButton(-1);
            CenterPortrait.Position = Center - new Vector2(22.5f);
            CenterPortrait.SetAvatar(CurrentAvatar());
            Add(CenterPortrait);

            // Layout (0x5290c0): one item box per family member — measured
            // label + 6px at the decoded anchor case; the portrait slot
            // button rides at the box center (GetCurItemCenterX/Y law).
            // Ring 2's item 0 is the label-only back item (InsertString
            // vt+560 with a NULL sub-button → Layout's text path).
            var font = OriginalGlyphFont.LoadDefault(GameFacade.GraphicsDevice);
            if (IsSub)
            {
                var bw = font.Measure(BackLabel) + 6;
                var bh = font.LineHeight + 6;
                var blocal = SlotBox(0, bw, bh);
                BackBox = new Rectangle(blocal.X + (int)Center.X, blocal.Y + (int)Center.Y, bw, bh);
            }
            for (int k = 0; k < SlotCount; k++)
            {
                if (IsSub && k == 0) { SlotButtons[0] = null; continue; } // label-only item 0
                var av = (Family != null && k < Family.Length) ? Family[k] : null;
                var name = av != null ? (av.Name ?? "") : "";
                var w = font.Measure(name) + 6;
                var h = font.LineHeight + 6;
                var box = SlotBox(k, w, h); // pie-center-relative
                var abs = new Rectangle(box.X + (int)Center.X, box.Y + (int)Center.Y, box.Width, box.Height);
                SlotBoxes[k] = abs;
                if (!IsSub) LastSlotBoxes[k] = abs;
                var center = new Vector2(abs.X + abs.Width / 2f, abs.Y + abs.Height / 2f);
                if (!IsSub) LastSlotCenters[k] = center; else SubLastSlotCenters[k] = center;
                var b = new UIOriginalWebcamButton(k)
                {
                    Position = center - new Vector2(22.5f),
                    Tooltip = av != null ? av.Name : null
                };
                b.SetAvatar(av);
                // InputManager dispatches mouse events to the single topmost
                // region and children outrank their container, so a click on
                // the portrait face lands on the button's own ClickHandler —
                // only this wiring (not the container-level CalcItem hit-test)
                // can select it. Dropped once in the UI-14 rewrite; restored.
                var idx = k;
                b.OnButtonClick += (btn) => SelectSlot(idx);
                Add(b);
                SlotButtons[k] = b;
                if (av != null) { if (IsSub) SubSlotsMounted++; else SlotsMounted++; }
            }
            if (!IsSub) LastSelectedIndex = CurrentIndex();

            // Engagement (law §6/§7): the native view-global predicate is
            // BSS/relocation-walled pending REF-01 — the port lands the
            // machinery on the plan's sanctioned condition: census overflow
            // beyond the main ring's 8 slots engages ring 2 (pet routing
            // pending REF-01). Mount happens on first Update so ring 2 is
            // the later sibling (topmost = the active surface).
            if (!IsSub)
            {
                var overflow = census.Skip(SlotCount).ToArray();
                if (overflow.Length > 0)
                    _pendingSub = new UIOriginalPeoplePie(game, globalCenter, true, overflow);
            }

            ListenForMouse(new Rectangle(0, 0, WindowSize, WindowSize), OnMouse);
        }

        /// <summary>First-Update mount: adds the engaged ring 2 above this
        /// ring (engine: DoMenu shows both rings, 0x503820(pie,1) main
        /// @0x184404 then sub @0x184418; the sub is the active target).</summary>
        internal void MountSub()
        {
            if (IsSub || _pendingSub == null || Parent == null) return;
            var s = _pendingSub;
            _pendingSub = null;
            s.Owner = this;
            Sub = s;
            Parent.Add(s);
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
            // ring 2's slot 0 is the back affordance (no member portrait)
            return Family != null && k < Family.Length && Family[k] != null;
        }

        /// <summary>CalcItem (0x5275b0) law: a click inside an occupied
        /// item box selects it (TrackPerson 0x28bf00); a click on no box
        /// cancels the pie. Ring 2's item-0 back box is hit first; the
        /// ACTIVE-ring law routes clicks away from the main ring while a
        /// sub ring is engaged (the sub is the active target, +156).</summary>
        private void OnMouse(UIMouseEventType type, UpdateState state)
        {
            if (!IsSub && (Sub != null || _pendingSub != null)) return; // sub ring is active
            if (type != UIMouseEventType.MouseDown) return;
            var p = new Vector2(state.MouseState.X, state.MouseState.Y) - Position;
            if (IsSub && BackBox.Contains((int)p.X, (int)p.Y)) { StepOut(); return; }
            for (int k = 0; k < SlotCount; k++)
            {
                if (!Occupied(k)) continue;
                if (SlotBoxes[k].Contains((int)p.X, (int)p.Y))
                {
                    SelectSlot(k);
                    return;
                }
            }
            // a click on no box cancels the pie — from ring 2 this is the
            // CancelPieMenu pair (dismiss BOTH rings, main first)
            if (IsSub && Owner != null) Owner.Close();
            else Close();
        }

        /// <summary>TrackPerson (0x28bf00) equivalent: the slice click makes
        /// that Sim the tracked/selected person, then the popup ends.
        /// Ring 2: slot 0 is the back affordance (step-out, vt+628(−1));
        /// a member click ends the whole popup (MenuItemSelected → the
        /// CancelPieMenu pair via the owner).</summary>
        public void SelectSlot(int k)
        {
            if (IsSub)
            {
                if (k == 0) { StepOut(); return; }
                if (Family == null || k >= Family.Length || Family[k] == null) return;
                var av = Family[k];
                if (av != null && Game.vm != null)
                {
                    Game.vm.MyUID = av.PersistID;
                    SubSelections++;
                    TrackedIndex = k;
                }
                if (Owner != null) Owner.Close();
                else Close();
                return;
            }
            if (Family == null || k >= Family.Length) return;
            var avm = Family[k];
            if (avm != null && Game.vm != null)
            {
                Game.vm.MyUID = avm.PersistID;
                Selections++;
                LastSelectedIndex = k;
            }
            Close();
        }

        /// <summary>CancelPieMenu (0x2125e0) law: closing dismisses BOTH
        /// rings, main first (vt+628(−1) on the main ring, then the sub).
        /// Ring 2's own close is a plain removal (SubCloses probe).</summary>
        public void Close()
        {
            if (IsSub)
            {
                SubCloses++;
                if (Owner != null && Owner.Sub == this) Owner.Sub = null;
                Parent?.Remove(this);
                return;
            }
            Closes++;
            var sub = Sub;
            Sub = null;
            LastDismissal += "M";
            Parent?.Remove(this);            // main first
            if (sub != null) { LastDismissal += "S"; sub.Close(); } // then sub
        }

        /// <summary>The vt+628(−1) STEP-OUT (law §4 keys/nest): ring 2
        /// closes and ring 1 remains — the Back item click and the ESC
        /// nudge both land here.</summary>
        public void StepOut()
        {
            if (!IsSub) return;
            SubSteps++;
            if (Owner != null) Owner._subSteppedFrame = true; // ESC consumed this frame
            Close();
        }

        /// <summary>ESC on the active ring (TSOnCommand 0x28cd8c-28): the
        /// sub ring is tested FIRST (vt+508() liveness priority) — a live
        /// sub steps out sub→main; with no sub live this is the full
        /// CancelPieMenu dismissal. Returns true when a step-out consumed
        /// the key and the main ring stays open.</summary>
        public bool EscapeStep()
        {
            if (IsSub) { StepOut(); return true; }
            if (Sub != null) { Sub.StepOut(); return true; }
            Close();
            return false;
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (!IsSub) { _subSteppedFrame = false; MountSub(); }
            PressHeld = state.MouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            var esc = state.KeyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape);
            if (IsSub)
            {
                // ESC on ring 2 = the step-out (sub→main); ring 1 remains
                if (esc) StepOut();
                return;
            }
            // sub ring has key priority: only when no sub is engaged (and
            // none stepped out this frame) does ESC reach the main ring
            if (esc && Sub == null && _pendingSub == null && !_subSteppedFrame) EscapeStep();
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
            // ring 2's item 0: the back affordance — the shared +208 string
            // drawn through the same TSPaint path (frame tinted +100, text
            // at (+3,+3); untracked → the +208 lowlight color).
            if (IsSub)
            {
                DrawFrame(batch, BackBox, BaseColor);
                var blabel = LabelFor(0, BackLabel, font, new Vector2(BackBox.X + 3, BackBox.Y + 3));
                blabel.Color = LowlightColor;
                blabel.Draw(batch);
            }
            var tracked = IsSub ? TrackedIndex : LastSelectedIndex;
            for (int k = 0; k < SlotCount; k++)
            {
                if (!Occupied(k)) continue;
                var box = SlotBoxes[k];
                DrawFrame(batch, box, BaseColor);
                var name = Family[k].Name ?? "";
                var color = (k == tracked)
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
