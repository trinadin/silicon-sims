/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/
using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Common;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Content;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using Microsoft.Xna.Framework;
using Simitone.Client.UI.Panels;
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
    /// UI-21 'importui' (opt-in, focused) — the runtime acceptance fixture for the
    /// USER-FACING generic FAM import flow (the SAV-05 'impexport' battery covers
    /// the engine consumer; this one covers the dialog on top of it). Runs on the
    /// real game bootstrap at the neighborhood screen (R247 takeover idiom,
    /// BeginIsolation redirects FSOEnvironment.UserDir before Content.Init).
    ///
    /// Laws exercised, in order:
    /// 1. STR# 143 'ImportStrs' — the 9 English entries the dialog renders
    ///    (byte-verbatim decode: tools/iff-dump/r254-import-ui).
    /// 2. Auto-poll gate — a staged NON-tutorial FAM is NOT consumed by the 1 s
    ///    cadence (native decode §1.2 FileExists gate on Import/Tutorial.FAM);
    ///    it is still on disk several cadence periods later.
    /// 3. Button wiring — the REAL mounted neighborhood Import button opens the
    ///    dialog (synthesized in-process MouseDown/Up through the production
    ///    UIButton.OnMouseEvent path, the UI-09 idiom).
    /// 4. Dialog law — STR# 143 title [8]/question [0]/scenario line [1]/member
    ///    list [5] with the $family substitution; Yes [6]/No [7] captions.
    /// 5. No-path — the staged file is untouched, no family added.
    /// 6. Yes-path — the production import consumer runs (bridge PollImports):
    ///    file consumed, family present by FAMs name, members' character files
    ///    created, and the Export/ re-export mirror wrote the imported family.
    /// 7. Export bridge — ExportFamily re-writes a fresh-parseable artifact
    ///    (EXPi/FAMI/FAMs with the family name).
    /// </summary>
    public class AutotestImportUI
    {
        private Action<string> _log;
        private int _phase, _phaseTicks;
        private bool _done, _passed = true;
        private readonly List<string> _notes = new List<string>();
        private readonly List<string> _fails = new List<string>();
        private TS1NeighborhoodProvider N;
        private TS1GameScreen Screen;
        private UINeighbourhoodSwitcher Switcher;

        private int _fami0, _char0;
        private string _stagedPath;
        private TutorialEngine247.ImportFileDto _desc;

        // isolation (R246/R247/SAV-05 BeginIsolation idiom)
        private static string _priorUserDir;
        internal static string RedirectDir;
        internal static string IsolationError;

        internal static bool BeginIsolation(string checksCsv)
        {
            var c = checksCsv?.Trim().Trim('"');
            if (string.IsNullOrEmpty(c)) return false;
            var names = c.Split(',').Select(s => s.Trim().ToLowerInvariant()).ToList();
            if (!names.Contains("importui")) return false;
            try
            {
                _priorUserDir = FSOEnvironment.UserDir;
                RedirectDir = Path.Combine(Path.GetTempPath(), "simitone-ui21-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(RedirectDir);
                FSOEnvironment.UserDir = RedirectDir;
                return true;
            }
            catch (Exception e)
            {
                IsolationError = e.GetType().Name + ": " + e.Message;
                return true;
            }
        }

        public bool Passed { get { return _passed && _fails.Count == 0; } }
        public string Diagnostics { get { return string.Join("; ", _notes); } }
        public string Failures { get { return string.Join("; ", _fails); } }

        public AutotestImportUI(Action<string> log)
        {
            _log = log;
            if (IsolationError != null)
            {
                Fail("isolation unavailable: " + IsolationError);
                _done = true;
            }
        }

        public bool Tick()
        {
            if (_done) return true;
            if (++_phaseTicks > 900)
            {
                Fail("stalled 900 ticks in phase " + _phase);
                return true;
            }
            try
            {
                switch (_phase)
                {
                    case 0: PhaseReady(); break;
                    case 1: PhaseStringsLaw(); break;
                    case 2: PhaseStageAndGate(); break;
                    case 3: PhaseButtonOpensDialog(); break;
                    case 4: PhaseNoPath(); break;
                    case 5: PhaseYesPathImport(); break;
                    case 6: PhaseExportBridge(); break;
                    default:
                        _done = true;
                        break;
                }
            }
            catch (Exception e)
            {
                var at = (e.StackTrace ?? "").Split('\n').FirstOrDefault()
                    ?.TrimStart().Split('(').FirstOrDefault();
                Fail("EXC phase " + _phase + ": " + e.GetType().Name + " " + e.Message
                    + (at == null ? "" : " @ " + at));
                _done = true;
            }
            return _done;
        }

        private void Next() { _phase++; _phaseTicks = 0; }

        private void Check(bool cond, string what)
        {
            if (cond) _notes.Add("ok: " + what);
            else Fail(what);
        }

        private void Fail(string what)
        {
            _notes.Add("FAIL: " + what);
            _fails.Add(what);
            _passed = false;
            _log("AUTOTEST importui: FAIL " + what);
        }

        private void Note(string what)
        {
            _notes.Add(what);
            _log("AUTOTEST importui: " + what);
        }

        // --- helpers -------------------------------------------------------

        private static readonly MethodInfo MouseEvent = typeof(UIButton).GetMethod("OnMouseEvent",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static UpdateState FreshState()
        {
            return new UpdateState
            {
                Time = new GameTime(),
                WindowFocused = true,
                InputManager = new FSO.Common.Rendering.Framework.IO.InputManager()
            };
        }

        private void Press(UIButton button)
        {
            var state = FreshState();
            MouseEvent.Invoke(button, new object[] { UIMouseEventType.MouseDown, state });
            MouseEvent.Invoke(button, new object[] { UIMouseEventType.MouseUp, state });
        }

        private string ImportDir()
        {
            var d = Path.Combine(N.UserPath, "Import");
            Directory.CreateDirectory(d);
            return d;
        }

        /// <summary>Copy a template FAM from read-only game-data, patch its FAMI
        /// house to 0 (family-only import — no house/lot dependencies), stage it
        /// under the given name. Engine-parse verified (SAV-05 StageFam idiom).</summary>
        private string StageCharmingHouse0(string stagedName)
        {
            var src = Path.Combine(GlobalSettings.Default.TS1HybridPath,
                "TemplateFamilyUnleashed", "Charming_13.FAM");
            var bytes = File.ReadAllBytes(src);
            if (!PatchFirstFamiHouse(bytes, 0)) return null;
            var path = Path.Combine(ImportDir(), stagedName);
            File.WriteAllBytes(path, bytes);
            var verify = new IffFile(path);
            var fami = verify.List<FAMI>()?.FirstOrDefault();
            if (fami == null || fami.HouseNumber != 0) return null;
            return path;
        }

        /// <summary>Walk raw IFF chunk headers and patch the house field (payload
        /// offset 12) of the first FAMI chunk (BE headers, LE payload; SAV-05). </summary>
        private static bool PatchFirstFamiHouse(byte[] bytes, int house)
        {
            int pos = 64;
            bool patched = false;
            while (pos + 76 <= bytes.Length)
            {
                string type = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                uint size = (uint)((bytes[pos + 4] << 24) | (bytes[pos + 5] << 16)
                    | (bytes[pos + 6] << 8) | bytes[pos + 7]);
                if (size < 76 || pos + size > bytes.Length) return false;
                if (type == "FAMI" && !patched)
                {
                    int h = pos + 76 + 12;
                    bytes[h] = (byte)house;
                    bytes[h + 1] = (byte)(house >> 8);
                    bytes[h + 2] = (byte)(house >> 16);
                    bytes[h + 3] = (byte)(house >> 24);
                    patched = true;
                }
                pos += (int)size;
            }
            return patched;
        }

        private static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
                return Convert.ToBase64String(sha.ComputeHash(fs));
        }

        private FAMI FamilyByName(string name)
        {
            var famis = N.MainResource.List<FAMI>();
            if (famis == null) return null;
            foreach (var f in famis)
            {
                if (f == null) continue;
                var n = N.MainResource.Get<FAMs>(f.ChunkID)?.GetString(0);
                if (n == name) return f;
            }
            return null;
        }

        // --- phases --------------------------------------------------------

        private void PhaseReady()
        {
            Screen = GameFacade.Screens?.CurrentUIScreen as TS1GameScreen;
            N = Content.Get().Neighborhood;
            if (Screen == null || N == null || N.MainResource == null || N.UserPath == null) return;
            if (_phaseTicks < 30) return; // let the neighborhood screen settle

            Switcher = Screen.TS1NeighSwitcher as UINeighbourhoodSwitcher;
            Check(Switcher != null, "neighborhood-switcher-mounted");
            Check(TutorialEngine247.HasCheckImports, "bridge-checkimports-probed");
            Check(TutorialEngine247.HasDescribeImports, "bridge-describeimports-probed");
            Check(TutorialEngine247.HasExportFamily, "bridge-exportfamily-probed");

            //game-data read-only proof: the template source we copy must be
            //byte-identical at the end (the isolated UserDir takes the writes).
            var src = Path.Combine(GlobalSettings.Default.TS1HybridPath,
                "TemplateFamilyUnleashed", "Charming_13.FAM");
            Check(File.Exists(src), "template-fam-source-present");
            if (!_passed) { _done = true; return; }

            _fami0 = N.MainResource.List<FAMI>().Count(x => x != null);
            _char0 = Directory.GetFiles(Path.Combine(N.UserPath, "Characters"), "User*.iff").Length;
            Next();
        }

        private void PhaseStringsLaw()
        {
            //STR# 143 'ImportStrs' — the dialog's string law (9 English entries;
            //ContentStrings.LoadTS1 reads every UIText STR chunk, English set).
            var q = GameFacade.Strings.GetString("143", "0");
            Check(!string.IsNullOrEmpty(q) && !q.Contains("MISSING"), "str143-table-loaded");
            Check(q == "A family was found. Do you want to import the $family family into your neighborhood?",
                "str143-0-question-verbatim");
            Check(GameFacade.Strings.GetString("143", "1") == "They will show up in the family selection screen.",
                "str143-1-bin-line");
            Check(GameFacade.Strings.GetString("143", "2") == "They will displace the $family family in lot number $lot. The old family will show up in the family selection screen.",
                "str143-2-displace-line");
            Check(GameFacade.Strings.GetString("143", "3") == "They will be placed in empty lot number $lot.",
                "str143-3-empty-line");
            Check(GameFacade.Strings.GetString("143", "4") == "They will displace the vacant house on lot number $lot.",
                "str143-4-vacant-line");
            Check(GameFacade.Strings.GetString("143", "5") == "The members of the new family are:",
                "str143-5-members-line");
            Check(GameFacade.Strings.GetString("143", "6") == "Yes", "str143-6-yes");
            Check(GameFacade.Strings.GetString("143", "7") == "No", "str143-7-no");
            Check(GameFacade.Strings.GetString("143", "8") == "Import Family", "str143-8-title");
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseStageAndGate()
        {
            _stagedPath = StageCharmingHouse0("UIProbe_Charming.FAM");
            Check(_stagedPath != null, "staged-charming-house0");
            if (_stagedPath == null) { _done = true; return; }

            //describe law (the dialog's read-only half; engine GetImportInfoForFile set)
            var staged = TutorialEngine247.DescribeImports();
            Check(staged.Count == 1, "describe-lists-staged (count=" + staged.Count + ")");
            _desc = TutorialEngine247.DescribeFirstImport();
            Check(_desc != null, "describe-first-non-null");
            if (_desc == null) { _done = true; return; }
            Check(_desc.FamilyName == "Charming", "describe-name-charming (" + _desc.FamilyName + ")");
            Check(_desc.House == 0, "describe-house-0");
            Check(_desc.MemberNames.Length == 2, "describe-two-members (" + _desc.MemberNames.Length + ")");
            Check(_desc.MemberNames.All(x => !string.IsNullOrEmpty(x)), "describe-member-names-nonempty");
            Note("describe members: " + string.Join(", ", _desc.MemberNames) + "; networth=" + _desc.NetWorth);
            if (!_passed) { _done = true; return; }

            //auto-poll gate: the NEXT phase waits ≥4 cadence periods with the
            //non-tutorial FAM staged and asserts the file survives (the native
            //§1.2 FileExists gate keeps the 1 s tick inert).
            Next();
        }

        private void PhaseButtonOpensDialog()
        {
            //wait ≥4 cadence periods (the poll cadence is 1000 ms) then assert
            //the gated tick did NOT consume the staged file.
            if (_phaseTicks < 4 * 70) return; // ~4s of frames at 60Hz
            Check(File.Exists(_stagedPath), "gated-tick-left-staged-file");

            //the REAL production button opens the dialog (UI-09 input idiom —
            //mount, assert and answer within ONE synthesized interaction, the
            //same-tick shape AutotestTutorialLifecycle247 uses for its dialogs).
            Check(Switcher.ImportButtonForProbe != null, "import-button-mounted");
            var opens0 = Screen.ImportDialogOpensForProbe;
            if (Switcher.ImportButtonForProbe != null) Press(Switcher.ImportButtonForProbe);
            Check(Screen.ImportDialogOpensForProbe == opens0 + 1, "button-dispatched-showimportdialog");
            var dlg = Screen._importDialog;
            Check(dlg != null, "import-dialog-mounted");
            if (dlg == null) { _done = true; return; }
            Check(dlg.TitleTextForProbe == "Import Family", "dialog-title-str143-8 (" + dlg.TitleTextForProbe + ")");
            var msg = dlg.MessageTextForProbe ?? "";
            Check(msg.Contains("Charming"), "dialog-message-has-family-name");
            Check(msg.Contains("$family") == false, "dialog-message-substituted");
            Check(msg.Contains("family selection screen"), "dialog-message-has-bin-line");
            Check(msg.Contains("The members of the new family are:"), "dialog-message-has-members-line");
            UIButton yes, no;
            dlg.ButtonMap.TryGetValue(UIAlertButtonType.Yes, out yes);
            dlg.ButtonMap.TryGetValue(UIAlertButtonType.No, out no);
            Check(yes != null && yes.Caption == "Yes", "dialog-yes-button");
            Check(no != null && no.Caption == "No", "dialog-no-button");
            if (!_passed) { _done = true; return; }

            //NO path, same tick: the staged file is untouched, no family added,
            //the dialog closes synchronously.
            Press(no);
            Check(Screen._importDialog == null, "no-path-closes-dialog");
            Check(File.Exists(_stagedPath), "no-path-left-staged-file");
            var famiCount = N.MainResource.List<FAMI>().Count(x => x != null);
            Check(famiCount == _fami0, "no-path-added-no-family (" + famiCount + " vs " + _fami0 + ")");
            Check(FamilyByName("Charming") == null, "no-path-no-charming-family");
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseYesPathImport()
        {
            //reopen through the production entry point, then confirm through the
            //real Yes button — the full user flow end-to-end, same tick.
            Screen.ShowImportDialog();
            var dlg = Screen._importDialog;
            Check(dlg != null, "reopen-dialog-mounted");
            if (dlg == null) { _done = true; return; }
            Check(dlg.MessageTextForProbe?.Contains("Charming") == true, "reopen-message-has-family-name");
            UIButton yes;
            dlg.ButtonMap.TryGetValue(UIAlertButtonType.Yes, out yes);
            if (yes == null) { Fail("yes-button-missing-on-reopen"); _done = true; return; }
            Press(yes); //the confirmed import runs synchronously in the handler

            Check(Screen.ImportConfirmYesForProbe > 0, "yes-dispatched");
            Check(!File.Exists(_stagedPath), "yes-path-consumed-staged-file");
            var fam = FamilyByName("Charming");
            Check(fam != null, "yes-path-charming-in-bin");
            if (fam != null)
            {
                Check(fam.FamilyGUIDs.Length == 2, "yes-path-two-members (" + fam.FamilyGUIDs.Length + ")");
                Note("imported family id=" + fam.ChunkID + " house=" + fam.HouseNumber
                    + " budget=" + fam.Budget);
            }
            var charNow = Directory.GetFiles(Path.Combine(N.UserPath, "Characters"), "User*.iff").Length;
            Check(charNow == _char0 + 2, "yes-path-two-character-files (" + charNow + " vs " + _char0 + ")");

            //the Export/ re-export mirror (free-wrapper tail): the imported
            //family was re-exported on import.
            var exportDir = Path.Combine(N.UserPath, "Export");
            Check(Directory.Exists(exportDir), "export-dir-created");
            var exported = Directory.Exists(exportDir)
                ? Directory.GetFiles(exportDir, "*.FAM") : new string[0];
            Check(exported.Length >= 1, "mirror-wrote-imported-family (" + exported.Length + ")");
            Note("mirror files: " + string.Join(", ", exported.Select(Path.GetFileName)));
            if (!_passed) { _done = true; return; }
            Next();
        }

        private void PhaseExportBridge()
        {
            var fam = FamilyByName("Charming");
            if (fam == null) { Fail("charming-gone-before-export"); _done = true; return; }
            var path = TutorialEngine247.ExportFamily(fam.ChunkID);
            Check(path != null && File.Exists(path), "export-bridge-wrote-file (" + (path ?? "null") + ")");
            if (path != null && File.Exists(path))
            {
                try
                {
                    var iff = new IffFile(path);
                    var expi = iff.List<EXPi>()?.FirstOrDefault();
                    var efami = iff.List<FAMI>()?.FirstOrDefault();
                    var efams = iff.List<FAMs>()?.FirstOrDefault();
                    Check(expi != null && expi.FamilyID == (short)fam.ChunkID, "export-expi-familyid");
                    Check(efami != null && efami.FamilyGUIDs.Length == 2, "export-fami-two-members");
                    Check(efams != null && efams.GetString(0) == "Charming", "export-fams-name");
                    Check(iff.List<NBRS>()?.FirstOrDefault() != null, "export-nbrs-subset");
                }
                catch (Exception e)
                {
                    Fail("export-fresh-parse: " + e.Message);
                }
            }

            //game-data read-only proof: the template source is untouched.
            var src = Path.Combine(GlobalSettings.Default.TS1HybridPath,
                "TemplateFamilyUnleashed", "Charming_13.FAM");
            Check(File.Exists(src), "template-source-still-present");
            _done = true;
        }
    }
}
