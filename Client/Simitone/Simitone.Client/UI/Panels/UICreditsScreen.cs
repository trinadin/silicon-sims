using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common;
using FSO.Common.Rendering.Framework.Model;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Controls;
using System;
using System.Collections.Generic;
using System.IO;

namespace Simitone.Client.UI.Panels
{
    // UI-30: the neighborhood CREDITS screen, ported from the ORIGINAL PPC
    // engine's cWinCredits law (coordination/evidence/UI-29/credits-law.md;
    // ctor 0x2729c0-family, window 0x158 bytes):
    //
    //  - ENTRY: constructed by every screen's RegularModeBtnHandler when the
    //    Credits button/banner fires (VC site 0x471600); a static latch allows
    //    only one credits window; SetArea covers the whole neighborhood window
    //    and swallows input (port: a full-screen overlay mounted last on
    //    TS1GameScreen, empty mouse listener, TS1GameScreen.ShowCreditsScreen
    //    owns the latch).
    //  - NO BACKGROUND ART (Init @0x272660 has no LoadBuffer): a plain filled
    //    cTSWin painted over the neighborhood (the native fill primitive
    //    0x4d82a0's color is not extracted — port paints opaque black,
    //    disclosed).
    //  - CONTENT = ONE SHARED TIMELINE (Init + AddCredits @0x271c70):
    //    InsertMacCredits (resource-fork 'STR#' 10000 — ported as the
    //    extracted UICreditsMacSTR10000 text block) + expansion STR sets
    //    171/170/169/167/166/165 + (168 OR 164+163). The native gates the last
    //    group on the installed-expansion bitfield: bit 27 (Deluxe) loads 168
    //    INSTEAD of 164+163; the port replicates the BRANCH by set
    //    availability (Credits.iff = the rsmp expansion map: 164=Livin'
    //    Large ... 171=Makin' Magic, 163="1.0") — with the shipped Complete
    //    data all sets exist, so 168 loads and 164/163 stay off the roll,
    //    exactly like the native Complete install. AddCredits splits each
    //    STR# string on CR/LF into lines and ADVANCES THE SHARED START TICK
    //    BY 0x2EE = 770 MS PER LINE across all sets, the first line at
    //    +1000 ms (Init this+0x148 = now+1000, credits-law §3/§4) — one
    //    timeline, Mac credits first, base credits LAST.
    //  - SCROLL: TSPaint computes a constant upward crawl (speed float from a
    //    constant table — value not extracted; port uses the disclosed model
    //    40 px/s, so the 770 ms cadence = ~31 px line pitch); per-line Credit
    //    machine (Credit::Tick 0x273370): 0 waiting (until the line's
    //    scheduled tick) -> 1 armed (400 ms pre-roll, 0x190) -> 2 crawling ->
    //    4 done (removed from the roll). The native's state-3 "hold until
    //    deadline (+0x34)" edge-snap window is not decodable to a constant —
    //    the port removes a line once it fully passes the clip top
    //    (disclosed).
    //  - EXIT: ESC (0x1b) or ENTER (0xd) ONLY (TSOnKeyDown 0x270f30 posts the
    //    close message; no mouse exit, no close button). Shutdown restores
    //    the owned scroll-speed global (the port's crawl constant is
    //    read-only, nothing to restore).
    public class UICreditsScreen : UIContainer
    {
        // Timing law constants (credits-law §4: +1000 ms first line, 0x2ee = 770 ms
        // per line; Credit::Tick state 1 = 400 ms armed pre-roll, 0x190).
        public const int FirstLineDelayMs = 1000;
        public const int LineDelayMs = 770;
        public const int ArmedPreRollMs = 400;
        // Disclosed crawl model: the native reads a saved float and a constant-table
        // value (credits-law §5) that are not in the decoded window.
        public const float CrawlPixelsPerSecond = 40f;
        // Roll-area margins (Init geometry fields +0x110/+0x118: 16 px inset).
        public const int ClipMargin = 16;

        // Per-line states (Credit::Tick 0x273370 five-state machine; the port
        // skips the undecodable state-3 hold window, see the class comment).
        public const int StateWaiting = 0;
        public const int StateArmed = 1;
        public const int StateCrawling = 2;
        public const int StateHold = 3;
        public const int StateDone = 4;

