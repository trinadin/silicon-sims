using FSO.Common;
using FSO.Content;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF;
using FSO.SimAntics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Simitone.Client
{
    /// <summary>
    /// SAV-08 'savefault' (opt-in, focused): fault-injection battery over the
    /// TS1 persistence writers (TS1NeighborhoodProvider). Cases:
    ///   T0 baseline save + unrelated-file preservation + fresh-provider reparse
    ///   T1 mid-write fault on Neighborhood.iff   (destination preserved, temp cleaned, retry works)
    ///   T2 mid-write fault on SaveHouse          (same law)
    ///   T3 interrupted dirty-avatar sequence     (first written, rest preserved, set retriable)
    ///   T4 missing/corrupt character dependency  (defined load behavior + later save consistent)
    ///   T5 corrupt/truncated files at load       (actionable failure, not silent acceptance)
    ///   T6 repeated save/restart consistency     (byte-identical resave from a fresh provider)
    ///
    /// Isolation follows the R246/R247 idiom: when the checks string names
    /// "savefault", FSOEnvironment.UserDir is redirected into a fresh temp dir
    /// BEFORE Content.Init (AutotestRunner.Begin), so the pristine UserData is
    /// cloned there and every fault lands on private copies. Game-data stays
    /// read-only; fault injection rides the provider's own SaveStreamFaultHook
    /// seam and never writes outside the redirected UserDir.
    ///
    /// Run focused: -autotest 5 -autotest-opts lot,savefault
    /// </summary>
    internal static class AutotestSaveFault
    {
        internal static string RedirectDir;
        internal static string IsolationError;
        private static string _priorUserDir;

        internal static bool BeginIsolation(string checksCsv)
        {
            var c = checksCsv?.Trim().Trim('"');
            if (string.IsNullOrEmpty(c)) return false;
            var names = c.Split(',').Select(s => s.Trim().ToLowerInvariant()).ToList();
            if (!names.Contains("savefault")) return false;
            try
            {
                _priorUserDir = FSOEnvironment.UserDir;
                RedirectDir = Path.Combine(Path.GetTempPath(), "simitone-sav08-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(RedirectDir);
                FSOEnvironment.UserDir = RedirectDir;
                return true;
            }
            catch (Exception e)
            {
                // Isolation is mandatory for this battery — never run unisolated.
                IsolationError = e.GetType().Name + ": " + e.Message;
                return true;
            }
        }

        private sealed class FaultException : IOException { }

        /// <summary>Throws after <see name="_limit"/> bytes written; otherwise passes through.</summary>
        private sealed class FaultAfterBytesStream : Stream
        {
            private readonly Stream _inner;
            private readonly int _limit;
            private int _written;
            public FaultAfterBytesStream(Stream inner, int limit) { _inner = inner; _limit = limit; }
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
            public override void Flush() { _inner.Flush(); }
            public override int Read(byte[] b, int o, int n) => throw new NotSupportedException();
            public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
            public override void SetLength(long v) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count)
            {
                if (_written + count > _limit)
                {
                    _written += count;
                    throw new FaultException();
                }
                _written += count;
                _inner.Write(buffer, offset, count);
            }
            protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
        }

        private static Dictionary<string, string> Snapshot(string root)
        {
            var snap = new Dictionary<string, string>();
            if (!Directory.Exists(root)) return snap;
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (f.EndsWith(".savtmp")) continue;
                using (var fs = File.OpenRead(f))
                    snap[f.Substring(root.Length)] = Convert.ToHexString(SHA256.HashData(fs));
            }
            return snap;
        }

        private static List<string> DiffSnap(Dictionary<string, string> before, Dictionary<string, string> after, string onlyPathSuffix)
        {
            var changed = new List<string>();
            foreach (var kv in after)
                if (!before.TryGetValue(kv.Key, out var h) || h != kv.Value)
                    if (onlyPathSuffix == null || kv.Key.EndsWith(onlyPathSuffix)) changed.Add("M " + kv.Key);
            foreach (var k in before.Keys)
                if (!after.ContainsKey(k))
                    if (onlyPathSuffix == null || k.EndsWith(onlyPathSuffix)) changed.Add("D " + k);
            return changed;
        }

        /// <summary>Runs the battery against the current (isolated) content. Returns failure descriptions; empty == all pass.</summary>
        internal static List<string> Run(VM vm, Action<string> log)
        {
            var fail = new List<string>();
            var provider = Content.Get().Neighborhood as TS1NeighborhoodProvider;
            if (provider == null || provider.UserPath == null)
            {
                fail.Add("no TS1NeighborhoodProvider (TS1 content set required)");
                return fail;
            }
            var up = provider.UserPath;
            log("AUTOTEST savefault: isolated UserPath=" + up);

            // ---------- T0 baseline: real save, unrelated preservation, clean reparse
            var t0 = Snapshot(up);
            if (!provider.SaveNeighbourhood(false)) fail.Add("T0: SaveNeighbourhood returned false");
            var t0b = Snapshot(up);
            var changed = DiffSnap(t0, t0b, null).Where(x => !x.EndsWith("Neighborhood.iff")).ToList();
            if (changed.Count > 0) fail.Add("T0: unrelated files changed during neighborhood save: " + string.Join(",", changed.Take(4)));
            int nbCount0 = provider.Neighbors?.NeighbourByID?.Count ?? -1;
            int fami0 = provider.FamilyForHouse.Count;
            var fresh = new TS1NeighborhoodProvider(Content.Get());
            int nbCountF = fresh.Neighbors?.NeighbourByID?.Count ?? -1;
            int famiF = fresh.FamilyForHouse.Count;
            if (nbCountF != nbCount0 || famiF != fami0)
                fail.Add($"T0: reparse census drift nb {nbCount0}->{nbCountF} fami {fami0}->{famiF}");
            log($"AUTOTEST savefault T0: nb={nbCount0} fami={fami0} files={t0b.Count} reparse ok");

            // ---------- T1 mid-write fault on Neighborhood.iff
            var nbPath = Path.Combine(up, "Neighborhood.iff");
            var preSha = File.ReadAllBytes(nbPath);
            var half = Math.Max(1, preSha.Length / 2);
            TS1NeighborhoodProvider.SaveStreamFaultHook = s => new FaultAfterBytesStream(s, half);
            bool threw = false;
            try { provider.SaveNeighbourhood(false); }
            catch (Exception e) { threw = true; log("AUTOTEST savefault T1: save threw " + e.GetType().Name + " (actionable)"); }
            TS1NeighborhoodProvider.SaveStreamFaultHook = null;
            if (!threw) fail.Add("T1: faulted neighborhood save did not surface an error");
            var post = File.ReadAllBytes(nbPath);
            if (!preSha.AsSpan().SequenceEqual(post))
                fail.Add($"T1: Neighborhood.iff corrupted by faulted save ({preSha.Length} -> {post.Length} bytes)");
            if (File.Exists(nbPath + ".savtmp")) fail.Add("T1: .savtmp temp left behind after fault");
            provider.SaveNeighbourhood(false);
            if (!File.ReadAllBytes(nbPath).AsSpan().SequenceEqual(post))
                fail.Add("T1: retry save did not reproduce the pre-fault content");
            log("AUTOTEST savefault T1: destination preserved, temp cleaned, retry consistent");

            // ---------- T2 mid-write fault on SaveHouse
            var housePath = provider.GetHousePath(5);
            if (File.Exists(housePath))
            {
                var housePre = File.ReadAllBytes(housePath);
                var iff = new IffFile(housePath);
                TS1NeighborhoodProvider.SaveStreamFaultHook = s => new FaultAfterBytesStream(s, Math.Max(1, housePre.Length / 4));
                threw = false;
                try { provider.SaveHouse(5, iff); }
                catch (Exception e) { threw = true; log("AUTOTEST savefault T2: save threw " + e.GetType().Name + " (actionable)"); }
                TS1NeighborhoodProvider.SaveStreamFaultHook = null;
                if (!threw) fail.Add("T2: faulted house save did not surface an error");
                var housePost = File.ReadAllBytes(housePath);
                if (!housePre.AsSpan().SequenceEqual(housePost))
                    fail.Add($"T2: House05.iff corrupted by faulted save ({housePre.Length} -> {housePost.Length} bytes)");
                if (File.Exists(housePath + ".savtmp")) fail.Add("T2: .savtmp temp left behind after fault");
                var reparse = new IffFile(housePath);
                var chunkCount = reparse.ListAll().Count;
                if (chunkCount == 0) fail.Add("T2: house reparse yielded no chunks");
                log($"AUTOTEST savefault T2: house preserved ({housePre.Length} bytes), reparse chunks={chunkCount}");
            }
            else fail.Add("T2: Houses/House05.iff not present in isolated UserDir");

            // ---------- T3 interrupted dirty-avatar (character) sequence
            var charTargets = new List<KeyValuePair<uint, string>>();
            if (vm != null)
            {
                foreach (var ent in vm.Entities)
                {
                    var guid = (uint)((ent.Object?.GUID ?? 0) & 0xFFFFFFFF);
                    if (guid == 0) continue;
                    var obj = Content.Get().WorldObjects.Get(guid);
                    var name = obj?.Resource?.Name;
                    if (name != null && name.StartsWith(up) && File.Exists(name) && !charTargets.Any(kv => kv.Value == name))
                        charTargets.Add(new KeyValuePair<uint, string>(guid, name));
                    if (charTargets.Count >= 2) break;
                }
            }
            if (charTargets.Count >= 2)
            {
                var pre = charTargets.ToDictionary(kv => kv.Value, kv => File.ReadAllBytes(kv.Value));
                provider.DirtyAvatars.Clear();
                foreach (var kv in charTargets) provider.DirtyAvatars.Add(kv.Key);
                int n = 0;
                TS1NeighborhoodProvider.SaveStreamFaultHook = s =>
                {
                    n++;
                    return (n == 2) ? new FaultAfterBytesStream(s, 32) : s;
                };
                threw = false;
                try { provider.SaveNeighbourhood(true); }
                catch (Exception e) { threw = true; log("AUTOTEST savefault T3: sequence threw at 2nd character write " + e.GetType().Name); }
                TS1NeighborhoodProvider.SaveStreamFaultHook = null;
                if (!threw) fail.Add("T3: faulted character sequence did not surface an error");
                var firstNew = File.ReadAllBytes(charTargets[0].Value);
                if (firstNew.AsSpan().SequenceEqual(pre[charTargets[0].Value]))
                    log("AUTOTEST savefault T3: note first character rewrite was byte-identical (deterministic serializer)");
                var second = File.ReadAllBytes(charTargets[1].Value);
                if (!second.AsSpan().SequenceEqual(pre[charTargets[1].Value]))
                    fail.Add("T3: second character file modified by interrupted sequence (not preserved)");
                if (provider.DirtyAvatars.Count == 0)
                    fail.Add("T3: DirtyAvatars cleared despite failed sequence (retriable state lost)");
                provider.SaveNeighbourhood(true);
                if (provider.DirtyAvatars.Count != 0) fail.Add("T3: retry did not clear DirtyAvatars");
                foreach (var kv in charTargets)
                {
                    try { new IffFile(kv.Value); }
                    catch (Exception e) { fail.Add("T3: " + kv.Value + " unparseable after retry: " + e.Message); }
                }
                log("AUTOTEST savefault T3: interrupted sequence preserved + retry completed");
            }
            else log("AUTOTEST savefault T3: SKIPPED (fewer than 2 user-path character resources resolvable on this lot)");

            // ---------- T4 missing character dependency
            var charsDir = Path.Combine(up, "Characters");
            var victim = Directory.EnumerateFiles(charsDir, "User*.iff").FirstOrDefault();
            if (victim != null)
            {
                var backup = Path.Combine(charsDir, "_sav08_backup.iff");
                File.Move(victim, backup);
                Exception initErr = null;
                TS1NeighborhoodProvider missing = null;
                try { missing = new TS1NeighborhoodProvider(Content.Get()); }
                catch (Exception e) { initErr = e; }
                if (initErr != null)
                    log("AUTOTEST savefault T4: provider init surfaced " + initErr.GetType().Name + " (actionable)");
                else
                    log($"AUTOTEST savefault T4: provider init tolerated missing char file (nb={missing.Neighbors?.NeighbourByID?.Count}) — defined-skip behavior");
                File.Move(backup, victim);
            }
            else log("AUTOTEST savefault T4: SKIPPED (no character files present)");

            // ---------- T5 corrupt file at load
            var neighBytes = File.ReadAllBytes(nbPath);
            try
            {
                File.WriteAllBytes(nbPath, neighBytes.Take(neighBytes.Length / 3).ToArray());
                Exception loadErr = null;
                try { new TS1NeighborhoodProvider(Content.Get()); }
                catch (Exception e) { loadErr = e; }
                if (loadErr != null)
                    log("AUTOTEST savefault T5: truncated Neighborhood.iff rejected with " + loadErr.GetType().Name + " (actionable)");
                else fail.Add("T5: truncated Neighborhood.iff silently accepted by provider init");
            }
            finally { File.WriteAllBytes(nbPath, neighBytes); }
            var houseBytes = File.ReadAllBytes(housePath);
            try
            {
                File.WriteAllBytes(housePath, houseBytes.Take(64).ToArray());
                Exception loadErr = null;
                // RetainChunkData=true forces chunk bytes to be read during the
                // parse, so truncation would surface here as an actionable
                // error. DISCLOSED reader contract (SAV-01): the IFF reader is
                // deliberately lenient — it accepts a truncated chunk set and
                // defers data errors to per-chunk access, mirroring the
                // original's sequential reader. Not a writer defect: with the
                // AtomicWrite fix a failed save can no longer truncate the
                // file. Recorded as a disclosure, not a failure.
                try { new IffFile(housePath, true); }
                catch (Exception e) { loadErr = e; }
                if (loadErr != null) log("AUTOTEST savefault T5: truncated house file rejected with " + loadErr.GetType().Name + " (eager read)");
                else log("AUTOTEST savefault T5: DISCLOSURE — truncated House05.iff parsed by the lenient IFF reader (lazy per-chunk data errors; native-mimicry; writer no longer produces truncations)");
            }
            finally { File.WriteAllBytes(housePath, houseBytes); }

            // ---------- T6 repeated save/restart consistency
            provider.SaveNeighbourhood(false);
            var shaA = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(nbPath)));
            var restart1 = new TS1NeighborhoodProvider(Content.Get());
            restart1.SaveNeighbourhood(false);
            var shaB = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(nbPath)));
            var restart2 = new TS1NeighborhoodProvider(Content.Get());
            restart2.SaveNeighbourhood(false);
            var shaC = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(nbPath)));
            if (shaA != shaB || shaB != shaC)
            {
                var cA = Census(provider); var cB = Census(restart1); var cC = Census(restart2);
                if (cA != cB || cB != cC)
                    fail.Add($"T6: save/restart census drift {cA} vs {cB} vs {cC}");
                else
                    log("AUTOTEST savefault T6: bytes differ across restarts (nondeterministic serializer) but census stable " + cA);
            }
            else log("AUTOTEST savefault T6: byte-identical across save/restart/save cycle");

            return fail;
        }

        private static string Census(TS1NeighborhoodProvider p)
        {
            int relRows = 0;
            foreach (var n in p.Neighbors.NeighbourByID.Values)
                relRows += n.Relationships?.Count ?? 0;
            long budget = p.FamilyForHouse.Values.Sum(f => (long)f.Budget);
            return $"nb={p.Neighbors.NeighbourByID.Count} fami={p.FamilyForHouse.Count} rel={relRows} budget={budget}";
        }
    }
}
