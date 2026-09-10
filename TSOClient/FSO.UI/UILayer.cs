using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Controls;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Rendering.Framework;
using System.Diagnostics;
using FSO.Client.Utils;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Utils;
using FSO.Common;

namespace FSO.Client.UI
{
    public class UILayer : IGraphicsLayer
    {
        private Microsoft.Xna.Framework.Game m_G;
        private List<UIScreen> m_Screens = new List<UIScreen>();
        private List<UIExternalContainer> m_ExtContainers = new List<UIExternalContainer>();
        private List<IUIProcess> m_UIProcess = new List<IUIProcess>();

        public UITooltipProperties TooltipProperties = new UITooltipProperties();
        public string Tooltip;

        private SpriteFont m_SprFontBig;
        private SpriteFont m_SprFontSmall;

        //For displaying 3D objects (sims).
        private Matrix m_WorldMatrix, m_ViewMatrix, m_ProjectionMatrix;
        private Dictionary<int, string> m_TextDict;

        //for fps counter
        private Stopwatch fpsStopwatch;

        /// <summary>
        /// Top most UI container
        /// </summary>
        private UIContainer mainUI;
        private UIContainer dialogContainer;

        public InputManager inputManager;
        private UIScreen currentScreen;

        /** Animation utility **/
        public UITween Tween;

        public Microsoft.Xna.Framework.Game GameComponent
        {
            get { return m_G; }
        }

        public UIContainer Root
        {
            get { return mainUI; }
        }

        /// <summary>
        /// A worldmatrix, used to display 3D objects (sims).
        /// Initialized in the ScreenManager's constructor.
        /// </summary>
        public Matrix WorldMatrix
        {
            get { return m_WorldMatrix; }
            set { m_WorldMatrix = value; }
        }

        /// <summary>
        /// A viewmatrix, used to display 3D objects (sims).
        /// Initialized in the ScreenManager's constructor.
        /// </summary>
        public Matrix ViewMatrix
        {
            get { return m_ViewMatrix; }
            set { m_WorldMatrix = value; }
        }

        /// <summary>
        /// A projectionmatrix, used to display 3D objects (sims).
        /// Initialized in the ScreenManager's constructor.
        /// </summary>
        public Matrix ProjectionMatrix
        {
            get { return m_ProjectionMatrix; }
            set { m_ProjectionMatrix = value; }
        }

        /// <summary>
        /// The graphicsdevice that is part of the game instance.
        /// Used when calling XNA's graphic functions.
        /// </summary>
        public GraphicsDevice GraphicsDevice
        {
            get { return m_G.GraphicsDevice; }
        }

        /// <summary>
        /// The UIScreen instance that is currently being 
        /// updated and rendered by this ScreenManager instance.
        /// </summary>
        public UIScreen CurrentUIScreen
        {
            get
            {
                return currentScreen;
            }
        }

        /// <summary>
        /// Gets or sets the internal dictionary containing all the strings for the game.
        /// </summary>
        public Dictionary<int, string> TextDict
        {
            get { return m_TextDict; }
            set { m_TextDict = value; }
        }

