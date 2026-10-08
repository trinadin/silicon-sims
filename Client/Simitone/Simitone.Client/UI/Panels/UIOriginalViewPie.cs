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
    /// spokes).
    /// UI-37 UPDATE — UpdateViewMenu FULLY DECODED (image 0x1020ccb0, raw
    /// 0x2051c0-space): the item ladder is mag = diff/42 + 1 (the r217
    /// "/42 quantize magic"; the getter pair resolves through the pie
    /// vtable — modeled here as cursor distance past the ctor's 20px
    /// inactive radius, +0x178 = 0x14), commit deltas zoom = ±mag clamped
    /// to zoom state [1,3] and rotation = ±mag clamped ±2, and the CELL
    /// ladder is exact: zoom base cells by state {0:(0,0), 1:(4,0),
    /// 2:(1,1), 3:(0,4)} (ZoomIn,ZoomOut), rotation base 0, with the
    /// pending-delta preview +1 (|d|==1) / +3 (|d|==2) and +1 more while a
    /// shift key is held; |d|==3 previews no cell change (the native
    /// switch falls through).
    /// POP GESTURE re-attempted with the improved tooling (UI-37):
    /// cWinViewControl::TSOnMouseDownL @0x102aac80 is provably EMPTY
    /// (returns 1); cTSWinBtn::TSOnMouseDownL/MouseMove contain no
    /// drag-distance threshold; the cTSPieMenu vtable (TOC-0x60D0 ->
    /// sec1+0x75d00) survives only as unrelocated section-relative words,
    /// so the popup entry points (Popup/PopupBegin/BeginPopupWindow) are
    /// virtual-dispatch-only — the r218 verdict CONFIRMED with new
    /// evidence. DragThreshold stays a disclosed port model.
    /// DoSpeedTransitionSound attribution CORRECTED (UI-37): it is NOT a
    /// zoom/rotate sound — fully decoded at 0x102aa100 it maps the
    /// PAUSE/1/2/3 game-speed states through the 4x4 "UI_speed_XtoY"
    /// name table (sec1+0x5ae58; the 12 events exist in
    /// SoundData/SimsGeneratedHitSource.hot) and is called only from the
    /// speed-button branches (+0x110..+0x11c) of cWinViewControl::
    /// TSOnCommand; the zoom/rotate branches (+0xd0..+0xdc) call
    /// CPState::Rotate/Zoom with NO sound. The port already plays the
    /// exact matrix (UIClockPanel.SwitchSpeed). No zoom sound exists to
    /// recover.
    /// </summary>
    public class UIOriginalViewPie : UIContainer
    {
        public const int OriginalRadius = 90;   // cDDDSimsView::Init vtable+488
        public const int DragThreshold = 10;    // port model (disclosed; UI-37 re-attempt confirmed unrecoverable)
        public const int InactiveRadius = 20;   // cTSPieMenu ctor +0x178 = 0x14
        public const int MagnitudeQuantum = 42; // UpdateViewMenu /0x2a quantize

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
        private readonly Action<string, int> _onSelect;
        private readonly Action _onClose;
        private readonly Func<int> _zoomLevel;
        private double _grow;
        private bool _done;
        private bool _wasDown = true;   // opened during a press
        private int _hoverMag = 1;      // UI-37: the /42 magnitude under the cursor

        public UIOriginalViewPie(Action<string, int> onSelect, Func<int> zoomLevel, Action onClose = null)
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
        /// UI-37 decoded law — cDDDSimsView::UpdateViewMenu @ image 0x1020ccb0.
        /// zoomState (native +0x115c) picks the base cells: {0:(0,0), 1:(4,0),
        /// 2:(1,1), 3:(0,4)} for (ZoomIn, ZoomOut); rotation base is 0. A
        /// pending delta of ±1 adds shift+1, ±2 adds shift+3 to the stepping
        /// item's cell; ±3 adds nothing (the native switch falls through).
        /// </summary>
        public int CellFor(int itemIndex, int pendingDelta = 0, bool shiftHeld = false)
        {
            var level = (_zoomLevel != null) ? Math.Max(0, Math.Min(3, _zoomLevel())) : 2;
            var key = (itemIndex < Items.Count) ? Items[itemIndex].Key : ItemKeyByIndex(itemIndex);
            int bonus = shiftHeld ? 1 : 0;
            int mag = Math.Abs(pendingDelta);
            int step = mag == 2 ? bonus + 3 : mag == 1 ? bonus + 1 : 0;
            switch (key)
            {
                case "zoomin":
                    {
                        int col = level == 1 ? 4 : level == 2 ? 1 : 0; // state0 -> 0
                        return pendingDelta > 0 ? Math.Min(8, col + step) : col;
                    }
                case "zoomout":
                    {
                        int col = level == 2 ? 1 : level == 3 ? 4 : 0;
                        return pendingDelta < 0 ? Math.Min(8, col + step) : col;
                    }
                case "rotright": return pendingDelta > 0 ? step : 0;
                default: return pendingDelta < 0 ? step : 0; // rotleft
            }
        }

        /// <summary>The UI-37 magnitude model: the /42 quantize over the
        /// cursor distance past the ctor's 20px inactive radius, clamped to
        /// the ladder's 1..3 (the native getter pair is vtable-dispatched —
        /// disclosed model of the exact /42 + 1 arithmetic).</summary>
        public static int MagnitudeFor(float distanceFromCenter)
        {
            return Math.Max(1, Math.Min(3, (int)(distanceFromCenter - InactiveRadius) / MagnitudeQuantum + 1));
        }

        /// <summary>The commit delta for an item key on the decoded
        /// UpdateViewMenu clamps: zoom ±mag keeps state in [1,3];
        /// rotation ±mag clamps to ±2.</summary>
        public static int CommitDelta(string key, int mag)
        {
            switch (key)
            {
                case "zoomin": return mag;
                case "zoomout": return -mag;
                case "rotright": return Math.Min(2, mag);
                default: return -Math.Min(2, mag); // rotleft
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
            bool shift = Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)
                || Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
            for (int i = 0; i < Items.Count; i++)
            {
                int delta = (i == HoverIndex && HoverIndex > -1)
                    ? CommitDelta(Items[i].Key, _hoverMag) : 0;
                var old = Items[i].Icon.Texture;
                Items[i].Icon.Texture = CropCell(Items[i], CellFor(i, delta, shift));
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
            int newMag = hover > -1 ? MagnitudeFor(dist) : 1;
            if (hover != HoverIndex || newMag != _hoverMag)
            {
                if (hover > -1) HITVM.Get().PlaySoundEvent(FSO.Client.UI.Model.UISounds.PieMenuHighlight);
                HoverIndex = hover;
                _hoverMag = newMag;
                RefreshCells(); // the ladder previews the pending step (UI-37)
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
                var mag = _hoverMag;
                try { _onSelect(item.Key, mag); } catch { }
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
