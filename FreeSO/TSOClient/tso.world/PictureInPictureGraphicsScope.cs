using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FSO.LotView
{
    // Secondary rendering shares loaded shader programs with the main world.
    // Preserve values as well as GPU bindings: restoring a camera alone leaves
    // avatar, terrain and sprite uniforms pointing at the secondary viewport.
    internal sealed class PictureInPictureGraphicsScope : IDisposable
    {
        private readonly GraphicsDevice Device;
        private readonly RenderTargetBinding[] Targets;
        private readonly Viewport Viewport;
        private readonly Rectangle Scissor;
        private readonly BlendState Blend;
        private readonly Color BlendFactor;
        private readonly DepthStencilState Depth;
        private readonly RasterizerState Rasterizer;
        private readonly IndexBuffer Indices;
        private readonly VertexBufferBinding[] Vertices;
        private readonly List<Action> Restore = new List<Action>();
        private bool Disposed;

        internal PictureInPictureGraphicsScope(GraphicsDevice gd)
        {
            Device = gd;
            Targets = gd.GetRenderTargets();
            Viewport = gd.Viewport;
            Scissor = gd.ScissorRectangle;
            Blend = gd.BlendState;
            BlendFactor = gd.BlendFactor;
            Depth = gd.DepthStencilState;
            Rasterizer = gd.RasterizerState;
            Indices = gd.Indices;
            Vertices = GetVertexBindings(gd);
            CaptureTextures(gd.Textures, gd.SamplerStates);
            CaptureTextures(gd.VertexTextures, gd.VertexSamplerStates);
            foreach (var effect in WorldContent.LightEffects.Cast<Effect>().Distinct())
            {
                var technique = effect.CurrentTechnique;
                Restore.Add(() => effect.CurrentTechnique = technique);
                foreach (var parameter in effect.Parameters) Capture(parameter);
            }
        }

        // The pinned FSOMonoGame build has the binding getter on its internal
        // collection, but does not expose GraphicsDevice.GetVertexBuffers.
        // Read that existing getter rather than patching the graphics library
        // or discarding the previous vertex binding at the end of a capture.
        [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicFields, typeof(GraphicsDevice))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods,
            "Microsoft.Xna.Framework.Graphics.VertexBufferBindings", "MonoGame.Framework")]
        private static VertexBufferBinding[] GetVertexBindings(GraphicsDevice gd)
        {
            var getter = typeof(GraphicsDevice).GetMethod("GetVertexBuffers", Type.EmptyTypes);
            if (getter != null) return (VertexBufferBinding[])getter.Invoke(gd, null);
            var field = typeof(GraphicsDevice).GetField("_vertexBuffers", BindingFlags.Instance | BindingFlags.NonPublic);
            var bindings = field?.GetValue(gd);
            var get = bindings?.GetType().GetMethod("Get", Type.EmptyTypes);
            if (get == null) throw new NotSupportedException("This graphics build cannot preserve vertex bindings for PIP.");
            return (VertexBufferBinding[])get.Invoke(bindings, null);
        }

        private void CaptureTextures(TextureCollection textures, SamplerStateCollection samplers)
        {
            // The collections do not expose Count; the bound is the device's
            // actual slot count rather than a guessed desktop GL capability.
            for (int slot = 0; slot < 32; slot++)
            {
                Texture texture;
                SamplerState sampler;
                try { texture = textures[slot]; sampler = samplers[slot]; }
                catch (IndexOutOfRangeException) { break; }
                var index = slot;
                Restore.Add(() =>
                {
                    if (!ReferenceEquals(textures[index], texture)) textures[index] = texture;
                    if (!ReferenceEquals(samplers[index], sampler)) samplers[index] = sampler;
                });
            }
        }

        private void Capture(EffectParameter parameter)
        {
            if (parameter.Elements.Count > 0)
            {
                foreach (var element in parameter.Elements) Capture(element);
                return;
            }
            if (parameter.StructureMembers.Count > 0)
            {
                foreach (var member in parameter.StructureMembers) Capture(member);
                return;
            }
            switch (parameter.ParameterType)
            {
                case EffectParameterType.Single:
                    if (parameter.ParameterClass == EffectParameterClass.Matrix)
                    {
                        // GetValueMatrix only supports 4x4, while bone bindings
                        // can be rectangular. GetValueSingleArray exposes the
                        // same column-major values consumed by SetValue(Matrix).
                        var data = parameter.GetValueSingleArray();
                        int rows = parameter.RowCount, cols = parameter.ColumnCount;
                        Func<int,int,float> value = (r,c) => r < rows && c < cols ? data[c * rows + r] : 0;
                        var matrix = new Matrix(value(0,0),value(0,1),value(0,2),value(0,3),
                            value(1,0),value(1,1),value(1,2),value(1,3),
                            value(2,0),value(2,1),value(2,2),value(2,3),
                            value(3,0),value(3,1),value(3,2),value(3,3));
                        Restore.Add(() => parameter.SetValue(matrix));
                    }
                    else if (parameter.ParameterClass == EffectParameterClass.Vector)
                    {
                        switch (parameter.ColumnCount)
                        {
                            case 2: var v2 = parameter.GetValueVector2(); Restore.Add(() => parameter.SetValue(v2)); break;
                            case 3: var v3 = parameter.GetValueVector3(); Restore.Add(() => parameter.SetValue(v3)); break;
                            case 4: var v4 = parameter.GetValueVector4(); Restore.Add(() => parameter.SetValue(v4)); break;
                        }
                    }
                    else { var scalar = parameter.GetValueSingle(); Restore.Add(() => parameter.SetValue(scalar)); }
                    break;
                case EffectParameterType.Bool:
                    var boolean = parameter.GetValueBoolean(); Restore.Add(() => parameter.SetValue(boolean)); break;
                case EffectParameterType.Int32:
                    var integer = parameter.GetValueInt32(); Restore.Add(() => parameter.SetValue(integer)); break;
                case EffectParameterType.Texture2D:
                    var texture2D = parameter.GetValueTexture2D(); Restore.Add(() => parameter.SetValue(texture2D)); break;
                case EffectParameterType.Texture3D:
                    var texture3D = parameter.GetValueTexture3D(); Restore.Add(() => parameter.SetValue(texture3D)); break;
                case EffectParameterType.TextureCube:
                    var textureCube = parameter.GetValueTextureCube(); Restore.Add(() => parameter.SetValue(textureCube)); break;
            }
        }

        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            Device.SetRenderTargets(Targets);
            Device.Viewport = Viewport;
            Device.ScissorRectangle = Scissor;
            foreach (var restore in Restore) restore();
            Device.BlendState = Blend;
            Device.BlendFactor = BlendFactor;
            Device.DepthStencilState = Depth;
            Device.RasterizerState = Rasterizer;
            Device.Indices = Indices;
            Device.SetVertexBuffers(Vertices);
        }
    }
}
