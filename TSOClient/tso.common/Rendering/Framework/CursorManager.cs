using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using FSO.Common.Utils;

namespace FSO.Common.Rendering.Framework
{
    public enum CursorType
    {
        Normal,
        ArrowUp,
        ArrowUpLeft,
        ArrowUpRight,
        ArrowDown,
        ArrowDownLeft,
        ArrowDownRight,
        ArrowLeft,
        ArrowRight,
        LiveNothing,
        LiveObjectUnavail,
        LivePerson,
        IBeam,

        SimsRotate,
        SimsRotateNE,
        SimsRotateSE,
        SimsRotateSW,
        SimsRotateNW,

        SimsMove,
        SimsPlace,

        Hourglass,

        LiveObjectAvail,
        LiveObject1Star,
        LiveObject2Star,
        LiveObject3Star,
        LiveObject4Star,
        LiveObject5Star,
        LiveObjectSpecial,
    }

    /// <summary>
    /// Manages cursors in the game.
    /// </summary>
    public class CursorManager
    {
        public static CursorManager INSTANCE;

        private Dictionary<CursorType, CursorGroup> m_CursorMap;
        private GraphicsDevice GD;
        public CursorType CurrentCursor { get; internal set;} = CursorType.Normal;
        public int CurrentPriority { get; private set; } = 0;

        public CursorManager(GraphicsDevice gd)
        {
            INSTANCE = this;
            m_CursorMap = new Dictionary<CursorType, CursorGroup>();
            this.GD = gd;
        }

        public void SetCursorPriority(int priority)
        {
            CurrentPriority = priority;
        }

        public void SetCursor(CursorType type, int priority = 0)
        {
            if (CurrentCursor != type && priority >= CurrentPriority && m_CursorMap.ContainsKey(type))
            {
                CurrentCursor = type;
                Mouse.SetCursor(m_CursorMap[type].MouseCursor);
            }
        }

        public Dictionary<CursorType, string> GenMap()
        {
            return new Dictionary< CursorType, string> (){
                //{CursorType.Normal, "arrow.cur"},
                { CursorType.ArrowUp, "up.cur"},
                { CursorType.ArrowUpLeft, "upleft.cur"},
                { CursorType.ArrowUpRight, "upright.cur"},
                { CursorType.ArrowDown, "down.cur"},
                { CursorType.ArrowDownLeft, "downleft.cur"},
                { CursorType.ArrowDownRight, "downright.cur"},
                { CursorType.ArrowLeft, "left.cur"},
                { CursorType.ArrowRight, "right.cur"},
                { CursorType.LiveNothing, "livenothing.cur"},
                { CursorType.LiveObjectAvail, "liveobjectavail.cur"},
                { CursorType.LiveObjectUnavail, "liveobjectunavail.cur"},
                { CursorType.LivePerson, "liveperson.cur"},

                { CursorType.SimsRotate, "simsrotate.cur" },
                { CursorType.SimsRotateNE, "simsrotatene.cur" },
                { CursorType.SimsRotateNW, "simsrotatenw.cur" },
                { CursorType.SimsRotateSE, "simsrotatese.cur" },
                { CursorType.SimsRotateSW, "simsrotatesw.cur" },

                { CursorType.SimsMove, "simsmove.cur" },
                { CursorType.SimsPlace, "simsplace.cur" },

                { CursorType.Hourglass, "hourglass.cur" }
            };
        }

        public void Init(string basepath, bool ts1)
        {
            Init(basepath, ts1, null);
        }

        /// <summary>
        /// Builds the cursor table. iffResolver, when non-null, IFF-first serves the ORIGINAL
        /// UIGraphics.far .cur member bytes (keyed by the member basename, e.g. "liveperson.cur")
        /// for a cursor; the resolver returns null if IFF data is unavailable and the filesystem
        /// path is used instead. Every load is guarded so a missing/corrupt member degrades to the
        /// platform default cursor instead of throwing (R87 IFF-literalism: the member bytes are the
        /// ORIGINAL game cursor files - IFF-first, never invented).
        /// </summary>
        public void Init(string basepath, bool ts1, Func<string, byte[]> iffResolver)
        {
            var map = GenMap();
            var curPath = "UIGraphics/Shared/cursors/";
            if (!ts1) curPath = curPath.ToLowerInvariant();
            foreach (var item in map)
            {
                m_CursorMap.Add(item.Key,
                    LoadCustomCursor(
                        Path.Combine(basepath, curPath, item.Value), item.Value, iffResolver));
            }

            var starMax = 5;
            var stars = LoadUpgradeCursors(Path.Combine(basepath, curPath, "liveobjectavail.cur"), "liveobjectavail.cur", iffResolver, starMax);
            for (int i=0; i<starMax; i++)
            {
                m_CursorMap.Add(CursorType.LiveObject1Star + i, stars[i]);
            }

            m_CursorMap.Add(CursorType.IBeam, new CursorGroup(MouseCursor.IBeam));
            //m_CursorMap.Add(CursorType.Hourglass, MouseCursor.Wait);
            m_CursorMap.Add(CursorType.Normal, new CursorGroup(MouseCursor.Arrow));
        }

        /// <summary>R87 rendered-control gate hook: byte length of the ORIGINAL liveperson.cur member
        /// the IFF-first live mount loaded (== 326), or -1 if IFF-first never ran.</summary>
        public static long LastMountedLivePersonLen = -1;

        public CursorGroup GetCurrentGroup()
        {
            if (m_CursorMap.TryGetValue(CurrentCursor, out CursorGroup value))
            {
                return value;
            }

            return default;
        }

        private CursorGroup[] LoadUpgradeCursors(string path, string iffName, Func<string, byte[]> iff, int maxStars)
        {
            try
            {
                Stream src = null;
                if (iff != null)
                {
                    var bytes = iff(iffName);
                    if (bytes != null) src = new MemoryStream(bytes);
                }
                if (src == null) src = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using (src)
                    return CurLoader.LoadUpgradeCursors(GD, src, maxStars);
            }
            catch (Exception)
            {
                // IFF-first original cursor bytes still mount; the FreeSO star overlay is optional.
                var baseG = LoadCustomCursor(path, iffName, iff);
                var result = new CursorGroup[maxStars];
                for (int i = 0; i < maxStars; i++) result[i] = baseG;
                return result;
            }
        }

        private CursorGroup LoadCustomCursor(string path, string iffName, Func<string, byte[]> iff)
        {
            try
            {
                if (iff != null)
                {
                    var bytes = iff(iffName);
                    if (bytes != null)
                    {
                        // IFF-first: the ORIGINAL member bytes are mounted for this cursor (the GPU
                        // texture bake below is IFF-literal and any render quirk still keeps the bytes).
                        if (string.Equals(iffName, "liveperson.cur", StringComparison.OrdinalIgnoreCase))
                            LastMountedLivePersonLen = bytes.Length;
                        using (var ms = new MemoryStream(bytes))
                        {
                            return CurLoader.LoadMonoCursor(GD, ms);
                        }
                    }
                }
                return CurLoader.LoadMonoCursor(GD, File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read));
            }
            catch (Exception)
            {
                return new CursorGroup(MouseCursor.Arrow);
            }
        }
    }
}
