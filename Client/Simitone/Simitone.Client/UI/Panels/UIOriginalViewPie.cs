using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Controls;
using FSO.HIT;
using FSO.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Simitone.Client.UI.Controls;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// R218: the VIEW pie — the engine's cDDDSimsView-owned cTSPieMenu
    /// (view+0x13c) with the four ViewMenu sheet items. The r217 decode:
    /// cDDDSimsView::Init 0x218c64-0x218f98 creates the four cTSWinBtn
    /// sheets (ViewMenuZoomIn/Out 9x1 42x84 cells, ViewMenuRotateL/R 5x1
    /// 84x42 cells) and registers them into the pie with
    /// cTSPieMenu::InsertString in the order ZoomOut, RotateRight, ZoomIn,
    /// RotateLeft; the pie's popup background = member 825
    /// cpanel\ViewMenuBackground.bmp (17x17 chip), max radius 90, label
    /// color RGB(187,187,187) (r142 dialog-chrome-law §5); slice count from
    /// the Layout quantizer (4 items -> 8 slices, items on the even
    /// spokes); UpdateViewMenu @0x215b42 refreshes the item ladder cells.
    /// The pop gesture is not statically recoverable (virtual dispatch
    /// through the window manager; the panel buttons' click path provably
    /// only steps): the port models the classic tear-off — dragging >=
    /// DragThreshold px while a small zoom/rotate button is held opens the
    /// radial at that button; a plain click still steps. Item cells follow
    /// the UpdateViewMenu ladder law quantized onto the port's 3-level zoom
    /// (base cell = level*2) and 4-quarter rotation (center cell 2, +/-1
    /// preview) — disclosed models.
    /// </summary>
    public class UIOriginalViewPie : UIContainer
    {
        public const int OriginalRadius = 90;   // cDDDSimsView::Init vtable+488
        public const int DragThreshold = 10;    // port model (disclosed)

        // gate counters
        public static int PiesOpened;
        public static int ItemsSelected;
        public static int Cancels;
        public static string LastSelected;

        public class ViewPieItem
        {
            public string Key;          // zoomout | rotright | zoomin | rotleft
            public string Caption;      // STR# 138 tooltip names
            public Texture2D Sheet;     // the full original sheet
            public UIImage Icon;
            public UIOriginalText Label;
            public int CellW, CellH;
            public Vector2 Center;      // pie-local radial position
        }

        public readonly List<ViewPieItem> Items = new List<ViewPieItem>();
        public int HoverIndex = -1;

        private readonly UIImage _bg;
        private readonly Action<string> _onSelect;
        private readonly Action _onClose;
        private readonly Func<int> _zoomLevel;
        private double _grow;
        private bool _done;
        private bool _wasDown = true;   // opened during a press

        public UIOriginalViewPie(Action<string> onSelect, Func<int> zoomLevel, Action onClose = null)
        {
            _onSelect = onSelect;
            _zoomLevel = zoomLevel;
            _onClose = onClose;
            var gd = GameFacade.GraphicsDevice;

            // the pie disc — the same original chip the engine scales (r142).
            var bgTex = Simitone.Client.UI.Model.UIOriginal.EnsureResolved("cpanel\\ViewMenuBackground.bmp")?.Get(gd);
            _bg = new UIImage(bgTex);
            _bg.SetSize(2, 2); // scaled up by the grow animation
            _bg.Position = new Vector2(-1, -1);
            AddAt(0, _bg);

            // the four items in the engine's InsertString order: ZoomOut,
            // RotateRight, ZoomIn, RotateLeft — spokes 0/2/4/6 of 8.
            var defs = new[]
            {
                new { key = "zoomout", member = "cpanel\\ViewMenuZoomOut.bmp",  cols = 9, str = "6" },
                new { key = "rotright", member = "cpanel\\ViewMenuRotateRight.bmp", cols = 5, str = "4" },
                new { key = "zoomin",  member = "cpanel\\ViewMenuZoomIn.bmp",   cols = 9, str = "5" },
                new { key = "rotleft", member = "cpanel\\ViewMenuRotateLeft.bmp",  cols = 5, str = "3" },
            };
            var font = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadByIndex(8, gd);
            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                var sheet = Simitone.Client.UI.Model.UIOriginal.EnsureResolved(d.member)?.Get(gd);
                if (sheet == null) continue;
                var item = new ViewPieItem
                {
                    Key = d.key,
                    Caption = GameFacade.Strings.GetString("138", d.str),
                    Sheet = sheet,
                    CellW = sheet.Width / d.cols,
                    CellH = sheet.Height,
                };
                double dir = ((double)i / 4.0) * Math.PI * 2;
                item.Center = new Vector2(
                    (float)(Math.Sin(dir) * OriginalRadius),
                    (float)(Math.Cos(dir) * -OriginalRadius));
                item.Icon = new UIImage(CropCell(item, CellFor(i)));
                item.Icon.Position = item.Center - new Vector2(item.CellW / 2f, item.CellH / 2f);
                Add(item.Icon);
                if (font != null)
                {
                    item.Label = new UIOriginalText(item.Caption, font)
                    {
                        Color = new Color(187, 187, 187) // the pie label color (r142)
                    };
                    Add(item.Label);
                }
                Items.Add(item);
            }
            LayoutLabels();
            RefreshCells();
        }

        /// <summary>
        /// The UpdateViewMenu ladder model: each item's cell previews its own
        /// step from the current state (zoom base cell = level*2 of 9; the
        /// rotate items sit at +/-1 of the 5-cell center 2).
        /// </summary>
        public int CellFor(int itemIndex)
        {
            var level = (_zoomLevel != null) ? Math.Max(1, Math.Min(3, _zoomLevel())) : 2;
            var key = (itemIndex < Items.Count) ? Items[itemIndex].Key : ItemKeyByIndex(itemIndex);
            switch (key)
            {
                case "zoomin": return Math.Min(8, level * 2 + 1);
                case "zoomout": return Math.Max(0, level * 2 - 1);
                case "rotright": return 3;
                default: return 1; // rotleft
            }
        }

        private static string ItemKeyByIndex(int i)
        {
            switch (i) { case 0: return "zoomout"; case 1: return "rotright"; case 2: return "zoomin"; default: return "rotleft"; }
        }

        /// <summary>Re-derives an item's icon from its sheet at the given cell.</summary>
        public Texture2D CropCell(ViewPieItem item, int cell)
        {
            var gd = item.Sheet.GraphicsDevice;
            var src = new Rectangle(cell * item.CellW, 0, item.CellW, item.CellH);
            var data = new Color[item.CellW * item.CellH];
            item.Sheet.GetData(0, src, data, 0, data.Length);
            var tex = new Texture2D(gd, item.CellW, item.CellH);
            tex.SetData(data);
            Simitone.Client.UI.Model.UIArtProvenance.NoteOriginal(tex, "viewmenu-cell"); // R210 registry
            return tex;
        }

        private void LayoutLabels()
        {
            foreach (var item in Items)
            {
                if (item.Label == null) continue;
                var w = item.Label.Font != null ? item.Label.Font.Measure(item.Caption) : 0;
                // captions sit just outside their icon, away from the center
                var outward = (item.Center.Length() > 0) ? Vector2.Normalize(item.Center) : -Vector2.UnitY;
                item.Label.Position = item.Center + outward * (item.CellH / 2f + 12f) - new Vector2(w / 2f, 6f);
            }
        }

        private void RefreshCells()
        {
            for (int i = 0; i < Items.Count; i++)
            {
                var old = Items[i].Icon.Texture;
                Items[i].Icon.Texture = CropCell(Items[i], CellFor(i));
                if (old != null) old.Dispose();
                Items[i].Icon.SetSize(Items[i].CellW, Items[i].CellH);
            }
        }

        public override void Update(FSO.Common.Rendering.Framework.Model.UpdateState state)
        {
            base.Update(state);
            if (_done) return;
            if (_grow < 1)
            {
                _grow += 1.0 / 30.0 * (60.0 / FSOEnvironment.RefreshRate);
                _bg.SetSize((float)_grow * 200, (float)_grow * 200);
                _bg.X = (float)_grow * (-100);
                _bg.Y = (float)_grow * (-100);
            }

            // engine CalcItem @0x527590: angle/distance from the popup center.
            var p = GlobalPoint(new Vector2(state.MouseState.X, state.MouseState.Y));
            int hover = -1;
            var dist = p.Length();
            if (dist <= OriginalRadius + 24)
            {
                double ang = Math.Atan2(p.X, -p.Y);
                if (ang < 0) ang += Math.PI * 2;
                var slice = (int)Math.Floor(ang / (Math.PI / 4));
                if ((slice & 1) == 0) hover = slice / 2;
            }
            if (hover >= Items.Count) hover = -1;
            if (hover != HoverIndex)
            {
                if (hover > -1) HITVM.Get().PlaySoundEvent(FSO.Client.UI.Model.UISounds.PieMenuHighlight);
                HoverIndex = hover;
            }

            // the pie completes on the release of the gesture that opened it
            // (cTSPieMenu::TSOnMouseUpL); Escape cancels (TSOnKeyDown).
            var down = state.MouseState.LeftButton == ButtonState.Pressed;
            var released = _wasDown && !down;
            _wasDown = down;
            var esc = state.KeyboardState.IsKeyDown(Keys.Escape);
            if (esc || released) Complete(HoverIndex, esc);
        }

        /// <summary>The gate path — the same completion the release runs.</summary>
        public void CompleteForProbe(int hoverIndex, bool cancel)
        {
            Complete(hoverIndex, cancel);
        }

        private void Complete(int hoverIndex, bool cancelled)
        {
            if (_done) return;
            _done = true;
            if (!cancelled && hoverIndex > -1 && hoverIndex < Items.Count)
            {
                var item = Items[hoverIndex];
                LastSelected = item.Key;
                ItemsSelected++;
                HITVM.Get().PlaySoundEvent(FSO.Client.UI.Model.UISounds.PieMenuSelect);
                try { _onSelect(item.Key); } catch { }
            }
            else Cancels++;
            var parent = Parent as UIContainer;
            if (parent != null) parent.Remove(this);
            // ALWAYS tell the owner (the UCP drops its reference on cancel
            // too — a stale one would suppress the step handlers forever).
            try { if (_onClose != null) _onClose(); } catch { }
        }
    }
}
