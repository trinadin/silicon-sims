using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Utils;
using System;
using System.Text;

namespace Simitone.Client
{
    /// <summary>
    /// R138 TEXT DIAGNOSTICS (-textdiag): renders sample strings through the
    /// REAL game text paths (direct OriginalGlyphFont.Draw, the full
    /// UILabel->DrawLocalString->OriginalVectorFont UI path) into a
    /// RenderTarget2D, reads the pixels back and prints ASCII art of BOTH the
    /// alpha channel and the RGB ink, plus coverage statistics, to stdout.
    ///
    /// Motivation: the user reports garbled/unreadable text across the board.
    /// Every prior gate pinned the .ffn corpus byte-verbatim ON DISK but never
    /// verified RENDERED PIXELS. This harness makes the pixels readable as
    /// text in a log (the agent cannot look at screenshots).
    ///
    /// Discriminates the premultiplied-alpha hypothesis: the atlas was built
    /// white-RGB/straight-alpha while all batches blend with
    /// BlendState.AlphaBlend (One, InverseSourceAlpha). With that mismatch,
    /// any pixel with alpha >= 1/15 saturates RGB to the full tint => every
    /// glyph renders as a solid block. Signature: rgb art solid where alpha
    /// art shows AA ramps; over an opaque background the glyph cells become
    /// near-solid rectangles.
    /// </summary>
    public static class TextDiagnostics
    {
        private static DateTime _deadline;
        private static bool _done;

        public static void Begin()
        {
            _deadline = DateTime.UtcNow.AddSeconds(120);
            Console.WriteLine("TEXTDIAG START");
            GameThread.EveryUpdate(s => Tick());
        }

        private static void Tick()
        {
            if (_done) return;
            try
            {
                var gd = GameFacade.GraphicsDevice;
                var content = FSO.Content.Content.Get();
                if (gd == null || content == null || GameFacade.VectorFont == null)
                {
                    if (DateTime.UtcNow > _deadline)
                    {
                        Console.WriteLine("TEXTDIAG TIMEOUT waiting for content/gd");
                        _done = true;
                        Environment.Exit(0);
                    }
                    return;
                }
                // the TS1 FAR provider finishes its scan asynchronously; retry
                // until the first table mounts (then everything is warm)
                try
                {
                    var probe = UI.Controls.OriginalGlyphFont.Load(
                        "Fonts\\variablesans_14.ffn", gd);
                    if (probe == null)
                    {
                        if (DateTime.UtcNow > _deadline)
                        {
                            Console.WriteLine("TEXTDIAG TIMEOUT waiting for far tables");
                            _done = true;
                            Environment.Exit(0);
                        }
                        return;
                    }
                }
                catch
                {
                    if (DateTime.UtcNow > _deadline)
                    {
                        Console.WriteLine("TEXTDIAG TIMEOUT far probe kept throwing");
                        _done = true;
                        Environment.Exit(0);
                    }
                    return;
                }
                Run(gd);
                _done = true;
                Environment.Exit(0);
            }
            catch (Exception e)
            {
                Console.WriteLine("TEXTDIAG EXC " + e.GetType().Name + ": " + e.Message);
                Console.WriteLine(e.StackTrace);
                _done = true;
                Environment.Exit(0);
            }
        }

