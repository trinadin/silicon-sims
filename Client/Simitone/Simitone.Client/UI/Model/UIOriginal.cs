using FSO.Content;
using FSO.Content.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Simitone.Client.UI.Model
{
    // R85: IFF-mount helper (mirrors LoadingGameScreen.UISimitoneBg.ResolveOriginal).
    // Resolves an ORIGINAL UIGraphics.far member to its ITextureRef on the UI thread the
    // first time it is asked for, then caches it. Returns null on ANY failure (IFF not yet
    // mounted, member missing, decode error) so callers keep their CustomUI png fallback.
    public static class UIOriginal
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ITextureRef> _cache =
            new System.Collections.Concurrent.ConcurrentDictionary<string, ITextureRef>();

        private static readonly object _resourceMapLock = new object();
        private static Dictionary<int, string> _resourceNamesByID;

        private static readonly Regex HeaderConstant = new Regex(
            @"const\s+\w+\s+(\w+)\s*=\s*(-?\d+)\s*;",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ResourceTemplateRow = new Regex(
            @"\{\s*(?:BITMAP|RLEBMP|TARGA|JPEG)\s*,\s*(\w+)\s*,\s*""([^""]+)""\s*(?:,\s*(\w+)\s*)?\}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static ITextureRef EnsureResolved(string memberName)
        {
            ITextureRef hit;
            if (_cache.TryGetValue(memberName, out hit)) return hit;
            var ts1 = Content.Get()?.TS1Global;
            if (ts1 == null) return null;
            try
            {
                var tx = ts1.Get(memberName) as ITextureRef;
                // R121: the FAR lookup is CASE-SENSITIVE and the manifest casing is
                // chaotic ('OptSave.BMP' vs 'optgraphics.bmp' vs the RT templates'
                // 'CPanel/...'). On an exact-case miss, resolve through the entry
                // list (OrdinalIgnoreCase) and re-Get by the manifest's true name.
                if (tx == null)
                {
                    var wanted = memberName.Replace('/', '\\').ToLowerInvariant();
                    foreach (var en in ts1.GetFarEntries(".bmp"))
                    {
                        if (en != null && en.FarEntry != null &&
                            string.Equals(en.FarEntry.Filename.Replace('/', '\\'), wanted, StringComparison.OrdinalIgnoreCase))
                        {
                            tx = ts1.Get(en.FarEntry.Filename) as ITextureRef;
                            break;
                        }
                    }
                }
                if (tx == null) return null;
                if (_cache.TryAdd(memberName, tx))
                {
                    Simitone.Client.GameLog.Write("uioriginal: IFF-mount original " + memberName);
                    // R210: provenance registry (Tier C ui-total gate).
                    try { UIArtProvenance.NoteOriginal(tx.Get(FSO.Client.GameFacade.GraphicsDevice), memberName); }
                    catch { }
                }
                else
                {
                    // already cached: the texture may predate the registry
                    try { UIArtProvenance.NoteOriginal(tx.Get(FSO.Client.GameFacade.GraphicsDevice), null); }
                    catch { }
                }
                return tx;
            }
            catch (Exception e)
            {
                Simitone.Client.GameLog.Write("uioriginal: IFF-mount EXC " + e.GetType().Name + " " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Resolve a classic global UI resource number through the game's own
        /// Res_*.h/Res_*.RT tables. ObjectDialog's named "gz"/"gzi" commands
        /// carry only this integer, so filename-only lookup cannot implement
        /// the authored behavior. The tables remain local proprietary input;
        /// only the derived id-to-member map is retained in memory.
        /// </summary>
        public static ITextureRef EnsureResolvedByID(int resourceID)
        {
            EnsureResourceMap();
            string member;
            return _resourceNamesByID != null
                && _resourceNamesByID.TryGetValue(resourceID, out member)
                ? EnsureResolved(member)
                : null;
        }

        internal static string ResourceNameForIDForProbe(int resourceID)
        {
            EnsureResourceMap();
            string member;
            return _resourceNamesByID != null
                && _resourceNamesByID.TryGetValue(resourceID, out member)
                ? member
                : null;
        }

        private static void EnsureResourceMap()
        {
            if (_resourceNamesByID != null) return;
            lock (_resourceMapLock)
            {
                if (_resourceNamesByID != null) return;
                var result = new Dictionary<int, string>();
                var symbols = new Dictionary<string, int>(StringComparer.Ordinal);
                var neutral = new HashSet<int>();
                var ts1 = Content.Get()?.TS1Global;
                if (ts1 == null)
                {
                    return;
                }

                try
                {
                    var headers = ts1.GetFarEntries(".h");
                    if (headers != null)
                    {
                        foreach (var entry in headers)
                        {
                            var bytes = entry.Archive.GetEntry(entry.FarEntry);
                            var text = Encoding.ASCII.GetString(bytes);
                            foreach (Match match in HeaderConstant.Matches(text))
                            {
                                int id;
                                if (int.TryParse(match.Groups[2].Value, out id))
                                    symbols[match.Groups[1].Value] = id;
                            }
                        }
                    }

                    var templates = ts1.GetFarEntries(".rt");
                    if (templates != null)
                    {
                        foreach (var entry in templates)
                        {
                            var bytes = entry.Archive.GetEntry(entry.FarEntry);
                            var text = Encoding.ASCII.GetString(bytes);
                            foreach (Match match in ResourceTemplateRow.Matches(text))
                            {
                                int id;
                                if (!symbols.TryGetValue(match.Groups[1].Value, out id)) continue;
                                bool localizedOverride = match.Groups[3].Success;
                                if (!result.ContainsKey(id) || (!localizedOverride && !neutral.Contains(id)))
                                    result[id] = match.Groups[2].Value.Replace('/', '\\');
                                if (!localizedOverride) neutral.Add(id);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Simitone.Client.GameLog.Write("uioriginal: resource-id map EXC "
                        + e.GetType().Name + " " + e.Message);
                }
                _resourceNamesByID = result;
            }
        }

        // R122: original multi-frame sheets mount as ONE wide texture (catalog
        // SubSortIcons are 4 frames of 36x36, ThumbTemplate 4 of 45x45). This
        // crops a single frame out, cached per member+frame. Art that does not
        // divide into 2-4 equal frames (plaques, single buttons) passes through
        // uncropped. Frame order is the UIButton convention (0 up / 1 down /
        // 2 hover / 3 disabled) — our disclosed reading, as in R121.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Microsoft.Xna.Framework.Graphics.Texture2D> _frameCache =
            new System.Collections.Concurrent.ConcurrentDictionary<string, Microsoft.Xna.Framework.Graphics.Texture2D>();

        public static Microsoft.Xna.Framework.Graphics.Texture2D Frame(string memberName, int frame)
        {
            var key = memberName + "#" + frame;
            Microsoft.Xna.Framework.Graphics.Texture2D hit;
            if (_frameCache.TryGetValue(key, out hit)) return hit;
            try
            {
                var tx = EnsureResolved(memberName);
                if (tx == null) return null;
                var tex = tx.Get(FSO.Client.GameFacade.GraphicsDevice);
                var result = tex;
                if (tex.Width > tex.Height && tex.Width % tex.Height == 0)
                {
                    var frames = tex.Width / tex.Height;
                    if (frames >= 2 && frames <= 4)
                    {
                        var fw = tex.Height;
                        var data = new Microsoft.Xna.Framework.Color[fw * tex.Height];
                        tex.GetData(0, new Microsoft.Xna.Framework.Rectangle(frame * fw, 0, fw, tex.Height), data, 0, data.Length);
                        var crop = new Microsoft.Xna.Framework.Graphics.Texture2D(FSO.Client.GameFacade.GraphicsDevice, fw, tex.Height);
                        crop.SetData(data);
                        result = crop;
                    }
                }
                _frameCache[key] = result;
                return result;
            }
            catch (Exception e)
            {
                Simitone.Client.GameLog.Write("uioriginal: frame-crop EXC " + memberName + " " + e.GetType().Name + " " + e.Message);
                return null;
            }
        }

        // R86: IFF-first resolve with an engine png fallback and a last-resort texture.
        // Worst case on any failure = today's look; never throws.
        public static Microsoft.Xna.Framework.Graphics.Texture2D ResolveOrPng(string iffName, string pngName, Microsoft.Xna.Framework.Graphics.Texture2D fallback)
        {
            try
            {
                var iffTx = EnsureResolved(iffName);
                if (iffTx != null) return iffTx.Get(FSO.Client.GameFacade.GraphicsDevice);
            }
            catch { }
            try
            {
                var px = Content.Get().CustomUI.Get(pngName);
                if (px != null)
                {
                    // R210: the png twin is a byte-faithful render of the far
                    // bytes (r141 evidence) — register as original. The
                    // fallback is modern and stays unregistered.
                    try { UIArtProvenance.NoteOriginal(px.Get(FSO.Client.GameFacade.GraphicsDevice), pngName); }
                    catch { }
                    return px.Get(FSO.Client.GameFacade.GraphicsDevice);
                }
            }
            catch { }
            return fallback;
        }

        // R142: crop an ARBITRARY rect out of a member sheet. The engine's pause
        // button (cpanel\Buttons\pause.bmp 60x30) is a 4x2 grid of 15x15 states —
        // horizontal Frame()/FrameW() cannot express the row; SetImage(art, cols,
        // rows) in the engine (vtable+0x1a4) divides BOTH axes.
        public static Microsoft.Xna.Framework.Graphics.Texture2D Rect(string memberName, int x, int y, int w, int h)
        {
            var key = memberName + "#r" + x + ":" + y + ":" + w + ":" + h;
            Microsoft.Xna.Framework.Graphics.Texture2D hit;
            if (_frameCache.TryGetValue(key, out hit)) return hit;
            try
            {
                var tx = EnsureResolved(memberName);
                if (tx == null) return null;
                var tex = tx.Get(FSO.Client.GameFacade.GraphicsDevice);
                if (x + w > tex.Width || y + h > tex.Height) { _frameCache[key] = tex; return tex; }
                var data = new Microsoft.Xna.Framework.Color[w * h];
                tex.GetData(0, new Microsoft.Xna.Framework.Rectangle(x, y, w, h), data, 0, data.Length);
                var crop = new Microsoft.Xna.Framework.Graphics.Texture2D(FSO.Client.GameFacade.GraphicsDevice, w, h);
                UIArtProvenance.NoteOriginal(crop, key); // R210: a crop of an original sheet
                crop.SetData(data);
                _frameCache[key] = crop;
                return crop;
            }
            catch (Exception e)
            {
                Simitone.Client.GameLog.Write("uioriginal: rect-crop EXC " + memberName + " " + e.GetType().Name + " " + e.Message);
                return null;
            }
        }

        // R141: crop a frame of EXPLICIT width fw out of a horizontal sheet. Several
        // original UCP strips do not divide Width%Height==0 (nowall 88x15 = 4x 22x15,
        // LevRoof 88x19 = 4x 22x19, Lev1 84x11 = 4x 21x11, pause 60x30 = 2x 30x30,
        // Speed1/2/3 44/80/128x15), so Frame() would pass them through uncropped.
        // Frame order is the same 0up/1down/2hover/3disabled disclosure as Frame().
        public static Microsoft.Xna.Framework.Graphics.Texture2D FrameW(string memberName, int frame, int fw)
        {
            var key = memberName + "#w" + frame + ":" + fw;
            Microsoft.Xna.Framework.Graphics.Texture2D hit;
            if (_frameCache.TryGetValue(key, out hit)) return hit;
            try
            {
                var tx = EnsureResolved(memberName);
                if (tx == null) return null;
                var tex = tx.Get(FSO.Client.GameFacade.GraphicsDevice);
                if (fw <= 0 || tex.Width < fw || tex.Width % fw != 0)
                {
                    _frameCache[key] = tex;
                    return tex;
                }
                var data = new Microsoft.Xna.Framework.Color[fw * tex.Height];
                tex.GetData(0, new Microsoft.Xna.Framework.Rectangle(frame * fw, 0, fw, tex.Height), data, 0, data.Length);
                var crop = new Microsoft.Xna.Framework.Graphics.Texture2D(FSO.Client.GameFacade.GraphicsDevice, fw, tex.Height);
                crop.SetData(data);
                _frameCache[key] = crop;
                return crop;
            }
            catch (Exception e)
            {
                Simitone.Client.GameLog.Write("uioriginal: framew-crop EXC " + memberName + " " + e.GetType().Name + " " + e.Message);
                return null;
            }
        }
    }
}
