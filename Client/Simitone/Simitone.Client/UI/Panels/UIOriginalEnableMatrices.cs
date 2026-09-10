using FSO.Content;
using System;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// R149: the plaque ENABLE matrices — engine cWinCatalog::Init
    /// 0x26bd3c-0x26c374 builds one 8x8 byte matrix per sort family from the
    /// catalog product list; LoadBooks writes each subsort plaque's
    /// +0x160 enable mask from its row (repaint-as-disabled when 0, the
    /// ghosted frame-3 cell). EXACT LAW (per product, func = the product's
    /// function canon index):
    /// - roomFunc[room*8+func]   = 1 iff the product carries that room's
    ///   RoomFlags bit (room display index -> data bit via CanonRoomToFlagBit;
    ///   gate test 0x20b400 = "has any room").
    /// - {downtown,vacation,community,studio,magic}[mainSlot*8+func] = 1 iff
    ///   the product's expansion sort byte carries mainSlot's bit
    ///   (slots 0-3 = bits 1/2/4/8, slot 7 (Misc) = 0x80; slots 4-6 never
    ///   written — the jump table skips them, exactly like the plaque art).
    /// - functionSub[func*8+sub] = 1 iff the product's OBJD FunctionSubsort
    ///   value == sub (engine subs 0-3 = SubSortOne..Four, 4 = Pets, 5 =
    ///   Magic, 6 = Other, 7 = All — the LoadBooks plaque 4/5/6/7 specials;
    ///   gate tests 0x20b1e0+0x20b170 = catalog-eligible with a subsort).
    /// Matrices are consumed in SUBSORT state only — main plaques never gray.
    /// </summary>
    public static class UIOriginalEnableMatrices
    {
        public static byte[] RoomFunc = new byte[64];
        public static byte[] Downtown = new byte[64];
        public static byte[] Vacation = new byte[64];
        public static byte[] Community = new byte[64];
        public static byte[] Studio = new byte[64];
        public static byte[] Magic = new byte[64];
        public static byte[] FunctionSub = new byte[64];

        public static int Builds;
        private static bool _Built;

        /// port MaskBit scheme (0-3 subs, 5 Pets, 6 Magic, 7 Other, 8 All)
        /// -> the engine's OBJD FunctionSubsort index.
        public static int EngineSubFromMaskBit(int maskBit)
        {
            switch (maskBit)
            {
                case 0: case 1: case 2: case 3: return maskBit;
                case 5: return 4;   // Pets
                case 6: return 5;   // Magic
                case 7: return 6;   // Other
                case 8: return 7;   // All
                default: return -1;
            }
        }

        private static int FuncCanon(sbyte category)
        {
            var map = LiveSubpanels.UIBuyBrowsePanel.CanonFuncToCatalogID;
            for (int f = 0; f < 8; f++) if (map[f] == category) return f;
            return -1;
        }

        public static void Ensure()
        {
            if (_Built) return;
            var content = Content.Get();
            if (content == null || content.WorldCatalog == null) return;   // not ready — retry on the next relayout
            _Built = true;
            Builds++;
            Build();
        }

        /// recompute from scratch (gate re-runs use this to prove determinism)
        public static void Rebuild()
        {
            _Built = false;
            Ensure();
        }

        private static void Build()
        {
            Array.Clear(RoomFunc, 0, 64);
            Array.Clear(Downtown, 0, 64);
            Array.Clear(Vacation, 0, 64);
            Array.Clear(Community, 0, 64);
            Array.Clear(Studio, 0, 64);
            Array.Clear(Magic, 0, 64);
            Array.Clear(FunctionSub, 0, 64);
            var catalog = Content.Get().WorldCatalog;
            for (sbyte c = 0; c < 8; c++)
            {
                var items = catalog.GetItemsByCategory(c);
                if (items == null) continue;
                int f = FuncCanon(c);
                if (f < 0) continue;
                foreach (var it in items)
                {
                    if (it.RoomSort != 0)
                    {
                        var bits = LiveSubpanels.UIBuyBrowsePanel.CanonRoomToFlagBit;
                        for (int r = 0; r < 8; r++)
                            if ((it.RoomSort & (1 << bits[r])) != 0) RoomFunc[r * 8 + f] = 1;
                    }
                    ExpRow(it.DowntownSort, Downtown, f);
                    ExpRow(it.VacationSort, Vacation, f);
                    ExpRow(it.CommunitySort, Community, f);
                    ExpRow(it.StudiotownSort, Studio, f);
                    ExpRow(it.MagictownSort, Magic, f);
                    int sub = it.Subsort;
                    if (sub >= 0 && sub < 8) FunctionSub[f * 8 + sub] = 1;
                }
            }
        }

        /// the expansion row law: slots 0-3 = bits 1/2/4/8, slot 7 = 0x80
        /// (GetMagictownMask jump table; 4-6 never written).
        private static void ExpRow(byte sortByte, byte[] matrix, int f)
        {
            if (sortByte == 0) return;
            for (int s = 0; s < 4; s++)
                if ((sortByte & (1 << s)) != 0) matrix[s * 8 + f] = 1;
            if ((sortByte & 0x80) != 0) matrix[7 * 8 + f] = 1;
        }

        /// the consumption law: LoadBooks reads the row of the CURRENT main
        /// (subsort state only). mainRow = room display index (room mode),
        /// function canon index (function mode) or expansion plaque slot.
        public static bool IsEnabled(byte[] matrix, int mainRow, int subCol)
        {
            Ensure();
            if (mainRow < 0 || mainRow > 7 || subCol < 0 || subCol > 7) return false;
            return matrix[mainRow * 8 + subCol] != 0;
        }
    }
}
