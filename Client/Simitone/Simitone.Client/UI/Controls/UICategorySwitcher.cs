using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common.Utils;
using FSO.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSO.Common.Rendering.Framework.Model;

namespace Simitone.Client.UI.Controls
{
    public class UICategorySwitcher : UIContainer
    {
        public UICatButton MainButton;
        public UIDiagonalStripe Stripe;
        public UIVertGrad Grad;
        public event Action<int> OnCategorySelect;
        public event Action OnOpen;
        public int ActiveCategory;
        public List<UICategory> Categories;
        public List<UIStencilButton> CatSwitchButtons = new List<UIStencilButton>();

        private float _ce;
        public float CategoryExpand
        {
            get
            {
                return _ce;
            }
            set
            {
                var scrHeight = GameFacade.Screens.CurrentUIScreen.ScreenHeight;
                var size = (scrHeight - (128 + 15));
                if (Stripe != null)
                {
                    Stripe.Y = (-value) * size;
                    Stripe.BodySize = new Point(85, (int)(value*size));
                }

                var i = 0;
                foreach (var btn in CatSwitchButtons)
                {
                    btn.Y = i++ * -70 * value - 75;
                    btn.Opacity = value;
                    btn.Visible = value > 0;
                }

                if (Grad != null)
                {
                    Grad.Visible = value > 0;
                    Grad.GSize = new Vector2(size, 75*value);
                }
                if (Stripe != null) Stripe.Visible = value > 0;
                _ce = value;
            }
        }

        // R142: desktop runs the original category plaques bare — the diagonal
        // stripe and the rotated title gradient are the mobile column's chrome and
        // have no original counterpart (the engine's category column is plain
        // plaques floating over the game view).
        public readonly bool OriginalChrome = !FSO.Common.FSOEnvironment.SoftwareKeyboard;

        public UICategorySwitcher()
        {
            if (!OriginalChrome)
            {
                Stripe = new UIDiagonalStripe(new Point(), UIDiagonalStripeSide.UP, UIStyle.Current.Bg);
                Add(Stripe);

                Grad = new UIVertGrad();
                Grad.Position = new Vector2(43, 0);
                Grad.Visible = false;
                Add(Grad);
            }

            MainButton = new UICatButton(TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice));
            MainButton.Position = new Microsoft.Xna.Framework.Vector2(10, 31);
            MainButton.OnButtonClick += (b) => { Open(); };
            MainButton.Selected = true;
            // R142: desktop never draws the modern round plaque behind original art.
            MainButton.OriginalStyle = OriginalChrome;
            Add(MainButton);

            CategoryExpand = CategoryExpand;
        }

        private Texture2D CategoryTexture(UICategory catG)
        {
            if (catG.OriginalName != null)
            {
                var iffTx = Simitone.Client.UI.Model.UIOriginal.EnsureResolved(catG.OriginalName);
                if (iffTx != null)
                {
                    try { return iffTx.Get(GameFacade.GraphicsDevice); }
                    catch { }
                }
            }
            return Content.Get().CustomUI.Get(catG.IconName).Get(GameFacade.GraphicsDevice);
        }

        public void Select(int cat)
        {
            foreach (var item in CatSwitchButtons)
            {
                Remove(item);
            }
            CatSwitchButtons.Clear();
            foreach (var catG in Categories)
            {
                var id = catG.ID;
                if (catG.ID == cat)
                {
                    MainButton.Texture = CategoryTexture(catG);
                    MainButton.Tooltip = catG.Caption;
                }
                // R141: the ACTIVE category must NOT get a stencil button in the
                // expanding column — the >=720 clause double-rendered it stacked
                // directly over the main plaque on every desktop screen.
                if (catG.ID != cat)
                {
                    var btn = new UIStencilButton(CategoryTexture(catG));
                    btn.Shadow = true;
                    btn.X = 10;
                    btn.Visible = false;
                    btn.Tooltip = catG.Caption;
                    btn.OnButtonClick += (b) => { Select(id); };
                    Add(btn);
                    CatSwitchButtons.Add(btn);
                }
            }
            if (CategoryExpand > 0)
            {
                CategoryExpand = CategoryExpand;
                Close();
            }
            OnCategorySelect?.Invoke(cat);
            ActiveCategory = cat;
        }

        public void InitCategories(List<UICategory> cats)
        {
            Categories = cats;
            Select(Categories[0].ID);
        }

        public void Open()
        {
            if (CategoryExpand > 0)
            {
                Close(); return;
            }
            OnOpen?.Invoke();
            GameFacade.Screens.Tween.To(this, 0.3f, new Dictionary<string, float>() { { "CategoryExpand", 1f } }, TweenQuad.EaseOut);
        }

        public void Close()
        {
            GameFacade.Screens.Tween.To(this, 0.3f, new Dictionary<string, float>() { { "CategoryExpand", 0f } }, TweenQuad.EaseOut);
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
        }
    }

    public class UICategory
    {
        public int ID;
        public string IconName;
        public string OriginalName;
        // R122: the original sort-button tooltip (STR# 150's own label calls these
        // strings sort TIPS — they are the button captions the original showed on
        // hover, not on-screen labels; the plaque art carries the visible text).
        public string Caption;
    }

    public class UIVertGrad : UIElement
    {
        public Texture2D Grad;
        public Vector2 GSize;

        public UIVertGrad() : base()
        {
            Grad = Content.Get().CustomUI.Get("dialog_title_grad.png").Get(GameFacade.GraphicsDevice);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            DrawLocalTexture(batch, Grad, null, new Vector2(0, 0), new Vector2(GSize.X / Grad.Width, GSize.Y), Color.White, (float)Math.PI / -2, new Vector2(0, 0.5f));
        }
    }
}
