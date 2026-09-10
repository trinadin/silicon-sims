/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Framework.Parser;
using FSO.Client.UI.Model;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Rendering.Framework.IO;
using FSO.Client.Utils;
using FSO.SimAntics;
using FSO.Common.Utils;
using FSO.Client.UI.Controls;
using Simitone.Client.UI.Panels;
using FSO.Client;
using FSO.HIT;
using FSO.Content;

namespace Simitone.Client.UI.Controls
{
    /// <summary>
    /// Used to display an interaction. Will eventually have all features like the timer, big huge red x support for cancel etc.
    /// </summary>
    public class UIInteraction : UIContainer
    {
        public const int OriginalIconSize = 45;

        public Texture2D Icon;
        private UITooltipHandler m_TooltipHandler;
        private Texture2D Background;
        private Texture2D Overlay;
        private Texture2D Rim;
        private bool Active;
        private UIMouseEventRef ClickHandler;
        public event ButtonClickDelegate OnMouseEvent;
        public delegate void InteractionResultDelegate(UIElement me, bool accepted);
        public event InteractionResultDelegate OnInteractionResult;
        public UIIQTrackEntry ParentEntry;

        public UIButton AcceptButton;
        public UIButton DeclineButton;

        public float OverlayScale;
        public float RimScale;
        public bool Dead;
        internal Rectangle InteractionClickRegion => ClickHandler.Region;
        internal Texture2D OriginalFrameTexture => Background;
        internal Texture2D OriginalCancelTexture => Overlay;

        public void SetCancelled()
        {
            OverlayScale = OriginalStyle ? 1f : 2f;
            // R142: the ORIGINAL cancel overlay — Res_Other id 9004
            // Other/QueueCancel.bmp 45x45 (the red X over queued interactions);
            // int_cancel.png was the Simitone-era X.
            try
            {
                var qc = Simitone.Client.UI.Model.UIOriginal.EnsureResolved("other\\QueueCancel.bmp");
                if (qc != null) Overlay = qc.Get(GameFacade.GraphicsDevice);
            }
            catch { }
            if (Overlay == null) Overlay = Content.Get().CustomUI.Get("int_cancel.png").Get(GameFacade.GraphicsDevice);
            if (!OriginalStyle)
            {
                GameFacade.Screens.Tween.To(this, 0.5f, new Dictionary<string, float>() { { "OverlayScale", 1f }}, TweenQuad.EaseOut);
            }
        }

        // Desktop uses the original 45x45 cActionIcon composition. The pill
        // backgrounds, large active icon and rim remain the mobile skin.
        public static readonly bool OriginalStyle = !FSO.Common.FSOEnvironment.SoftwareKeyboard;

        public void SetActive(bool active)
        {
            if (OriginalStyle)
            {
                Active = active;
                ClickHandler.Region = new Rectangle(0, 0, OriginalIconSize, OriginalIconSize);
                ScaleX = ScaleY = 1f;
                Rim = null;
                ResolveOriginalFrame();
                return;
            }

            if (active)
            {
                Rim = Content.Get().CustomUI.Get("int_big_sel.png").Get(GameFacade.GraphicsDevice);
                Background = Content.Get().CustomUI.Get("int_big_bg.png").Get(GameFacade.GraphicsDevice);
                if (!Active)
                {
                    ClickHandler.Region = new Rectangle(-55, -55, 110, 110);
                    ScaleX = ScaleY = 0.5f;
                    GameFacade.Screens.Tween.To(this, 0.5f, new Dictionary<string, float>() { { "ScaleX", 1f }, { "ScaleY", 1f } }, TweenQuad.EaseOut);

                    RimScale = 2f;
                    GameFacade.Screens.Tween.To(this, 0.5f, new Dictionary<string, float>() { { "RimScale", 1f } }, TweenQuad.EaseOut);
                }
            }
            else
            {
                Rim = null;
                Background = Content.Get().CustomUI.Get("int_small_bg.png").Get(GameFacade.GraphicsDevice);
                if (Active)
                {
                    ClickHandler.Region = new Rectangle(-23, -23, 46, 46);
                    ScaleX = ScaleY = 2f;
                    GameFacade.Screens.Tween.To(this, 0.5f, new Dictionary<string, float>() { { "ScaleX", 1f }, { "ScaleY", 1f } }, TweenQuad.EaseOut);
                }
            }
            this.Active = active;
        }

        public UIInteraction(bool Active)
        {
            ClickHandler = ListenForMouse(new Rectangle(-23, -23, 46, 46), new UIMouseEvent(MouseEvt));
            SetActive(Active);
            m_TooltipHandler = UIUtils.GiveTooltip(this);
        }


        public void Kill()
        {
            GameFacade.Screens.Tween.To(this, 0.3f, new Dictionary<string, float>() { { "Y", -60f } }, TweenQuad.EaseIn);
            GameThread.SetTimeout(() => Parent.Remove(this), 300);
            Dead = true;
        }

