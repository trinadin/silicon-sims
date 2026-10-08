using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Client.UI.Framework;
using FSO.Client.Utils;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.UI.Panels.CAS
{
    /// <summary>
    /// R143: base for the ORIGINAL 800x600 create-a-family flow screens. The
    /// original screens are fixed 800x600 compositions (kDesignCharBkg /
    /// kDesignFamilyBkg / kPickFamilyBkg in UIGraphics.far, BI_RLE8); every
    /// child is anchored in that 800x600 space and the whole panel letterbox-
    /// centers in the desktop window. All art mounts through the IFF-first
    /// far resolver (BmpRLE8 decodes the RLE family since R143).
    /// </summary>
    public class UICASOriginalScreen : UIContainer
    {
        public const int ORIG_W = 800;
        public const int ORIG_H = 600;

        public UIImage Background;

        /// <summary>
        /// R143 engine quirk (cas-layout-law.md §2): the Create-A-Character and
        /// Create-A-Family screens blit their 800x600 art at (ox,oy) =
        /// max(0,(artW-winW)/2) - i.e. TOP-LEFT once the window is >= 800 wide,
        /// leaving the margin unpainted. Pick-A-Family instead CENTERS
        /// (nbhd-layout-law.md §1: offX = max(0,(winW-800))/2). The engine
        /// does both, so we reproduce both.
        /// </summary>
        public bool CenterArtboard = true;

        // offset of the 800x600 space inside the window (desktop)
        public Vector2 Origin
        {
            get { return Position; }
        }

        public UICASOriginalScreen(string backgroundMember, bool center = true)
        {
            var tx = UIOriginal.ResolveOrPng(backgroundMember, null, null);
            Background = tx != null ? new UIImage(tx) : new UIImage();
            Add(Background);
            CenterArtboard = center;
            CenterInWindow();
        }

        public void CenterInWindow()
        {
            var sw = UIScreen.Current != null ? UIScreen.Current.ScreenWidth : ORIG_W;
            var sh = UIScreen.Current != null ? UIScreen.Current.ScreenHeight : ORIG_H;
            if (CenterArtboard)
            {
                X = Math.Max(0, (sw - ORIG_W) / 2);
                Y = Math.Max(0, (sh - ORIG_H) / 2);
            }
            else
            {
                X = 0;
                Y = 0;
            }
        }

        public override void GameResized()
        {
            base.GameResized();
            CenterInWindow();
        }

        /// <summary>
        /// Punches a fully transparent hole (the original view-control windows
        /// where the 3D head/body show through the painted background) by
        /// clearing premultiplied color in a rectangle of the background texture.
        /// AUD-17 F-4: works on a CLONE — the input is the process-wide cached
        /// CreateACharBack instance, and mutating it baked a permanent hole
        /// into every other consumer (masked today only because the vita
        /// preview's static backdrop captures once per process).
        /// </summary>
        public static Texture2D CutHole(Texture2D tex, Rectangle rect)
        {
            var clone = new Texture2D(tex.GraphicsDevice, tex.Width, tex.Height);
            var data = new Microsoft.Xna.Framework.Color[tex.Width * tex.Height];
            tex.GetData(data);
            for (int y = rect.Y; y < rect.Y + rect.Height && y < tex.Height; y++)
                for (int x = rect.X; x < rect.X + rect.Width && x < tex.Width; x++)
                    data[y * tex.Width + x] = Color.Transparent;
            clone.SetData(data);
            return clone;
        }
    }

    /// <summary>
    /// R143: a cTSWinBtn on one of the original horizontal multi-state sheets
    /// (SetImage(cols,1) law, r143/nbhd-layout-law.md). Frame 0..3 of the
    /// sheet is drawn for the up/down/hover/disabled states - the same
    /// disclosed order convention as UIOriginal.Frame since R122.
    /// </summary>
    public class UIOriginalSheetButton : UIButton
    {
        public readonly string Member;
        public readonly int StateWidth;
        private Texture2D[] States;

        private static Texture2D ResolveSheet(string member)
        {
            return UIOriginal.EnsureResolved(member)?.Get(GameFacade.GraphicsDevice)
                ?? FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
        }

        public UIOriginalSheetButton(string member, int stateWidth)
            : base(ResolveSheet(member))
        {
            // Native SetImage(sheet,4,1) establishes the full per-state
            // rectangle for both rendering and input. A 1px placeholder left
            // every desktop CAS sheet control with a 1x1 mouse region.
            ImageStates = Texture.Width >= 4 ? 4 : 1;
            Member = member;
            StateWidth = stateWidth;
        }

        private Texture2D FrameTex(int i)
        {
            if (States == null)
            {
                var tx = UIOriginal.EnsureResolved(Member)?.Get(GameFacade.GraphicsDevice);
                if (tx == null) { States = new Texture2D[1]; return null; }
                var n = tx.Width / StateWidth;
                States = new Texture2D[n];
                for (int k = 0; k < n; k++) States[k] = UIOriginal.Rect(Member, k * StateWidth, 0, StateWidth, tx.Height);
            }
            if (i >= States.Length) i = 0;
            return States[i];
        }

        public override void Draw(UISpriteBatch SBatch)
        {
            if (!Visible) return;
            var frame = Disabled ? 3 : Selected || IsDown ? 1 : Hovered ? 2 : 0;
            var tex = FrameTex(Math.Min(frame, 3));
            if (tex != null) DrawLocalTexture(SBatch, tex, null, Vector2.Zero, Vector2.One);
        }
    }

    /// <summary>
    /// R143: one family card of the original Pick-A-Family list
    /// (cPickFamilyItem law, r143/nbhd-layout-law.md §2.6): 690x68, the
    /// FamilyPickItemBkg sheet is THREE VERTICAL states (rows=3:
    /// normal/hilite/selected); name (20,0), funds (526,21), member count
    /// (620,21), member portraits 45x68 at (110+i*48, 14) wrapping at 494,
    /// smiley (657,19); text cyan (0,255,255) selected / white hover /
    /// (195,205,205) normal (InitSimsColors 0x25d600).
    /// </summary>
    public class UIOriginalFamilyCard : UIContainer
    {
        public const int CARD_W = 690, CARD_H = 68;

        public readonly FAMI Family;
        private UIImage Art;
        private Simitone.Client.UI.Controls.UIOriginalText NameText;
        private Simitone.Client.UI.Controls.UIOriginalText FundsText;
        private Simitone.Client.UI.Controls.UIOriginalText CountText;
        public List<UIOriginalPersonPortrait> Members = new List<UIOriginalPersonPortrait>();
        public UIImage Smiley;

        public bool CardSelected;
        private bool Hover;
        public Action<UIOriginalFamilyCard> OnCardClick;

        private static readonly Color NormalColor = new Color(195, 205, 205);
        private static readonly Color HoverColor = new Color(255, 255, 255);
        private static readonly Color SelectedColor = new Color(0, 255, 255);

        public UIOriginalFamilyCard(FAMI family, VM vm)
        {
            Family = family;
            var state = UIOriginal.Rect("nbhd\\FamilyPickItemBkg.bmp", 0, 0, CARD_W, CARD_H);
            Art = new UIImage(state);
            Add(Art);

            var fams = family.ChunkParent.Get<FAMs>(family.ChunkID);
            var font = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadCaption(GameFacade.GraphicsDevice);
            NameText = new Simitone.Client.UI.Controls.UIOriginalText(fams?.GetString(0) ?? "", font) { X = 20, Y = 0, Color = NormalColor };
            FundsText = new Simitone.Client.UI.Controls.UIOriginalText("$" + Math.Abs(family.Budget), font) { X = 526, Y = 21, Color = NormalColor };
            CountText = new Simitone.Client.UI.Controls.UIOriginalText(family.FamilyGUIDs.Length.ToString(), font) { X = 620, Y = 21, Color = NormalColor };
            Add(NameText); Add(FundsText); Add(CountText);

            if (vm != null)
            {
                int i = 0;
                foreach (var guid in family.FamilyGUIDs)
                {
                    if (i >= 8) break;
                    // AUD-17 F-2: CreateObjectInstance returns null when the
                    // person export behind the GUID is missing (user-deleted
                    // Export/ file, neighborhood copied from another install)
                    // — the old deref crashed card construction and took the
                    // whole CAS entry screen down. Skip the portrait and log.
                    var group = vm.Context.CreateObjectInstance(guid, FSO.LotView.Model.LotTilePos.OUT_OF_WORLD, FSO.LotView.Model.Direction.NORTH);
                    if (group == null)
                    {
                        GameLog.Write("cas: family " + family.ChunkID +
                            " member GUID 0x" + guid.ToString("X") + " unresolvable — portrait skipped");
                        continue;
                    }
                    var sim = group.BaseObject;
                    try
                    {
                        sim.Tick();
                        var portrait = UIOriginalPersonPortrait.Create((VMAvatar)sim, true);
                        portrait.Position = new Vector2(110 + i * 48, 14);
                        // Native child is 45x68, clipped by its 68px-high family
                        // parent. Route clicks through the card and bound hover to
                        // that intersection so it cannot spill into the next row.
                        portrait.AddUpdateHook(state => portrait.Column = CardSelected ||
                            new Rectangle(0, 0, 45, CARD_H - 14).Contains(portrait.GetMousePosition(state.MouseState)) ? 2 : 0);
                        Members.Add(portrait);
                        Add(portrait);
                    }
                    finally { sim.Delete(true, vm.Context); }
                    i++;
                }
            }

            var smileyTx = UIOriginal.EnsureResolved("nbhd\\Smiley.bmp")?.Get(GameFacade.GraphicsDevice);
            if (smileyTx != null)
            {
                Smiley = new UIImage(smileyTx) { X = 657, Y = 19 };
                Smiley.Visible = false;
                Add(Smiley);
            }

            ListenForMouse(new Rectangle(0, 0, CARD_W, CARD_H), (type, state) =>
            {
                if (type == UIMouseEventType.MouseOver) Hover = true;
                else if (type == UIMouseEventType.MouseOut) Hover = false;
                else if (type == UIMouseEventType.MouseDown) OnCardClick?.Invoke(this);
            });
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            var row = CardSelected ? 2 : (Hover ? 1 : 0);
            var state = UIOriginal.Rect("nbhd\\FamilyPickItemBkg.bmp", 0, row * CARD_H, CARD_W, CARD_H);
            if (state != null) Art.Texture = state;
            var col = CardSelected ? SelectedColor : (Hover ? HoverColor : NormalColor);
            NameText.Color = FundsText.Color = CountText.Color = col;
            base.Draw(batch);
        }
    }

    /// <summary>
    /// R143: the ORIGINAL Pick-A-Family screen (cWinPickFamily law,
    /// r143/nbhd-layout-law.md §2): opaque 800x600 kPickFamilyBkg, family
    /// list of 6 visible cards at (41,76) (68px rows), PAF buttons Add
    /// 92x62 (322,530) / Delete 92x62 (414,530) / MoveIn 164x62 (506,530)
    /// (4-state sheets), the text-captioned Cancel system button centered in
    /// the provisional (100,529,300,591) area, and scroll arrow hotspots
    /// 26x23 at (586,42)/(662,42). PAFCancel 5104 is dead art; the executable
    /// builds button 3 from NbhdTileBtn and STR#128[5]/[15].
    /// </summary>
    public class UIOriginalPickFamily : UICASOriginalScreen
    {
        public const int LIST_X = 41, LIST_Y = 76, VISIBLE = 6;

        public List<UIOriginalFamilyCard> Cards = new List<UIOriginalFamilyCard>();
        public UIOriginalSheetButton AddButton;
        public UIOriginalSheetButton DeleteButton;
        public UIOriginalSheetButton MoveInButton;
        public UIOriginalSystemButton CancelButton;
        public Simitone.Client.UI.Controls.UIOriginalText TitleText;

        public int TopRow;
        private int Selection = -1;
        public List<FAMI> Families = new List<FAMI>();
        private VM vm;

        public event Action OnNewFamily;
        public event Action OnDeleteFamily;
        public event Action OnCancel;

        public UIOriginalPickFamily() : base("nbhd\\PickBkg.bmp")
        {
            var font = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadCaption(GameFacade.GraphicsDevice);
            // R116 string provenance kept: 128[8] 'SELECT A FAMILY' top title.
            TitleText = new Simitone.Client.UI.Controls.UIOriginalText(GameFacade.Strings.GetString("128", "8"), font) { X = 0, Y = 12, Color = new Color(195, 205, 205) };
            Add(TitleText);

            AddButton = new UIOriginalSheetButton("nbhd\\PAFAddFamily.bmp", 92) { Position = new Vector2(322, 530), Tooltip = GameFacade.Strings.GetString("128", "2") };
            AddButton.OnButtonClick += (b) => OnNewFamily?.Invoke();
            Add(AddButton);
            DeleteButton = new UIOriginalSheetButton("nbhd\\PAFDeleteFamily.bmp", 92) { Position = new Vector2(414, 530), Tooltip = GameFacade.Strings.GetString("128", "4") };
            DeleteButton.OnButtonClick += (b) => OnDeleteFamily?.Invoke();
            Add(DeleteButton);
            MoveInButton = new UIOriginalSheetButton("nbhd\\PAFMoveIn.bmp", 164) { Position = new Vector2(506, 530), Tooltip = GameFacade.Strings.GetString("128", "3") };
            Add(MoveInButton);

            CancelButton = new UIOriginalSystemButton(GameFacade.Strings.GetString("128", "5"), font)
            {
                Position = new Vector2(100, 529),
                Tooltip = GameFacade.Strings.GetString("128", "15")
            };
            CancelButton.CenterCaption();
            CancelButton.OnButtonClick += (b) => OnCancel?.Invoke();
            Add(CancelButton);

            // scroll arrow hotspots (26x23 transparent click zones)
            ArrowZone(586, 42, () => Scroll(-1));
            ArrowZone(662, 42, () => Scroll(1));
        }

        private UIElement ArrowZone(int x, int y, Action act)
        {
            var el = new UIContainer();
            el.Position = new Vector2(x, y);
            el.ListenForMouse(new Rectangle(0, 0, 26, 23), (type, state) =>
            {
                if (type == UIMouseEventType.MouseDown) act();
            });
            Add(el);
            return el;
        }

        public void Scroll(int dir)
        {
            var max = Math.Max(0, Families.Count - VISIBLE);
            TopRow = Math.Max(0, Math.Min(max, TopRow + dir));
            Relayout();
        }

        public void UpdateFamilies(List<FAMI> families, VM vm)
        {
            this.vm = vm;
            Families = families;
            foreach (var card in Cards) Remove(card);
            Cards.Clear();
            foreach (var fam in families)
            {
                var card = new UIOriginalFamilyCard(fam, vm);
                card.OnCardClick = SelectCard;
                Cards.Add(card);
                Add(card);
            }
            Selection = -1;
            TopRow = 0;
            Relayout();
        }

        private void SelectCard(UIOriginalFamilyCard card)
        {
            SetSelection(Cards.IndexOf(card));
        }

        public void SetSelection(int index)
        {
            Selection = index >= 0 && index < Cards.Count ? index : -1;
            for (int i = 0; i < Cards.Count; i++) Cards[i].CardSelected = (i == Selection);
            Relayout();
        }

        public int GetSelection() { return Selection; }

        private void Relayout()
        {
            for (int i = 0; i < Cards.Count; i++)
            {
                var vis = i >= TopRow && i < TopRow + VISIBLE;
                Cards[i].Visible = vis;
                Cards[i].Position = new Vector2(LIST_X, LIST_Y + (i - TopRow) * UIOriginalFamilyCard.CARD_H);
            }
            DeleteButton.Disabled = Selection == -1;
            MoveInButton.Disabled = Selection == -1;
        }
    }

    /// <summary>
    /// R183: the original cTSSystemButton used for CAS/Family Done+Cancel and
    /// PAF Cancel. The ctor splits NbhdTileBtn into four 91x62 states. ImageBlt
    /// preserves 45px left/right caps and stretches only the one-pixel center.
    /// SetArea first installs a 200x62 area, then SetCaption selects font[12]
    /// and recenters a final textWidth+70 control inside that provisional area.
    /// Commands fire only
    /// on mouse-up-inside; leaving while captured disarms and re-entering rearms.
    /// </summary>
    public class UIOriginalSystemButton : UIContainer
    {
        public const int StateCount = 4;
        public const int ProvisionalWidth = 200;
        public const int ButtonHeight = 62;
        public const int CapWidth = 45;
        public const int CenterSourceWidth = 1;
        public const int CaptionX = 35;
        public const int CaptionMargins = 70;

        public Simitone.Client.UI.Controls.UIOriginalText Caption;
        public bool Hover;
        public bool Pressed;
        public bool Captured { get; private set; }
        public int StateWidth { get; private set; }
        public int FontIndex { get; private set; }
        public int ControlWidth { get; private set; }
        public int CaptionBaseY { get; private set; }
        public int CurrentArtFrame => Disabled ? 3 : Pressed ? 1 : Hover ? 2 : 0;
        public Texture2D CurrentArtTexture => ArtStates[CurrentArtFrame];

        private bool _Disabled;
        public bool Disabled
        {
            get { return _Disabled; }
            set
            {
                _Disabled = value;
                if (value)
                {
                    Captured = false;
                    Pressed = false;
                    Hover = false;
                }
                ApplyVisualState();
            }
        }

        public override Vector2 Size
        {
            get { return new Vector2(ControlWidth, ButtonHeight); }
            set { }
        }

        public override Rectangle GetBounds()
        {
            // UIContainer's inherited bounds are empty. The original control
            // exposes its post-caption area for tooltips as well as hit tests.
            return new Rectangle(0, 0, ControlWidth, ButtonHeight);
        }

        private readonly Texture2D[] ArtStates = new Texture2D[StateCount];
        private bool CaptionCentered;

        public event Action<UIOriginalSystemButton> OnButtonClick;

        public Texture2D GetArtState(int frame)
        {
            return frame >= 0 && frame < ArtStates.Length ? ArtStates[frame] : null;
        }

        public UIOriginalSystemButton(string caption, Simitone.Client.UI.Controls.OriginalGlyphFont font)
        {
            var tx = UIOriginal.EnsureResolved("nbhd\\NbhdTileBtn.BMP")?.Get(GameFacade.GraphicsDevice);
            if (tx != null)
            {
                // kNghDefaultBtn is a horizontal four-state cTSWinBtn sheet
                // (364x62 = 4 x 91x62). ImageBlt does not uniformly scale a
                // state: it preserves the 45px ends and stretches x=45 only.
                StateWidth = (tx.Width % StateCount == 0) ? tx.Width / StateCount : tx.Width;
                for (int i = 0; i < StateCount; i++)
                {
                    ArtStates[i] = StateWidth == tx.Width
                        ? tx
                        : UIOriginal.Rect("nbhd\\NbhdTileBtn.BMP", i * StateWidth, 0, StateWidth, tx.Height);
                }
            }

            FontIndex = 12;
            var nativeFont = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadByIndex(FontIndex, GameFacade.GraphicsDevice);
            nativeFont = nativeFont ?? font;
            var textWidth = nativeFont?.Measure(caption ?? "") ?? 130;
            ControlWidth = textWidth + CaptionMargins;
            CaptionBaseY = nativeFont == null ? 0 : (ButtonHeight - nativeFont.LineHeight) / 2;
            Caption = new Simitone.Client.UI.Controls.UIOriginalText(caption, nativeFont);
            Add(Caption);
            ListenForMouse(new Rectangle(0, 0, ControlWidth, ButtonHeight), HandleMouseEvent);
            UIUtils.GiveTooltip(this);
            ApplyVisualState();
        }

        public void HandleMouseEvent(UIMouseEventType type, UpdateState state)
        {
            if (type == UIMouseEventType.MouseOut)
            {
                Hover = false;
                Pressed = false;
                ApplyVisualState();
                return;
            }
            if (Disabled) return;
            if (type == UIMouseEventType.MouseOver)
            {
                Hover = true;
                if (Captured) Pressed = true;
            }
            else if (type == UIMouseEventType.MouseDown)
            {
                Hover = true;
                Captured = true;
                Pressed = true;
            }
            else if (type == UIMouseEventType.MouseUp)
            {
                var click = Captured && Hover && Pressed;
                Captured = false;
                Pressed = false;
                if (click) OnButtonClick?.Invoke(this);
            }
            ApplyVisualState();
        }

        public void CenterCaption()
        {
            // Native SetCaption recenters the final odd-width control inside
            // the already centered 200px SetArea using integer division.
            if (!CaptionCentered)
            {
                X += (ProvisionalWidth - ControlWidth) / 2;
                CaptionCentered = true;
            }
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            if (Caption == null) return;
            Caption.X = CaptionX + (Pressed ? 2 : 0);
            Caption.Y = CaptionBaseY + (Pressed ? 2 : 0);
            Caption.Color = Disabled ? new Color(0x40, 0x5D, 0x5F)
                : Pressed ? new Color(0x00, 0xFF, 0xFF)
                : Hover ? Color.White
                : new Color(0xC3, 0xCD, 0xCD);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            ApplyVisualState();
            var tex = CurrentArtTexture;
            if (tex != null)
            {
                DrawLocalTexture(batch, tex, new Rectangle(0, 0, CapWidth, ButtonHeight), Vector2.Zero, Vector2.One);
                var centerWidth = Math.Max(1, ControlWidth - CapWidth * 2);
                DrawLocalTexture(batch, tex,
                    new Rectangle(CapWidth, 0, CenterSourceWidth, ButtonHeight),
                    new Vector2(CapWidth, 0), new Vector2(centerWidth, 1));
                DrawLocalTexture(batch, tex,
                    new Rectangle(CapWidth + CenterSourceWidth, 0, CapWidth, ButtonHeight),
                    new Vector2(ControlWidth - CapWidth, 0), Vector2.One);
            }
            base.Draw(batch);
        }
    }

    /// <summary>
    /// R143: the ORIGINAL Create-A-Character screen, engine law
    /// r143/cas-layout-law.md §3. Every anchor below is a decoded immediate:
    /// the 13 buttons (SetImage(4,1) sheets), the 59x25 personality LEDs with
    /// lit width = value*6 (click zones at value*5 - engine quirk), the pool-25
    /// remaining-points bar at (117,342) (width = pool*6 of 149), the zodiac
    /// readout on the (129,290)-(249,310) points button (nearest archetype,
    /// engine table at data 0x4c144 decoded this round), the 100x220 Vita
    /// viewport at (618,145), name (275,52)-(530,77) capacity 25, bio
    /// (22,425)-(783,521) capacity 2048, white text (InitSimsColors 0x92984).
    /// </summary>
    public class UIOriginalDesignChar : UICASOriginalScreen
    {
        // anchor law (screen-local == art coords at ox=oy=0)
        public const int LED_X = 228, LED_Y0 = 131, LED_PITCH = 32;
        // engine quirk (cas-layout-law.md §3.4): LEDs DRAW at value*6 px but
        // the click zones split at value*(59/10)=5 px.
        public const int LED_DRAW_PITCH = 6, LED_CLICK_PITCH = 5;
        public const int POINTS_BAR_X = 117, POINTS_BAR_Y = 342, POINTS_POOL = 25;
        public UIOriginalVitaPreview VitaSurface;
        public static readonly Rectangle VITA_RECT = new Rectangle(618, 145, 100, 220);
        public static readonly Rectangle POINTS_BTN_RECT = new Rectangle(129, 290, 120, 20);

        public UIOriginalSheetButton AdultBtn, ChildBtn, FemaleBtn, MaleBtn, DarkBtn, MediumBtn, LightBtn;
        public UIOriginalSheetButton BodyPrevBtn, BodyNextBtn, HeadPrevBtn, HeadNextBtn;
        public UIOriginalSystemButton DoneBtn, CancelBtn;

        public UIOriginalPersonNameBox NameBox;
        public Simitone.Client.UI.Controls.UIOriginalTextEdit BioEdit;

        public Simitone.Client.UI.Controls.UIOriginalText TitleText, PersonText, NameLabelText, BioLabelText;
        public Simitone.Client.UI.Controls.UIOriginalText ZodiacText;
        public UIElement ZodiacButton;

        // personality: value per trait 0..10, pool counts DOWN from 25
        public int[] Values = new int[5];
        public int Pool = 25;
        public UIImage[] LedStrips = new UIImage[5];
        public Simitone.Client.UI.Controls.UIOriginalText[] TraitLabels = new Simitone.Client.UI.Controls.UIOriginalText[5];
        public UIImage PointsBar;

        public string AType = "ma";
        public string SkinType = "lgt";

        // engine zodiac archetype table (data 0x4c144, Neat/Outgoing/Active/Playful/Nice)
        public static readonly float[][] ZodiacArchetypes = new float[][]
        {
            new float[] { 5, 4, 8, 5, 3 },   // Aries
            new float[] { 7, 3, 3, 4, 8 },   // Taurus
            new float[] { 3, 6, 4, 6, 6 },   // Gemini
            new float[] { 10, 3, 4, 4, 4 },  // Cancer
            new float[] { 2, 5, 3, 9, 6 },   // Leo
            new float[] { 8, 7, 6, 2, 2 },   // Virgo
            new float[] { 5, 3, 3, 6, 8 },   // Libra
            new float[] { 3, 4, 7, 2, 9 },   // Scorpio
            new float[] { 4, 5, 8, 7, 1 },   // Sagittarius
            new float[] { 4, 6, 7, 4, 4 },   // Capricorn
            new float[] { 3, 7, 3, 5, 7 },   // Aquarius
            new float[] { 0, 0, 0, 0, 0 },   // Pisces (zero row: unassigned sims)
        };
        // R159: the names now read from the ORIGINAL UIText.iff STR# 164
        // 'signs' ([0..11], the corpus the r143 decode identified); the
        // literals stay only as a load-failure fallback.
        public static string ZodiacName(int i)
        {
            var t = FSO.Client.GameFacade.Strings.GetString("164", i.ToString());
            return (!string.IsNullOrEmpty(t) && t != "164:" + i) ? t
                : new string[] { "Aries", "Taurus", "Gemini", "Cancer", "Leo", "Virgo", "Libra", "Scorpio", "Sagittarius", "Capricorn", "Aquarius", "Pisces" }[i];
        }

        public event Action<string, string> OnCollectionChange;
        public event Action<int> OnCycleHead;
        public event Action<int> OnCycleBody;
        public event Action OnDone;
        public event Action OnCancel;

        public UIOriginalDesignChar() : base("nbhd\\CreateACharBack.BMP", false)
        {
            var font = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadCaption(GameFacade.GraphicsDevice);
            var bfont = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadByIndex(16, GameFacade.GraphicsDevice);

            // the 3D head/body viewport hole (cWinVitaBtn rect)
            if (Background.Texture != null)
            {
                var bg = Background.Texture;
                if (bg.Width == ORIG_W && bg.Height == ORIG_H)
                {
                    VitaSurface = new UIOriginalVitaPreview(bg, VITA_RECT);
                    Add(VitaSurface);
                    // AUD-17 F-4: CutHole now returns a CLONE (bg is the
                    // process-wide cached CreateACharBack instance — the old
                    // in-place cut baked a permanent hole into it). The vita
                    // preview keeps the UNCUT original for its own backdrop.
                    Background.Texture = CutHole(bg, VITA_RECT);
                }
            }

            AdultBtn = Sheet("nbhd\\DesignCharAdultBtn.bmp", 57, 497, 101, "130:0");
            ChildBtn = Sheet("nbhd\\DesignCharChildBtn.bmp", 92, 406, 111, "130:1");
            FemaleBtn = Sheet("nbhd\\DesignCharFemaleBtn.bmp", 57, 499, 274, "130:2");
            MaleBtn = Sheet("nbhd\\DesignCharMaleBtn.bmp", 98, 400, 274, "130:3");
            DarkBtn = Sheet("nbhd\\DesignCharDarkBtn.bmp", 57, 499, 202, "130:4");
            MediumBtn = Sheet("nbhd\\DesignCharMediumBtn.bmp", 46, 452, 202, "130:5");
            LightBtn = Sheet("nbhd\\DesignCharLightBtn.bmp", 55, 396, 202, "130:6");
            BodyPrevBtn = Sheet("nbhd\\DesignCharSkinsLeftBtn.bmp", 18, 580, 291, "130:7");
            BodyNextBtn = Sheet("nbhd\\DesignCharSkinsRightBtn.bmp", 18, 729, 291, "130:8");
            HeadPrevBtn = Sheet("nbhd\\DesignCharSkinsLeftBtn.bmp", 18, 597, 160, "130:9");
            HeadNextBtn = Sheet("nbhd\\DesignCharSkinsRightBtn.bmp", 18, 714, 160, "130:10");

            AdultBtn.OnButtonClick += (b) => SetAge(true);
            ChildBtn.OnButtonClick += (b) => SetAge(false);
            MaleBtn.OnButtonClick += (b) => SetGender('m');
            FemaleBtn.OnButtonClick += (b) => SetGender('f');
            LightBtn.OnButtonClick += (b) => SetSkin("lgt");
            MediumBtn.OnButtonClick += (b) => SetSkin("med");
            DarkBtn.OnButtonClick += (b) => SetSkin("drk");
            BodyPrevBtn.OnButtonClick += (b) => { OnCycleBody?.Invoke(-1); FSO.HIT.HITVM.Get().PlaySoundEvent("UI_CAC_CycleParts"); };
            BodyNextBtn.OnButtonClick += (b) => { OnCycleBody?.Invoke(1); FSO.HIT.HITVM.Get().PlaySoundEvent("UI_CAC_CycleParts"); };
            HeadPrevBtn.OnButtonClick += (b) => { OnCycleHead?.Invoke(-1); FSO.HIT.HITVM.Get().PlaySoundEvent("UI_CAC_Cyclehead"); };
            HeadNextBtn.OnButtonClick += (b) => { OnCycleHead?.Invoke(1); FSO.HIT.HITVM.Get().PlaySoundEvent("UI_CAC_Cyclehead"); };

            DoneBtn = new UIOriginalSystemButton(GameFacade.Strings.GetString("130", "11"), font) { Position = new Vector2(11, 529) };
            DoneBtn.CenterCaption();
            DoneBtn.OnButtonClick += (b) => OnDone?.Invoke();
            Add(DoneBtn);
            CancelBtn = new UIOriginalSystemButton(GameFacade.Strings.GetString("130", "12"), font) { Position = new Vector2(300, 529) };
            CancelBtn.CenterCaption();
            CancelBtn.OnButtonClick += (b) => OnCancel?.Invoke();
            Add(CancelBtn);

            // labels (engine: title strip y+6; PERSONALITY centered at x=198 y=114;
            // 'Enter First Name:' right-anchored at x=255 y=52; 'Bio for %s' above bio)
            TitleText = new Simitone.Client.UI.Controls.UIOriginalText(GameFacade.Strings.GetString("130", "15"), bfont) { Y = 6, Color = Color.White };
            if (bfont != null) TitleText.X = (ORIG_W - bfont.Measure(TitleText.Text)) / 2;
            Add(TitleText);
            var labelFont = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadByIndex(12, GameFacade.GraphicsDevice);
            var traitFont = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadByIndex(
                (int)STR.DefaultLangCode == 3 || (int)STR.DefaultLangCode == 4 ? 12 : 14, GameFacade.GraphicsDevice);
            PersonText = new Simitone.Client.UI.Controls.UIOriginalText(GameFacade.Strings.GetString("130", "16"), labelFont) { Y = 114, Color = Color.White };
            if (labelFont != null) PersonText.X = 198 - labelFont.Measure(PersonText.Text) / 2;
            Add(PersonText);
            NameLabelText = new Simitone.Client.UI.Controls.UIOriginalText(GameFacade.Strings.GetString("130", "26"), labelFont) { Y = 52, Color = Color.White };
            if (labelFont != null) NameLabelText.X = 255 - labelFont.Measure(NameLabelText.Text);
            Add(NameLabelText);
            BioLabelText = new Simitone.Client.UI.Controls.UIOriginalText(GameFacade.Strings.GetString("130", "24"), font) { Y = 425 - 2 * font.LineHeight, X = 22, Color = Color.White };
            Add(BioLabelText);

            // name field (275,52)-(530,77), white, capacity 25 (16 when the
            // language byte is 15 — r143 §3.3 0x2cf47c-0x2cf4a4), transparent.
            // CAS-02: single-line native behavior lives in the box subclass.
            NameBox = new UIOriginalPersonNameBox() { Position = new Vector2(275, 52) };
            NameBox.SetSize(255, 25);
            NameBox.MaxLines = 1;
            NameBox.MaxChars = (int)STR.DefaultLangCode == 15 ? 16 : 25;
            NameBox.BackgroundTextureReference = null;
            NameBox.TextMargin = new Rectangle(2, 2, 2, 2);
            NameBox.TextStyle = NameBox.TextStyle.Clone();
            NameBox.TextStyle.Color = Color.White;
            // Original Init @0x2cf3f0 selects font-table slot 10.
            NameBox.TextStyle.Size = 10;
            // Return routes as the dialog default command (cTSWinGenDlg law,
            // 0x533728 + 0x2cdffc): Done, guarded by the disabled state.
            NameBox.OnReturn += () => { if (!DoneBtn.Disabled) OnDone?.Invoke(); };
            Add(NameBox);

            // bio field (22,425)-(783,521), multiline, capacity 2048
            // cTSWinTextEdit2, font[10], transparent, unlimited lines.
            // Reuse the recovered glyph-based caret/selection/navigation law.
            BioEdit = new Simitone.Client.UI.Controls.UIOriginalTextEdit(font, 761, 96, horizontalInset: 5)
            {
                Position = new Vector2(22, 425), Capacity = 2048, TextColor = Color.White
            };
            Add(BioEdit);

            // personality rows: LED strip at (228, 131+32i), caption right-aligned at LED.x-12
            for (int i = 0; i < 5; i++)
            {
                TraitLabels[i] = new Simitone.Client.UI.Controls.UIOriginalText(GameFacade.Strings.GetString("130", (17 + i).ToString()), traitFont) { Y = LED_Y0 + i * LED_PITCH, Color = Color.White };
                Add(TraitLabels[i]);
                LedStrips[i] = new UIImage() { Position = new Vector2(LED_X, LED_Y0 + i * LED_PITCH) };
                Add(LedStrips[i]);
                int idx = i;
                LedStrips[i].ListenForMouse(new Rectangle(0, 0, 59, 25), (type, state) =>
                {
                    if (type != UIMouseEventType.MouseDown) return;
                    var boundary = LED_X + Values[idx] * LED_CLICK_PITCH;
                    var plus = state.MouseState.X > boundary;
                    AdjustTrait(idx, plus ? 1 : -1);
                });
            }

            // remaining-points bar (117,342), width = pool*6 of 149
            PointsBar = new UIImage() { Position = new Vector2(POINTS_BAR_X, POINTS_BAR_Y) };
            Add(PointsBar);

            // zodiac points button (129,290)-(249,310): invisible, tooltip 130:27
            // AUD-17 F-9: the comment promised the native tooltip; it was never
            // attached (the in-game personality subpanel's zodiac carries it).
            ZodiacButton = new UIContainer() { Position = new Vector2(POINTS_BTN_RECT.X, POINTS_BTN_RECT.Y) };
            ZodiacButton.ListenForMouse(new Rectangle(0, 0, POINTS_BTN_RECT.Width, POINTS_BTN_RECT.Height), (t, s) => { });
            ZodiacButton.Tooltip = GameFacade.Strings.GetString("130", "27") ?? "Astrological Sign";
            Add(ZodiacButton);
            ZodiacText = new Simitone.Client.UI.Controls.UIOriginalText("", labelFont) { Y = POINTS_BTN_RECT.Y, Color = Color.White };
            Add(ZodiacText);

            UpdateType();
            UpdateLeds();
        }

        private UIOriginalSheetButton Sheet(string member, int w, int x, int y, string tip)
        {
            var cat = tip.Split(':')[0];
            var idx = tip.Split(':')[1];
            var btn = new UIOriginalSheetButton(member, w) { Position = new Vector2(x, y), Tooltip = GameFacade.Strings.GetString(cat, idx) };
            Add(btn);
            return btn;
        }

        public void SetAge(bool adult)
        {
            var c = AType.ToCharArray();
            c[1] = adult ? 'a' : 'c';
            AType = new string(c);
            UpdateType();
        }

        public void SetGender(char g)
        {
            var c = AType.ToCharArray();
            c[0] = g;
            AType = new string(c);
            UpdateType();
        }

        public void SetSkin(string skin)
        {
            SkinType = skin;
            UpdateType();
        }

        public void UpdateType()
        {
            AdultBtn.Selected = AType[1] == 'a';
            ChildBtn.Selected = AType[1] == 'c';
            MaleBtn.Selected = AType[0] == 'm';
            FemaleBtn.Selected = AType[0] == 'f';
            LightBtn.Selected = SkinType == "lgt";
            MediumBtn.Selected = SkinType == "med";
            DarkBtn.Selected = SkinType == "drk";
            OnCollectionChange?.Invoke(AType, SkinType);
        }

        /// <summary>
        /// The engine pool law (cas-layout-law.md §3.4): value 0..10, pool
        /// counts down from 25, plus-zone clicks denied (deny sound) at pool 0.
        /// </summary>
        public void AdjustTrait(int i, int delta)
        {
            if (delta > 0)
            {
                if (Values[i] >= 10) return;
                if (Pool <= 0)
                {
                    // AUD-19: the deny click must fire "ui_nhood_error" — the
                    // string physically at the native CAS deny call (PlaySoundA of
                    // the blob slot after "UI_CAC_personpts", data 0x5ceb0+0x11;
                    // see tools/iff-dump/r143/cas-layout-law.md §click). The old
                    // name "UI_CAC_personpts_deny" exists nowhere in the shipped
                    // corpus, so the deny was silent.
                    FSO.HIT.HITVM.Get().PlaySoundEvent(FSO.Client.UI.Model.UISounds.NeighborhoodError);
                    return;
                }
                Values[i]++;
                Pool--;
            }
            else
            {
                if (Values[i] <= 0) return;
                Values[i]--;
                Pool++;
            }
            FSO.HIT.HITVM.Get().PlaySoundEvent("UI_CAC_personpts");
            UpdateLeds();
        }

        public void UpdateLeds()
        {
            var led = UIOriginal.EnsureResolved("nbhd\\DesignCharPersLED.bmp")?.Get(GameFacade.GraphicsDevice);
            for (int i = 0; i < 5; i++)
            {
                if (led != null)
                {
                    var lit = Math.Min(59, Values[i] * LED_DRAW_PITCH);
                    LedStrips[i].Texture = lit > 0 ? UIOriginal.Rect("nbhd\\DesignCharPersLED.bmp", 0, 0, lit, 25) : null;
                }
                if (TraitLabels[i].Font != null) TraitLabels[i].X = LED_X - 12 - TraitLabels[i].Font.Measure(TraitLabels[i].Text);
            }
            var bar = UIOriginal.EnsureResolved("nbhd\\CACPointRemainingBars.bmp")?.Get(GameFacade.GraphicsDevice);
            if (bar != null)
            {
                var w = Math.Min(149, Pool * LED_DRAW_PITCH);
                PointsBar.Texture = w > 0 ? UIOriginal.Rect("nbhd\\CACPointRemainingBars.bmp", 0, 0, w, 25) : null;
            }
            // zodiac caption: "(Sign)" whenever pool < 25 (engine PrepareButtons)
            var sign = (Pool < 25) ? "(" + ZodiacName(ComputeZodiac()) + ")" : "";
            ZodiacText.Text = sign;
            if (ZodiacText.Font != null) ZodiacText.X = POINTS_BTN_RECT.X + (POINTS_BTN_RECT.Width - ZodiacText.Font.Measure(sign)) / 2;
        }

        /// <summary>
        /// Engine ComputeZodiacSign @0x172170: nearest archetype by squared
        /// Euclidean distance over the five trait values; Pisces's zero row
        /// makes an unassigned sim a Pisces.
        /// </summary>
        public int ComputeZodiac()
        {
            int best = 11;
            float bestD = float.MaxValue;
            for (int s = 0; s < 12; s++)
            {
                float d = 0;
                for (int t = 0; t < 5; t++)
                {
                    var diff = Values[t] - ZodiacArchetypes[s][t];
                    d += diff * diff;
                }
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }
    }

    /// <summary>
    /// CAS-02: the native PERSON name field is a single-line cTSWinTextEdit2
    /// (SetLinesAllowed(1) at 0x2cf4a4, capacity 25 / 16 for language 15,
    /// r143 §3.3). cTSWinTextEdit2::TSOnCharacter 0x5336e8-0x533724 normalizes
    /// CR to LF and, with linesAllowed==1, never inserts it: the LF is
    /// swallowed (gate 0x50==0) or routed to the parent as command 3/0x17
    /// (0x533728-0x533740). The CAS window forwards that command to
    /// cTSWinGenDlg::TSOnCommand (0x2cdffc) — the dialog default action, i.e.
    /// Done. The shared InputManager instead inserts a literal '\n' on Enter,
    /// so this box suppresses the key before the shared mutation and raises
    /// the base OnEnterPress event itself, and strips CR/LF from the frame
    /// text stream so a pasted name cannot carry a line break. Horizontal
    /// caret scrolling and initial focus are the base control's existing
    /// (r239-era) mechanics; the empty/focused frame is transparent by the
    /// R143 construction and Done-disable covers the native flash-on-empty.
    /// </summary>
    public class UIOriginalPersonNameBox : UITextBox
    {
        /// <summary>The native Return routing (dialog default command). The
        /// base OnEnterPress event cannot be raised from a subclass, and the
        /// shared Enter key is suppressed before it could fire, so this is the
        /// equivalent native hook.</summary>
        public event Action OnReturn;

        public override void Update(UpdateState state)
        {
            if (state.InputManager?.GetFocus() != this) { base.Update(state); return; }
            var text = state.FrameTextInput;
            var keys = state.NewKeys;
            var enter = keys.Contains(Keys.Enter);
            try
            {
                // Native TSOnCharacter: CR normalizes to LF, LF never enters a
                // linesAllowed==1 buffer (typed, pasted or frame-input stream).
                if (text != null) state.FrameTextInput = text.Where(c => c != '\n' && c != '\r').ToList();
                state.NewKeys = keys.Where(k => k != Keys.Enter).ToList();
                base.Update(state);
                if (enter) OnReturn?.Invoke();
            }
            finally { state.FrameTextInput = text; state.NewKeys = keys; }
        }
    }

    /// <summary>The native family-name editor rejects its fixed filename
    /// character table during typed input. Paste and SetText bypass that flag.
    /// Keep the rule local so other text fields retain their established input.</summary>
    public class UIOriginalFamilyNameBox : UITextBox
    {        // TSOnCharacter scans exactly20 bytes at CODE59c3a8 (file5a5238).
        internal const string ForbiddenTypedCharacters = "\\/|*?:<>\"'%()&;@!#,.";
        internal static bool AllowsTypedCharacter(char value) => ForbiddenTypedCharacters.IndexOf(value) < 0;

        public override void Update(UpdateState state)
        {
            if (state.InputManager?.GetFocus() != this) { base.Update(state); return; }
            var text = state.FrameTextInput;
            var keys = state.NewKeys;
            try
            {
                if (text != null) state.FrameTextInput = text.Where(AllowsTypedCharacter).ToList();
                else state.NewKeys = keys.Where(k => AllowsTypedCharacter(InputManager.TranslateChar(k,
                    state.ShiftDown, state.KeyboardState.CapsLock, state.KeyboardState.NumLock))).ToList();
                // Filtering before ApplyKeyboardInput preserves the selection
                // when a rejected character is typed over selected text.
                base.Update(state);
            }
            finally { state.FrameTextInput = text; state.NewKeys = keys; }
        }
    }

    /// <summary>
    /// R143: the ORIGINAL Create-A-Family screen, engine law
    /// r143/cas-layout-law.md §4: Add 139x103 (593,103) / Delete 149x99
    /// (594,208) / Edit 137x99 (594,311) 4-state sheets; Done/Cancel system
    /// buttons centered (200,529)/(600,529); family name (274,50)-(529,75)
    /// capacity 24 (file-char filtered); 8 member slots 85x105 in the grid
    /// region (111,121)-(551,481), horizontal-first pitch (col 120, row 140),
    /// portrait offset (15,10), name at the slot bottom. Add enabled iff
    /// count&lt;8 AND name non-empty; Delete/Edit iff a member is selected.
    /// The 'Enter Last Name:' label is parked OFFSCREEN-LEFT by the engine
    /// (quirk, kept canon).
    /// </summary>
    public class UIOriginalDesignFamily : UICASOriginalScreen
    {
        public UIOriginalSheetButton AddBtn, DeleteBtn, EditBtn;
        public UIOriginalSystemButton DoneBtn, CancelBtn;
        public UITextBox FamilyNameBox;
        public UIContainer[] Slots = new UIContainer[8];
        public Simitone.Client.UI.Controls.UIOriginalText TitleText;

        public int SelectedMember = -1;
        public int MemberCount;
        public Action<bool, int> ModifySim;
        public Action OnFamilyDone;
        public Action OnFamilyCancel;

        public UIOriginalDesignFamily() : base("nbhd\\DsgnFamBkg.bmp", false)
        {
            var font = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadCaption(GameFacade.GraphicsDevice);
            var bfont = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadByIndex(16, GameFacade.GraphicsDevice);

            TitleText = new Simitone.Client.UI.Controls.UIOriginalText(GameFacade.Strings.GetString("129", "9"), bfont) { Y = 6, Color = Color.White };
            if (bfont != null) TitleText.X = (ORIG_W - bfont.Measure(TitleText.Text)) / 2;
            Add(TitleText);

            AddBtn = new UIOriginalSheetButton("nbhd\\AddPersonBtn.bmp", 139) { Position = new Vector2(593, 103), Tooltip = GameFacade.Strings.GetString("129", "0") };
            AddBtn.OnButtonClick += (b) => ModifySim?.Invoke(false, -1);
            Add(AddBtn);
            DeleteBtn = new UIOriginalSheetButton("nbhd\\DeletePersonBtn.bmp", 149) { Position = new Vector2(594, 208), Tooltip = GameFacade.Strings.GetString("129", "1") };
            DeleteBtn.OnButtonClick += (b) => ModifySim?.Invoke(true, SelectedMember);
            Add(DeleteBtn);
            EditBtn = new UIOriginalSheetButton("nbhd\\EditPersonBtn.bmp", 137) { Position = new Vector2(594, 311), Tooltip = GameFacade.Strings.GetString("129", "2") };
            EditBtn.OnButtonClick += (b) => ModifySim?.Invoke(false, SelectedMember);
            Add(EditBtn);

            DoneBtn = new UIOriginalSystemButton(GameFacade.Strings.GetString("129", "3"), font) { Position = new Vector2(100, 529) };
            DoneBtn.CenterCaption();
            DoneBtn.OnButtonClick += (b) => OnFamilyDone?.Invoke();
            Add(DoneBtn);
            CancelBtn = new UIOriginalSystemButton(GameFacade.Strings.GetString("129", "4"), font) { Position = new Vector2(500, 529) };
            CancelBtn.CenterCaption();
            CancelBtn.OnButtonClick += (b) => OnFamilyCancel?.Invoke();
            Add(CancelBtn);

            FamilyNameBox = new UIOriginalFamilyNameBox() { Position = new Vector2(274, 50) };
            FamilyNameBox.SetSize(255, 25);
            FamilyNameBox.MaxChars = 24;
            FamilyNameBox.BackgroundTextureReference = null;
            FamilyNameBox.TextMargin = new Rectangle(2, 2, 2, 2);
            FamilyNameBox.TextStyle = FamilyNameBox.TextStyle.Clone();
            FamilyNameBox.TextStyle.Color = Color.White;
            // Original family Init @0x2d14f8 selects font[10].
            FamilyNameBox.TextStyle.Size = 10;
            Add(FamilyNameBox);

            // Native Init 0x2d1880 leaves flow flag zero: four columns, two rows.
            // R143 incorrectly described the inactive vertical branch as active.
            for (int i = 0; i < 8; i++)
            {
                int col = i % 4, row = i / 4;
                var slot = new UIContainer() { Position = new Vector2(111 + col * 120, 121 + row * 140) };
                int idx = i;
                slot.ListenForMouse(new Rectangle(0, 0, 85, 105), (type, state) =>
                {
                    if (!slot.Visible) return;
                    if (type == UIMouseEventType.MouseOver) SlotHovered[idx] = true;
                    else if (type == UIMouseEventType.MouseOut) SlotHovered[idx] = false;
                    else if (type == UIMouseEventType.MouseDown) SelectMember(idx);
                    RefreshSelection();
                });
                Slots[i] = slot;
                Add(slot);
            }
        }

        private readonly bool[] SlotHovered = new bool[8];

        public void RefreshSelection()
        {
            if (SelectedMember < 0 || SelectedMember >= MemberCount) SelectedMember = -1;
            for (int i = 0; i < 8; i++)
            {
                Slots[i].Opacity = 1f;
                foreach (var child in Slots[i].GetChildren())
                {
                    if (child is UIOriginalPersonPortrait portrait)
                        portrait.Column = i == SelectedMember ? 1 : SlotHovered[i] ? 2 : 0;
                    var label = child as Simitone.Client.UI.Controls.UIOriginalText;
                    if (label != null) label.Color = i == SelectedMember
                        ? new Color(0, 255, 255) : SlotHovered[i] ? Color.White : new Color(195, 205, 205);
                }
            }
            DeleteBtn.Disabled = SelectedMember == -1;
            EditBtn.Disabled = SelectedMember == -1;
            AddBtn.Disabled = MemberCount >= 8 || FamilyNameBox.CurrentText.Length == 0;
        }

        public void SelectMember(int index)
        {
            SelectedMember = index;
            RefreshSelection();
        }
    }
}