        /// <summary>One timeline line: text plus its SHARED-timeline start tick.
        /// Separator lines ('-' in the Mac block) render blank but still occupy
        /// their 770 ms slot — AddCredits advanced the tick for every line.</summary>
        public sealed class CreditLine
        {
            public string Text;
            public int StartMs;
            public int State;
        }

        private readonly List<CreditLine> Lines = new List<CreditLine>();
        /// <summary>STR-set provenance of the timeline, in load order
        /// (10000 = the Mac resource-fork block; the law's expansion order).</summary>
        public readonly List<int> SetsLoaded = new List<int>();
        private OriginalGlyphFont Font;
        private double ClockMs;
        private bool Closed;
        private int DrawWidth, DrawHeight;
        private readonly Action OnExit;

        public UICreditsScreen(Action onExit = null)
        {
            OnExit = onExit;
            var screen = UIScreen.Current;
            int width = screen?.ScreenWidth ?? GlobalSettings.Default.GraphicsWidth;
            int height = screen?.ScreenHeight ?? GlobalSettings.Default.GraphicsHeight;
            // Cover the whole neighborhood window (SetArea from the parent rect,
            // credits-law §2) and swallow mouse input like the native topmost child.
            ScaleX = ScaleY = 1;
            X = Y = 0;
            DrawWidth = width;
            DrawHeight = height;
            ListenForMouse(new Rectangle(0, 0, width, height), (e, s) => { });

            Font = OriginalGlyphFont.LoadByIndex(12, GameFacade.GraphicsDevice);
            BuildTimeline();
        }

