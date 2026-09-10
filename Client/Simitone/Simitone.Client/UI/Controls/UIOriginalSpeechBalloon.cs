using FSO.Client;
using FSO.Client.UI.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Simitone.Client.UI.Controls
{
    /// <summary>
    /// R194: the speech-balloon chrome on the decoded law (evidence
    /// r194/r194-speech-balloon-law.md).
    ///
    /// Product::GenerateSpeechIcons (0x209cc0) loads kSpeechMediumBmp 9000
    /// (`Other\SpeechMedium.bmp` 16x16) and kSpeechLargeBmp 9001
    /// (`Other\SpeechLarge.bmp` 32x32) into the product's image handles and,
    /// when both resolve, composes the balloon through 0x236650 (edge
    /// composition with buffer vtable +0x28/+0xd4/+0x2c queries).
    /// Product::GetSpeechImage 0x209bf0 is consumed by
    /// cXObject::DrawSpriteSlot 0xcf74c — the in-game speech path.
    ///
    /// The port composes the balloon as a nine-slice of the tile: 16x16
    /// medium = 4px corners / 8px edges, 32x32 large = 8px corners / 16px
    /// edges (modeled from the tile halves; the compositor's exact edge
    /// arithmetic was not fully decoded, disclosed).
    /// </summary>
    public class UIOriginalSpeechBalloon : UIContainer
    {
        public const int MediumTile = 16, LargeTile = 32;
        public static readonly int[] CornerFor = { 4, 8 };   // medium, large

        public static int BalloonsMounted;

        private Texture2D Tile;
        private int Corner;
        public readonly UIOriginalText Label;
        public int ContentWidth, ContentHeight;
        public bool Large;

        public UIOriginalSpeechBalloon(string text, OriginalGlyphFont font, bool large)
        {
            Large = large;
            Tile = Simitone.Client.UI.Model.UIOriginal.EnsureResolved(
                large ? "Other\\\\SpeechLarge.bmp" : "Other\\\\SpeechMedium.bmp")?.Get(GameFacade.GraphicsDevice);
            Corner = CornerFor[large ? 1 : 0];
            Label = new UIOriginalText(text ?? "", font)
            {
                Position = new Vector2(Corner + 2, Corner)
            };
            Add(Label);
            BalloonsMounted++;
        }

        public void SetText(string text)
        {
            Label.Text = text ?? "";
            Label.Visible = !string.IsNullOrEmpty(Label.Text);
        }

        public void SizeToContent(int textWidth, int lineHeight)
        {
            ContentWidth = textWidth + 2 * (Corner + 2);
            ContentHeight = lineHeight + 2 * Corner;
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            if (Tile != null && ContentWidth > 0 && ContentHeight > 0)
            {
                var edge = Tile.Width - 2 * Corner;
                // nine-slice: corners, tiled edges, stretched center
                for (int gx = 0; gx < 3; gx++)
                {
                    int sx = gx * (Corner + (gx == 1 ? edge : 0));
                    int sw = (gx == 1) ? edge : Corner;
                    int dx = (gx == 0) ? 0 : (gx == 1 ? Corner : ContentWidth - Corner);
                    int dw = (gx == 1) ? ContentWidth - 2 * Corner : Corner;
                    for (int gy = 0; gy < 3; gy++)
                    {
                        int sy = gy * (Corner + (gy == 1 ? edge : 0));
                        int sh = (gy == 1) ? edge : Corner;
                        int dy = (gy == 0) ? 0 : (gy == 1 ? Corner : ContentHeight - Corner);
                        int dh = (gy == 1) ? ContentHeight - 2 * Corner : Corner;
                        if (dw <= 0 || dh <= 0) continue;
                        DrawLocalTexture(batch, Tile,
                            new Rectangle(sx, sy, sw, sh), new Vector2(dx, dy),
                            new Vector2(dw / (float)sw, dh / (float)sh));
                    }
                }
            }
            base.Draw(batch);
        }
    }
}