        public UILayer(Microsoft.Xna.Framework.Game G)
        {
            fpsStopwatch = new Stopwatch();
            fpsStopwatch.Start();

            m_G = G;

            m_WorldMatrix = Matrix.Identity;
            m_ViewMatrix = Matrix.CreateLookAt(Vector3.Right * 5, Vector3.Zero, Vector3.Forward);
            m_ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(MathHelper.Pi / 4.0f,
                    (float)GraphicsDevice.PresentationParameters.BackBufferWidth / 
                    (float)GraphicsDevice.PresentationParameters.BackBufferHeight,
                    1.0f, 100.0f);

            TextStyle.DefaultTitle = new TextStyle {
                Font = GameFacade.MainFont,
                VFont = GameFacade.VectorFont,
                Size = 10,
                Color = new Color(255,249,157),
                SelectedColor = new Color(0x00, 0x38, 0x7B),
                SelectionBoxColor = new Color(255, 249, 157)
            };

            TextStyle.DefaultButton = new TextStyle
            {
                Font = GameFacade.MainFont,
                VFont = GameFacade.VectorFont,
                Size = 10,
                Color = new Color(255, 249, 157),
                SelectedColor = new Color(0x00, 0x38, 0x7B),
                SelectionBoxColor = new Color(255, 249, 157)
            };

            TextStyle.DefaultLabel = new TextStyle
            {
                Font = GameFacade.MainFont,
                VFont = GameFacade.VectorFont,
                Size = 10,
                Color = new Color(255, 249, 157),
                SelectedColor = new Color(0x00, 0x38, 0x7B),
                SelectionBoxColor = new Color(255, 249, 157)
            };

            Tween = new UITween();
            this.AddProcess(Tween);

            inputManager = new InputManager();
            inputManager.RequireWindowFocus = true;
            mainUI = new UIContainer();
            dialogContainer = new UIContainer();
            mainUI.Add(dialogContainer);

            // Create a new SpriteBatch, which can be used to draw textures.
            SpriteBatch = new UISpriteBatch(GraphicsDevice, 0);
            //GameFacade.OnContentLoaderReady += new BasicEventHandler(GameFacade_OnContentLoaderReady);
            m_G.GraphicsDevice.DeviceReset += new EventHandler<EventArgs>(GraphicsDevice_DeviceReset);
        }

        private void GraphicsDevice_DeviceReset(object sender, EventArgs e)
        {
            for (int i = 0; i < m_Screens.Count; i++)
                m_Screens[i].DeviceReset(m_G.GraphicsDevice);
        }

        public void AddProcess(IUIProcess Proc)
        {
            m_UIProcess.Add(Proc);
        }

        public void RemoveProcess(IUIProcess Proc)
        {
            m_UIProcess.Remove(Proc);
        }

        /// <summary>
        /// Adds a UIScreen instance to this ScreenManager's list of screens.
        /// This function is called from Lua.
        /// </summary>
        /// <param name="Screen">The UIScreen instance to be added.</param>
        public void AddScreen(UIScreen Screen)
        {
            AssetStreaming.EndStreaming();

            /*if (currentScreen != null)
            {
                mainUI.Remove(currentScreen);
            }*/
            /** Add screen on top **/
            mainUI.Add(Screen);
            /** Bring dialogs to top **/
            mainUI.Add(dialogContainer);
            /** Bring debug to the top **/
            //mainUI.Add(debugButton);

            Screen.OnShow();

            m_Screens.Add(Screen);
            currentScreen = Screen;
        }

        public void RemoveScreen(UIScreen Screen)
        {
            if (Screen == currentScreen)
            {
                currentScreen = null;
            }
            Screen.OnHide();
            mainUI.Remove(Screen);
            m_Screens.Remove(Screen);

            /** Put the previous screen back into the UI **/
            if (m_Screens.Count > 0)
            {
                currentScreen = m_Screens.Last();
                mainUI.AddAt(0, currentScreen);
            }
        }

        public void AddExternal(UIExternalContainer cont)
        {
            //todo: init?
            lock (m_ExtContainers)
            {
                m_ExtContainers.Add(cont);
            }
        }

        public void RemoveExternal(UIExternalContainer cont)
        {
            lock (m_ExtContainers)
            {
                //todo: release resources?
                GameThread.NextUpdate(x =>
                {
                    cont.CleanupFocus(x);
                    cont.Removed();
                });
                m_ExtContainers.Remove(cont);
            }
        }

        public void RemoveCurrent()
        {
            /** Remove all dialogs **/
            while (Dialogs.Count > 0)
            {
                RemoveDialog(Dialogs[0]);
            }

            var currentScreen = mainUI.GetChildren().OfType<UIScreen>().FirstOrDefault();
            if (currentScreen != null)
            {
                ((UIScreen)currentScreen).OnHide();
                mainUI.Remove(currentScreen);
                m_Screens.Remove(currentScreen);
            }
        }

