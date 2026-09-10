/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/
using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Utils;
using FSO.Content;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using FSO.SimAntics.Primitives;
using FSO.SimAntics.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Panels.LiveSubpanels;
using Simitone.Client.UI.Screens;
using Simitone.Client.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace Simitone.Client
{
    /// <summary>
    /// R247 'uitutorial-lifecycle' (opt-in, focused) — the tutorial lifecycle
    /// battery (r247-tut-lifecycle + r247-fam-import decode law):
    ///   (a) the options reset row through the REAL panel and dialogs (confirm
    ///       → save gate → StageTutorialReset → neighborhood screen), then the
    ///       second attempt failing per the native fail-if-exists copy;
    ///   (b) the neighborhood import poll consuming a staged
    ///       Import/Tutorial.FAM (family/characters/House07 per the import law,
    ///       fresh IffFile parses only — never a second TS1NeighborhoodProvider);
    ///   (c) VMGenericTS1Call 0/8/9 driven through the real VM (TutorialState
    ///       transitions + the global-26 clears);
    ///   (d) the tutorial-object spawner on isolated VMs (armed house spawns
    ///       out-of-world; g26 == 0 and state 3 do not);
    ///   (e) the ESC cancel (owner dispatched + highlight hidden);
    ///   (f) the move-in refusal guard order and, when the engine flags a
    ///       tutorial house, the real 132[16]/[17] dialog.
    ///
    /// OPT-IN (not in the default Checks string): sections (b)-(f) hard-assert
    /// the R247 engine contract, which may land after this client round — with
    /// the API missing the battery asserts the documented stub contract and
    /// logs pending-engine disclosures, so its meaning depends on the engine
    /// build under test. Like ucasflow it also writes files (reset copy +
    /// import), hence the R246 BeginIsolation UserDir redirect: a fresh temp
    /// dir BEFORE Content.Init clones the pristine UserData there; game-data
    /// stays read-only (Tutorial.FAM + Neighborhood.iff hashed at Begin and
    /// re-verified at the end).
    ///
    /// Determinism: frame-based waits (the only wall-clock gate is the native
    /// 1000 ms import cadence probe, bounded well under a second), the auto
    /// poll is suppressed so every import is battery-driven, and no literal
    /// GUID asserts.
    /// </summary>
    internal sealed class AutotestTutorialLifecycle247
    {
        // --- isolation (driven from AutotestRunner.Begin, before Content.Init) ---
        internal static string RedirectDir;
        internal static string IsolationError;
        private static string _priorUserDir;
        private static string _gdTutorialFam;
        private static string _gdTutorialSha;
        private static long _gdTutorialLen;
        private static DateTime _gdTutorialMtime;
        private static string _gdNeighPath;
        private static string _gdNeighSha;
        private static long _gdNeighLen;
        private static DateTime _gdNeighMtime;

        /// <summary>
        /// Opt-in isolation hook (AutotestCASFlow.BeginIsolation idiom): when
        /// the checks string requests uitutorial-lifecycle, FSOEnvironment.UserDir
        /// is redirected into a fresh temp dir. The focused -autotest-opts string
        /// replaces Config.Checks, so the default suite is inert.
        /// </summary>
        internal static bool BeginIsolation(string checksCsv)
        {
            var c = checksCsv?.Trim().Trim('"');
            if (string.IsNullOrEmpty(c)) return false;
            var names = c.Split(',').Select(s => s.Trim().ToLowerInvariant()).ToList();
            if (!names.Contains("uitutorial-lifecycle")) return false;
            try
            {
                _priorUserDir = FSOEnvironment.UserDir;
                RedirectDir = Path.Combine(Path.GetTempPath(), "simitone-r247-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(RedirectDir);
                FSOEnvironment.UserDir = RedirectDir;
                // game-data integrity baseline (READ-ONLY spot-check files)
                var basePath = GlobalSettings.Default.TS1HybridPath;
                if (!string.IsNullOrEmpty(basePath))
                {
                    _gdTutorialFam = Path.Combine(basePath, "UserData", "Tutorial.FAM");
                    if (File.Exists(_gdTutorialFam))
                    {
                        _gdTutorialLen = new FileInfo(_gdTutorialFam).Length;
                        _gdTutorialMtime = File.GetLastWriteTimeUtc(_gdTutorialFam);
                        _gdTutorialSha = Sha256(_gdTutorialFam);
                    }
                    _gdNeighPath = Path.Combine(basePath, "UserData", "Neighborhood.iff");
                    if (File.Exists(_gdNeighPath))
                    {
                        _gdNeighLen = new FileInfo(_gdNeighPath).Length;
                        _gdNeighMtime = File.GetLastWriteTimeUtc(_gdNeighPath);
                        _gdNeighSha = Sha256(_gdNeighPath);
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                // Isolation is mandatory for this flow — never run unisolated.
                IsolationError = e.GetType().Name + ": " + e.Message;
                return true;
            }
        }

        private static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        // phases
        private const int P_BOOT = 0;        // nbhd ready -> load house 5
        private const int P_LOT = 1;         // wait in-lot -> mount OPTIONS
        private const int P_OPTS = 2;        // press the reset row button
        private const int P_CONFIRM = 3;     // assert + accept the confirm dialog
        private const int P_SAVEPROMPT = 4;  // assert + accept the save gate (Yes)
        private const int P_RESET1 = 5;      // success path asserts
        private const int P_LOT2 = 6;        // reload lot for the second attempt
        private const int P_OPTS2 = 7;
        private const int P_CONFIRM2 = 8;
        private const int P_SAVEPROMPT2 = 9; // save gate (No: proceed without saving)
        private const int P_RESET2 = 10;     // fail-if-exists asserts
        private const int P_IMPORT = 11;     // stage + drive the import poll
        private const int P_LOAD7 = 12;      // load the tutorial house
        private const int P_COMPLETE = 13;   // generic calls 0/8/9 through the real VM
        private const int P_CANCEL = 14;     // ESC binding on the same session
        private const int P_SPAWNER = 15;    // isolated-VM spawner law
        private const int P_CHEAT = 16;      // F1: the real cheat console → spawner inhibit law
        private const int P_REFUSAL = 17;    // move-in refusal
        private const int P_EVIDENCE = 18;
        private const int P_FINISH = 19;

        private const short ProbeHouse = 5;   // ordinary residential lot for the reset flow
        private const short TutorialHouse = 7; // the Tutorial.FAM FAMI house

        private readonly Action<string> _log;
        private int _phase = P_BOOT;
        private int _frames;
        private int _passed, _failed;
        private readonly List<string> _failures = new List<string>();

        private TS1GameScreen _screen;
        private UIOriginalOptionsPanel _options;
        private VM _liveVm;
        private bool _engineReset;      // StageTutorialReset API present
        private int _resetCallBaseline; // StageResetCallCount before reset #2
        private bool _seamOwner;        // cancel phase used SetTutorialObject
        private VMEntity _cancelOwner;

        // cheat-dispatch sub-phase (P_CHEAT, F1)
        private UICheatTextbox _cheatBox;
        private MethodInfo _cheatCommand; // the production commandEntered entry
        private int _cheatStep;           // 0=entry, 1..4 = off/on/0/5 awaiting their flip
        private bool _cheatConsole;       // the real console path resolves a caller
        private bool _inhibitBaseline;

        // import cadence sub-phase (P_IMPORT, F9)
        private int _importStage = -1;    // -1=body, 0/1=probe stages
        private int _cadenceArmBefore, _cadenceRearmBefore;
        private DateTime _cadenceFireTime;
        private int _spawnEvidenceBaseline; // spawner law evidence before the tutorial-house load

        // import-phase baselines (fresh-parse world)
        private HashSet<ushort> _baselineFamiIds;
        private List<string> _baselineCharFiles;
        private Dictionary<string, string> _baselineHouse07Chunks; // type:id -> label
        private int _tutorialStateBaseline;

        public int Failed => _failed;
        public string Diagnostics => "passed=" + _passed + " failed=" + _failed + " phase=" + _phase
            + (_failures.Count == 0 ? "" : " FAIL [" + string.Join("; ", _failures) + "]");

        public AutotestTutorialLifecycle247(Action<string> log)
        {
            _log = log ?? (s => { });
        }

        // ------------------------------------------------------------------
        // drivers
        // ------------------------------------------------------------------

        public bool Tick()
        {
            if (_phase == P_FINISH) return true;
            try
            {
                switch (_phase)
                {
                    case P_BOOT: PhaseBoot(); break;
                    case P_LOT: PhaseLot(1); break;
                    case P_OPTS: PhaseOpts(P_CONFIRM); break;
                    case P_CONFIRM: PhaseConfirm(); break;
                    case P_SAVEPROMPT: PhaseSavePrompt(true); break;
                    case P_RESET1: PhaseReset1(); break;
                    case P_LOT2: PhaseLot(2); break;
                    case P_OPTS2: PhaseOpts(P_CONFIRM2); break;
                    case P_CONFIRM2: PhaseConfirm2(); break;
                    case P_SAVEPROMPT2: PhaseSavePrompt(false); break;
                    case P_RESET2: PhaseReset2(); break;
                    case P_IMPORT: PhaseImport(); break;
                    case P_LOAD7: PhaseLoad7(); break;
                    case P_COMPLETE: PhaseComplete(); break;
                    case P_CANCEL: PhaseCancel(); break;
                    case P_SPAWNER: PhaseSpawner(); break;
                    case P_CHEAT: PhaseCheat(); break;
                    case P_REFUSAL: PhaseRefusal(); break;
                    case P_EVIDENCE: PhaseEvidence(); break;
                }
            }
            catch (Exception e)
            {
                Check(false, "phase" + _phase + "-exception");
                _log("AUTOTEST uitutorial-lifecycle phase" + _phase + " EXC " + e.GetType().Name + ": " + e.Message
                    + " AT " + (e.StackTrace ?? "").Replace('\n', ' '));
                _phase = P_FINISH;
            }
            return _phase == P_FINISH;
        }

        private void Advance(int phase)
        {
            _phase = phase;
            _frames = 0;
        }

        private void ToFinish()
        {
            _phase = P_FINISH;
        }

        private bool Expired(string what, int limit = 3600)
        {
            if (_frames <= limit) return false;
            Check(false, what + "-frames-timeout");
            ToFinish();
            return true;
        }

        private void Check(bool ok, string name)
        {
            if (ok) { _passed++; }
            else { _failed++; _failures.Add(name); }
            _log("AUTOTEST uitutorial-lifecycle " + name + ": " + (ok ? "PASS" : "FAIL"));
        }

        private string UserPath(params string[] parts)
        {
            var acc = FSOEnvironment.UserDir;
            foreach (var p in parts) acc = Path.Combine(acc, p);
            return acc;
        }

        // ------------------------------------------------------------------
        // input idioms (AutotestCASFlow ports — production handlers only)
        // ------------------------------------------------------------------

        private static readonly MethodInfo MouseEvent = typeof(UIButton).GetMethod("OnMouseEvent",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo TickPoller = typeof(UILotControl).GetMethod("TickTutorialPoller",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static UpdateState FreshState()
        {
            return new UpdateState
            {
                Time = new GameTime(),
                WindowFocused = true,
                InputManager = new InputManager()
            };
        }

        private void Press(UIButton button)
        {
            var state = FreshState();
            MouseEvent.Invoke(button, new object[] { UIMouseEventType.MouseDown, state });
            MouseEvent.Invoke(button, new object[] { UIMouseEventType.MouseUp, state });
        }

        /// <summary>Answer a mounted UIMobileAlert through its real buttons
        /// (YesNo/YesNoCancel order: [0] = Yes, [1] = No).</summary>
        private void PressAlertButton(UIMobileAlert dialog, int index)
        {
            var buttons = typeof(UIMobileAlert).GetField("Buttons", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(dialog) as List<UIButton>;
            if (buttons == null || index >= buttons.Count) { Check(false, "alert-buttons-missing"); ToFinish(); return; }
            Press(buttons[index]);
        }

        /// <summary>GPU capture (AutotestCASFlow recipe) into ui-audit/r247.</summary>
        private void Capture(string tag)
        {
            try
            {
                var ui = GameFacade.Screens;
                var gd = GameFacade.GraphicsDevice;
                if (gd == null) { _log("AUTOTEST uitutorial-lifecycle capture " + tag + ": no device"); return; }
                int w = gd.Viewport.Width, h = gd.Viewport.Height;
                if (w <= 0 || h <= 0)
                {
                    w = gd.PresentationParameters.BackBufferWidth;
                    h = gd.PresentationParameters.BackBufferHeight;
                }
                using (var rt = PPXDepthEngine.CreateRenderTarget(gd, 1, 0, SurfaceFormat.Color, w, h, DepthFormat.None))
                {
                    gd.SetRenderTarget(rt);
                    gd.Clear(new Color(0x72, 0x72, 0x72, 0xFF));
                    ui.SpriteBatch.UIBegin(BlendState.AlphaBlend, SpriteSortMode.Immediate);
                    try
                    {
                        var warm = ui.CurrentUIScreen;
                        foreach (var child in (warm as UIContainer)?.GetChildren() ?? new List<UIElement>())
                            child.PreDraw(ui.SpriteBatch);
                        warm?.PreDraw(ui.SpriteBatch);
                        warm?.Draw(ui.SpriteBatch);
                    }
                    catch { }
                    ui.SpriteBatch.End();
                    try { GameFacade.Scenes.PreDraw(gd); } catch (Exception we) { _log("AUTOTEST uitutorial-lifecycle capture " + tag + " world-predraw EXC " + we.Message); }
                    gd.SetRenderTarget(rt);
                    gd.Clear(new Color(0x72, 0x72, 0x72, 0xFF));
                    try { GameFacade.Scenes.Draw(gd); } catch (Exception we) { _log("AUTOTEST uitutorial-lifecycle capture " + tag + " world-draw EXC " + we.Message); }
                    ui.SpriteBatch.UIBegin(BlendState.AlphaBlend, SpriteSortMode.Immediate);
                    try { ui.Draw(ui.SpriteBatch); } finally { ui.SpriteBatch.End(); }
                    gd.SetRenderTarget(null);
                    var dir = Path.Combine(FSOEnvironment.UserDir, "ui-audit", "r247");
                    Directory.CreateDirectory(dir);
                    var path = Path.Combine(dir, "tutlifecycle-" + tag + ".png");
                    using (var fs = File.Create(path))
                        rt.SaveAsPng(fs, rt.Width, rt.Height);
                    _log("AUTOTEST uitutorial-lifecycle capture " + tag + " " + w + "x" + h + " -> " + path);
                }
            }
            catch (Exception e)
            {
                Check(false, "capture-" + tag + "-exception");
                _log("AUTOTEST uitutorial-lifecycle capture EXC " + tag + " " + e.GetType().Name + ": " + e.Message);
            }
        }

        private void LoadHouse(short house)
        {
            // The production lot-load choke point (TryNextHouse idiom).
            var screen = _screen;
            GameThread.NextUpdate(x =>
            {
                try { screen.PlayHouse(house, null); }
                catch (Exception e)
                {
                    _log("AUTOTEST uitutorial-lifecycle playhouse EXC " + e.GetType().Name + ": " + e.Message);
                }
            });
        }

        private void ExitLotDeferred()
        {
            var screen = _screen;
            GameThread.NextUpdate(x =>
            {
                try { screen.ExitLot(); }
                catch (Exception e)
                {
                    _log("AUTOTEST uitutorial-lifecycle exitlot EXC " + e.GetType().Name + ": " + e.Message);
                }
            });
        }

        private void MountOptions()
        {
            var mp = _screen.Frontend?.MainPanel;
            if (mp == null) { Check(false, "mainpanel-missing"); ToFinish(); return; }
            // uiaudit mount idiom: real SetMode with the panel forced active.
            mp.SetMode(UIMainPanelMode.OPTIONS);
            mp.Visible = true;
            mp.PanelActive = true;
            foreach (var fadeable in mp.GetFadeables())
                if (fadeable != null) fadeable.Opacity = 1;
        }

        private UIOriginalOptionsPanel MountedOptions()
        {
            return _screen.Frontend?.MainPanel?.SubPanel as UIOriginalOptionsPanel;
        }

        private UIButton ResetRowButton(UIOriginalOptionsPanel options)
        {
            var row = options.Rows.FirstOrDefault(r => r.CaptionIndex == 69);
            return row?.Widget as UIButton;
        }

        // ------------------------------------------------------------------
        // phases
        // ------------------------------------------------------------------

        private void PhaseBoot()
        {
            if (IsolationError != null)
            {
                Check(false, "isolation-redirect (" + IsolationError + ")");
                ToFinish();
                return;
            }
            _frames++;
            _screen = GameFacade.Screens?.CurrentUIScreen as TS1GameScreen;
            if (_screen == null || _screen.InLot || _screen.TS1NeighSwitcher == null)
            {
                if (Expired("boot", 18000)) return; // first-run UserData clone can be slow
                return;
            }
            if (_frames < 60) return; // settle, matching the uicasflow entry
            _engineReset = TutorialEngine247.HasStageReset;
            _log("AUTOTEST uitutorial-lifecycle engine-contract stageReset=" + TutorialEngine247.HasStageReset
                + " checkImports=" + TutorialEngine247.HasCheckImports
                + " isTutorialHouse=" + TutorialEngine247.HasIsTutorialHouse
                + " tutorialState=" + TutorialEngine247.HasTutorialState
                + " requestCancel=" + TutorialEngine247.HasRequestCancel
                + " userdir=" + FSOEnvironment.UserDir);
            // The battery drives every import poll itself; the production 1 s
            // cadence stays suppressed so the staged file cannot race.
            _screen.TutorialImportPollSuppressed = true;
            LoadHouse(ProbeHouse);
            Advance(P_LOT);
        }

        private void PhaseLot(int attempt)
        {
            _frames++;
            _screen = GameFacade.Screens?.CurrentUIScreen as TS1GameScreen;
            if (_screen == null || !_screen.InLot || _screen.vm == null || !_screen.vm.Ready
                || _screen.Frontend?.MainPanel == null)
            {
                if (Expired("lot" + attempt)) return;
                return;
            }
            if (_frames < 30) return; // let the first VM ticks settle
            _liveVm = _screen.vm;
            Check(true, "lot" + attempt + "-loaded");
            MountOptions();
            Advance(attempt == 1 ? P_OPTS : P_OPTS2);
        }

        private void PhaseOpts(int next)
        {
            _frames++;
            _options = MountedOptions();
            Check(_options != null, "options-mounted");
            if (_options == null) { ToFinish(); return; }
            // The reset row lives on the Play sub-screen; the panel ctor only
            // mounts the main grid, so open the section first (uiopts idiom).
            _options.ShowScreen("play");
            var row = _options.Rows.FirstOrDefault(r => r.CaptionIndex == 69);
            Check(row != null, "reset-row-mounted");
            if (row == null) { ToFinish(); return; }
            Check(row.Caption == UIOriginalOptionsPanel.S(69), "reset-row-canon-caption");
            var btn = ResetRowButton(_options);
            Check(btn != null, "reset-row-button-mounted");
            if (btn == null) { ToFinish(); return; }
            Press(btn);
            _phase = next;
            _frames = 0;
        }

        private void PhaseConfirm()
        {
            _frames++;
            if (_frames < 2) return;
            var dialog = _options.ResetConfirmDialogForProbe;
            Check(dialog != null, "reset-confirm-dialog-mounts");
            if (dialog == null) { ToFinish(); return; }
            // STRING GAP (disclosed at the row): the native confirm literals are
            // runtime-localized; the port surfaces the row's canon 145[69]/[71].
            Check(dialog.TitleTextForProbe == UIOriginalOptionsPanel.S(69), "reset-confirm-title");
            Check(dialog.MessageTextForProbe == UIOriginalOptionsPanel.S(71), "reset-confirm-message");
            Capture("reset-confirm-dialog");
            PressAlertButton(dialog, 0); // Yes — the native result-3 proceed
            Advance(P_SAVEPROMPT);
        }

        private void PhaseSavePrompt(bool pressYes)
        {
            _frames++;
            if (_frames < 2) return;
            var prompt = _options.ResetSavePromptForProbe;
            // Port adaptation (skeptic correction 3, disclosed at the row): the
            // options panel only exists in-lot, so the gate follows the port's
            // existing save machinery prompt instead of the native dirty byte.
            Check(prompt != null, "reset-savegate-prompt-mounts");
            if (prompt == null) { ToFinish(); return; }
            Check(prompt.TitleTextForProbe == GameFacade.Strings.GetString("153", "3"), "reset-savegate-title");
            PressAlertButton(prompt, pressYes ? 0 : 1); // Yes: save+proceed / No: proceed
            Advance(pressYes ? P_RESET1 : P_RESET2);
        }

        private void PhaseReset1()
        {
            _frames++;
            if (_engineReset)
            {
                if (TutorialEngine247.StageResetCallCount < 1)
                {
                    if (Expired("reset1-dispatch")) return;
                    return;
                }
                Check(true, "reset-stage-called");
                // Native success path: DoNbhdScreen(true) — the port exits the
                // lot synchronously inside the dialog callback.
                if (_screen.InLot)
                {
                    if (Expired("reset1-exitlot")) return;
                    return;
                }
                Check(true, "reset-success-neighborhood-screen");
                Check(File.Exists(UserPath("UserData", "Import", "Tutorial.FAM")),
                    "reset-import-fam-staged");
                Check(File.Exists(UserPath("UserData", "Import", "Tutorial.FAM"))
                    && new FileInfo(UserPath("UserData", "Import", "Tutorial.FAM")).Length
                        == new FileInfo(UserPath("UserData", "Tutorial.FAM")).Length,
                    "reset-import-fam-byte-length");
                Check(_screen.TutorialImportPollSuppressed, "reset-import-poll-suppressed");
                _log("AUTOTEST uitutorial-lifecycle reset#1 OK; reloading house "
                    + ProbeHouse + " for the fail-if-exists attempt");
                LoadHouse(ProbeHouse);
                Advance(P_LOT2);
                return;
            }

            // Engine API missing: the row follows the native FAILURE law.
            if (_frames < 2) return;
            var error = _options.ResetErrorDialogForProbe;
            Check(error != null, "reset-stub-error-dialog-mounts");
            Check(_screen.InLot, "reset-failure-stays-in-lot");
            Check(!File.Exists(UserPath("UserData", "Import", "Tutorial.FAM")),
                "reset-stub-no-disk-write");
            if (error != null)
            {
                Capture("reset-error-dialog");
                PressAlertButton(error, 0); // Ok
            }
            _log("AUTOTEST uitutorial-lifecycle PENDING-ENGINE: StageTutorialReset missing — "
                + "reset battery asserts the stub failure contract only");
            var mp = _screen.Frontend?.MainPanel;
            if (mp != null) mp.SetMode(UIMainPanelMode.LIVE);
            ExitLotDeferred();
            Advance(P_IMPORT);
        }

        private void PhaseConfirm2()
        {
            _frames++;
            if (_frames < 2) return;
            var dialog = _options.ResetConfirmDialogForProbe;
            Check(dialog != null, "reset2-confirm-dialog-mounts");
            if (dialog == null) { ToFinish(); return; }
            PressAlertButton(dialog, 0);
            Advance(P_SAVEPROMPT2);
        }

        private void PhaseReset2()
        {
            _frames++;
            _resetCallBaseline = Math.Max(_resetCallBaseline, TutorialEngine247.StageResetCallCount);
            if (TutorialEngine247.StageResetCallCount < 2)
            {
                if (Expired("reset2-dispatch")) return;
                return;
            }
            Check(TutorialEngine247.StageResetCallCount >= 2, "reset-stage-called-again");
            var error = _options.ResetErrorDialogForProbe;
            // Native fail-if-exists: the second reset's CopyFileA(flag 1)
            // refuses because Import/Tutorial.FAM already exists.
            Check(error != null, "reset-second-fails-if-exists-dialog");
            Check(File.Exists(UserPath("UserData", "Import", "Tutorial.FAM")),
                "reset-failure-file-intact");
            Check(_screen.InLot, "reset2-failure-stays-in-lot");
            if (error != null)
            {
                Capture("reset-error-dialog");
                PressAlertButton(error, 0); // Ok
            }
            ExitLotDeferred();
            Advance(P_IMPORT);
        }

        private void PhaseImport()
        {
            _frames++;
            if (_importStage >= 0) { ContinueCadenceProbe(); return; }
            _screen = GameFacade.Screens?.CurrentUIScreen as TS1GameScreen;
            if (_screen == null || _screen.InLot || _screen.TS1NeighSwitcher == null)
            {
                if (Expired("import-boot")) return;
                return;
            }
            if (_frames < 10) return; // neighborhood screen settled

            var importFile = UserPath("UserData", "Import", "Tutorial.FAM");
            var pristine = UserPath("UserData", "Tutorial.FAM");
            if (!TutorialEngine247.HasCheckImports)
            {
                // Stub contract: stage the file, drive the poll entry, and
                // assert the wired-but-inert behavior.
                Directory.CreateDirectory(UserPath("UserData", "Import"));
                File.Copy(pristine, importFile, true);
                var stubResult = _screen.PollNeighborhoodImports();
                Check(stubResult == TutorialEngine247.ImportPollResult.Nothing, "poll-stub-noop");
                Check(File.Exists(importFile), "poll-stub-file-untouched");
                _log("AUTOTEST uitutorial-lifecycle PENDING-ENGINE: CheckForNewImports missing — "
                    + "import battery section is stub-tier (opt-in-verbose pending the engine)");
                BeginCadenceProbe();
                return;
            }

            // --- baselines before the import (fresh disk parses only) ---
            var neighPath = UserPath("UserData", "Neighborhood.iff");
            var baselineIff = new IffFile(neighPath);
            _baselineFamiIds = (baselineIff.List<FAMI>() ?? new List<FAMI>())
                .Select(f => f.ChunkID).ToHashSet();
            var baselineFami1 = baselineIff.Get<FAMI>(1);
            var baselineFami1Budget = baselineFami1?.Budget ?? 0;
            var baselineFami1Arch = baselineFami1?.ValueInArch ?? 0;
            var charDir = UserPath("UserData", "Characters");
            _baselineCharFiles = Directory.GetFiles(charDir, "User*.iff")
                .Select(Path.GetFileName).ToList();
            var house07Path = UserPath("UserData", "Houses",
                "House" + TutorialHouse.ToString().PadLeft(2, '0') + ".iff");
            var houseBefore = new IffFile(house07Path);
            _baselineHouse07Chunks = houseBefore.ListAll().ToDictionary(
                c => c.ChunkType + ":" + c.ChunkID, c => c.ChunkLabel ?? "");
            var houseHadFami = _baselineHouse07Chunks.Keys.Any(k => k.StartsWith("FAMI:"));
            // F9: pristine-no-fami is data-dependent on the machine UserData.
            // Decode precondition (r247 §C downstream notes): the SHIPPED NGBH
            // carries word1=1 (tutorial state) / word2=7 (tutorial house). Only
            // when the live boot mirror matches that shipped pair does the
            // decode's model govern this data — then a FAMI in the pristine
            // House07 FAILS LOUDLY; any other data skips with disclosure.
            var shippedMirror = TutorialEngine247.HasTutorialState
                && TutorialEngine247.HasTutorialHouse
                && TutorialEngine247.GetTutorialState() == 1
                && TutorialEngine247.GetTutorialHouse() == 7;
            if (shippedMirror)
            {
                Check(!houseHadFami, "house07-pristine-no-fami");
            }
            else
            {
                Check(true, "house07-pristine-precondition-unmet");
                _log("AUTOTEST uitutorial-lifecycle DATA DISCLOSURE: boot mirror state="
                    + TutorialEngine247.GetTutorialState() + " house=" + TutorialEngine247.GetTutorialHouse()
                    + " house07 FAMI=" + houseHadFami
                    + " — machine UserData diverges from the decode's shipped NGBH pair "
                    + "(word1=1/word2=7); pristine-no-fami assert skipped");
            }
            _tutorialStateBaseline = TutorialEngine247.GetTutorialState();

            // P0 template-restore baseline: the SHARED template person (the
            // generator's TEMPLATE_GUID — the same live object the provider's
            // import renames, re-GUIDes and whose STR# 200 it overwrites with
            // the uChr clone) must keep its pre-import body strings. Read
            // through the REAL content path the provider mutates
            // (Content.WorldObjects), not a fresh parse (R248 P0).
            var templateObj = Content.Get().WorldObjects.Get(SimitoneNeighbourGenerator.TEMPLATE_GUID);
            var templateStr = templateObj?.Resource?.Get<STR>(200);
            var templateSnapshot = (templateStr != null)
                ? Enumerable.Range(0, templateStr.Length)
                    .Select(i => (templateStr.GetString(i) ?? "<null>") + "\u0001"
                        + (templateStr.GetComment(i) ?? "<null>"))
                    .ToList()
                : null;
            Check(templateSnapshot != null, "template-str200-baseline");

            // stage: a REAL copy of the pristine FAM into Import/ (what the
            // reset row's success leaves behind)
            Directory.CreateDirectory(UserPath("UserData", "Import"));
            File.Copy(pristine, importFile, true);
            var famChunks = new IffFile(pristine).ListAll()
                .Select(c => c.ChunkType + ":" + c.ChunkID).ToHashSet();

            var result = _screen.PollNeighborhoodImports();
            if (result != TutorialEngine247.ImportPollResult.Imported)
            {
                // The bool contract cannot distinguish a stub from a refusal:
                // either way the file must be untouched and the client inert.
                Check(File.Exists(importFile), "poll-false-file-untouched");
                Check(result == TutorialEngine247.ImportPollResult.Nothing,
                    "poll-false-not-error (" + result + ")"); //an Error with a staged file is an engine failure
                _log("AUTOTEST uitutorial-lifecycle PENDING-ENGINE: CheckForNewImports returned "
                    + result + " with a staged file — the engine's declared R247-fam-import stub "
                    + "contract holds (file untouched, no error); the import law (family/characters/"
                    + "House07 asserts) stays opt-in-verbose until the engine lands it");
                BeginCadenceProbe();
                return;
            }
            Check(true, "poll-imported-true");

            // import law: the Import/ file never survives
            Check(!File.Exists(importFile), "import-file-consumed");

            // the FAM becomes Houses/House07.iff (moved, Gtab persisted first)
            var houseAfter = new IffFile(house07Path);
            var afterChunks = houseAfter.ListAll()
                .Select(c => c.ChunkType + ":" + c.ChunkID).ToHashSet();
            Check(afterChunks.Contains("FAMI:1"), "house07-fami-chunk-present");
            Check(afterChunks.IsSupersetOf(famChunks), "house07-is-the-fam-file");
            var simiAfter = houseAfter.Get<SIMI>(1);
            _log("AUTOTEST uitutorial-lifecycle house07-simi-globals=" + (simiAfter?.GlobalData?.Length ?? -1)
                + " g26=" + (simiAfter != null && simiAfter.GlobalData.Length > 26
                    ? simiAfter.GlobalData[26].ToString() : "<unparsed>")
                + " (SIMI parse covers 0x20/0x40 items by version; g26 feeds the spawner gate)");

            // the imported family: the CORRECTED id law (r247-fam-import decode §3,
            // skeptic §3 — keep the EXPi famId when free in the bin, otherwise 0;
            // there is NO replace-on-collision). The shipped-mirror UserData this
            // battery clones already carries the tutorial family as FAMI 1 (house 7,
            // the two tutorial members), so the real run is the PLAYED path: id 1 is
            // taken → the import claims the id-0 Default slot, EVICTS family 1
            // (loses the house, keeps its net worth) and deletes its members. The
            // R247 battery's fresh-path `id 1` assert encoded only the un-played
            // case — fixed to the corrected law (R248).
            var expectedId = _baselineFamiIds.Contains((ushort)1) ? (ushort)0 : (ushort)1;
            var afterIff = new IffFile(neighPath);
            var fami = afterIff.Get<FAMI>(expectedId);
            Check(fami != null, "import-fami-present (" + expectedId + ")");
            if (fami != null)
            {
                Check(fami.HouseNumber == TutorialHouse, "import-fami-house-7 (" + fami.HouseNumber + ")");
                Check(fami.Budget == 665, "import-fami-funds-665 (" + fami.Budget + ")");
                Check(fami.FamilyGUIDs.Length == 2, "import-fami-two-members (" + fami.FamilyGUIDs.Length + ")");
                var fams = afterIff.Get<FAMs>(expectedId);
                Check(fams != null && fams.GetString(0) == "Newbie",
                    "import-fams-name-ctss1 (" + (fams?.GetString(0) ?? "null") + ")");
            }
            // the eviction law on the played path: the old family 1 keeps its net
            // worth (budget + value-in-arch) and loses the house
            if (expectedId == 0)
            {
                var evicted = afterIff.Get<FAMI>(1);
                Check(evicted != null && evicted.HouseNumber == 0,
                    "import-evicted-loses-house (" + evicted?.HouseNumber.ToString() ?? "null" + ")");
                Check(evicted != null && evicted.Budget == baselineFami1Budget + baselineFami1Arch,
                    "import-evicted-net-worth (" + (evicted?.Budget.ToString() ?? "null")
                    + " expected " + (baselineFami1Budget + baselineFami1Arch) + ")");
            }

            // two fresh character files (uChr 47/48)
            var newFiles = Directory.GetFiles(charDir, "User*.iff")
                .Select(Path.GetFileName).Except(_baselineCharFiles).ToList();
            Check(newFiles.Count == 2, "import-two-character-files (" + newFiles.Count + ")");
            foreach (var file in newFiles)
            {
                var cif = new IffFile(Path.Combine(charDir, file));
                Check(cif.Get<OBJD>(128) != null, "import-char-objd (" + file + ")");
                Check(cif.Get<STR>(200) != null, "import-char-str200 (" + file + ")");
            }

            // P0 template restore: after the import, the template person's
            // STR# 200 slots must equal their pre-import values (value AND
            // comment, every slot) — the uChr clone mutates the STRItems in
            // place, so only a real (deep) restore passes; a shallow snapshot
            // would leak imported text into the shared template and, from
            // there, into every future CAS-created character.
            if (templateSnapshot != null)
            {
                var templateAfter = templateObj.Resource.Get<STR>(200);
                var restored = templateAfter != null
                    && templateAfter.Length == templateSnapshot.Count;
                for (int i = 0; restored && i < templateAfter.Length; i++)
                {
                    restored = (templateAfter.GetString(i) ?? "<null>") + "\u0001"
                        + (templateAfter.GetComment(i) ?? "<null>") == templateSnapshot[i];
                }
                Check(restored, "template-str200-restored (" + (templateAfter?.Length ?? -1)
                    + " strings, expected " + templateSnapshot.Count + ")");
            }

            // the import never touches the completion state (+0x12a)
            if (TutorialEngine247.HasTutorialState)
                Check(TutorialEngine247.GetTutorialState() == _tutorialStateBaseline,
                    "import-tutorialstate-unchanged");
            else
                _log("AUTOTEST uitutorial-lifecycle PENDING-ENGINE: TutorialState missing — "
                    + "state mirror assert skipped");

            // the tutorial latch (import law step 20: the FAM's SIMI global 26 is
            // nonzero → +0x12c = the import house). Data-gated on the shipped
            // mirror like the pristine-house assert above.
            if (TutorialEngine247.HasTutorialHouse && shippedMirror)
                Check(TutorialEngine247.GetTutorialHouse() == TutorialHouse,
                    "import-tutorialhouse-7 (" + TutorialEngine247.GetTutorialHouse() + ")");

            // the poll refresh contract + the REAL 1 s cadence gate (F9: the
            // old single assert was vacuous — both suppression flags toggled
            // inside one battery Tick, so no screen Update ever saw the
            // unsuppressed window. Now REAL frames pass in each state.)
            BeginCadenceProbe();
        }

        /// <summary>F9: the cadence gate, driven across real frames. Stage 0:
        /// un-suppress — the FIRST screen Update on the idle neighborhood
        /// screen must fire the cadence poll (no auto poll has run this
        /// session). Stage 1: stay un-suppressed across a real ≥300 ms window
        /// (well under the 1 s re-arm) — no second fire may happen. Then the
        /// battery's suppression invariant is restored.</summary>
        private void BeginCadenceProbe()
        {
            Check(_screen.TS1NeighPanel != null, "poll-refresh-neighborhood-mounted");
            _cadenceArmBefore = _screen.TutorialImportPollAutoCount;
            _screen.TutorialImportPollSuppressed = false;
            _importStage = 0;
            _frames = 0;
        }

        private void ContinueCadenceProbe()
        {
            if (_importStage == 0)
            {
                if (_screen.TutorialImportPollAutoCount > _cadenceArmBefore)
                {
                    Check(true, "poll-cadence-fires-on-idle-frame");
                    _cadenceFireTime = DateTime.UtcNow;
                    _cadenceRearmBefore = _screen.TutorialImportPollAutoCount;
                    _importStage = 1;
                    _frames = 0;
                    return;
                }
                if (Expired("poll-cadence-arm", 120)) return;
                return;
            }
            if (_importStage == 1)
            {
                var elapsed = (DateTime.UtcNow - _cadenceFireTime).TotalMilliseconds;
                if (_frames < 2 || elapsed < 300)
                {
                    if (Expired("poll-cadence-window", 600)) return;
                    return;
                }
                Check(_screen.TutorialImportPollAutoCount == _cadenceRearmBefore,
                    "poll-cadence-sub-second-rearm (count=" + _screen.TutorialImportPollAutoCount
                    + " expected " + _cadenceRearmBefore + ")");
                _screen.TutorialImportPollSuppressed = true; // restore the battery invariant
                Capture("post-import-neighborhood");
                Advance(P_LOAD7);
            }
        }

        private void PhaseLoad7()
        {
            _frames++;
            if (_frames == 1)
            {
                _spawnEvidenceBaseline = VMTS1TutorialSpawner.SpawnEvidence;
                LoadHouse(TutorialHouse);
                return;
            }
            _screen = GameFacade.Screens?.CurrentUIScreen as TS1GameScreen;
            if (_screen == null || !_screen.InLot || _screen.vm == null || !_screen.vm.Ready)
            {
                if (Expired("load7")) return;
                return;
            }
            if (_frames < 30) return;
            _liveVm = _screen.vm;
            _log("AUTOTEST uitutorial-lifecycle load7 global10=" + _liveVm.GetGlobalValue(10)
                + " (SIMI-carried; informational — the activator leaves it data-side)");
            // The production load (VMBlueprintRestoreCmd tail) runs the LoadHouse
            // spawner: this house is armed (SIMI g26 = 1) and the neighborhood
            // state is still < 3, so the Tutorial object must be spawned
            // out-of-world. R248: the positive evidence is the spawner's own
            // SpawnEvidence seam — on the imported (played-path) state the
            // family holds the CORRECTED collision id 0, and the tutorial lesson
            // DATA cancels the freshly spawned object within a few ticks. That
            // lifecycle is data-driven (the same trees run against the same
            // state natively); the ENGINE law is the five guards + the spawn.
            var spawned = _liveVm.Entities.FirstOrDefault(
                x => x.Object?.OBJ?.GUID == VMTS1TutorialSpawner.TutorialGUID);
            if (spawned != null)
            {
                Check(true, "spawner-live-armed");
                Check(spawned.Position == LotTilePos.OUT_OF_WORLD, "spawner-out-of-world");
            }
            else
            {
                // F3: with the R247 engine contract present, an ARMED load
                // (state < 3, g26 nonzero, not inhibited) that spawns nothing
                // is an engine failure — no stub tier. Absence only passes when
                // a guard legitimately holds, the spawn evidence advanced (the
                // R248 imported-state disclosure above), or the API is missing.
                var state = TutorialEngine247.GetTutorialState();
                var g26 = _liveVm.GetGlobalValue(26);
                if (TutorialEngine247.HasTutorialState && state < 3 && g26 != 0
                    && !VMTS1TutorialSpawner.InhibitSpawn)
                {
                    if (VMTS1TutorialSpawner.SpawnEvidence > _spawnEvidenceBaseline)
                    {
                        Check(true, "spawner-live-armed (spawned-then-data-cancelled)");
                        _log("AUTOTEST uitutorial-lifecycle DATA DISCLOSURE: the spawner ran "
                            + "(evidence " + (VMTS1TutorialSpawner.SpawnEvidence - _spawnEvidenceBaseline)
                            + " spawn(s)) but the Tutorial object is already gone — the imported "
                            + "family carries the corrected collision id 0 (r247-fam-import decode "
                            + "§3 id law on the played path) and the tutorial lesson data cancels "
                            + "the object on that state. Engine law held; lifecycle is data-side.");
                    }
                    else
                    {
                        Check(false, "spawner-live-armed-missing (state=" + state + " g26=" + g26 + ")");
                    }
                }
                else
                {
                    Check(true, "spawner-live-precondition-unmet");
                    _log("AUTOTEST uitutorial-lifecycle spawner-load preconditions: state=" + state
                        + " g26=" + g26 + " inhibit=" + VMTS1TutorialSpawner.InhibitSpawn
                        + " engineStateMirror=" + TutorialEngine247.HasTutorialState
                        + " (spawn correctly absent when a guard holds)");
                }
            }
            Advance(P_COMPLETE);
        }

        private VMPrimitiveExitCode DriveGenericCall(int mode, VMAvatar caller)
        {
            // The genericcall12 live idiom: a synthetic frame through the real
            // primitive on the live VM's thread.
            var previousTemp0 = caller.Thread.TempRegisters[0];
            try
            {
                var frame = new VMStackFrame
                {
                    Thread = caller.Thread,
                    Caller = caller,
                    Callee = caller,
                    StackObject = caller,
                };
                return new VMGenericTS1Call().Execute(frame,
                    new VMGenericTS1CallOperand { Call = (VMGenericTS1CallMode)mode });
            }
            finally
            {
                caller.Thread.TempRegisters[0] = previousTemp0;
            }
        }

        /// <summary>Drive one generic call, surviving an engine-side crash:
        /// the exception surfaces as a named FAIL (the engine bug stays visible
        /// without masking the rest of the battery) and returns null.</summary>
        private VMPrimitiveExitCode? DriveGenericCallSafe(int mode, VMAvatar caller, string tag)
        {
            try
            {
                return DriveGenericCall(mode, caller);
            }
            catch (Exception e)
            {
                Check(false, tag + "-engine-exception (" + e.GetType().Name + " at "
                    + (e.StackTrace ?? "").Split('\n').FirstOrDefault() + ")");
                _log("AUTOTEST uitutorial-lifecycle ENGINE-BUG " + tag + ": " + e.Message);
                return null;
            }
        }

        /// <summary>Raw read of one signed word of a house file's SIMI globals —
        /// the same chunk-walk law PatchHouseSimiGlobal uses ([type:4]
        /// [size:u32 BE][id:2][flags:2][label:64] records from byte 64, payload
        /// [pad:4][version:4]["IMIS":4][globals i16 LE...]) — independent of the
        /// SIMI codec's version-gated parse. Null when the chunk or the global
        /// is absent.</summary>
        private static short? ReadHouseSimiGlobal(string housePath, int index)
        {
            try
            {
                var data = File.ReadAllBytes(housePath);
                var off = 64;
                while (off + 76 <= data.Length)
                {
                    var size = (data[off + 4] << 24) | (data[off + 5] << 16) | (data[off + 6] << 8) | data[off + 7];
                    if (size < 76 || off + size > data.Length) return null;
                    if (data[off] == 'S' && data[off + 1] == 'I' && data[off + 2] == 'M' && data[off + 3] == 'I')
                    {
                        var payload = off + 76;
                        if (payload + 12 > data.Length
                            || data[payload + 8] != 'I' || data[payload + 9] != 'M'
                            || data[payload + 10] != 'I' || data[payload + 11] != 'S') return null;
                        var globalPos = payload + 12 + (index * 2);
                        if (globalPos + 2 > payload + (size - 76)) return null;
                        return (short)(data[globalPos] | (data[globalPos + 1] << 8));
                    }
                    off += size;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private void PhaseComplete()
        {
            _frames++;
            if (_frames < 5) return; // let the load ticks run the setup trees
            var caller = _liveVm.Entities.OfType<VMAvatar>().FirstOrDefault(a => a.Thread != null);
            Check(caller != null, "completion-live-caller");
            if (caller == null) { ExitLotDeferred(); Advance(P_REFUSAL); return; }

            var state0 = TutorialEngine247.GetTutorialState();
            var g26Before = _liveVm.GetGlobalValue(26);
            _log("AUTOTEST uitutorial-lifecycle completion-baseline state=" + state0
                + " live-g26=" + g26Before + " currentHouse=" + _liveVm.TS1State.CurrentHouse);

            var exit0 = DriveGenericCallSafe(0, caller, "completion-mode0"); // HouseTutorialComplete
            if (exit0 != null)
            {
                Check(exit0 == VMPrimitiveExitCode.GOTO_TRUE, "completion-mode0-exit (" + exit0 + ")");
                var state1 = TutorialEngine247.GetTutorialState();
                if (TutorialEngine247.HasTutorialState)
                {
                    // F3: engine contract present — the native law must hold
                    // (no stub tier). +0x12a is an ABSOLUTE write of arg+1, so
                    // call 0 lands on state 1 (invisible when the shipped NGBH
                    // already carries word1=1 — that case never excuses the
                    // arg-0 half): the live global-26 clear AND the current
                    // house file's SIMI global-26 patch must both happen.
                    Check(state1 == 1, "completion-mode0-state-1 (" + state1 + ")");
                    Check(_liveVm.GetGlobalValue(26) == 0, "completion-live-g26-cleared ("
                        + _liveVm.GetGlobalValue(26) + ")");
                    var fileG26 = ReadHouseSimiGlobal(UserPath("UserData", "Houses",
                        "House" + TutorialHouse.ToString().PadLeft(2, '0') + ".iff"), 26);
                    Check(fileG26 == 0, "completion-housefile-g26-cleared ("
                        + (fileG26?.ToString() ?? "absent") + ")");
                }
                else if (state1 == state0 + 1)
                {
                    Check(true, "completion-state-1");
                }
                else if (state1 == state0)
                {
                    Check(true, "completion-mode0-not-advanced");
                    _log("AUTOTEST uitutorial-lifecycle PENDING-ENGINE: TutorialState missing — generic "
                        + "call 0's mirror asserts (" + state0 + "->" + state1 + ") are stub-tier");
                }
                // (a crashed call already FAILED above; no tier assert here.)
            }

            var exit8 = DriveGenericCallSafe(8, caller, "completion-mode8"); // FamilyTutorialComplete
            var exit9 = DriveGenericCallSafe(9, caller, "completion-mode9"); // ArchitectureTutorialComplete
            if (exit8 != null && exit9 != null)
                Check(exit8 == VMPrimitiveExitCode.GOTO_TRUE && exit9 == VMPrimitiveExitCode.GOTO_TRUE,
                    "completion-mode8-9-exit");
            var state3 = TutorialEngine247.GetTutorialState();
            if (TutorialEngine247.HasTutorialState)
            {
                // F3: absolute writes — call 8 lands on 2, call 9 on 3. Anything
                // else with the engine present is a failure.
                Check(state3 == 3, "completion-state-3 (" + state3 + ")");
            }
            else if (state3 == state0 + 3)
                Check(true, "completion-state-3");
            else
            {
                Check(true, "completion-partial-state (" + state3 + ")");
                _log("AUTOTEST uitutorial-lifecycle PENDING-ENGINE: TutorialState missing — the "
                    + "call-8/9 mirror tier is stub-tier (" + state3 + ")");
            }
            // state 3+ permanently disables the spawner (exercised with explicit
            // staging in the isolated spawner section below).
            Advance(P_CANCEL);
        }

        private void PhaseCancel()
        {
            _frames++;
            if (_frames < 5) return;
            if (_frames == 5)
            {
                // Owner tiers: (1) the real latch (the object's own TryTutorial
                // acquired it during the load ticks), (2) the spawned Tutorial
                // object without the latch — armed via the public SetTutorialObject
                // seam, (3) any avatar. Tier logged; only (1) is fully native.
                _cancelOwner = _liveVm.Context.TutorialObject;
                _seamOwner = false;
                if (_cancelOwner == null)
                {
                    var spawned = _liveVm.Entities.FirstOrDefault(
                        x => x.Object?.OBJ?.GUID == VMTS1TutorialSpawner.TutorialGUID);
                    _cancelOwner = spawned
                        ?? (VMEntity)(_liveVm.Entities.OfType<VMAvatar>().FirstOrDefault()
                            ?? _liveVm.Entities.FirstOrDefault());
                    if (_cancelOwner == null || !_liveVm.Context.SetTutorialObject(_cancelOwner))
                    {
                        Check(false, "cancel-owner-seam");
                        ToFinish();
                        return;
                    }
                    _seamOwner = true;
                    _log("AUTOTEST uitutorial-lifecycle cancel-owner-seam entity="
                        + _cancelOwner.ObjectID
                        + (spawned != null ? " (the spawned Tutorial object, latch not yet acquired)" : "")
                        + (TutorialEngine247.HasRequestCancel ? "" : " (PENDING-ENGINE: no RequestTutorialCancel)"));
                }
                var hl = _screen.LotControl?.TutorialHighlight;
                Check(hl != null, "cancel-highlighter-mounted");
                var before = TutorialEngine247.CancelCallCount;
                // The native ESC case sits behind the lot-view key handler's
                // child-window guard; the port's stand-in skips while a modal
                // dialog is tracked. A tutorial lesson dialog may legitimately
                // be open by now — dismiss it through the production handler
                // (DialogResponse) so the ESC law is exercised.
                var blocking = _screen.LotControl?.GetType().GetField("BlockingDialog",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_screen.LotControl)
                    as Simitone.Client.UI.Controls.UIMobileDialog;
                if (blocking != null)
                {
                    _log("AUTOTEST uitutorial-lifecycle cancel-blocking-dialog-present; dismissing "
                        + "via production DialogResponse");
                    typeof(UILotControl).GetMethod("DialogResponse",
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        null, new Type[] { typeof(byte) }, null)
                        ?.Invoke(_screen.LotControl, new object[] { (byte)0 });
                }
                // Force the visible premise: the ESC law's job is the HIDE.
                if (hl != null) hl.Visible = true;
                var state = FreshState();
                state.NewKeys = new List<Keys> { Keys.Escape };
                if (TickPoller == null || _screen.LotControl == null)
                {
                    Check(false, "cancel-poller-missing");
                    ToFinish();
                    return;
                }
                TickPoller.Invoke(_screen.LotControl, new object[] { state });
                if (TutorialEngine247.HasRequestCancel)
                    Check(TutorialEngine247.CancelCallCount == before + 1, "cancel-engine-dispatched");
                else
                    _log("AUTOTEST uitutorial-lifecycle PENDING-ENGINE: RequestTutorialCancel missing — "
                        + "asserting the client-side highlight half only");
                Check(hl != null && !hl.Visible, "cancel-highlight-hidden");
                return;
            }
            // give the engine a tick to run the tree + kill
            var ownerAfter = _liveVm.Context.TutorialObject;
            if (TutorialEngine247.HasRequestCancel && !_seamOwner)
            {
                Check(ownerAfter == null, "cancel-owner-killed (" + (ownerAfter?.ObjectID ?? -1) + ")");
            }
            else
            {
                _log("AUTOTEST uitutorial-lifecycle cancel-owner-after=" + (ownerAfter?.ObjectID ?? -1)
                    + " seam=" + _seamOwner + " (kill assert engine-gated)");
            }
            if (_seamOwner) _liveVm.Context.SetTutorialObject(null);
            Advance(P_SPAWNER);
        }

        private void PhaseSpawner()
        {
            _frames++;
            if (_frames < 2) return;
            // The armed-load spawn evidence is taken on the LIVE session at
            // PhaseLoad7 (the production VMBlueprintRestoreCmd tail runs the
            // spawner; asserting here would race the cancel section's kill).
            // This phase pins the remaining guard law directly against the
            // engine's public RunHouseLoadSpawn entry on bare VMs (AutotestOBJM242
            // idiom — a headless full lot load is not supported by VMContext).
            VM.GlobTS1 = true;

            // 1) g26 == 0 -> no spawn (guard 3)
            var vmB = NewIsolatedVm();
            VMTS1TutorialSpawner.RunHouseLoadSpawn(vmB);
            Check(vmB.Entities.All(x => x.Object?.OBJ?.GUID != VMTS1TutorialSpawner.TutorialGUID),
                "spawner-g26-zero-no-spawn");

            // 2) state 3 -> no spawn even when g26 is armed (guard 2; the
            // permanent-off law). Stage the latch through the provider, then
            // restore: the armed VM's global 26 is nonzero.
            var restoredState = TutorialEngine247.GetTutorialState();
            var stateControllable = TutorialEngine247.SetTutorialState(3);
            if (stateControllable)
            {
                var vmC = NewIsolatedVm();
                vmC.SetGlobalValue(26, 1); // armed latch: only the state guard may refuse
                VMTS1TutorialSpawner.RunHouseLoadSpawn(vmC);
                Check(vmC.Entities.All(x => x.Object?.OBJ?.GUID != VMTS1TutorialSpawner.TutorialGUID),
                    "spawner-state3-no-spawn");
                TutorialEngine247.SetTutorialState(restoredState);
            }
            else
            {
                _log("AUTOTEST uitutorial-lifecycle PENDING-ENGINE: TutorialState setter missing — "
                    + "state-3 spawner assert skipped");
            }

            // 3) inhibit flag -> no spawn (guard 1; skeptic correction 1: the
            // cheat's "off" sets it, a fresh session leaves it cleared).
            var inhibitRestore = VMTS1TutorialSpawner.InhibitSpawn;
            VMTS1TutorialSpawner.InhibitSpawn = true;
            var vmD = NewIsolatedVm();
            vmD.SetGlobalValue(26, 1);
            VMTS1TutorialSpawner.RunHouseLoadSpawn(vmD);
            VMTS1TutorialSpawner.InhibitSpawn = inhibitRestore;
            Check(vmD.Entities.All(x => x.Object?.OBJ?.GUID != VMTS1TutorialSpawner.TutorialGUID),
                "spawner-inhibit-no-spawn");
            Advance(P_CHEAT);
        }

        // ------------------------------------------------------------------
        // F1: the real cheat console → the spawner inhibit law (P_CHEAT)
        // ------------------------------------------------------------------

        private static readonly MethodInfo CheatCommandEntry = typeof(UICheatTextbox).GetMethod(
            "commandEntered", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly string[] CheatStepCommand =
            { null, "tutorial off", "tutorial on", "tutorial 0", "tutorial 5" };
        private static readonly bool[] CheatStepInhibited =
            { false, true, false, true, false };
        private static readonly string[] CheatStepName =
            { null, "cheat-dispatch-off-inhibits", "cheat-dispatch-on-allows",
              "cheat-dispatch-0-inhibits", "cheat-dispatch-5-allows" };

        private void PhaseCheat()
        {
            _frames++;
            if (_cheatStep == 0)
            {
                if (_frames != 1) return; // one-shot entry
                try { _cheatBox = new UICheatTextbox(_liveVm); }
                catch (Exception ce)
                {
                    Check(false, "cheat-console-mount (" + ce.GetType().Name + ")");
                    ToFinish();
                    return;
                }
                if (_cheatBox == null || CheatCommandEntry == null)
                {
                    Check(false, "cheat-console-entry");
                    ToFinish();
                    return;
                }

                // 1) the production parse entry must know both R247 cheats and
                // carry the modifier law (on/off → Modifier, numeric → Amount;
                // restore_tut is type 0 — no parameters).
                var off = _cheatBox.parseCommandString("tutorial off");
                Check(off != null && off.CheatBehavior == VMCheatContext.VMCheatType.Tutorial
                    && off.Modifier == false, "cheat-parse-tutorial-off");
                var on = _cheatBox.parseCommandString("tutorial on");
                Check(on != null && on.CheatBehavior == VMCheatContext.VMCheatType.Tutorial
                    && on.Modifier == true, "cheat-parse-tutorial-on");
                var five = _cheatBox.parseCommandString("tutorial 5");
                Check(five != null && five.CheatBehavior == VMCheatContext.VMCheatType.Tutorial
                    && five.Amount == 5 && five.Modifier == false, "cheat-parse-tutorial-5-numeric");
                var zero = _cheatBox.parseCommandString("tutorial 0");
                Check(zero != null && zero.CheatBehavior == VMCheatContext.VMCheatType.Tutorial
                    && zero.Amount == 0 && zero.Modifier == false, "cheat-parse-tutorial-0-numeric");
                var restore = _cheatBox.parseCommandString("restore_tut");
                Check(restore != null && restore.CheatBehavior == VMCheatContext.VMCheatType.RestoreTut,
                    "cheat-parse-restore-tut");

                // 2) the dispatch law across four REAL invocations, each a
                // state flip (no vacuous pass): off→inhibited, on→allowed,
                // 0→inhibited, 5→allowed (the binary's flag = (param == 0),
                // skeptic correction 1). The real console path
                // (commandEntered → VMNetCheatCmd → ts1VM.SendCommand → the
                // VM's next tick batch) is used when the command's caller
                // resolves headlessly; otherwise the parsed VMCheatContext
                // executes directly on the live VM (the same production entry
                // the driver calls), with disclosure.
                _inhibitBaseline = VMTS1TutorialSpawner.InhibitSpawn;
                _cheatConsole = _liveVm.GetAvatarByPersist(_liveVm.MyUID) != null;
                _log("AUTOTEST uitutorial-lifecycle cheat-dispatch path="
                    + (_cheatConsole ? "console(commandEntered)" : "direct VMCheatContext.Execute")
                    + " baselineInhibit=" + _inhibitBaseline);
                _cheatStep = 1;
                _frames = 0;
                IssueCheat();
                return;
            }
            if (VMTS1TutorialSpawner.InhibitSpawn != CheatStepInhibited[_cheatStep])
            {
                if (Expired("cheat-dispatch-step" + _cheatStep, 300)) return;
                return;
            }
            Check(true, CheatStepName[_cheatStep]);
            _cheatStep++;
            if (_cheatStep > 4)
            {
                VMTS1TutorialSpawner.InhibitSpawn = _inhibitBaseline; // no static residue
                _log("AUTOTEST uitutorial-lifecycle cheat-dispatch OK (off/on/0/5 → flag=(param==0))");
                Advance(P_REFUSAL);
                return;
            }
            _frames = 0;
            IssueCheat();
        }

        private void IssueCheat()
        {
            var command = CheatStepCommand[_cheatStep];
            if (_cheatConsole)
            {
                // the production console entry: parse + VMNetCheatCmd +
                // ts1VM.SendCommand (the effect lands on a later VM tick)
                CheatCommandEntry.Invoke(_cheatBox, new object[] { command, null });
            }
            else
            {
                var context = _cheatBox.parseCommandString(command);
                if (context == null)
                {
                    Check(false, "cheat-fallback-parse-" + command);
                    ToFinish();
                    return;
                }
                context.Execute(_liveVm, _liveVm.GetAvatarByPersist(_liveVm.MyUID));
            }
        }

        private static VM NewIsolatedVm()
        {
            // Bare VM (AutotestOBJM242 idiom) with Init()'s simulator globals —
            // enough for the spawner guard law; a headless full lot load is NOT
            // (VMContext.Load needs a live world), so the armed-path spawn
            // evidence comes from the real production load in PhaseLoad7.
            var context = new VMContext(null);
            var vm = new VM(context, null, null) { TS1 = true };
            VM.GlobTS1 = true;
            vm.Init();
            return vm;
        }

        private void PhaseRefusal()
        {
            _frames++;
            if (_frames == 1)
            {
                ExitLotDeferred();
                return;
            }
            _screen = GameFacade.Screens?.CurrentUIScreen as TS1GameScreen;
            if (_screen == null || _screen.InLot || _screen.TS1NeighPanel == null)
            {
                if (Expired("refusal-boot")) return;
                return;
            }
            if (_frames < 10) return;

            // The guard-order law is engine-independent (r197/r247 §G): the
            // tutorial flag is the FIRST guard, before family/afford/occupied.
            Check(TS1GameScreen.NativeMoveInDecision(true, true, true, false, true, false, true)
                == TS1GameScreen.NativeMoveInOutcome.RefuseTutorial, "refusal-guard-order-law");
            Check(TS1GameScreen.NativeMoveInTitleIndex(TS1GameScreen.NativeMoveInOutcome.RefuseTutorial) == 16,
                "refusal-title-index-16");

            // Live dialog path — only reachable when the engine flags a house
            // (HouseInfo+0x14 = house-SIMI g58). Today's SIMI parse cannot
            // carry g58 (0x20-item version gate), so the default is the stub
            // contract: no house is flagged, no refusal dialog mounts.
            var flagged = TutorialHouse;
            if (!TutorialEngine247.HasIsTutorialHouse
                || !TutorialEngine247.IsTutorialHouse(flagged)) flagged = -1;
            if (flagged > 0)
            {
                _screen.StartMoveIn(0); // family validity is IRRELEVANT: tutorial is the first guard
                _screen.TS1NeighPanel.SelectHouse(flagged); // synchronous desktop dispatch
                var dialog = GameFacade.Screens.TopVisibleDialog as UIMobileAlert;
                Check(dialog != null
                    && dialog.TitleTextForProbe == GameFacade.Strings.GetString("132", "16")
                    && dialog.MessageTextForProbe == GameFacade.Strings.GetString("132", "17"),
                    "refusal-dialog-native-strings");
                Capture("refusal-dialog");
                dialog?.Close();
            }
            else
            {
                // Stub contract: with no flagged house the first guard lets the
                // chain continue — whatever dialog appears next must NOT be the
                // refusal (guard-order proof). The follow-up guards may mount
                // their own dialogs (afford/occupied/purchase) or exit silently.
                _screen.StartMoveIn(0);
                _screen.TS1NeighPanel.SelectHouse(ProbeHouse); // synchronous desktop dispatch
                var dialog = GameFacade.Screens.TopVisibleDialog as UIMobileAlert;
                var refused = dialog != null
                    && dialog.TitleTextForProbe == GameFacade.Strings.GetString("132", "16");
                Check(!refused, "refusal-stub-no-dialog");
                _log("AUTOTEST uitutorial-lifecycle refusal-fallback-dialog="
                    + (dialog == null ? "none (silent chain exit)" : "'" + dialog.TitleTextForProbe + "'")
                    + " (a later guard firing proves the chain walked PAST the inert tutorial guard)");
                dialog?.Close();
                _log("AUTOTEST uitutorial-lifecycle PENDING-ENGINE: IsTutorialHouse(g58) false for every "
                    + "house (the port's SIMI parse cannot stage g58) — live refusal dialog pending");
            }
            _screen.MoveInFamily = null;
            Advance(P_EVIDENCE);
        }

        private void PhaseEvidence()
        {
            _frames++;
            if (_frames != 1) return;
            try
            {
                _log("AUTOTEST uitutorial-lifecycle isolation userdir=" + FSOEnvironment.UserDir
                    + " (redirect from " + (_priorUserDir ?? "?") + ")");
                if (_gdTutorialFam == null || _gdTutorialSha == null || _gdNeighSha == null)
                {
                    Check(false, "gamedata-baseline-missing");
                    ToFinish();
                    return;
                }
                var tut = new FileInfo(_gdTutorialFam);
                Check(tut.Length == _gdTutorialLen && Sha256(_gdTutorialFam) == _gdTutorialSha,
                    "gamedata-tutorialfam-byte-identical");
                Check(File.GetLastWriteTimeUtc(_gdTutorialFam) == _gdTutorialMtime,
                    "gamedata-tutorialfam-mtime-untouched");
                var neigh = new FileInfo(_gdNeighPath);
                Check(neigh.Length == _gdNeighLen && Sha256(_gdNeighPath) == _gdNeighSha,
                    "gamedata-neighborhood-byte-identical");
                Check(File.GetLastWriteTimeUtc(_gdNeighPath) == _gdNeighMtime,
                    "gamedata-neighborhood-mtime-untouched");

                // evidence: the post-import house file + the staged FAM hash
                var audit = UserPath("ui-audit", "r247");
                Directory.CreateDirectory(audit);
                var lines = new List<string>();
                var house07 = UserPath("UserData", "Houses",
                    "House" + TutorialHouse.ToString().PadLeft(2, '0') + ".iff");
                if (File.Exists(house07))
                {
                    File.Copy(house07, Path.Combine(audit, "House07-post-import.iff"), true);
                    lines.Add(Sha256(Path.Combine(audit, "House07-post-import.iff"))
                        + "  House07-post-import.iff");
                }
                File.WriteAllLines(Path.Combine(audit, "sha256.txt"), lines);
                _log("AUTOTEST uitutorial-lifecycle evidence copied to " + audit);
            }
            catch (Exception e)
            {
                Check(false, "gamedata-integrity-exception (" + e.GetType().Name + ": " + e.Message + ")");
            }
            ToFinish();
        }
    }
}
