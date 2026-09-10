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
using FSO.Files.Utils;
using FSO.SimAntics.Utils;
using FSO.Vitaboy;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Panels.CAS;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace Simitone.Client
{
    /// <summary>
    /// R246 'ucasflow' (opt-in, focused) — the REAL-flow Create-A-Family audit.
    /// Unlike uicasorig's fixture screens, every step here runs the production
    /// path on the live game: the neighborhood MoveIn button drives
    /// GameController.EnterCAS(), the original 800x600 CAS panels take real
    /// typed input (R240 idiom) and real button presses, SaveFamily runs the
    /// REAL SimitoneNeighbourGenerator.CreateFamily writer (the site of the
    /// R246 short[5]/short[6] personality crash), and every written byte is
    /// re-parsed fresh from disk (AutotestOBJM242 idiom — never a second
    /// TS1NeighborhoodProvider, which would double-add to TS1ObjectProvider).
    ///
    /// Isolation: when ucasflow is requested, AutotestRunner.Begin redirects
    /// FSOEnvironment.UserDir into a fresh temp dir BEFORE Content.Init /
    /// TS1NeighbourProvider.InitSpecific, so the pristine UserData clone and
    /// every CAS write land there; game-data and ~/Documents/Simitone stay
    /// untouched (the game-data Neighborhood.iff is hashed at Begin and
    /// re-verified at the end). Known writes OUTSIDE the redirect, documented:
    ///   - autotest.log -> <UserDir>/autotest.log (redirected temp) AND a
    ///     /tmp/autotest.log copy (AutotestRunner.Log dual sink).
    /// CAS family diagnostics go to standard output, not the signed bundle.
    ///
    /// Determinism: frame-based waits only (no wall-clock sleeps), fresh temp
    /// dir per run, no literal GUID/skill asserts (GUIDs and skills/interests
    /// are random by design — range/presence checks only).
    /// </summary>
    internal sealed class AutotestCASFlow
    {
        // --- isolation (driven from AutotestRunner.Begin, before Content.Init) ---
        internal static string RedirectDir;
        internal static string IsolationError;
        // R252: set in BeginIsolation when the checks string names "recordfmt"
        // (the opt-in original-format assertion pass). Also drives the recordfmt
        // verdict surfacing in AutotestRunner.StateCASFlow.
        internal static bool RecordFmtEnabled;
        private static string _priorUserDir;
        private static string _gameDataNeighPath;
        private static string _gameDataNeighSha;
        private static long _gameDataNeighLen;
        private static DateTime _gameDataNeighMtime;

        /// <summary>
        /// Opt-in isolation hook. Returns true when the checks string requests
        /// ucasflow (in which case UserDir now points at a fresh temp dir).
        /// Opt-in resolution matches CheckEnabled semantics: the passed
        /// -autotest-opts string REPLACES Config.Checks, so a focused run of
        /// "-autotest-opts uicasflow" enables exactly this flow. The default
        /// Checks string never lists ucasflow, so the default suite is inert.
        /// </summary>
        internal static bool BeginIsolation(string checksCsv)
        {
            var c = checksCsv?.Trim().Trim('"');
            if (string.IsNullOrEmpty(c)) return false;
            var names = c.Split(',').Select(s => s.Trim().ToLowerInvariant()).ToList();
            // both opt-in spellings are accepted (the canonical check name is
            // ucasflow; the focused-gate flag historically reads uicasflow);
            // recordfmt piggybacks on the same CAS drive (R252).
            var enabled = names.Contains("ucasflow") || names.Contains("uicasflow") || names.Contains("recordfmt");
            RecordFmtEnabled = names.Contains("recordfmt");
            if (!enabled) return false;
            try
            {
                _priorUserDir = FSOEnvironment.UserDir;
                RedirectDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "simitone-r246-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(RedirectDir);
                FSOEnvironment.UserDir = RedirectDir;
                // game-data integrity baseline (READ-ONLY spot-check file)
                var basePath = GlobalSettings.Default.TS1HybridPath;
                if (!string.IsNullOrEmpty(basePath))
                {
                    _gameDataNeighPath = System.IO.Path.Combine(basePath, "UserData", "Neighborhood.iff");
                    if (File.Exists(_gameDataNeighPath))
                    {
                        _gameDataNeighLen = new FileInfo(_gameDataNeighPath).Length;
                        _gameDataNeighMtime = File.GetLastWriteTimeUtc(_gameDataNeighPath);
                        _gameDataNeighSha = Sha256(_gameDataNeighPath);
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

        // --- the chosen CAS record (deterministic; documented in the report) ---
        // Personality vector in DesktopCAS.Values display order
        // [Neat, Outgoing, Active, Playful, Nice]; sum = 24, so Pool = 1 > 0
        // which is what mounts the REAL unspent-points confirmation.
        internal static readonly int[] TargetVector = { 8, 2, 3, 5, 6 };
        private const string FamilyTypedRaw = "Audit:Fam0123456789012345678"; // ':' must be dropped, then capped at 24
        private const string FamilyTypedFinal = "AuditFam0123456789012345";
        private const string PersonTypedRaw = "AuditSimABCDEFGHIJKLMNOPQRST"; // capped at 25
        private const string PersonTypedFinal = "AuditSimABCDEFGHIJKLMNOPQ";
        private const string BioTyped = "R246 audit bio\nEnjoys music and painting.\nLikes meeting the neighbors.\nDreams of a house with a garden.\nKeeps a journal.\nSix paragraphs survive editing and saving.";

        // phases
        private const int P_BOOT = 0;
        private const int P_CASWAIT = 1;
        private const int P_FAMILYEDIT = 2;
        private const int P_SIMEDIT = 3;
        private const int P_TYPESWITCH = 4;
        private const int P_SUITSETTLE = 5;
        private const int P_UNSPENT_REAL = 6;
        private const int P_UNSPENT_ANSWERED = 7;
        private const int P_SEAM_EDIT = 8;
        private const int P_SEAM_ACCEPTED = 9;
        private const int P_SAVE_CONFIRM = 10;
        private const int P_SAVED = 11;
        private const int P_MOVEIN = 12;
        private const int P_DISK = 13;
        private const int P_EVIDENCE = 14;
        private const int P_FINISH = 15;

        private readonly Action<string> _log;
        private int _phase = P_BOOT;
        private int _frames;
        private int _passed, _failed;
        // R252 recordfmt (opt-in): original-format assertion counters, kept
        // separate from the ucasflow counters so the recordfmt verdict can be
        // surfaced independently in AutotestRunner.StateCASFlow.
        private int _recordFmtPassed, _recordFmtFailed;
        private bool _recordFmtCompleted;
        private readonly List<string> _recordFmtFailures = new List<string>();
        private readonly List<string> _recordFmtPasses = new List<string>();
        private readonly List<string> _failures = new List<string>();
        private readonly List<string> _passes = new List<string>();

        internal bool RecordFmtSucceeded => _recordFmtCompleted && _recordFmtPassed > 0
            && _recordFmtFailed == 0 && Failed == 0;
        internal string RecordFmtDiagnostics =>
            "passed=" + _recordFmtPassed + " failed=" + _recordFmtFailed
            + " completed=" + _recordFmtCompleted + " flowFailed=" + Failed
            + (_recordFmtFailures.Count == 0 ? "" : " FAIL [" + string.Join("; ", _recordFmtFailures) + "]");

        private TS1CASScreen _cas;
        private string _familyName, _personName;
        private CASFamilyMember _member;
        private FAMI _newFam;
        private string _newCharFile;
        private int? _movedIn;

        // baselines captured before the save
        private HashSet<int> _baselineFamiIds;
        private HashSet<uint> _baselineFamiGuids;
        private int _baselineMaxFamilyNumber;
        private int _baselineCensusCount;
        private List<(short id, uint guid)> _baselineNb;
        private HashSet<string> _baselineInventory;
        private List<string> _baselineCharFiles;
        private Dictionary<int, string> _templateStr;

        public int Failed => _failed;
        public string Diagnostics =>
            "passed=" + _passed + " failed=" + _failed + " phase=" + _phase
            + (_failures.Count == 0 ? "" : " FAIL [" + string.Join("; ", _failures) + "]");

        public AutotestCASFlow(Action<string> log)
        {
            _log = log ?? (s => { });
        }

        // ------------------------------------------------------------------
        // drivers
        // ------------------------------------------------------------------

        /// <summary>One game-thread tick. Returns true when the flow completed.</summary>
        public bool Tick()
        {
            if (_phase == P_FINISH) return true;
            try
            {
                switch (_phase)
                {
                    case P_BOOT: PhaseBoot(); break;
                    case P_CASWAIT: PhaseCasWait(); break;
                    case P_FAMILYEDIT: PhaseFamilyEdit(); break;
                    case P_SIMEDIT: PhaseSimEdit(); break;
                    case P_TYPESWITCH: PhaseTypeSwitch(); break;
                    case P_SUITSETTLE: PhaseSuitSettle(); break;
                    case P_UNSPENT_REAL: PhaseUnspentReal(); break;
                    case P_UNSPENT_ANSWERED: PhaseUnspentAnswered(); break;
                    case P_SEAM_EDIT: PhaseSeamEdit(); break;
                    case P_SEAM_ACCEPTED: PhaseSeamAccepted(); break;
                    case P_SAVE_CONFIRM: PhaseSaveConfirm(); break;
                    case P_SAVED: PhaseSaved(); break;
                    case P_MOVEIN: PhaseMoveIn(); break;
                    case P_DISK: PhaseDisk(); break;
                    case P_EVIDENCE: PhaseEvidence(); break;
                }
            }
            catch (Exception e)
            {
                Check(false, "phase" + _phase + "-exception");
                _log("AUTOTEST uicasflow phase" + _phase + " EXC " + e.GetType().Name + ": " + e.Message
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

        /// <summary>True when the current phase blew its frame budget
        /// (default 60s; boot allows 5 min).</summary>
        private bool Expired(string what, int limit = 3600)
        {
            if (_frames <= limit) return false;
            Check(false, what + "-frames-timeout");
            ToFinish();
            return true;
        }

        private void Check(bool ok, string name)
        {
            if (ok) { _passed++; _passes.Add(name); }
            else { _failed++; _failures.Add(name); }
            _log("AUTOTEST uicasflow " + name + ": " + (ok ? "PASS" : "FAIL"));
        }

        /// <summary>recordfmt (R252) assertion — counted separately from the
        /// ucasflow checks so the recordfmt verdict surfaces independently.</summary>
        private void CheckFmt(bool ok, string name)
        {
            if (ok) { _recordFmtPassed++; _recordFmtPasses.Add(name); }
            else { _recordFmtFailed++; _recordFmtFailures.Add(name); }
            _log("AUTOTEST recordfmt " + name + ": " + (ok ? "PASS" : "FAIL"));
        }

        // ------------------------------------------------------------------
        // input idioms (R239/R240 ports — production handlers only)
        // ------------------------------------------------------------------

        private static readonly MethodInfo MouseEvent = typeof(UIButton).GetMethod("OnMouseEvent",
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

        /// <summary>Real UIButton press via the OnMouseEvent reflection idiom
        /// (AutotestCAS240.cs:70-76).</summary>
        private void Press(UIButton button)
        {
            var state = FreshState();
            MouseEvent.Invoke(button, new object[] { UIMouseEventType.MouseDown, state });
            MouseEvent.Invoke(button, new object[] { UIMouseEventType.MouseUp, state });
        }

        /// <summary>Real cTSSystemButton press through its own public mouse
        /// handler (commands fire on mouse-up-inside, R183 law).</summary>
        private void Press(UIOriginalSystemButton button)
        {
            var state = FreshState();
            button.HandleMouseEvent(UIMouseEventType.MouseOver, state);
            button.HandleMouseEvent(UIMouseEventType.MouseDown, state);
            button.HandleMouseEvent(UIMouseEventType.MouseUp, state);
        }

        /// <summary>Real typed input through the focused field (R240 idiom:
        /// private InputManager, printable text events and Return key events).</summary>
        private void Type<T>(T field, string text) where T : UIElement, IFocusableUI
        {
            var input = FreshState();
            input.InputManager.SetFocus(field);
            // SDL delivers Return as a key event, never as printable text input.
            var paragraphs = text.Split('\n');
            for (int i = 0; i < paragraphs.Length; i++)
            {
                if (i > 0)
                {
                    input.FrameTextInput = new List<char>();
                    input.KeyboardState = new KeyboardState(Keys.Enter);
                    input.NewKeys.Add(Keys.Enter);
                    field.Update(input);
                    input.KeyboardState = new KeyboardState();
                    input.NewKeys.Clear();
                }
                input.FrameTextInput = paragraphs[i].ToList();
                field.Update(input);
            }
            input.InputManager.SetFocus(null);
        }

        private void TypeInitiallyFocused(UITextBox field, string text)
        {
            var input = FreshState();
            input.InputManager = GameFacade.LastUpdateState.InputManager;
            Check(input.InputManager.GetFocus() == field, "dialog-name-focus-before-typing windowFocused="
                + GameFacade.LastUpdateState.WindowFocused + " actual=" + input.InputManager.GetFocus()?.GetType().Name);
            Capture(field == _cas.DesktopFamily.FamilyNameBox ? "02-family-initial-focus" : "03-person-initial-focus");
            input.FrameTextInput = text.ToList();
            field.Update(input);
            input.InputManager.SetFocus(null);
        }

        /// <summary>Real personality LED-zone click: dispatch the strip's own
        /// ListenForMouse handler with a synthetic mouse X on the plus/minus
        /// side of the value*5 engine boundary (cas-layout-law §3.4).</summary>
        private bool LedClick(int row, bool plus)
        {
            var refs = typeof(UIElement).GetField("m_MouseRefs", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(_cas.DesktopCAS.LedStrips[row]) as List<UIMouseEventRef>;
            if (refs == null || refs.Count == 0) return false;
            var state = FreshState();
            state.MouseState = new MouseState(plus ? 10000 : -10000, 0, 0,
                ButtonState.Pressed, ButtonState.Released, ButtonState.Released,
                ButtonState.Released, ButtonState.Released);
            refs[0].Callback(UIMouseEventType.MouseDown, state);
            return true;
        }

        /// <summary>Answer a mounted UIMobileAlert through its real buttons
        /// (options order: YesNo -> [0] = Yes).</summary>
        private void PressAlertButton(UIMobileAlert dialog, int index)
        {
            var buttons = typeof(UIMobileAlert).GetField("Buttons", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(dialog) as List<UIButton>;
            if (buttons == null || index >= buttons.Count) { Check(false, "alert-buttons-missing"); ToFinish(); return; }
            Press(buttons[index]);
        }

        /// <summary>GPU capture (SurveyDump recipe) into ui-audit/r246.</summary>
        private void Capture(string tag)
        {
            try
            {
                var ui = GameFacade.Screens;
                var gd = GameFacade.GraphicsDevice;
                if (gd == null) { _log("AUTOTEST uicasflow capture " + tag + ": no device"); return; }
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
                    // warm-up pass: the first explicit draw after an RT switch
                    // paints cached containers (SurveyDump law)
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
                    // the CAS world is a 3D scene beside the UI layer
                    try { GameFacade.Scenes.PreDraw(gd); } catch (Exception we) { _log("AUTOTEST uicasflow capture " + tag + " world-predraw EXC " + we.Message); }
                    gd.SetRenderTarget(rt);
                    gd.Clear(new Color(0x72, 0x72, 0x72, 0xFF));
                    try { GameFacade.Scenes.Draw(gd); } catch (Exception we) { _log("AUTOTEST uicasflow capture " + tag + " world-draw EXC " + we.Message); }
                    ui.SpriteBatch.UIBegin(BlendState.AlphaBlend, SpriteSortMode.Immediate);
                    try { ui.Draw(ui.SpriteBatch); } finally { ui.SpriteBatch.End(); }
                    gd.SetRenderTarget(null);
                    var dir = System.IO.Path.Combine(FSOEnvironment.UserDir, "ui-audit", "r246");
                    Directory.CreateDirectory(dir);
                    var path = System.IO.Path.Combine(dir, "casflow-" + tag + ".png");
                    using (var fs = File.Create(path))
                        rt.SaveAsPng(fs, rt.Width, rt.Height);
                    _log("AUTOTEST uicasflow capture " + tag + " " + w + "x" + h + " -> " + path);
                }
            }
            catch (Exception e)
            {
                Check(false, "capture-" + tag + "-exception");
                _log("AUTOTEST uicasflow capture EXC " + tag + " " + e.GetType().Name + ": " + e.Message);
            }
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
            var screen = GameFacade.Screens?.CurrentUIScreen as TS1GameScreen;
            if (screen == null || screen.InLot || screen.TS1NeighSwitcher == null)
            {
                if (Expired("boot", 18000)) return; // first-run UserData clone can be slow
                return;
            }
            // drive the REAL MoveIn navbar button (EngineLaw slot 0) — its
            // click handler is GameController.EnterCAS() production code
            var switcher = screen.TS1NeighSwitcher as UINeighbourhoodSwitcher;
            var moveIn = switcher?.Buttons.FirstOrDefault(b => b.Member != null && b.Member.EndsWith("MoveIn.bmp"));
            Check(switcher != null && moveIn != null, "neigh-switcher-mount");
            if (moveIn == null) { ToFinish(); return; }
            Press(moveIn);
            _log("AUTOTEST uicasflow production MoveIn pressed (GameController.EnterCAS)");
            Advance(P_CASWAIT);
        }

        private void PhaseCasWait()
        {
            _frames++;
            var cas = GameFacade.Screens?.CurrentUIScreen as TS1CASScreen;
            if (cas == null || !cas.Initialized || cas.CurrentMode != UICASMode.FamilySelect)
            {
                if (Expired("cas-boot")) return;
                return;
            }
            if (_frames < 70) return; // let the first SetMode tween + census settle
            _cas = cas;
            Check(cas.Original, "cas-desktop-original");
            Check(cas.vm != null, "cas-real-vm");
            SnapshotBaseline();
            Capture("01-pickafamily-initial");
            Press(cas.DesktopFamilies.AddButton); // OnNewFamily -> SetMode(FamilyEdit)
            Advance(P_FAMILYEDIT);
        }

        private bool _familyTyped;
        private int _familyTypedFrame = -1;
        private bool _familyEntryVisible, _personEntryVisible;

        private void PhaseFamilyEdit()
        {
            _frames++;
            if (_cas.CurrentMode != UICASMode.FamilyEdit || !_cas.DesktopFamily.Visible)
            {
                if (Expired("family-edit")) return;
                return;
            }
            // Tween changes visibility after the screen's Update. Allow its
            // next normal Update to deliver queued focus before injecting keys.
            if (!_familyEntryVisible) { _familyEntryVisible = true; return; }
            if (!_familyTyped)
            {
                // Visible can flip mid-tween (value > -0.5), so this runs at
                // whatever frame visibility settles — never assume a fixed one.
                _familyTyped = true;
                _familyTypedFrame = _frames;
                var box = _cas.DesktopFamily.FamilyNameBox;
                Check(box is UIOriginalFamilyNameBox, "family-name-real-field");
                TypeInitiallyFocused(box, FamilyTypedRaw);
                var text = box.CurrentText;
                _familyName = text;
                Check(!text.Contains(":"), "family-name-forbidden-dropped");
                Check(text.Length == 24, "family-name-cap24 (len=" + text.Length + ")");
                Check(text == FamilyTypedFinal, "family-name-exact");
                _log("AUTOTEST uicasflow family-name=" + text);
                Capture("02-familyedit-typed");
                return;
            }
            // AddBtn.Disabled is re-evaluated by the screen's own Update every
            // frame (name length gate) — let real frames pass after typing or
            // the press lands on a still-disabled button.
            if (_frames < _familyTypedFrame + 4) return;
            Press(_cas.DesktopFamily.AddBtn); // ModifySim(false,-1) -> SimEdit
            _log("AUTOTEST uicasflow post-addbtn mode=" + _cas.CurrentMode
                + " casVisible=" + _cas.DesktopCAS.Visible
                + " famVisible=" + _cas.DesktopFamily.Visible
                + " interp=" + _cas.FamilySimInterp);
            Advance(P_SIMEDIT);
        }

        private void PhaseSimEdit()
        {
            _frames++;
            if (_cas.CurrentMode != UICASMode.SimEdit || !_cas.DesktopCAS.Visible)
            {
                if (Expired("sim-edit")) return;
                return;
            }
            if (!_personEntryVisible) { _personEntryVisible = true; return; }
            var nameBox = _cas.DesktopCAS.NameBox;
            TypeInitiallyFocused(nameBox, PersonTypedRaw);
            _personName = nameBox.CurrentText;
            Check(_personName == PersonTypedFinal, "person-name-cap25 (len=" + _personName.Length + ")");
            Type(_cas.DesktopCAS.BioEdit, BioTyped);
            Check(_cas.DesktopCAS.BioEdit.Text == BioTyped, "bio-typed");
            Advance(P_TYPESWITCH);
        }

        private void CheckPreviewSurface()
        {
            var surface = _cas.DesktopCAS.VitaSurface;
            Check(surface != null && surface.Visible && surface.Person == _cas.VitaPreview,
                "preview-surface-bound");
            if (surface == null) return;
            var gd = GameFacade.GraphicsDevice;
            var viewport = gd.Viewport;
            var avatar = _cas.VitaPreview.Avatar;
            Check(!_cas.VitaPreview.WorldUI.Visible, "preview-staging-avatar-hidden-from-lot");
            var lights = avatar.LightPositions;
            var position = _cas.VitaPreview.VisualPosition;
            surface.RenderAvatar();
            var first = new Color[100 * 220];
            surface.RenderedAvatar.GetData(first);
            int left = 100, right = -1, top = 220, bottom = -1, count = 0;
            for (int y = 0; y < 220; y++)
                for (int x = 0; x < 100; x++)
                    if (first[y * 100 + x].A > 10)
                    {
                        count++;
                        left = Math.Min(left, x); right = Math.Max(right, x);
                        top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                    }
            Check(count > 500 && left > 0 && right < 99 && top > 0 && bottom < 219,
                "preview-avatar-unclipped pixels=" + count + " bounds=" + left + "," + top + "-" + right + "," + bottom);
            try
            {
                gd.Viewport = new Viewport(0, 0, viewport.Width == 800 ? 1024 : 800, 600);
                var alternate = gd.Viewport;
                surface.RenderAvatar();
                var second = new Color[first.Length];
                surface.RenderedAvatar.GetData(second);
                Check(first.SequenceEqual(second), "preview-projection-independent-of-host-viewport");
                Check(gd.Viewport.Equals(alternate), "preview-restores-host-viewport");
            }
            finally { gd.Viewport = viewport; }
            Check(ReferenceEquals(lights, avatar.LightPositions)
                && _cas.VitaPreview.VisualPosition == position, "preview-preserves-lot-avatar-state");
        }

        private void PhaseTypeSwitch()
        {
            _frames++;
            if (_frames < 6) return; // preview frames with typed state before mutations
            if (_frames == 6)
            {
                CheckPreviewSurface();
                Capture("03-characteredit-preview");
                Press(_cas.DesktopCAS.ChildBtn);
                return;
            }
            if (_frames < 12) return;
            if (_frames == 12)
            {
                Check(_cas.CurrentCode == "mc", "preview-child-type");
                var bio = _cas.DesktopCAS.BioEdit;
                Check(bio.Text == BioTyped && bio.Visible && bio.Opacity == 1
                    && bio.FirstVisibleLine < bio.LineCount, "child-switch-preserves-biography text="
                    + bio.Text.Length + " firstLine=" + bio.FirstVisibleLine + " lines=" + bio.LineCount);
                CheckPreviewSurface();
                Capture("03-child-preview");
                Press(_cas.DesktopCAS.AdultBtn);
                return;
            }
            if (_frames < 18) return;
            Check(_cas.VitaPreview != null && _cas.VitaPreview.VisualPosition == new Vector3(30, 58 / 3f, 0),
                "desktop-preview-position-survives-update");
            Check(_cas.BodyAvatars.Skip(1).All(a => a.VisualPosition.X == 9999)
                && _cas.HeadAvatars.All(a => a.VisualPosition.X == 9999),
                "desktop-mobile-carousel-stays-hidden");
            Check(_cas.CurrentCode == "ma", "preview-adult-type-restored");
            // real gender/skin round trips exercise the four-type suit memory
            // (R143 native law); then cycle Head/Body once each
            Press(_cas.DesktopCAS.FemaleBtn);
            Press(_cas.DesktopCAS.MaleBtn);
            Press(_cas.DesktopCAS.MediumBtn);
            Press(_cas.DesktopCAS.LightBtn);
            Press(_cas.DesktopCAS.HeadNextBtn);
            Press(_cas.DesktopCAS.BodyNextBtn);
            Advance(P_SUITSETTLE);
        }

        private void PhaseSuitSettle()
        {
            _frames++;
            if (_frames < 10) return;
            Check(_cas.DesktopCAS.AType == "ma" && _cas.DesktopCAS.SkinType == "lgt", "type-skin-restored");
            Check(_cas.CurrentCode == "ma" && _cas.CurrentSkin == "lgt", "screen-code-skin-restored");
            Capture("04-preview-suitmemory");
            // personality allocation through the REAL LED zones
            for (int i = 0; i < 5; i++)
            {
                while (_cas.DesktopCAS.Values[i] < TargetVector[i])
                {
                    if (!LedClick(i, true)) { Check(false, "led-zone-missing-row" + i); ToFinish(); return; }
                }
            }
            Check(TargetVector.Sum() == 24, "vector-sum-24");
            Check(_cas.DesktopCAS.Pool == 1, "pool-one-left (pool=" + _cas.DesktopCAS.Pool + ")");
            var zodiac = _cas.DesktopCAS.ComputeZodiac();
            var expected = ArgminZodiac(TargetVector);
            Check(zodiac == expected, "zodiac-nearest-archetype (" + zodiac + " vs " + expected + ")");
            _log("AUTOTEST uicasflow zodiac=" + UIOriginalDesignChar.ZodiacName(zodiac)
                + " vector=[Neat=" + TargetVector[0] + ",Outgoing=" + TargetVector[1]
                + ",Active=" + TargetVector[2] + ",Playful=" + TargetVector[3]
                + ",Nice=" + TargetVector[4] + "]");
            Advance(P_UNSPENT_REAL);
        }

        /// <summary>Independent nearest-archetype recomputation (squared
        /// Euclidean) over the decoded engine table.</summary>
        private static int ArgminZodiac(int[] values)
        {
            int best = 11;
            float bestD = float.MaxValue;
            for (int s = 0; s < UIOriginalDesignChar.ZodiacArchetypes.Length; s++)
            {
                float d = 0;
                for (int t = 0; t < 5; t++)
                {
                    var diff = values[t] - UIOriginalDesignChar.ZodiacArchetypes[s][t];
                    d += diff * diff;
                }
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        private void PhaseUnspentReal()
        {
            _frames++;
            if (_frames == 1)
            {
                Press(_cas.DesktopCAS.DoneBtn); // Pool>0 -> real ShowConfirmation, NO seam
                return;
            }
            if (_frames < 5) return;
            var dialog = _cas.ConfirmDialog;
            Check(dialog != null && _cas.ConfirmationPending, "unspent-real-alert-mounts");
            if (dialog == null) { ToFinish(); return; }
            Check(dialog.TitleTextForProbe == GameFacade.Strings.GetString("130", "13"), "unspent-real-alert-title");
            Capture("05-unspent-alert");
            PressAlertButton(dialog, 0); // real Yes button
            Advance(P_UNSPENT_ANSWERED);
        }

        private void PhaseUnspentAnswered()
        {
            _frames++;
            if (_cas.ConfirmationPending || _cas.CurrentMode != UICASMode.FamilyEdit)
            {
                if (Expired("unspent-answer")) return;
                return;
            }
            if (_frames < 70) return; // SetMode tween + UpdateFamilySlots settle
            Check(_cas.WIPFamily.Count == 1, "wip-one-member-after-accept");
            // re-enter SimEdit through the real member-edit button
            _cas.DesktopFamily.SelectMember(0);
            Press(_cas.DesktopFamily.EditBtn);
            Advance(P_SEAM_EDIT);
        }

        private void PhaseSeamEdit()
        {
            _frames++;
            if (_cas.CurrentMode != UICASMode.SimEdit || !_cas.DesktopCAS.Visible)
            {
                if (Expired("re-edit")) return;
                return;
            }
            if (_frames < 20) return;
            // R239 seam for determinism: same ShowConfirmation, recorded answers
            string title = null, message = null;
            Action yes = null;
            _cas.ConfirmationPresenter = (t, m, y, n) => { title = t; message = m; yes = y; };
            Press(_cas.DesktopCAS.DoneBtn);
            Check(_cas.ConfirmationPending, "unspent-seam-pending");
            Check(title == GameFacade.Strings.GetString("130", "13"), "unspent-seam-title");
            Check(message == GameFacade.Strings.GetString("130", "14", new[] { _cas.DesktopCAS.NameBox.CurrentText }),
                "unspent-seam-message");
            yes?.Invoke(); // accepted: AcceptMember + SetMode(FamilyEdit)
            Advance(P_SEAM_ACCEPTED);
        }

        private void PhaseSeamAccepted()
        {
            _frames++;
            if (_cas.CurrentMode != UICASMode.FamilyEdit)
            {
                if (Expired("seam-accept")) return;
                return;
            }
            if (_frames < 70) return;
            _cas.ConfirmationPresenter = null;
            var member = _cas.BuildMember();
            var wip = _cas.WIPFamily.Count > 0 ? _cas.WIPFamily[0] : null;
            Check(wip != null, "wip-member-present");
            if (wip != null)
            {
                _personName = wip.Name;
                Check(wip.Name == member.Name && wip.Name == PersonTypedFinal && wip.Name.Length == 25,
                    "wip-name (len=" + wip.Name.Length + ")");
                Check(wip.Bio == BioTyped, "wip-bio");
                Check(wip.Gender == 0, "wip-gender-adult-male");
                Check(wip.SkinColor == "lgt", "wip-skin");
                Check(_cas.ActiveHeads != null && _cas.ActiveBodies != null
                    && _cas.ActiveHeads.Contains(wip.Head) && _cas.ActiveBodies.Contains(wip.Body),
                    "wip-head-body-in-collections");
                Check(!string.IsNullOrEmpty(wip.HeadTex) && !string.IsNullOrEmpty(wip.BodyTex)
                    && !string.IsNullOrEmpty(wip.HandgroupTex), "wip-textures");
                // the UI vector stays 5-slot in DesktopCAS display order, x100
                var expectedUi = TargetVector.Select(v => (short)(v * 100)).ToArray();
                Check(wip.Personality.Length == 5 && wip.Personality.SequenceEqual(expectedUi),
                    "wip-personality-ui-order");
                Check(member.Head == wip.Head && member.Body == wip.Body, "buildmember-matches-wip");
            }
            _member = member;
            Advance(P_SAVE_CONFIRM);
        }

        private void PhaseSaveConfirm()
        {
            _frames++;
            if (_frames != 1) return;
            // move-in boundary seam installed BEFORE the transition (R239/R246
            // precedent, TS1CASScreen.cs NeighborhoodTransition)
            _movedIn = null;
            _cas.NeighborhoodTransition = family => { _movedIn = family; };
            string title = null, message = null;
            Action yes = null;
            _cas.ConfirmationPresenter = (t, m, yOk, n) => { title = t; message = m; yes = yOk; };
            Press(_cas.DesktopFamily.DoneBtn); // Accept(FamilyEdit) -> save confirm
            Check(title == GameFacade.Strings.GetString("129", "13"), "save-confirm-title");
            Check(message == GameFacade.Strings.GetString("129", "14"), "save-confirm-message");
            // REAL SaveFamily path — no FamilySaveWriter seam. This is where
            // the pre-fix personality bug threw.
            _log("AUTOTEST uicasflow invoking real SaveFamily (SimitoneNeighbourGenerator.CreateFamily)");
            yes?.Invoke();
            _cas.ConfirmationPresenter = null;
            Advance(P_SAVED);
        }

        private void PhaseSaved()
        {
            _frames++;
            if (_cas.CurrentMode != UICASMode.FamilySelect)
            {
                if (Expired("save")) return;
                return;
            }
            if (_frames < 70) return; // SetFamilies + card rebuild settle
            var fams = _cas.DesktopFamilies.Families;
            Check(fams.Count == _baselineCensusCount + 1, "census-plus-one (" + fams.Count + " vs " + _baselineCensusCount + ")");
            var newFam = fams.FirstOrDefault(f => !_baselineFamiIds.Contains(f.ChunkID));
            Check(newFam != null, "new-family-card-in-census");
            if (newFam == null) { ToFinish(); return; }
            _newFam = newFam;
            var famsName = newFam.ChunkParent?.Get<FAMs>(newFam.ChunkID)?.GetString(0);
            Check(famsName == _familyName, "new-card-fams-name (" + (famsName ?? "null") + ")");
            Capture("06-pickafamily-newcard");
            Advance(P_MOVEIN);
        }

        private void PhaseMoveIn()
        {
            _frames++;
            if (_frames != 1) return;
            var idx = _cas.DesktopFamilies.Families.IndexOf(_newFam);
            _cas.DesktopFamilies.SetSelection(idx);
            Check(_cas.DesktopFamilies.GetSelection() == idx, "new-card-selected");
            Press(_cas.DesktopFamilies.MoveInButton); // Accept(FamilySelect) -> SetMode(ToNeighborhood) -> seam
            Check(_movedIn == _newFam.ChunkID, "movein-seam-chunkid (" + _movedIn + ")");
            Check(_cas.CurrentMode == UICASMode.FamilySelect, "movein-boundary-no-lot-load");
            Advance(P_DISK);
        }

        private void SnapshotBaseline()
        {
            var neigh = Content.Get().Neighborhood;
            var allFami = neigh.MainResource.List<FAMI>() ?? new List<FAMI>();
            _baselineFamiIds = allFami.Select(f => (int)f.ChunkID).ToHashSet();
            _baselineFamiGuids = allFami.SelectMany(f => f.FamilyGUIDs ?? new uint[0]).ToHashSet();
            _baselineMaxFamilyNumber = allFami.Count > 0 ? allFami.Max(f => f.FamilyNumber) : 0;
            _baselineCensusCount = _cas.DesktopFamilies.Families.Count;
            // The NBRS writer's source of truth is NeighbourByID (the validly
            // decoded entries keyed by id). Entries additionally holds the
            // malformed decodes of the pristine Maxis chunk that the existing
            // writer already drops (it writes Entries.Count as the declared
            // count but iterates NeighbourByID — a pre-existing round-trip
            // residual, documented, not fixed this round).
            _baselineNb = neigh.Neighbors.NeighbourByID.Values
                .Select(n => (n.NeighbourID, n.GUID)).ToList();
            _log("AUTOTEST uicasflow nbrs-writer-residual: Entries=" + neigh.Neighbors.Entries.Count
                + " NeighbourByID=" + neigh.Neighbors.NeighbourByID.Count
                + " (the delta is malformed pristine-file decodes the existing NBRS writer drops)");
            _baselineInventory = neigh.MainResource.ListAll()
                .Select(c => c.ChunkType + ":" + c.ChunkID).ToHashSet();
            _baselineCharFiles = Directory.GetFiles(
                    System.IO.Path.Combine(FSOEnvironment.UserDir, "UserData", "Characters"), "User*.iff")
                .Select(System.IO.Path.GetFileName).ToHashSet().ToList();
            // TemplatePerson STR#200 baseline BEFORE the save mutates it in memory
            _templateStr = new Dictionary<int, string>();
            try
            {
                var tpl = Content.Get().WorldObjects.Get(SimitoneNeighbourGenerator.TEMPLATE_GUID);
                var tplStr = tpl?.Resource?.MainIff?.Get<STR>(200);
                if (tplStr != null)
                    for (int i = 0; i < tplStr.Length; i++)
                        _templateStr[i] = tplStr.GetString(i) ?? "";
            }
            catch (Exception e)
            {
                _log("AUTOTEST uicasflow template-str-baseline EXC " + e.GetType().Name + ": " + e.Message);
            }
            _log("AUTOTEST uicasflow baseline: famiIds=[" + string.Join(",", _baselineFamiIds.OrderBy(x => x))
                + "] maxFamilyNumber=" + _baselineMaxFamilyNumber
                + " census=" + _baselineCensusCount
                + " nbEntries=" + _baselineNb.Count
                + " chunks=" + _baselineInventory.Count
                + " charFiles=" + _baselineCharFiles.Count
                + " templateStrSlots=" + _templateStr.Count
                + " userdir=" + FSOEnvironment.UserDir);
        }

        private void PhaseDisk()
        {
            _frames++;
            var userDir = FSOEnvironment.UserDir;
            var neighPath = System.IO.Path.Combine(userDir, "UserData", "Neighborhood.iff");
            Check(File.Exists(neighPath), "neighborhood-written");
            if (!File.Exists(neighPath)) { ToFinish(); return; }

            // fresh parses only — never a second TS1NeighborhoodProvider
            // fresh parse with RetainChunkData so the raw chunk bytes stay in
            // chunk.OriginalData (the reader deliberately nulls ChunkData after
            // parsing; recordfmt re-scans the raw bytes from OriginalData).
            var iff = new IffFile(neighPath, true);
            var inventory = iff.ListAll().Select(c => c.ChunkType + ":" + c.ChunkID).ToHashSet();
            var unknownNew = inventory.Where(i => !_baselineInventory.Contains(i)).ToList();
            // rsmp is the derived resource-map chunk: the existing IffFile
            // writer never emits it and readers walk chunks sequentially, so
            // its absence after a save is a documented writer residual.
            var rsmpDropped = _baselineInventory.Where(i => i.StartsWith("rsmp:")).ToList();
            var preservedBaseline = _baselineInventory.Where(i => !i.StartsWith("rsmp:"));
            Check(preservedBaseline.All(inventory.Contains), "preexisting-chunks-survive");
            _log("AUTOTEST uicasflow rsmp-dropped-by-writer=[" + string.Join(",", rsmpDropped)
                + "] (derived resource map; documented residual, not fixed this round)");
            Check(iff.List<NGBH>()?.FirstOrDefault() != null, "ngbh-parses");
            Check(iff.List<TATT>()?.FirstOrDefault() != null, "tatt-parses");
            var fami0 = iff.Get<FAMI>(0);
            var fams0 = iff.Get<FAMs>(0);
            Check(fami0 != null && fams0 != null && fams0.GetString(0) == "Default", "default-family-survives");
            _log("AUTOTEST uicasflow new-chunks=[" + string.Join(",", unknownNew) + "] (expect the new FAMI/FAMs pair only)");

            var allFami = iff.List<FAMI>() ?? new List<FAMI>();
            var newOnes = allFami.Where(f => !_baselineFamiIds.Contains(f.ChunkID)).ToList();
            Check(newOnes.Count == 1, "exactly-one-new-fami");
            var newFami = newOnes.FirstOrDefault();
            ushort newId = 0;
            if (newFami != null)
            {
                newId = newFami.ChunkID;
                Check(newFami.ChunkID == _newFam.ChunkID, "fami-id-matches-census");
                Check(newFami.HouseNumber == 0, "fami-house-zero");
                Check(newFami.FamilyNumber == _baselineMaxFamilyNumber + 1,
                    "fami-number-max-plus-one (" + newFami.FamilyNumber + " vs " + _baselineMaxFamilyNumber + ")");
                Check(newFami.Budget == 20000, "fami-budget-20000");
                Check(newFami.Unknown == 1, "fami-unknown-1 (SAV-02: original writes 1 for a created+moved-in family; r252 decode.md §3)");
                Check(newFami.FamilyGUIDs != null && newFami.FamilyGUIDs.Length == 1, "fami-guid-count-1");
                if (newFami.FamilyGUIDs != null && newFami.FamilyGUIDs.Length > 0)
                {
                    var g = newFami.FamilyGUIDs[0];
                    Check(g != 0 && !_baselineFamiGuids.Contains(g), "fami-guid-unique");
                }
                var newFams = iff.Get<FAMs>(newId);
                Check(newFams != null && newFams.GetString(0) == _familyName, "fams-lang1-name");
            }

            // R252: the new character file + its lowercase name stem. Discovered
            // BEFORE the NBRS member checks because the NBRS record Name is the
            // character-file stem (e.g. "user00024"), and reused by the recordfmt
            // raw assertions and the char-file checks below.
            var charDir = System.IO.Path.Combine(userDir, "UserData", "Characters");
            var newFiles = Directory.GetFiles(charDir, "User*.iff")
                .Select(System.IO.Path.GetFileName).Except(_baselineCharFiles).ToList();
            var charStem = newFiles.Count == 1
                ? System.IO.Path.GetFileNameWithoutExtension(newFiles[0]).ToLowerInvariant() : null;

            var nbrs = iff.List<NBRS>()?.FirstOrDefault();
            Check(nbrs != null, "nbrs-parses");
            // Hoisted so the recordfmt (R252) block below can re-check them.
            Neighbour member = null;
            short[] pd = null;
            uint famGuid = 0;
            if (nbrs != null)
            {
                var entries = nbrs.Entries;
                Check(_baselineNb.All(b => entries.Any(e => e.NeighbourID == b.Item1 && e.GUID == b.Item2)),
                    "nbrs-preexisting-preserved");
                famGuid = newFami?.FamilyGUIDs is uint[] guids && guids.Length > 0 ? guids[0] : 0u;
                member = entries.FirstOrDefault(e => famGuid != 0 && e.GUID == famGuid);
                Check(member != null, "nbrs-member-present");
                pd = member?.PersonData;
                if (member != null)
                {
                    var usedByOthers = entries.Where(e => e != member).Select(e => e.NeighbourID).ToHashSet();
                    var lowestFree = (short)1;
                    while (usedByOthers.Contains(lowestFree)) lowestFree++;
                    Check(member.NeighbourID == lowestFree, "nbrs-lowest-free-id (" + member.NeighbourID + ")");
                    Check(member.Version == 0x4, "nbrs-record-version-4 (" + member.Version + ")");
                    Check(member.PersonMode == 5, "personmode-5-original (" + member.PersonMode + ")");
                    Check(member.Name == charStem, "nbrs-name-stem (" + (member.Name ?? "null") + " vs " + (charStem ?? "null") + ")");
                    Check(pd != null && pd.Length >= 88, "persondata-88 (len=" + (pd?.Length ?? -1) + ")");
                    if (pd != null && pd.Length >= 88)
                    {
                        // THE R246 regression pin: the canon mapping of the chosen
                        // sliders (pd[2..7] = Nice/Active/Generous/Playful/Outgoing/Neat, x100).
                        Check(pd[2] == TargetVector[4] * 100, "pd2-nice-x100 (" + pd[2] + ")");
                        Check(pd[3] == TargetVector[2] * 100, "pd3-active-x100 (" + pd[3] + ")");
                        Check(pd[4] == 0, "pd4-generous-zero (" + pd[4] + ")");
                        Check(pd[5] == TargetVector[3] * 100, "pd5-playful-x100 (" + pd[5] + ")");
                        Check(pd[6] == TargetVector[1] * 100, "pd6-outgoing-x100 (" + pd[6] + ")");
                        Check(pd[7] == TargetVector[0] * 100, "pd7-neat-x100 (" + pd[7] + ")");
                        Check(pd[58] == 27, "pd58-age-27-adult (" + pd[58] + ")");
                        Check(pd[60] == 1, "pd60-skin-original-encoding (lgt=1; " + pd[60] + ")");
                        Check(pd[61] == (short)newId, "pd61-family-chunkid (" + pd[61] + ")");
                        Check(pd[65] == 0, "pd65-gender-male (" + pd[65] + ")");
                        Check(pd[56] == -1, "pd56-job-type-minus1");
                        Check(pd[57] == 4, "pd57-job-level-4");
                        Check(pd[36] == 50, "pd36-autonomy-50");
                        Check(pd[33] == 25, "pd33-priority-25");
                        Check(pd[29] == 1, "pd29-cheats-1");
                        Check(pd[32] == 2, "pd32-person-type-2");
                        Check(pd[70] == 0, "pd70-zodiac-absent (canon: never persisted)");
                        var skills = new[] { pd[9], pd[10], pd[11], pd[12], pd[15], pd[17], pd[18] };
                        Check(skills.All(v => v >= 0 && v < 1000), "skills-random-in-range");
                        _log("AUTOTEST uicasflow random-presence skills=[" + string.Join(",", skills)
                            + "] interests pd13=" + pd[13] + " pd14=" + pd[14] + " pd16=" + pd[16]
                            + " pd20=" + pd[20] + " pd26=" + pd[26] + " pd46=" + pd[46]
                            + " (random by design — presence/range only)");
                    }
                }
            }

            // the character-file block reuses the hoisted discovery above
            Check(newFiles.Count == 1, "one-new-character-file (" + newFiles.Count + ")");
            if (newFiles.Count == 1)
            {
                _newCharFile = System.IO.Path.Combine(charDir, newFiles[0]);
                var cif = new IffFile(_newCharFile);
                var objd = cif.Get<OBJD>(128);
                Check(objd != null, "char-objd-128");
                if (objd != null)
                {
                    Check(objd.GUID == famGuid, "char-objd-guid-in-family");
                    // generator label: "user" + 5-digit-padded userid ("User00024.iff" stem)
                    var expectedLabel = charStem + " - " + _personName;
                    Check(objd.ChunkLabel == expectedLabel,
                        "char-objd-label (" + (objd.ChunkLabel ?? "null") + ")");
                }
                var ctss = cif.Get<CTSS>(2000);
                Check(ctss != null && ctss.GetString(0) == _personName && ctss.GetString(1) == BioTyped,
                    "char-ctss-name-bio");
                var str = cif.Get<STR>(200);
                Check(str != null, "char-str200-present");
                if (str != null && _member != null)
                {
                    Check(str.GetString(0) == "adult", "str0-adult (" + str.GetString(0) + ")");
                    Check(str.GetString(12) == "male", "str12-male (" + str.GetString(12) + ")");
                    Check(str.GetString(13) == "27", "str13-27 (" + str.GetString(13) + ")");
                    Check(str.GetString(14) == "lgt", "str14-skin (" + str.GetString(14) + ")");
                    Check(str.GetString(1) == _member.Body + ",BODY=" + _member.BodyTex, "str1-body-pair");
                    Check(str.GetString(2) == _member.Head + ",HEAD-HEAD=" + _member.HeadTex, "str2-head-pair");
                    DiffTemplate(str);
                }
            }

            if (RecordFmtEnabled) CheckRecordFmt(iff, nbrs, member, pd, famGuid, newId, charStem, charDir, newFiles);

            FinishIsolationChecks();
            Advance(P_EVIDENCE);
        }

        /// <summary>
        /// R252 recordfmt (opt-in) assertions, run on the freshly-written
        /// Neighborhood.iff + character file that PhaseDisk already parsed.
        /// These assert the ORIGINAL TS1 created-record byte format: NBRS chunk
        /// data Version 0x3E, record Version 0x4 (raw PersonData 0xa0 = 80
        /// shorts), PersonMode 5, Name = lowercase file stem, skin pd[60] in the
        /// 1/2/3 dialect, and FAMI Version 7 with NO trailing bytes. Also asserts
        /// write-&gt;read-&gt;re-write is lossless (the reader must not mutate the
        /// stored skin array). Verdict is surfaced separately via RecordFmtSucceeded.
        /// </summary>
        private void CheckRecordFmt(IffFile iff, NBRS nbrs, Neighbour member, short[] pd,
            uint famGuid, ushort newId, string charStem, string charDir, List<string> newFiles)
        {
            // NBRS chunk-data Version must be 0x3E (original), not 0x49.
            CheckFmt(nbrs != null && nbrs.Version == 0x3E, "nbrs-chunkver-3e (" + (nbrs?.Version) + ")");

            // raw record scan (strong codec-level byte-format assertion). Reused
            // the production Neighbour codec over chunk.OriginalData, so it is
            // guaranteed to parse every record exactly as NBRS.Read does (a
            // hand-rolled scanner desyncs on the templateperson relationship
            // block). Returns -1 when no raw data, 0 when the member wasn't found,
            // 1 when found.
            int rawStatus = -1;
            int rv = 0, rpMode = 0, rPdBytes = 0;
            string rName = null;
            if (nbrs != null && nbrs.OriginalData != null)
            {
                rawStatus = RawScanNbrs(nbrs.OriginalData, famGuid, out rv, out rpMode, out rName, out rPdBytes);
            }
            bool rawFound = rawStatus == 1;
            CheckFmt(rawFound, "nbrs-raw-member-found");
            if (rawFound)
            {
                CheckFmt(rv == 0x4, "nbrs-raw-rec-version-4 (" + rv + ")");
                CheckFmt(rpMode == 5, "nbrs-raw-pm-5 (" + rpMode + ")");
                CheckFmt(rName == charStem, "nbrs-raw-name-stem (" + (rName ?? "null") + " vs " + (charStem ?? "null") + ")");
                CheckFmt(rPdBytes == 0xa0, "nbrs-raw-pd-bytes-a0 (" + rPdBytes + ")");
            }
            else if (rawStatus == 0 && nbrs != null)
            {
                _log("AUTOTEST recordfmt nbrs-raw-scan-miss: originalData.len=" + (nbrs.OriginalData?.Length)
                    + " famGuid=" + famGuid.ToString("x8"));
            }

            // in-memory member (parsed) assertions
            CheckFmt(member != null && member.Version == 0x4, "member-version-4");
            CheckFmt(member != null && member.PersonMode == 5, "member-pm-5");
            CheckFmt(member != null && member.Name == charStem, "member-name-stem");
            if (pd != null && pd.Length >= 88)
            {
                CheckFmt(pd[2] == TargetVector[4] * 100 && pd[3] == TargetVector[2] * 100 && pd[4] == 0
                    && pd[5] == TargetVector[3] * 100 && pd[6] == TargetVector[1] * 100 && pd[7] == TargetVector[0] * 100,
                    "pd2-7-personality (generous=0)");
                CheckFmt(pd[58] == 27, "pd58-adult-27 (" + pd[58] + ")");
                CheckFmt(pd[65] == 0, "pd65-male-0 (" + pd[65] + ")");
                CheckFmt(pd[60] == 1, "pd60-skin-light-1 (" + pd[60] + ")");
                CheckFmt(pd[61] == (short)newId, "pd61-family-chunkid (" + pd[61] + ")");
                CheckFmt(pd[70] == 0, "pd70-zodiac-0 (" + pd[70] + ")");
            }

            // R252 P0-2: assert the production disk<->AppearanceType map DIRECTLY so
            // the dialect on both branches is proven, independent of the lot-load
            // path. These are the exact constants from VMTS1ActivatorNew
            // (DiskSkinToAppearance) and SimitoneNeighbourGenerator (SkinToDisk):
            // original disk (Version 0x4) 1=lgt,2=drk,3=med; legacy port disk
            // (Version 0xA) 0=lgt,1=med,2=drk. AppearanceType { Light=0, Medium=1,
            // Dark=2 }.
            CheckFmt(VMTS1ActivatorNew.DiskSkinToAppearance(1, 0x4) == AppearanceType.Light,
                "skinmap-original-1-light");
            CheckFmt(VMTS1ActivatorNew.DiskSkinToAppearance(3, 0x4) == AppearanceType.Medium,
                "skinmap-original-3-medium");
            CheckFmt(VMTS1ActivatorNew.DiskSkinToAppearance(2, 0x4) == AppearanceType.Dark,
                "skinmap-original-2-dark");
            CheckFmt(VMTS1ActivatorNew.DiskSkinToAppearance(0, 0xA) == AppearanceType.Light,
                "skinmap-legacy-0-light");
            CheckFmt(VMTS1ActivatorNew.DiskSkinToAppearance(1, 0xA) == AppearanceType.Medium,
                "skinmap-legacy-1-medium");
            CheckFmt(VMTS1ActivatorNew.DiskSkinToAppearance(2, 0xA) == AppearanceType.Dark,
                "skinmap-legacy-2-dark");

            // FAMI Version 7 with raw data length exactly 40 + 4*guidCount (no trailing).
            var famiN = iff.Get<FAMI>(newId);
            CheckFmt(famiN != null && famiN.Version == 7, "fami-version-7 (" + (famiN?.Version) + ")");
            if (famiN != null)
            {
                var expect = 40 + 4 * famiN.FamilyGUIDs.Length;
                CheckFmt(famiN.OriginalData != null && famiN.OriginalData.Length == expect,
                    "fami-no-trailing (len=" + (famiN.OriginalData?.Length) + " expect " + expect + ")");
            }

            // write -> read -> re-write round trip: reloading the port's own
            // new-format record must not lose any field (a reader that mutated
            // pd[60] to 0/1/2 would fail here).
            try
            {
                var rtPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(charDir), "recordfmt-rt.iff");
                using (var fs = File.Create(rtPath)) iff.Write(fs);
                var rt2 = new IffFile(rtPath);
                var rtNbrs = rt2.List<NBRS>()?.FirstOrDefault();
                var rtMember = rtNbrs?.Entries.FirstOrDefault(e => famGuid != 0 && e.GUID == famGuid);
                CheckFmt(rtMember != null, "roundtrip-member");
                if (rtMember != null)
                {
                    CheckFmt(rtMember.Version == 0x4 && rtMember.PersonMode == 5 && rtMember.Name == charStem,
                        "roundtrip-ver-pm-name");
                    var rpd = rtMember.PersonData;
                    CheckFmt(rpd != null && rpd.Length >= 88, "roundtrip-pd-present");
                    if (rpd != null && pd != null && rpd.Length >= 88)
                    {
                        CheckFmt(rpd[60] == pd[60] && rpd[58] == pd[58] && rpd[65] == pd[65]
                            && rpd[61] == pd[61] && rpd[70] == pd[70], "roundtrip-fields-preserved");
                        CheckFmt(rpd[60] == 1, "roundtrip-pd60-still-disk-1 (" + rpd[60] + ")");
                    }
                }
                try { File.Delete(rtPath); } catch { }
            }
            catch (Exception e)
            {
                CheckFmt(false, "roundtrip-exception (" + e.GetType().Name + ": " + e.Message + ")");
            }
            _recordFmtCompleted = true;
        }

        /// <summary>
        /// Raw scan of an NBRS chunk's data bytes for the record with the given
        /// GUID, reusing the production Neighbour codec (IoBuffer + new
        /// Neighbour(IoBuffer)) so it parses exactly like NBRS.Read. Returns
        /// -1 when there is no raw data, 0 when the member was not found, 1 when
        /// found. personDataBytes is the MEASURED on-disk PersonData byte span the
        /// codec actually consumed for the record (0xa0 for Version 0x4, 0x200 for
        /// 0xA, 0 when personMode is 0). It comes from Neighbour.PersonDataBytes
        /// (a stream-position delta set during read), NOT re-derived from Version,
        /// so it genuinely verifies the written record shape. Used by recordfmt to
        /// assert the on-disk record is the original 0x4 / 0xa0 shape.
        /// </summary>
        private static int RawScanNbrs(byte[] data, uint targetGuid, out int recordVersion,
            out int personMode, out string name, out int personDataBytes)
        {
            recordVersion = 0; personMode = 0; name = null; personDataBytes = 0;
            if (data == null || data.Length < 16) return -1;
            try
            {
                using (var ms = new MemoryStream(data))
                using (var io = IoBuffer.FromStream(ms, ByteOrder.LITTLE_ENDIAN))
                {
                    io.ReadUInt32(); // pad
                    io.ReadUInt32(); // chunk-data version
                    io.ReadCString(4); // magic 'SRBN'
                    var count = io.ReadUInt32();
                    for (int i = 0; i < count; i++)
                    {
                        if (!io.HasMore) break;
                        var nb = new Neighbour(io);
                        if (nb.Unknown1 == 1 && nb.GUID == targetGuid)
                        {
                            recordVersion = nb.Version;
                            personMode = nb.PersonMode;
                            name = nb.Name;
                            personDataBytes = nb.PersonMode > 0 ? nb.PersonDataBytes : 0;
                            return 1;
                        }
                    }
                }
            }
            catch { }
            return 0;
        }

        /// <summary>Diff the written STR#200 against the TemplatePerson baseline.
        /// CAS-controlled slots are pinned by the value asserts above; the
        /// generator extras ([30]-[34]) and prefix-divergent slots ([27]-[29])
        /// are DOCUMENTED as residuals, not fixed this round.</summary>
        private void DiffTemplate(STR written)
        {
            var changed = new List<int>();
            for (int i = 0; i < written.Length; i++)
            {
                var value = written.GetString(i) ?? "";
                _templateStr.TryGetValue(i, out var baseline);
                if (value != (baseline ?? "")) changed.Add(i);
            }
            Check(changed.Contains(1) && changed.Contains(2), "str-diff-body-head-changed");
            _log("AUTOTEST uicasflow str200-changed-slots=[" + string.Join(",", changed) + "]"
                + " baselineSlots=" + _templateStr.Count + " writtenSlots=" + written.Length);
            for (int i = 27; i <= 34; i++)
            {
                var value = i < written.Length ? (written.GetString(i) ?? "") : "<absent>";
                _templateStr.TryGetValue(i, out var baseline);
                if (i >= 30)
                    _log("AUTOTEST uicasflow str-extra-slot-" + i + "='" + value
                        + "' (generator extra; original base-game TemplatePerson never carries it — residual, documented)");
                else
                    _log("AUTOTEST uicasflow str-divergent-slot-" + i + " written='" + value + "' template='"
                        + (baseline ?? "<absent>") + "' (prefix divergence — residual, documented)");
            }
        }

        private void FinishIsolationChecks()
        {
            try
            {
                _log("AUTOTEST uicasflow isolation userdir=" + FSOEnvironment.UserDir
                    + " (redirect from " + (_priorUserDir ?? "?") + ")");
                _log("AUTOTEST uicasflow note: autotest.log writes to <UserDir>/autotest.log (temp redirect) "
                    + "+ /tmp/autotest.log; CAS family diagnostics go to standard output");
                if (_gameDataNeighPath == null || _gameDataNeighSha == null)
                {
                    Check(false, "gamedata-baseline-missing");
                    return;
                }
                var info = new FileInfo(_gameDataNeighPath);
                var sha = Sha256(_gameDataNeighPath);
                Check(info.Length == _gameDataNeighLen && sha == _gameDataNeighSha,
                    "gamedata-neighborhood-byte-identical");
                Check(File.GetLastWriteTimeUtc(_gameDataNeighPath) == _gameDataNeighMtime,
                    "gamedata-neighborhood-mtime-untouched");
            }
            catch (Exception e)
            {
                Check(false, "gamedata-integrity-exception (" + e.GetType().Name + ": " + e.Message + ")");
            }
        }

        private void PhaseEvidence()
        {
            _frames++;
            if (_frames != 1) return;
            try
            {
                var userDir = FSOEnvironment.UserDir;
                var audit = System.IO.Path.Combine(userDir, "ui-audit", "r246");
                Directory.CreateDirectory(audit);
                var lines = new List<string>();
                CopyEvidence(System.IO.Path.Combine(userDir, "UserData", "Neighborhood.iff"), audit, "Neighborhood.iff", lines);
                if (_newCharFile != null)
                    CopyEvidence(_newCharFile, audit, System.IO.Path.GetFileName(_newCharFile), lines);
                lines.Add("record: family=" + _familyName + " person=" + _personName
                    + " vector=[Neat=" + TargetVector[0] + ",Outgoing=" + TargetVector[1]
                    + ",Active=" + TargetVector[2] + ",Playful=" + TargetVector[3]
                    + ",Nice=" + TargetVector[4] + "] zodiac=" + UIOriginalDesignChar.ZodiacName(ArgminZodiac(TargetVector)));
                File.WriteAllLines(System.IO.Path.Combine(audit, "sha256.txt"), lines);
                _log("AUTOTEST uicasflow evidence copied to " + audit);
            }
            catch (Exception e)
            {
                Check(false, "evidence-copy-exception (" + e.GetType().Name + ": " + e.Message + ")");
            }
            ToFinish();
        }

        private void CopyEvidence(string src, string auditDir, string name, List<string> lines)
        {
            if (!File.Exists(src)) { Check(false, "evidence-missing-" + name); return; }
            var dst = System.IO.Path.Combine(auditDir, name);
            File.Copy(src, dst, true);
            lines.Add(Sha256(dst) + "  " + name);
            _log("AUTOTEST uicasflow evidence " + name + " sha256=" + Sha256(dst));
        }
    }
}
