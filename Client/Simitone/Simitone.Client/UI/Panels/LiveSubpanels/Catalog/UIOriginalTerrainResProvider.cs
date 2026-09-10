using System;
using FSO.Client;
using FSO.Client.UI.Controls.Catalog;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Model;

namespace Simitone.Client.UI.Panels.LiveSubpanels.Catalog
{
    // R127: the ORIGINAL build-mode terrain tools. Replaces the TSO-only
    // UIFileIDs provider, whose IDs cannot resolve on TS1 data (UIElement
    // returns a 1x1 white fallback — the cells rendered blank) and which
    // mounted a wrong-asset placeholder (a TSO wallpaper button) for Grass.
    // Mapping per res_cpanel.RT + the FSO provider's own TSO comments
    // (icon = the kBldSbTl* cell plaque, thumb = the kBldPopup* popup art):
    //   id 0 raise -> TerrainUpIcon / PopupTerrainUp        (cpanel\Build)
    //   id 1 level -> TerrainLevelIcon / PopupTerrainLevel   (cpanel\Build)
    //   id 2 grass -> TerrainGrassIcon / popupGrass          (cpanel\HDBuild, Hot Date)
    //   id 3 lower -> TerrainDownIcon / PopupTerrainDown     (cpanel\Build)
    // id 3 is the R127 split of the port's combined raise/lower tool — the
    // original has distinct icons AND distinct popups per direction.
    // The plaques are pictorial (no baked text) and UIText.iff carries NO
    // tool-name strings (r127 scan; the TSO table f107 is absent) — names
    // below are port-authored, disclosed. The popup thumbs carry the
    // original instructions as art, so GetDescription stays null.
    public class UIOriginalTerrainResProvider : UICatalogResProvider
    {
        public static string[] IconMembers = new string[]
        {
            "cpanel\\Build\\TerrainUpIcon.BMP",
            "cpanel\\Build\\TerrainLevelIcon.BMP",
            "cpanel\\HDBuild\\TerrainGrassIcon.BMP",
            "cpanel\\Build\\TerrainDownIcon.BMP"
        };
        public static string[] ThumbMembers = new string[]
        {
            "cpanel\\Build\\PopupTerrainUp.bmp",
            "cpanel\\Build\\PopupTerrainLevel.bmp",
            "cpanel\\HDBuild\\popupGrass.bmp",
            "cpanel\\Build\\PopupTerrainDown.bmp"
        };
        public static string[] ToolNames = new string[]
        {
            "Raise Terrain", "Flatten Terrain", "Grass Tool", "Lower Terrain"
        };

        private static Texture2D Resolve(string member)
        {
            if (member == null) return null;
            try { return UIOriginal.EnsureResolved(member)?.Get(GameFacade.GraphicsDevice); }
            catch { return null; }
        }

        public override Texture2D GetIcon(ulong id)
        {
            var i = (int)id;
            return (i >= 0 && i < IconMembers.Length) ? Resolve(IconMembers[i]) : null;
        }

        public override Texture2D GetThumb(ulong id)
        {
            var i = (int)id;
            return (i >= 0 && i < ThumbMembers.Length) ? Resolve(ThumbMembers[i]) : null;
        }

        public override string GetName(ulong id)
        {
            var i = (int)id;
            return (i >= 0 && i < ToolNames.Length) ? ToolNames[i] : null;
        }

        public override string GetDescription(ulong id)
        {
            return null; // the original instructions live in the popup art (r127 RLE8 reads)
        }

        public override int GetPrice(ulong id)
        {
            return 1;
        }

        public override bool DoDispose()
        {
            return false;
        }
    }
}
