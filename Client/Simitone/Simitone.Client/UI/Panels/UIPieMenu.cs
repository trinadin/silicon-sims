using System;
using System.Collections.Generic;
using System.Linq;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Model;
using FSO.Client.UI.Controls;
using Microsoft.Xna.Framework;
using FSO.SimAntics;
using FSO.HIT;
using FSO.Vitaboy;
using FSO.Common.Rendering.Framework.Camera;
using FSO.Common.Rendering.Framework;
using FSO.Common.Utils;
using FSO.SimAntics.NetPlay.Model.Commands;
using FSO.Common;
using FSO.Client;

namespace Simitone.Client.UI.Panels
{
    public class UIPieMenu : UIContainer
    {
        // UI-11: the original per-side frame inset, decoded from cTSPieMenu::
        // Layout (0x5290c0) and DrawLabelFrame (0x528d10) — every bubble rect
        // is the measured label bounding box + 3px on each side
        // (tools/iff-dump/pie-label-paint-law.md). Supersedes the R142-era
        // 8px safety margin; the glyph-overhang worry it covered belongs to
        // the old mobile MSDF metrics, while the OriginalVectorFont advances
        // are ink-exact (r140).
        public const int OriginalButtonMargin = 3;

        // R142: exposed for the autotest gate (uidlgchrome) — the engine's exact
        // slice-count band law from cTSPieMenu::Layout @ 0x5292b8.
        public static int SliceCount(int count)
        {
            return (count == 0) ? 1 : (count <= 2) ? count + 2 : (count <= 4) ? count + 4 : count + 8;
        }

        public UIPieMenuItem m_PieTree;
        public List<UIButton> m_PieButtons;
        public UIPieMenuItem m_CurrentItem;
        public VMEntity m_Obj;
        public VMEntity m_Caller;
        public UILotControl m_Parent;
        public UIImage m_Bg;

        private Vector2 currentTarget;
        private Vector2 curRot;
        private float lerpSpeed;

        private _3DTargetScene HeadScene;
        private BasicCamera HeadCamera;
        private double m_BgGrow;
        private float TrueScale;

        private bool ShiftDown; //shift activates IDE

        //This is a standard AdultVitaboyModel instance. Since nothing is needed but the head for pie menus,
        //the other parts of the body will be stripped from it (see constructor).
        private SimAvatar m_Head;

        private TextStyle ButtonStyle;
        private TextStyle HighlightStyle;

        // UI-11: decoded pie label chrome (tools/iff-dump/pie-label-paint-law.md);
        // color roles corrected in UI-15 against the raw TSPaint words.
        public static readonly Color PieFrameTint = new Color(187, 187, 187);
        public static readonly Color UntrackedLabelColor = new Color(185, 185, 208); // +208
        public static readonly Color HoverLabelColor = new Color(0, 255, 255);       // +212
        public UIPieBubble PressedBubble;

