using System;
using System.Collections.Generic;
using System.Linq;
using FSO.Client;
using FSO.Client.UI.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Panels;

namespace Simitone.Client
{
    internal static class AutotestPIPCaption240
    {
        // Invoke before UI drawing, alongside the corpus checks. All graphics
        // resources here are temporary; original font atlases remain borrowed.
        internal static bool Check(out string diagnostics)
        {
            var failures = new List<string>();
            Action<bool,string> require = (ok,label) => { if (!ok) failures.Add(label); };
            Func<string,int,int> fit = (text,width) => UIOriginalPIPCaption.NativeLineLength(text,0,width,c=>4,c=>5);
            require(fit("",16)==0,"empty");
            require(fit("ABC",14)==3,"exact-final-ink");
            require(fit("ABCDEFG",16)==2,"native-long-word-count-minus-one");
            require(fit("AB-CD",16)==3,"hyphen-included");
            require(fit("AB CD",16)==3,"space-included");
            require(fit("AB   CD",16)==5,"trailing-spaces-consumed");
            require(fit("A\nBCD",100)==2,"newline-included");
            require(fit("ABCDE FG",2)==6,"too-narrow-word-and-space");
            require(fit("A",0)==1,"forward-progress");
            require(new[]{0,1,2,3}.Select(UIOriginalPictureInPicture.AvatarVerticalOffset)
                .SequenceEqual(new[]{-9,-19,-39,-78}),"avatar-main-zoom-offset");
            require(UIOriginalPictureInPicture.ObjectVerticalOffset(160,3,3)==-20 &&
                UIOriginalPictureInPicture.ObjectVerticalOffset(160,3,1)==-5 &&
                UIOriginalPictureInPicture.ObjectVerticalOffset(40,1,3)==-20,"object-damage-offset");
            var gd = GameFacade.GraphicsDevice;
            var targets = gd.GetRenderTargets();
            var viewport = gd.Viewport;
            var blend = gd.BlendState;
            var depth = gd.DepthStencilState;
            var rasterizer = gd.RasterizerState;
            var sampler = gd.SamplerStates[0];
            var oldTexture = gd.Textures[0];
            try
            {
                var font = OriginalGlyphFont.LoadByIndex(12,gd);
                if (font?.Atlas == null) throw new InvalidOperationException("Owned font12 unavailable");
                var caption = new UIOriginalPIPCaption(font) { Pixels=100, Text="A caption", Position=new Vector2(16,16) };
                caption.CalculateMatrix();
                using (var target = new RenderTarget2D(gd,132,132,false,SurfaceFormat.Color,DepthFormat.None))
                using (var batch = new UISpriteBatch(gd,0))
                {
                    Action draw = () => {
                        gd.SetRenderTarget(target); gd.Clear(Color.Transparent);
                        batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,
                            DepthStencilState.None,RasterizerState.CullNone);
                        caption.Draw(batch); batch.End();
                        gd.SetRenderTargets(targets);
                    };
                    draw();
                    var pixels = new Color[132*132]; target.GetData(pixels);
                    int ink = 0, outside = 0;
                    for(int y=0;y<132;y++) for(int x=0;x<132;x++) if(pixels[y*132+x].A>0)
                    {
                        ink++;
                        if(x<16 || x>=116 || y<16 || y>=116) outside++;
                    }
                    require(ink>20 && outside==0,"actual-caption-ink-contained("+ink+","+outside+")");
                    caption.Text="A\nB\nC\nD\nE\nF";
                    draw(); target.GetData(pixels);
                    require(caption.LineCount==6 && pixels.All(p=>p.A==0),"native-overheight-draws-nothing");
                }
            }
            catch(Exception ex) { failures.Add(ex.GetType().Name+":"+ex.Message); }
            finally
            {
                gd.SetRenderTargets(targets); gd.Viewport=viewport;
                gd.BlendState=blend; gd.DepthStencilState=depth; gd.RasterizerState=rasterizer;
                gd.SamplerStates[0]=sampler; gd.Textures[0]=oldTexture;
            }
            diagnostics = "native caption wrap/ink/bounds/vertical offsets "+(failures.Count==0?"pass":string.Join(",",failures));
            return failures.Count==0;
        }
    }
}
