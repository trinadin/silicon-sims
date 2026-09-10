using System;
using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Model;

namespace Simitone.Client.UI.Panels
{
    // cTutorialIcon and cWinPictureDialog::SetPositioning(1). All coordinates
    // are original logical pixels; UIScreen supplies the display scale once.
    public sealed class UIOriginalTutorial : UIContainer, IDisposable
    {
        private readonly Action ShowInfo;
        private readonly TutorialBitmap Icon = new TutorialBitmap();
        private readonly UIMouseEventRef Mouse;
        private Texture2D IconTexture;
        private VMEntity Owner;
        private UIMobileAlert Window;
        private Rectangle? LastTarget;
        private Rectangle From, To;
        private Rectangle CurrentArea, PendingTarget;
        private long Started;
        private int Transition;
        internal Func<long> Clock = () => Environment.TickCount64;
        internal Rectangle Outline { get; private set; }
        internal Rectangle Home { get; private set; }
        internal bool Animating => Transition != 0;
        internal UIMobileAlert CurrentDialog => Window;

        public UIOriginalTutorial(Action showInfo)
        {
            ShowInfo = showInfo;
            Icon.Visible = false;
            Add(Icon);
            Mouse = ListenForMouse(Rectangle.Empty, OnMouse);
        }

        public void SetOwner(VMEntity owner)
        {
            if (ReferenceEquals(owner, Owner)) return;
            Cancel();
            Owner = owner;
            LastTarget = null;
            Icon.Texture = null;
            IconTexture?.Dispose();
            IconTexture = null;
            try
            {
                IconTexture = owner?.Object.Resource.Get<BMP>(300)?.GetTexture(GameFacade.GraphicsDevice);
                if (IconTexture != null)
                {
                    UIArtProvenance.NoteOriginal(IconTexture, "tutorial-private-bmp300");
                    Icon.Texture = IconTexture;
                    int frameHeight = IconTexture.Height == 45 ? 45 : IconTexture.Height / 6;
                    Icon.SourceRectangle = new Rectangle(0, 0, IconTexture.Width, frameHeight);
                    Icon.SetSize(IconTexture.Width, frameHeight);
                }
            }
            catch { } // A missing decorative bitmap must not strand a lesson.
            if (IconTexture == null) Icon.SetSize(0, 0);
            GameResized();
            CurrentArea = Home;
            UpdateIcon();
        }

        public static Vector2 Anchor(Rectangle reference, Point size, int width, int height)
        {
            var quadrants = new[] {
                new Rectangle(0, 0, width/2, height/2),
                new Rectangle(width/2, 0, width-width/2, height/2),
                new Rectangle(0, height/2, width/2, height-height/2),
                new Rectangle(width/2, height/2, width-width/2, height-height/2)
            };
            int best = 0, area = 0;
            for (int i = 0; i < quadrants.Length; i++)
            {
                var overlap = Rectangle.Intersect(reference, quadrants[i]);
                int next = overlap.Width * overlap.Height;
                if (next > area) { best = i; area = next; }
            }
            return new Vector2((best & 1) == 0 ? reference.Left : reference.Right-size.X,
                (best & 2) == 0 ? reference.Top : reference.Bottom-size.Y);
        }

        internal static Rectangle Interpolate(Rectangle from, Rectangle to, long elapsed)
        {
            double t = Math.Clamp(elapsed / 500.0, 0, 1);
            int left = (int)(from.Left + (to.Left-from.Left)*t);
            int top = (int)(from.Top + (to.Top-from.Top)*t);
            int right = (int)(from.Right + (to.Right-from.Right)*t);
            int bottom = (int)(from.Bottom + (to.Bottom-from.Bottom)*t);
            return new Rectangle(left, top, right-left, bottom-top);
        }

        public void Open(UIMobileAlert window)
        {
            Window = window;
            window.TutorialPresentation = true;
            window.TutorialClose = () => Close(window);
            window.InterpolatedAnimation = 1;
            window.Position = Anchor(LastTarget ?? Home, new Point(window.BoxWidth, window.Height),
                UIScreen.Current.ScreenWidth, UIScreen.Current.ScreenHeight);
            window.Visible = false;
            PendingTarget = new Rectangle((int)window.X, (int)window.Y, window.BoxWidth, window.Height);
            From = CurrentArea == Home ? Home : LastTarget ?? CurrentArea;
            To = CurrentArea == Home ? PendingTarget : Home;
            Begin(CurrentArea == Home ? 1 : 3);
        }

