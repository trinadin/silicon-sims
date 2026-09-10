using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Controls;
using System.Threading;
using FSO.Client;
using FSO.Common.Rendering.Framework.Model;
using FSO.Content;
using FSO.Content.Model;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FSO.Common.Utils;
using FSO.SimAntics;

namespace Simitone.Client.UI.Screens
{
    /// <summary>
    /// ROUND-118: the REAL loading screen only, from the FIRST FRAME. The R88/R111 mounts
    /// happened in Update() AFTER the content thread resolved TS1Global (~1.5s window,
    /// longer on cold starts) - during that window the port drew its own chrome: the
    /// load_static_bg.png gradient, the Simitone wordmark, two diagonal stripes, the
    /// elastic musical-notes progress bar and MSDF status labels (which render as
    /// unreadable blocks on this GL path). ALL of that plumbing is DELETED; the ctor now
    /// reads the ORIGINAL kSimsLogo member (Other\setup.bmp) DIRECTLY from UIGraphics.far
    /// (FAR1 stores BMPs uncompressed, so a synchronous read of the archive manifest +
    /// member is byte-verbatim and fast) and shows it from frame one. The engine-drawn
    /// bar (R88: the original declares no bar asset - proven by template absence) and the
    /// R111 splash strings mount on top as before. The only fallback, if the direct read
    /// ever fails, is a plain navy (#000029, uipal anchor) fill - never Simitone art.
    /// </summary>
    public class LoadingScreen : FSO.Client.UI.Framework.GameScreen
    {
        public UISimitoneBg Bg;
        public bool LoadingComplete;

        // ROUND-88 original boot/logo screen (kSimsLogo, Res_Other.RT id 9003 =
        // Other\setup.bmp) live-mount state, readable by the uiboot gate. R118: mounted
        // DIRECTLY from the FAR in the ctor (BootDirect* state below); the flags keep
        // their names so the gate contract is unchanged.
        public static bool BootLogoMounted = false;
        public static string BootLogoDims = "";
        public static bool SimitoneChromeHidden = false;
        public static bool OriginalBarActive = false;
        public static float OriginalBarLastFill = -1f;

        // ROUND-118: the ctor-time DIRECT FAR read state. BootDirectSha is the sha256 of
        // the extracted Other\setup.bmp member bytes - the gate pins it against the R88
        // canon (8ecf9500...), so the first-frame art is proven byte-verbatim, not just
        // dimension-matched.
        public static bool BootDirectMounted = false;
        public static string BootDirectSha = "";
        public static int BootDirectLen = 0;

        // ROUND-111 original splash-progress strings (readable by the uisplash gate).
        // Corpus: GameData/UIText.iff STR# 'splashprogress' (chunkID 155, format -3
        // lang-pairs, 340 entries = 20 languages x 17). The English set is the canon
        // title-screen scroll - translator comments in the chunk mark entries
        // 0,1,2,3,12,13,15 '!Title Screen: Scrolling Text' (boot anchors) and the rest
        // '##SPELLBOUND' (Makin' Magic-era humor). Mounted via GameFacade.Strings table
        // "155" (built from the same UIText.iff in SimitoneGame.Initialize); the gate
        // re-reads the chunk from disk and pins it sha256-verbatim against this array.
        public static string[] OriginalSplashTips = null;
        public static bool OriginalSplashMounted = false;
        public static string OriginalSplashFontName = "";

        public UIOriginalText SplashTip;
        private int SplashTipIndex;
        private int SplashTipFrame;
        private bool SplashProgressPulse = false;
        private bool SplashMountExcLogged = false;
        private bool BootDirectExcLogged = false;

        public UIOriginalLoadBar OriginalBar;

