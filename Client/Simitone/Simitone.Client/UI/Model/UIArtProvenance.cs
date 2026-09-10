using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Simitone.Client.UI.Model
{
    /// ROUND-210 'uitotal' (Tier C enforcement): the original-art provenance
    /// registry. Every texture mounted through the ORIGINAL art pipeline (the
    /// UIOriginal FAR resolver, the byte-faithful png twins, the original
    /// glyph font atlases) registers here; the ui-total gate walks the live
    /// desktop UI tree and fails on any texture-bearing element whose texture
    /// is neither original-registered, a solid fill, a render target, nor a
    /// whitelisted disclosed no-canon item.
    public static class UIArtProvenance
    {
        private static readonly object _lock = new object();
        private static readonly HashSet<Texture2D> _original = new HashSet<Texture2D>();
        private static readonly HashSet<Texture2D> _disclosed = new HashSet<Texture2D>();
        private static readonly List<string> _mounts = new List<string>();

        public static int OriginalCount { get { lock (_lock) { return _original.Count; } } }
        public static int MountNameCount { get { lock (_lock) { return _mounts.Count; } } }

        public static void NoteOriginal(Texture2D tex, string sourceName)
        {
            if (tex == null) return;
            lock (_lock)
            {
                if (_original.Add(tex) && sourceName != null) _mounts.Add(sourceName);
            }
        }

        public static bool IsOriginal(Texture2D tex)
        {
            if (tex == null) return false;
            lock (_lock) { return _original.Contains(tex); }
        }

        /// R210: individually-disclosed port additions with no original
        /// counterpart (each cites its disclosure at the call site).
        public static void NoteDisclosed(Texture2D tex)
        {
            if (tex == null) return;
            lock (_lock) { _disclosed.Add(tex); }
        }

        public static bool IsDisclosed(Texture2D tex)
        {
            if (tex == null) return false;
            lock (_lock) { return _disclosed.Contains(tex); }
        }
    }
}
