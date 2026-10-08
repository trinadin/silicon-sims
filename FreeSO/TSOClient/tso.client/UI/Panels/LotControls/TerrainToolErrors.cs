using System;
using FSO.Client;

namespace FSO.Client.UI.Panels.LotControls
{
    /// <summary>
    /// R203: the original dirt-tool error surface, decoded from the PPC engine
    /// (tools/iff-dump/r203/r203-dirt-tool-law.md). cDirtTool::UpdateErrorMessage
    /// @0x18d230 switches on this->0x38 with code = UIText STR# 149 index + 1 and
    /// routes EVERY case — errors and the plain cost readout alike — through
    /// cTool::SetToolTip @0x191c30, the shared tooltip window that never
    /// recolors (the R198 color law: BLACK). The error codes set by the engine
    /// family (cDirtTool / cLevelDirtTool / cGrassTool):
    ///   0  clear                                  (Drag 0x173b2c, Float, Select, ctors)
    ///   1  MayModifyTerrain fail @0x18d610        (map bounds / GetRoofLayer
    ///      0x162ab0 / HasFlatObjects 0x18dba0) + cLevelDirtTool::Apply flat-object
    ///      corners + ForceGrade  → STR# 149[0] 'Tile cannot be modified'
    ///   5  cLevelDirtTool::Apply 0x17349c height loop → STR# 149[4]
    ///      "Can't divide a multi-tile object"
    ///   6  cLevelDirtTool::Drag 0x173b5c: SetFunds(cost) left funds < cost
    ///      → STR# 149[5] 'Insufficient funds'
    /// The cost readout itself is the head of UpdateErrorMessage: no error and
    /// this->0x30 != 0 formats "$N" / "-$N" into the same BLACK tooltip — the
    /// port's old DarkRed cost tooltip and VMPlacementError popups had no
    /// engine counterpart on this family.
    /// </summary>
    public static class TerrainToolErrors
    {
        public const int CodeNone = 0;
        public const int CodeTileCannotBeModified = 1;
        public const int CodeCantDivideMultiTile = 5;
        public const int CodeInsufficientFunds = 6;

        private static string S(int index)
        {
            return GameFacade.Strings.GetString("149", index.ToString());
        }

        /// <summary>STR# 149 text for an error code; null for none/unknown
        /// (the engine's default case clears the tooltip).</summary>
        public static string Text(int code)
        {
            switch (code)
            {
                case CodeTileCannotBeModified: return S(0);
                case CodeCantDivideMultiTile: return S(4);
                case CodeInsufficientFunds: return S(5);
                default: return null;
            }
        }

        /// <summary>The engine's cost readout ("$N" for a cost, "-$N" for a
        /// refund), shown through the same BLACK tooltip while a drag is
        /// active and no error is set (UpdateErrorMessage head path).</summary>
        public static string CostText(int cost)
        {
            return (cost < 0) ? ("-$" + (-cost)) : ("$" + cost);
        }
    }
}