        public LoadingScreen()
        {
            Content.InitBasic(GlobalSettings.Default.StartupPath, GameFacade.GraphicsDevice);

            Bg = new UISimitoneBg(false); // R118: no modern gradient fallback, ever
            Bg.Position = (new Vector2(ScreenWidth, ScreenHeight)) / 2;
            Add(Bg);

            // The REAL boot screen from frame one (see class comment). Sets the R88 gate
            // flags on success; on failure Bg stays navy until the GO screen mounts.
            SimitoneChromeHidden = true; // the chrome no longer exists at all (R118)
            MountBootOriginalDirect();
            if (Bg.Bg == null)
            {
                var px = new Texture2D(GameFacade.GraphicsDevice, 1, 1);
                px.SetData(new Color[] { new Color(0, 0, 41) }); // uipal navy anchor
                Bg.Bg = px;
            }

            OriginalBar = new UIOriginalLoadBar();
            // ctor-time positioning must NOT touch GameFacade.Screens.CurrentUIScreen -
            // it is null mid-AddScreen (RemoveCurrent runs first; r118p1 crash). Use this
            // screen's own metrics; GetBarWidth stays null-safe for the same window.
            var barW = (int)(ScreenWidth * 0.62f);
            OriginalBar.Position = new Vector2((ScreenWidth - barW) / 2, ScreenHeight - 60f);
            Add(OriginalBar);
            OriginalBarActive = true;
            OriginalBarLastFill = OriginalBar.Fill;

            (new Thread(() => {
                try
                {
                    Simitone.Client.GameLog.Write("content-init: begin");
                    VMContext.InitVMConfig(true);
                    FSO.Content.Content.Init(GlobalSettings.Default.StartupPath, GameFacade.GraphicsDevice);
                    Simitone.Client.GameLog.Write("content-init: done");
                    // R87: IFF-first original-cursor mount - runs here, right after the IFF content
                    // mount, so GameFacade.Cursor serves the ORIGINAL UIGraphics.far .cur member bytes.
                    Simitone.Client.SimitoneGame.R87CursorMount();
                }
                catch (Exception cie)
                {
                    Simitone.Client.GameLog.Write("content-init: EXCEPTION " + cie.GetType().Name + " " + cie.Message);
                    Simitone.Client.GameLog.Write(cie.ToString().Replace(Environment.NewLine, " | "));
                }
                lock (this)
                {
                    LoadingComplete = true;
                }
            })).Start();
        }

        /// <summary>
        /// R118: synchronous ctor-time mount of the ORIGINAL kSimsLogo boot art straight
        /// from UIGraphics.far - FAR1 members are stored uncompressed, so this reads the
        /// archive manifest, extracts the Other\setup.bmp bytes (1,440,056 - R88 canon)
        /// and decodes the 24-bit BMP directly. No content-thread dependency, no
        /// pre-mount window: the first frame the user sees is the original screen. The
        /// extracted-bytes sha256 is published for the uiboot gate to pin against the R88
        /// canon. Returns false (leaving Bg null -> plain navy) on any failure - the
        /// Simitone art is gone, so a failed read can only ever show navy.
        /// </summary>
        private bool MountBootOriginalDirect()
        {
            try
            {
                var farPath = System.IO.Path.Combine(
                    FSO.Content.Content.TS1HybridBasePath, "UIGraphics/UIGraphics.far");
                if (!System.IO.File.Exists(farPath))
                {
                    Simitone.Client.GameLog.Write("loadscreen: direct boot mount - no UIGraphics.far at " + farPath);
                    return false;
                }
                // v1a manifest (32-bit filename lengths) - proven by the r83/r88 canon
                // tools' own python walk of this exact archive (tools/iff-dump/_farnames_scan.py);
                // the v1b flag desyncs the manifest (negative DataLength, r118p1).
                var far = new FSO.Files.FAR1.FAR1Archive(farPath, false);
                try
                {
                    FSO.Files.FAR1.FarEntry hit = null;
                    foreach (var e in far.GetAllFarEntries())
                    {
                        if (e.Filename == "Other\\setup.bmp") { hit = e; break; }
                    }
                    if (hit == null)
                    {
                        Simitone.Client.GameLog.Write("loadscreen: direct boot mount - Other\\setup.bmp not in manifest");
                        return false;
                    }
                    var bytes = far.GetEntry(hit);
                    if (bytes == null || bytes.Length != hit.DataLength) return false;
                    var hash = System.Security.Cryptography.SHA256.Create().ComputeHash(bytes);
                    var sb = new System.Text.StringBuilder();
                    foreach (var b in hash) sb.Append(b.ToString("x2"));
                    using (var ms = new System.IO.MemoryStream(bytes))
                    {
                        var tex = Texture2D.FromStream(GameFacade.GraphicsDevice, ms);
                        if (tex == null) return false;
                        Bg.Bg = tex;
                        BootLogoMounted = true;
                        BootLogoDims = tex.Width + "x" + tex.Height;
                        BootDirectMounted = true;
                        BootDirectSha = sb.ToString();
                        BootDirectLen = bytes.Length;
                        Simitone.Client.GameLog.Write("loadscreen: DIRECT FAR mount original kSimsLogo boot screen "
                            + BootLogoDims + " len=" + bytes.Length + " sha=" + BootDirectSha.Substring(0, 8)
                            + " (from frame one; Simitone loader chrome deleted R118)");
                        return true;
                    }
                }
                finally
                {
                    far.Close();
                }
            }
            catch (Exception e)
            {
                if (!BootDirectExcLogged)
                {
                    BootDirectExcLogged = true;
                    Simitone.Client.GameLog.Write("loadscreen: direct boot mount EXC " + e.GetType().Name + " " + e.Message);
                }
                return false;
            }
        }

