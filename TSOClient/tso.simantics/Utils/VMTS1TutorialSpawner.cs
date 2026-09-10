using FSO.Content;
using FSO.LotView.Model;
using System.Linq;

namespace FSO.SimAntics.Utils
{
    /// <summary>
    /// Port of the tutorial object spawner at the tail of Neighborhood::LoadHouse
    /// (0xb0540..0xb05a0; r247 decode.md §F, skeptic-corrections.md claim 4). The
    /// original runs five ordered guards after PostLoad, then creates the Tutorial
    /// object OUT-OF-WORLD via ObjectModule::MakeNewOutOfWorldObject — the port's
    /// equivalent is CreateObjectInstance at OUT_OF_WORLD, the same precedent used
    /// for controller objects. The ownership latch is deliberately not set here:
    /// the original acquires it only through the object's own TryTutorial
    /// primitive (VMContext.SetTutorialObject).
    ///
    /// Must run on the VM thread; the house-load path calls this from
    /// VMBlueprintRestoreCmd, which executes there.
    /// </summary>
    public static class VMTS1TutorialSpawner
    {
        /// <summary>
        /// The Tutorial object's GUID (lis 0xC325/addi -0x65E3 = 0xc3249a1d, built
        /// twice at 0xb0568/0xb0584). Exhaustive scans found no other creation
        /// path for it in the original executable.
        /// </summary>
        public const uint TutorialGUID = 0xc3249a1d;

        /// <summary>
        /// The cheat-toggled inhibit flag (native BSS 0x852c8, zero-filled:
        /// spawning enabled on a fresh session). Skeptic correction 1: the
        /// `tutorial` cheat stores flag = (param == 0), so `tutorial on` (param 1)
        /// clears the inhibit and `tutorial off` (param 0) sets it.
        /// </summary>
        public static bool InhibitSpawn;

        /// <summary>
        /// Count of actual out-of-world spawns RunHouseLoadSpawn has performed since
        /// process start (R248 battery seam): the positive evidence of the LoadHouse
        /// tail law, independent of what the tutorial lesson data does with the
        /// object afterwards.
        /// </summary>
        public static int SpawnEvidence;

        /// <summary>
        /// The five ordered LoadHouse-tail guards, then the out-of-world spawn:
        /// inhibit flag; neighborhood tutorial state &gt;= 3 (permanently done);
        /// simulator global 26 must be nonzero (the "tutorial session active in
        /// this house" latch, provided by the loaded house's SIMI); an object with
        /// the Tutorial GUID must not already exist; the object's selector must
        /// exist in the object folder.
        /// </summary>
        public static void RunHouseLoadSpawn(VM vm)
        {
            if (!vm.TS1) return;
            if (InhibitSpawn) return;
            var nbhd = Content.Content.Get().Neighborhood;
            if (nbhd == null || nbhd.Neighborhood == null) return;
            if (nbhd.TutorialState >= 3) return;
            if (vm.GetGlobalValue(26) == 0) return;
            if (vm.Entities.Any(x => x.Object?.OBJ?.GUID == TutorialGUID)) return;
            if (Content.Content.Get().WorldObjects.Get(TutorialGUID) == null) return;
            vm.Context.CreateObjectInstance(TutorialGUID, LotTilePos.OUT_OF_WORLD, Direction.NORTH);
            SpawnEvidence++;
        }
    }
}
