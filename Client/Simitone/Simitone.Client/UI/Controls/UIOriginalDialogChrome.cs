using FSO.Client.UI.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Simitone.Client.UI.Controls
{
    /// <summary>
    /// R142 — the ORIGINAL system dialog chrome, decoded from the engine this round
    /// (tools/iff-dump/r142/dialog-chrome-law.md):
    ///
    /// - Frame: SMCtrlMgrRes id 22 kSMSystemGenDlgEdge = shared\dlg\GenDlg.bmp, a
    ///   30x30 nine-slice tile (10px corners/edges/center — measured off the tile:
    ///   beveled slate border band ~10px, near-black center). Fetched by
    ///   cTSWinGenDlg::Init via cTSFrameWork_CtrlMgr()->SystemBMP(9).
    /// - Fill: cSimsApp::GetDialogFillColor @ 0x253fe0 = hard-coded RGB(0, 0, 82).
    /// - Buttons: id 17 kSMSystemBtn = shared\sys\WinBtn.bmp 260x33 = 4 states of
    ///   65x33 (normal/hilite/pressed/disabled); the engine stretches one state
    ///   buffer to the button rect (classic system-control behavior).
    /// - PositionButtons law (cTSWinMsgBox::PositionButtons @ 0x5234b0):
    ///   maxW = widest button; spacing = (width - 32 - n*maxW) / (n+1), clamped to
    ///   minimum 16; first button left edge at 16 + spacing, advanced by
    ///   (spacing + maxW); bottom-aligned with a 32px inset.
    /// - Auto-size (cTSWinMsgBox::Init @ 0x524150): content height + 32, minimum
    ///   width 200; message-box timeout default 40000 ms.
    ///
    /// The nine-slice border widths live in a TOC-referenced engine data global that
    /// could not be recovered statically (dialog-chrome-law.md §7.1); 10/10/10 is the
    /// measured split of the 30x30 tile itself (disclosed interpretation).
    /// </summary>
    public static class UIOriginalDialogChrome
    {
        public static readonly Color FillColor = new Color(0, 0, 82, 255);
        public const int Border = 10;          // GenDlg tile: 30x30 at 10/10/10
        public const int PictureBorder = 12;   // PopupInfoTiles: 36x36 at 12/12/12
        public const int BtnSheetStates = 4;   // WinBtn 260x33 -> 4x 65x33

        private static Texture2D GenDlg;
        private static Texture2D PictureTiles;
        private static Texture2D WinBtn;
        private static Texture2D WhitePx;

        private static Texture2D Resolve(string member, ref Texture2D cache)
        {
            if (cache != null) return cache;
            try
            {
                var tx = Simitone.Client.UI.Model.UIOriginal.EnsureResolved(member);
                if (tx != null) cache = tx.Get(FSO.Client.GameFacade.GraphicsDevice);
            }
            catch { }
            return cache;
        }

        public static Texture2D GetGenDlg() { return Resolve("shared\\dlg\\GenDlg.bmp", ref GenDlg); }
        public static Texture2D GetPictureTiles() { return Resolve("cpanel\\Backgrounds\\PopupInfoTiles.bmp", ref PictureTiles); }
        public static Texture2D GetWinBtn() { return Resolve("shared\\sys\\WinBtn.bmp", ref WinBtn); }

        private static Texture2D GetWhitePx()
        {
            if (WhitePx == null) WhitePx = FSO.Common.Utils.TextureGenerator.GetPxWhite(FSO.Client.GameFacade.GraphicsDevice);
            return WhitePx;
        }

        /// <summary>True when the original system chrome is mountable (falls back to
        /// the caller's existing skin if the FAR members are unavailable).</summary>
        public static bool Available() { return GetGenDlg() != null && GetWinBtn() != null; }

        internal static float EdgeScaleForLength(int destinationLength)
        {
            return Math.Max(0, destinationLength) / (float)Border;
        }

        internal static float EdgeScaleForLength(int destinationLength, int sourceLength)
        {
            return Math.Max(0, destinationLength) / (float)Math.Max(1, sourceLength);
        }

        internal static int TiledSegmentCount(int destinationLength, int tileLength)
        {
            if (destinationLength <= 0) return 0;
            tileLength = Math.Max(1, tileLength);
            return (destinationLength + tileLength - 1) / tileLength;
        }

        internal static int TiledFinalSegmentLength(int destinationLength, int tileLength)
        {
            if (destinationLength <= 0) return 0;
            tileLength = Math.Max(1, tileLength);
            int remainder = destinationLength % tileLength;
            return remainder == 0 ? tileLength : remainder;
        }

        /// <summary>GenDlg nine-slice + the engine's RGB(0,0,82) interior fill,
        /// drawn in the given UIElement's local space at (x,y) size (w,h).</summary>
        public static void DrawWindow(UIElement el, UISpriteBatch batch, int x, int y, int w, int h)
        {
            var tile = GetGenDlg();
            if (tile == null) return;
            DrawNineSlice(el, batch, tile, Border, x, y, w, h);
        }

        /// <summary>
        /// cWinPictureDialog uses SystemBMP slot 5, PopupInfoTiles.bmp 36x36,
        /// whose thirds establish a 12px client inset. It is a separate window
        /// frame from cTSWinMsgBox's 30px GenDlg tile.
        /// </summary>
        public static void DrawPictureWindow(UIElement el, UISpriteBatch batch, int x, int y, int w, int h)
        {
            var tile = GetPictureTiles();
            if (tile == null)
            {
                DrawWindow(el, batch, x, y, w, h);
                return;
            }
            DrawTiledNineSlice(el, batch, tile, PictureBorder, x, y, w, h);
        }

        /// <summary>Draw an arbitrary original tiled-edge sheet whose source is
        /// a 3x3 grid of equal cells. cWinLotPopup uses the 111x111
        /// HotSpotPopupTiler with 37px cells through this same BltEdge law.</summary>
        public static void DrawTiledWindow(UIElement el, UISpriteBatch batch,
            Texture2D tile, int cell, int x, int y, int w, int h)
        {
            if (tile == null || cell <= 0) return;
            DrawTiledNineSlice(el, batch, tile, cell, x, y, w, h);
        }

        private static void DrawTiledNineSlice(UIElement el, UISpriteBatch batch,
            Texture2D tile, int b, int x, int y, int w, int h)
        {
            int iw = Math.Max(0, w - 2 * b);
            int ih = Math.Max(0, h - 2 * b);

            // cTSBuffer::BltEdge copies corners at native size and calls
            // BltTiled for all four edges and the center. The last repeat is
            // source-clipped; there is no solid fill under this frame.
            DrawTiledCell(el, batch, tile, new Rectangle(b, b, b, b),
                x + b, y + b, iw, ih);
            DrawTiledCell(el, batch, tile, new Rectangle(b, 0, b, b),
                x + b, y, iw, b);
            DrawTiledCell(el, batch, tile, new Rectangle(b, 2 * b, b, b),
                x + b, y + h - b, iw, b);
            DrawTiledCell(el, batch, tile, new Rectangle(0, b, b, b),
                x, y + b, b, ih);
            DrawTiledCell(el, batch, tile, new Rectangle(2 * b, b, b, b),
                x + w - b, y + b, b, ih);

            DrawSnappedTile(el, batch, tile, new Rectangle(0, 0, b, b), x, y);
            DrawSnappedTile(el, batch, tile, new Rectangle(2 * b, 0, b, b), x + w - b, y);
            DrawSnappedTile(el, batch, tile, new Rectangle(0, 2 * b, b, b), x, y + h - b);
            DrawSnappedTile(el, batch, tile, new Rectangle(2 * b, 2 * b, b, b), x + w - b, y + h - b);
        }

        private static void DrawTiledCell(UIElement el, UISpriteBatch batch,
            Texture2D tile, Rectangle source, int x, int y, int width, int height)
        {
            for (int dy = 0; dy < height; dy += source.Height)
            {
                int drawHeight = Math.Min(source.Height, height - dy);
                for (int dx = 0; dx < width; dx += source.Width)
                {
                    int drawWidth = Math.Min(source.Width, width - dx);
                    DrawSnappedTile(el, batch, tile,
                        new Rectangle(source.X, source.Y, drawWidth, drawHeight),
                        x + dx, y + dy);
                }
            }
        }

        private static void DrawSnappedTile(UIElement el, UISpriteBatch batch,
            Texture2D tile, Rectangle source, int x, int y)
        {
            // These axis-aligned native tiles share logical edges. Transform
            // both endpoints before snapping so adjacent cells share the same
            // physical edge at fractional DPI. Flooring each origin but keeping
            // a fractional width leaves one-pixel holes between some repeats.
            var topLeft = el.FlooredLocalPoint(new Vector2(x, y));
            var bottomRight = el.FlooredLocalPoint(new Vector2(x + source.Width, y + source.Height));
            var destination = new Rectangle((int)topLeft.X, (int)topLeft.Y,
                (int)(bottomRight.X - topLeft.X), (int)(bottomRight.Y - topLeft.Y));
            if (destination.Width <= 0 || destination.Height <= 0) return;
            // UIElement's BlendColor carries its opacity. The existing batch
            // transform remains in effect, including cached-container offsets.
            batch.Draw(tile, destination, source, el.BlendColor, 0f, Vector2.Zero, el.SpriteEffect, 0f);
        }

        private static void DrawNineSlice(UIElement el, UISpriteBatch batch,
            Texture2D tile, int b, int x, int y, int w, int h)
        {
            var fill = GetWhitePx();
            int iw = Math.Max(0, w - 2 * b), ih = Math.Max(0, h - 2 * b);

            // interior fill first (the engine paints MainFillColor over the tile)
            if (fill != null && iw > 0 && ih > 0)
                el.DrawLocalTexture(batch, fill, null, new Vector2(x + b, y + b), new Vector2(iw, ih), FillColor);

            // corners (natural size)
            el.DrawLocalTexture(batch, tile, new Rectangle?(new Rectangle(0, 0, b, b)), new Vector2(x, y), Vector2.One);
            el.DrawLocalTexture(batch, tile, new Rectangle?(new Rectangle(2 * b, 0, b, b)), new Vector2(x + w - b, y), Vector2.One);
            el.DrawLocalTexture(batch, tile, new Rectangle?(new Rectangle(0, 2 * b, b, b)), new Vector2(x, y + h - b), Vector2.One);
            el.DrawLocalTexture(batch, tile, new Rectangle?(new Rectangle(2 * b, 2 * b, b, b)), new Vector2(x + w - b, y + h - b), Vector2.One);
            // edges (stretched)
            if (iw > 0)
            {
                // DrawLocalTexture's final vector is a multiplier, not a destination
                // size. Each GenDlg edge source is b pixels long, so divide the
                // requested interior length by b (the old iw multiplier drew every
                // horizontal edge ten times too wide).
                float sx = EdgeScaleForLength(iw, b);
                el.DrawLocalTexture(batch, tile, new Rectangle?(new Rectangle(b, 0, b, b)), new Vector2(x + b, y), new Vector2(sx, 1));
                el.DrawLocalTexture(batch, tile, new Rectangle?(new Rectangle(b, 2 * b, b, b)), new Vector2(x + b, y + h - b), new Vector2(sx, 1));
            }
            if (ih > 0)
            {
                float sy = EdgeScaleForLength(ih, b);
                el.DrawLocalTexture(batch, tile, new Rectangle?(new Rectangle(0, b, b, b)), new Vector2(x, y + b), new Vector2(1, sy));
                el.DrawLocalTexture(batch, tile, new Rectangle?(new Rectangle(2 * b, b, b, b)), new Vector2(x + w - b, y + b), new Vector2(1, sy));
            }
        }

        /// <summary>The engine's button-row law: returns each button's left edge.
        /// spacing = (w - 32 - n*maxW)/(n+1) clamped to >= 16; first at 16+spacing.</summary>
        public static int[] ButtonLefts(int windowWidth, int n, int maxW)
        {
            var lefts = new int[n];
            int denom = n + 1;
            int spacing = (windowWidth - 32 - n * maxW) / denom;
            if (spacing < 16) spacing = 16;
            for (int i = 0; i < n; i++) lefts[i] = 16 + spacing + i * (spacing + maxW);
            return lefts;
        }

        /// <summary>Bottom-aligned button row: 32px inset from the window bottom,
        /// buttons vertically centered on the 33px WinBtn height.</summary>
        public static int ButtonTop(int windowHeight, int buttonHeight)
        {
            return windowHeight - 32 - buttonHeight;
        }
    }
}
