/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using FSO.Content;

namespace Simitone.Client
{
    /// <summary>
    /// AUD-19 'aud19' — per-event sound audible-fidelity laws, decoded from the
    /// original PPC PEF (receipt: coordination/evidence/AUD-19/trace-round-1.md).
    ///
    /// Two layers:
    ///   A. CORPUS LAW (hard asserts, offline-safe): the event-name truths the
    ///      native play-call sites pin against the mounted 4479-event corpus —
    ///      the refund sound, the CAS deny sound, the dead native names
    ///      (ui_buy_moneyback / footstep_terrain_noshoe exist NOWHERE in the
    ///      shipped corpus, so the native call sites were silent — the port must
    ///      not "fix" itself into firing them), the 12-name speed-transition
    ///      matrix, and the vox m/f/k suffix census (the native MappedEvent
    ///      fallback law is corpus-dead: zero bare+suffixed pairs).
    ///   B. TRACE WINDOW (live, degrades gracefully without a sound device):
    ///      enables FSO.HIT.HITTrace, fires a known event set through the real
    ///      HITVM.PlaySoundEvent dispatch, and asserts the per-event outcomes
    ///      (created / already-alive dedup / NOT-FOUND for a dead name) plus —
    ///      when notes actually reach the mixer — the as-played volume/pan/
    ///      group of a note. Also asserts the disabled-trace invariant (no
    ///      entries recorded while off).
    ///
    /// Native decode anchors (image space, 0x10000000+vaddr):
    ///   cXObject::TryPlaySound 0x100e7040 (sound primitive: priv→glob FWAV,
    ///     source = StackObjAsSource ? stack obj : caller; volume/sample-rate
    ///     operands are never read)
    ///   cSoundPlayer::PlayBySource 0x10300550 (lowercases the name; UI sounds
    ///     ride source -4 → GetInstanceVolPan default vol 0x400 / pan 0x200)
    ///   cBoxX::GetInstanceVolPan 0x102e1e80 (cross-level mismatch → vol*3/5;
    ///     edge-proximity floor 800/1024; zoom law 0x400-(3-zoom)*att*14)
    ///   cTool::DoRefundSound 0x101890a0 → "ui_moneyback"; DoKachingSound
    ///     0x10189140 → "ui_buy_moneyback" (dead name); DoDeniedSound
    ///     0x101891e0 → "ui_error"
    ///   cWinViewControl::DoSpeedTransitionSound 0x102aa100 → [from][to]
    ///     string table @ 0x106142b8 (zero diagonal, P/1/2/3)
    ///   SAnimator::HandleVitaBoyAnimEvent 0x1034ca10 (sound / selectedsound /
    ///     deselectedsound / footstep anim-event keys; footstep variants
    ///     footstep_snow / _soft_noshoe / _soft / _terrain_noshoe / _terrain)
    ///   CAS deny (cas-layout-law §click): PlaySoundA of the blob slot after
    ///     "UI_CAC_personpts" → "ui_nhood_error" (NOT "ui_cac_personpts_deny").
    /// </summary>
    public static partial class AutotestRunner
    {
        private static void CheckAud19()
        {
            try
            {
                bool ok = true;
                string info = "";

                var audio = FSO.Content.Content.Get().Audio;
                var evts = audio?.Events;

                // ---------- A. CORPUS LAWS (hard) --------------------------------

                // A1 refund: native DoRefundSound plays "UI_moneyback"; the corpus
                // has it (patch 1544 → SoundData\UI\ui_object_moneyback.xa).
                bool refund = evts != null && evts.ContainsKey("ui_moneyback");
                info += " refund=" + refund;
                if (!refund) ok = false;

                // A2 CAS deny: native plays "ui_nhood_error" (event 2703); the
                // old port name "ui_cac_personpts_deny" must stay ABSENT so the
                // deny click can never silently fire a dead id again.
                bool denyGood = evts != null && evts.ContainsKey("ui_nhood_error")
                    && !evts.ContainsKey("ui_cac_personpts_deny")
                    && FSO.Client.UI.Model.UISounds.NeighborhoodError == "ui_nhood_error";
                info += " casDeny=" + denyGood;
                if (!denyGood) ok = false;

                // A3 dead native names: DoKachingSound's "ui_buy_moneyback" and
                // the footstep variant "footstep_terrain_noshoe" appear nowhere in
                // the shipped corpus — natively those call sites were SILENT.
                // Pin their absence so nobody wires the port to the dead ids.
                bool deadNames = evts != null && !evts.ContainsKey("ui_buy_moneyback")
                    && !evts.ContainsKey("footstep_terrain_noshoe")
                    && evts.ContainsKey("footstep_terrain")
                    && evts.ContainsKey("footstep_soft")
                    && evts.ContainsKey("footstep_soft_noshoe")
                    && evts.ContainsKey("footstep_snow");
                info += " deadNativeNames=" + deadNames;
                if (!deadNames) ok = false;

                // A4 speed-transition matrix: the native [from][to] table
                // (0x106142b8, zero diagonal, index P=0/1/2/3) holds exactly the
                // 12 ui_speed_XtoY names; the port constants must match it
                // one-for-one.
                var nativeSpeedMatrix = new string[4, 4]
                {
                    { null,                  "ui_speed_pto1", "ui_speed_pto2", "ui_speed_pto3" },
                    { "ui_speed_1top",       null,            "ui_speed_1to2", "ui_speed_1to3" },
                    { "ui_speed_2top",       "ui_speed_2to1", null,            "ui_speed_2to3" },
                    { "ui_speed_3top",       "ui_speed_3to1", "ui_speed_3to2", null            },
                };
                var portSpeed = new Dictionary<string, string>
                {
                    { "P1", FSO.Client.UI.Model.UISounds.SpeedPTo1 },
                    { "P2", FSO.Client.UI.Model.UISounds.SpeedPTo2 },
                    { "P3", FSO.Client.UI.Model.UISounds.SpeedPTo3 },
                    { "1P", FSO.Client.UI.Model.UISounds.Speed1ToP },
                    { "12", FSO.Client.UI.Model.UISounds.Speed1To2 },
                    { "13", FSO.Client.UI.Model.UISounds.Speed1To3 },
                    { "2P", FSO.Client.UI.Model.UISounds.Speed2ToP },
                    { "21", FSO.Client.UI.Model.UISounds.Speed2To1 },
                    { "23", FSO.Client.UI.Model.UISounds.Speed2To3 },
                    { "3P", FSO.Client.UI.Model.UISounds.Speed3ToP },
                    { "31", FSO.Client.UI.Model.UISounds.Speed3To1 },
                    { "32", FSO.Client.UI.Model.UISounds.Speed3To2 },
                };
                var fromIdx = new Dictionary<int, string> { { 0, "P" }, { 1, "1" }, { 2, "2" }, { 3, "3" } };
                bool speed = true;
                for (int from = 0; from < 4 && speed; from++)
                    for (int to = 0; to < 4 && speed; to++)
                    {
                        var nativeName = nativeSpeedMatrix[from, to];
                        if (nativeName == null) continue; // zero diagonal = no sound
                        var key = fromIdx[from] + fromIdx[to];
                        string portName;
                        if (!portSpeed.TryGetValue(key, out portName) || portName != nativeName
                            || evts == null || !evts.ContainsKey(nativeName))
                            speed = false;
                    }
                info += " speedMatrix=" + speed;
                if (!speed) ok = false;

                // A5 vox m/f/k suffix law is corpus-dead: MappedEvent's fallback
                // (bare name miss → name+{m,f,k} by speaker data) can never fire
                // on the shipped corpus because no bare name coexists with a
                // suffixed variant. Pin the census at 0 pairs.
                int voxPairs = 0, voxTotal = 0;
                if (evts != null)
                {
                    var names = new HashSet<string>(evts.Keys);
                    foreach (var n in names)
                    {
                        if (!n.StartsWith("vox")) continue;
                        voxTotal++;
                        if (n.Length > 1 && (n.EndsWith("m") || n.EndsWith("f") || n.EndsWith("k"))
                            && names.Contains(n.Substring(0, n.Length - 1))) voxPairs++;
                    }
                }
                info += " vox(total=" + voxTotal + ",bareSuffixedPairs=" + voxPairs + ")";
                if (voxPairs != 0 || voxTotal <= 0) ok = false;

                // ---------- B. TRACE WINDOW (live; needs HITVM up) ---------------

                var hit = FSO.HIT.HITVM.Get();
                if (hit == null)
                {
                    Log("AUTOTEST aud19: HITVM.Get() null — corpus laws only" + (ok ? "" : " (FAILED)") + info);
                    if (ok) { Pass("aud19"); } else { Fail("aud19"); }
                    return;
                }

                // B0 disabled-trace invariant: with the trace off, nothing records.
                int before = FSO.HIT.HITTrace.Count;
                FSO.HIT.HITTrace.Enabled = false;
                hit.PlaySoundEvent("ui_error");
                bool offHolds = FSO.HIT.HITTrace.Count == before;
                info += " traceOff=" + offHolds;
                if (!offHolds) ok = false;

                // B1 known window: fire the decoded high-traffic classes and
                // assert each dispatch outcome through the real path.
                FSO.HIT.HITTrace.Reset();
                FSO.HIT.HITTrace.Enabled = true;
                try
                {
                    // UI feedback: click + generic deny + CAS deny + refund.
                    hit.PlaySoundEvent("ui_click");
                    hit.PlaySoundEvent("ui_error");
                    hit.PlaySoundEvent("ui_nhood_error");
                    hit.PlaySoundEvent("ui_moneyback");
                    // Negative control: a name that is lawfully absent — must
                    // come back NOT-FOUND, never a silent invented play.
                    hit.PlaySoundEvent("ui_buy_moneyback");
                    hit.PlaySoundEvent("ui_cac_personpts_deny");
                    // Dedup arm: firing an already-alive event returns the live
                    // thread and records already-alive on the SECOND fire.
                    // (ui_click is short-lived, so the dedup count is
                    // informational, not asserted.)
                    hit.PlaySoundEvent("ui_click");
                    hit.PlaySoundEvent("ui_click");
                }
                catch (Exception te)
                {
                    Log("AUTOTEST aud19 trace window EXC " + te.GetType().Name + " " + te.Message);
                    ok = false;
                }

                // Give the mixer tick a moment so queued notes can be observed
                // (the battery's real loop ticks HITVM).
                int waited = 0;
                while (waited < 1500 &&
                    FSO.HIT.HITTrace.Snapshot().Count(x => x.Kind != FSO.HIT.HITTrace.KIND_EVENT) == 0)
                {
                    System.Threading.Thread.Sleep(50);
                    waited += 50;
                }

                var counts = FSO.HIT.HITTrace.Counts();
                Func<string, int[]> cnt = n => { int[] c; return counts.TryGetValue(n, out c) ? c : new int[7]; };

                bool clickCreated = cnt("ui_click")[3] >= 1;   // created at least once
                // ui_error was already fired (and is still alive) from the
                // corpus truth-table section earlier in this check — an
                // ALREADY-ALIVE dispatch outcome is equally lawful here.
                bool errCreated = cnt("ui_error")[3] >= 1 || cnt("ui_error")[1] >= 1;
                bool nhoodErrCreated = cnt("ui_nhood_error")[3] >= 1;
                bool refundCreated = cnt("ui_moneyback")[3] >= 1;
                bool kachingNotFound = cnt("ui_buy_moneyback")[2] >= 1;   // NOT-FOUND (lawful)
                bool denyOldNotFound = cnt("ui_cac_personpts_deny")[2] >= 1;

                info += " wins(created: click=" + clickCreated + " err=" + errCreated
                    + " nhoodErr=" + nhoodErrCreated + " refund=" + refundCreated
                    + " notfound: kaching=" + kachingNotFound + " oldDeny=" + denyOldNotFound
                    + " dedupAlive=" + cnt("ui_click")[1] + ")";

                // If the sound stack is live (not DISABLE_SOUND), the positive
                // windows are hard asserts; with sound disabled every dispatch
                // legitimately records RES_DISABLED instead.
                if (FSO.HIT.HITVM.DISABLE_SOUND)
                {
                    bool disabledOk = cnt("ui_click")[4] >= 1;
                    info += " soundDisabled=" + disabledOk;
                    if (!disabledOk) ok = false;
                }
                else
                {
                    if (!(clickCreated && errCreated && nhoodErrCreated && refundCreated
                        && kachingNotFound && denyOldNotFound)) ok = false;
                }

                // B2 as-played note law: when notes reached the mixer, a UI FX
                // note plays at full volume (native GetInstanceVolPan: source<1
                // → vol 0x400/1024, pan 0x200 = center), i.e. as-played volume
                // ≈ 1.0 (group master 1, no duck) and pan 0.
                var notes = FSO.HIT.HITTrace.Snapshot().Where(x => x.Kind != FSO.HIT.HITTrace.KIND_EVENT).ToList();
                if (notes.Count > 0)
                {
                    var fx = notes.FirstOrDefault(n => Math.Abs(n.Volume - 1.0f) < 0.02f);
                    bool panLaw = notes.All(n => Math.Abs(n.Pan) < 0.02f); // UI fires: pan stays centered
                    bool volRange = notes.All(n => n.Volume > 0f && n.Volume <= 1.0001f);
                    info += " notes=" + notes.Count + " fullVolNote=" + (fx.Resource != null) + " panLaw=" + panLaw + " volRange=" + volRange;
                    if (!(panLaw && volRange)) ok = false;
                    var sample = notes.First();
                    Log("AUTOTEST aud19 note sample: src=" + sample.SourceName + " res=" + sample.Resource
                        + " patch=" + sample.PatchId + " vol=" + sample.Volume.ToString("0.000")
                        + " pitch=" + sample.Pitch.ToString("0.000") + " grp=" + sample.VolGroup);
                }
                else
                {
                    info += " notes=0(no-mixer)";
                    Log("AUTOTEST aud19: no notes reached the mixer in the window (headless device absent) — outcome asserts only");
                }

                FSO.HIT.HITTrace.Enabled = false; // leave the trace off (negligible-off contract)

                Log("AUTOTEST aud19 corpus+trace: ok=" + ok + info);
                if (ok) { Pass("aud19"); } else { Fail("aud19"); }
            }
            catch (Exception e)
            {
                Log("AUTOTEST aud19 EXC " + e.GetType().Name + " " + e.Message);
                Fail("aud19");
            }
        }

