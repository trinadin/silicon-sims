using System;
using System.Collections.Generic;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;

namespace FSO.SimAntics.Primitives
{
    /// <summary>
    /// Neighbour-id resolution + throttled diagnostic for the TS1 neighbourhood-matrix path
    /// of VMRelationship (UseNeighbor flag). The NBRS matrix is keyed by NEIGHBOUR ids, while
    /// in-lot avatars are pushed onto the stack by OBJECT id (verified against original save
    /// data + runtime: Goth avatars obj 16/18/21 vs nid 32/33/34). UseNeighborNid resolves an
    /// in-lot avatar stack object back to its neighbour id so lookups hit the stored record;
    /// off-lot references / non-avatar stack objects pass through unchanged. The Observe probe
    /// logs UseNeighbor executions so headless runs can confirm the divergence is closed
    /// (see tools/iff-dump + PARITY.md).
    /// </summary>
    public static class UseNeighborLog
    {
        public static short UseNeighborNid(VMEntity stack, short fallback)
        {
            var ava = stack as VMAvatar;
            if (ava != null)
            {
                var nid = ava.GetPersonData(VMPersonDataVariable.NeighborId);
                if (nid > 0) return nid;
            }
            return fallback;
        }

        private const int MaxNonAvatar = 24;
        private const int MaxAvatar = 120;
        private static int _avaLog;
        private static int _calls;
        private static int _misses;
        private static int _objMiss;
        private static int _avaCalls;

        public static void Observe(VMStackFrame context, VMRelationshipOperand operand,
            short myNID, short targNID, Dictionary<int, List<short>> rels)
        {
            _calls++;
            var callerNid = (context.Caller as VMAvatar)?.GetPersonData(VMPersonDataVariable.NeighborId) ?? 0;
            var stackAva = context.StackObject as VMAvatar;
            var stackNid = stackAva?.GetPersonData(VMPersonDataVariable.NeighborId) ?? 0;
            var exists = rels.ContainsKey(targNID);
            var bucketMismatch = (stackAva != null && stackNid > 0 && targNID != stackNid);
            if (!exists) { _misses++; }
            if (bucketMismatch) { _objMiss++; }
            if (stackAva != null) { _avaCalls++; }
            if (stackAva != null) _avaLog++;
            var logIt = (stackAva != null && _avaLog <= MaxAvatar) || (stackAva == null && _calls <= MaxNonAvatar);
            if (logIt)
            {
                Console.WriteLine("RELPROBE mode=" + operand.Mode +
                    " myNID=" + myNID +
                    " caller[obj=" + context.Caller.ObjectID + ",nid=" + callerNid + "]" +
                    " targNID=" + targNID +
                    " stack[obj=" + (context.StackObject?.ObjectID ?? 0) + ",nid=" + stackNid + ",avatar=" + (stackAva != null) + "]" +
                    " exists=" + exists + " bucketMismatch=" + bucketMismatch);
            }
        }

        public static void Summary()
        {
            Console.WriteLine("RELPROBE SUMMARY calls=" + _calls + " misses=" + _misses +
                " objectbucket=" + _objMiss + " avatarStack=" + _avaCalls);
        }
    }
}