        private static void Run(Microsoft.Xna.Framework.Graphics.GraphicsDevice gd)
        {
            // ---- table inventory ----
            var paths = new[] {
                "Fonts\\variablesans_07.ffn", "Fonts\\variablesans_08.ffn",
                "Fonts\\variablesans_09.ffn", "Fonts\\variablesans_10.ffn",
                "Fonts\\variablesans_11.ffn", "Fonts\\variablesans_12.ffn",
                "Fonts\\variablesans_14.ffn", "Fonts\\variablesans_16.ffn",
                "Fonts\\variablesans_18.ffn", "Fonts\\variablesans_20.ffn",
                "Fonts\\variablesans_12_bs.ffn", "Fonts\\variablesans_11_s.ffn",
                "Fonts\\variablesans_48.ffn"
            };
            UI.Controls.OriginalGlyphFont def14 = null, bs = null, sm = null;
            foreach (var p in paths)
            {
                var f = UI.Controls.OriginalGlyphFont.Load(p, gd);
                if (f == null) { Console.WriteLine("TEXTDIAG table " + p + ": LOAD-FAIL"); continue; }
                int missing = 0;
                for (int c = 32; c <= 255; c++) if (!f.ByChar.ContainsKey((char)c)) missing++;
                Console.WriteLine("TEXTDIAG table " + p + " chars=" + f.NumChars
                    + " atlas=" + f.AtlasW + "x" + f.AtlasH + " missing32_255=" + missing);
                if (p.EndsWith("_14.ffn")) def14 = f;
                if (p.EndsWith("_12_bs.ffn")) bs = f;
                if (p.EndsWith("_11_s.ffn")) sm = f;
            }

            if (def14 != null)
            {
                RenderDirect(gd, def14, "Handgloves 0123", Microsoft.Xna.Framework.Color.White,
                    Microsoft.Xna.Framework.Color.Transparent);
                RenderDirect(gd, def14, "Household Net Worth: \u00A712,345",
                    Microsoft.Xna.Framework.Color.White, Microsoft.Xna.Framework.Color.Transparent);
                // windows-1252 byte-range chars, as they arrive from STR# decoding
                RenderDirect(gd, def14, "win1252: \u0093Q\u0094 \u0092t\u0092 \u0085",
                    Microsoft.Xna.Framework.Color.White, Microsoft.Xna.Framework.Color.Transparent);
                // true-Unicode literals (expected to MISS the byte-keyed table)
                RenderDirect(gd, def14, "unicode: \u201CQ\u201D \u2019t\u2019 \u2026",
                    Microsoft.Xna.Framework.Color.White, Microsoft.Xna.Framework.Color.Transparent);
                // the user-visible case: ink over an opaque background
                RenderDirect(gd, def14, "Handgloves on bg",
                    new Microsoft.Xna.Framework.Color(255, 249, 157),
                    new Microsoft.Xna.Framework.Color(40, 40, 60));
                if (bs != null) RenderDirect(gd, bs, "Bold 12: Handgloves",
                    Microsoft.Xna.Framework.Color.White, Microsoft.Xna.Framework.Color.Transparent);
                if (sm != null) RenderDirect(gd, sm, "Small 11s: Handgloves",
                    Microsoft.Xna.Framework.Color.White, Microsoft.Xna.Framework.Color.Transparent);
            }

            // ---- full UI path (UILabel -> DrawLocalString -> VFont) ----
            RenderUILabel(gd, "UILabel12: The quick brown fox", 12);
            RenderUILabel(gd, "UILabel13: off-ladder scale", 13);

            // ---- R139b: the neighborhood house-name strings (user-reported
            // garbled tooltip text — this is the string source for the lot
            // hover captions and the house-select panel). Print the RAW
            // decoded values with escape codes so byte-level garbage shows.
            try
            {
                var neigh = FSO.Content.Content.Get().Neighborhood;
                if (neigh == null) Console.WriteLine("TEXTDIAG nbr: provider null");
                else
                {
                    for (int h = 1; h <= 12; h++)
                    {
                        var nd = neigh.GetHouseNameDesc(h);
                        string nm = nd != null ? (nd.Item1 ?? "") : "<null>";
                        string ds = nd != null ? (nd.Item2 ?? "") : "";
                        Console.WriteLine("TEXTDIAG nbr house=" + h
                            + " name=" + Escape(nm)
                            + " desc=" + Escape(ds.Length > 40 ? ds.Substring(0, 40) : ds));
                    }
                    var ndesc = neigh.NeighbourhoodDesc;
                    if (ndesc != null)
                    {
                        for (ushort id = 2001; id <= 2006; id++)
                        {
                            var str = ndesc.Get<FSO.Files.Formats.IFF.Chunks.STR>(id);
                            if (str == null) { Console.WriteLine("TEXTDIAG str " + id + ": null"); continue; }
                            var s0 = str.GetString(0) ?? "";
                            var s1 = str.GetString(1) ?? "";
                            Console.WriteLine("TEXTDIAG str " + id + " [0]=" + Escape(s0.Length > 48 ? s0.Substring(0, 48) : s0)
                                + " [1]=" + Escape(s1.Length > 48 ? s1.Substring(0, 48) : s1));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("TEXTDIAG nbr EXC " + e.GetType().Name + ": " + e.Message);
            }
            Console.WriteLine("TEXTDIAG END");
        }

        private static void RenderDirect(Microsoft.Xna.Framework.Graphics.GraphicsDevice gd,
            UI.Controls.OriginalGlyphFont font, string text,
            Microsoft.Xna.Framework.Color tint, Microsoft.Xna.Framework.Color bg)
        {
            var W = 480; var H = 48;
            var rt = new Microsoft.Xna.Framework.Graphics.RenderTarget2D(gd, W, H);
            gd.SetRenderTarget(rt);
            gd.Clear(bg);
            using (var sb = new Microsoft.Xna.Framework.Graphics.SpriteBatch(gd))
            {
                sb.Begin(Microsoft.Xna.Framework.Graphics.SpriteSortMode.Deferred,
                    Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend);
                font.Draw(sb, text, new Microsoft.Xna.Framework.Vector2(4, 8), tint);
                sb.End();
            }
            gd.SetRenderTarget(null);
            PrintReadback(rt, "DIRECT tint=" + Describe(tint) + " bg=" + Describe(bg) + " text='" + Escape(text) + "'");
            rt.Dispose();
        }

        private static void RenderUILabel(Microsoft.Xna.Framework.Graphics.GraphicsDevice gd, string text, int size)
        {
            var W = 480; var H = 48;
            var rt = new Microsoft.Xna.Framework.Graphics.RenderTarget2D(gd, W, H);
            gd.SetRenderTarget(rt);
            gd.Clear(Microsoft.Xna.Framework.Color.Transparent);
            var label = new UILabel();
            label.Caption = text;
            label.CaptionStyle = TextStyle.DefaultLabel.Clone();
            label.CaptionStyle.Size = size;
            label.CaptionStyle.Color = Microsoft.Xna.Framework.Color.White;
            var batch = new UISpriteBatch(gd, 0);
            batch.UIBegin(Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend,
                Microsoft.Xna.Framework.Graphics.SpriteSortMode.Immediate);
            label.Draw(batch);
            batch.End();
            gd.SetRenderTarget(null);
            PrintReadback(rt, "UILABEL size=" + size + " text='" + Escape(text) + "'");
            rt.Dispose();
        }

        private static string Escape(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in s)
            {
                if (ch >= 32 && ch < 127) sb.Append(ch);
                else sb.Append(string.Format("<{0:X4}>", (ushort)ch));
            }
            return sb.ToString();
        }

        private static string Describe(Microsoft.Xna.Framework.Color c)
        {
            return string.Format("({0},{1},{2},{3})", c.R, c.G, c.B, c.A);
        }

        private static readonly string Ramp = " .:-=+*#%@";

        private static void PrintReadback(Microsoft.Xna.Framework.Graphics.RenderTarget2D rt, string header)
        {
            var W = rt.Width; var H = rt.Height;
            var px = new Microsoft.Xna.Framework.Color[W * H];
            rt.GetData(px);
            int inkX0 = W, inkX1 = -1, inkY0 = H, inkY1 = -1;
            int solidA = 0, midA = 0, satRgb = 0, premultEq = 0, inkPix = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var c = px[y * W + x];
                    if (c.A == 0) continue;
                    inkPix++;
                    if (x < inkX0) inkX0 = x; if (x > inkX1) inkX1 = x;
                    if (y < inkY0) inkY0 = y; if (y > inkY1) inkY1 = y;
                    if (c.A >= 250) solidA++;
                    else if (c.A >= 30) midA++;
                    var mx = Math.Max(c.R, Math.Max(c.G, c.B));
                    if (c.A >= 30 && mx >= 250) satRgb++;
                    if (Math.Abs(mx - c.A) <= 2) premultEq++;
                }
            Console.WriteLine("TEXTDIAG ==== " + header);
            Console.WriteLine("TEXTDIAG ink=" + inkPix + " bbox=" + (inkX1 < 0 ? "none" :
                (inkX0 + "," + inkY0 + ")-(" + inkX1 + "," + inkY1)) +
                " alphaSolid(>=250)=" + solidA + " alphaMid(30..249)=" + midA +
                " rgbSaturated(>=250)=" + satRgb + " rgbEqAlpha=" + premultEq);
            if (inkX1 < 0) { Console.WriteLine("TEXTDIAG (no ink)"); return; }
            // clip the art to the ink bbox (max 100 cols x 30 rows)
            int cx0 = Math.Max(0, inkX0), cy0 = Math.Max(0, inkY0);
            int cx1 = Math.Min(W - 1, Math.Min(inkX1, cx0 + 99));
            int cy1 = Math.Min(H - 1, Math.Min(inkY1, cy0 + 29));
            Console.WriteLine("TEXTDIAG -- alpha art --");
            for (int y = cy0; y <= cy1; y++)
                Console.WriteLine("TEXTDIAG A|" + ArtRow(px, W, y, cx0, cx1, c => c.A));
            Console.WriteLine("TEXTDIAG -- rgb ink art (max channel) --");
            for (int y = cy0; y <= cy1; y++)
                Console.WriteLine("TEXTDIAG R|" + ArtRow(px, W, y, cx0, cx1,
                    c => Math.Max(c.R, Math.Max(c.G, c.B))));
        }

        private static string ArtRow(Microsoft.Xna.Framework.Color[] px, int W, int y, int x0, int x1,
            Func<Microsoft.Xna.Framework.Color, int> key)
        {
            var sb = new StringBuilder();
            for (int x = x0; x <= x1; x++)
            {
                var v = key(px[y * W + x]);
                sb.Append(Ramp[Math.Min(9, v / 26)]);
            }
            return sb.ToString();
        }
    }
}