        public void Close()
        {
            // R118: no Simitone chrome remains to tween away - fade the original art out
            // as the neighborhood screen appears underneath (the children were already
            // moved into the game screen by GameController.EnterGameMode).
            GameFacade.Screens.Tween.To(Bg, 0.5f, new Dictionary<string, float>() { { "Opacity", 0f } }, TweenQuad.EaseOut);
            GameThread.SetTimeout(() =>
            {
                if (Bg.Parent != null) Bg.Parent.Remove(Bg);
                if (OriginalBar != null && OriginalBar.Parent != null) OriginalBar.Parent.Remove(OriginalBar);
                if (SplashTip != null && SplashTip.Parent != null) SplashTip.Parent.Remove(SplashTip);
            }, 750);
        }

        public override void GameResized()
        {
            base.GameResized();
            Bg.Position = (new Vector2(ScreenWidth, ScreenHeight)) / 2;
            if (OriginalBar != null) OriginalBar.Position = new Vector2((ScreenWidth - UIOriginalLoadBar.GetBarWidth()) / 2, ScreenHeight - 60f);
            if (SplashTip != null) SetSplashTip(SplashTip.Text);
        }

        public ContentLoadingProgress LastProgress = ContentLoadingProgress.Invalid;

        private int _diagFrame;
        public override void Update(UpdateState state)
        {
            _diagFrame++;
            if ((_diagFrame % 120) == 0)
                Simitone.Client.GameLog.Write("loading-screen: update frame=" + _diagFrame + " complete=" + LoadingComplete + " progress=" + Content.LoadProgress);
            if (LastProgress != Content.LoadProgress)
            {
                LastProgress = Content.LoadProgress;
                var pct = (float)LastProgress / (float)ContentLoadingProgress.Done;
                // R118: the R88/111 rule is now unconditional - the original boot screen
                // is clean logo art + the engine-drawn bar; the R111 string layer
                // advances on progress. (The Simitone status-label layer is deleted.)
                SplashProgressPulse = true;
                if (OriginalBar != null) { OriginalBar.Fill = pct; OriginalBarLastFill = pct; }
            }

            // ROUND-111: mount the ORIGINAL splash-progress strings (UIText.iff STR#
            // 'splashprogress', chunkID 155) rendered with an ORIGINAL .ffn glyph table
            // (variablesans_10, the caption table - the engine's exact title-screen font
            // pick is not recovered, disclosed). Needs the font's FAR mount, so this
            // retries each Update until it resolves; until then the screen shows the
            // original art + bar with no text (never modern text).
            MountSplashOriginal();
            if (OriginalSplashMounted && SplashTip != null)
            {
                // sequential scroll through the whole corpus: advance every ~1.5s of
                // frames, and on every loading-progress change (stage anchors + scroll,
                // mirroring the original's mix per the chunk's comment taxonomy).
                SplashTipFrame++;
                var progressAdvanced = SplashProgressPulse;
                if (SplashTipFrame >= 90 || progressAdvanced)
                {
                    SplashTipFrame = 0;
                    SplashProgressPulse = false;
                    SplashTipIndex = (SplashTipIndex + 1) % OriginalSplashTips.Length;
                    SetSplashTip(OriginalSplashTips[SplashTipIndex]);
                }
            }

            // ROUND-83: IFF-mount the original neighborhood GO screen (kNeighborhoodGo) as the
            // load-complete stage, right before EnterGameMode - the original sequence shows the
            // boot logo screen first, then the GO screen.
            if (LoadingComplete)
            {
                Bg.ResolveOriginal();
                Bg.MountOriginal();
            }

            lock (this)
            {
                if (LoadingComplete)
                {
                    GameController.EnterGameMode("", false);
                }
            }
            base.Update(state);
        }