        /*
          actionfaceselection = 0x1B200000001,
          actionhappy = 0x1B300000001,
          actionmad = 0x1B400000001,
          actionneutral = 0x1B500000001,
          actionsad = 0x1B600000001,
          */

        public void UpdateInteractionResult(sbyte result)
        {
            return;
        }

        private void MouseEvt(UIMouseEventType type, UpdateState state)
        {
            if (Dead) return;
            if (type == UIMouseEventType.MouseDown) OnMouseEvent(this); //pass to parents to handle
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            base.Draw(batch);
            if (OriginalStyle)
            {
                ResolveOriginalFrame();
                if (Background != null)
                {
                    // MakeActionBitmap's flag selects the left/right 45px cell.
                    // SetAction defaults that flag true; the active Sim derives it
                    // from an original person field the port does not model. Do not
                    // conflate it with queue position: use the proven default cell.
                    var frame = new Rectangle(0, 0, OriginalIconSize, OriginalIconSize);
                    if (frame.Right <= Background.Width && frame.Bottom <= Background.Height)
                    {
                        DrawLocalTexture(batch, Background, frame, Vector2.Zero, Vector2.One);
                    }
                    else
                    {
                        DrawLocalTexture(batch, Background, null, Vector2.Zero,
                            new Vector2(OriginalIconSize / (float)Background.Width, OriginalIconSize / (float)Background.Height));
                    }
                }
                // IconFrame.bmp is an OPAQUE blue button face, not a rim.
                // cActionIcon paints it first and composites the action bitmap
                // over it. Painting it after the icon hid every queue image and
                // produced the solid-blue 45x45 placeholder seen in LIVE mode.
                DrawOriginalIcon(batch);
                if (Overlay != null)
                {
                    DrawLocalTexture(batch, Overlay, null, Vector2.Zero,
                        new Vector2(OriginalIconSize / (float)Overlay.Width, OriginalIconSize / (float)Overlay.Height));
                }
                return;
            }

            if (Background != null) DrawLocalTexture(batch, Background, null, new Vector2(-Background.Width, -Background.Height)/2, Vector2.One, new Color(104, 164, 184, 255));
            var iconSize = (Active) ? 74f : 37f;
            if (Icon != null)
            {
                if (Icon.Width/(float)Icon.Height < 1.1f)
                {
                    DrawLocalTexture(batch, Icon, new Rectangle(0, 0, Icon.Width, Icon.Height), new Vector2(iconSize/-2, iconSize / -2), new Vector2(iconSize / Icon.Width, iconSize / Icon.Height));
                }
                else DrawLocalTexture(batch, Icon, new Rectangle(0, 0, Icon.Width / 2, Icon.Height), new Vector2(iconSize / -2, iconSize / -2), new Vector2(iconSize / Icon.Height, iconSize / Icon.Height));
            }
            if (Overlay != null) DrawLocalTexture(batch, Overlay, null, new Vector2(Overlay.Width, Overlay.Height) * (OverlayScale/-2), new Vector2(OverlayScale), Color.White * (2 - OverlayScale));
            if (Rim != null) DrawLocalTexture(batch, Rim, null, new Vector2(Rim.Width, Rim.Height) * (RimScale / -2), new Vector2(RimScale), Color.White * (2-RimScale));
        }

        private void DrawOriginalIcon(UISpriteBatch batch)
        {
            if (Icon == null) return;

            var source = OriginalSourceRectangle(Icon.Width, Icon.Height);
            DrawLocalTexture(batch, Icon, source, Vector2.Zero,
                new Vector2(OriginalIconSize / (float)source.Width, OriginalIconSize / (float)source.Height));
        }

        /// <summary>
        /// cActionIcon::SetIcon accepts canonical 45/180/270px-wide sheets and
        /// 45/135px-high sheets. MakeActionBitmap selects column zero and the
        /// middle row of a 135px-high sheet. The two-column branch preserves the
        /// port's existing compatibility with its non-canonical wide thumbnails.
        /// </summary>
        public static Rectangle OriginalSourceRectangle(int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            var columns = (width == 270) ? 6 : (width == 180) ? 4 : 1;
            if (columns == 1 && width > height * 1.1f) columns = 2;
            var rows = (height == 135) ? 3 : 1;
            var sourceWidth = Math.Max(1, width / columns);
            var sourceHeight = Math.Max(1, height / rows);
            return new Rectangle(0, (rows == 3) ? sourceHeight : 0,
                sourceWidth, sourceHeight);
        }

        private void ResolveOriginalFrame()
        {
            if (Background != null) return;
            try
            {
                Background = Simitone.Client.UI.Model.UIOriginal.EnsureResolved("cpanel\\Buttons\\IconFrame.bmp")?.Get(GameFacade.GraphicsDevice);
            }
            catch { }
        }

        public override Rectangle GetBounds()
        {
            return new Rectangle(0, 0, ClickHandler.Region.Width, ClickHandler.Region.Height);
        }
    }
}