        public void Update(UpdateState state)
        {
            if (GameFacade.Game.Window == null) return;

            var mousePosition = state.MouseState.Position;
            var bounds = GameFacade.Game.Window.ClientBounds;
            state.MouseOverWindow = mousePosition.X > 0 && mousePosition.Y > 0 &&
                                    mousePosition.X < bounds.Width && mousePosition.Y < bounds.Height;
            if (FSOEnvironment.SoftwareKeyboard) state.MouseOverWindow = true;
            state.WindowFocused = GameFacade.Game.IsActive;

            /** 
             * Handle the mouse events from the previous frame
             * It's important to do this before the update calls because
             * a lot of mouse events will make changes to the UI. If they do,
             * we want the matrices to be recalculated before the draw
             * method and that is done in the update method.
             */
            if (state.ProcessMouseEvents){
                inputManager.HandleMouseEvents(state);
            }
            state.MouseEvents.Clear();

            state.InputManager = inputManager;
            Content.Content.Get()?.Changes.RunResModifications();
            mainUI.Update(state);

            if (state.AltDown && state.NewKeys.Contains(Microsoft.Xna.Framework.Input.Keys.Enter))
            {
                GameFacade.GraphicsDeviceManager.ToggleFullScreen();
            }

            lock (m_ExtContainers)
            {
                var extCopy = new List<UIExternalContainer>(m_ExtContainers);
                foreach (var ext in extCopy)
                {
                    lock (ext)
                    {
                        ext.Update(state);
                    }
                }
            }

            /** Process external update handlers **/
            foreach (var item in m_UIProcess)
            {
                item.Update(state);
            }

            Tooltip = state.UIState.Tooltip;
            TooltipProperties = state.UIState.TooltipProperties;
        }

        public void PreDraw(UISpriteBatch SBatch)
        {
            mainUI.PreDraw(SBatch);
        }

        public void Draw(UISpriteBatch SBatch)
        {
            mainUI.Draw(SBatch);

            if (TooltipProperties.UpdateDead) TooltipProperties.Show = false;
            if (Tooltip != null && TooltipProperties.Show) DrawTooltip(SBatch, TooltipProperties.Position, TooltipProperties.Opacity, TooltipProperties.Color);
            TooltipProperties.UpdateDead = true;
        }

