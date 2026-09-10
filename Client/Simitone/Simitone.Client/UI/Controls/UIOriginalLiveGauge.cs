using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using FSO.Common.Rendering.Framework.Model;

namespace Simitone.Client.UI.Controls
{
    // R131: the ORIGINAL live-tab mood gauge (cWinPeople, engine id kLiveModeGauge
    // = 4911 -> cpanel\Backgrounds\LiveGadget.BMP, 108x100). The engine composition
    // (decoded from The Sims Complete, cWinPeople::Init 0x28eee0-0x28eeec +
    // TSPaint 0x28d5fc-0x28da68):
    //   * the backdrop blits once per paint through the panel canvas;
    //   * the fill is the SAME motive-bar art as the needs bars — gadgets
    //     kGreenbars (4506) / kRedBars (4510, cpanel\Greenbars.bmp/Redbars.bmp,
    //     27x25) — blitted as a strip of height rating*5 px where
    //     rating = min(|animated mood|, 5);
    //   * the animated value is a FLOAT-EASED mood (fsubs/fadds/fmuls chain in
    //     TSPaint); the sign selects the bar: mood >= 0 -> green, < 0 -> red
    //     (the engine's rating is sign-flipped: base - value).
// Port mapping: the R135 TOC-resolution recipe recovered the engine's const
// pool from FILE DATA — the rating law is exact (see Rating below); the
// art placement (channel x=41,y=5) and the second channel remain the
// disclosed port-side interpretations (the engine's {dx,dy} offset pairs
// at [TOC-20396/-20400/-20404] are BSS — proven runtime-only, r135).
// The 'Mood Rating' caption (STR# 154 'MiscStrings' [15]; ratings family
// House/Friend/Job/Mood = [12]/[13]/[14]/[15]) is hosted by
// UIMotiveSubpanel, like the motive name labels.
    public class UIOriginalLiveGauge : UIElement
    {
        public static Texture2D BackdropTex;
        public static Texture2D GreenTex;
        public static Texture2D RedTex;
        public static int ArtLoads;

        public int MoodValue;          // target, short range [-100,100]
        public float DisplayMood;      // engine-style eased display value

        // R135: the EXACT engine rating law, constants recovered from the
        // original binary's const pool ([TOC-20376] -> file 0x5a45ec):
        // {+0x14 0.0, +0x18 0.5, +0x1c 11.0, +0x20 100.0, +0x24 201.0, +0x28 5.0}.
        // cWinPeople::TSPaint 0x28d718-0x28d7a8:
        //   f1 = 11.0f*(100.0f + mood)/201.0f - 5.0f;
        //   rating = symround(f1) clamped to [-5, +5];
        //   rating < 0 -> kRedBars, > 0 -> kGreenbars; height = |rating|*5 px.
        // Check points: 100 -> +5, 50 -> +3, 0 -> 0, -50 -> -2, -60 -> -3, -100 -> -5.
        public static int Rating(int mood)
        {
            float f1 = (11.0f * (100.0f + mood)) / 201.0f - 5.0f;
            int r = (f1 >= 0f) ? (int)(0.5f + f1) : (int)(f1 - 0.5f); // symmetric round
            if (r > 5) r = 5;
            if (r < -5) r = -5;
            return r;
        }

        // engine strip height: |rating| * 5 px (max 25 = the full bar art height)
        public int FillHeight
        {
            get { return System.Math.Abs(Rating((int)DisplayMood)) * 5; }
        }

        // engine polarity: rating < 0 -> kRedbars, else kGreenbars
        public Texture2D CurrentBarTex
        {
            get { return Rating((int)DisplayMood) < 0 ? RedTex : GreenTex; }
        }

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

        public static void LoadArt()
        {
            if (ArtLoads > 0) return;
            ArtLoads++;
            BackdropTex = Resolve("cpanel\\Backgrounds\\LiveGadget.bmp", "gauge_live.png");
            GreenTex = Resolve("cpanel\\Greenbars.BMP", "bars_green.png");
            RedTex = Resolve("cpanel\\Redbars.bmp", "bars_red.png");
        }

        public UIOriginalLiveGauge()
        {
            LoadArt();
        }

        public void StepEase()
        {
            // R135 correction: the engine chain (TSPaint 0x28d718+) is a MEMORYLESS
            // per-paint transform of the motive value — no ease state exists there
            // (R131's "float-eased display" was a misread of the round/compare
            // selects). The display tracks the target directly.
            DisplayMood = MoodValue;
        }

        public override void Update(UpdateState state)
        {
            StepEase();
            base.Update(state);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            if (BackdropTex != null)
            {
                DrawLocalTexture(batch, BackdropTex, null, Vector2.Zero, Vector2.One);
            }
            var bar = CurrentBarTex;
            if (bar != null)
            {
                var h = FillHeight;
                if (h > 0)
                {
                    // top h rows of the 27x25 bar art, natural width, in the left channel
                    DrawLocalTexture(batch, bar, new Rectangle(0, 0, bar.Width, h),
                        new Vector2(41, 5), Vector2.One);
                }
            }
        }
    }
}