        private void Begin(int transition)
        {
            Transition = transition;
            Started = Clock();
            Outline = From;
            UpdateIcon();
        }

        public void Close(UIMobileAlert window)
        {
            if (Window != window || Transition == 2) return;
            LastTarget = new Rectangle((int)window.X, (int)window.Y, window.BoxWidth, window.Height);
            window.Visible = false;
            From = LastTarget.Value;
            To = Home;
            window.TutorialClose = null;
            Window = null;
            UIScreen.RemoveDialog(window);
            Begin(2);
        }

        public void Forget(UIMobileAlert window)
        {
            if (Window != window) return;
            LastTarget = new Rectangle((int)window.X, (int)window.Y, window.BoxWidth, window.Height);
            Cancel();
        }

        private void Cancel()
        {
            if (Window != null) Window.TutorialClose = null;
            Window = null;
            Transition = 0;
            Outline = Rectangle.Empty;
            UpdateIcon();
        }

        private void OnMouse(UIMouseEventType type, UpdateState state)
        {
            if (type == UIMouseEventType.MouseDown && state.MouseState.LeftButton == ButtonState.Pressed &&
                !Animating && Owner != null && Window?.Visible != true)
                ShowInfo?.Invoke();
        }

        private void UpdateIcon()
        {
            Icon.Visible = Owner != null && IconTexture != null && !Animating && Window?.Visible != true;
            Mouse.Region = Owner != null && !Animating && Window?.Visible != true ? Home : Rectangle.Empty;
        }

        public override void GameResized()
        {
            base.GameResized();
            int width = IconTexture == null ? 45 : (int)Icon.Width;
            int height = IconTexture == null ? 45 : (int)Icon.Height;
            Home = new Rectangle(UIScreen.Current.ScreenWidth-1-width, 1, width, height);
            CurrentArea = Home; // Native SetIconTopRight also sets the current area.
            Icon.Position = new Vector2(Home.X, Home.Y);
            UpdateIcon();
        }

        internal void Advance()
        {
            if (!Animating) return;
            long elapsed = Clock()-Started;
            Outline = Interpolate(From, To, elapsed);
            CurrentArea = Outline;
            if (elapsed < 500) return;
            if (Transition != 1) CurrentArea = Home;
            if (Transition == 3)
            {
                From = Home;
                To = PendingTarget;
                Begin(1);
                return;
            }
            if (Transition == 1 && Window != null) Window.Visible = true;
            Transition = 0;
            Outline = Rectangle.Empty;
            UpdateIcon();
        }

        public override void Update(UpdateState state)
        {
            Advance();
            base.Update(state);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            base.Draw(batch);
            if (!Animating && (Owner == null || IconTexture != null || Window?.Visible == true)) return;
            var rect = Animating ? Outline : Home;
            for (int i = 0; i < 3; i++)
            {
                if (rect.Width <= 0 || rect.Height <= 0) break;
                var color = i == 1 ? Color.White : Color.Black;
                DrawEdge(batch, new Rectangle(rect.Left, rect.Top, rect.Width, 1), color);
                DrawEdge(batch, new Rectangle(rect.Left, rect.Bottom-1, rect.Width, 1), color);
                DrawEdge(batch, new Rectangle(rect.Left, rect.Top, 1, rect.Height), color);
                DrawEdge(batch, new Rectangle(rect.Right-1, rect.Top, 1, rect.Height), color);
                rect.Inflate(-1, -1);
            }
        }

        private void DrawEdge(UISpriteBatch batch, Rectangle rect, Color color)
        {
            var pixel = FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
            var topLeft = FlooredLocalPoint(new Vector2(rect.Left, rect.Top));
            var bottomRight = FlooredLocalPoint(new Vector2(rect.Right, rect.Bottom));
            batch.Draw(pixel, new Rectangle((int)topLeft.X, (int)topLeft.Y,
                (int)(bottomRight.X-topLeft.X), (int)(bottomRight.Y-topLeft.Y)), color);
        }

        public void Dispose()
        {
            Cancel();
            Icon.Texture = null;
            IconTexture?.Dispose();
            IconTexture = null;
        }

        private sealed class TutorialBitmap : UIImage
        {
            public override void Draw(UISpriteBatch batch)
            {
                if (Visible && Texture != null)
                    DrawLocalTexture(batch, Texture, SourceRectangle, Vector2.Zero, Vector2.One);
            }
        }
    }
}
