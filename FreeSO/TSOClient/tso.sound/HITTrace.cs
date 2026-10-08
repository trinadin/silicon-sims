using System;
using System.Collections.Generic;
using System.Text;

namespace FSO.HIT
{
    /// <summary>
    /// AUD-19: runtime-toggleable per-event sound fidelity trace.
    ///
    /// Records what the sound dispatch path actually fires — one entry per
    /// HITVM.PlaySoundEvent call (with its resolution outcome) and one per
    /// audible note (NoteOn/NoteLoop: patch/resource, volume AS PLAYED after
    /// group-master and duck factors, pan, pitch). The battery enables this
    /// with Enabled=true, exercises a known window, then dumps the census
    /// via Snapshot()/Counts().
    ///
    /// Cost when disabled is a single volatile bool test at each call site;
    /// every record path is exception-isolated so a trace bug can never
    /// break playback. Fixed-size ring buffer — no growth, no GC pressure
    /// beyond the entry strings themselves.
    /// </summary>
    public static class HITTrace
    {
        // dispatch outcomes (native-law names, see evidence/AUD-19)
        public const byte RES_DISABLED = 0;      // sound globally disabled
        public const byte RES_ALREADY_ALIVE = 1; // dedup: an event of this name is already active
        public const byte RES_NOT_FOUND = 2;     // name absent from the mounted event corpus
        public const byte RES_CREATED = 3;       // a new thread was created and started
        public const byte RES_PIANO_REMAP = 4;   // piano_play hardcoded remap to playpiano

        public const byte KIND_EVENT = 0;        // HITVM.PlaySoundEvent dispatch
        public const byte KIND_NOTE = 1;         // HITThread.NoteOn
        public const byte KIND_NOTE_LOOP = 2;    // HITThread.NoteLoop

        public struct Entry
        {
            public long Seq;
            public int TimeMs;
            public byte Kind;
            public byte Result;
            public string EventName;   // requested event name (lowercased)
            public string SourceName;  // owning thread's name (per-event dedup id)
            public string Resource;    // note: patch resource/file name
            public uint TrackId;
            public uint PatchId;
            public float Volume;       // as-played (post master+duck)
            public float InstVolume;   // pre-master instance volume
            public float Pan;
            public float Pitch;
            public byte VolGroup;
            public int OwnerID;        // LastMainOwner at note time (-1 = none)
        }

        public static volatile bool Enabled;

        private const int CAPACITY = 4096;
        private static Entry[] Ring = new Entry[CAPACITY];
        private static long NextSeq;
        private static int Head; // next write slot
        private static readonly object TraceLock = new object();

        /// <summary>Clear the buffer and reset the sequence (start a trace window).</summary>
        public static void Reset()
        {
            lock (TraceLock)
            {
                Head = 0;
                NextSeq = 0;
                for (int i = 0; i < CAPACITY; i++) Ring[i] = new Entry();
            }
        }

        /// <summary>Record a PlaySoundEvent dispatch outcome.</summary>
        public static void Event(string eventName, byte result, uint trackId, string sourceName)
        {
            if (!Enabled) return;
            try
            {
                var e = new Entry
                {
                    Seq = 0,
                    TimeMs = Environment.TickCount,
                    Kind = KIND_EVENT,
                    Result = result,
                    EventName = eventName,
                    SourceName = sourceName,
                    TrackId = trackId,
                    OwnerID = -1,
                };
                lock (TraceLock)
                {
                    e.Seq = NextSeq++;
                    Ring[Head] = e;
                    Head = (Head + 1) % CAPACITY;
                }
            }
            catch { }
        }