        public UIPieMenu(List<VMPieMenuInteraction> pie, VMEntity obj, VMEntity caller, UILotControl parent)
        {
            if (FSOEnvironment.UIZoomFactor > 1.33f) ScaleX = ScaleY = FSOEnvironment.UIZoomFactor * 0.75f;
            TrueScale = ScaleX * FSOEnvironment.DPIScaleFactor;
            m_PieButtons = new List<UIButton>();
            this.m_Obj = obj;
            this.m_Caller = caller;
            this.m_Parent = parent;
            // UI-11/15: the decoded TSPaint label state law (raw-verified
            // 0x528a74-0x528aa0; word 0x4082001c = BNE): untracked bubbles
            // draw the muted lavender RGB(185,185,208) (+208) regardless of
            // press state; the tracked (hovered) bubble draws the highlight
            // cyan RGB(0,255,255) (+212) and flips to WHITE (+216) while a
            // press is held on it. RGB(187,187,187) is the engine's +100
            // FRAME color (DrawLabelFrame tint), not the text color.
            this.ButtonStyle = new TextStyle
            {
                Font = GameFacade.MainFont,
                VFont = GameFacade.VectorFont,
                Size = 12,
                Color = UntrackedLabelColor,
                HighlightedColor = HoverLabelColor,
                SelectedColor = Color.White,
                CursorColor = Color.White
            };

            HighlightStyle = ButtonStyle.Clone();
            // ColorMod items keep the dimmed-interaction lavender as their
            // base color: data-driven, and TSPaint has no per-item color
            // branch (its item flag +8 hides the row entirely).
            HighlightStyle.Color = UntrackedLabelColor;

            lerpSpeed = 0.125f * (60.0f / FSOEnvironment.RefreshRate);
            // R142: the ORIGINAL interaction-pie background — SMCtrlMgrRes-adjacent
            // RT 825 cpanel\ViewMenuBackground.bmp (17x17 disc tile), max radius 90
            // per cDDDSimsView::Init. The old TextureGenerator radial-blue was TSO's.
            var pieBgTex = Simitone.Client.UI.Model.UIOriginal.EnsureResolved("cpanel\\ViewMenuBackground.bmp")?.Get(GameFacade.GraphicsDevice)
                ?? TextureGenerator.GetPieBG(GameFacade.GraphicsDevice);
            m_Bg = new UIImage(pieBgTex);
            m_Bg.SetSize(2, 2); //is scaled up later
            m_Bg.Position = new Vector2(-1, -1);
            this.AddAt(0, m_Bg);

            m_PieTree = new UIPieMenuItem()
            {
                Category = true
            };

            for (int i = 0; i < pie.Count; i++)
            {
                string[] depth = (pie[i].Name == null) ? new string[] { "???" } : pie[i].Name.Split('/');

                var category = m_PieTree; //set category to root
                for (int j = 0; j < depth.Length - 1; j++) //iterate through categories
                {
                    if (category.ChildrenByName.ContainsKey(depth[j]))
                    {
                        category = category.ChildrenByName[depth[j]];
                    }
                    else
                    {
                        var newCat = new UIPieMenuItem()
                        {
                            Category = true,
                            Name = depth[j],
                            Parent = category
                        };
                        category.Children.Add(newCat);
                        category.ChildrenByName[depth[j]] = newCat;
                        category = newCat;
                    }
                }
                //we are in the category, put the interaction in here;

                var name = depth[depth.Length - 1];
                var semiInd = name.LastIndexOf(';');
                int colorMod = 0;
                if (semiInd > -1)
                {
                    int.TryParse(name.Substring(semiInd + 1), out colorMod);
                    name = name.Substring(0, semiInd);
                }

                var item = new UIPieMenuItem()
                {
                    Category = false,
                    Name = name,
                    ColorMod = colorMod,
                    ID = pie[i].ID,
                    Param0 = pie[i].Param0,
                    Global = pie[i].Global
                };
                category.Children.Add(item);
                category.ChildrenByName[item.Name] = item;
            }

            m_CurrentItem = m_PieTree;
            m_PieButtons = new List<UIButton>();
            RenderMenu();

            VMAvatar Avatar = (VMAvatar)caller;
            m_Head = new SimAvatar(Avatar.Avatar); //talk about confusing...
            m_Head.StripAllButHead();

            initSimHead();
        }

        private void initSimHead()
        {
            HeadCamera = new BasicCamera(GameFacade.GraphicsDevice, new Vector3(0.0f, 7.0f, -17.0f), Vector3.Zero, Vector3.Up);

            var pos2 = m_Head.Skeleton.GetBone("HEAD").AbsolutePosition;

            HeadCamera.Position = new Vector3(0, pos2.Y, 12.5f);
            HeadCamera.Target = pos2;

            HeadScene = new _3DTargetScene(GameFacade.GraphicsDevice, HeadCamera, new Point((int)(200 * TrueScale), (int)(200 * TrueScale)), (GlobalSettings.Default.AntiAlias > 0) ? 8 : 0);
            HeadScene.ID = "UIPieMenuHead";

            m_Head.Scene = HeadScene;
            m_Head.Scale = new Vector3(1f);

            HeadCamera.Zoom = 0f;
            HeadScene.Add(m_Head);
            GameFacade.Scenes.AddExternal(HeadScene); //AddExternal(HeadScene);
        }

