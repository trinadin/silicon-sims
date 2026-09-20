/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/
using FSO.Content;
using FSO.SimAntics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// R247 defensive bridge to the tutorial-lifecycle engine contract (the
    /// parallel FreeSO-side work): TS1NeighborhoodProvider.TutorialState /
    /// StageTutorialReset() / IsTutorialHouse(short) / CheckForNewImports()
    /// and VMContext.RequestTutorialCancel(). The client features that consume
    /// them (options reset row, neighborhood import poll, move-in refusal
    /// un-pin, ESC cancel) must compile and behave sanely whether or not the
    /// engine API has landed, so every member is resolved by reflection once
    /// and callers gate on the Has* flags. Missing API = the documented legacy
    /// behavior (reset failure dialog, no import, no refusal, no cancel) with
    /// the disclosure comments at each call site.
    ///
    /// CheckForNewImports contract coordination (r247-fam-import decode §1.2):
    /// the native law returns "imported any" and shows its own failure dialog
    /// inside CheckForNewImports. The engine contract is bool, where false
    /// ambiguously means "no file" or "error"; when a richer enum appears the
    /// bridge maps it (0 nothing / 1 imported / 2 error) so the client can
    /// surface the native "Couldn't import the family"/"Error" literals
    /// (blob 0x6d1c0+0xaf/+0xca — static, not runtime-localized).
    /// </summary>
    internal static class TutorialEngine247
    {
        public enum ImportPollResult
        {
            Nothing = 0,
            Imported = 1,
            Error = 2,
        }

        private static int _probed;
        private static PropertyInfo _tutorialState;
        private static PropertyInfo _tutorialHouse;
        private static PropertyInfo _lastImportHouse;
        private static PropertyInfo _userPath;
        private static MethodInfo _stageReset;
        private static MethodInfo _isTutorialHouse;
        private static MethodInfo _checkImports;
        private static MethodInfo _requestCancel;
        private static MethodInfo _describeImports;
        private static MethodInfo _exportFamily;

        /// <summary>StageTutorialReset dispatches since the process started
        /// (battery evidence that the row command reached the engine).</summary>
        public static int StageResetCallCount { get; private set; }
        /// <summary>RequestTutorialCancel dispatches since process start.</summary>
        public static int CancelCallCount { get; private set; }

        private static void Probe()
        {
            if (Interlocked.CompareExchange(ref _probed, 1, 0) != 0) return;
            try
            {
                var nbhdType = typeof(FSO.Content.TS1.TS1NeighborhoodProvider);
                _tutorialState = nbhdType.GetProperty("TutorialState",
                    BindingFlags.Public | BindingFlags.Instance);
                _tutorialHouse = nbhdType.GetProperty("TutorialHouse",
                    BindingFlags.Public | BindingFlags.Instance);
                _lastImportHouse = nbhdType.GetProperty("LastImportHouse",
                    BindingFlags.Public | BindingFlags.Instance);
                _stageReset = nbhdType.GetMethod("StageTutorialReset",
                    BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                _isTutorialHouse = nbhdType.GetMethod("IsTutorialHouse",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new Type[] { typeof(short) }, null);
                _checkImports = nbhdType.GetMethod("CheckForNewImports",
                    BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                _requestCancel = typeof(VMContext).GetMethod("RequestTutorialCancel",
                    BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                _userPath = nbhdType.GetProperty("UserPath",
                    BindingFlags.Public | BindingFlags.Instance);
                _describeImports = nbhdType.GetMethod("DescribeImports",
                    BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                _exportFamily = nbhdType.GetMethod("ExportFamily",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new Type[] { typeof(ushort) }, null);
            }
            catch
            {
                // Resolution failure = the documented legacy behavior.
                _tutorialState = null;
                _tutorialHouse = null;
                _lastImportHouse = null;
                _stageReset = null;
                _isTutorialHouse = null;
                _checkImports = null;
                _requestCancel = null;
                _userPath = null;
                _describeImports = null;
                _exportFamily = null;
            }
        }

        private static object Provider
        {
            get
            {
                try { return Content.Get().Neighborhood; }
                catch { return null; }
            }
        }

        public static bool HasStageReset { get { Probe(); return _stageReset != null; } }
        public static bool HasIsTutorialHouse { get { Probe(); return _isTutorialHouse != null; } }
        public static bool HasCheckImports { get { Probe(); return _checkImports != null; } }
        public static bool HasTutorialState { get { Probe(); return _tutorialState != null; } }
        public static bool HasTutorialHouse { get { Probe(); return _tutorialHouse != null; } }
        public static bool HasLastImportHouse { get { Probe(); return _lastImportHouse != null; } }
        public static bool HasRequestCancel { get { Probe(); return _requestCancel != null; } }
        public static bool HasDescribeImports { get { Probe(); return _describeImports != null; } }
        public static bool HasExportFamily { get { Probe(); return _exportFamily != null; } }

        /// <summary>
        /// UI-21: the client-side DTO the import DIALOG renders (r247-fam-import
        /// decode §2.1 ImportInfo's observable fields, mapped from the engine's
        /// TS1NeighborhoodProvider.TS1ImportInfo through the seam). Null when the
        /// engine API is missing or nothing valid is staged.
        /// </summary>
        public sealed class ImportFileDto
        {
            public string Path;
            public string FamilyName;
            public int House;
            public bool HouseFileExists;
            public string OccupantName;
            public string[] MemberNames;
            public int NetWorth;
        }

        /// <summary>
        /// Describe the *.FAM files staged in &lt;UserData&gt;Import/ (scan order,
        /// the same order PollImports consumes them). Empty list when the API is
        /// missing or nothing importable is staged (the native import UI is
        /// presence-gated, model+0x160).
        /// </summary>
        public static List<ImportFileDto> DescribeImports()
        {
            var result = new List<ImportFileDto>();
            Probe();
            if (_describeImports == null) return result;
            var provider = Provider;
            if (provider == null) return result;
            try
            {
                if (!(_describeImports.Invoke(provider, null) is System.Collections.IEnumerable raw)) return result;
                foreach (var item in raw)
                {
                    if (item == null) continue;
                    var type = item.GetType();
                    result.Add(new ImportFileDto
                    {
                        Path = type.GetField("Path")?.GetValue(item) as string,
                        FamilyName = type.GetField("FamilyName")?.GetValue(item) as string,
                        House = Convert.ToInt32(type.GetField("House")?.GetValue(item)),
                        HouseFileExists = Convert.ToBoolean(type.GetField("HouseFileExists")?.GetValue(item)),
                        OccupantName = type.GetField("OccupantName")?.GetValue(item) as string,
                        MemberNames = (type.GetField("MemberNames")?.GetValue(item) as string[]) ?? new string[0],
                        NetWorth = Convert.ToInt32(type.GetField("NetWorth")?.GetValue(item)),
                    });
                }
            }
            catch
            {
                //describe failure = nothing staged (the dialog degrades to no-op)
            }
            return result;
        }

        /// <summary>The first valid staged FAM in scan order — the one a confirmed
        /// import will consume (PollImports takes the same first-valid).</summary>
        public static ImportFileDto DescribeFirstImport()
        {
            return DescribeImports().FirstOrDefault();
        }

        /// <summary>
        /// UI-21 export path: serialize a bin family to &lt;UserData&gt;Export/.
        /// Returns the written path, or null on failure/missing API.
        /// </summary>
        public static string ExportFamily(int familyId)
        {
            Probe();
            if (_exportFamily == null) return null;
            var provider = Provider;
            if (provider == null) return null;
            try
            {
                return _exportFamily.Invoke(provider, new object[] { (ushort)familyId }) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// The native auto-import's FILE fact (decode §1.2: pathB =
        /// FindDataDirectory + GetString(0x58) + 'Tutorial.FAM'; FileExists gate).
        /// The client's 1 s cadence only auto-polls while this ONE file is staged;
        /// every other staged FAM waits for the explicit Import-button dialog.
        /// False when the API/provider is missing (wired-but-inert cadence).
        /// </summary>
        public static bool TutorialFamStaged()
        {
            Probe();
            if (_userPath == null) return false;
            var provider = Provider;
            if (provider == null) return false;
            try
            {
                var userPath = _userPath.GetValue(provider) as string;
                if (string.IsNullOrEmpty(userPath)) return false;
                return File.Exists(Path.Combine(userPath, "Import", "Tutorial.FAM"));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// The reset row's engine half: confirm/save-gate live client-side, then
        /// the engine copies UserData/Tutorial.FAM → UserData/Import/Tutorial.FAM
        /// (fail-if-exists per the native CopyFileA flag 1). False when the API
        /// is missing (legacy failure-dialog behavior) or the engine refused.
        /// </summary>
        public static bool StageTutorialReset()
        {
            Probe();
            if (_stageReset == null) return false;
            var provider = Provider;
            if (provider == null) return false;
            try
            {
                StageResetCallCount++;
                return (bool)_stageReset.Invoke(provider, null);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// HouseInfo+0x14 = house-SIMI global 58 (r247 §G). Missing API → false
        /// (the pre-R247 literal, keeping the legacy behavior byte-for-byte).
        /// </summary>
        public static bool IsTutorialHouse(short house)
        {
            Probe();
            if (_isTutorialHouse == null) return false;
            var provider = Provider;
            if (provider == null) return false;
            try
            {
                return (bool)_isTutorialHouse.Invoke(provider, new object[] { house });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// The neighborhood-screen import poll (r247-fam-import §1.2: Init +
        /// 1 s timer). Missing API → Nothing (the wired-but-inert cadence).
        /// </summary>
        public static ImportPollResult PollImports()
        {
            Probe();
            if (_checkImports == null) return ImportPollResult.Nothing;
            var provider = Provider;
            if (provider == null) return ImportPollResult.Nothing;
            try
            {
                var raw = _checkImports.Invoke(provider, null);
                if (raw is bool) return (bool)raw ? ImportPollResult.Imported : ImportPollResult.Nothing;
                if (raw is int) return (ImportPollResult)Convert.ToInt32(raw);
                if (raw is Enum)
                {
                    // Richer engine contract: by convention 0=nothing, 1=imported,
                    // 2=error (see class comment). Unknown values are Nothing.
                    int v = Convert.ToInt32(raw);
                    return (v >= 0 && v <= 2) ? (ImportPollResult)v : ImportPollResult.Nothing;
                }
                return ImportPollResult.Nothing;
            }
            catch
            {
                return ImportPollResult.Error;
            }
        }

        /// <summary>
        /// The live neighborhood tutorial-state latch (native Neighborhood+0x12a;
        /// state >= 3 permanently disables the spawner). Missing API → 0.
        /// </summary>
        public static int GetTutorialState()
        {
            Probe();
            if (_tutorialState == null) return 0;
            var provider = Provider;
            if (provider == null) return 0;
            try { return Convert.ToInt32(_tutorialState.GetValue(provider)); }
            catch { return 0; }
        }

        /// <summary>
        /// The live tutorial-house latch (native Neighborhood+0x12c, NGBH word
        /// 2 — holds the house number an imported tutorial family moved into).
        /// Missing API → 0.
        /// </summary>
        public static int GetTutorialHouse()
        {
            Probe();
            if (_tutorialHouse == null) return 0;
            var provider = Provider;
            if (provider == null) return 0;
            try { return Convert.ToInt32(_tutorialHouse.GetValue(provider)); }
            catch { return 0; }
        }

        /// <summary>
        /// The house number of the last successful import (0 = family-only or
        /// none) — the native reloadScreen gate (decode §1.2: refresh only when
        /// the import carried a nonzero house). Missing API → 0.
        /// </summary>
        public static int GetLastImportHouse()
        {
            Probe();
            if (_lastImportHouse == null) return 0;
            var provider = Provider;
            if (provider == null) return 0;
            try { return Convert.ToInt32(_lastImportHouse.GetValue(provider)); }
            catch { return 0; }
        }

        public static bool SetTutorialState(int value)
        {
            Probe();
            if (_tutorialState == null || !_tutorialState.CanWrite) return false;
            var provider = Provider;
            if (provider == null) return false;
            try
            {
                _tutorialState.SetValue(provider, value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// The ESC cancel (native CancelTutorial: run the owner's
        /// "cancel tutorial" tree, then kill the owner). False when the API is
        /// missing — callers keep their client-side half (highlight hide) and
        /// disclose.
        /// </summary>
        public static bool RequestTutorialCancel(VMContext context)
        {
            Probe();
            if (_requestCancel == null || context == null) return false;
            try
            {
                CancelCallCount++;
                _requestCancel.Invoke(context, null);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
