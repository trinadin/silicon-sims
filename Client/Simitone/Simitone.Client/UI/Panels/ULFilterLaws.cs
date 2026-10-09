using System.Collections.Generic;
using System.IO;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// UI-38b: the UL filter-bar's two unbanked laws, production side.
    ///
    /// LAW 1 — LOT CATEGORY BITS (native lot+0x1ac). The native writer is
    /// NOT located (every direct stw-to-+0x1ac site in the PPC image is a
    /// window ctor/clear; the value arrives via a computed/indexed store —
    /// see the UI-38 receipt's disclosure). The port's representation: a
    /// per-lot census of the lot's own house-file objects (OBJT GUIDs)
    /// against a venue-marker table derived from the shipped corpus — every
    /// entry below is a corpus object whose family names the venue class
    /// (garden seeds/controllers = Gardening; cafe signs/counters/seating =
    /// Food; market/giftshop signs and carts = Shopping; pet-store signs,
    /// small-pet display cases = SmallAnimal; the dog/cat adoption floor
    /// pens = DogCat; park/play/fountain families = Recreation; no Lodging
    /// marker exists in the corpus — UL Old Town ships none, so that bit
    /// lawfully stays dark). Census ground truth (the LIVE n=1 community
    /// lots, the port's version-exact OBJT parse): 58=0x00, 61=0x6e,
    /// 70=0x7c, 71=0x2e, 72=0x04, 73=0x46, 74=0x26, 75=0x2e — the
    /// button-visibility law (a button shows iff at least one community lot
    /// carries its bit) yields six of the seven buttons on n=1 (all but
    /// Lodging).
    ///
    /// LAW 2 — FILTER PERSISTENCE (native LoadCurrentFilter @0x10458d60).
    /// Byte-proven storage: STR# 6 'Filter Bar Settings' in the
    /// neighborhood directory's LotLocations.iff, slot 3 (1-based) holding
    /// the persisted cmd-bit as a decimal string — every shipped
    /// UserData*/LotLocations.iff carries ['2','2','4','16','2048'] (the
    /// '16' = the 0x10 DogCat default). At community entry the Init tail
    /// applies the default and THEN loads+reprocesses the persisted value;
    /// every filter change writes it back.
    /// </summary>
    public static class ULFilterLaws
    {
        // corpus-derived venue markers (see the class comment for the law and
        // the disclosure). Generated from ExpansionPack5/Objects/ExpansionShared.
        private static readonly Dictionary<uint, int> VenueMarkers = new Dictionary<uint, int>
        {
            { 0x00FE9850u, 0x20 }, // Display Case - Masks
            { 0x02E286F1u, 0x04 }, // Controller - Garden
            { 0x03F2EB26u, 0x40 }, // Park Rocker Whale Middle
            { 0x04C55AD4u, 0x04 }, // Garden - Seeds - Tomato
            { 0x0CD9491Fu, 0x40 }, // Play Structure - Part C
            { 0x106C123Du, 0x40 }, // Play Structure - Part B
            { 0x1C27493Au, 0x08 }, // Table Gift Shop - Right
            { 0x1F287B65u, 0x20 }, // Display Counter Top - Collars
            { 0x25BA0090u, 0x08 }, // Table Gift Shop
            { 0x351112C2u, 0x04 }, // Garden - Seeds - Lettuce
            { 0x36016415u, 0x02 }, // Coffee Table - Cafe - Left
            { 0x36017FE9u, 0x02 }, // Coffee Table - Cafe - Right
            { 0x36113A6Du, 0x02 }, // Coffee Table - Cafe
            { 0x38232E50u, 0x02 }, // Sofa - Loveseat - Cafe
            { 0x3D3E7889u, 0x10 }, // Display - Floor - PetPenDog
            { 0x3F034CD9u, 0x40 }, // Play Structure - Part A
            { 0x45D91233u, 0x08 }, // Cart - Unleashed - Vegetable Reserve F
            { 0x4C442733u, 0x20 }, // Counter - Unleashed - Pet Store
            { 0x4C446F39u, 0x02 }, // Counter - Unleashed - Cafe
            { 0x52C81F9Bu, 0x40 }, // Pet - Play Fountain
            { 0x56BE5D67u, 0x40 }, // Play Structure - Part F
            { 0x5925B298u, 0x40 }, // Park Rocker Whale Front
            { 0x594A89F0u, 0x20 }, // Stereo - Unleashed - Pet Store Wall
            { 0x623A4754u, 0x40 }, // Play Structure - Part E
            { 0x6350E96Fu, 0x10 }, // Display - Floor - PetPenCat
            { 0x65670BBEu, 0x04 }, // Garden - Seeds - Bean
            { 0x6786A538u, 0x40 }, // Basketball - Standard - Front Empty Tile
            { 0x68AF92A3u, 0x40 }, // Park Rocker Horse
            { 0x68B803B0u, 0x08 }, // Cart - Unleashed - Vegetable Reserve E
            { 0x68FF9E84u, 0x40 }, // Park Rocker Horse Front
            { 0x6A53DE3Cu, 0x20 }, // Display Case - Fish
            { 0x6B45ADF0u, 0x40 }, // Park Rocker Horse Middle
            { 0x6BAD868Bu, 0x40 }, // Hydrant - Unleashed
            { 0x6BCDFAC1u, 0x40 }, // Park Rocker Horse Back
            { 0x70B0D5F2u, 0x20 }, // Display Case - Dog Collars
            { 0x724CA405u, 0x20 }, // Painting - Pet Store - Small
            { 0x7C5E5C6Du, 0x02 }, // Chair - Living Room - Cafe
            { 0x7E1F25B1u, 0x40 }, // Play Structure
            { 0x7E7A70F9u, 0x40 }, // Play Structure - Part D
            { 0x7F1AE4CFu, 0x08 }, // Sign - Building - GiftShop
            { 0x863B0CFEu, 0x40 }, // Play Structure - Part I
            { 0x8728A60Eu, 0x40 }, // Basketball - Standard - Empty Middle Tile
            { 0x8E2308BEu, 0x04 }, // Garden - Seeds - Carrot
            { 0x8FD67691u, 0x08 }, // Cart - Unleashed - Vegetable A
            { 0x94F61F3Au, 0x40 }, // Play Structure - Part H
            { 0x9864CE28u, 0x40 }, // Park Rocker Whale Back
            { 0x9AA2312Fu, 0x20 }, // Display Case - Turtle
            { 0x9F7295A9u, 0x20 }, // Display Counter Top - Candles
            { 0x9F733D4Fu, 0x08 }, // Cart - Unleashed - Vegetable
            { 0xA072951Fu, 0x02 }, // Table - Dining - Unleashed  
            { 0xA82CA08Eu, 0x40 }, // Fountain
            { 0xAC53F89Eu, 0x40 }, // Pet Gym - Part D
            { 0xACDBD5FBu, 0x40 }, // Pet Gym - Part C
            { 0xAD429EA9u, 0x40 }, // Pet Gym - Part F
            { 0xADC78F13u, 0x40 }, // Pet Gym - Part E
            { 0xAE34DBD2u, 0x40 }, // Pet Gym - Part H
            { 0xAE7B7CE6u, 0x40 }, // Play Structure - Part G
            { 0xAEB6C02Eu, 0x40 }, // Pet Gym - Part G
            { 0xAFB2ED10u, 0x40 }, // Pet Gym - Part I
            { 0xB01C2620u, 0x08 }, // Sign - Building - Market
            { 0xB0706E2Au, 0x02 }, // Sign - Building - Cafe
            { 0xB1B176BDu, 0x20 }, // Sign - Building - PetStore
            { 0xB26AB256u, 0x40 }, // Pet Gym
            { 0xB357AAC1u, 0x40 }, // Pet Gym - Part B
            { 0xB3E9FA5Cu, 0x40 }, // Pet Gym - Part A
            { 0xBA9DC16Au, 0x40 }, // Basketball - Standard
            { 0xD0EA267Fu, 0x08 }, // Cart - Unleashed - Vegetable B
            { 0xD985A7ADu, 0x20 }, // Display Counter Top - Seeds
            { 0xDB240A0Eu, 0x20 }, // Display Case - Dog Toys
            { 0xED4818F7u, 0x08 }, // Table Gift Shop - Left
            { 0xF126EF7Cu, 0x40 }, // Park Rocker Whale
            { 0xF444866Cu, 0x40 }, // Basketball - Standard - Backboard
            { 0xFEFDA8D6u, 0x02 }, // Food Counter - Coffee
            { 0xFFC0EA58u, 0x20 }, // Display Counter Top - Pet Treats
        };

        /// <summary>LAW 1: the per-lot category bits from the lot's own
        /// house-file object census (native lot+0x1ac, port representation —
        /// see the class comment).</summary>
        public static int ComputeCategoryBits(IffFile houseIff)
        {
            if (houseIff == null) return 0;
            var objt = houseIff.Get<OBJT>(0);
            if (objt?.Entries == null) return 0;
            var bits = 0;
            foreach (var e in objt.Entries)
            {
                int bit;
                if (e.GUID != 0 && VenueMarkers.TryGetValue(e.GUID, out bit)) bits |= bit;
            }
            return bits;
        }

        /// <summary>LAW 2: the persisted filter cmd-bit (STR# 6 slot 3,
        /// 1-based). Returns -1 when absent/corrupt (the caller keeps the
        /// Init default, matching the native's parse-fail path).</summary>
        public static int LoadPersistedFilterCmd(IffFile lotLocations)
        {
            try
            {
                var str = lotLocations?.Get<STR>(6);
                var s = str?.GetString(2); // 0-based slot 3
                if (string.IsNullOrEmpty(s)) return -1;
                var v = int.Parse(s.Trim());
                return (v >= 1 && v <= 0x7F) ? v : -1;
            }
            catch { return -1; }
        }

        /// <summary>LAW 2: write the persisted cmd-bit back to the
        /// neighborhood's LotLocations.iff (STR# 6 slot 3).</summary>
        public static void SavePersistedFilterCmd(string lotLocationsPath, int cmdBit)
        {
            try
            {
                var iff = new IffFile(lotLocationsPath);
                var str = iff.Get<STR>(6);
                if (str == null) return;
                str.SetStringForce(2, cmdBit.ToString());
                var tmp = lotLocationsPath + ".tmp";
                using (var s = File.Create(tmp)) iff.Write(s);
                if (File.Exists(lotLocationsPath)) File.Delete(lotLocationsPath);
                File.Move(tmp, lotLocationsPath);
            }
            catch { /* parse/write failures keep the last good file */ }
        }
    }
}