        /// <summary>Record an audible note with its as-played parameters.</summary>
        public static void Note(byte kind, string sourceName, string resource, uint patchId, uint trackId,
            float volume, float instVolume, float pan, float pitch, byte volGroup, int ownerID)
        {
            if (!Enabled) return;
            try
            {
                var e = new Entry
                {
                    Seq = 0,
                    TimeMs = Environment.TickCount,
                    Kind = kind,
                    EventName = null,
                    SourceName = sourceName,
                    Resource = resource,
                    PatchId = patchId,
                    TrackId = trackId,
                    Volume = volume,
                    InstVolume = instVolume,
                    Pan = pan,
                    Pitch = pitch,
                    VolGroup = volGroup,
                    OwnerID = ownerID,
                };
                lock (TraceLock)
                {
                    e.Seq = NextSeq++;
                    Ring[Head] = e;
                    Head = (Head + 1) % CAPACITY;
                }
            }
            catch { }
        }

        /// <summary>Ordered copy of the recorded window (oldest first).</summary>
        public static List<Entry> Snapshot()
        {
            var result = new List<Entry>(CAPACITY);
            lock (TraceLock)
            {
                long total = NextSeq;
                int count = (int)Math.Min(total, CAPACITY);
                for (int i = count; i > 0; i--)
                {
                    int idx = ((Head - i) % CAPACITY + CAPACITY) % CAPACITY;
                    result.Add(Ring[idx]);
                }
            }
            return result;
        }

        /// <summary>Total entries recorded in the window.</summary>
        public static int Count
        {
            get { lock (TraceLock) { return (int)Math.Min(NextSeq, CAPACITY); } }
        }

        /// <summary>
        /// Census: event-name -> dispatch count, split by outcome, for the window.
        /// Format per line: name total [alive X notfound Y created Z disabled W remap V].
        /// </summary>
        public static Dictionary<string, int[]> Counts()
        {
            var counts = new Dictionary<string, int[]>();
            foreach (var e in Snapshot())
            {
                if (e.Kind != KIND_EVENT || e.EventName == null) continue;
                int[] c;
                if (!counts.TryGetValue(e.EventName, out c))
                {
                    c = new int[7]; // total, alive, notfound, created, disabled, remap, other
                    counts[e.EventName] = c;
                }
                c[0]++;
                switch (e.Result)
                {
                    case RES_ALREADY_ALIVE: c[1]++; break;
                    case RES_NOT_FOUND: c[2]++; break;
                    case RES_CREATED: c[3]++; break;
                    case RES_DISABLED: c[4]++; break;
                    case RES_PIANO_REMAP: c[5]++; break;
                    default: c[6]++; break;
                }
            }
            return counts;
        }

        /// <summary>Human-readable dump of the first max lines of the window.</summary>
        public static string Dump(int max)
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var e in Snapshot())
            {
                if (n++ >= max) { sb.AppendLine("... (truncated at " + max + ")"); break; }
                if (e.Kind == KIND_EVENT)
                {
                    sb.Append('#').Append(e.Seq).Append(" EVT ").Append(e.EventName)
                      .Append(" -> ").Append(ResultName(e.Result))
                      .Append(" track=").Append(e.TrackId);
                    if (e.SourceName != null) sb.Append(" src=").Append(e.SourceName);
                }
                else
                {
                    sb.Append('#').Append(e.Seq).Append(' ').Append(e.Kind == KIND_NOTE ? "NOTE" : "LOOP")
                      .Append(" src=").Append(e.SourceName)
                      .Append(" res=").Append(e.Resource)
                      .Append(" patch=").Append(e.PatchId)
                      .Append(" vol=").Append(e.Volume.ToString("0.000"))
                      .Append(" inst=").Append(e.InstVolume.ToString("0.000"))
                      .Append(" pan=").Append(e.Pan.ToString("0.000"))
                      .Append(" pitch=").Append(e.Pitch.ToString("0.000"))
                      .Append(" grp=").Append(e.VolGroup)
                      .Append(" owner=").Append(e.OwnerID);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public static string ResultName(byte r)
        {
            switch (r)
            {
                case RES_DISABLED: return "disabled";
                case RES_ALREADY_ALIVE: return "already-alive";
                case RES_NOT_FOUND: return "NOT-FOUND";
                case RES_CREATED: return "created";
                case RES_PIANO_REMAP: return "piano-remap";
                default: return "?";
            }
        }
    }
}
