using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView.Components;
using FSO.SimAntics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Rendering.Framework.IO;
using FSO.UI.Panels;

namespace Simitone.Client.UI.Panels
{
    public class UIPickupPanel : UIContainer
    {
        public UILabel TitleLabel;
        public UILabel SubtextLabel;
        public Texture2D Thumbnail;
        public UI3DThumb Thumb3D;
        protected UIMouseEventRef ClickHandler;

        public UICatButton CancelButton;

        // ORIG-01 #3 (completing R144's "future round"): the desktop carry
        // plaque, composed from the original kit — Gendlg picture-window
        // chrome + original caption glyphs + the WinBtn UIBigButton — with
        // the same original STR# 136 sellback strings the mobile strip used.
        // ORIG-02 dispose-popup-law CLOSED the residual: there is NO
        // cWinDisposePopup window class in the binary (symbol census) — the
        // native surface is cMoveTool::StartDisposing 0x1016b940 feeding
        // CPState's dispose machinery, whose plaque chrome is CPState's
        // toast. No layout law exists to decode; sizes/position here are
        // port-chosen, materials are 100% original.
        private const int PlaqueW = 450, PlaqueH = 64;
        private Simitone.Client.UI.Controls.UIOriginalText TitleOriginal;
        private Simitone.Client.UI.Controls.UIOriginalText SubtextOriginal;
        private Simitone.Client.UI.Controls.UIBigButton CancelDesktop;
        private string TitleText = "";
        private string SubText = "";

        public event Action<bool> OnResponse;

        public UIPickupPanel()
        {
            TitleLabel = new UILabel();
            TitleLabel.Position = new Vector2(450, 14);
            TitleLabel.CaptionStyle = TitleLabel.CaptionStyle.Clone();
            TitleLabel.CaptionStyle.Size = 19;
            TitleLabel.CaptionStyle.Color = UIStyle.Current.SecondaryText;
            Add(TitleLabel);

            SubtextLabel = new UILabel();
            SubtextLabel.Position = new Vector2(450, 44);
            SubtextLabel.CaptionStyle = SubtextLabel.CaptionStyle.Clone();
            SubtextLabel.CaptionStyle.Size = 12;
            SubtextLabel.CaptionStyle.Color = UIStyle.Current.Text;
            Add(SubtextLabel);

            if (FSO.Common.FSOEnvironment.SoftwareKeyboard)
            {
                CancelButton = new UICatButton(Content.Get().CustomUI.Get("cat_cancel.png").Get(GameFacade.GraphicsDevice));
                CancelButton.Position = new Vector2(174, 31);
                CancelButton.OnButtonClick += CancelButton_OnButtonClick;
                Add(CancelButton);
            }
            else
            {
                var font = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadCaption(GameFacade.GraphicsDevice);
                TitleOriginal = new Simitone.Client.UI.Controls.UIOriginalText("", font) { Color = new Color(195, 205, 205) };
                SubtextOriginal = new Simitone.Client.UI.Controls.UIOriginalText("", font) { Color = new Color(139, 148, 160) };
                CancelDesktop = new Simitone.Client.UI.Controls.UIBigButton(false);
                CancelDesktop.Caption = GameFacade.Strings.GetString("142", "1"); // R115 ObjDialogs [1] — the cancel law
                CancelDesktop.OnButtonClick += CancelButton_OnButtonClick;
                Add(TitleOriginal); Add(SubtextOriginal); Add(CancelDesktop);
                UpdatePlaqueLayout();
            }

            ClickHandler =
                ListenForMouse(new Rectangle(0, 0, 400, 128), new UIMouseEvent(OnMouseEvent));
        }

        private void UpdatePlaqueLayout()
        {
            var sw = UIScreen.Current?.ScreenWidth ?? GlobalSettings.Default.GraphicsWidth;
            X = sw - PlaqueW - 10;
            Y = (UIScreen.Current?.ScreenHeight ?? GlobalSettings.Default.GraphicsHeight) - 100 - PlaqueH - 8;
            if (TitleOriginal != null)
            {
                var tf = TitleOriginal.Font;
                TitleOriginal.X = (PlaqueW - 190 - (tf?.Measure(TitleText) ?? 0)) / 2;
                TitleOriginal.Y = 8;
            }
            if (SubtextOriginal != null)
            {
                var sf = SubtextOriginal.Font;
                SubtextOriginal.X = (PlaqueW - 190 - (sf?.Measure(SubText) ?? 0)) / 2;
                SubtextOriginal.Y = 32;
            }
            if (CancelDesktop != null)
            {
                CancelDesktop.Position = new Vector2(PlaqueW - 95, (PlaqueH - 33) / 2);
            }
        }

        public override void GameResized()
        {
            UpdatePlaqueLayout();
            base.GameResized();
        }

