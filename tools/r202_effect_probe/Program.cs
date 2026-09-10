// R202 effect probe: load the shipped OGL Vitaboy.xnb through the exact
// MonoGame 3.8.4 DesktopGL runtime the game uses, dissect its runtime state
// (constant buffers, uniform locations, attribute tables), and attempt a
// synthetic avatar draw through every technique. Isolates MonoGame upload/
// binding vs engine state as the sim-invisibility mechanism.
using System;
using System.Reflection;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace r202_effect_probe
{
    public struct FakeVitaboyVertex : IVertexType
    {
        public Vector3 Position;
        public Vector2 TextureCoordinate;
        public Vector3 BvPosition;
        public Vector3 Parameters;
        public Vector3 Normal;
        public Vector3 BvNormal;

        public FakeVitaboyVertex(Vector3 p, Vector2 t, Vector3 bv, Vector3 par, Vector3 n, Vector3 bvn)
        { Position = p; TextureCoordinate = t; BvPosition = bv; Parameters = par; Normal = n; BvNormal = bvn; }

        public static readonly VertexDeclaration Decl = new VertexDeclaration
        (
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(sizeof(float) * 3, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(sizeof(float) * 5, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 1),
            new VertexElement(sizeof(float) * 8, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 2),
            new VertexElement(sizeof(float) * 11, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(sizeof(float) * 14, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 3)
        );

        VertexDeclaration IVertexType.VertexDeclaration => Decl;
    }

    public static class Program
    {
        static string F(Type t, object o, string field)
        {
            var fi = t.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi == null) return "<no " + field + ">";
            var v = fi.GetValue(o);
            return v == null ? "null" : v.ToString();
        }
        static Color new128() { return new Color(128,128,128); }

        static object FV(Type t, object o, string field)
        {
            return t.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(o);
        }

        class ProbeGame : Game
        {
            public string Path;
            private bool _done;
            public ProbeGame() { new GraphicsDeviceManager(this); IsFixedTimeStep = false; }
            protected override void Draw(GameTime gameTime)
            {
                if (_done) return;
                _done = true;
                try { RunProbe(this, Path); }
                catch (Exception ex) { Console.WriteLine("PROBE EX: " + ex); }
                Exit();
            }
        }

        public static void Main(string[] args)
        {
            var path = args.Length > 0 ? args[0] :
                "../../../../../FreeSO/TSOClient/tso.content/Content/OGL/Effects/Vitaboy.xnb";
            using var game = new ProbeGame { Path = path };
            game.Run();
        }

        static void RunProbe(Game game, string path)
        {
            var gd = game.GraphicsDevice;
            Console.WriteLine("GLVersion=" + gd.GraphicsProfile + " adapter=" + gd.Adapter.Description);
            var all = System.IO.File.ReadAllBytes(path);
            var mg = System.Array.IndexOf(all, (byte)'M');
            int m = -1;
            for (int k = 0; k + 4 <= all.Length; k++)
                if (all[k] == (byte)'M' && all[k+1] == (byte)'G' && all[k+2] == (byte)'F' && all[k+3] == (byte)'X') { m = k; break; }
            var bytes = new byte[all.Length - m];
            System.Array.Copy(all, m, bytes, 0, bytes.Length);
            Console.WriteLine("xnb=" + all.Length + "B  mgfx@" + m + " -> " + bytes.Length + "B");
            var eff = new Effect(gd, bytes);
            Console.WriteLine("effect loaded OK, techniques=" + eff.Techniques.Count);
            foreach (var t in new[] { typeof(Effect).Assembly.GetType("Microsoft.Xna.Framework.Graphics.Effect"), typeof(Effect).Assembly.GetType("Microsoft.Xna.Framework.Graphics.ConstantBuffer"), typeof(Effect).Assembly.GetType("Microsoft.Xna.Framework.Graphics.Shader") })
            {
                if (t == null) continue;
                Console.WriteLine("TYPE " + t.FullName + " :");
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                    Console.WriteLine("   " + f.FieldType.Name + " " + f.Name);
            }
            for (int t = 0; t < eff.Techniques.Count; t++)
                Console.WriteLine($"  tech[{t}] {eff.Techniques[t].Name} passes={eff.Techniques[t].Passes.Count}");
            for (int p = 0; p < eff.Parameters.Count; p++)
            {
                var par = eff.Parameters[p];
                Console.WriteLine($"  param[{p}] {par.Name} class={par.ParameterClass} type={par.ParameterType} rows={par.RowCount} cols={par.ColumnCount} elems={par.Elements.Count} structMembers={par.StructureMembers.Count}");
            }

            // constant buffers via reflection
            var effType = typeof(Effect);
            var cbs = FV(effType, eff, "<ConstantBuffers>k__BackingField") as System.Collections.IEnumerable;
            if (cbs != null)
            {
                foreach (var cb in cbs)
                {
                    var ct = cb.GetType();
                    var buf = FV(ct, cb, "_buffer") as byte[];
                    var pars = FV(ct, cb, "_parameters") as int[];
                    var offs = FV(ct, cb, "_offsets") as int[];
                    var desc = pars != null && offs != null
                        ? string.Join(",", System.Linq.Enumerable.Range(0, pars.Length).Select(i => pars[i] + "@" + offs[i]))
                        : "?";
                    Console.WriteLine($"  CB name={F(ct, cb, "_name")} bufLen={buf?.Length ?? -1} params={desc}");
                }
            }

            // shaders + attribute tables via reflection (Effect._shaders)
            var shaders = FV(effType, eff, "_shaders") as Array;
            if (shaders != null)
            {
                for (int s = 0; s < shaders.Length; s++)
                {
                    var sh = shaders.GetValue(s);
                    var st = sh.GetType();
                    var attrs = FV(st, sh, "<Attributes>k__BackingField") as Array;
                    var glsl = FV(st, sh, "_glslCode") as string;
                    Console.WriteLine($"  shader[{s}] stage={F(st, sh, "<Stage>k__BackingField")} glslLen={glsl?.Length ?? -1} attrs={attrs?.Length ?? -1}");
                    if (attrs != null)
                        foreach (var a in attrs)
                        {
                            var at = a.GetType();
                            Console.WriteLine($"     attr usage={F(at, a, "usage")} index={F(at, a, "index")} name={F(at, a, "name")} location={F(at, a, "location")}");
                        }
                }
            }

            // synthetic draw through every technique
            var verts = new FakeVitaboyVertex[]
            {
                new FakeVitaboyVertex(new Vector3(-1,-1,0), new Vector2(0,0), new Vector3(0,0,0), new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(0,1,0)),
                new FakeVitaboyVertex(new Vector3( 3,-1,0), new Vector2(1,0), new Vector3(0,0,0), new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(0,1,0)),
                new FakeVitaboyVertex(new Vector3(-1, 3,0), new Vector2(0,1), new Vector3(0,0,0), new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(0,1,0)),
            };
            var vb = new VertexBuffer(gd, typeof(FakeVitaboyVertex), 3, BufferUsage.None);
            vb.SetData(verts);
            var ib = new IndexBuffer(gd, IndexElementSize.SixteenBits, 3, BufferUsage.None);
            ib.SetData(new short[] { 0, 1, 2 });

            var white = new Texture2D(gd, 1, 1);
            white.SetData(new Color[] { Color.White });
            var bones = new Matrix[50];
            for (int i = 0; i < 50; i++) bones[i] = Matrix.Identity;

            var rt = new RenderTarget2D(gd, 256, 256);
            var prev = gd.GetRenderTargets();
            gd.SetRenderTarget(rt);

            foreach (var tech in eff.Techniques)
            {
                eff.CurrentTechnique = tech;
                eff.Parameters["World"].SetValue(Matrix.Identity);
                eff.Parameters["View"].SetValue(Matrix.Identity);
                eff.Parameters["Projection"].SetValue(Matrix.Identity);
                eff.Parameters["SkelBindings"].SetValue(bones);
                eff.Parameters["AmbientLight"].SetValue(new Vector4(1, 1, 1, 1));
                eff.Parameters["MeshTex"].SetValue(white);
                var sd = eff.Parameters["SoftwareDepth"]; if (sd != null) sd.SetValue(false);
                var dm = eff.Parameters["depthOutMode"]; if (dm != null) dm.SetValue(false);

                gd.Clear(new Color(128, 128, 128));
                gd.SetVertexBuffer(vb);
                gd.Indices = ib;
                foreach (var pass in tech.Passes)
                {
                    pass.Apply();
                    gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 1);
                }
                var px = new Color[256 * 256];
                rt.GetData(px);
                int colored = 0;
                foreach (var c in px) if (c != new Color(128, 128, 128)) colored++;
                Console.WriteLine($"  DRAW {tech.Name} [CCW idx, defaultCull]: colored={colored} {(colored > 1000 ? "RENDERED" : "EMPTY")}");
                // variant: cull none
                gd.RasterizerState = RasterizerState.CullNone;
                gd.Clear(new Color(128, 128, 128));
                foreach (var pass in tech.Passes)
                {
                    pass.Apply();
                    gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 1);
                }
                rt.GetData(px);
                colored = 0;
                foreach (var c in px) if (c != new Color(128, 128, 128)) colored++;
                Console.WriteLine($"  DRAW {tech.Name} [CCW idx, CullNone]: colored={colored} {(colored > 1000 ? "RENDERED" : "EMPTY")}");
                // variant: CW winding (flip index order) with default cull
                ib.SetData(new short[] { 0, 2, 1 });
                gd.RasterizerState = RasterizerState.CullCounterClockwise;
                gd.Clear(new Color(128, 128, 128));
                foreach (var pass in tech.Passes)
                {
                    pass.Apply();
                    gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 1);
                }
                rt.GetData(px);
                colored = 0;
                foreach (var c in px) if (c != new128()) colored++;
                Console.WriteLine($"  DRAW {tech.Name} [CW idx, defaultCull]: colored={colored} {(colored > 1000 ? "RENDERED" : "EMPTY")}");
                ib.SetData(new short[] { 0, 1, 2 });
            }

            // post-draw constant buffer state (locations resolved?)
            if (cbs != null)
                foreach (var cb in cbs)
                {
                    var ct = cb.GetType();
                    var buf = FV(ct, cb, "_buffer") as byte[];
                    var nonzero = 0; var tail = "";
                    if (buf != null)
                    {
                        for (int i = 0; i + 3 < buf.Length; i += 4)
                            if (System.BitConverter.ToSingle(buf, i) != 0) nonzero++;
                        var f0 = System.BitConverter.ToSingle(buf, 0);
                        var f200 = System.BitConverter.ToSingle(buf, Math.Min(3200, buf.Length - 4));
                        tail = " u[0].x=" + f0 + " u[200].x=" + f200;
                    }
                    Console.WriteLine($"  POST CB name={F(ct, cb, "_name")} bufLen={buf?.Length ?? -1} nonzeroVec4s={nonzero}{tail} " +
                        $"_location={F(ct, cb, "_location")} _shaderProgram={(FV(ct, cb, "_shaderProgram") == null ? "null" : "SET")} _dirty={F(ct, cb, "_dirty")}");
                }
            // CONTROL 1: BasicEffect through same RT/clear/count machinery
            {
                var be = new BasicEffect(gd) { World = Matrix.Identity, View = Matrix.Identity, Projection = Matrix.Identity, VertexColorEnabled = true, TextureEnabled = false };
                var cv = new[] { new VertexPositionColor(new Vector3(-1,-1,0), Color.White), new VertexPositionColor(new Vector3(3,-1,0), Color.White), new VertexPositionColor(new Vector3(-1,3,0), Color.White) };
                var cvb = new VertexBuffer(gd, typeof(VertexPositionColor), 3, BufferUsage.None); cvb.SetData(cv);
                gd.Clear(new Color(128,128,128));
                gd.SetVertexBuffer(cvb); gd.Indices = null;
                foreach (var pass in be.CurrentTechnique.Passes) { pass.Apply(); gd.DrawPrimitives(PrimitiveType.TriangleList, 0, 1); }
                var px = new Color[256*256]; rt.GetData(px);
                int colored = 0; foreach (var c in px) if (c != new Color(128,128,128)) colored++;
                Console.WriteLine($"  CONTROL BasicEffect [CCW, defaultCull]: colored={colored} {(colored > 1000 ? "RENDERED" : "EMPTY")}");
                gd.RasterizerState = RasterizerState.CullNone;
                gd.Clear(new Color(128,128,128));
                foreach (var pass in be.CurrentTechnique.Passes) { pass.Apply(); gd.DrawPrimitives(PrimitiveType.TriangleList, 0, 1); }
                rt.GetData(px);
                colored = 0; foreach (var c in px) if (c != new Color(128,128,128)) colored++;
                Console.WriteLine($"  CONTROL BasicEffect [CCW, CullNone]: colored={colored} {(colored > 1000 ? "RENDERED" : "EMPTY")}");
            }
            // CONTROL 2: RCObject.xnb effect with its own vertex layout
            try
            {
                var rcPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path) ?? ".", "RCObject.xnb");
                var rcAll = System.IO.File.ReadAllBytes(rcPath);
                int rm = -1; for (int k = 0; k + 4 <= rcAll.Length; k++) if (rcAll[k]==(byte)'M'&&rcAll[k+1]==(byte)'G'&&rcAll[k+2]==(byte)'F'&&rcAll[k+3]==(byte)'X') { rm=k; break; }
                var rcBytes = new byte[rcAll.Length-rm]; System.Array.Copy(rcAll, rm, rcBytes, 0, rcBytes.Length);
                var rc = new Effect(gd, rcBytes);
                Console.WriteLine("  CONTROL RCObject loaded, techniques=" + rc.Techniques.Count);
                foreach (var tch in rc.Techniques)
                {
                    rc.CurrentTechnique = tch;
                    var pw = rc.Parameters["World"]; if (pw != null) pw.SetValue(Matrix.Identity);
                    var pvp = rc.Parameters["ViewProjection"]; if (pvp != null) pvp.SetValue(Matrix.Identity);
                    var pal = rc.Parameters["AmbientLight"]; if (pal != null) pal.SetValue(new Vector4(1,1,1,1));
                    var pu = rc.Parameters["UVScale"]; if (pu != null) pu.SetValue(Vector2.One);
                    var pt = rc.Parameters["MeshTex"]; if (pt != null) pt.SetValue(white);
                    gd.Clear(new Color(128,128,128));
                    var rv = new[] { new RcVertex(new Vector3(-1,-1,0), new Vector2(0,0), new Vector3(0,1,0)),
                                     new RcVertex(new Vector3(3,-1,0), new Vector2(1,0), new Vector3(0,1,0)),
                                     new RcVertex(new Vector3(-1,3,0), new Vector2(0,1), new Vector3(0,1,0)) };
                    var rvb = new VertexBuffer(gd, typeof(RcVertex), 3, BufferUsage.None); rvb.SetData(rv);
                    gd.SetVertexBuffer(rvb); gd.Indices = null;
                    foreach (var pass in tch.Passes) { pass.Apply(); gd.DrawPrimitives(PrimitiveType.TriangleList, 0, 1); }
                    var px = new Color[256*256]; rt.GetData(px);
                    int colored = 0; foreach (var c in px) if (c != new Color(128,128,128)) colored++;
                    Console.WriteLine($"  CONTROL RCObject {tch.Name}: colored={colored} {(colored > 1000 ? "RENDERED" : "EMPTY")}");
                }
            }
            catch (Exception ex) { Console.WriteLine("  CONTROL RCObject EX: " + ex.Message); }
            gd.SetRenderTargets(prev);
        }

        public struct RcVertex : IVertexType
        {
            public Vector3 Position; public Vector2 Tex; public Vector3 Normal;
            public RcVertex(Vector3 p, Vector2 t, Vector3 n) { Position = p; Tex = t; Normal = n; }
            public static readonly VertexDeclaration Decl = new VertexDeclaration
            (
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(sizeof(float)*3, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
                new VertexElement(sizeof(float)*5, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 1)
            );
            VertexDeclaration IVertexType.VertexDeclaration => Decl;
        }
    }
}
