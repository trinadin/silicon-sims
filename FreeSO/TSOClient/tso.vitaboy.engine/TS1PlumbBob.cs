using System;
using FSO.Common.Utils;
using FSO.Content.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FSO.Vitaboy
{
    /// <summary>
    /// The owned TS1 head-arrow asset and its native Animator laws.
    /// Native references and reproducible checks: coordination/evidence/PLUMB-01.
    /// </summary>
    public sealed class TS1PlumbBob
    {
        public const string MeshName = "xskin-head-arrow-ROOT-ARROW";
        public const float HeadOffset = 3f;
        public const double RevolutionSeconds = 2;

        private readonly DGRP3DVert[] Vertices;
        private readonly int[] Indices;
        private readonly ITextureRef Texture;

        public TS1PlumbBob()
        {
            var content = Content.Content.Get();
            var mesh = content.AvatarMeshes.Get(MeshName);
            Texture = content.AvatarTextures.Get(mesh.TextureName);
            Vertices = new DGRP3DVert[mesh.VertexBuffer.Length];
            for (int i = 0; i < Vertices.Length; i++)
            {
                var vertex = mesh.VertexBuffer[i];
                Vertices[i] = new DGRP3DVert(vertex.Position, vertex.Normal, vertex.TextureCoordinate);
            }
            Indices = (int[])mesh.IndexBuffer.Clone();
        }

        public static Matrix LocalTransform(Vector3 headPosition, double seconds)
        {
            // SAnimator::Tick copies slot 1's bone TRANSLATION, without head rotation.
            // Animator::Render gives ROOT an Euler(pi/2, 0, elapsedMils*pi/1000)
            // rotation and (0,3,0) translation. BMF/BCF handedness converts this to
            // XNA's -pi/2 X rotation followed by the Y spin. No selection acceleration.
            float angle = (float)((seconds % RevolutionSeconds) * Math.PI);
            return Matrix.CreateRotationX(-MathHelper.PiOver2)
                * Matrix.CreateRotationY(angle)
                * Matrix.CreateTranslation(headPosition + new Vector3(0, HeadOffset, 0));
        }

        public static Vector3 MoodColor(int mood)
        {
            // Animator::Render, mode 1 (head-arrow): 0x47e614..0x47e6dc.
            float value = MathHelper.Clamp(1.2f * mood / 100f, -1f, 1f);
            if (value < 0)
            {
                float remaining = 1 - value * value;
                return new Vector3(1, remaining,
                    value > -0.4f ? remaining - (0.4f + value) : remaining);
            }
            // AUD-18-E: the blue arm is the native subtraction (1−v)−(0.4−v),
            // kept in that exact form — a literal 0.6f differs by 1 ulp for
            // 13 of 201 mood values.
            return new Vector3(1 - value, 1, value < 0.4f ? (1 - value) - (0.4f - value) : 1 - value);
        }

        public static Vector3 LightDirection(Matrix view)
        {
            // Animator's private directional light follows the camera ray, transformed
            // by native RotationTf(Y,80deg)*RotationTf(X,30deg). Convert handedness.
            var cameraRay = Matrix.Invert(view).Forward;
            return Vector3.Normalize(Vector3.TransformNormal(cameraRay,
                Matrix.CreateRotationY(-MathHelper.ToRadians(80))
                * Matrix.CreateRotationX(MathHelper.ToRadians(30))));
        }

        public static Vector3 FacetLight(Vector3 color, Vector3 worldNormal, Vector3 lightDirection)
        {
            // Native light intensity 1; ambient = 0.05 + 0.25*channel.
            float diffuse = Math.Max(0, Vector3.Dot(Vector3.Normalize(worldNormal), -lightDirection));
            return Vector3.Clamp(new Vector3(0.05f) + color * (0.25f + diffuse), Vector3.Zero, Vector3.One);
        }

        public void Draw(GraphicsDevice device, Effect effect, Vector3 head, int mood, double seconds)
        {
            var world = effect.Parameters["World"].GetValueMatrix();
            var ambient = effect.Parameters["AmbientLight"].GetValueVector4();
            var technique = effect.CurrentTechnique;
            var texture = effect.Parameters["MeshTex"].GetValueTexture2D();
            var specular = effect.Parameters["HOToonSpecColor"].GetValueVector3();
            var threshold = effect.Parameters["HOToonSpecThresh"].GetValueSingle();
            var camera = effect.Parameters["HOCameraPosition"].GetValueVector3();
            var direction = effect.Parameters["advancedDirection"].GetValueTexture2D();
            var mapLayout = effect.Parameters["MapLayout"].GetValueVector2();
            var factor = effect.Parameters["WorldToLightFactor"].GetValueVector3();
            var offset = effect.Parameters["LightOffset"].GetValueVector2();
            var level = effect.Parameters["Level"].GetValueSingle();
            var rasterizer = device.RasterizerState;
            var indices = device.Indices;
            try
            {
                var view = effect.Parameters["View"].GetValueMatrix();
                var transform = LocalTransform(head, seconds) * world;
                var color = MoodColor(mood);
                var light = LightDirection(view);
                effect.Parameters["World"].SetValue(transform);
                effect.Parameters["MeshTex"].SetValue(Texture.Get(device));
                effect.Parameters["HOToonSpecColor"].SetValue(Vector3.Zero);
                effect.Parameters["HOToonSpecThresh"].SetValue(1f);
                effect.Parameters["HOCameraPosition"].SetValue(Matrix.Invert(view).Translation);
                // Use the existing PPX-aware head-object shader, without its toon glint.
                // Supply a finite direction even when advanced lighting is disabled;
                // normalizing a missing light sample can otherwise produce NaNs.
                effect.Parameters["advancedDirection"].SetValue(TextureGenerator.GetPxWhite(device));
                effect.Parameters["MapLayout"].SetValue(Vector2.One);
                effect.Parameters["WorldToLightFactor"].SetValue(Vector3.Zero);
                effect.Parameters["LightOffset"].SetValue(Vector2.Zero);
                effect.Parameters["Level"].SetValue(0f);
                effect.CurrentTechnique = effect.Techniques[6];
                device.RasterizerState = RasterizerState.CullCounterClockwise;
                // All 32 original triangles have identical normals at their three
                // vertices. Applying native lighting per facet preserves that shading
                // without adding a shader variant or altering shared avatar lighting.
                for (int i = 0; i < Indices.Length; i += 3)
                {
                    var normal = Vector3.TransformNormal(Vertices[Indices[i]].Normal, transform);
                    effect.Parameters["AmbientLight"].SetValue(new Vector4(FacetLight(color, normal, light), 1));
                    effect.CurrentTechnique.Passes[0].Apply();
                    device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                        Vertices, 0, Vertices.Length, Indices, i, 1);
                }
            }
            finally
            {
                effect.Parameters["World"].SetValue(world);
                effect.Parameters["AmbientLight"].SetValue(ambient);
                effect.Parameters["MeshTex"].SetValue(texture);
                effect.Parameters["HOToonSpecColor"].SetValue(specular);
                effect.Parameters["HOToonSpecThresh"].SetValue(threshold);
                effect.Parameters["HOCameraPosition"].SetValue(camera);
                effect.Parameters["advancedDirection"].SetValue(direction);
                effect.Parameters["MapLayout"].SetValue(mapLayout);
                effect.Parameters["WorldToLightFactor"].SetValue(factor);
                effect.Parameters["LightOffset"].SetValue(offset);
                effect.Parameters["Level"].SetValue(level);
                effect.CurrentTechnique = technique;
                device.RasterizerState = rasterizer;
                device.Indices = indices;
            }
        }
    }
}
