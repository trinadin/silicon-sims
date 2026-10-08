using FSO.Client;
using FSO.Common;
using FSO.Common.Rendering.Framework.Model;
using FSO.Content;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics.Entities;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Panels.LiveSubpanels;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client
{
    /// <summary>
    /// EXP-16 'ssfame' (opt-in, one-shot at state 2) — the Superstar residual
    /// legs, each pinned against its decode receipt
    /// (coordination/evidence/EXP-16/):
    ///
    ///  A. GENDERED JOB TITLES (gendered-job-titles-law.md):
    ///     career STR per-language block law [0]=track name, [1]/[2]=offer
    ///     male/female, [3+2L]=male title, [4+2L]=desc, [23+L]=female title
    ///     (empty = neutral -> male fallback); cJob::GetName(bool)@0x10051ab0;
    ///     consumers: job panel title (GetShortName law @0x1029a900), VM
    ///     dialog Job:/JobDesc:/JobOffer: tokens (ParseUIString @0x100ef780),
    ///     web export (GenerateFamilyMemberData @0x1021d798).
    ///  B. FAME SCREENS reconcile (fame-screens-reconcile.md): the fame
    ///     subpanel display laws re-pinned live at a nontrivial level —
    ///     UpdateFameLabel @0x10455de0 (STR[level+1]), UpdateFamePopup
    ///     @0x10455cf0 (pair [13+2L]/[14+2L]), SetFriendsNeeded @0x10456000
    ///     (format [12]); the panel label/popup equality is index law, the
    ///     friends threshold TOC constants stay disclosed.
    ///  C. FAME OBJECTS census (studio-town-objects.md): every interactive
    ///     Studio Town object file (EP6/ExpansionPack6.far) mounts with a
    ///     non-empty TTAB and every interaction's action AND test tree
    ///     resolves under the native scope rule (private >= 4096,
    ///     semi-global >= 8192, else global) — "missing interactions" at the
    ///     opcode level were enumerated NONE (primitive census over the same
    ///     set: every opcode < 256 is in the port's registered set).
    /// </summary>
    public static class AutotestExp16SSFame
    {
        public struct PartResult { public string Key; public bool Ok; public string Detail; }

        // EP6 interactive set (the corpus' Studio Town object inventory minus
        // pure decor/npc-skin families; controllers + carpool included).
        private static readonly string[] SSObjects =
        {
            "karaokemachinesuperstar", "openmicsuperstar", "musicrecordingstudio",
            "musicvideoset", "setphotoshoot", "setsoapopera", "fashionrunway",
            "stage", "skydivingsimulator", "massagetable", "oxygenbar",
            "spasteamer", "scubatank", "hottubfame", "photocamera",
            "pianosuperstar", "starawards", "celebawardcabinet", "publicitytester",
            "tabloid", "satellitedish", "mirrordressingroom", "reclinermassage",
            "spatubsingle", "stereospeakerssuperstar", "studiominitrailer",
            "urnstonecelebrity", "aquariumfame", "npccontrollerss",
            "controllerstudiolot", "controllerspa", "tokenssuperstar",
            "phonestudiotown", "phonepluginstudiolots",
            "npc_superstar_obsessedfan", "npc_superstar_obsfancontroller",
            "npc_superstar_paparazzi", "npc_superstar_pa", "npc_superstar_pa_help",
            "npc_superstar_butler", "npc_superstar_maintenance",
            "npc_superstar_avril", "npc_superstar_christina", "npc_superstar_monroe",
            "npc_car_presenter", "carstudio", "carstudiocarpool"
        };


        public static List<PartResult> Run(FSO.SimAntics.VM vm)
        {
            var results = new List<PartResult>();
            Action<string, bool, string> emit = (k, ok, d) => results.Add(
                new PartResult { Key = k, Ok = ok, Detail = d });

            // ---------- A. gendered job titles ----------
            var jobs = Content.Get()?.Jobs;
            bool providerLaw = jobs != null;
            string diag = "";
            if (providerLaw)
            {
                var ent = jobs.JobStrings(2);   // Entertainment
                var law = jobs.JobStrings(3);   // Law Enforcement
                var pol = jobs.JobStrings(7);   // Politics
                var mus = jobs.JobStrings(12);  // Musician (identity female set)
                providerLaw =
                    ent != null && law != null && pol != null && mus != null
                    // STR block law (0-based; native GetString is 1-based = ours+1)
                    && ent.GetString(3) == "Waiter" && ent.GetString(23) == "Waitress"
                    && law.GetString(7) == "Patrolman" && law.GetString(25) == "Patrolwoman"
                    && pol.GetString(13) == "State Assemblyman" && pol.GetString(28) == "Assemblywoman"
                    && pol.GetString(15) == "Congressman" && pol.GetString(29) == "Congresswoman"
                    && mus.GetString(21) == "Celebrity Activist" && mus.GetString(32) == "Celebrity Activist"
                    // offer-dialog pair law ([1] male, [2] female-empty)
                    && !string.IsNullOrEmpty(ent.GetString(1)) && string.IsNullOrEmpty(ent.GetString(2))
                    // cJob::GetName law via the provider
                    && jobs.JobTitle(2, 0, false) == "Waiter"
                    && jobs.JobTitle(2, 0, true) == "Waitress"
                    && jobs.JobTitle(3, 2, true) == "Patrolwoman"
                    && jobs.JobTitle(7, 5, true) == "Assemblywoman"
                    && jobs.JobTitle(7, 6, true) == "Congresswoman"
                    && jobs.JobTitle(12, 9, true) == "Celebrity Activist"
                    // empty-female fallback: Business L0 has no female variant
                    && jobs.JobTitle(1, 0, true) == "Mailroom Clerk"
                    // offer fallback law (female offer empty -> male offer)
                    && jobs.JobOffer(2, 0, true) == jobs.JobStrings(2).GetString(1)
                    && jobs.JobOffer(2, 0, false) == jobs.JobStrings(2).GetString(1)
                    // desc index law [4+2L]
                    && !string.IsNullOrEmpty(jobs.JobStrings(12).GetString(4));
                if (!providerLaw)
                {
                    diag = " ent=" + (ent?.GetString(3) ?? "null")
                         + " wait=" + (jobs.JobTitle(2, 0, true) ?? "null")
                         + " mail=" + (jobs.JobTitle(1, 0, true) ?? "null")
                         + " offer=" + (jobs.JobOffer(2, 0, true) ?? "null");
                }
            }
            emit("ssfame-title-law", providerLaw, "titleLaw=" + providerLaw + diag);

            // LIVE panel law: the native TSPaint title row is
            // GetShortName(job, GetGender==1); the port must render the
            // gendered title and re-render on a same-level gender flip.
            bool panelLaw = false;
            string panelDiag = "";
            try
            {
                var game = GameFacade.Screens.CurrentUIScreen as TS1GameScreen;
                var sel = game?.SelectedAvatar;
                if (sel != null)
                {
                    var panel = new UIJobSubpanel(game);
                    short g0 = sel.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.Gender);
                    short jt0 = sel.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobType);
                    short jl0 = sel.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobPromotionLevel);
                    short age0 = sel.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.PersonsAge);
                    // force the adult job surface (a child selection would
                    // route to ReportCard and never touch JobTitle)
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.PersonsAge, 27);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.Gender, 0);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobType, 2);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobPromotionLevel, 0);
                    var st = new UpdateState();
                    panel.Update(st);
                    string male = panel.JobTitle.Caption;
                    panel.Update(st);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.Gender, 1);
                    panel.Update(st);
                    string female = panel.JobTitle.Caption;
                    panel.Update(st);
                    panelDiag = " male='" + male + "' female='" + female + "'";
                    panelLaw = male == "Waiter" && female == "Waitress";
                    // restore
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.PersonsAge, age0);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.Gender, g0);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobType, jt0);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobPromotionLevel, jl0);
                    panel.Update(st);
                }
                else panelDiag = " no-selected-avatar";
            }
            catch (Exception e) { panelDiag = " EXC " + e.GetType().Name + " " + e.Message; }
            emit("ssfame-title-panel", panelLaw, "panel=" + panelLaw + panelDiag);

            // Web-export token law (GenerateFamilyMemberData @ 0x1021d798).
            bool webLaw = false;
            string webDiag = "";
            try
            {
                var game = GameFacade.Screens.CurrentUIScreen as TS1GameScreen;
                var sel = game?.SelectedAvatar;
                if (sel != null)
                {
                    short g0 = sel.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.Gender);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.Gender, 1);
                    string f = OriginalWebExporter.FamilyMemberJobTitle(sel, 3, 2, null);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.Gender, 0);
                    string m = OriginalWebExporter.FamilyMemberJobTitle(sel, 3, 2, null);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.Gender, g0);
                    webDiag = " male='" + m + "' female='" + f + "'";
                    webLaw = m == "Patrolman" && f == "Patrolwoman";
                }
                else webDiag = " no-selected-avatar";
            }
            catch (Exception e) { webDiag = " EXC " + e.GetType().Name; }
            emit("ssfame-title-web", webLaw, "web=" + webLaw + webDiag);

            // ---------- B. fame screens (live re-pin at level 5) ----------
            bool fameLaw = false;
            string fameDiag = "";
            try
            {
                var game = GameFacade.Screens.CurrentUIScreen as TS1GameScreen;
                var sel = game?.SelectedAvatar;
                if (sel != null)
                {
                    // fame STR block via the same loader family the panel uses
                    var fameIff = Content.Get().TS1Global.Get("fame.iff") as IffFile;
                    var fs = fameIff?.Get<STR>(1);
                    // arm the fame triple (the famesess PD levers) at level 5
                    short s0 = sel.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.TS1FameScore);
                    short l0 = sel.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.TS1FameStarPower);
                    short p0 = sel.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.TS1FameStarHighWatermark);
                    short jt0 = sel.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobType);
                    short age0 = sel.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.PersonsAge);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.PersonsAge, 27); // adult -> Fame mode reachable
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.TS1FameScore, 100);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.TS1FameStarPower, 5);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.TS1FameStarHighWatermark, 5);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobType, 0); // no CARR -> Fame mode
                    var panel = new UIJobSubpanel(game);
                    var st = new UpdateState();
                    panel.Update(st);
                    panel.Update(st);
                    fameDiag = " level='" + (panel.FameLevelLabel?.Text ?? "null") + "'";
                    fameLaw = fs != null
                        && panel.FameLevelLabel != null
                        && panel.FameLevelLabel.Text == fs.GetString(5 + 1)      // UpdateFameLabel @0x10455de0
                        && fs.GetString(13 + 5 * 2) != null
                        && fs.GetString(13 + 5 * 2).Contains(fs.GetString(6))    // popup pair law: title [13+2L] carries the level name
                        && fs.GetString(12).Contains("%s")                        // friends format [12]
                        && panel.FameTitle != null && panel.FameTitle.Text == fs.GetString(0);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.TS1FameScore, s0);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.TS1FameStarPower, l0);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.TS1FameStarHighWatermark, p0);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobType, jt0);
                    sel.SetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.PersonsAge, age0);
                }
                else fameDiag = " no-selected-avatar";
            }
            catch (Exception e) { fameDiag = " EXC " + e.GetType().Name + " " + e.Message; }
            emit("ssfame-screens", fameLaw, "screens=" + fameLaw + fameDiag);

            // ---------- C. Studio Town objects census ----------
            int files = 0, mounted = 0, interactions = 0, resolved = 0, unresolved = 0;
            var unresolvedList = new List<string>();
            try
            {
                var wop = Content.Get().WorldObjects;
                var byFile = new Dictionary<string, List<GameObjectReference>>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var e in wop.Entries.Values)
                {
                    var fn = (e.FileName ?? "").Replace('\\', '/').ToLowerInvariant();
                    var baseName = fn.Substring(fn.LastIndexOf('/') + 1);
                    List<GameObjectReference> list;
                    if (!byFile.TryGetValue(baseName, out list))
                        byFile[baseName] = list = new List<GameObjectReference>();
                    list.Add(e);
                }
                foreach (var name in SSObjects)
                {
                    files++;
                    List<GameObjectReference> refs;
                    if (!byFile.TryGetValue(name + ".iff", out refs) || refs.Count == 0)
                    {
                        unresolvedList.Add(name + ":NOT-MOUNTED");
                        unresolved++;
                        continue;
                    }
                    mounted++;
                    foreach (var r in refs)
                    {
                        var gobj = r.Get();
                        var res = gobj?.Resource;
                        var objd = gobj?.OBJ;
                        if (res == null || objd == null) { unresolvedList.Add(name + ":NO-RES"); unresolved++; continue; }
                        var ttab = res.Get<TTAB>(objd.TreeTableID) ?? res.List<TTAB>().FirstOrDefault();
                        if (ttab == null || ttab.Interactions.Length == 0) continue; // decor slabs: legal
                        foreach (var ix in ttab.Interactions)
                        {
                            interactions++;
                            bool okTree = TreeResolves(vm, res, ix.ActionFunction)
                                       && (ix.TestFunction == 0 || TreeResolves(vm, res, ix.TestFunction));
                            if (okTree) resolved++;
                            else
                            {
                                unresolved++;
                                if (unresolvedList.Count < 30)
                                    unresolvedList.Add(name + ":tree"
                                        + ix.ActionFunction.ToString() + "/t" + ix.TestFunction);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                unresolvedList.Add("EXC " + e.GetType().Name + " " + e.Message);
                unresolved++;
            }
            bool objectsLaw = files > 0 && mounted == files && unresolved == 0 && interactions > 100;
            emit("ssfame-objects", objectsLaw,
                "objects=" + objectsLaw + " files=" + files + "/" + mounted
                + " ix=" + interactions + " unresolved=" + unresolved
                + (unresolvedList.Count > 0 ? " bad=[" + string.Join(",", unresolvedList.Take(8)) + "]" : ""));
            return results;
        }

        /// <summary>The native subroutine-scope rule (VMThread.TS1SubRoutineResolves):
        /// &gt;= 8192 semi-global, &gt;= 4096 private, else global.</summary>
        private static bool TreeResolves(FSO.SimAntics.VM vm, GameIffResource res, ushort id)
        {
            try
            {
                if (id >= 8192) return res.SemiGlobal?.GetRoutine(id) != null;
                if (id >= 4096) return res.GetRoutine(id) != null;
                return vm?.Context?.Globals?.Resource?.GetRoutine(id) != null;
            }
            catch { return false; }
        }
    }
}