        /// <summary>The law's composition + shared-timeline cadence. Split out so
        /// the 'nghbtns' gate can pin set order and per-line start ticks against
        /// the real Credits.iff without driving frames.</summary>
        private void BuildTimeline()
        {
            var sets = new List<string[]>();
            // 1. InsertMacCredits: the Mac resource-fork 'STR#' 10000 block, FIRST
            // (credits-law §4.1). '-' lines keep their timeline slot (rendered blank).
            SetsLoaded.Add(UICreditsMacSTR10000.ResourceId);
            sets.Add(UICreditsMacSTR10000.Lines);

            // 2. Expansion credit sets from GameData/Credits.iff (the rsmp
            // expansion-credits container; its own STR# 163..171 chunks carry the
            // English credit text). Load order per the decoded Init: 171, 170,
            // 169, 167, 166, 165, then the Deluxe branch — 168 if the set exists
            // (bit 27), else 164 + 163 (base "1.0" credits, unconditionally last
            // on the non-Deluxe path). Absent sets are skipped: the native gates
            // on the installed-expansion bitfield, the port on set availability
            // (disclosed).
            string[] deluxe, livinLarge, baseSet;
            foreach (var id in new[] { 171, 170, 169, 167, 166, 165 })
            {
                var set = ReadCreditsSet(id);
                if (set == null) continue;
                SetsLoaded.Add(id);
                sets.Add(set);
            }
            deluxe = ReadCreditsSet(168);
            if (deluxe != null)
            {
                SetsLoaded.Add(168);
                sets.Add(deluxe);
            }
            else
            {
                livinLarge = ReadCreditsSet(164);
                if (livinLarge != null)
                {
                    SetsLoaded.Add(164);
                    sets.Add(livinLarge);
                }
                baseSet = ReadCreditsSet(163);
                if (baseSet != null)
                {
                    SetsLoaded.Add(163);
                    sets.Add(baseSet);
                }
            }

            // AddCredits law: split each string on CR/LF, one timeline start tick
            // advanced 770 ms per line across ALL sets, first line at +1000 ms.
            int start = FirstLineDelayMs;
            foreach (var set in sets)
            {
                foreach (var str in set)
                {
                    if (str == null) continue;
                    foreach (var line in str.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
                    {
                        Lines.Add(new CreditLine
                        {
                            Text = line,
                            StartMs = start,
                            State = StateWaiting,
                        });
                        start += LineDelayMs;
                    }
                }
            }
        }

        /// <summary>Reads one STR# set from GameData/Credits.iff (the English-US
        /// language set). Null when the file or the chunk is absent — the port's
        /// stand-in for the native's installed-expansion bitfield.</summary>
        internal static string[] ReadCreditsSet(int id)
        {
            try
            {
                var path = Path.Combine(GlobalSettings.Default.TS1HybridPath, "GameData", "Credits.iff");
                if (!File.Exists(path)) return null;
                var iff = new IffFile(path);
                var str = iff.Get<STR>((ushort)id);
                if (str == null || str.Length == 0) return null;
                var lines = new string[str.Length];
                for (int i = 0; i < str.Length; i++) lines[i] = str.GetString(i);
                return lines;
            }
            catch
            {
                return null;
            }
        }

        public int LineCount { get { return Lines.Count; } }
        public bool IsClosed { get { return Closed; } }

        public CreditLine Line(int i) { return Lines[i]; }

        // --- timeline machine (Credit::Tick shape; deterministic entry point) ---

        /// <summary>Advances the credits clock by ms and re-evaluates every line's
        /// state. Called from Update with the frame delta; the gate drives it
        /// directly for deterministic cadence pins.</summary>
        public void Advance(double ms)
        {
            ClockMs += Math.Max(0, ms);
            int top = ClipMargin;
            int bottom = DrawHeight - ClipMargin;
            float crawl = (float)(CrawlPixelsPerSecond * ClockMs / 1000.0);
            foreach (var line in Lines)
            {
                switch (line.State)
                {
                    case StateWaiting:
                        if (ClockMs >= line.StartMs) line.State = StateArmed;
                        break;
                    case StateArmed:
                        if (ClockMs >= line.StartMs + ArmedPreRollMs) line.State = StateCrawling;
                        break;
                    case StateCrawling:
                        // The line crawls up from below the clip; once it has fully
                        // passed the clip top it is done (native state-3 hold window
                        // modeled away, disclosed).
                        if (LineTop(line, crawl, bottom) + LineHeight() < top) line.State = StateDone;
                        break;
                }
            }
        }

        private int LineTop(CreditLine line, float crawl, int bottom)
        {
            // The line's crawl starts from its own scheduled tick (the 770 ms
            // stagger IS the inter-line spacing law).
            float own = (float)(CrawlPixelsPerSecond
                * Math.Max(0, ClockMs - line.StartMs - ArmedPreRollMs) / 1000.0);
            return bottom - (int)own;
        }

        private int LineHeight() { return Font?.LineHeight ?? 14; }

        /// <summary>The TSOnKeyDown law: ESC (0x1b) or ENTER (0xd) close the
        /// credits; everything else is ignored. Split out so the gate can pin
        /// both keys plus a non-exit key deterministically.</summary>
        public bool HandleKey(Keys key)
        {
            if (key == Keys.Escape || key == Keys.Enter)
            {
                if (Closed) return true;
                Closed = true;
                OnExit?.Invoke();
                return true;
            }
            return false;
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            foreach (var key in state.NewKeys)
            {
                if (HandleKey(key)) break;
            }
            Advance(state.Time.ElapsedGameTime.TotalMilliseconds);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            // No background art (credits-law §3): plain fill over the neighborhood.
            DrawLocalTexture(batch, FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice),
                new Rectangle(0, 0, DrawWidth, DrawHeight), Vector2.Zero, Vector2.One, new Color(0, 0, 0));

            if (Font == null || Font.Atlas == null) return;
            int top = ClipMargin;
            int bottom = DrawHeight - ClipMargin;
            foreach (var line in Lines)
            {
                if (line.State != StateCrawling) continue;
                var text = line.Text;
                if (text.Length > 0 && text[0] == '-')
                {
                    // The native swaps '-' lines for the language object's
                    // alternate (credits-law §4.1); the port renders them as the
                    // blank separator line the English data implies (disclosed).
                    continue;
                }
                int y = LineTop(line, 0f, bottom);
                if (y + LineHeight() < top || y > bottom) continue;
                int w = Font.Measure(text);
                int x = (DrawWidth - w) / 2;
                foreach (var ch0 in text)
                {
                    var ch = OriginalGlyphFont.MapChar(ch0);
                    OriginalGlyphFont.Glyph g;
                    if (Font.ByChar.TryGetValue(ch, out g) && g.W > 1)
                    {
                        DrawLocalTexture(batch, Font.Atlas,
                            new Rectangle(g.U, g.V, g.W, g.H),
                            new Vector2(x, y + Font.GlyphY(g)), Vector2.One, Color.White);
                    }
                    x += Font.Advance(ch);
                }
            }
        }
    }
}
