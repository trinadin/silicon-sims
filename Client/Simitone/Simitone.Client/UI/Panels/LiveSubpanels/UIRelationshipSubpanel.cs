using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.UI.Panels.LiveSubpanels
{
    /// <summary>
    /// R184: the native cWinPeople relationship surface. The original is a
    /// fixed, paged row of compact 45x90 cWinRelationship compositors; it is
    /// not a free-scrolling modern list. English uses five cards in a 280px
    /// host, with the 1024-wide desktop special case expanding the host to
    /// 504px and ten cards.
    /// </summary>
    public class UIRelationshipSubpanel : UISubpanel
    {
        public const int HOST_W_ENGLISH = 280;
        public const int HOST_W_1024 = 504;
        public const int HOST_H = 100;
        public const int CARD_X = 35;
        public const int CARD_Y = 18;
        public const int CARD_W = 45;
        public const int CARD_H = 90;
        public const int PAGE_SIZE_ENGLISH = 5;
        public const int PAGE_SIZE_1024 = 10;
        public const string POPUP_MEMBER = "cpanel\\Backgrounds\\SocialInfoPopup.BMP";
        public const string POPUP_TITLE_FALLBACK = "Relationship";
        public const string POPUP_BODY_FALLBACK = "Your relationship with this Sim is represented by daily (top bar) and lifetime (bottom bar) relationship scores. Your daily relationship changes quickly and shows recent developments. Your lifetime relationship changes slowly and shows long term trends.  Remember, this Sim may not feel the same way about you as you feel about them.";

        // Retained for source compatibility with callers written against the
        // former touch list. Native desktop composition deliberately leaves it
        // null and exposes Cards/Page instead.
        public UITouchScroll ScrollView;
        public List<Tuple<int, int>> Items = new List<Tuple<int, int>>();
        public readonly List<UIRelationshipDisplay> Cards = new List<UIRelationshipDisplay>();
        public int RelSort = 3;
        public int Page;

        public UIOriginalText HostTitle;
        public UIOriginalSortButton FriendButton;
        public UIOriginalSortButton FamButton;
        public UIOriginalSortButton AllButton;
        public UIOriginalSortButton FameButton;
        public UIOriginalSheetButton PagePrev;
        public UIOriginalSheetButton PageNext;
        public UIContainer CardLayer;
        public UIOptionAboutPopup CurrentPopup;
        public UIRelationshipDisplay PopupCard;

        public static int OriginalSortsMounted;

        public int NativeHostWidth => Capacity == PAGE_SIZE_1024 ? HOST_W_1024 : HOST_W_ENGLISH;
        public int Capacity => CapacityForScreenWidth(UIScreen.Current?.ScreenWidth ?? 800);

        public static int CapacityForScreenWidth(int screenWidth)
        {
            return screenWidth == 1024 ? PAGE_SIZE_1024 : PAGE_SIZE_ENGLISH;
        }

        public UIRelationshipSubpanel(TS1GameScreen game) : base(game)
        {
            var gd = GameFacade.GraphicsDevice;
            var titleFont = OriginalGlyphFont.LoadByIndex(6, gd);
            var title = OriginalLiveStrings.Entry(132, 0) ?? "Relationships";
            HostTitle = new UIOriginalText(title, titleFont)
            {
                Position = new Vector2(5, 0),
                Size = new Vector2(100, titleFont?.LineHeight ?? 13)
            };
            Add(HostTitle);

            int titleH = titleFont?.LineHeight ?? 13;
            FamButton = MakeSort("cpanel\\Buttons\\RelFamilySort.bmp", 0,
                GameFacade.Strings.GetString("240", "0"), 3, titleH + 1);
            FriendButton = MakeSort("cpanel\\Buttons\\RelFriendSort.bmp", 1,
                GameFacade.Strings.GetString("240", "1"), 2, titleH + 22);
            FameButton = MakeSort("cpanel\\Buttons\\RelStarSort.bmp", 2,
                GameFacade.Strings.GetString("240", "2"), 2, titleH + 43);
            AllButton = MakeSort("cpanel\\Buttons\\RelAllSort.bmp", 3,
                GameFacade.Strings.GetString("240", "3"), 2, titleH + 64);
            if (FamButton.OriginalMounted && FriendButton.OriginalMounted
                && AllButton.OriginalMounted && FameButton.OriginalMounted)
                OriginalSortsMounted++;

            CardLayer = new UIContainer();
            Add(CardLayer);

            var pagerY = titleH + (HOST_H - titleH - 49) / 2;
            PagePrev = new UIOriginalSheetButton("cpanel\\Buttons\\ScrollLeft.bmp")
            {
                Position = new Vector2(24, pagerY),
                Tooltip = GameFacade.Strings.GetString("154", "0")
            };
            PagePrev.OnButtonClick += b => SetPage(Page - 1);
            Add(PagePrev);
            PageNext = new UIOriginalSheetButton("cpanel\\Buttons\\ScrollRight.bmp")
            {
                Position = new Vector2(NativeHostWidth - 11, pagerY),
                Tooltip = GameFacade.Strings.GetString("154", "1")
            };
            PageNext.OnButtonClick += b => SetPage(Page + 1);
            Add(PageNext);

            ChangeCat(3);
            UpdateRelView(true);
        }

        private UIOriginalSortButton MakeSort(string member, int category, string tooltip, int x, int y)
        {
            var button = new UIOriginalSortButton(member, tooltip)
            {
                Position = new Vector2(x, y)
            };
            button.OnButtonClick += b => ChangeCat(category);
            Add(button);
            return button;
        }

        public void ChangeCat(int cat)
        {
            RelSort = cat;
            Page = 0;
            if (FriendButton != null) FriendButton.SelectedState = cat == 1;
            if (FamButton != null) FamButton.SelectedState = cat == 0;
            if (AllButton != null) AllButton.SelectedState = cat == 3;
            if (FameButton != null) FameButton.SelectedState = cat == 2;
            UpdateRelView(true);
        }

        public void SetPage(int page)
        {
            int last = Math.Max(0, (Items.Count - 1) / Capacity);
            Page = Math.Max(0, Math.Min(last, page));
            RebuildPage();
        }

        /// <summary>
        /// Native marker bit model used by the card compositor: friend-smiley
        /// is independent (bit 0), while deep-heart (bit 2) takes precedence
        /// over ordinary heart (bit 1) at the shared x=14 anchor.
        /// </summary>
        public static string MarkerFor(int flags)
        {
            if ((flags & 4) != 0) return "cpanel\\Buttons\\heartdeep.bmp";
            if ((flags & 2) != 0) return "cpanel\\Buttons\\heart.bmp";
            if ((flags & 1) != 0) return "cpanel\\Buttons\\smiley.bmp";
            return null;
        }

        public static int RelationshipValue(IList<short> values, int slot)
        {
            return values != null && slot >= 0 && slot < values.Count
                ? Math.Max(-100, Math.Min(100, (int)values[slot])) : 0;
        }

        /// <summary>
        /// Native PersonFinder::GetRelation returns a relation record whose
        /// classification byte at +5 is the Friends-filter/smiley predicate.
        /// This is deliberately not reconstructed from the two daily scores:
        /// the executable reads the stored classification directly.
        /// </summary>
        public static bool IsNativeFriend(IList<short> forward)
        {
            return forward != null && forward.Count > 5 && forward[5] == 1;
        }

        // Kept for source compatibility with older probes. The reverse vector
        // is not consulted by the native predicate.
        public static bool IsMutualFriend(IList<short> forward, IList<short> reverse)
        {
            return IsNativeFriend(forward);
        }

        /// <summary>The Famous filter requires a resolved person and PD81.</summary>
        public static bool IsNativeFamous(bool resolved, int fameStarPower)
        {
            return resolved && fameStarPower != 0;
        }

        public static int MarkerFlagsFor(IList<short> forward, IList<short> reverse)
        {
            int result = IsNativeFriend(forward) ? 1 : 0;
            if (forward != null && forward.Count > 1 && forward[1] != 0) result |= 2;
            if (forward != null && forward.Count > 3 && forward[3] != 0) result |= 4;
            return result;
        }

        public static bool SameFamily(IList<short> source, IList<short> candidate)
        {
            int familyWord = (int)VMPersonDataVariable.TS1FamilyNumber;
            return source != null && candidate != null
                && source.Count > familyWord && candidate.Count > familyWord
                && source[familyWord] == candidate[familyWord];
        }

        /// <summary>
        /// Exact cWinPeople/GetRelatedPeople candidate comparator. Family and
        /// resolved runtime persons sort before their peers, relationship slot
        /// zero sorts high-to-low, and the persisted candidate index is the
        /// deterministic final key.
        /// </summary>
        public static int CompareNativeOrder(bool leftFamily, bool rightFamily,
            bool leftResolved, bool rightResolved, int leftScore, int rightScore,
            int leftIndex, int rightIndex)
        {
            if (leftFamily != rightFamily) return leftFamily ? -1 : 1;
            if (leftResolved != rightResolved) return leftResolved ? -1 : 1;
            var scoreOrder = rightScore.CompareTo(leftScore);
            return scoreOrder != 0 ? scoreOrder : leftIndex.CompareTo(rightIndex);
        }

        private bool PassesFilter(int from, int target)
        {
            var provider = Content.Get().Neighborhood;
            var source = provider.GetNeighborByID((short)from);
            var other = provider.GetNeighborByID((short)target);
            if (source == null || other == null || from == target) return false;

            if (RelSort == 0)
            {
                // PersonFinder's native Family predicate compares the selected
                // and candidate Neighbor records' +0xEE halfwords directly.
                return SameFamily(source.PersonData, other.PersonData);
            }
            if (RelSort == 1)
            {
                List<short> forward = null;
                source.Relationships?.TryGetValue(target, out forward);
                return IsNativeFriend(forward);
            }
            if (RelSort == 2)
            {
                try
                {
                    var live = Game.vm?.Context?.ObjectQueries?.Avatars?
                        .OfType<VMAvatar>()
                        .FirstOrDefault(x => x.Position != LotTilePos.OUT_OF_WORLD
                            && x.GetPersonData(VMPersonDataVariable.NeighborId) == target);
                    return IsNativeFamous(live != null,
                        live?.GetPersonData(VMPersonDataVariable.TS1FameStarPower) ?? 0);
                }
                catch { return false; }
            }
            return true;
        }

        public UITSContainer DisplayProvider(int index)
        {
            var item = Items[index];
            var card = new UIRelationshipDisplay(item.Item1, item.Item2, Game.vm, Game);
            card.OnLeftClick = SelectRelationship;
            return card;
        }

        public override void Update(UpdateState state)
        {
            UpdateRelView();
            base.Update(state);
        }

        public override void GameResized()
        {
            base.GameResized();
            PageNext.Position = new Vector2(NativeHostWidth - 11, PageNext.Y);
            SetPage(Page);
        }

        public void UpdateRelView(bool force = false)
        {
            var selected = Game.SelectedAvatar;
            if (selected == null)
            {
                if (Items.Count != 0 || force)
                {
                    Items = new List<Tuple<int, int>>();
                    Page = 0;
                    RebuildPage();
                }
                return;
            }

            int from = selected.GetPersonData(VMPersonDataVariable.NeighborId);
            var source = Content.Get().Neighborhood.GetNeighborByID((short)from);
            if (source?.Relationships == null) return;

            var provider = Content.Get().Neighborhood;
            var activeFamily = Game.ActiveFamily?.FamilyGUIDs;
            var resolved = new HashSet<int>();
            try
            {
                if (Game.vm?.Context?.ObjectQueries?.Avatars != null)
                    foreach (var avatar in Game.vm.Context.ObjectQueries.Avatars.OfType<VMAvatar>())
                        resolved.Add(avatar.GetPersonData(VMPersonDataVariable.NeighborId));
            }
            catch { }

            var candidates = source.Relationships.Keys
                .Where(target => PassesFilter(from, target))
                .ToList();
            candidates.Sort((left, right) =>
            {
                var leftNeighbor = provider.GetNeighborByID((short)left);
                var rightNeighbor = provider.GetNeighborByID((short)right);
                var leftFamily = leftNeighbor != null && activeFamily != null
                    && activeFamily.Contains(leftNeighbor.GUID);
                var rightFamily = rightNeighbor != null && activeFamily != null
                    && activeFamily.Contains(rightNeighbor.GUID);
                List<short> leftValues = null;
                List<short> rightValues = null;
                source.Relationships.TryGetValue(left, out leftValues);
                source.Relationships.TryGetValue(right, out rightValues);
                return CompareNativeOrder(leftFamily, rightFamily,
                    resolved.Contains(left), resolved.Contains(right),
                    RelationshipValue(leftValues, 0), RelationshipValue(rightValues, 0),
                    left, right);
            });
            var next = candidates.Select(target => Tuple.Create(from, target)).ToList();
            bool changed = force || next.Count != Items.Count;
            if (!changed)
            {
                for (int i = 0; i < next.Count; i++)
                    if (!next[i].Equals(Items[i])) { changed = true; break; }
            }
            if (!changed) return;

            Items = next;
            int last = Math.Max(0, (Items.Count - 1) / Capacity);
            Page = Math.Min(Page, last);
            RebuildPage();
        }

        private void RebuildPage()
        {
            // cWinPeople disables its one shared live popup before a filter or
            // pager rebuild detaches the relationship-card clients.
            CloseRelationshipPopup();
            foreach (var card in Cards.ToArray()) CardLayer.Remove(card);
            Cards.Clear();

            int start = Page * Capacity;
            int end = Math.Min(Items.Count, start + Capacity);
            for (int i = start; i < end; i++)
            {
                var item = Items[i];
                var card = new UIRelationshipDisplay(item.Item1, item.Item2, Game.vm, Game)
                {
                    Position = new Vector2(CARD_X + (i - start) * CARD_W, CARD_Y)
                };
                card.OnLeftClick = SelectRelationship;
                CardLayer.Add(card);
                Cards.Add(card);
            }

            PagePrev.Visible = Page > 0;
            PageNext.Visible = end < Items.Count;
            PageNext.Position = new Vector2(NativeHostWidth - 11, PageNext.Y);
            Invalidate();
        }

        /// <summary>
        /// cWinPeople::TSOnCommand's relationship branch owns one
        /// cWinLivePopup. Selecting the active card toggles it closed;
        /// selecting another card retargets that same popup in place.
        /// </summary>
        internal void SelectRelationship(UIRelationshipDisplay card)
        {
            if (card == null) return;
            var title = OriginalLiveStrings.Entry(132, 1) ?? POPUP_TITLE_FALLBACK;
            var body = RelationshipPopupBody(card.NeighborID);
            if (CurrentPopup != null)
            {
                if (ReferenceEquals(PopupCard, card))
                {
                    CloseRelationshipPopup();
                    return;
                }

                // Native SetClient keeps the popup object alive, but each card
                // reruns SetupClient because its full name, zodiac and catalog
                // biography suffix can differ.
                CurrentPopup.SetContent(title, body);
                CurrentPopup.Position = new Vector2(Size.X - CurrentPopup.Size.X,
                    -CurrentPopup.Size.Y);
                CurrentPopup.CanonPos = new Point((int)CurrentPopup.X, (int)CurrentPopup.Y);
                PopupCard = card;
                return;
            }

            var row = new UIOriginalOptionsPanel.OptRow
            {
                Caption = card.Tooltip,
                AboutTitle = title,
                AboutBody = body,
                PopupMember = POPUP_MEMBER
            };
            var artRef = UIOriginal.EnsureResolved(POPUP_MEMBER);
            CurrentPopup = new UIOptionAboutPopup(null, row,
                artRef != null ? artRef.Get(GameFacade.GraphicsDevice) : null);
            CurrentPopup.Position = new Vector2(Size.X - CurrentPopup.Size.X,
                -CurrentPopup.Size.Y);
            CurrentPopup.CanonPos = new Point((int)CurrentPopup.X, (int)CurrentPopup.Y);
            PopupCard = card;
            DynamicOverlay.Add(CurrentPopup);
        }

        /// <summary>
        /// cWinRelationship::SetupClient appends the PersonFinder-composed
        /// identity after two newlines: catalog name, optional family surname,
        /// optional zodiac, " -- ", and the person's catalog description.
        /// </summary>
        internal string RelationshipPopupBody(int neighborID)
        {
            var generic = OriginalLiveStrings.Entry(132, 2) ?? POPUP_BODY_FALLBACK;
            var provider = Content.Get().Neighborhood;
            var neighbor = provider?.GetNeighborByID((short)neighborID);
            if (neighbor == null) return generic;

            GameObject obj = null;
            try { obj = Content.Get().WorldObjects.Get(neighbor.GUID); }
            catch { }
            var catalog = obj?.Resource?.Get<CTSS>(obj.OBJ.CatalogStringsID);
            var name = catalog?.GetString(0) ?? "";
            var description = catalog?.GetString(1) ?? "";

            IList<short> data = neighbor.PersonData;
            try
            {
                var live = Game?.vm?.Context?.ObjectQueries?.Avatars?.OfType<VMAvatar>()
                    .FirstOrDefault(x => x.GetPersonData(VMPersonDataVariable.NeighborId) == neighborID);
                if (live != null)
                {
                    var runtimeData = new short[Math.Max(neighbor.PersonData?.Length ?? 0,
                        (int)VMPersonDataVariable.TS1Zodiac + 1)];
                    if (neighbor.PersonData != null)
                        Array.Copy(neighbor.PersonData, runtimeData, neighbor.PersonData.Length);
                    var runtimeWords = new VMPersonDataVariable[]
                    {
                        VMPersonDataVariable.NicePersonality,
                        VMPersonDataVariable.ActivePersonality,
                        VMPersonDataVariable.PlayfulPersonality,
                        VMPersonDataVariable.OutgoingPersonality,
                        VMPersonDataVariable.NeatPersonality,
                        VMPersonDataVariable.PersonType,
                        VMPersonDataVariable.TS1FamilyNumber,
                        VMPersonDataVariable.TS1Zodiac,
                    };
                    foreach (var word in runtimeWords)
                        runtimeData[(int)word] = live.GetPersonData(word);
                    data = runtimeData;
                }
            }
            catch { }

            string familyName = "";
            bool appendFamily = false;
            int zodiac = 0;
            if (data != null)
            {
                int personTypeWord = (int)VMPersonDataVariable.PersonType;
                int familyWord = (int)VMPersonDataVariable.TS1FamilyNumber;
                int zodiacWord = (int)VMPersonDataVariable.TS1Zodiac;
                int personType = data.Count > personTypeWord ? data[personTypeWord] : 0;
                int family = data.Count > familyWord ? data[familyWord] : 0;
                appendFamily = personType != 0 && personType != 4 && family != 0;
                if (appendFamily)
                    familyName = provider.GetFamilyString((ushort)family)?.GetString(0) ?? "";
                zodiac = data.Count > zodiacWord ? data[zodiacWord] : 0;
                if (zodiac == 0) zodiac = ComputeZodiacCode(data);
            }
            var identity = ComposePopupIdentity(name, familyName, appendFamily,
                zodiac, description);
            return generic + "\n\n" + identity;
        }

        internal static string ComposePopupIdentity(string name, string familyName,
            bool appendFamily, int zodiac, string description)
        {
            var result = name ?? "";
            if (appendFamily && !string.IsNullOrEmpty(familyName)) result += " " + familyName;
            if (!string.IsNullOrEmpty(result) && zodiac > 0)
            {
                int sign = Math.Max(1, Math.Min(12, zodiac));
                result += " (" + Simitone.Client.UI.Panels.CAS.UIOriginalDesignChar.ZodiacName(sign - 1) + ")";
            }
            return result + " -- " + (description ?? "");
        }

        internal static int ComputeZodiacCode(IList<short> data)
        {
            if (data == null) return 0;
            var traitWords = new int[]
            {
                (int)VMPersonDataVariable.NeatPersonality,
                (int)VMPersonDataVariable.OutgoingPersonality,
                (int)VMPersonDataVariable.ActivePersonality,
                (int)VMPersonDataVariable.PlayfulPersonality,
                (int)VMPersonDataVariable.NicePersonality,
            };
            var archetypes = Simitone.Client.UI.Panels.CAS.UIOriginalDesignChar.ZodiacArchetypes;
            int best = 11;
            float bestDistance = float.MaxValue;
            for (int sign = 0; sign < archetypes.Length; sign++)
            {
                float distance = 0;
                for (int trait = 0; trait < traitWords.Length; trait++)
                {
                    float value = data.Count > traitWords[trait] ? data[traitWords[trait]] / 100f : 0;
                    float delta = value - archetypes[sign][trait];
                    distance += delta * delta;
                }
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = sign;
                }
            }
            return best + 1;
        }

        public void CloseRelationshipPopup()
        {
            if (CurrentPopup != null) DynamicOverlay.Remove(CurrentPopup);
            CurrentPopup = null;
            PopupCard = null;
        }

        public override void Kill()
        {
            CloseRelationshipPopup();
            base.Kill();
        }
    }

    /// <summary>One exact four-state, 20x20 relationship-filter sheet.</summary>
    public class UIOriginalSortButton : UIOriginalSheetButton
    {
        public bool OriginalMounted;
        public bool SelectedState
        {
            get { return State == 1; }
            set { State = (byte)(value ? 1 : 0); Invalidate(); }
        }

        public UIOriginalSortButton(string member, string tooltip) : base(member)
        {
            OriginalMounted = UIOriginal.EnsureResolved(member)?.Get(GameFacade.GraphicsDevice) != null;
            Tooltip = tooltip;
        }
    }

    /// <summary>
    /// Native 45x90 relationship card. PersonFinder maps relation slot 0 to
    /// the upper +408 score and slot 2 to the lower +404 score; slots 1/3 are
    /// ordinary/deep-heart flags. The friend smiley is a separate mutual flag.
    /// </summary>
    public class UIRelationshipDisplay : UITSContainer
    {
        public const int WIDTH = 45, HEIGHT = 90;
        public const int PORTRAIT_X = 2, PORTRAIT_Y = 0, PORTRAIT_W = 40, PORTRAIT_H = 40;
        public const int UPPER_TEXT_Y = 38, UPPER_BAR_Y = 51, UPPER_BAR_H = 2;
        public const int LOWER_BAR_Y = 55, LOWER_BAR_H = 4, LOWER_TEXT_Y = 59;
        public const int MARKER_Y = 70, SMILEY_X = 2, HEART_X = 14;

        public int FromNeighborID { get; private set; }
        public int NeighborID { get; private set; }
        public int UpperScore { get; private set; }
        public int LowerScore { get; private set; }
        public bool ShowSmiley { get; private set; }
        public bool ShowHeart { get; private set; }
        public bool ShowDeepHeart { get; private set; }
        public int PortraitFrame => PortraitFrameForScore(LowerScore);
        public int UpperFillWidth => FillWidthForScore(UpperScore);
        public int LowerFillWidth => FillWidthForScore(LowerScore);

        public static int PortraitFrameForScore(int score)
        {
            return Math.Min(4, Math.Max(0, (Math.Max(-100, Math.Min(100, score)) + 100) / 40));
        }

        public static int FillWidthForScore(int score)
        {
            score = Math.Max(-100, Math.Min(100, score));
            return Math.Max(0, Math.Min(40, (score + 100) * 40 / 200));
        }

        private readonly VM VM;
        private readonly UIOriginalText UpperText;
        private readonly UIOriginalText LowerText;
        private readonly Texture2D White;
        private readonly Texture2D UnknownStrip;
        private readonly Texture2D Smiley;
        private readonly Texture2D Heart;
        private readonly Texture2D DeepHeart;
        private Texture2D RelationshipStrip;
        private Texture2D Portrait;
        private bool MouseDown;
        private bool Hovered;
        private bool RightDown;
        private Vector2 _Size = new Vector2(WIDTH, HEIGHT);
        public override Vector2 Size { get { return _Size; } set { _Size = value; } }
        public Action<UIRelationshipDisplay> OnLeftClick;

        public UIRelationshipDisplay(int nidFrom, int nid, VM curVM, TS1GameScreen game = null)
        {
            FromNeighborID = nidFrom;
            NeighborID = nid;
            VM = curVM;
            White = FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
            UnknownStrip = UIOriginal.EnsureResolved("cpanel\\People\\UnknownRel.bmp")?.Get(GameFacade.GraphicsDevice);
            Smiley = UIOriginal.EnsureResolved("cpanel\\Buttons\\smiley.bmp")?.Get(GameFacade.GraphicsDevice);
            Heart = UIOriginal.EnsureResolved("cpanel\\Buttons\\heart.bmp")?.Get(GameFacade.GraphicsDevice);
            DeepHeart = UIOriginal.EnsureResolved("cpanel\\Buttons\\heartdeep.bmp")?.Get(GameFacade.GraphicsDevice);

            var font = OriginalGlyphFont.LoadByIndex(7, GameFacade.GraphicsDevice);
            UpperText = new UIOriginalText("", font) { Position = new Vector2(0, UPPER_TEXT_Y) };
            LowerText = new UIOriginalText("", font) { Position = new Vector2(2, LOWER_TEXT_Y) };
            Add(UpperText);
            Add(LowerText);

            ResolvePortrait();
            RefreshValues();
            ListenForMouse(new Rectangle(0, 0, WIDTH, HEIGHT), MouseEvent);
            FSO.Client.Utils.UIUtils.GiveTooltip(this);
        }

        private void ResolvePortrait()
        {
            var provider = Content.Get().Neighborhood;
            var neighbor = provider.GetNeighborByID((short)NeighborID);
            Tooltip = neighbor?.Name ?? "";
            if (neighbor == null || VM == null) return;

            VMMultitileGroup temporary = null;
            try
            {
                var avatar = VM.Context.ObjectQueries.Avatars.OfType<VMAvatar>()
                    .FirstOrDefault(x => x.GetPersonData(VMPersonDataVariable.NeighborId) == NeighborID);
                if (avatar == null)
                {
                    temporary = VM.Context.CreateObjectInstance(neighbor.GUID,
                        LotTilePos.OUT_OF_WORLD, Direction.NORTH, true);
                    avatar = temporary?.BaseObject as VMAvatar;
                }
                if (avatar == null) return;
                RelationshipStrip = avatar.Object?.Resource?.Get<BMP>(2003)
                    ?.GetTexture(GameFacade.GraphicsDevice);
                if (RelationshipStrip == null) Portrait = UIIconCache.GetObject(avatar);
            }
            catch { }
            finally { temporary?.Delete(VM.Context); }
        }

        private static Color MeterColor(int score)
        {
            float t = Math.Max(0f, Math.Min(1f, (score + 100) / 200f));
            return new Color((int)Math.Round(255 * (1 - t)),
                (int)Math.Round(255 * t), 0, 255);
        }

        private void RefreshValues()
        {
            List<short> forward = null;
            List<short> reverse = null;
            try
            {
                var provider = Content.Get().Neighborhood;
                var source = provider.GetNeighborByID((short)FromNeighborID);
                var target = provider.GetNeighborByID((short)NeighborID);
                source?.Relationships?.TryGetValue(NeighborID, out forward);
                target?.Relationships?.TryGetValue(FromNeighborID, out reverse);
            }
            catch { }

            UpperScore = UIRelationshipSubpanel.RelationshipValue(forward, 0);
            LowerScore = UIRelationshipSubpanel.RelationshipValue(forward, 2);
            int markers = UIRelationshipSubpanel.MarkerFlagsFor(forward, reverse);
            ShowSmiley = (markers & 1) != 0;
            ShowHeart = (markers & 2) != 0;
            ShowDeepHeart = (markers & 4) != 0;

            UpperText.Text = UpperScore.ToString();
            LowerText.Text = LowerScore.ToString();
            if (UpperText.Font != null)
                UpperText.X = Math.Max(0, (WIDTH - UpperText.Font.Measure(UpperText.Text)) / 2);
            if (LowerText.Font != null)
                LowerText.X = 2 + Math.Max(0, (40 - LowerText.Font.Measure(LowerText.Text)) / 2);
        }

        internal void MouseEvent(UIMouseEventType type, UpdateState state)
        {
            if (type == UIMouseEventType.MouseOver)
            {
                Hovered = true;
            }
            else if (type == UIMouseEventType.MouseOut)
            {
                Hovered = false;
            }
            else if (type == UIMouseEventType.MouseDown)
            {
                MouseDown = true;
            }
            else if (type == UIMouseEventType.MouseUp)
            {
                if (MouseDown) OnLeftClick?.Invoke(this);
                MouseDown = false;
            }
        }

        /// <summary>
        /// InputManager emits its mouse callbacks from the left-button edge
        /// only. Poll the right edge while hovered, as UIVMPersonButton does,
        /// so native TSOnMouseDownR remains reachable and fires once per press.
        /// The return value is the decoded edge action and is used by the
        /// focused parity probe.
        /// </summary>
        internal bool PollRightButton(bool pressed, bool hovered)
        {
            bool fire = pressed && !RightDown && hovered;
            RightDown = pressed;
            if (fire) CenterOnNeighbor();
            return fire;
        }

        private void CenterOnNeighbor()
        {
            if (VM?.Context?.World == null) return;
            var avatar = VM.Context.ObjectQueries.Avatars.OfType<VMAvatar>()
                .FirstOrDefault(x => x.GetPersonData(VMPersonDataVariable.NeighborId) == NeighborID);
            if (avatar == null || avatar.Position == LotTilePos.OUT_OF_WORLD
                || avatar.WorldUI == null) return;
            VM.Context.World.CenterTo(avatar.WorldUI);
        }

        public override void Update(UpdateState state)
        {
            RefreshValues();
            base.Update(state);
            PollRightButton(state.MouseState.RightButton == ButtonState.Pressed, Hovered);
        }

        private void DrawPortrait(UISpriteBatch batch)
        {
            var strip = RelationshipStrip ?? UnknownStrip;
            if (strip != null && strip.Width >= 5 && strip.Height > 0)
            {
                int frameW = strip.Width / 5;
                var source = new Rectangle(PortraitFrame * frameW, 0, frameW, strip.Height);
                DrawLocalTexture(batch, strip, source, new Vector2(PORTRAIT_X, PORTRAIT_Y),
                    new Vector2(PORTRAIT_W / (float)frameW, PORTRAIT_H / (float)strip.Height));
            }
            if (RelationshipStrip == null && Portrait != null)
            {
                float scale = Math.Min(PORTRAIT_W / (float)Portrait.Width,
                    PORTRAIT_H / (float)Portrait.Height);
                DrawLocalTexture(batch, Portrait, null,
                    new Vector2(PORTRAIT_X + (PORTRAIT_W - Portrait.Width * scale) / 2,
                        PORTRAIT_Y + (PORTRAIT_H - Portrait.Height * scale) / 2),
                    new Vector2(scale));
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            DrawPortrait(batch);
            if (UpperFillWidth > 0)
                DrawLocalTexture(batch, White, null, new Vector2(2, UPPER_BAR_Y),
                    new Vector2(UpperFillWidth, UPPER_BAR_H), MeterColor(UpperScore));
            if (LowerFillWidth > 0)
                DrawLocalTexture(batch, White, null, new Vector2(2, LOWER_BAR_Y),
                    new Vector2(LowerFillWidth, LOWER_BAR_H), MeterColor(LowerScore));
            if (ShowSmiley && Smiley != null)
                DrawLocalTexture(batch, Smiley, null, new Vector2(SMILEY_X, MARKER_Y), Vector2.One);
            var heart = ShowDeepHeart ? DeepHeart : ShowHeart ? Heart : null;
            if (heart != null)
                DrawLocalTexture(batch, heart, null, new Vector2(HEART_X, MARKER_Y), Vector2.One);
            base.Draw(batch);
        }
    }
}
