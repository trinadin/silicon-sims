// ORIG-01 asset audit #1: this surface IS TS1-triggered (205 person-data-1
// 'MoneyAmmountOverHead' write instructions across 100+ shipped objects —
// MailBox, Fridges, Easel, Global.iff and every expansion); the old header's
// "no TS1 counterpart" was wrong about reachability. ORIG-02
// money-headline-renderer-law closed the renderer question to the floor:
// the native has NO dedicated money renderer, window, art asset, or layout
// table — the amount is drawn as generic float text through the Animator's
// mode-1 text pass (the same Animator::Render family as the head arrow;
// census: no money* member in UIGraphics.far or Sprites.iff). The chrome
// below therefore remains the port's most original composition: the game's
// own SpeechMedium.bmp nine-slice behind original .ffn bold money glyphs —
// exactly the UCP plate floater (R194). The TSO-era money_bg.png pill is
// gone.
﻿using FSO.SimAntics.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSO.LotView;
using Microsoft.Xna.Framework.Graphics;
using FSO.Client.UI.Framework;
using Microsoft.Xna.Framework;
using FSO.Client;

namespace Simitone.Client.UI.Panels.WorldUI
{
    public class UIMoneyHeadline : VMHeadlineRenderer
    {
        private RenderTarget2D MoneyTarget;
        private TextStyle Style;
        private Texture2D Tile;          // Other\SpeechMedium.bmp (16x16, 4px corners)
        private Texture2D DeprecatedMoneyBG; // unused; kept null
        private string Text;
        private const int Corner = 4;

        public UIMoneyHeadline(VMRuntimeHeadline headline) : base(headline)
        {
            Style = TextStyle.DefaultLabel.Clone();
            Style.Size = 12;
            var value = (int)(headline.Operand.Flags2 | (headline.Operand.Duration << 16));
            if (value < -10000)
            {
                Text = (-10000 - value).ToString();
                Style.Color = Model.UIStyle.Current.SecondaryText;
            }
            else
            {
                // AUD-17 C1-4: one signed value covers every case; the color
                // law mirrors the UCP plate floater (Text / NegMoney).
                Text = "§" + value;
                Style.Color = (value < 0) ? Model.UIStyle.Current.NegMoney : Model.UIStyle.Current.Text;
            }
            var measure = Style.MeasureString(Text);
            Tile = Simitone.Client.UI.Model.UIOriginal.EnsureResolved("Other\\SpeechMedium.bmp")?.Get(GameFacade.GraphicsDevice);

            var GD = GameFacade.GraphicsDevice;
            // +2*(corner+2) chrome padding; +30 headroom for the per-frame
            // duration offset the world renderer applies inside the target.
            MoneyTarget = new RenderTarget2D(GD, (int)measure.X + 2 * (Corner + 2), (int)measure.Y + 2 * Corner + 30);

            DrawNewFrame();
        }

        public void DrawNewFrame()
        {
            var GD = GameFacade.GraphicsDevice;
            GD.SetRenderTarget(MoneyTarget);
            GD.Clear(Color.Transparent);
            var batch = GameFacade.Screens.SpriteBatch;
            var opacity = (Headline.Duration / 60f);
            batch.Begin();
            float yOff = Headline.Duration / 2;
            var measure = Style.MeasureString(Text);
            int cw = (int)measure.X + 2 * (Corner + 2);
            int ch = (int)measure.Y + 2 * Corner;
            if (Tile != null)
            {
                // R194 nine-slice: corners, tiled edges, stretched center.
                int edge = Tile.Width - 2 * Corner;
                for (int gx = 0; gx < 3; gx++)
                {
                    int sx = gx * (Corner + (gx == 1 ? edge : 0));
                    int sw = (gx == 1) ? edge : Corner;
                    int dx = (gx == 0) ? 0 : (gx == 1 ? Corner : cw - Corner);
                    int dw = (gx == 1) ? cw - 2 * Corner : Corner;
                    for (int gy = 0; gy < 3; gy++)
                    {
                        int sy = gy * (Corner + (gy == 1 ? edge : 0));
                        int sh = (gy == 1) ? edge : Corner;
                        int dy = (gy == 0) ? 0 : (gy == 1 ? Corner : ch - Corner);
                        int dh = (gy == 1) ? ch - 2 * Corner : Corner;
                        if (dw <= 0 || dh <= 0) continue;
                        batch.Draw(Tile, new Rectangle(dx, (int)yOff + dy, dw, dh),
                            new Rectangle(sx, sy, sw, sh), Color.White * opacity);
                    }
                }
            }
            Style.Color.A = (byte)(opacity * 255);

            batch.End();
            Style.VFont.Draw(GD, Text, new Vector2(Corner + 2, yOff + Corner - 2), Style.Color, new Vector2(Style.Scale), null);

            GD.SetRenderTarget(null);
        }

        public override Texture2D DrawFrame(World world)
        {
            DrawNewFrame();
            return MoneyTarget;
        }

        public override void Dispose()
        {
            // AUD-17 C1-4: the per-headline render target was never disposed
            // (sibling UIHeadlineRenderer disposes its texture).
            MoneyTarget?.Dispose();
            base.Dispose();
        }
    }
}