        // R142: shared\sys\PieButt.bmp (17x17) — cached; falls back to the old
        // generated pill if the FAR member ever fails to mount.
        private static Microsoft.Xna.Framework.Graphics.Texture2D _pieButt;
        private static Microsoft.Xna.Framework.Graphics.Texture2D PieButtonTexture()
        {
            if (_pieButt != null) return _pieButt;
            try
            {
                var tx = Simitone.Client.UI.Model.UIOriginal.EnsureResolved("shared\\sys\\PieButt.bmp");
                if (tx != null) _pieButt = tx.Get(GameFacade.GraphicsDevice);
            }
            catch { }
            if (_pieButt == null) _pieButt = TextureGenerator.GetPieButtonImg(GameFacade.GraphicsDevice);
            return _pieButt;
        }

        public void RotateHeadCam(Vector2 point)
        {
            curRot = Vector2.Lerp(curRot, currentTarget, lerpSpeed);
            double xdir = Math.Atan(-curRot.X / 100.0);
            double ydir = Math.Atan(-curRot.Y / 100.0);

            Vector3 off = new Vector3(0, 0, 13.5f);
            Matrix mat = Microsoft.Xna.Framework.Matrix.CreateRotationY((float)xdir) * Microsoft.Xna.Framework.Matrix.CreateRotationX((float)ydir);

            HeadCamera.Position = new Vector3(0, 5.2f, 0) + Vector3.Transform(off, mat);
        }

        public void RemoveSimScene()
        {
            GameFacade.Scenes.RemoveExternal(HeadScene);
            HeadScene.Target.Dispose();
        }

        public void UpdateHeadPosition(int x, int y)
        {
            HeadCamera.ProjectionOrigin = new Vector2(100, 100);
        }

        public override void Update(FSO.Common.Rendering.Framework.Model.UpdateState state)
        {
            base.Update(state);
            if (m_BgGrow < 1)
            {
                m_BgGrow += 1.0 / 30.0 * (60.0 / FSOEnvironment.RefreshRate);
                HeadCamera.Zoom = (float)m_BgGrow * 5.12f;

                // UI-12: the disc grows to 2×max radius = 180 (cDDDSimsView::
                // Init vtable+488 arg 90; r142's own law text) — the code's
                // 200 predated the radius pin.
                m_Bg.SetSize((float)m_BgGrow * 180, (float)m_BgGrow * 180);
                m_Bg.X = (float)m_BgGrow * (-90);
                m_Bg.Y = (float)m_BgGrow * (-90);
            }
            RotateHeadCam(GlobalPoint(new Vector2(state.MouseState.X, state.MouseState.Y)));
            ShiftDown = state.ShiftDown;
        }

