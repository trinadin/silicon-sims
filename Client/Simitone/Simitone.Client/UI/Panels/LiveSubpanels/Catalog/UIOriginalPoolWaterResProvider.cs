using FSO.Client;
using FSO.Client.UI.Controls.Catalog;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Model;

namespace Simitone.Client.UI.Panels.LiveSubpanels.Catalog
{
    /// <summary>
    /// Original pool and pond subtool presentation. WorldFloorProvider exposes
    /// these as sentinel floor ids so UIFloorPainter can execute them, but its
    /// isometric floor thumbnails are not their control-panel art. The shipped
    /// UI supplies four-state 45x45 sheets plus dedicated 145x103 popup art.
    /// </summary>
    public class UIOriginalPoolWaterResProvider : UICatalogResProvider
    {
        public const ulong PoolID = 65535;
        public const ulong WaterID = 65534;

        public static string IconMember(ulong id)
        {
            return id == PoolID ? "cpanel\\Build\\PoolToolIcon.bmp"
                : id == WaterID ? "cpanel\\HDBuild\\WaterToolIcon.bmp" : null;
        }

        public static string ThumbMember(ulong id)
        {
            return id == PoolID ? "cpanel\\Build\\PopupPool.BMP"
                : id == WaterID ? "cpanel\\HDBuild\\PopupWaterTool.bmp" : null;
        }

        private static Texture2D Resolve(string member)
        {
            if (member == null) return null;
            try { return UIOriginal.EnsureResolved(member)?.Get(GameFacade.GraphicsDevice); }
            catch { return null; }
        }

        public override Texture2D GetIcon(ulong id)
        {
            return Resolve(IconMember(id));
        }

        public override Texture2D GetThumb(ulong id)
        {
            return Resolve(ThumbMember(id));
        }

        public override string GetName(ulong id)
        {
            FSO.Content.FloorReference entry;
            return FSO.Content.Content.Get().WorldFloors.Entries.TryGetValue((ushort)id, out entry)
                ? entry.Name : null;
        }

        public override string GetDescription(ulong id)
        {
            FSO.Content.FloorReference entry;
            return FSO.Content.Content.Get().WorldFloors.Entries.TryGetValue((ushort)id, out entry)
                ? entry.Description : null;
        }

        public override int GetPrice(ulong id)
        {
            FSO.Content.FloorReference entry;
            return FSO.Content.Content.Get().WorldFloors.Entries.TryGetValue((ushort)id, out entry)
                ? entry.Price : 0;
        }

        public override bool DoDispose()
        {
            return false;
        }
    }
}