        public void DrawTooltip(SpriteBatch batch, Vector2 position, float opacity, Color color)
        {
            TextStyle style = TextStyle.DefaultLabel.Clone();
            var toolScale = FSOEnvironment.DPIScaleFactor; //*zoom scale?
            style.Color = color;
            // TS1 cDefaultTTWindow::Init asks the font factory for face 1 at
            // size 7 (0x3b9884-0x3b98a0). SetToolTip then sizes the window to
            // textWidth + 6 by charHeight + 2 and paints at x=3, y=1
            // (0x3b9428-0x3b9448, 0x3b9634-0x3b964c). The shipped _07 FFN's
            // cached glyph sheet normalizes its -2 top bearing; the shared
            // FFN renderer now applies the same normalization to destinations.
            const int tooltipFontSize = 7;
            const int tooltipLineHeight = 13;
            const int tooltipXInset = 3;
            const int tooltipYInset = 1;
            // Select the original native face first, then apply runtime DPI
            // uniformly to its glyphs and measured tooltip geometry.
            style.Size = tooltipFontSize;

            var scale = new Vector2(toolScale, toolScale);
            if (style.Scale != 1.0f)
            {
                scale = new Vector2(scale.X * style.Scale, scale.Y * style.Scale);
            }

            // SetToolTipLong can still need the port's 300px safety wrap. Keep
            // that behavior, but give its content the original 3px sides.
            var wrapped = UIUtils.WordWrap(Tooltip, 294, style);

            int width = (int)((wrapped.MaxWidth + 6) * toolScale);
            int height = (int)(toolScale * tooltipLineHeight * wrapped.Lines.Count + 2 * toolScale);

            position.X = Math.Min(position.X, GlobalSettings.Default.GraphicsWidth*FSOEnvironment.DPIScaleFactor - width);
            position.Y = Math.Max(position.Y, height);

            var whiteRectangle = TextureGenerator.GetPxWhite(batch.GraphicsDevice);

            batch.Draw(whiteRectangle, new Rectangle((int)position.X, (int)position.Y - height, width, height), Color.White*opacity); //note: in XNA4 colours need to be premultiplied

            //border
            batch.Draw(whiteRectangle, new Rectangle((int)position.X, (int)position.Y - height, 1, height), color * opacity);
            batch.Draw(whiteRectangle, new Rectangle((int)position.X, (int)position.Y - height, width, 1), color * opacity);
            batch.Draw(whiteRectangle, new Rectangle((int)position.X + width, (int)position.Y - height, 1, height), color * opacity);
            batch.Draw(whiteRectangle, new Rectangle((int)position.X, (int)position.Y, width, 1), color * opacity);

            position.Y -= height;
            position.Y += tooltipYInset * toolScale;

            for (int i = 0; i < wrapped.Lines.Count; i++)
            {
                var pos = position + new Vector2(tooltipXInset * toolScale, 0);
                if (style.VFont != null)
                {
                    batch.End();
                    style.VFont.Draw(batch.GraphicsDevice, wrapped.Lines[i], pos, color * opacity, scale, null);
                    batch.Begin();
                }
                else
                    batch.DrawString(style.SpriteFont, wrapped.Lines[i], pos, color * opacity, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
                position.Y += tooltipLineHeight * toolScale;
            }
        }

        private List<DialogReference> Dialogs = new List<DialogReference>();
        public UIElement TopVisibleDialog => Dialogs.LastOrDefault(x => x.Dialog.Visible)?.Dialog;
        public void AddDialog(DialogReference dialog)
        {
            //dialogContainer.Add(dialog.Dialog);
            CurrentUIScreen.Add(dialog.Dialog);
            if(dialog.Controller != null){
                dialog.Dialog.Controller = dialog.Controller;
            }
            if(dialog.LogicalParent != null){
                dialog.Dialog.LogicalParent = dialog.LogicalParent;
            }

            Dialogs.Add(dialog);
            AdjustModal();
        }

        public void RemoveDialog(DialogReference dialog)
        {
            //dialogContainer.Remove(dialog.Dialog);
            if (dialog.Dialog.Parent != null)
            {
                dialog.Dialog.Parent.Remove(dialog.Dialog);
            }
            Dialogs.Remove(dialog);
            AdjustModal();
        }

        public void RemoveDialog(UIElement dialog)
        {
            var reference = Dialogs.FirstOrDefault(x => x.Dialog == dialog);
            if (reference != null)
            {
                Dialogs.Remove(reference);
                dialog.Parent.Remove(reference.Dialog);
                AdjustModal();
            }
        }

        private UIBlocker ModalBlocker = new UIBlocker();
        private void AdjustModal()
        {
            var topMostModal = Dialogs.LastOrDefault(x => x.Modal);
            /** Remove modal blocker **/
            if (ModalBlocker.Parent != null)
            {
                ModalBlocker.Parent.Remove(ModalBlocker);
            }

            if (topMostModal == null)
            {
                
            }
            else
            {
                CurrentUIScreen.AddBefore(ModalBlocker, topMostModal.Dialog);
            }
        }


        #region IGraphicsLayer Members

        public UISpriteBatch SpriteBatch;

        public void PreDraw(GraphicsDevice device)
        {
            lock (m_ExtContainers)
            {
                if (m_ExtContainers.Count > 0)
                {
                    SpriteBatch.UIBegin(BlendState.AlphaBlend, SpriteSortMode.Immediate);
                    foreach (var ext in m_ExtContainers)
                    {
                        lock (ext)
                        {
                            if (!ext.HasUpdated) ext.Update(null);
                            ext.PreDraw(SpriteBatch);
                            ext.Draw(SpriteBatch);
                        }
                    }
                    SpriteBatch.End();
                }
            }

            SpriteBatch.UIBegin(BlendState.AlphaBlend, SpriteSortMode.Immediate);
            this.PreDraw(SpriteBatch);
            SpriteBatch.Pause();
        }

        public void Draw(GraphicsDevice device)
        {

            SpriteBatch.UIBegin(BlendState.AlphaBlend, SpriteSortMode.Immediate);
            this.Draw(SpriteBatch);
            SpriteBatch.End();
        }

        #endregion

        #region IGraphicsLayer Members

        public void Initialize(GraphicsDevice device)
        {
        }

        #endregion
    }

    public delegate void UpdateHookDelegate(UpdateState state);

    public class DialogReference
    {
        public UIElement Dialog;
        public bool Modal;
        public object Controller;
        public UIContainer LogicalParent;
    }
}