        public void RenderMenu()
        {
            PressedBubble = null;
            for (int i = 0; i < m_PieButtons.Count; i++) //remove previous buttons
            {
                this.Remove(m_PieButtons[i]);
            }
            m_PieButtons.Clear();

            var elems = m_CurrentItem.Children;
            // R142: the ORIGINAL slice-count quantizer (cTSPieMenu::Layout
            // 0x5290c0, band logic decoded with corrected branch senses):
            // 0 -> 1 slice; 1-2 items -> count+2; 3-4 -> count+4; >=5 -> count+8.
            // The old fixed 2/4/8 configs were the port's own spacing.
            int dirConfig = SliceCount(elems.Count);

            // AUD-17 C1-1: the +N bands are SPACING slots — at most 8 real
            // items ride the ring; items 8+ belong to the overflow stack
            // below. dirConfig > elems.Count by construction, so the old
            // `i >= elems.Count` break could never stop this loop and items
            // 8+ were placed radially AND stacked (duplicates whose click
            // index ran past Children). Cap the ring; angles still i/dirConfig.
            int ring = Math.Min(elems.Count, 8);
            for (int i = 0; i < ring; i++)
            {
                var elem = elems.ElementAt(i);
                var but = NewPieBubble(elem.Name, (elem.ColorMod > 0) ? HighlightStyle : ButtonStyle);

                // R142: original interaction-pie radius 90 (was 60).
                double dir = (((double)i) / dirConfig) * Math.PI * 2;

                if (i == 0)
                { //top
                    but.X = (float)(Math.Sin(dir) * 90 - but.Width / 2);
                    but.Y = (float)((Math.Cos(dir) * -90) - but.Size.Y);
                }
                else if (i == dirConfig / 2)
                { //bottom
                    but.X = (float)(Math.Sin(dir) * 90 - but.Width / 2);
                    but.Y = (float)((Math.Cos(dir) * -90));
                }
                else if (i < dirConfig / 2) //on right side
                {
                    but.X = (float)(Math.Sin(dir) * 90);
                    but.Y = (float)((Math.Cos(dir) * -90) - but.Size.Y / 2);
                }
                else //on left side
                {
                    but.X = (float)(Math.Sin(dir) * 90 - but.Width);
                    but.Y = (float)((Math.Cos(dir) * -90) - but.Size.Y / 2);
                }

                this.Add(but);
                m_PieButtons.Add(but);
                // UI-11: the original pie disc frame — shared\sys\PieButt.bmp
                // 17x17 (ctrl-mgr slot 15) 9-patched to the label rect and
                // tinted with the engine's frame color; label colors follow
                // TSPaint. R142's per-label 3-slice stretch (fixed 17px
                // height) superseded.
                but.OnButtonClick += new ButtonClickDelegate(PieButtonClick);
                but.OnButtonHover += new ButtonClickDelegate(PieButtonHover);
                but.OnButtonExit += new ButtonClickDelegate(PieButtonExit);
                but.OnButtonDown += new ButtonClickDelegate(PieButtonPress);
            }

            bool top = true;
            for (int i = 8; i < elems.Count; i++)
            {
                var elem = elems.ElementAt(i);
                // UI-11: overflow bubbles use the same decoded PieButt frame —
                // the pre-R142 generated pill survived only in this loop.
                var but = NewPieBubble(elem.Name + ((elem.Category) ? "..." : ""),
                    (elem.ColorMod > 0) ? HighlightStyle : ButtonStyle);

                but.X = (float)(-but.Width / 2);
                if (top)
                { //top
                    but.Y = (float)(-60 - but.Size.Y * ((i - 8) / 2 + 2));
                }
                else
                {
                    but.Y = (float)(60 + but.Size.Y * ((i - 8) / 2 + 1));
                }

                this.Add(but);
                m_PieButtons.Add(but);
                but.OnButtonClick += new ButtonClickDelegate(PieButtonClick);
                but.OnButtonHover += new ButtonClickDelegate(PieButtonHover);
                but.OnButtonExit += new ButtonClickDelegate(PieButtonExit);
                but.OnButtonDown += new ButtonClickDelegate(PieButtonPress);
                top = !top;
            }

            if (m_CurrentItem.Parent != null)
            {
                // UI-11: the title button draws in the selected/white family
                // like any label (native title path passes +216); the old
                // forced-cyan caption had no native counterpart.
                var but = NewPieBubble(m_CurrentItem.Name, ButtonStyle);

                but.X = (float)(-but.Width / 2);
                but.Y = (float)(-but.Size.Y / 2);
                this.Add(but);
                m_PieButtons.Add(but);
                but.OnButtonClick += new ButtonClickDelegate(BackButtonPress);
            }
        }

        private UIPieBubble NewPieBubble(string caption, TextStyle style)
        {
            var but = new UIPieBubble
            {
                Caption = caption,
                CaptionStyle = style,
                ImageStates = 1,
                Texture = PieButtonTexture(),
                AutoMargins = OriginalButtonMargin
            };
            but.Owner = this;
            but.FitToCaption();
            return but;
        }

        private void PieButtonPress(UIElement button)
        {
            PressedBubble = button as UIPieBubble;
        }

        void PieButtonHover(UIElement button)
        {
            var uiB = (UIButton)button;
            int index = m_PieButtons.IndexOf(uiB);
            currentTarget = button.Position + new Vector2(uiB.Width / 2f, uiB.Size.Y / 2f);
            HITVM.Get().PlaySoundEvent(UISounds.PieMenuHighlight);
        }

        void PieButtonExit(UIElement button)
        {
            currentTarget = Vector2.Zero;
            // a press that leaves its bubble ends the hold state
            if (PressedBubble == button as UIPieBubble) PressedBubble = null;
        }

