using System.Collections.Generic;
using System.Linq;
using System.IO;
using FSO.Common.Rendering.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using FSO.Content.Model;
using FSO.Vitaboy.Model;
using FSO.Files.RC;

namespace FSO.Vitaboy
{
    /// <summary>
    /// The base class for all avatars in the game.
    /// </summary>
    public abstract class Avatar : _3DComponent
    {
        // Debug logging for censorship system
        private static string _logPath;
        private static object _logLock = new object();
        public static bool CensorDebugEnabled = true;
        private static bool _logInitialized = false;

        // DEBUG: Set to true to force censorship on ALL body parts for testing
        // This will make ALL avatars show the mosaic blur - just for testing!
        public static bool FORCE_CENSOR_TEST = false;

        private static void InitLog()
        {
            if (_logInitialized) return;
            _logInitialized = true;

            // Try desktop first, fall back to current directory
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop))
            {
                desktop = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
            if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop))
            {
                desktop = ".";
            }
            _logPath = Path.Combine(desktop, "simitone_censor_debug.log");

            try
            {
                File.AppendAllText(_logPath, $"\n\n=== Simitone Censor Debug Log Started {DateTime.Now} ===\n");
                File.AppendAllText(_logPath, $"Log path: {_logPath}\n");
            }
            catch { }
        }

        public static void LogCensor(string message)
        {
            if (!CensorDebugEnabled) return;
            try
            {
                InitLog();
                lock (_logLock)
                {
                    File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
                }
            }
            catch { }
        }

        public List<AvatarBindingInstance> Bindings = new List<AvatarBindingInstance>();
        public static Effect Effect;
        public Skeleton Skeleton { get; set; }
        public Skeleton BaseSkeleton { get; set; }
        public List<Vector2> LightPositions;
        public DGRP3DMesh HeadObject;
        public float HeadObjectRotation;
        public float HeadObjectSpeedyVel;
        public bool HideHead;
        protected Matrix[] SkelBones;

        public static void setVitaboyEffect(Effect e) {
            Effect = e;
        }

        /// <summary>
        /// Creates a new Avatar instance.
        /// </summary>
        /// <param name="skel">A Skeleton instance.</param>
        public Avatar(Skeleton skel)
        {
            this.Skeleton = skel?.Clone();
            this.BaseSkeleton = skel?.Clone(); //keep a copy we can revert back to
        }

        public Avatar(Avatar old)
        {
            this.BaseSkeleton = old.BaseSkeleton.Clone();
            this.Skeleton = old.BaseSkeleton.Clone();
            for (int i = 0; i < old.Bindings.Count(); i++)
            {
                AvatarBindingInstance oldb = old.Bindings[i];
                Bindings.Add(new AvatarBindingInstance()
                {
                    Mesh = oldb.Mesh,
                    Texture = oldb.Texture,
                    CensorFlagBits = oldb.CensorFlagBits
                });
            }
            for (int i = 0; i < old.Accessories.Count(); i++)
            {
                this.Accessories.Add(old.Accessories.Keys.ElementAt(i), old.Accessories.Values.ElementAt(i));
            } //just shallow copy the binding and accessory list, as the data inside isn't going to change any time soon...
        }

        private Dictionary<Appearance, AvatarAppearanceInstance> Accessories = new Dictionary<Appearance, AvatarAppearanceInstance>();
        
        /// <summary>
        /// Adds an accessory to this avatar.
        /// </summary>
        /// <param name="apr">The Appearance instance of the accessory.</param>
        public void AddAccessory(Appearance apr)
        {
            if (Accessories.ContainsKey(apr))
            {
                return;
            }

            var add = AddAppearance(apr, null);
            Accessories.Add(apr, add);
        }

        /// <summary>
        /// Remove an accessory from this avatar.
        /// </summary>
        /// <param name="apr">The Appearance of the accessory to remove.</param>
        public void RemoveAccessory(Appearance apr)
        {
            if (apr == null) return;
            if (Accessories.ContainsKey(apr))
            {
                RemoveAppearance(Accessories[apr], true);
                Accessories.Remove(apr);
            }
        }

        private bool GPUMode;
        private GraphicsDevice GPUDevice;
        public void StoreOnGPU(GraphicsDevice device)
        {
            GPUMode = true;
            GPUDevice = device;
            lock (Bindings)
            {
                foreach (var binding in Bindings)
                {
                    binding.Mesh.StoreOnGPU(device);
                    binding.Texture.Get(device);
                }
            }
        }

        private string UniformName(string name)
        {
            return name.ToLowerInvariant().Replace("lgt", "").Replace("med", "").Replace("drk", "");
        }

        /// <summary>
        /// Adds an Appearance instance to this avatar.
        /// </summary>
        /// <param name="appearance">The Appearance instance to add.</param>
        /// <returns>An AvatarAppearanceInstance instance.</returns>
        protected AvatarAppearanceInstance AddAppearance(Appearance appearance, string texOverride)
        {
            var result = new AvatarAppearanceInstance();
            result.Original = appearance;
            result.Bindings = new List<AvatarBindingInstance>();

            int i = 0;
            int replaced = 0;
            var realBindings = appearance.Bindings.Select(bindingReference =>
                bindingReference.RealBinding ?? FSO.Content.Content.Get().AvatarBindings?.Get(bindingReference.TypeID, bindingReference.FileID)).ToList();

            if (Content.Content.Get().TS1)
            {
                foreach (var binding in realBindings)
                {
                    if (binding == null) { i++; continue; }
                    var mesh = Content.Content.Get().AvatarMeshes.Get(binding.MeshName);
                    if (texOverride != null &&
                            (UniformName(mesh.TextureName.ToLowerInvariant()).EndsWith(UniformName(texOverride.ToLowerInvariant()))
                            || mesh.TextureName.ToLowerInvariant() == "x"))
                    {
                        replaced = i;
                    }
                    i++;
                }

                if (texOverride != null)
                {
                    realBindings[replaced] = realBindings[replaced].TS1Copy();
                    realBindings[replaced].TextureName = texOverride;
                }
            }

            foreach (var binding in realBindings)
            {
                if (binding == null) { continue; }
                result.Bindings.Add(AddBinding(binding));
            }

            return result;
        }

        /// <summary>
        /// Removes an Appearance instance from this avatar.
        /// </summary>
        /// <param name="appearance">The Appearance instance to remove.</param>
        /// <param name="dispose">Should the appearance be disposed?</param>
        public void RemoveAppearance(AvatarAppearanceInstance appearance, bool dispose)
        {
            lock (Bindings)
            {
                if (appearance == null) return;
                foreach (var binding in appearance.Bindings)
                {
                    RemoveBinding(binding, dispose);
                }
            }
        }

        /// <summary>
        /// Adds a Binding instance to this avatar.
        /// </summary>
        /// <param name="binding">The Binding instance to add.</param>
        /// <returns>An AvatarBindingInstance instance.</returns>
        protected AvatarBindingInstance AddBinding(Binding binding)
        {
            var content = FSO.Content.Content.Get();
            var instance = new AvatarBindingInstance();
            if (binding.MeshName != null)
            {
                instance.Mesh = content.AvatarMeshes.Get(binding.MeshName);
                instance.Texture = content.AvatarTextures.Get(binding.TextureName ?? instance.Mesh.TextureName);
            }
            else
            {
                instance.Mesh = content.AvatarMeshes.Get(binding.MeshTypeID, binding.MeshFileID);
                instance.Texture = content.AvatarTextures.Get(binding.TextureTypeID, binding.TextureFileID);
            }

            /*if (instance.Mesh != null)
            {
                //We make a copy so we can modify it, most of the variables
                //are kept as pointers because we only change a few locals
                //per sim, the rest are global
                instance.Mesh = instance.Mesh.Clone();
            }*/

            instance.Mesh.Prepare(Skeleton.RootBone);
            instance.CensorFlagBits = binding.CensorFlagBits; // Preserve censorship flags for rendering

            // Debug log binding CensorFlagBits
            if (binding.CensorFlagBits != 0)
            {
                LogCensor($"AddBinding: mesh={binding.MeshName ?? "?"} CensorFlagBits={binding.CensorFlagBits} (0x{binding.CensorFlagBits:X})");
            }

            /*if (GPUMode)
            {
                instance.Mesh.StoreOnGPU(GPUDevice);
                instance.Texture.Get(GPUDevice);
            }*/

            lock (Bindings)
            {
                Bindings.Add(instance);
            }
            return instance;
        }

        /// <summary>
        /// Removes a Binding instance from this avatar.
        /// </summary>
        /// <param name="instance">The Binding instance to remove.</param>
        /// <param name="dispose">Should the binding be disposed?</param>
        protected void RemoveBinding(AvatarBindingInstance instance, bool dispose)
        {
            lock (Bindings)
            {
                Bindings.Remove(instance);
            }
        }

        /// <summary>
        /// Initializes this Avatar instance.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
        }

        /// <summary>
        /// When the skeleton changes (for example due to an animation) this
        /// method will recompute the meshes to adhere to the new skeleton positions.
        /// </summary>
        public void ReloadSkeleton()
        {
            if (Skeleton == null) return;
            Skeleton.ComputeBonePositions(Skeleton.RootBone, Matrix.Identity);
            SkelBones = new Matrix[Skeleton.Bones.Length];
            for (int i = 0; i < Skeleton.Bones.Length; i++)
            {
                SkelBones[i] = Skeleton.Bones[i].AbsoluteMatrix;
            }
        }

        /// <summary>
        /// Updates this Avatar instance.
        /// </summary>
        /// <param name="state">An UpdateState instance.</param>
        public override void Update(FSO.Common.Rendering.Framework.Model.UpdateState state)
        {
        }

        public static int DefaultTechnique = 0;
        public Vector4 AmbientLight = Vector4.One;

        // Censorship pixelation effect resources
        private static SpriteBatch _censorSpriteBatch;
        private static Texture2D _censorMosaicTexture;
        private static int _mosaicSize = 8; // Number of mosaic blocks per side

        /// <summary>
        /// Draws the meshes making up this Avatar instance.
        /// </summary>
        /// <param name="device">A GraphicsDevice instance.</param>
        public override void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device)
        {
            Effect.CurrentTechnique = Effect.Techniques[DefaultTechnique];
            Effect.Parameters["View"].SetValue(View);
            Effect.Parameters["Projection"].SetValue(Projection);
            Effect.Parameters["World"].SetValue(World);
            Effect.Parameters["AmbientLight"].SetValue(AmbientLight);

            DrawGeometry(device, Effect);
        }

        public void DrawHeadOnly(Microsoft.Xna.Framework.Graphics.GraphicsDevice device, Effect effect, Matrix headMatrix)
        {
            //Effect.CurrentTechnique = Effect.Techniques[0];
            if (SkelBones == null) ReloadSkeleton();
            var matrixCopy = new Matrix[SkelBones.Length];
            // Locate the head bone and inject the custom matrix.

            for (int i = 0; i < Skeleton.Bones.Length; i++)
            {
                var bone = Skeleton.Bones[i];

                if (bone.Name == "HEAD") matrixCopy[i] = headMatrix;
            }

            effect.Parameters["SkelBindings"].SetValue(matrixCopy);

            lock (Bindings)
            {
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    foreach (var binding in Bindings)
                    {
                        if (binding.Texture != null)
                        {
                            var tex = binding.Texture.Get(device);
                            effect.Parameters["MeshTex"].SetValue(tex);
                        }
                        else
                        {
                            effect.Parameters["MeshTex"].SetValue((Texture2D)null);
                        }
                        pass.Apply();
                        binding.Mesh.Draw(device);
                    }
                }
            }
        }

        private static int _lastLoggedCensorFlags = -1;
        private static DateTime _lastCensorLog = DateTime.MinValue;

        public void DrawGeometry(Microsoft.Xna.Framework.Graphics.GraphicsDevice device, Effect effect, int censorshipFlags = 0)
        {
            // DEBUG: Force censorship test mode - censor ALL body parts
            if (FORCE_CENSOR_TEST)
            {
                censorshipFlags = 0xFF; // All flags on - Pelvis, Spine, Head, Hands, Feet, FullBody
            }

            // Debug log censorship flags (throttled to avoid spam)
            if (censorshipFlags != 0 && (censorshipFlags != _lastLoggedCensorFlags || (DateTime.Now - _lastCensorLog).TotalSeconds > 2))
            {
                _lastLoggedCensorFlags = censorshipFlags;
                _lastCensorLog = DateTime.Now;
                LogCensor($"DrawGeometry: censorshipFlags={censorshipFlags} (0x{censorshipFlags:X}), Bindings.Count={Bindings.Count}");
                foreach (var b in Bindings)
                {
                    LogCensor($"  - Binding CensorFlagBits={b.CensorFlagBits} (0x{b.CensorFlagBits:X}), match={(b.CensorFlagBits & censorshipFlags) != 0}");
                }
            }

            if (SkelBones == null) ReloadSkeleton();
            effect.Parameters["SkelBindings"].SetValue(SkelBones);

            // Check if censorship is active (either from game or forced for testing)
            bool showCensorBlur = censorshipFlags != 0 || FORCE_CENSOR_TEST;

            lock (Bindings)
            {
                // Draw all meshes normally
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    foreach (var binding in Bindings)
                    {
                        if (HideHead && binding.Mesh.BoneBindings.Any(meshBind => meshBind.BoneName == "HEAD"))
                        {
                            continue;
                        }

                        if (binding.Texture != null)
                        {
                            var tex = binding.Texture.Get(device);
                            effect.Parameters["MeshTex"].SetValue(tex);
                        }
                        else
                        {
                            effect.Parameters["MeshTex"].SetValue((Texture2D)null);
                        }
                        pass.Apply();
                        binding.Mesh.Draw(device);
                    }
                }

                // If censorship is active, draw mosaic overlay at pelvis position
                if (showCensorBlur)
                {
                    DrawCensoredMeshesPixelated(device, effect, censorshipFlags);
                }
            }

            //skip drawing shadows if we're drawing id
            if (LightPositions == null || effect.CurrentTechnique == effect.Techniques[1]) return;

            if (ShadBuf == null)
            {
                var shadVerts = new ShadowVertex[]
                {
                new ShadowVertex(new Vector3(-1, 0, -1), 25),
                new ShadowVertex(new Vector3(-1, 0, 1), 25),
                new ShadowVertex(new Vector3(1, 0, 1), 25),
                new ShadowVertex(new Vector3(1, 0, -1), 25),

                new ShadowVertex(new Vector3(-1, 0, -1), 19),
                new ShadowVertex(new Vector3(-1, 0, 1), 19),
                new ShadowVertex(new Vector3(1, 0, 1), 19),
                new ShadowVertex(new Vector3(1, 0, -1), 19)
                };
                for (int i = 0; i < shadVerts.Length; i++) shadVerts[i].Position *= 1f;
                int[] shadInd = new int[] { 2, 1, 0, 2, 0, 3, 6, 5, 4, 6, 4, 7 };

                ShadBuf = new VertexBuffer(device, typeof(ShadowVertex), shadVerts.Length, BufferUsage.None);
                ShadBuf.SetData(shadVerts);
                ShadIBuf = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, shadInd.Length, BufferUsage.None);
                ShadIBuf.SetData(shadInd);
            }

            foreach (var light in LightPositions)
            {
                //effect.Parameters["FloorHeight"].SetValue((float)(Math.Floor(Position.Y/2.95)*2.95 + 0.05));
                effect.Parameters["LightPosition"].SetValue(light);
                var oldTech = effect.CurrentTechnique;
                effect.CurrentTechnique = effect.Techniques[4];
                effect.CurrentTechnique.Passes[0].Apply();
                device.DepthStencilState = DepthStencilState.DepthRead;
                device.SetVertexBuffer(ShadBuf);
                device.Indices = ShadIBuf;
                device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 4);
                effect.CurrentTechnique = oldTech;
                device.DepthStencilState = DepthStencilState.Default;
            }

            DrawHeadObject(device, effect);
        }

        /// <summary>
        /// Creates a procedural mosaic texture for the censorship blur effect.
        /// Uses skin-tone colors in a randomized pattern like the original TS1.
        /// </summary>
        private static Texture2D CreateMosaicTexture(GraphicsDevice device)
        {
            int size = _mosaicSize * 8; // 64x64 texture with 8x8 blocks
            var texture = new Texture2D(device, size, size);
            var colors = new Color[size * size];

            // Skin tone palette (various flesh colors)
            var palette = new Color[]
            {
                new Color(255, 224, 189),  // Light peach
                new Color(255, 205, 148),  // Peach
                new Color(234, 192, 134),  // Tan
                new Color(255, 173, 96),   // Light orange
                new Color(224, 172, 105),  // Medium tan
                new Color(241, 194, 125),  // Golden
                new Color(255, 219, 172),  // Pale peach
                new Color(209, 163, 102),  // Darker tan
            };

            var rand = new Random(42); // Fixed seed for consistent look
            int blockSize = size / _mosaicSize;

            for (int by = 0; by < _mosaicSize; by++)
            {
                for (int bx = 0; bx < _mosaicSize; bx++)
                {
                    // Pick a random color from palette for this block
                    var blockColor = palette[rand.Next(palette.Length)];

                    // Fill the block
                    for (int py = 0; py < blockSize; py++)
                    {
                        for (int px = 0; px < blockSize; px++)
                        {
                            int x = bx * blockSize + px;
                            int y = by * blockSize + py;
                            colors[y * size + x] = blockColor;
                        }
                    }
                }
            }

            texture.SetData(colors);
            LogCensor($"Created mosaic texture: {size}x{size} with {_mosaicSize}x{_mosaicSize} blocks");
            return texture;
        }

        /// <summary>
        /// Draws censored meshes normally, then overlays a mosaic sprite at the pelvis position.
        /// This matches the original TS1 behavior - body is drawn but covered by pixelated blur.
        /// </summary>
        private void DrawCensoredMeshesPixelated(GraphicsDevice device, Effect effect, int censorshipFlags)
        {
            // Initialize sprite batch if needed
            if (_censorSpriteBatch == null || _censorSpriteBatch.IsDisposed ||
                _censorSpriteBatch.GraphicsDevice != device)
            {
                _censorSpriteBatch?.Dispose();
                _censorSpriteBatch = new SpriteBatch(device);
            }

            // Create mosaic texture if needed
            if (_censorMosaicTexture == null || _censorMosaicTexture.IsDisposed ||
                _censorMosaicTexture.GraphicsDevice != device)
            {
                _censorMosaicTexture?.Dispose();
                _censorMosaicTexture = CreateMosaicTexture(device);
            }

            // Get the View and Projection matrices from the effect
            var viewMatrix = effect.Parameters["View"].GetValueMatrix();
            var projMatrix = effect.Parameters["Projection"].GetValueMatrix();
            var worldMatrix = effect.Parameters["World"].GetValueMatrix();

            // Find the pelvis bone position for centering the censor blur
            Vector3 pelvisWorld = Vector3.Zero;
            if (Skeleton != null)
            {
                var pelvisBone = Skeleton.GetBone("PELVIS");
                if (pelvisBone != null)
                {
                    pelvisWorld = Vector3.Transform(pelvisBone.AbsolutePosition, worldMatrix);
                }
            }

            // Project pelvis to screen space
            var viewport = device.Viewport;
            var screenPos = viewport.Project(pelvisWorld, projMatrix, viewMatrix, Matrix.Identity);

            // Don't draw if behind camera
            if (screenPos.Z < 0 || screenPos.Z > 1) return;

            // Save current state
            var originalBlendState = device.BlendState;
            var originalDepthState = device.DepthStencilState;
            var originalRasterState = device.RasterizerState;

            // Calculate screen rectangle for the censor blur
            // Size it based on viewport (roughly 60-100 pixels depending on zoom)
            int censorSize = Math.Max(50, viewport.Height / 10);
            int halfSize = censorSize / 2;

            var destRect = new Rectangle(
                (int)screenPos.X - halfSize,
                (int)screenPos.Y - halfSize - (censorSize / 4), // Offset up slightly to cover pelvis area
                censorSize,
                censorSize
            );

            // Draw the mosaic texture at the pelvis position
            _censorSpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            _censorSpriteBatch.Draw(_censorMosaicTexture, destRect, Color.White);

            _censorSpriteBatch.End();

            // Restore graphics state
            device.BlendState = originalBlendState;
            device.DepthStencilState = originalDepthState;
            device.RasterizerState = originalRasterState;
        }

        public void DrawHeadObject(GraphicsDevice device, Effect effect)
        {
            //0: high reflective
            //1: light reflective
            //2: outline
            if (effect.Techniques[1] == effect.CurrentTechnique) return;
            var headObj = HeadObject;
            if (headObj == null) return;
            var oldTech = effect.CurrentTechnique;
            effect.CurrentTechnique = effect.Techniques[6];
            device.RasterizerState = RasterizerState.CullClockwise;

            var trans = Matrix.Invert(effect.Parameters["View"].GetValueMatrix()).Translation;
            effect.Parameters["HOCameraPosition"].SetValue(trans);
            effect.Parameters["World"].SetValue(Matrix.CreateScale(1.33f) * Matrix.CreateRotationY(HeadObjectRotation) * Matrix.CreateTranslation(Skeleton.GetBone("HEAD").AbsoluteMatrix.Translation + new Vector3(0, 3.25f, 0)) * effect.Parameters["World"].GetValueMatrix());

            for (int i=0; i<headObj.Geoms.Count; i++)
            {
                var multi = 0.5f / (headObj.Geoms.Count - 1);
                var geom = headObj.Geoms[i];
                foreach (var item in geom)
                {
                    effect.Parameters["MeshTex"].SetValue(item.Key);
                    effect.Parameters["HOToonSpecThresh"].SetValue(0.5f);
                    effect.Parameters["HOToonSpecColor"].SetValue(new Vector3(multi * ((headObj.Geoms.Count-1) - i)));

                    effect.CurrentTechnique.Passes[0].Apply();
                    //if (i != 1) device.RasterizerState = RasterizerState.CullClockwise;
                    device.SetVertexBuffer(item.Value.Verts);
                    device.Indices = item.Value.Indices;
                    device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, item.Value.PrimCount);
                    //if (i != 1) device.RasterizerState = RasterizerState.CullCounterClockwise;
                }
            }
            device.RasterizerState = RasterizerState.CullCounterClockwise;
            effect.CurrentTechnique = oldTech;
        }

        // TODO: memory leak
        private static VertexBuffer ShadBuf;
        private static IndexBuffer ShadIBuf;

        public override void DeviceReset(GraphicsDevice Device)
        {
        }
    }

    /// <summary>
    /// Holds bindings for an avatar.
    /// </summary>
    public class AvatarAppearanceInstance
    {
        public Appearance Original;
        public List<AvatarBindingInstance> Bindings;
    }

    /// <summary>
    /// Holds a mesh and texture for an avatar.
    /// </summary>
    public class AvatarBindingInstance
    {
        public Mesh Mesh;
        public ITextureRef Texture;
        public int CensorFlagBits; // Which body parts this mesh covers (for censorship blur)
    }
}
