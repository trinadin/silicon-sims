using FSO.Content;
using FSO.Content.Framework;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using FSO.Vitaboy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Simitone.Client
{
    /// <summary>
    /// CC-06 opt-in pin ("cc06"): custom animated objects — the loader/binding
    /// laws for user IFF/BCF/CFP custom content, mounted through the REAL
    /// provider classes (TS1Provider + TS1BCFProvider over a private fixture
    /// root, exactly as the game constructs them), plus the live-path shipped
    /// regression through Content.Get()'s own providers.
    ///
    /// Laws pinned (native citations from the PPC PEF, sims-complete-pef):
    ///   1. DUPLICATE NAMES NEVER THROW — native VBAnimMgr::LoadAnimationFromStream
    ///      @0x1038aa30: a duplicate skeleton/suit/skill name against an unloaded
    ///      envelope silently REPLACES it (boot-time net effect: last
    ///      registration wins); against a loaded one the newcomer is silently
    ///      Released. No failure record either way. The port bug: SkelHostBCF.Add
    ///      threw ArgumentException on the second file carrying a name (custom
    ///      animation BCFs re-ship a skeleton as a CMX-format necessity),
    ///      aborting that file's remaining registrations and recording a spurious
    ///      failure — one custom BCF could knock the shipped skeletons out
    ///      depending on enumeration order.
    ///   2. CORRUPT CFP IS BOUNDED — native ReadCompressedFloats @0x10364450
    ///      returns 0 on a failed read and LoadAnimationFromStream then calls
    ///      ReportBadFormat and skips; no crash. The port bug: a truncated CFP
    ///      threw EndOfStreamException (and a hostile one IndexOutOfRange) out
    ///      of AvatarAnimations.Get — with no catch anywhere in the VM, that is
    ///      a game crash the first time a Sim plays the custom object's
    ///      animation. Port law: record the failure (provider table + the game's
    ///      bounded content-failure channel) and serve the animation
    ///      untranslated, the same observable class as the missing-CFP law
    ///      (CC-01 matrix §5 / CC-03).
    ///   3. CFP DECODER IS CLAMPED — native keeps a pending-repeat counter
    ///      consumed one slot per iteration inside count-bounded loops, so an
    ///      overshooting 0xFE repeat block can never write past the destination,
    ///      and a code above the 0xFD-entry delta table (InitFloatCompression
    ///      @0x10364330 fills exactly 0xFD entries) reads the zeroed static
    ///      memory just past the table — effectively delta 0 — instead of
    ///      faulting. Port ReadNFloats now mirrors that structure 1:1.
    ///   4. UNTRANSLATED ANIMATIONS RENDER AS STILL POSES — an animation served
    ///      without pose data (missing/corrupt CFP) must leave every bone it
    ///      moves at its current pose, not crash the renderer (native: the skill
    ///      simply has no data to apply).
    ///   5. NULL SKELETON IS SURVIVABLE — native FindSkeleton @0x10387680
    ///      returns 0 on a miss and the animation does not play. The port
    ///      renderer/head-seek/draw paths must tolerate an avatar whose
    ///      skeleton could not be resolved (custom skeleton removed from a
    ///      save, or a registration knocked out per law 1's old bug).
    ///   6. SHIPPED CORPUS UNCHANGED — the dart board's own shipped animations
    ///      still enrich with exact count agreement and render a full pose pass
    ///      through the live in-game providers, and the game's own boot scan
    ///      records zero duplicate-name failures.
    ///
    /// The fixture is synthesized at runtime from the SHIPPED corpus through the
    /// live providers (dart_board.iff + the a2o-playdarts BCF/CFP pair): the
    /// shipped animation is re-identified under custom names, its skeleton is
    /// re-shipped twice under one custom rig id (the duplicate law), one CFP is
    /// truncated, one overshoots, one is absent. Headless byte-splice variants
    /// (STR#129 rebinding with size-preserving splices, DGRP frame-index
    /// out-of-range) are additionally pinned by the banked harness at
    /// coordination/evidence/CC-06/tools/harness (13/13 after the fixes).
    ///
    /// COORDINATOR WIRING (not registered here): inside a
    /// CheckEnabled("cc06") branch once content is initialized (the runner's
    /// init or first-game-screen hook), call AutotestCc06.Run(); Ran / Passed /
    /// Failures / Diagnostics are the verdict. Pure static law probe — no
    /// screen takeover. Touches no game state beyond a temp fixture directory
    /// under FSOEnvironment.UserDir (removed on exit) and a save/restored
    /// TS1BasePath/TS1AllFiles window on the content singleton (restored in
    /// finally; the probe is single-threaded with the main loop).
    /// </summary>
    public static class AutotestCc06
    {
        // fixture identities (same set as the banked harness)
        private const string AnimCustom = "cc06-custom-loop-11";
        private const string AnimNocfp = "cc06-nocfp-loop-2222";
        private const string AnimBoneBind = "cc06-bonebind-loop-2";
        private const string AnimBadCfp = "cc06-badcfp-loop";
        private const string AnimOvershoot = "cc06-overshoot-loop";
        private const string CustomRig = "cc06-customrig";

        public static bool Ran;
        public static bool Passed;
        public static readonly List<string> Failures = new List<string>();
        public static readonly List<string> Diagnostics = new List<string>();

        private static int _pass, _fail;

        public static void Run(Action<string> log = null)
        {
            Ran = true;
            Passed = false;
            Failures.Clear();
            Diagnostics.Clear();
            _pass = 0; _fail = 0;
            string tempRoot = null;
            var content = Content.Get();
            var priorBasePath = content.TS1BasePath;
            var priorAllFiles = content.TS1AllFiles;
            var priorFailedCount = Content.FailedContentFiles.Count;
            // The probe's private provider Init RESETS the static
            // FAR1Provider.MountedResourceCounts registry (line 238: Clear +
            // refill from the initializing provider) — the live game's counts
            // must be snapshotted and restored or the later 'iff' census
            // reads the (deleted) temp farm's zeros.
            var priorMountedCounts = new Dictionary<string, int>(
                FSO.Content.Framework.FAR1Provider<object>.MountedResourceCounts);
            try
            {
                if (content?.BCFGlobal == null || content?.TS1Global == null)
                {
                    Fail("prereq", "content not initialized (BCFGlobal/TS1Global null) — wire the probe after Content.Init");
                    return;
                }

                // ---- fixture synthesis from the SHIPPED corpus via live providers ----
                tempRoot = Path.Combine(FSO.Common.FSOEnvironment.UserDir ?? ".", "cc06probe");
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch { }
                var dl = Path.Combine(tempRoot, "Downloads", "cc06");
                Directory.CreateDirectory(dl);

                var liveB = content.BCFGlobal;
                string hostFile;
                if (!liveB.AnimHostBCF.TryGetValue("a2o-playdarts-start", out hostFile))
                {
                    Fail("prereq", "shipped a2o-playdarts-start not indexed by the live provider");
                    return;
                }
                var shippedBcf = liveB.BCFProvider.Get(hostFile) as BCF;
                var shippedCfp = liveB.CFPProvider.Get("xskill-a2o-playdarts-start.cfp") as CFP;
                if (shippedBcf == null || shippedCfp == null || shippedCfp.Data == null || shippedCfp.Data.Length == 0)
                {
                    Fail("prereq", "shipped dart BCF/CFP pair unavailable (bcf=" + (shippedBcf != null) + " cfp=" + (shippedCfp != null) + ")");
                    return;
                }

                var shippedAnim = shippedBcf.Animations.FirstOrDefault(a => a.Name.ToLowerInvariant() == "a2o-playdarts-start");
                if (shippedAnim == null)
                {
                    Fail("prereq", "a2o-playdarts-start animation missing from its host BCF");
                    return;
                }

                // the rig source is the shipped ADULT skeleton BCF — the shipped
                // animation BCFs carry zero skeletons; real custom animation BCFs
                // re-ship the skeleton themselves, which is exactly what the
                // fixture reproduces.
                string skelHost;
                if (!liveB.SkelHostBCF.TryGetValue("adult", out skelHost))
                {
                    Fail("prereq", "shipped adult skeleton not indexed by the live provider");
                    return;
                }
                var skelBcf = liveB.BCFProvider.Get(skelHost) as BCF;
                if (skelBcf == null || skelBcf.Skeletons.Length == 0)
                {
                    Fail("prereq", "adult skeleton host BCF unavailable");
                    return;
                }

                // cc06-animtest.bcf: BOTH the custom rig and a re-identified
                // copy of the shipped animation set (fresh clones — the live
                // cached objects must not be mutated).
                var rigSkel = skelBcf.Skeletons[0].Clone();
                rigSkel.Name = CustomRig;
                var anims = new List<Animation>
                {
                    CloneAnim(shippedAnim, AnimCustom, null),
                    CloneAnim(shippedAnim, AnimNocfp, null),
                    CloneAnim(shippedAnim, AnimBoneBind, a => a.Motions[0].BoneName = "CC06BONE0"),
                    CloneAnim(shippedAnim, AnimBadCfp, null),
                    CloneAnim(shippedAnim, AnimOvershoot, null),
                };
                var animtest = new BCF(new[] { rigSkel }, new Appearance[0], anims.ToArray());
                using (var s = File.Create(Path.Combine(dl, "cc06-animtest.bcf")))
                    animtest.Write(s, false); // binary BCF — the format the .bcf SmartCodec decodes

                // cc06-customrig.bcf: the SAME rig id again from a second file —
                // the duplicate-name registration law.
                var rigSkel2 = skelBcf.Skeletons[0].Clone();
                rigSkel2.Name = CustomRig;
                var rigBcf = new BCF(new[] { rigSkel2 }, new Appearance[0], new Animation[0]);
                using (var s = File.Create(Path.Combine(dl, "cc06-customrig.bcf")))
                    rigBcf.Write(s, false);

                // CFPs: verbatim / truncated / overshoot / absent
                var cfpBytes = shippedCfp.Data;
                File.WriteAllBytes(Path.Combine(dl, AnimCustom + ".cfp"), cfpBytes);
                File.WriteAllBytes(Path.Combine(dl, AnimBoneBind + ".cfp"), cfpBytes);
                File.WriteAllBytes(Path.Combine(dl, AnimBadCfp + ".cfp"), cfpBytes.Take(cfpBytes.Length / 3).ToArray());
                File.WriteAllBytes(Path.Combine(dl, AnimOvershoot + ".cfp"), new byte[] { 0xFE, 0xE8, 0x03 });
                // no CFP for AnimNocfp (missing-file law)

                // ---- private mount through the REAL provider classes ----
                var entries = new List<string>();
                foreach (var f in Directory.GetFiles(tempRoot, "*", SearchOption.AllDirectories))
                    entries.Add(f.Substring(tempRoot.Length).TrimStart('/', '\\').Replace('\\', '/'));
                content.TS1BasePath = tempRoot + Path.DirectorySeparatorChar;
                content.TS1AllFiles = entries.ToArray();
                var ts1prov = new TS1Provider(content);
                ts1prov.Init();
                var B = new TS1BCFProvider(content, ts1prov);
                B.Init();
                var animsProv = new TS1BCFAnimationProvider(B);
                var skelsProv = new TS1BCFSkeletonProvider(B);

                // CC06-01 mount: the fixture animation is indexed by the real scan
                if (!B.AnimHostBCF.ContainsKey(AnimCustom)) Fail("01-mount", "fixture anim not indexed");
                else Pass("01-mount");

                // CC06-02 bind: custom-id animation enriches from its CFP
                Animation aCustom = null;
                try { aCustom = animsProv.Get(AnimCustom + ".anim"); }
                catch (Exception e) { Fail("02-bind", "Get threw " + e.GetType().Name); }
                if (aCustom == null) Fail("02-bind", "null");
                else if (aCustom.Translations == null) Fail("02-bind", "translations null (CFP not enriched)");
                else if (aCustom.Translations.Length != aCustom.TranslationCount) Fail("02-bind", "translation count mismatch");
                else if (aCustom.NumFrames <= 0) Fail("02-bind", "no frames");
                else Pass("02-bind");
                Note($"02 anim {AnimCustom}: motions={aCustom?.Motions?.Length} frames={aCustom?.NumFrames} translations={aCustom?.Translations?.Length}");

                // CC06-03 strbind: object STR#129 binding law — the object's
                // animation-table string resolves through the provider exactly
                // as VMMemory.GetAnimation does (name + ".anim"); fixture names
                // through the fixture provider, and the LIVE dart board's own
                // entries 1-3 through the LIVE provider (shipped regression).
                try
                {
                    bool allBind = true; var why = "";
                    foreach (var name in new[] { AnimCustom, AnimNocfp, AnimBoneBind })
                    {
                        var anim = animsProv.Get(name + ".anim");
                        if (anim == null) { allBind = false; why += " " + name; }
                    }
                    var dartIff = content.TS1Global.Get("dart_board.iff") as IffFile;
                    if (dartIff == null) { allBind = false; why += " (dart_board.iff not mounted)"; }
                    else
                    {
                        var str129 = dartIff.Get<STR>(129);
                        for (var i = 1; i <= 3; i++)
                        {
                            var name = str129.GetString(i);
                            var anim = (name == null) ? null : content.AvatarAnimations.Get(name + ".anim");
                            Note($"03 STR#129[{i}] = '{name}' -> {(anim == null ? "NULL" : "ok")}");
                            if (anim == null) { allBind = false; why += " live:" + name; }
                        }
                    }
                    if (allBind) Pass("03-strbind"); else Fail("03-strbind", "unresolved:" + why);
                }
                catch (Exception e) { Fail("03-strbind", e.GetType().Name + " " + e.Message); }

                // CC06-04 skeldup: duplicate skeleton id across two files —
                // no throw, no failure record, the id still resolves with bones
                // (native: silent replace/release; law 1).
                var dupFailure = B.FailedFiles.FirstOrDefault(f =>
                    (f.ErrorMessage ?? "").Contains("same key", StringComparison.OrdinalIgnoreCase)
                    || f.ErrorType == "ArgumentException");
                string dupHost;
                var hasHost = B.SkelHostBCF.TryGetValue(CustomRig, out dupHost);
                Note("04 SkelHostBCF[" + CustomRig + "] = " + dupHost + " (duplicate carried by cc06-animtest.bcf AND cc06-customrig.bcf)");
                if (dupFailure != null) Fail("04-skeldup", "spurious failure recorded: " + dupFailure.Filename + " | " + dupFailure.ErrorMessage);
                else if (!hasHost) Fail("04-skeldup", "duplicate id dropped entirely (null)");
                else Pass("04-skeldup");

                // CC06-05 customrig: the winning registration serves a full skeleton
                var rig = skelsProv.Get(CustomRig + ".skel");
                if (rig == null) Fail("05-customrig", "null");
                else if (rig.Bones == null || rig.Bones.Length == 0) Fail("05-customrig", "no bones");
                else if (rig.RootBone == null) Fail("05-customrig", "no root bone");
                else Pass("05-customrig");
                Note($"05 rig bones={rig?.Bones?.Length} root={(rig == null || rig.RootBone == null ? "-" : rig.RootBone.Name)}");

                // CC06-06 badcfp: truncated CFP — no throw out of Get, animation
                // served untranslated, failure recorded in BOTH channels.
                Animation aBad = null; Exception badEx = null;
                var failCountBefore = B.FailedFiles.Count;
                try { aBad = animsProv.Get(AnimBadCfp + ".anim"); }
                catch (Exception e) { badEx = e; }
                var boundedEntry = Content.FailedContentFiles.Skip(priorFailedCount)
                    .FirstOrDefault(f => (f.Filename ?? "").Contains(AnimBadCfp));
                if (badEx != null) Fail("06-badcfp", "Get threw " + badEx.GetType().Name + ": " + badEx.Message);
                else if (aBad == null) Fail("06-badcfp", "anim null (dropped, not untranslated)");
                else if (B.FailedFiles.Count == failCountBefore && boundedEntry == null)
                    Fail("06-badcfp", "served but failure not recorded");
                else Pass("06-badcfp");
                Note("06 bounded-channel entry: " + (boundedEntry == null ? "(provider table only)" : boundedEntry.Filename));

                // CC06-07 overshoot: hostile 0xFE repeat block — clamped, no throw
                Animation aOver = null; Exception overEx = null;
                try { aOver = animsProv.Get(AnimOvershoot + ".anim"); }
                catch (Exception e) { overEx = e; }
                if (overEx != null) Fail("07-overshoot", "Get threw " + overEx.GetType().Name + ": " + overEx.Message);
                else if (aOver == null) Fail("07-overshoot", "anim null");
                else Pass("07-overshoot");

                // CC06-08 nocfp + still-pose law: an animation served without
                // pose data renders as a STILL pose — every bone it moves keeps
                // its current translation/rotation across all frames.
                var adultSkel = content.AvatarSkeletons.Get("adult.skel");
                var avatar = new FSO.Vitaboy.SimAvatar(adultSkel);
                Animation aNocfp = null; Exception nocfpEx = null;
                try { aNocfp = animsProv.Get(AnimNocfp + ".anim"); }
                catch (Exception e) { nocfpEx = e; }
                if (nocfpEx != null) Fail("08-nocfp", "Get threw " + nocfpEx.GetType().Name);
                else if (aNocfp == null) Fail("08-nocfp", "null");
                else
                {
                    var renderEx = RenderAll(aNocfp, avatar);
                    if (renderEx != null) Fail("08-nocfp", "render threw " + renderEx.GetType().Name + " " + renderEx.Message);
                    else
                    {
                        // still-pose law: bind pose untouched by the untranslated anim
                        var fresh = new FSO.Vitaboy.SimAvatar(adultSkel);
                        var bone = fresh.Skeleton.GetBone("PELVIS") ?? fresh.Skeleton.RootBone;
                        var before = bone.Translation;
                        FSO.Vitaboy.Animator.RenderFrame(fresh, aNocfp, Math.Max(1, aNocfp.NumFrames / 2), 0.5f, 1f);
                        if (before != bone.Translation) Fail("08-nocfp", "untranslated anim moved a bone (must be a still pose)");
                        else Pass("08-nocfp");
                    }
                }

                // CC06-09 missingbone: a motion naming a bone the skeleton lacks
                // is skipped, not crashed
                var aBone = animsProv.Get(AnimBoneBind + ".anim");
                var boneEx = RenderAll(aBone, avatar);
                if (boneEx != null) Fail("09-missingbone", boneEx.GetType().Name + " " + boneEx.Message);
                else Pass("09-missingbone");

                // CC06-10 missingskel: absent rig -> provider null (native
                // FindSkeleton @0x10387680 returns 0); null-skeleton avatar is
                // survivable through render/head-seek/strip paths.
                var missing = skelsProv.Get("cc06-missing-rig.skel");
                if (missing != null) Fail("10-missingskel", "expected null, got " + missing.Name);
                else
                {
                    var nullAv = new FSO.Vitaboy.SimAvatar((Skeleton)null);
                    var ex2 = RenderAll(aCustom, nullAv);
                    var seekQ = FSO.Vitaboy.Animator.CalculateHeadSeek(nullAv, new Microsoft.Xna.Framework.Vector3(1, 1, 1), 0f);
                    FSO.Vitaboy.Animator.ApplyHeadSeek(nullAv, seekQ, 0.5f);
                    nullAv.StripAllButHead();
                    nullAv.ReloadSkeleton();
                    if (ex2 != null) Fail("10-missingskel", "render threw " + ex2.GetType().Name + " " + ex2.Message);
                    else if (seekQ != Microsoft.Xna.Framework.Quaternion.Identity) Fail("10-missingskel", "head seek on null skeleton should be identity");
                    else Pass("10-missingskel");
                }

                // CC06-11 shipped regression through the LIVE providers: the
                // dart board's own animations enrich with exact count agreement,
                // in-range motion windows, and a full render pass on the adult
                // skeleton.
                try
                {
                    string[] shipped = { "a2o-playdarts-start", "a2o-playdarts-throw1", "a2o-playdarts-throw2",
                                         "a2o-playdarts-throw3", "a2o-playdarts-badshot", "a2o-playdarts-goodshot" };
                    int ok = 0; var why = "";
                    foreach (var name in shipped)
                    {
                        var a = content.AvatarAnimations.Get(name + ".anim");
                        if (a == null) { why += $" {name}:null"; continue; }
                        if (a.Translations == null) { why += $" {name}:untranslated"; continue; }
                        if (a.Translations.Length != a.TranslationCount) { why += $" {name}:count-mismatch"; continue; }
                        var idxOk = true;
                        foreach (var m in a.Motions)
                        {
                            if (m.HasTranslation && (m.FirstTranslationIndex < 0 ||
                                m.FirstTranslationIndex + (int)m.FrameCount > a.Translations.Length)) { idxOk = false; break; }
                            if (m.HasRotation && (m.FirstRotationIndex < 0 ||
                                m.FirstRotationIndex + (int)m.FrameCount > a.Rotations.Length)) { idxOk = false; break; }
                        }
                        if (!idxOk) { why += $" {name}:idx-oob"; continue; }
                        ok++;
                    }
                    var renderEx = (ok == shipped.Length) ? RenderAll(content.AvatarAnimations.Get("a2o-playdarts-start.anim"), avatar) : null;
                    if (ok == shipped.Length && renderEx == null) Pass("11-shipped-regression");
                    else Fail("11-shipped-regression", ok + "/" + shipped.Length + why + (renderEx == null ? "" : " render:" + renderEx.GetType().Name));
                }
                catch (Exception e) { Fail("11-shipped-regression", e.GetType().Name + " " + e.Message); }

                // CC06-12 boot-clean: the game's own boot scan recorded zero
                // duplicate-name (ArgumentException) failures — the shipped
                // corpus alone never trips law 1.
                var bootDup = liveB.FailedFiles.FirstOrDefault(f =>
                    (f.ErrorMessage ?? "").Contains("same key", StringComparison.OrdinalIgnoreCase)
                    || f.ErrorType == "ArgumentException");
                if (bootDup != null) Fail("12-bootclean", "boot recorded a duplicate-name failure: " + bootDup.Filename + " | " + bootDup.ErrorMessage);
                else Pass("12-bootclean");
            }
            catch (Exception e)
            {
                Fail("harness", e.GetType().Name + ": " + e.Message + " @ " + e.StackTrace?.Split('\n')[0]);
            }
            finally
            {
                // restore the live content singleton's TS1 state before returning
                content.TS1BasePath = priorBasePath;
                content.TS1AllFiles = priorAllFiles;
                FSO.Content.Framework.FAR1Provider<object>.MountedResourceCounts.Clear();
                foreach (var kv in priorMountedCounts)
                    FSO.Content.Framework.FAR1Provider<object>.MountedResourceCounts[kv.Key] = kv.Value;
                if (tempRoot != null)
                {
                    try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch { }
                }
            }

            Passed = _fail == 0;
            Diagnostics.Add($"cc06 probe: {_pass} pass / {_fail} fail");
            (log ?? (s => { }))("AUTOTEST cc06: " + (_fail == 0 ? "PASS" : "FAIL") + $" ({_pass}/{_pass + _fail})");
            foreach (var f in Failures) (log ?? (s => { }))( "AUTOTEST cc06 FAIL: " + f);
        }

        private static void Pass(string id) { _pass++; Diagnostics.Add("PASS " + id); }
        private static void Fail(string id, string why)
        {
            _fail++;
            Failures.Add(id + ": " + why);
            Diagnostics.Add("FAIL " + id + ": " + why);
        }
        private static void Note(string s) { Diagnostics.Add("note " + s); }

        /// <summary>
        /// Fresh clone of an animation read from a live cached BCF — the cached
        /// object (used by the running game) is never mutated. Motion property
        /// lists are shared read-only.
        /// </summary>
        private static Animation CloneAnim(Animation src, string name, Action<Animation> tweak)
        {
            var a = new Animation
            {
                Name = name,
                XSkillName = name,
                Duration = src.Duration,
                Distance = src.Distance,
                IsMoving = src.IsMoving,
                TranslationCount = src.TranslationCount,
                RotationCount = src.RotationCount,
                NumFrames = src.NumFrames,
                Motions = src.Motions.Select(m => new AnimationMotion
                {
                    BoneName = m.BoneName,
                    FrameCount = m.FrameCount,
                    Duration = m.Duration,
                    HasTranslation = m.HasTranslation,
                    HasRotation = m.HasRotation,
                    FirstTranslationIndex = m.FirstTranslationIndex,
                    FirstRotationIndex = m.FirstRotationIndex,
                    Properties = m.Properties,
                    TimeProperties = m.TimeProperties,
                }).ToArray(),
            };
            a.UpdateFPS();
            tweak?.Invoke(a);
            return a;
        }

        private static Exception RenderAll(Animation anim, FSO.Vitaboy.Avatar avatar)
        {
            if (anim == null) return null;
            try
            {
                for (int f = 0; f <= anim.NumFrames + 2; f++)
                {
                    FSO.Vitaboy.Animator.RenderFrame(avatar, anim, f, 0f, 1f);
                }
                return null;
            }
            catch (Exception e) { return e; }
        }
    }
}