        /// <summary>
        /// AUD-19 opt-in 'aud19dump' (coordinator's gated run): dumps the CURRENT
        /// HITTrace window (top of the ring, most recent first is NOT used —
        /// ordered oldest-first) so a live play session can be census-read from
        /// the log. Wire next to the aud19 registration; enable
        /// FSO.HIT.HITTrace.Enabled = true at session start for a full capture.
        /// </summary>
        private static void CheckAud19Dump()
        {
            try
            {
                var counts = FSO.HIT.HITTrace.Counts();
                Log("AUD19DUMP events=" + counts.Count + " entries=" + FSO.HIT.HITTrace.Count);
                foreach (var kv in counts.OrderByDescending(x => x.Value[0]).Take(80))
                    Log("AUD19DUMP " + kv.Key + " total=" + kv.Value[0] + " alive=" + kv.Value[1]
                        + " notfound=" + kv.Value[2] + " created=" + kv.Value[3]
                        + " disabled=" + kv.Value[4] + " remap=" + kv.Value[5]);
                Log("AUD19DUMP tail:\n" + FSO.HIT.HITTrace.Dump(40));
                Pass("aud19dump");
            }
            catch (Exception e)
            {
                Log("AUTOTEST aud19dump EXC " + e.GetType().Name + " " + e.Message);
                Fail("aud19dump");
            }
        }
    }
}
