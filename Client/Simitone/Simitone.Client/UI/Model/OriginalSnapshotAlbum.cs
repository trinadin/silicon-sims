using FSO.Client;
using FSO.Common;
using FSO.Common.Utils;
using FSO.Files.Formats.IFF.Chunks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = Microsoft.Xna.Framework.Color;
using System.Globalization;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Simitone.Client.UI.Model
{
    /// <summary>
    /// One photo-album page. The original model is cScrapbook::cPage
    /// (photo buffer + description + file identity); the port keeps the
    /// photo as a captured Texture2D plus the description string.
    /// </summary>
    public class OriginalSnapshotPage
    {
        public string FileName;      // stored base name, e.g. "photo-1"
        public string Description = "";
        public Texture2D Photo;      // decoded stored photo, owned by this page
        public string SourceImagePath; // legacy PNGs retain shared ownership and their original path
        public bool LegacyShared;
        public int JpegQuality = 50;
        internal bool ImageSaved;
        internal string SavedDescription;
    }

    /// <summary>
    /// The native album uses one PhotoAlbum directory with family export-name
    /// prefixes, JPEG photos and thumbnails, and plain-text captions. R239
    /// recovers the exact naming/quality law; legacy shared port PNG pages remain
    /// accessible without reassignment or image conversion.
    /// </summary>
    public static class OriginalSnapshotAlbum
    {
        // CPState ctor 0x212100 table writes: [w,h] per CameraSize enum.
        public static readonly int[,] FixedSnapshotDims = new int[3, 2]
        {
            { 200, 150 },   // Small
            { 400, 300 },   // Medium
            { 600, 400 }    // Large
        };

        /// <summary>CamSetSize(int,int) clamp law: custom sizes are bounded
        /// by the small/large tables on each axis independently.</summary>
        public static void ClampCustom(ref int w, ref int h)
        {
            if (w < FixedSnapshotDims[0, 0]) w = FixedSnapshotDims[0, 0];
            if (w > FixedSnapshotDims[2, 0]) w = FixedSnapshotDims[2, 0];
            if (h < FixedSnapshotDims[0, 1]) h = FixedSnapshotDims[0, 1];
            if (h > FixedSnapshotDims[2, 1]) h = FixedSnapshotDims[2, 1];
        }

        public static int CustomW = FixedSnapshotDims[1, 0];
        public static int CustomH = FixedSnapshotDims[1, 1];

        /// <summary>Snapshot dims for the port's CameraSize index
        /// (0=Small..2=Large, 3=Custom).</summary>
        public static void DimsForSize(int size, out int w, out int h)
        {
            if (size >= 0 && size < 3)
            {
                w = FixedSnapshotDims[size, 0];
                h = FixedSnapshotDims[size, 1];
            }
            else
            {
                w = CustomW; h = CustomH;
                ClampCustom(ref w, ref h);
            }
        }

        // ---- gate counters (pinned by the uiscrap check) ----
        public static int Captures;
        public static int Deletes;
        public static int Loads;
        public static int Saves;

        public static List<OriginalSnapshotPage> Pages = new List<OriginalSnapshotPage>();
        public static int CurrentPage;

        // Autotests isolate disk persistence from the owner's photo album.
        // Product callers leave this null and retain UserDir/PhotoAlbum.
        internal static string StorageDirectoryOverride;

        /// <summary>Swap every album field into an isolated test context and
        /// restore the owner's exact in-memory album without disk writes.</summary>
        internal static IDisposable IsolateForTest(string directory) => new TestScope(directory);

        private sealed class TestScope : IDisposable
        {
            private readonly List<OriginalSnapshotPage> SavedPages = Pages;
            private readonly string Directory = StorageDirectoryOverride, Family = FamilyExportName;
            private readonly int Current = CurrentPage, Sequence = NextSequence, W = CustomW, H = CustomH;
            private readonly int CaptureCount = Captures, DeleteCount = Deletes, LoadCount = Loads, SaveCount = Saves;
            private bool Disposed;
            public TestScope(string directory)
            {
                if (string.IsNullOrEmpty(directory)) throw new ArgumentException("An isolated album directory is required.", nameof(directory));
                Pages = new List<OriginalSnapshotPage>();
                StorageDirectoryOverride = directory;
                FamilyExportName = null;
                NextSequence = 0;
                CurrentPage = 0;
            }
            public void Dispose()
            {
                if (Disposed) return;
                Disposed = true;
                foreach (var page in Pages) page.Photo?.Dispose();
                Pages = SavedPages;
                StorageDirectoryOverride = Directory;
                FamilyExportName = Family;
                NextSequence = Sequence;
                CurrentPage = Current;
                CustomW = W; CustomH = H;
                Captures = CaptureCount; Deletes = DeleteCount; Loads = LoadCount; Saves = SaveCount;
            }
        }

        private static string StorageDirectory => StorageDirectoryOverride
            ?? Path.Combine(FSOEnvironment.UserDir, "PhotoAlbum");

        private static string AlbumDir
        {
            get
            {
                var dir = StorageDirectory;
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string FamilyExportName { get; private set; }
        private static int NextSequence;

        public static int QualityForIndex(int quality) => quality == 0 ? 20 : quality == 2 ? 90 : 50;

        public static string ExportName(string familyName, int familyID)
        {
            // Family::GetExportName 0x760b0 uses the FAMI resource ID (+0x10c),
            // not the distinct persistent FamilyNumber (+0x114). Prevent path
            // separators in player-entered names from escaping the album.
            var safe = new string((familyName ?? "").Select(c =>
                c == '/' || c == '\\' || c == ':' || char.IsControl(c) ? '_' : c).ToArray());
            return safe + "_" + familyID.ToString(CultureInfo.InvariantCulture);
        }

        public static void ResetForLot(FAMI family = null)
        {
            ResetForFamily(family == null ? null : ExportName(
                FSO.Content.Content.Get().Neighborhood.GetFamilyString(family.ChunkID)?.GetString(0) ?? "",
                family.ChunkID));
        }

        // Also permits isolated persistence tests without a loaded neighborhood.
        internal static void ResetForFamily(string exportName)
        {
            foreach (var p in Pages) p.Photo?.Dispose();
            Pages.Clear();
            FamilyExportName = exportName;
            NextSequence = 0;
            CurrentPage = 0;
            Load();
        }

        private static string CapturePrefix => FamilyExportName == null ? "photo-" : FamilyExportName + "_";

        private static int SequenceOf(string name, string prefix)
        {
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return -1;
            return int.TryParse(name.Substring(prefix.Length), NumberStyles.None,
                CultureInfo.InvariantCulture, out int sequence) ? sequence : -1;
        }

        public static OriginalSnapshotPage AddCaptured(Texture2D photo, int quality = 1)
        {
            if (photo == null) throw new ArgumentNullException(nameof(photo));
            var dir = AlbumDir;
            string name;
            // Keep the native monotonic sequence even after deletion. Avoid a
            // collision with an unreadable existing photo or orphan caption.
            do { name = CapturePrefix + (NextSequence++).ToString("D4", CultureInfo.InvariantCulture); }
            while (File.Exists(Path.Combine(dir, name + ".jpg")) || File.Exists(Path.Combine(dir, name + ".txt")));
            var page = new OriginalSnapshotPage
            {
                FileName = name,
                SourceImagePath = Path.Combine(dir, name + ".jpg"),
                Photo = photo,
                JpegQuality = QualityForIndex(quality)
            };
            Pages.Add(page);
            CurrentPage = Pages.Count - 1;
            Captures++;
            Save();
            return page;
        }

        public static void DeleteCurrent()
        {
            if (Pages.Count == 0) return;
            var page = Pages[CurrentPage];
            var path = PagePath(page);
            try { File.Delete(path); } catch { }
            try { File.Delete(Path.ChangeExtension(path, ".txt")); } catch { }
            if (!page.LegacyShared)
            {
                try { File.Delete(Path.Combine(Path.GetDirectoryName(path), page.FileName + "_thumb.jpg")); } catch { }
            }
            page.Photo?.Dispose();
            Pages.RemoveAt(CurrentPage);
            CurrentPage = Math.Max(0, Math.Min(CurrentPage, Pages.Count - 1));
            Deletes++;
            Save();
        }

        private static string PagePath(OriginalSnapshotPage page) => page.SourceImagePath
            ?? (page.SourceImagePath = Path.Combine(AlbumDir, page.FileName + (page.LegacyShared ? ".png" : ".jpg")));

        private static void SavePhoto(OriginalSnapshotPage page, string path)
        {
            if (page.LegacyShared)
            {
                using (var output = File.Create(path)) page.Photo.SaveAsPng(output, page.Photo.Width, page.Photo.Height);
                return;
            }
            var colors = new Color[page.Photo.Width * page.Photo.Height];
            page.Photo.GetData(colors);
            var pixels = new Rgba32[colors.Length];
            for (int i = 0; i < colors.Length; i++) pixels[i] = new Rgba32(colors[i].R, colors[i].G, colors[i].B, 255);
            var encoder = new JpegEncoder { Quality = page.JpegQuality };
            using (var image = Image.LoadPixelData<Rgba32>(pixels, page.Photo.Width, page.Photo.Height))
            {
                image.SaveAsJpeg(path, encoder);
                // cPage::Save 0x24a278..3d0: fit inside 100x75 without cropping.
                int tw = 100, th = 75;
                if (image.Width * 75 > image.Height * 100) th = Math.Max(1, image.Height * 100 / image.Width);
                else if (image.Width * 75 < image.Height * 100) tw = Math.Max(1, image.Width * 75 / image.Height);
                image.Mutate(x => x.Resize(tw, th));
                image.SaveAsJpeg(Path.Combine(Path.GetDirectoryName(path), page.FileName + "_thumb.jpg"), encoder);
            }
            // The original saves then reloads new pages: the viewer displays
            // the compressed JPEG, rather than the uncompressed capture.
            Texture2D decoded;
            using (var stream = File.OpenRead(path)) decoded = Texture2D.FromStream(GameFacade.GraphicsDevice, stream);
            var old = page.Photo;
            page.Photo = decoded;
            old.Dispose();
        }

        public static void Save()
        {
            Saves++;
            foreach (var page in Pages)
            {
                try
                {
                    var path = PagePath(page);
                    if (!page.ImageSaved && page.Photo != null)
                    {
                        SavePhoto(page, path);
                        page.ImageSaved = true;
                    }
                    var description = page.Description ?? "";
                    if (page.SavedDescription != description)
                    {
                        File.WriteAllText(Path.ChangeExtension(path, ".txt"), description);
                        page.SavedDescription = description;
                    }
                }
                catch (Exception e) { GameLog.Write("scrapbook save EXC " + e.GetType().Name); }
            }
        }

        private static void Load()
        {
            Loads++;
            var dir = StorageDirectory;
            if (!Directory.Exists(dir)) return;
            string[] files;
            try { files = Directory.GetFiles(dir); }
            catch (Exception e)
            {
                GameLog.Write("scrapbook load EXC " + e.GetType().Name);
                return;
            }
            foreach (var file in files)
            {
                var extension = Path.GetExtension(file);
                if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
                    NextSequence = Math.Max(NextSequence, SequenceOf(Path.GetFileNameWithoutExtension(file), CapturePrefix) + 1);
            }
            // Sequence belongs to this family. Legacy PNG pages remain a shared
            // compatibility layer; they never enter the family's JPEG sequence.
            var native = files.Where(f => Path.GetExtension(f).Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                && SequenceOf(Path.GetFileNameWithoutExtension(f), CapturePrefix) >= 0)
                .OrderBy(f => SequenceOf(Path.GetFileNameWithoutExtension(f), CapturePrefix));
            var legacy = files.Where(f => Path.GetExtension(f).Equals(".png", StringComparison.OrdinalIgnoreCase)
                && SequenceOf(Path.GetFileNameWithoutExtension(f), "photo-") >= 0)
                .OrderBy(f => SequenceOf(Path.GetFileNameWithoutExtension(f), "photo-"));
            foreach (var file in native.Concat(legacy))
            {
                bool isLegacy = Path.GetExtension(file).Equals(".png", StringComparison.OrdinalIgnoreCase);
                var name = Path.GetFileNameWithoutExtension(file);
                if (!isLegacy) NextSequence = Math.Max(NextSequence, SequenceOf(name, CapturePrefix) + 1);
                Texture2D photo = null;
                try
                {
                    using (var input = File.OpenRead(file)) photo = Texture2D.FromStream(GameFacade.GraphicsDevice, input);
                    if (photo == null) continue;
                    var captionPath = Path.ChangeExtension(file, ".txt");
                    var caption = File.Exists(captionPath) ? File.ReadAllText(captionPath) : "";
                    Pages.Add(new OriginalSnapshotPage
                    {
                        FileName = name, SourceImagePath = file, LegacyShared = isLegacy,
                        Photo = photo, Description = caption, SavedDescription = caption, ImageSaved = true
                    });
                    photo = null; // page now owns the texture
                }
                catch (Exception e) { GameLog.Write("scrapbook load EXC " + e.GetType().Name); }
                finally { photo?.Dispose(); }
            }
            CurrentPage = 0;
        }

        /// <summary>cScrapbook::GetDescription (0x2491f2) — the current
        /// page's description text.</summary>
        public static string CurrentDescription()
        {
            if (Pages.Count == 0) return "";
            return Pages[CurrentPage].Description ?? "";
        }

        /// <summary>cScrapbook::SetDescription (0x249450). Navigation can
        /// defer persistence until Done, matching cWinScrapbook::TSOnCommand.
        /// Existing callers retain their immediate-save behavior.</summary>
        public static void SetCurrentDescription(string text, bool persist = true)
        {
            if (Pages.Count == 0) return;
            Pages[CurrentPage].Description = text ?? "";
            if (persist) Save();
        }
    }
}
