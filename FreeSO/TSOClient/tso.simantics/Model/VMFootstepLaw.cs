using System.Collections.Generic;
using FSO.LotView.Model;

namespace FSO.SimAntics.Model
{
    /// <summary>
    /// AUD-20 — the footstep surface/sound law, decoded from the original
    /// PPC engine (The Sims Complete). Full decode with addresses in
    /// coordination/evidence/AUD-20/footstep-law.md.
    ///
    /// Native pipeline: a "footstep" TimeProps event in a walk animation
    /// (values are authored as 1/2 forward and -1/-2 backward; 508 events
    /// ship in Animation.far) reaches
    /// SAnimator::HandleVitaBoyAnimEvent (0x1034ca10, branch 0x1034cd70),
    /// which computes the avatar's tile, classifies the surface with
    /// SAnimator::GetFootSound (0x10357c60), picks one of the footstep_*
    /// event names via the 12-case jumptable at 0x1061a00c, and plays it
    /// through cSoundPlayer::PlayBySource (0x10300550) with the person's
    /// object id as source — the same dispatch and volume law as the
    /// "sound" anim-event key.
    ///
    /// Surface classes (native return values):
    ///   0 grass/terrain  1 deck  2 no-floor(0xFF)  3 floor-sound 0
    ///   4 floor-sound 2  5 (unreachable)  6 plant  7 trash  8 ash
    ///   9 roach  10 puddle  11 pool
    /// </summary>
    public static class VMFootstepLaw
    {
        /// <summary>
        /// Object GUIDs that re-classify the tile under the walker
        /// (native GetFootSound GUID table; max-wins across all objects).
        /// </summary>
        private static readonly Dictionary<uint, int> SurfaceGUIDs = new Dictionary<uint, int>
        {
            { 0xA2DAA08Cu, 6 },  // plant
            { 0x96E3398Du, 6 },  // plant
            { 0xC02B406Au, 6 },  // plant
            { 0x65274A4Fu, 6 },  // plant
            { 0x9BE030B9u, 10 }, // puddle
            { 0x5C67FC8Fu, 10 }, // puddle
            { 0x3E7470F6u, 10 }, // puddle
            { 0x63416BA1u, 8 },  // ash
            { 0xBF62F653u, 7 },  // trash
            { 0x7F907075u, 7 },  // trash
            { 0x05F5E0FFu, 9 },  // roaches (decimal 99999999)
        };

        /// <summary>
        /// Barefoot outfit ids (native: outfit in {1,5,10,14}, bit test
        /// 1&lt;&lt;(outfit-1) &amp; 0x2013 at 0x1034ce38) — Naked,
        /// Sleepwear, Luau, Expanded Swimsuit in the VMPersonSuits
        /// numbering shared with the port.
        /// </summary>
        public static bool IsNoShoeOutfit(int outfit)
        {
            return outfit == 1 || outfit == 5 || outfit == 10 || outfit == 14;
        }

        /// <summary>
        /// Port equivalent of GetFootSound for the avatar's current tile.
        /// DISCLOSED port mappings (native column in the evidence receipt):
        ///  - the native water@level-0 branch is dead code (HasWater itself
        ///    requires level 1) and is intentionally not implemented;
        ///  - pool/deck come from the port floor grid markers (0xFFFF pool,
        ///    0xFFFE deck) instead of the native level-1 pool grids;
        ///  - the native object scan filtered PlacementSpec==0; the port
        ///    scans every entity on the tile.
        /// </summary>
        public static int GetSurfaceClass(FSO.SimAntics.VMAvatar avatar)
        {
            var ctx = avatar.Thread?.Context;
            if (ctx == null || ctx.Architecture == null) return 0;

            var pos = avatar.Position;
            if (pos == LotTilePos.OUT_OF_WORLD) return -1; // native -999 unplaced gate

            short tx = pos.TileX, ty = pos.TileY;

            // 1) object GUID scan, max-wins
            int best = 0;
            foreach (var ent in ctx.VM.Entities)
            {
                if (ent == avatar || ent.Object == null) continue;
                var p = ent.Position;
                if (p == LotTilePos.OUT_OF_WORLD || p.Level != pos.Level) continue;
                if (p.TileX != tx || p.TileY != ty) continue;
                int sc;
                if (SurfaceGUIDs.TryGetValue((uint)ent.Object.GUID, out sc) && sc > best) best = sc;
            }
            if (best > 0) return best;

            // 2) floor pattern (native reads a ushort per tile: 0xFF -> 2,
            //    0 -> terrain 0, else the catalog sound byte)
            var arch = ctx.Architecture;
            FloorTile ft;
            try { ft = arch.GetFloor(tx, ty, pos.Level); }
            catch { return 0; }
            var pat = ft.Pattern;
            if (pat == 0xFFFF) return 11; // pool (port marker)
            if (pat == 0xFFFE) return 1;  // deck (port marker)
            if (pat == 0x00FF) return 2;  // native "no floor" 0xFF -> hard
            if (pat == 0) return 0;       // grass / terrain

            // 3) catalog sound byte -> class (native GetFootSound tail:
            //    fs 0->3, 1->2, 2->4, 3->1)
            int fs = 2; // native map-miss default
            var floors = FSO.Content.Content.Get().WorldFloors;
            if (floors != null) fs = floors.GetFloorSound(pat);
            switch (fs)
            {
                case 0: return 3;
                case 1: return 2;
                case 3: return 1;
                default: return 4; // fs 2 (soft) and unknown (miss->2)
            }
        }

        /// <summary>
        /// The 12-case name jumptable at 0x1061a00c. noShoe selects the
        /// _noshoe twin for the paired families (hard/medium/soft and the
        /// terrain default). footstep_terrain_noshoe is corpus-DEAD (AUD-19
        /// pin): that pairing falls back to footstep_terrain so the dead id
        /// can never be dispatched.
        /// The class-0 default branch also consults the neighborhood
        /// terrain id natively (40..42 snow, 99 soft — house sub-object the
        /// port does not model); DISCLOSED: port always takes the default
        /// (grass) arm.
        /// </summary>
        public static string SoundNameForClass(int surfaceClass, bool noShoe)
        {
            switch (surfaceClass)
            {
                case 6: return "footstep_plant";
                case 7: return "footstep_trash";
                case 8: return "footstep_ash";
                case 9: return "footstep_roach";
                case 10: return "footstep_puddle";
                case 11: return "footstep_pool_swim_stroke";
                case 1:
                case 2: return noShoe ? "footstep_hard_noshoe" : "footstep_hard";
                case 3:
                case 5: return noShoe ? "footstep_medium_noshoe" : "footstep_medium";
                case 4: return noShoe ? "footstep_soft_noshoe" : "footstep_soft";
                // case 0 and anything unexpected: default (grass) naming.
                // The native noShoe twin here is footstep_terrain_noshoe —
                // corpus-DEAD, so both arms play the live name.
                default:
                    return "footstep_terrain";
            }
        }
    }
}