        void BackButtonPress(UIElement button)
        {
            if (m_CurrentItem.Parent == null) return; //shouldn't ever be...
            m_CurrentItem = m_CurrentItem.Parent;
            HITVM.Get().PlaySoundEvent(UISounds.PieMenuSelect);
            RenderMenu();
        }

        private void PieButtonClick(UIElement button)
        {
            PressedBubble = null;
            int index = m_PieButtons.IndexOf((UIButton)button);
            if (index == -1) return; //bail! this isn't meant to happen!
            var action = m_CurrentItem.Children.ElementAt(index);
            HITVM.Get().PlaySoundEvent(UISounds.PieMenuSelect);

            if (action.Category)
            {
                m_CurrentItem = action;
                RenderMenu();
            }
            else
            {

                if (m_Obj == m_Parent.GotoObject)
                {
                    m_Parent.vm.SendCommand(new VMNetGotoCmd
                    {
                        Interaction = action.ID,
                        Param0 = action.Param0,
                        ActorUID = m_Caller.PersistID,
                        x = m_Obj.Position.x,
                        y = m_Obj.Position.y,
                        level = m_Obj.Position.Level
                    });
                }
                else
                {
                    if (FSO.Client.Debug.IDEHook.IDE != null && ShiftDown)
                    {
                        if (m_Obj.TreeTable.InteractionByIndex.ContainsKey((uint)action.ID))
                        {
                            var act = m_Obj.TreeTable.InteractionByIndex[(uint)action.ID];
                            ushort ActionID = act.ActionFunction;

                            var function = m_Obj.GetBHAVWithOwner(ActionID, m_Parent.vm.Context);

                            FSO.Client.Debug.IDEHook.IDE.IDEOpenBHAV(
                                function.bhav,
                                m_Obj.Object
                            );
                        }
                    }
                    else
                    {
                        m_Parent.vm.SendCommand(new VMNetInteractionCmd
                        {
                            Interaction = action.ID,
                            ActorUID = m_Caller.PersistID,
                            CalleeID = m_Obj.ObjectID,
                            Param0 = action.Param0,
                            Global = action.Global
                        });
                    }
                }
                HITVM.Get().PlaySoundEvent(UISounds.QueueAdd);
                m_Parent.ClosePie();

            }
        }

        public override void PreDraw(UISpriteBatch batch)
        {
            HeadScene.Draw(GameFacade.GraphicsDevice);
            base.PreDraw(batch);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            base.Draw(batch);
            if (m_CurrentItem == m_PieTree)
            {
                var invScale = new Vector2(1 / TrueScale, 1 / TrueScale);
                DrawLocalTexture(batch, HeadScene.Target, null, new Vector2(-100, -100), invScale);
            } //if we're top level, draw head!
        }
    }

    // UI-11: one interaction-pie bubble on the decoded cTSPieMenu law
    // (tools/iff-dump/pie-label-paint-law.md). The frame is the 17x17
    // PieButt disc 9-patched to the measured label rect + 3px per side
    // (Layout 0x5290c0 / DrawLabelFrame 0x528d10) and tinted with the
    // engine's +100 frame color; label text draws per the TSPaint state law
    // (untracked lavender, hover cyan, press white). UIButton's 3-slice draw
    // fixes the height at the tile's 17px, which crushed labels against the
    // caps; input handling stays UIButton's.
    public class UIPieBubble : UIButton
    {
        // cap width kept from the engine 3-slice (17/3); the native patch
        // widths live behind the undecoded TOC-indirected DrawLabelFrame
        // state, so the port keeps its established cap curvature.
        public const int CornerSize = 5;

        private TextStyle m_OwnedStyle;
        private UIPieMenu m_Owner;

        public UIPieMenu Owner
        {
            get { return m_Owner; }
            set { m_Owner = value; }
        }

        // Rebuild the frame from the caption: width = advance + 2*margin
        // (AutoMargins drives the same law inside UIButton), height =
        // measured label height + 6. The base Size getter reports the
        // texture height (17), which fed the old fixed-height law into the
        // radius anchoring; report the real frame instead.
        public void FitToCaption()
        {
            m_OwnedStyle = (CaptionStyle != null) ? CaptionStyle.Clone() : null;
            var size = (m_OwnedStyle != null) ? m_OwnedStyle.MeasureString(Caption)
                                              : new Vector2(17, 12);
            int margin = UIPieMenu.OriginalButtonMargin;
            int w = (int)size.X + margin * 2;
            int h = (int)size.Y + margin * 2;

            Width = Math.Max(w, CornerSize * 2);
            if (ClickHandler != null)
            {
                ClickHandler.Region.Width = (int)Width;
                ClickHandler.Region.Height = Math.Max(h, CornerSize * 2);
            }
        }

