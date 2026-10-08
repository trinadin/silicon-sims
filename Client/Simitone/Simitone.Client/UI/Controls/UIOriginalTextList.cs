using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Simitone.Client.UI.Controls
{
    /// <summary>
    /// cTSWinTextList (0x537680), shared by Help and Phonebook. +e0 is the
    /// page size; +e8 is TSCharHeight+3, +e4 the top row. ChildAdd initializes
    /// and resizes the list to page-size * row-height. Rows are imageless
    /// cTSWinBtn children; the navy +64 value is the list fill, not text ink.
    /// Evidence: tools/iff-dump/r238-phonebook/{textlist,child-add}.txt.
    /// UI-37: the animated ScrollTo law (RampGenerator constant-speed ramp,
    /// pool {56, 1, 4, 60, 0.001, 0.5} at TOC-0x4350) and the scrollbar
    /// autorepeat law (cTSWinScrollbar::TSPaint @0x51d100: repeat the held
    /// zone action every >200ms of cTSTimer elapsed, then Reset+Start).
    /// </summary>
    public class UIOriginalTextList : UIContainer
    {
        public const string ScrollbarMember = "Shared\\Sys\\WinScrol.BMP";
        public const int ScrollbarWidth = 17, ScrollbarCellHeight = 20, ScrollbarGutter = 20;
        public readonly List<string> Items = new List<string>();
        public readonly List<UIOriginalText> Rows = new List<UIOriginalText>();
        public readonly OriginalGlyphFont Font;
        public readonly int ListWidth, VisibleRows, RowHeight;
        public int SelectedIndex { get; private set; } = -1;
        public int TopRow { get; private set; }
        public override Vector2 Size { get => new Vector2(ListWidth, HeightPixels); set { } }
        public override Rectangle GetBounds() => new Rectangle(0, 0, ListWidth, HeightPixels);
        public int MaxTopRow => Math.Max(0, Items.Count - VisibleRows);
        public Color BackgroundColor = new Color(0, 0, 57);
        public Color TextColor = new Color(195, 205, 205);
        public Color SelectedColor = Color.Cyan;
        public event Action<int> OnSelectionChange;
        public event Action<int> OnItemActivate;
        private readonly Texture2D Scrollbar;
        private bool WheelInitialized, Dragging;
        private int LastWheel, DragOffset, HoverRow = -1;
        private double LastClickTime;
        private int LastClickRow = -1;
        public bool KeyboardActive;
        private static UIOriginalTextList ActiveList;
        private int HeightPixels => VisibleRows * RowHeight;
        private int TrackHeight => Math.Max(0, HeightPixels - 2 * ScrollbarCellHeight);
        private int ThumbHeight => ScrollbarCellHeight;
        private int ThumbY => ScrollbarCellHeight + (MaxTopRow == 0 ? 0 :
            (int)Math.Round(TopRow * (double)Math.Max(0, TrackHeight - ThumbHeight) / MaxTopRow));

        // ==== UI-37 decoded scroll-animation state (native +0xf4 flag,
        // +0xfc RampGenerator {target, dirSpeed, speed, endMs}).
        private bool RampActive;
        private float RampTarget, RampSpeed;   // rows per ms
        private double RampEndMs;
        private int AnimatedOffset;            // native +0xec (negated pixel offset)
        public static double AnimPixelsTotal;  // gate counter (proof of animated frames)

        // ==== UI-37 decoded autorepeat state (cTSWinScrollbar +0x140 zone,
        // +0x144 pressed, +0x128 cTSTimer; repeat period 200ms).
        private int HeldZone;
        private bool HeldPressed;
        private double RepeatStartMs;
        public static int AutorepeatFires;

        private static double NowMs => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

        public UIOriginalTextList(OriginalGlyphFont font, int width, int visibleRows)
        {
            Font = font;
            ListWidth = width;
            VisibleRows = Math.Max(1, visibleRows);
            RowHeight = font.LineHeight + 3;
            Size = new Vector2(width, HeightPixels);
            Scrollbar = UIOriginal.EnsureResolved(ScrollbarMember)?.Get(GameFacade.GraphicsDevice);
            for (int i = 0; i < VisibleRows; i++)
            {
                var slot = i;
                var row = new ClippedListText("", font, width - ScrollbarGutter, RowHeight)
                { Position = new Vector2(0, i * RowHeight) };
                row.ListenForMouse(new Rectangle(0, 0, width - ScrollbarGutter, RowHeight), (type, state) =>
                {
                    if (type == UIMouseEventType.MouseOver) HoverRow = slot;
                    if (type == UIMouseEventType.MouseOut) HoverRow = -1;
                    if (type != UIMouseEventType.MouseDown || TopRow + slot >= Items.Count) return;
                    ActiveList = this;
                    // (A-7 note: ActiveList is released in the Removed override
                    // below — a dead list must not keep the keyboard latch.)
                    var index = TopRow + slot;
                    Select(index);
                    var now = Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;
                    if (index == LastClickRow && now - LastClickTime < 500) OnItemActivate?.Invoke(index);
                    LastClickRow = index;
                    LastClickTime = now;
                });
                Rows.Add(row);
                Add(row);
            }
                ListenForMouse(new Rectangle(width - ScrollbarGutter, 0, ScrollbarWidth, HeightPixels), (type, state) =>
                {
                    if (type != UIMouseEventType.MouseDown || MaxTopRow == 0) return;
                    ActiveList = this;
                    var y = GetMousePosition(state.MouseState).Y;
                    // UI-37 decoded zones (cTSWinScrollbar::DoCursorPositionHitTest
                    // 0x51c860): 1 up-line / 3 page-prev / 5 thumb / 4 page-next /
                    // 2 down-line along the axis; press fires the zone action
                    // immediately (TSOnMouseDownL 0x51cf90) and starts the repeat
                    // timer; thumb drag maps cursor position to value directly.
                    int zone = ScrollbarZone((int)y);
                    if (zone == 5)
                    {
                        Dragging = true;
                        DragOffset = (int)y - ThumbY;
                    }
                    else
                    {
                        FireZone(zone);
                        HeldZone = zone;
                        HeldPressed = true;
                        RepeatStartMs = NowMs;
                    }
                });
            ActiveList = null;
            RefreshRows();
        }

        public override void Removed()
        {
            // AUD-17 A-7: a removed list kept the static keyboard latch — a
            // reopened list never received arrow keys until the user clicked
            // some list again (KeyboardActive is only honored while
            // ActiveList == null).
            if (ReferenceEquals(ActiveList, this)) ActiveList = null;
            HeldPressed = false;
            HeldZone = 0;
            RampActive = false;
            base.Removed();
        }

        public void SetItems(IEnumerable<string> items)
        {
            Items.Clear();
            Items.AddRange(items.Select(x => x ?? ""));
            SelectedIndex = -1;
            LastClickRow = -1;
            TopRow = 0;
            RampActive = false;
            AnimatedOffset = 0;
            HeldPressed = false;
            HeldZone = 0;
            RefreshRows();
        }

        public void Select(int index)
        {
            index = Items.Count == 0 || index < 0 ? -1 : Math.Min(index, Items.Count - 1);
            bool changed = SelectedIndex != index;
            SelectedIndex = index;
            if (index >= 0)
            {
                // UI-37: the native select scrolls to the row through the
                // animated ScrollTo when it falls outside the visible page.
                if (index < TopRow) ScrollTo(index);
                else if (index >= TopRow + VisibleRows) ScrollTo(index - VisibleRows + 1);
            }
            RefreshRows();
            if (changed) OnSelectionChange?.Invoke(index);
        }

        public void ScrollBy(int delta)
        {
            ScrollTo(TopRow + delta);
        }

        /// <summary>
        /// UI-37 decoded law — cTSWinTextList::ScrollTo @raw 0x535f30 with the
        /// RampGenerator pool {56.0, 1.0, 4.0, 60.0, 0.001, 0.5} at TOC-0x4350
        /// (image 0x1059c3bc): speed(rows/ms) = clamp(4 + 56*(|d|-1)/(2*visRows-1),
        /// 4, 60) * 0.001; SetupConstantSpeedRamp(current, target, speed, now)
        /// stores {target, signedSpeed, endMs = now + |d|/speed}. GetVal (raw
        /// 0x535b00-family, image 0x10145aa0) = target - (end-now)*speed.
        /// </summary>
        public void ScrollTo(int row)
        {
            row = Math.Max(0, Math.Min(MaxTopRow, row));
            float current = RampActive ? RampValue(NowMs) : TopRow;
            double d = Math.Abs(current - row);
            float slope = 56f / Math.Max(1, 2 * VisibleRows - 1);
            float speedRowsPerS = Math.Min(60f, Math.Max(4f, 4f + slope * ((float)d - 1f)));
            RampSpeed = speedRowsPerS / 1000f;
            RampTarget = row;
            RampActive = true;
            RampEndMs = NowMs + d / RampSpeed;
            // Keep the integer row in sync immediately when idle so callers
            // reading TopRow right after ScrollTo see the destination intent
            // (the ramp advances it per frame as the native TSPaint does).
        }

        private float RampValue(double nowMs)
        {
            if (!RampActive || nowMs >= RampEndMs) return RampTarget;
            return RampTarget - (float)((RampEndMs - nowMs) * RampSpeed);
        }

        /// <summary>The TSPaint advance (raw 0x5370a0): rowInt = (int)(v+0.5),
        /// offset = -(int)((v-rowInt)*rowHeight + 0.5) with the >=rowHeight
        /// carry; done when now >= endMs.</summary>
        private void RampStep()
        {
            double now = NowMs;
            float v = RampValue(now);
            int rowInt = (int)Math.Floor(v + 0.5f);
            float frac = v - rowInt;
            int offset = (int)(frac * RowHeight + 0.5f);
            if (offset >= RowHeight) { offset = 0; rowInt++; }
            AnimatedOffset = -offset;
            if (rowInt != TopRow) { TopRow = Math.Max(0, Math.Min(MaxTopRow, rowInt)); }
            if (AnimatedOffset != 0) AnimPixelsTotal += Math.Abs(AnimatedOffset);
            if (now >= RampEndMs) { RampActive = false; AnimatedOffset = 0; }
            RefreshRows();
        }

        /// <summary>Probe seam: settle any active scroll ramp synchronously —
        /// the same RampStep law the per-frame Update drives, advanced on the
        /// real clock until the ramp completes (headless checks assert
        /// post-scroll state without a running frame loop).</summary>
        public void SettleRampForProbe()
        {
            var guard = Environment.TickCount + 5000;
            while (RampActive && Environment.TickCount < guard)
            {
                RampStep();
                if (RampActive) System.Threading.Thread.Sleep(10);
            }
        }

        /// <summary>Decoded hit zones (axis = vertical for these lists).</summary>
        public int ScrollbarZone(int y)
        {
            if (y < ScrollbarCellHeight) return 1;
            if (y >= HeightPixels - ScrollbarCellHeight) return 2;
            if (y >= ThumbY && y < ThumbY + ThumbHeight) return 5;
            return y < ThumbY ? 3 : 4;
        }

        /// <summary>Zone actions — line/page steps go through the animated
        /// ScrollTo (the native scrollbar dispatches cmd 12 to the list).</summary>
        private void FireZone(int zone)
        {
            switch (zone)
            {
                case 1: ScrollBy(-1); break;
                case 2: ScrollBy(1); break;
                case 3: ScrollBy(-VisibleRows); break;
                case 4: ScrollBy(VisibleRows); break;
            }
        }

        private void RefreshRows()
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                int index = TopRow + i;
                Rows[i].Text = index < Items.Count ? Items[index] : "";
                Rows[i].Color = index == SelectedIndex ? SelectedColor : TextColor;
                // Native TileItems: y = (index - topRow)*rowHeight + animatedOffset.
                Rows[i].Position = new Vector2(0, i * RowHeight + AnimatedOffset);
            }
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            var wheel = state.MouseState.ScrollWheelValue;
            var mouse = GetMousePosition(state.MouseState);
            bool inside = mouse.X >= 0 && mouse.X < ListWidth && mouse.Y >= 0 && mouse.Y < HeightPixels;
            if (WheelInitialized && inside && state.WindowFocused) ScrollBy(-(wheel - LastWheel) / 120);
            LastWheel = wheel;
            WheelInitialized = true;
            if (Dragging)
            {
                if (state.MouseState.LeftButton == ButtonState.Released) Dragging = false;
                else
                {
                    // UI-37: thumb drag maps cursor position to value directly
                    // (ConvertCursorPositionToValue 0x51c2e0) — no ramp.
                    RampActive = false;
                    var span = Math.Max(1, TrackHeight - ThumbHeight);
                    int target = (int)Math.Round((mouse.Y - DragOffset - ScrollbarCellHeight) * MaxTopRow / span);
                    target = Math.Max(0, Math.Min(MaxTopRow, target));
                    if (target != TopRow) { TopRow = target; RefreshRows(); }
                }
            }
            // UI-37 decoded autorepeat — cTSWinScrollbar::TSPaint 0x51d100:
            // while captured and pressed and zone in [1..4], if the cTSTimer
            // elapsed exceeds 200ms (0xc8) fire the zone action, then Reset +
            // Start (a fresh 200ms window). TSOnMouseMove 0x51cd50 keeps
            // pressed true only while the cursor stays in the SAME zone.
            if (HeldPressed)
            {
                if (state.MouseState.LeftButton == ButtonState.Released || HeldZone == 0 || HeldZone == 5)
                {
                    HeldPressed = false;
                    HeldZone = 0;
                }
                else
                {
                    bool insideGutter = mouse.X >= ListWidth - ScrollbarGutter && mouse.X < ListWidth - ScrollbarGutter + ScrollbarWidth
                        && mouse.Y >= 0 && mouse.Y < HeightPixels;
                    if (!insideGutter || ScrollbarZone((int)mouse.Y) != HeldZone)
                    {
                        HeldPressed = false;
                        HeldZone = 0;
                    }
                    else if (NowMs - RepeatStartMs > 200)
                    {
                        FireZone(HeldZone);
                        AutorepeatFires++;
                        RepeatStartMs = NowMs;
                    }
                }
            }
            if (RampActive) RampStep();
            if (state.WindowFocused && (ActiveList == this || (ActiveList == null && KeyboardActive)))
            {
                if (state.NewKeys.Contains(Keys.Up)) Select(Math.Max(0, SelectedIndex - 1));
                if (state.NewKeys.Contains(Keys.Down)) Select(SelectedIndex + 1);
                if (state.NewKeys.Contains(Keys.Home)) Select(0);
                if (state.NewKeys.Contains(Keys.End)) Select(Items.Count - 1);
                if (state.NewKeys.Contains(Keys.PageUp)) Select(Math.Max(0, SelectedIndex - VisibleRows));
                if (state.NewKeys.Contains(Keys.PageDown)) Select(SelectedIndex + VisibleRows);
            }
            RefreshRows();
            if (HoverRow >= 0 && HoverRow < Rows.Count && TopRow + HoverRow != SelectedIndex)
                Rows[HoverRow].Color = Color.White;
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            DrawLocalTexture(batch, TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice), null,
                Vector2.Zero, new Vector2(ListWidth, HeightPixels), BackgroundColor);
            if (Scrollbar != null && MaxTopRow > 0)
            {
                // Vertical group: up/up-down, thumb, track, down/down-down.
                Cell(batch, 9, ScrollbarCellHeight, TrackHeight);
                Cell(batch, 6, 0, ScrollbarCellHeight);
                Cell(batch, 10, HeightPixels - ScrollbarCellHeight, ScrollbarCellHeight);
                Cell(batch, 8, ThumbY, ThumbHeight);
            }
            base.Draw(batch);
        }

        private void Cell(UISpriteBatch batch, int cell, int y, int height)
        {
            DrawLocalTexture(batch, Scrollbar, new Rectangle(cell * ScrollbarWidth, 0, ScrollbarWidth, 20),
                new Vector2(ListWidth - ScrollbarGutter, y), new Vector2(1, height / 20f));
        }

        // The native row text is clipped by its exact caption rectangle.
        private sealed class ClippedListText : UIOriginalText
        {
            private readonly int WidthPixels, HeightPixels;
            public ClippedListText(string text, OriginalGlyphFont font, int width, int height) : base(text, font)
            { WidthPixels = width; HeightPixels = height; Size = new Vector2(width, height); }
            public override void Draw(UISpriteBatch batch)
            {
                if (!Visible || Font?.Atlas == null || string.IsNullOrEmpty(Text)) return;
                int x = 0;
                foreach (char original in Text)
                {
                    char c = OriginalGlyphFont.MapChar(original);
                    OriginalGlyphFont.Glyph g;
                    if (Font.ByChar.TryGetValue(c, out g) && g.W > 1)
                    {
                        var dst = new Rectangle(x, Font.GlyphY(g), g.W, g.H);
                        var clip = Rectangle.Intersect(dst, new Rectangle(0, 0, WidthPixels, HeightPixels));
                        if (clip.Width > 0 && clip.Height > 0)
                            DrawLocalTexture(batch, Font.Atlas,
                                new Rectangle(g.U + clip.X - dst.X, g.V + clip.Y - dst.Y, clip.Width, clip.Height),
                                new Vector2(clip.X, clip.Y), Vector2.One, Color * Opacity);
                    }
                    x += Font.Advance(c);
                    if (x >= WidthPixels) break;
                }
                StringsDrawn++;
            }
        }
    }
}
