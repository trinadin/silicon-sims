using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using FSO.Common.Rendering.Framework.Model;

namespace Simitone.Client.UI.Controls
{
    // R132: the ORIGINAL People-panel ratings bar (the *SubBars gadgets).
    // Engine decode (The Sims Complete):
    //   * cWinSubpanelHouse::Init 0x2a0ccc-0x2a0ce8: gadget factory
    //     0x3b6190(kHouseSubpanelBars 4900 -> cpanel\HouseSubBars.bmp 40x16,
    //     slot this+204) and 0x3b6190(kHouseSubBarsOff 4919 ->
    //     cpanel\Backgrounds\HouseSubBarsOff.BMP 40x16, slot this+208).
    //   * cWinSubpanelJob::Init 0x2a3fb0-0x2a3ff0: LANGUAGE-conditional —
    //     English builds kJobSubpanelBars 4901 -> cpanel\JobSubBars.bmp 40x11
    //     + kJobSubBarsOff 4920 (Backgrounds\JobSubBarsOff.bmp 40x11); the
    //     non-English variant falls back to the HOUSE art (0x20c670 = the
    //     non-English test).
    //   * cWinSubpanelHouse::TSPaint 0x2a0458-0x2a06a0 walks a 9-slot point
    //     table; the rating slot clamps the value to [0,100], then the fill
    //     width is 4*(value/10) px (mulhw 0x66666667 + srawi 2 = signed /10,
    //     then slw 2) — a 10-step bar, 4 px per step, 40 px = full sheet
    //     width. The Off backdrop is drawn first (vtable+160 fill), then the
    //     sheet region (0,0,fillW,H) blits over it, offset by the slot's
    //     point-table entry.
    // The value sources are the HouseStats getter family (0x8bba8-0x8c174:
    // GetLayoutScore/GetUpkeepScore/GetYardScore/GetFurnishingsScore/
    // GetSizeScore...) — their computation is NOT yet decoded (R133 target);
    // the port takes the value as an input. The 63x26 Buttons strips
    // (HouseBars/JobBars/RelBars 4800-4802) are the PIE-menu bars, a
    // separate surface.
    public class UIOriginalRatingBar : UIElement
    {
        public Texture2D BackdropTex;   // the *SubBarsOff member
        public Texture2D BarsTex;       // the *SubBars sheet

        public int Value;               // target rating, engine range [0,100]

        // the decoded engine fill: clamp to [0,100], then 4 * (value / 10) px
        // (integer division — value 49 -> 16 px, value 50 -> 20 px)
        public static int ComputeFill(int value)
        {
            var v = Math.Max(0, Math.Min(100, value));
            return 4 * (v / 10);
        }

        public int FillWidth { get { return ComputeFill(Value); } }

        private static Texture2D Resolve(string iffName, string pngName)
        {
            try
            {
                var iffTx = Simitone.Client.UI.Model.UIOriginal.EnsureResolved(iffName);
                if (iffTx != null) return iffTx.Get(GameFacade.GraphicsDevice);
            }
            catch { }
            try
            {
                var px = Content.Get().CustomUI.Get(pngName);
                if (px != null) return px.Get(GameFacade.GraphicsDevice);
            }
            catch { }
            return null;
        }

        public UIOriginalRatingBar(string iffOff, string iffOn, string pngOff, string pngOn)
        {
            BackdropTex = Resolve(iffOff, pngOff);
            BarsTex = Resolve(iffOn, pngOn);
        }

        public static UIOriginalRatingBar HouseBar()
        {
            return new UIOriginalRatingBar(
                "cpanel\\Backgrounds\\HouseSubBarsOff.BMP", "cpanel\\HouseSubBars.bmp",
                "rating_house_off.png", "rating_house_on.png");
        }

        public static UIOriginalRatingBar JobBar()
        {
            return new UIOriginalRatingBar(
                "cpanel\\Backgrounds\\JobSubBarsOff.bmp", "cpanel\\JobSubBars.bmp",
                "rating_job_off.png", "rating_job_on.png");
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            if (BackdropTex != null)
            {
                DrawLocalTexture(batch, BackdropTex, null, Vector2.Zero, Vector2.One);
            }
            var w = FillWidth;
            if (BarsTex != null && w > 0)
            {
                DrawLocalTexture(batch, BarsTex, new Rectangle(0, 0, w, BarsTex.Height),
                    Vector2.Zero, Vector2.One);
            }
        }
    }
}
