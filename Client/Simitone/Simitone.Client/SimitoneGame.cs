/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading;
using FSO.Common.Rendering.Framework;
using FSO.LotView;
using FSO.HIT;
using FSO.Client.UI;
using FSO.Client.GameContent;
using FSO.Common.Utils;
using FSO.Common;
using Microsoft.Xna.Framework.Audio;
using FSO.HIT.Model;
using FSO.Client;
using FSO.Files;
using FSO.SimAntics;
using MSDFData;
using FSO.LotView.Model;
using Simitone.Client.UI.Panels;
using System.IO;
using Simitone.Client.Utils;

namespace Simitone.Client
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class SimitoneGame : FSO.Common.Rendering.Framework.Game
    {
        public UILayer uiLayer;
        public _3DLayer SceneMgr;
        private bool HasUpdated;

        // AUD-17 F-8: the 800x600 OS-level window floor (DesktopGL already
        // owns SDL; Window.Handle is the SDL_Window*).
        [System.Runtime.InteropServices.DllImport("libSDL2-2.0.0.dylib", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private static extern void SDL_SetWindowMinimumSize(IntPtr window, int w, int h);

        // R87: IFF-first mount of the ORIGINAL UIGraphics.far .cur cursors (IFF-literalism: the
        // member bytes ARE the original game cursor files). Driven from LoadingGameScreen right after
        // the IFF content mount; guarded so any cursor that fails keeps today's fallback and never
        // breaks startup.
        internal static void R87CursorMount()
        {
            try
            {
                GameFacade.Cursor.Init(GlobalSettings.Default.TS1HybridPath, true, R87IffCursor);
                Simitone.Client.GameLog.Write("cursor-init: IFF-first original cursors mounted livePersonLen=" + FSO.Common.Rendering.Framework.CursorManager.LastMountedLivePersonLen);
            }
            catch (Exception e)
            {
                Simitone.Client.GameLog.Write("cursor-init: EXC " + e.GetType().Name + " " + e.Message);
            }
        }

        // R87 IFF-first resolver: serves the ORIGINAL UIGraphics.far .cur member bytes (matched by
        // the member basename, e.g. "liveperson.cur" -> Shared\cursors\LivePerson.cur), byte-verbatim
        // from the live FAR1 mount table. Returns null when IFF data is unavailable.
        private static byte[] R87IffCursor(string name)
        {
            try
            {
                var ts1 = FSO.Content.Content.Get()?.TS1Global;
                if (ts1 == null) return null;
                var entries = ts1.GetFarEntries(".cur");
                if (entries == null) return null;
                foreach (var en in entries)
                {
                    if (en?.FarEntry?.Filename == null) continue;
                    // IFF-literalism: the raw FAR stored name is the ORIGINAL game path, e.g.
                    // Shared\cursors\LivePerson.cur. Path.GetFileName does not split on backslashes
                    // on macOS, so normalize both separators explicitly.
                    var fn = en.FarEntry.Filename;
                    var idx = fn.LastIndexOfAny(new char[] { '/', '\\' });
                    var baseName = idx >= 0 ? fn.Substring(idx + 1) : fn;
                    if (string.Equals(baseName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        var raw = en.Archive.GetEntry(en.FarEntry);
                        if (string.Equals(name, "liveperson.cur", StringComparison.OrdinalIgnoreCase))
                            Simitone.Client.GameLog.Write("cursor-init: IFF resolve " + name + " -> " + en.FarEntry.Filename + " " + (raw == null ? -1 : raw.Length) + " bytes");
                        return raw;
                    }
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        public SimitoneGame() : base()
        {
            GameFacade.Game = this;
            GameThread.Game = Thread.CurrentThread;
            if (GameFacade.DirectX) TimedReferenceController.SetMode(CacheType.PERMANENT);
            Content.RootDirectory = FSOEnvironment.GFXContentDir;

            TargetElapsedTime = new TimeSpan(10000000 / GlobalSettings.Default.TargetRefreshRate);
            FSOEnvironment.RefreshRate = GlobalSettings.Default.TargetRefreshRate;
            FSOEnvironment.TexCompress = false;
            UILotControl.ShowSimanticsExceptions = !FSOEnvironment.Args.Contains("nosimantics-exc");

            FSOEnvironment.DPIScaleFactor = GlobalSettings.Default.DPIScaleFactor;

            if (!FSOEnvironment.SoftwareKeyboard)
            {
                if (!float.IsFinite(FSOEnvironment.DPIScaleFactor) || FSOEnvironment.DPIScaleFactor <= 0)
                    FSOEnvironment.DPIScaleFactor = 1;
                // Vsync off: SDL_GL_SwapWindow on macOS parks the game loop in a vsync
                // wait when vblank events are scarce (headless/remote sessions), freezing
                // every Update frame (Observed: content-init completes in ~1.1s yet the
                // UI never advances past EnterLoading; main thread 100% in
                // Cocoa_GL_SwapWindow). Without a swap-wait the loop runs freely.
                Graphics.SynchronizeWithVerticalRetrace = false;
                Graphics.PreferredBackBufferWidth = (int)(GlobalSettings.Default.GraphicsWidth * FSOEnvironment.DPIScaleFactor);
                Graphics.PreferredBackBufferHeight = (int)(GlobalSettings.Default.GraphicsHeight * FSOEnvironment.DPIScaleFactor);
                Graphics.HardwareModeSwitch = false;
                Graphics.ApplyChanges();
            }

            this.Window.AllowUserResizing = true;
            this.Window.ClientSizeChanged += new EventHandler<EventArgs>(Window_ClientSizeChanged);

            Thread.CurrentThread.Name = "Game";

            // Set window icon early for Linux/macOS (Windows uses Icon.ico from project settings)
            // This needs to happen before the window is shown for some window managers
            if (GameFacade.Linux)
            {
                SetWindowIcon();
            }
        }

        bool newChange = false;

        // The original desktop composition requires an 800x600 logical canvas.
        // The window manager may allocate less than requested (notably at 2x).
        // Keep the owner's requested density whenever it fits, otherwise reduce
        // only the runtime uniform scale; never rewrite the stored preference.
        internal static float FitDesktopDpiScale(float requested, int viewportWidth, int viewportHeight)
        {
            if (!float.IsFinite(requested) || requested <= 0) requested = 1;
            var limit = Math.Min(Math.Max(1, viewportWidth) / 800.0,
                Math.Max(1, viewportHeight) / 600.0);
            var fitted = (float)Math.Min(requested, limit);
            // A rounded-up float would turn the limiting logical dimension
            // into 599.999... when divided back, then truncate it to599.
            return fitted > limit ? MathF.BitDecrement(fitted) : fitted;
        }

        private void UpdateDesktopViewport(int viewportWidth, int viewportHeight, bool rememberWindowSize = false)
        {
            var previousScale = FSOEnvironment.DPIScaleFactor;
            var settings = GlobalSettings.Default;
            var requestedScale = settings.DPIScaleFactor;
            if (!float.IsFinite(requestedScale) || requestedScale <= 0) requestedScale = 1;
            var scale = FitDesktopDpiScale(requestedScale, viewportWidth, viewportHeight);
            FSOEnvironment.DPIScaleFactor = scale;
            if (rememberWindowSize)
            {
                // Remember an actual resize in units of the requested density,
                // never the fitted density. Startup fitting leaves the owner's
                // configured resolution intact.
                settings.GraphicsWidth = Math.Max(1, (int)Math.Round(viewportWidth / (double)requestedScale));
                settings.GraphicsHeight = Math.Max(1, (int)Math.Round(viewportHeight / (double)requestedScale));
            }
            // Guard the one-unit float truncation at a limiting viewport edge.
            settings.SetRuntimeViewport(Math.Max(800, (int)(viewportWidth / scale)),
                Math.Max(600, (int)(viewportHeight / scale)));
            var screen = uiLayer?.CurrentUIScreen;
            if (screen != null)
            {
                screen.ScaleX = screen.ScaleY = scale;
                screen.InvalidateMatrix();
            }
            if (previousScale != scale)
                GameLog.Write("ui-fit: viewport=" + viewportWidth + "x" + viewportHeight
                    + " requested=" + GlobalSettings.Default.DPIScaleFactor + " effective=" + scale
                    + " logical=" + GlobalSettings.Default.GraphicsWidth + "x" + GlobalSettings.Default.GraphicsHeight);
        }

        void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            // AUD-17 G-4: `Windowed` is never cleared by ToggleFullScreen, so
            // the Alt+Enter fullscreen switch itself used to land here and
            // OVERWRITE the stored windowed resolution with the screen size
            // (the round trip then came up screen-sized). Never remember a
            // size change observed while fullscreen.
            if (newChange || !GlobalSettings.Default.Windowed || Graphics.IsFullScreen) return;
            if (Window.ClientBounds.Width == 0 || Window.ClientBounds.Height == 0) return;
            newChange = true;
            try
            {
                Graphics.PreferredBackBufferWidth = Math.Max(1, Window.ClientBounds.Width);
                Graphics.PreferredBackBufferHeight = Math.Max(1, Window.ClientBounds.Height);
                Graphics.ApplyChanges();

                // R120: the DEVICE VIEWPORT is the single source of truth after the swap.
                // ClientBounds can be in points (macOS retina) while the backbuffer is in
                // pixels; reading the viewport keeps the batch buffer and the logical
                // settings consistent with what actually got allocated. The old code also
                // returned early (before the /DPI recompute) when no screen existed yet,
                // leaving GraphicsWidth/Height in PHYSICAL units — every later layout was
                // laid out at DPIScale x units and drawn at DPIScale x again (the
                // oversized-doubled UI observed after resizing during load).
                var vw = Math.Max(1, GraphicsDevice.Viewport.Width);
                var vh = Math.Max(1, GraphicsDevice.Viewport.Height);
                uiLayer?.SpriteBatch.ResizeBuffer(vw, vh);
                if (!FSOEnvironment.SoftwareKeyboard) UpdateDesktopViewport(vw, vh, true);
                else
                {
                    GlobalSettings.Default.GraphicsWidth = Math.Max(1, (int)(vw / FSOEnvironment.DPIScaleFactor));
                    GlobalSettings.Default.GraphicsHeight = Math.Max(1, (int)(vh / FSOEnvironment.DPIScaleFactor));
                }
                uiLayer?.CurrentUIScreen?.GameResized();
            }
            finally { newChange = false; }
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            GameLog.Write("exit-probe: Initialize enter");

            var settings = GlobalSettings.Default;
            if (!FSOEnvironment.SoftwareKeyboard)
                UpdateDesktopViewport(Math.Max(1, GraphicsDevice.Viewport.Width), Math.Max(1, GraphicsDevice.Viewport.Height));
            else if (FSOEnvironment.DPIScaleFactor != 1 || FSOEnvironment.SoftwareDepth)
            {
                settings.GraphicsWidth = (int)(GraphicsDevice.Viewport.Width / FSOEnvironment.DPIScaleFactor);
                settings.GraphicsHeight = (int)(GraphicsDevice.Viewport.Height / FSOEnvironment.DPIScaleFactor);
            }

            var initialMode = (GlobalGraphicsMode)settings.GlobalGraphicsMode;
            if (FSOEnvironment.Enable3D)
            {
                if (initialMode == GlobalGraphicsMode.Full2D) initialMode = GlobalGraphicsMode.Full3D;
            }
            else
            {
                initialMode = GlobalGraphicsMode.Full2D;
            }
            GraphicsModeControl.ChangeMode(initialMode);
            GraphicsModeControl.ModeChanged += SaveGraphicsModePreference;

            // UI-26 wire (r260-options-readiness WIRE row a, boot apply): the TS1
            // 'Lighting' option derives the runtime lighting mode ONLY when no
            // explicit LightingMode is stored (-1 = auto — the TSOGame derive
            // law, followed in its true direction). Explicit values are
            // preserved — critically the LightingMode=3 ultra pin that
            // Simitone.Windows/.Desktop Program.cs write+save EVERY launch
            // (P1 review fix, indep-review-ui26-20260921: an unconditional
            // derive silently downgraded every install to mode 1/0 with no UI
            // to restore it). TS1 'Shadows' rides the new WorldConfig.ObjShadows
            // gate (LMapBatch.DrawObjShadows).
            FSO.LotView.WorldConfig.Current = new FSO.LotView.WorldConfig()
            {
                LightingMode = DeriveBootLightingMode(settings.LightingMode, settings.Lighting),
                ObjShadows = settings.TS1Shadows,
                SmoothZoom = settings.SmoothZoom,
                SurroundingLots = settings.SurroundingLotMode,
                AA = settings.AntiAlias,
                Directional = settings.DirectionalLight3D,
                Complex = settings.ComplexShaders,
                EnableTransitions = settings.EnableTransitions
            };

            OperatingSystem os = Environment.OSVersion;
            PlatformID pid = os.Platform;
            GameFacade.Linux = (pid == PlatformID.MacOSX || pid == PlatformID.Unix);

            FSO.Content.Content.Target = FSO.Content.FSOEngineMode.TS1;
            FSO.Content.Content.TS1HybridBasePath = GlobalSettings.Default.TS1HybridPath;
            if (FSOEnvironment.Enable3D) FSO.Files.RC.DGRP3DMesh.InitRCWorkers();
            //FSO.Content.Content.Init(GlobalSettings.Default.StartupPath, GraphicsDevice);
            FSO.SimAntics.VMAvatar.MissingIconProvider = Simitone.Client.UI.Model.UIIconCache.GetObject;
            
            // Initialize Free Will setting from config
            VM.FreeWillEnabled = GlobalSettings.Default.TS1FreeWill;

            // UI-26 wire (r260-options-readiness WIRE row b, boot apply): TS1
            // 'Character Detail' selects the Vitaboy skin technique; Avatar.Draw
            // reads DefaultTechnique every frame, so this is live for every sim
            // drawn after boot (FSO.Vitaboy.Avatar.DefaultTechnique).
            FSO.Vitaboy.Avatar.DefaultTechnique =
                Simitone.Client.UI.Panels.LiveSubpanels.UIOriginalOptionsPanel.CharacterDetailTechnique(
                    GlobalSettings.Default.TS1CharacterDetail);

            base.Initialize();

            GameFacade.GameThread = Thread.CurrentThread;

            SceneMgr = new _3DLayer();
            SceneMgr.Initialize(GraphicsDevice);

            GameFacade.Scenes = SceneMgr;
            GameFacade.GraphicsDevice = GraphicsDevice;
            GameFacade.GraphicsDeviceManager = Graphics;
            GameFacade.Cursor = new CursorManager(GraphicsDevice);
            // R87: IFF-first original-cursor mount on EVERY platform (macOS included - the previous
            // GameFacade.Linux skip kept the ORIGINAL UIGraphics.far .cur cursors from ever mounting
            // here). Init now runs from LoadingGameScreen right after the IFF content mount, so the
            // resolver serves the ORIGINAL .cur member bytes (IFF-first). BmpLoaderFunc is required
            // by CurLoader and is harmless to set unconditionally.
            CurLoader.BmpLoaderFunc = ImageLoader.BaseFunction;
            if (!GameFacade.Linux)
            {
                SimitoneCursors.Init(GraphicsDevice);
            }

            /** Init any computed values **/
            GameFacade.Init();

            //init audio now
            HITVM.Init();
            var hit = HITVM.Get();
            hit.SetMasterVolume(HITVolumeGroup.FX, GlobalSettings.Default.FXVolume / 10f);
            hit.SetMasterVolume(HITVolumeGroup.MUSIC, GlobalSettings.Default.MusicVolume / 10f);
            hit.SetMasterVolume(HITVolumeGroup.VOX, GlobalSettings.Default.VoxVolume / 10f);
            hit.SetMasterVolume(HITVolumeGroup.AMBIENCE, GlobalSettings.Default.AmbienceVolume / 10f);

            ContentStrings.TS1 = true;
            GameFacade.Strings = new ContentStrings();

            GraphicsDevice.RasterizerState = new RasterizerState() { CullMode = CullMode.None };

            try
            {
                var audioTest = new SoundEffect(new byte[2], 44100, AudioChannels.Mono); //initialises XAudio.
                audioTest.CreateInstance().Play();
            }
            catch (Exception e)
            {
                //MessageBox.Show("Failed to initialize audio: \r\n\r\n" + e.StackTrace);
            }

            this.IsFixedTimeStep = true;

            WorldContent.Init(this.Services, Content.RootDirectory);
            base.Screen.Layers.Add(SceneMgr);
            base.Screen.Layers.Add(uiLayer);
            GameFacade.LastUpdateState = base.Screen.State;

            // Try setting icon again after full initialization (for window managers that need it)
            if (GameFacade.Linux)
            {
                SetWindowIcon();
            }

            if (!GlobalSettings.Default.Windowed && !GameFacade.GraphicsDeviceManager.IsFullScreen)
            {
                GameFacade.GraphicsDeviceManager.ToggleFullScreen();
            }

            GameLog.Write("exit-probe: Initialize exit");
        }

        private void SaveGraphicsModePreference(GlobalGraphicsMode obj)
        {
            GlobalSettings.Default.GlobalGraphicsMode = (int)obj;
            GlobalSettings.Default.Save();
        }

        /// <summary>
        /// Run this instance with GameRunBehavior forced as Synchronous.
        /// </summary>
        public new void Run()
        {
            GameLog.Write("exit-probe: Game.Run(Sync) enter");
            Run(GameRunBehavior.Synchronous);
        }

        /// <summary>
        /// Only used on desktop targets. Use extensive reflection to AVOID linking on iOS!
        /// </summary>
        void AddTextInput()
        {
            this.Window.GetType().GetEvent("TextInput").AddEventHandler(this.Window, (EventHandler<TextInputEventArgs>)GameScreen.TextInput);
        }

        void RegainFocus(object sender, EventArgs e)
        {
            GameFacade.Focus = true;
            RelayFocus(true);
        }

        void LostFocus(object sender, EventArgs e)
        {
            GameFacade.Focus = false;
            RelayFocus(false);
        }

        // UI-26 wire (r260-options-readiness WIRE row a; P1 review fix): TS1
        // 'Lighting' is the boot light source of truth ONLY when no explicit
        // LightingMode is stored (-1 = auto, TSOGame's true derive law).
        // Explicit values — incl. the ultra pin (3) that Simitone.Windows/
        // .Desktop Program.cs write+save every launch — pass through untouched;
        // the options row's toggle maps 0/1 on top of that afterwards
        // (UIOriginalOptionsPanel.ApplyLighting). Driven by the 'uioptswire' gate.
        internal static int DeriveBootLightingMode(int storedLightingMode, bool lighting)
        {
            return storedLightingMode == -1 ? (lighting ? 1 : 0) : storedLightingMode;
        }

        // UI-26 wire (r260-options-readiness WIRE row c, 'Sim In Background'):
        // the production focus→simulator relay over the native cSimulator +52
        // signed-speed law (VM.ApplyFocus suspends SpeedMultiplier at 0 while
        // the window lacks focus and restores the exact prior speed on regain;
        // 'Sim In Background' on leaves the simulator running). The OS handlers
        // above call this; the 'uioptswire' gate drives it directly and reads
        // VM.SpeedMultiplier back.
        internal static void RelayFocus(bool focused)
        {
            try
            {
                var screen = GameFacade.Screens?.CurrentUIScreen
                    as Simitone.Client.UI.Screens.TS1GameScreen;
                screen?.vm?.ApplyFocus(focused, GlobalSettings.Default.TS1SimInBackground);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Sets the window icon for Linux/macOS platforms.
        /// Attempts to load Icon.bmp from multiple locations.
        /// </summary>
        void SetWindowIcon()
        {
            try
            {
                // Get window handle using reflection (MonoGame DesktopGL)
                var handleProperty = this.Window.GetType().GetProperty("Handle");
                if (handleProperty == null)
                {
                    Console.WriteLine("Warning: Could not get window handle for icon");
                    return;
                }

                var windowHandle = (IntPtr)handleProperty.GetValue(this.Window);
                if (windowHandle == IntPtr.Zero)
                {
                    Console.WriteLine("Warning: Window handle is null");
                    return;
                }

                // Try to find Icon.bmp in common locations
                string iconPath = null;
                var possiblePaths = new[]
                {
                    "Icon.bmp",                    // Current directory
                    "Resources/Icon.bmp",          // Resources subdirectory
                    "../Icon.bmp",                 // Parent directory
                    "../Resources/Icon.bmp",       // Parent Resources subdirectory
                    "../../Icon.bmp",              // Two levels up
                    "../../../Icon.bmp",           // Three levels up
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icon.bmp"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icon.bmp")
                };

                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        iconPath = path;
                        break;
                    }
                }

                if (iconPath != null)
                {
                    Simitone.Client.Utils.IconLoader.SetWindowIcon(windowHandle, iconPath);
                }
                else
                {
                    Console.WriteLine("Warning: Icon.bmp not found in any expected location");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to set window icon: {ex.Message}");
            }
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            Effect vitaboyEffect = null;
            try
            {
                GameFacade.MainFont = new FSO.Client.UI.Framework.Font();
                //GameFacade.MainFont.AddSize(12, Content.Load<SpriteFont>("Fonts/Mobile_15px"));
                //GameFacade.MainFont.AddSize(15, Content.Load<SpriteFont>("Fonts/Mobile_20px"));
                //GameFacade.MainFont.AddSize(19, Content.Load<SpriteFont>("Fonts/Mobile_25px"));
                //GameFacade.MainFont.AddSize(37, Content.Load<SpriteFont>("Fonts/Mobile_50px"));

                GameFacade.EdithFont = new FSO.Client.UI.Framework.Font();
                //GameFacade.EdithFont.AddSize(12, Content.Load<SpriteFont>("Fonts/Trebuchet_12px"));
                //GameFacade.EdithFont.AddSize(14, Content.Load<SpriteFont>("Fonts/Trebuchet_14px"));

                // R119: the engine-wide default text renderer is the ORIGINAL
                // variablesans .ffn family (tables 07-20, 'uiglyph'-pinned) —
                // every label/button/dialog/list/text-edit/tooltip renders
                // original glyphs at native size instead of MSDF. MSDF remains
                // only for the Edith debug font below.
                GameFacade.VectorFont = Simitone.Client.UI.Controls.OriginalVectorFont.CreateRoot();
                GameFacade.EdithVectorFont = new FSO.UI.Framework.MSDFFont(Content.Load<FieldFont>("../Fonts/trebuchet"));
                GameFacade.EdithVectorFont.VectorScale = 0.366f;
                GameFacade.EdithVectorFont.Height = 15;
                GameFacade.EdithVectorFont.YOff = 11;

                FSO.UI.Framework.MSDFFont.MSDFEffect = Content.Load<Effect>("Effects/MSDFFont");

                vitaboyEffect = Content.Load<Effect>("Effects/Vitaboy"+((FSOEnvironment.SoftwareDepth)?"iOS":""));
                uiLayer = new UILayer(this);
            }
            catch (Exception e)
            {
                //MessageBox.Windows.Forms.MessageBox.Show("Content could not be loaded. Make sure that the FreeSO content has been compiled! (ContentSrc/TSOClientContent.mgcb)");
                Console.WriteLine(e.ToString());
                Exit();
            }

            FSO.Vitaboy.Avatar.setVitaboyEffect(vitaboyEffect);

            WeatherSounds.Load("Content");
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
            WeatherSounds.Unload();
        }


        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            base.OnExiting(sender, args);
            GameThread.SetKilled();
            var closeOk = GameFacade.Screens.CurrentUIScreen?.CloseAttempt() ?? true;
            args.Cancel = !closeOk;
            if (args.Cancel)
            {
                // Engine-exit fix: TS1GameScreen.CloseAttempt() defers (raises a "Save
                // before quitting?" confirm dialog via GameThread.NextUpdate) and returns
                // false, so MonoGame consumes _shouldExit once and never calls EndRun ->
                // the process hangs after Exit() (observed on mac-port, intermittent).
                // Re-arm Exit for the next update: once the dialog is up CloseAttempt()
                // returns true again and EndRun() proceeds.
                GameThread.NextUpdate(x => Exit());
            }
            GameLog.Write("exit-probe: OnExiting cancel=" + args.Cancel + " screen=" +
                (GameFacade.Screens.CurrentUIScreen?.GetType().Name ?? "null"));
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            if (!HasUpdated)
            {
                this.IsMouseVisible = true;
                if (!FSOEnvironment.SoftwareKeyboard) AddTextInput();
                // AUD-17 G-1: the UI-26 focus relay existed but was never
                // hooked to OS events, so "Sim In Background" did nothing and
                // the sim kept running on cmd-tab. MonoGame Game raises these
                // on IsActive edges (desktop only).
                this.Activated += RegainFocus;
                this.Deactivated += LostFocus;
                // AUD-17 F-8: enforce the native 800x600 floor at the OS level —
                // below it the original-canvas controls (Done/Cancel at y=529,
                // the bio field) fall outside the window and are unreachable.
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                        System.Runtime.InteropServices.OSPlatform.OSX))
                {
                    try { SDL_SetWindowMinimumSize(Window.Handle, 800, 600); }
                    catch (Exception mwx) { GameLog.Write("minwin: SDL SetWindowMinimumSize unavailable: " + mwx.GetType().Name); }
                }
                // R118: original game name in the title bar (was "Simitone" - residue
                // called out in the loader round).
                this.Window.Title = "The Sims";
                HasUpdated = true;
                GameFacade.Screens = uiLayer;
                GameController.EnterLoading();
            }
            GameThread.UpdateExecuting = true;

            if (HITVM.Get() != null) HITVM.Get().Tick();

            base.Update(gameTime);
            GameThread.UpdateExecuting = false;
        }
    }
}