        private void OnMouseEvent(UIMouseEventType type, UpdateState state)
        {
            if (type == UIMouseEventType.MouseDown)
            {
                OnResponse?.Invoke(true);
            }
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (Visible)
            {
                SubtextLabel.CaptionStyle.Color = UIStyle.Current.Text * Opacity;
                TitleLabel.CaptionStyle.Color = UIStyle.Current.SecondaryText * Opacity;
                if (CancelButton != null) CancelButton.Opacity = Opacity; // ORIG-01: desktop constructs CancelDesktop instead
            }
        }

        private void CancelButton_OnButtonClick(UIElement button)
        {
            OnResponse?.Invoke(false);
        }

        public void SetInfo(VM vm, VMEntity entity)
        {
            var obj = entity.Object;
            var def = entity.MasterDefinition;
            if (def == null) def = entity.Object.OBJ;

            CTSS catString = obj.Resource.Get<CTSS>(def.CatalogStringsID);
            if (catString != null)
            {
                TitleLabel.Caption = catString.GetString(0);
            }
            else
            {
                TitleLabel.Caption = entity.ToString();
            }
            TitleText = TitleLabel.Caption;
            var World = vm.Context.World;
            var sellback = entity.MultitileGroup.Price;

            var canDelete = entity.IsUserMovable(vm.Context, true) == FSO.SimAntics.Model.VMPlacementError.Success;
            if (!canDelete)
            {
                SubtextLabel.Caption = GameFacade.Strings.GetString("136", "2").Replace("%", TitleLabel.Caption); //cannot be deleted
            } else
            {
                if (sellback > 0)
                {
                    SubtextLabel.Caption = GameFacade.Strings.GetString("136", "1").Replace("%", TitleLabel.Caption).Replace("$", "§" + sellback.ToString());
                } else
                {
                    SubtextLabel.Caption = GameFacade.Strings.GetString("136", "0").Replace("%", TitleLabel.Caption);
                }

            }
            SubText = SubtextLabel.Caption;
            if (TitleOriginal != null)
            {
                TitleOriginal.Text = TitleText;
                SubtextOriginal.Text = SubText;
                UpdatePlaqueLayout();
            }

            if (entity is VMGameObject)
            {
                var objects = entity.MultitileGroup.Objects;
                ObjectComponent[] objComps = new ObjectComponent[objects.Count];
                for (int i = 0; i < objects.Count; i++)
                {
                    objComps[i] = (ObjectComponent)objects[i].WorldUI;
                }
                if (Thumbnail != null) Thumbnail.Dispose();
                if (Thumb3D != null) Thumb3D.Dispose();
                Thumbnail = null; Thumb3D = null;
                if (FSOEnvironment.Enable3D)
                {
                    Thumb3D = new UI3DThumb(entity);
                }
                else
                {
                    var thumb = World.GetObjectThumb(objComps, entity.MultitileGroup.GetBasePositions(), GameFacade.GraphicsDevice);
                    Thumbnail = thumb;
                }
            }
            else
            {
                if (Thumbnail != null) Thumbnail.Dispose();
                if (Thumb3D != null) Thumb3D.Dispose();
                Thumbnail = null; Thumb3D = null;
                Thumbnail = null;
            }
        }

        public override void PreDraw(UISpriteBatch batch)
        {
            if (!Visible) return;
            // R144/ORIG-01 #3: mobile keeps its strip; desktop now paints the
            // carry plaque composed from the original dialog kit (ORIG-02
            // closed the residual: no cWinDisposePopup class exists natively
            // — the surface is cMoveTool::StartDisposing 0x1016b940 +
            // CPState's toast; no layout law exists to decode).
            if (!FSO.Common.FSOEnvironment.SoftwareKeyboard) return;
            base.PreDraw(batch);
            Thumb3D?.Draw();
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            if (FSO.Common.FSOEnvironment.SoftwareKeyboard)
            {
                base.Draw(batch);
                var targSize = 180f;

                float scale = 1f;
                Texture2D thumb = null;
                if (Thumb3D != null)
                {
                    thumb = Thumb3D.Tex;
                }
                else if (Thumbnail != null)
                {
                    scale = targSize / (float)Math.Sqrt(Thumbnail.Width * Thumbnail.Width + Thumbnail.Height * Thumbnail.Height);
                    thumb = Thumbnail;
                }

                if (thumb != null)
                {
                    var pos = new Vector2(thumb.Width * scale - 350 * 2, thumb.Height * scale - 128) / -2;
                    DrawLocalTexture(batch, thumb, null, pos, new Vector2(scale));
                }
                return;
            }
            // desktop: the original-kit plaque (texts fade with the carry
            // tween's Opacity)
            Simitone.Client.UI.Controls.UIOriginalDialogChrome.DrawPictureWindow(this, batch, 0, 0, PlaqueW, PlaqueH);
            if (TitleOriginal != null)
            {
                TitleOriginal.Color = new Color(195, 205, 205) * Opacity;
                SubtextOriginal.Color = new Color(139, 148, 160) * Opacity;
            }
            CancelDesktop.Opacity = Opacity;
            base.Draw(batch);
        }

        public override void Removed()
        {
            if (Thumbnail != null) Thumbnail.Dispose();
            if (Thumb3D != null) Thumb3D.Dispose();
        }
    }
}