        /// <summary>
        /// ROUND-111: mounts the ORIGINAL splash-progress strings (UIText.iff STR#
        /// 'splashprogress', chunkID 155 - the title-screen scrolling corpus, 17 English
        /// entries) rendered with the ORIGINAL variablesans_10 caption glyph table.
        /// Strings come from GameFacade.Strings table "155" (parsed from the same
        /// UIText.iff by ContentStrings.LoadTS1 at SimitoneGame.Initialize); the font
        /// needs TS1Global (FAR .ffn mount), so this retries each Update until it
        /// resolves. A corpus guard requires exactly 17 strings (the English set) so a
        /// locale-shifted read FAILS closed rather than rendering silently-wrong text.
        /// </summary>
        public void MountSplashOriginal()
        {
            if (OriginalSplashMounted) return;
            var strings = FSO.Client.GameFacade.Strings;
            if (strings == null) return;
            try
            {
                var tips = new System.Collections.Generic.List<string>();
                for (int i = 0; i < 32; i++)
                {
                    var s = strings.GetString("155", i.ToString());
                    if (string.IsNullOrEmpty(s) || s == "***MISSING***") break;
                    tips.Add(s);
                }
                if (tips.Count != 17) return; // corpus guard: English set is 17 (r111 canon)
                var font = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadCaption(GameFacade.GraphicsDevice);
                if (font == null || font.Atlas == null) return;
                OriginalSplashTips = tips.ToArray();
                SplashTip = new UIOriginalText(OriginalSplashTips[0], font) { Color = Color.White };
                SplashTipIndex = 0;
                SplashTipFrame = 0;
                var w = font.Measure(OriginalSplashTips[0]);
                SplashTip.Position = new Vector2((ScreenWidth - w) / 2f, ScreenHeight - 96f);
                Add(SplashTip);
                OriginalSplashMounted = true;
                OriginalSplashFontName = "Fonts\\variablesans_10.ffn";
                Simitone.Client.GameLog.Write("loadscreen: IFF-mount original splashprogress strings (17) "
                    + "in variablesans_10 over the boot screen");
            }
            catch (Exception e)
            {
                if (!SplashMountExcLogged)
                {
                    SplashMountExcLogged = true;
                    Simitone.Client.GameLog.Write("loadscreen: splashprogress mount EXC (logs once) "
                        + e.GetType().Name + " " + e.Message);
                }
            }
        }

        private void SetSplashTip(string text)
        {
            if (SplashTip == null) return;
            SplashTip.Text = text;
            var w = SplashTip.Font != null ? SplashTip.Font.Measure(text) : 0;
            SplashTip.Position = new Vector2((ScreenWidth - w) / 2f, ScreenHeight - 96f);
        }
    }

    public class UISimitoneBg : UIElement
    {
        public Texture2D Bg;
        // ROUND-83 loading-screen mount (original asset, IFF-factual): the ORIGINAL Sims 1 loading
        // screen for the Neighborhood is IFF-declared in UIGraphics.far (original resource template
        // Res_Nbhd.h / Res_Nbhd.RT): kNeighborhoodGo (5600) = Community\\Bus_loadscreen_800x600.bmp,
        // kNeighborhoodGoBig (5601) = Community\\Bus_loadscreen_1024x768.bmp. TS1Global resolves them
        // only after the content thread initializes (the loading screen constructs before that), so
        // ResolveOriginal() runs on the UI thread and mounts the IFF texture the moment it is
        // IFF-resolvable.
        // R118: loadModernFallback=false skips the Simitone gradient entirely - the loading
        // screen sets its own art (original setup.bmp from frame one, or plain navy).
        public ITextureRef OriginalRef;
        public bool MountedOriginal;

        public UISimitoneBg(bool loadModernFallback = true) : base()
        {
            Bg = null;
            if (loadModernFallback)
            {
                var ui = Content.Get().CustomUI;
                Bg = ui.Get("load_static_bg.png").Get(GameFacade.GraphicsDevice);
            }
            OriginalRef = null;
            MountedOriginal = false;
        }

        public void ResolveOriginal()
        {
            if (MountedOriginal || OriginalRef != null) return;
            var ts1 = Content.Get()?.TS1Global;
            if (ts1 == null) return;
            var name = (GameFacade.Screens.CurrentUIScreen.ScreenWidth >= 1024)
                ? "Community\\Bus_loadscreen_1024x768.bmp"
                : "Community\\Bus_loadscreen_800x600.bmp";
            try
            {
                var tx = ts1.Get(name) as ITextureRef;
                if (tx == null) return;
                OriginalRef = tx;
                Simitone.Client.GameLog.Write("loadscreen: IFF-mount original " + name);
            }
            catch (Exception e)
            {
                Simitone.Client.GameLog.Write("loadscreen: IFF-mount EXC " + e.GetType().Name + " " + e.Message);
            }
        }

        public void MountOriginal()
        {
            if (MountedOriginal || OriginalRef == null) return;
            var tex = OriginalRef.Get(GameFacade.GraphicsDevice);
            Bg = tex;
            OriginalRef = null;
            MountedOriginal = true;
            Simitone.Client.GameLog.Write("loadscreen: IFF-mount texture " + (tex != null ? (tex.Width + "x" + tex.Height) : "null"));
        }

