using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.LotView.Model;
using FSO.Common.Rendering.Framework.Model;
using FSO.Content;
using FSO.SimAntics;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// Original CWinPhoneBook, recovered again in R238 against the full symbols.
    /// The two children are cTSWinTextList, not cTSWinText: ten visible rows,
    /// font-table slot12, row pitch font height+3, and a navy list background.
    /// The alleged dropdown is the DefaultLabel displaying the call target.
    /// Board, label, buttons, portrait and list anchors follow Init/TSPaint.
    /// See tools/iff-dump/r238-phonebook/corrected-law.md.
    /// </summary>
    public class UIOriginalPhoneBookDialog : UIContainer
    {
        public const int WindowW = 652, WindowH = 426;
        public override Vector2 Size { get => new Vector2(WindowW, WindowH); set { } }
        public override Rectangle GetBounds() => new Rectangle(0, 0, WindowW, WindowH);
        public const int BoardW = 620, BoardH = 356, BoardX = 17, BoardY = 8;
        public const int IconX = (WindowW - 23) / 2, IconY = WindowH - 49 + 5;
        public const int FamListL = 34, FamListT = 77, MemListL = 351, MemListT = 77;
        public const int ListWidth = 280, VisibleListRows = 10;
        public const int HeaderX = 75, HeaderY = 16, HeaderW = 540, HeaderH = 25;
        public const string FrameMember = "cpanel\\Backgrounds\\PopupInfoTiles.bmp";
        public const int FrameCell = 12;
        public static readonly Color ListBackground = new Color(0, 0, 57);
        public static int DialogsMounted, FamilyRowsMounted, MemberRowsMounted, CallClicks;
        public readonly Dictionary<short, List<short>> NeighborsByFamilyID = new Dictionary<short, List<short>>();
        public int SelectedFamily = -1;
        public short SelectedNeighbour = -1;
        public VM VM;
        public short CallerNID;
        public event Action<int> OnResult;
        public UIBigButton CallButton, CancelButton;
        public UIOriginalTextList FamilyList, MemberList;
        public List<UIOriginalText> FamilyRows => FamilyList.Rows;
        public List<UIOriginalText> MemberRows => MemberList.Rows;
        public UIOriginalText EmptyText, StripLabel;
        private readonly List<short> FamilyIDs = new List<short>();
        private readonly List<short> MemberIDs = new List<short>();
        private readonly List<string> FamilyNames = new List<string>();
        private readonly Texture2D Board, Icon;
        private Texture2D Portrait;
        private readonly string CallCaption, EmptyCaption;
        private readonly UIOriginalText CallLabel, CancelLabel;
        private readonly Vector2 CallLabelOrigin, CancelLabelOrigin;
        private bool UpdatingSelection;

        public UIOriginalPhoneBookDialog(short callerNID, VM vm)
        {
            VM = vm;
            CallerNID = callerNID;
            Size = new Vector2(WindowW, WindowH);
            UpdatePosition();
            Board = UIOriginal.EnsureResolved("cpanel\\backgrounds\\phonebookbkg.bmp")?.Get(GameFacade.GraphicsDevice);
            Icon = UIOriginal.EnsureResolved("cpanel\\backgrounds\\phoneicon.bmp")?.Get(GameFacade.GraphicsDevice);
            CallCaption = Text180(0, "Call");
            EmptyCaption = Text180(2, "doesn't know anyone.");
            var font = OriginalGlyphFont.LoadByIndex(12, GameFacade.GraphicsDevice);
            var systemFont = OriginalGlyphFont.LoadByIndex(11, GameFacade.GraphicsDevice);
            // DefaultPushBtn has a minimum width100; Init positions the pair at
            // opposite sides, with y=H-floor(3*buttonHeight/2).
            CallButton = new UIBigButton(false) { Caption = "", Width = 100,
                Position = new Vector2(20, WindowH - 49), Tooltip = CallCaption };
            CancelButton = new UIBigButton(false) { Caption = "", Width = 100,
                Position = new Vector2(WindowW - 120, WindowH - 49) };
            Add(CallButton);
            Add(CancelButton);
            var cancel = GameFacade.Strings.GetString("142", "1");
            if (string.IsNullOrEmpty(cancel)) cancel = "Cancel";
            CallLabel = ButtonLabel(CallCaption, CallButton, systemFont);
            CancelLabel = ButtonLabel(cancel, CancelButton, systemFont);
            CallLabelOrigin = CallLabel.Position;
            CancelLabelOrigin = CancelLabel.Position;
            CallButton.OnButtonClick += _ => Call();
            CancelButton.OnButtonClick += _ => OnResult?.Invoke(-1);
            StripLabel = new UIOriginalText("", systemFont)
            { Position = new Vector2(HeaderX, HeaderY), Size = new Vector2(HeaderW, HeaderH),
                Color = new Color(195, 205, 205) };
            EmptyText = StripLabel;
            Add(StripLabel);
            FamilyList = new UIOriginalTextList(font, ListWidth, VisibleListRows)
            { Position = new Vector2(FamListL, FamListT), KeyboardActive = true };
            MemberList = new UIOriginalTextList(font, ListWidth, VisibleListRows)
            { Position = new Vector2(MemListL, MemListT) };
            FamilyList.OnSelectionChange += SelectFamily;
            MemberList.OnSelectionChange += i => SelectMember(i >= 0 && i < MemberIDs.Count ? MemberIDs[i] : (short)-1, i);
            MemberList.OnItemActivate += _ => Call();
            Add(FamilyList);
            Add(MemberList);
            var nb = Content.Get().Neighborhood;
            var caller = nb.GetNeighborByID(callerNID);
            if (caller != null)
            {
                short callerFamily = caller.PersonData?.ElementAt((int)VMPersonDataVariable.TS1FamilyNumber) ?? 0;
                foreach (var relation in caller.Relationships.Keys)
                {
                    var neighbor = nb.GetNeighborByID((short)relation);
                    if (neighbor == null) continue;
                    short family = neighbor.PersonData?.ElementAt((int)VMPersonDataVariable.TS1FamilyNumber) ?? 0;
                    short gender = neighbor.PersonData?.ElementAt((int)VMPersonDataVariable.Gender) ?? 0;
                    if (family == 0 || family == callerFamily || gender > 1) continue;
                    List<short> people;
                    if (!NeighborsByFamilyID.TryGetValue(family, out people))
                        NeighborsByFamilyID.Add(family, people = new List<short>());
                    people.Add((short)relation);
                }
            }
            foreach (var family in NeighborsByFamilyID.OrderBy(x => x.Key))
            {
                FamilyIDs.Add(family.Key);
                FamilyNames.Add(nb.GetFamilyString((ushort)family.Key)?.GetString(0) ?? "?");
            }
            FamilyList.SetItems(FamilyNames);
            FamilyRowsMounted += FamilyIDs.Count;
            // Original Init auto-selects first family/member if available.
            SelectFamily(FamilyIDs.Count > 0 ? 0 : -1);
            DialogsMounted++;
        }

        private UIOriginalText ButtonLabel(string caption, UIBigButton button, OriginalGlyphFont font)
        {
            var text = new UIOriginalText(caption, font)
            { Position = button.Position + new Vector2((100 - font.Measure(caption)) / 2, font.ButtonCaptionY(33)),
                Color = new Color(195, 205, 205) };
            Add(text);
            return text;
        }

        private static string Text180(int index, string fallback)
        {
            try { return GameFacade.Strings.GetString("180", index.ToString()) ?? fallback; }
            catch { return fallback; }
        }

        private static string NeighborName(short id)
        {
            var neighbor = Content.Get().Neighborhood.GetNeighborByID(id);
            var obj = neighbor == null ? null : Content.Get().WorldObjects.Get(neighbor.GUID);
            return obj?.Resource.Get<FSO.Files.Formats.IFF.Chunks.CTSS>(obj.OBJ.CatalogStringsID)?.GetString(0) ?? "?";
        }

        public void SelectFamily(int index)
        {
            if (UpdatingSelection) return;
            UpdatingSelection = true;
            try
            {
                SelectedFamily = index >= 0 && index < FamilyIDs.Count ? index : -1;
                FamilyList.Select(SelectedFamily);
                MemberIDs.Clear();
                if (SelectedFamily >= 0) MemberIDs.AddRange(NeighborsByFamilyID[FamilyIDs[SelectedFamily]]);
                MemberList.SetItems(MemberIDs.Select(NeighborName));
                MemberRowsMounted += MemberIDs.Count;
                SelectMember(MemberIDs.Count > 0 ? MemberIDs[0] : (short)-1, MemberIDs.Count > 0 ? 0 : -1);
            }
            finally { UpdatingSelection = false; }
        }

        public void SelectMember(short id, int rowIndex)
        {
            bool valid = rowIndex >= 0 && rowIndex < MemberIDs.Count && MemberIDs[rowIndex] == id;
            SelectedNeighbour = valid ? id : (short)-1;
            if (MemberList.SelectedIndex != (valid ? rowIndex : -1)) MemberList.Select(valid ? rowIndex : -1);
            CallButton.Disabled = !valid;
            StripLabel.Text = valid ? Text180(0, "Call") + " " + NeighborName(id) + " " + FamilyNames[SelectedFamily] + "."
                : NeighborName(CallerNID) + " " + EmptyCaption;
            ResolvePortrait(valid ? id : CallerNID);
        }

        private void ResolvePortrait(short neighborID)
        {
            Portrait = null;
            if (VM == null) return;
            VMMultitileGroup temporary = null;
            try
            {
                var avatar = VM.Context.ObjectQueries.Avatars.OfType<VMAvatar>()
                    .FirstOrDefault(x => x.GetPersonData(VMPersonDataVariable.NeighborId) == neighborID);
                if (avatar == null)
                {
                    var neighbor = Content.Get().Neighborhood.GetNeighborByID(neighborID);
                    if (neighbor == null) return;
                    temporary = VM.Context.CreateObjectInstance(neighbor.GUID,
                        LotTilePos.OUT_OF_WORLD, Direction.NORTH, true);
                    avatar = temporary?.BaseObject as VMAvatar;
                }
                if (avatar != null) Portrait = UIIconCache.GetObject(avatar);
            }
            finally { temporary?.Delete(VM.Context); }
        }

        private void Call()
        {
            if (SelectedNeighbour < 0 || CallButton.Disabled) return;
            CallClicks++;
            OnResult?.Invoke(SelectedNeighbour);
        }

        public void Close() { UIScreen.RemoveDialog(this); }
        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (state.NewKeys.Contains(Keys.Escape)) OnResult?.Invoke(-1);
            else if (state.NewKeys.Contains(Keys.Enter)) Call();
            CallLabel.Color = ButtonInk(CallButton);
            CancelLabel.Color = ButtonInk(CancelButton);
            CallLabel.Position = CallLabelOrigin + (CallButton.IsDown ? new Vector2(2, 2) : Vector2.Zero);
            CancelLabel.Position = CancelLabelOrigin + (CancelButton.IsDown ? new Vector2(2, 2) : Vector2.Zero);
        }
        private static Color ButtonInk(UIBigButton button) => button.Disabled ? new Color(64, 93, 95)
            : button.IsDown ? Color.Cyan : button.Hovered ? Color.White : new Color(195, 205, 205);
        public void UpdatePosition()
        {
            var screen = GameFacade.Screens.CurrentUIScreen;
            Position = new Vector2((screen.ScreenWidth - WindowW) / 2f, (screen.ScreenHeight - WindowH) / 2f);
        }
        public override void GameResized() { UpdatePosition(); base.GameResized(); }
        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            UIOriginalDialogChrome.DrawPictureWindow(this, batch, 0, 0, WindowW, WindowH);
            if (Board != null) DrawLocalTexture(batch, Board, new Vector2(BoardX, BoardY));
            if (Icon != null) DrawLocalTexture(batch, Icon, new Vector2(IconX, IconY));
            if (Portrait != null) DrawLocalTexture(batch, Portrait, null, new Vector2(10), new Vector2(45f / Portrait.Width, 45f / Portrait.Height));
            base.Draw(batch);
        }
    }
}
