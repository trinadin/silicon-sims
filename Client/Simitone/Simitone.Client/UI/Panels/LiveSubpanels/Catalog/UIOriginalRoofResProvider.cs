using FSO.Client;
using FSO.Client.UI.Controls.Catalog;
using Microsoft.Xna.Framework.Graphics;

namespace Simitone.Client.UI.Panels.LiveSubpanels.Catalog
{
    /// <summary>
    /// R130: the ORIGINAL build-mode roof PITCH sub-tools — the four
    /// kBldSbTlRoof* plaques (Res_CPanel.RT ids 3902 Steep / 3901 Medium /
    /// 3900 Shallow / 3904 Flat; canon in tools/iff-dump/r130/). Display
    /// order follows the engine panel's own STR# 147 'roofpanelstrs' layout
    /// directives: Steep (10;1), Medium (10;30), Shallow (10;50), Flat
    /// (10;71) — a descending-steepness column. Names and the description
    /// come from STR# 147 ([5]/[3]/[1]/[12] and lore [15]); the plaques are
    /// PICTORIAL (RLE8 reads — no baked text). The shared popup thumb is
    /// kBldPopupRoofPitch (133x103).
    /// </summary>
    public class UIOriginalRoofPitchResProvider : UICatalogResProvider
    {
        public static readonly int[] PitchIds = { 3902, 3901, 3900, 3904 };
        public static readonly string[] PitchMembers =
        {
            "cpanel\\Build\\RoofSteep.bmp",
            "cpanel\\Build\\RoofMedium.bmp",
            "cpanel\\Build\\RoofShallow.bmp",
            "cpanel\\Build\\RoofFlat.bmp",
        };
        public static readonly int[] PitchStrInd = { 5, 3, 1, 12 };

        // ENGINE (cWinRoofPanel::TSOnCommand 0x29ccec-0x29cda4 + the float
        // table behind TOC -20260 at 0x5a3bbc): flat 0.0, shallow pi/5
        // (0.6283), medium 3pi/20 (0.4712), steep pi/10 (0.3142) — and the
        // engine DIVIDES by pitch (RenderRoofPolys `fdiv f0,f0,f1` at
        // 0x2dd2c8/0x2dd49c), so its shallow value is the largest. The port's
        // roof geometry MULTIPLIES (RoofComponent z*pitch), so the four are
        // re-expressed preserving the engine's exact slope ratios
        // 1 : 4/3 : 2 (shallow : medium : steep) with medium anchored at the
        // port default 0.66 — the absolute scale is a disclosed interpretation.
        public static readonly float[] FSO_PITCH =
        {
            0.99f,   // steep   (3902)
            0.66f,   // medium  (3901)
            0.495f,  // shallow (3900)
            0f,      // flat    (3904)
        };

        public static float PitchForRtId(int rtId)
        {
            int i = System.Array.IndexOf(PitchIds, rtId);
            return i < 0 ? 0.66f : FSO_PITCH[i];
        }

        private static int IndexOf(ulong id)
        {
            return System.Array.IndexOf(PitchIds, (int)id);
        }

        public override Texture2D GetIcon(ulong id)
        {
            int i = IndexOf(id);
            if (i < 0) return null;
            return Simitone.Client.UI.Model.UIOriginal.EnsureResolved(PitchMembers[i])?.Get(GameFacade.GraphicsDevice);
        }

        public override Texture2D GetThumb(ulong id)
        {
            return Simitone.Client.UI.Model.UIOriginal.EnsureResolved("cpanel\\Build\\popuproofpitch.bmp")?.Get(GameFacade.GraphicsDevice);
        }

        public override string GetName(ulong id)
        {
            int i = IndexOf(id);
            if (i < 0) return "";
            return GameFacade.Strings.GetString("147", PitchStrInd[i].ToString());
        }

        public override string GetDescription(ulong id)
        {
            return GameFacade.Strings.GetString("147", "15");
        }

        public override int GetPrice(ulong id)
        {
            return 0;
        }

        public override bool DoDispose()
        {
            return false;
        }
    }

    /// <summary>
    /// R130: the ORIGINAL roof PATTERN swatches — GameData/Roofs textures
    /// (the engine's cWinRoofPanel::TraverseRoofDirectory enumerates the same
    /// directory) composed inside the roof category's OWN 45x45
    /// kRoofPatternTemplate frame (see UICatalogItem.RoofSwatch), with the
    /// shared kBldPopupRoofPattern thumb (145x103) and the STR# 147 [17]
    /// pattern lore as the description. The original shows no name for
    /// pattern swatches (BuildPatternButtons composes swatch + template only).
    /// </summary>
    public class UIOriginalRoofResProvider : UICatalogResProvider
    {
        public override Texture2D GetIcon(ulong id)
        {
            var roofs = FSO.Content.Content.Get().WorldRoofs;
            return roofs.Get(roofs.IDToName((int)id)).Get(GameFacade.GraphicsDevice);
        }

        public override Texture2D GetThumb(ulong id)
        {
            return Simitone.Client.UI.Model.UIOriginal.EnsureResolved("cpanel\\Build\\popuproofpattern.bmp")?.Get(GameFacade.GraphicsDevice);
        }

        public override string GetName(ulong id)
        {
            return "";
        }

        public override string GetDescription(ulong id)
        {
            return GameFacade.Strings.GetString("147", "17");
        }

        public override int GetPrice(ulong id)
        {
            return 0;
        }

        public override bool DoDispose()
        {
            return false;
        }
    }
}
