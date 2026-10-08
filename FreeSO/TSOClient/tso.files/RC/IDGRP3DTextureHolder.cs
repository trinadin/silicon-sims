using Microsoft.Xna.Framework.Graphics;

namespace FSO.Files.RC
{
    public interface IDGRP3DTextureHolder
    {
        void Decode(GraphicsDevice gd);
        Texture2D GetTexture(GraphicsDevice gd);
    }
}