        // R160: the neighborhood surround — engine law r143 §1: windows wider than
        // the 800x600 artboard load kLargeMask 5001 (Downtown/LargeBack.bmp 1024x768)
        // as the surround around the centered artboard. Desktop keeps the decoded
        // unscaled 800x600 aperture; the background cover-fit is only relevant beyond
        // the original engine's maximum 1024x768 window. Replaces the retired Simitone
        // gradient on TS1GameScreen.
        public bool SurroundMounted;

        public void ResolveNeighborhoodSurround()
        {
            if (Bg != null || OriginalRef != null || MountedOriginal || SurroundMounted) return;
            try
            {
                // UIGraphics.far stores this member as Downtown\largeback.bmp.
                // TS1Provider.Get is case-sensitive, so the template spelling above
                // silently missed and left the screen's gray clear color visible around
                // the correctly centered 800x600 neighborhood. UIOriginal performs the
                // same exact-name lookup followed by a manifest-casing fallback used by
                // every other original UI mount.
                var tx = Simitone.Client.UI.Model.UIOriginal.EnsureResolved("Downtown\\LargeBack.bmp");
                if (tx == null) return;
                var tex = tx.Get(GameFacade.GraphicsDevice);
                if (tex == null) return;
                Bg = tex;
                SurroundMounted = true;
                Simitone.Client.GameLog.Write("uinav: IFF-mount neighborhood surround " + tex.Width + "x" + tex.Height);
            }
            catch (Exception e)
            {
                Simitone.Client.GameLog.Write("uinav: surround EXC " + e.GetType().Name + " " + e.Message);
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || Bg == null) return;
            // R160: scale from the ACTUAL art dims (was hardcoded 1136x640 for the
            // Simitone gradient) — cover-fit, centered.
            var scale = Math.Max(GameFacade.Screens.CurrentUIScreen.ScreenWidth / (float)Bg.Width,
                GameFacade.Screens.CurrentUIScreen.ScreenHeight / (float)Bg.Height);
            DrawLocalTexture(batch, Bg, null, new Vector2(Bg.Width, Bg.Height) / -2 * scale, new Vector2(scale));
        }
    }

    // ROUND-88: engine-drawn original-style loading bar. The ORIGINAL boot screen (kSimsLogo =
    // Other\setup.bmp) has NO bar asset - Res_Other.RT / Res_Nbhd.RT declare no progress-bar or
    // logo-control resource - so the original engine draws its bar; this control does the same
    // over the original screen art, colored from the pinned original UI palette (uipal:
    // navy #000029 track, cyan #00FFFF frame).
    public class UIOriginalLoadBar : UIElement
    {
        public float Fill;

        public static int GetBarWidth()
        {
            // R118: null-safe - the bar is constructed in the LoadingScreen ctor, where
            // CurrentUIScreen is still null (mid-AddScreen). Fallback width only matters
            // for that one window; Draw re-reads the live screen size every frame.
            var cur = GameFacade.Screens != null ? GameFacade.Screens.CurrentUIScreen : null;
            var w = cur != null ? cur.ScreenWidth : 800;
            return (int)(w * 0.62f);
        }

        public override Rectangle GetBounds()
        {
            return new Rectangle(0, 0, GetBarWidth(), 10);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            var w = GetBarWidth();
            const int h = 10;
            var whitepx = TextureGenerator.GetPxWhite(batch.GraphicsDevice);
            // track: translucent navy over the original art (uipal anchor #000029)
            DrawLocalTexture(batch, whitepx, null, Vector2.Zero, new Vector2(w, h), new Color(0, 0, 41) * 0.55f);
            // hairline frame (uipal cyan anchor #00FFFF)
            DrawLocalTexture(batch, whitepx, null, Vector2.Zero, new Vector2(w, 1), Color.Cyan);
            DrawLocalTexture(batch, whitepx, null, new Vector2(0, h - 1), new Vector2(w, 1), Color.Cyan);
            DrawLocalTexture(batch, whitepx, null, Vector2.Zero, new Vector2(1, h), Color.Cyan);
            DrawLocalTexture(batch, whitepx, null, new Vector2(w - 1, 0), new Vector2(1, h), Color.Cyan);
            var fw = (int)(w * Fill);
            if (fw > 0) DrawLocalTexture(batch, whitepx, null, Vector2.Zero, new Vector2(fw, h), Color.White);
        }
    }
}
