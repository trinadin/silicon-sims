using System;
using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Model;

namespace Simitone.Client.UI.Panels.CAS
{
    /// <summary>PersonFinder::BuildPersonBtn (0x23d2e0), using the owned BMP_2002 strip.</summary>
    public sealed class UIOriginalPersonPortrait : UIElement
    {
        public const int FrameSize = 45;
        public const int FaceWidth = 35, FaceHeight = 41;
        public const string TemplatePath = "cpanel\\People\\PeopleTemplate.bmp";
        private readonly Texture2D Portraits;
        private readonly Texture2D Template;
        private bool OwnsPortraits;
        public int Column;
        public int Row;
        public override Vector2 Size { get => new Vector2(FrameSize); set { } }
        public override Rectangle GetBounds() => new Rectangle(0, 0, FrameSize, FrameSize);

        public UIOriginalPersonPortrait(Texture2D portraits, Texture2D template)
        {
            Portraits = portraits;
            Template = template;
        }

        public static UIOriginalPersonPortrait Create(VMAvatar sim, bool useSavedPortrait)
        {
            var device = GameFacade.GraphicsDevice;
            var portraits = useSavedPortrait ? sim.Object.Resource.Get<BMP>(2002)?.GetTexture(device) : null;
            if (portraits != null && (portraits.Width != 105 || portraits.Height != 41))
            {
                portraits.Dispose();
                portraits = null;
            }
            portraits ??= OriginalPersonPortraitRenderer.Generate(device, sim.Avatar);
            return new UIOriginalPersonPortrait(portraits, UIOriginal.EnsureResolved(TemplatePath)?.Get(device))
                { OwnsPortraits = true };
        }

        public override void Removed()
        {
            if (OwnsPortraits) { Portraits.Dispose(); OwnsPortraits = false; }
            base.Removed();
        }

        // Native Init writes frame=(45,45), inset=(5,2), face=(35,41).
        // The 3 face views become rows; four template states become columns.
        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || Template == null || Portraits == null) return;
            int column = Math.Clamp(Column, 0, 3), row = Math.Clamp(Row, 0, 2);
            DrawLocalTexture(batch, Template, new Rectangle(column * FrameSize, 0, FrameSize, FrameSize),
                Vector2.Zero, Vector2.One, Color.White * Opacity);
            DrawLocalTexture(batch, Portraits, new Rectangle(row * FaceWidth, 0, FaceWidth, FaceHeight),
                new Vector2(5, 2), Vector2.One, Color.White * Opacity);
        }
    }
}