        public override Vector2 Size
        {
            get
            {
                return new Vector2(Width, GetBounds().Height);
            }
        }

        private Color CurrentLabelColor()
        {
            // TSPaint state law (UI-15, raw 0x528a74-0x528aa0): the tracked
            // bubble draws +212 idle and +216 while pressed; every other
            // bubble draws +208 regardless of press state.
            var pressed = (m_Owner != null) ? m_Owner.PressedBubble : null;
            if (pressed == this) return Color.White;
            if (Hovered) return UIPieMenu.HoverLabelColor;
            return UIPieMenu.UntrackedLabelColor;
        }

        public override void Draw(UISpriteBatch SBatch)
        {
            if (!Visible) return;
            var tex = Texture;
            if (tex == null)
            {
                base.Draw(SBatch);
                return;
            }

            int w = (int)Width; if (w <= 0) w = 17;
            int h = GetBounds().Height; if (h <= 0) h = 17;
            int c = CornerSize;
            if (w < c * 2) w = c * 2;
            if (h < c * 2) h = c * 2;
            float midW = w - c * 2, midH = h - c * 2;
            int tw = tex.Width, th = tex.Height;
            int srcMidW = tw - c * 2, srcMidH = th - c * 2;
            var tint = UIPieMenu.PieFrameTint;

            // corners, 1:1 so the disc arcs keep their curvature
            DrawLocalTexture(SBatch, tex, new Rectangle(0, 0, c, c), Vector2.Zero, Vector2.One, tint);
            DrawLocalTexture(SBatch, tex, new Rectangle(tw - c, 0, c, c), new Vector2(w - c, 0), Vector2.One, tint);
            DrawLocalTexture(SBatch, tex, new Rectangle(0, th - c, c, c), new Vector2(0, h - c), Vector2.One, tint);
            DrawLocalTexture(SBatch, tex, new Rectangle(tw - c, th - c, c, c), new Vector2(w - c, h - c), Vector2.One, tint);
            // edges, stretched
            DrawLocalTexture(SBatch, tex, new Rectangle(c, 0, srcMidW, c), new Vector2(c, 0), new Vector2(midW / srcMidW, 1f), tint);
            DrawLocalTexture(SBatch, tex, new Rectangle(c, th - c, srcMidW, c), new Vector2(c, h - c), new Vector2(midW / srcMidW, 1f), tint);
            DrawLocalTexture(SBatch, tex, new Rectangle(0, c, c, srcMidH), new Vector2(0, c), new Vector2(1f, midH / srcMidH), tint);
            DrawLocalTexture(SBatch, tex, new Rectangle(tw - c, c, c, srcMidH), new Vector2(w - c, c), new Vector2(1f, midH / srcMidH), tint);
            // center
            DrawLocalTexture(SBatch, tex, new Rectangle(c, c, srcMidW, srcMidH), new Vector2(c, c), new Vector2(midW / srcMidW, midH / srcMidH), tint);

            var caption = Caption;
            if (caption != null && m_OwnedStyle != null)
            {
                m_OwnedStyle.Color = CurrentLabelColor();
                var box = GetBounds();
                box.Height -= 2;
                this.DrawLocalString(SBatch, caption, Vector2.Zero, m_OwnedStyle, box,
                    TextAlignment.Center | TextAlignment.Middle, Rectangle.Empty, UIElementState.Normal);
            }
        }
    }

    public class UIPieMenuItem
    {
        public bool Category;
        public byte ID;
        public short Param0;
        public bool Global;
        public int ColorMod;
        public string Name;
        public List<UIPieMenuItem> Children = new List<UIPieMenuItem>();
        public Dictionary<string, UIPieMenuItem> ChildrenByName = new Dictionary<string, UIPieMenuItem>();
        public UIPieMenuItem Parent;
    }
}
