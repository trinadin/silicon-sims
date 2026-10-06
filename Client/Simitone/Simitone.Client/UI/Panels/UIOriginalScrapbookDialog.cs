using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// R190: the original photo-album window (cWinScrapbook, template
    /// 1200) on the decoded law.
    ///
    /// cWinScrapbook::Init (0x29f5a0) loads template 0x4b0 (1200 =
    /// kScrapBack 700x560) and registers string-set 0x90 (STR# 144
    /// 'ScrapStrs'); the English block [0..17] carries every anchor,
    /// label, and tooltip consumed below. cWinScrapbook::TSOnCommand
    /// (0x29ef02) routes the six buttons to cScrapbook SetCurPage /
    /// DeleteCurrentPage / SaveScrapbook; delete is confirmed through
    /// cSimsApp::MessageDialog (0x253f00) with STR# 144 [20]/[21].
    ///
    /// Board members (UIGraphics.far, byte-verified R190):
    ///   cpanel\Backgrounds\scrapback.bmp  700x560
    ///   cpanel\Buttons\scrapdone.bmp      520x41  (4 x 130x41)
    ///   cpanel\Buttons\scrapdelete.bmp    520x41  (4 x 130x41)
    ///   cpanel\Buttons\scraphome.bmp      152x47  (4 x 38x47)
    ///   cpanel\Buttons\scrapleft.bmp      116x47  (4 x 29x47)
    ///   cpanel\Buttons\scrapright.bmp     112x46  (4 x 28x46)
    ///   cpanel\Buttons\scrapend.bmp       148x46  (4 x 37x46)
    ///
    /// STR# 144 anchors (panel-local): Done (129,512), Delete (442,511),
    /// Prev (58,442), Next (614,442), First (19,442), Last (643,442);
    /// photo centered on (350,220); editable caption at (90,435), 520x72;
    /// page filename label at (100,415), 450x20, hidden by original Init.
    /// </summary>
    public class UIOriginalScrapbookDialog : UIContainer
    {
        public const int BoardW = 700, BoardH = 560;
        public const int PhotoCenterX = 350, PhotoCenterY = 220;
        public const int CaptionX = 90, CaptionY = 435;
        public const int CaptionW = 520, CaptionH = 72;
        public const int FileNameX = 100, FileNameY = 415;
        public const int FileNameW = 450, FileNameH = 20;

        public override Vector2 Size { get => new Vector2(BoardW, BoardH); set { } }
        public override Rectangle GetBounds() => new Rectangle(0, 0, BoardW, BoardH);

        public UIOriginalSheetButton DoneBtn;
        public UIOriginalSheetButton DeleteBtn;
        public UIOriginalSheetButton FirstBtn;
        public UIOriginalSheetButton PrevBtn;
        public UIOriginalSheetButton NextBtn;
        public UIOriginalSheetButton LastBtn;
        public UIOriginalTextEdit Caption;
        public UIOriginalText FileNameLabel;
        public UIOriginalText EmptyLabel;
        public UIOriginalText DoneCaption => ButtonCaptions[DoneBtn];
        public UIOriginalText DeleteCaption => ButtonCaptions[DeleteBtn];

        public static int DialogsMounted;

        private Texture2D Board;
        private bool ConfirmOpen;
        internal UIMobileAlert DeleteConfirmation { get; private set; }
        private readonly Dictionary<UIOriginalSheetButton, UIOriginalText> ButtonCaptions =
            new Dictionary<UIOriginalSheetButton, UIOriginalText>();

        public UIOriginalScrapbookDialog()
        {
            UpdatePosition();

            var font = OriginalGlyphFont.LoadDefault(GameFacade.GraphicsDevice);

            Board = UIOriginal.EnsureResolved("cpanel\\backgrounds\\scrapback.bmp")?.Get(GameFacade.GraphicsDevice);

            DoneBtn = Sheet("cpanel\\buttons\\scrapdone.bmp", 129, 512, 130, 41,
                S144(1, "Done"), S144(2, "Close Photo Album"), font);
            DeleteBtn = Sheet("cpanel\\buttons\\scrapdelete.bmp", 442, 511, 130, 41,
                S144(4, "Delete"), S144(5, "Delete this photo"), font);
            FirstBtn = Sheet("cpanel\\buttons\\scraphome.bmp", 19, 442, 38, 47, null,
                S144(11, "First Photo"), font);
            PrevBtn = Sheet("cpanel\\buttons\\scrapleft.bmp", 58, 442, 29, 47, null,
                S144(7, "Previous Photo"), font);
            NextBtn = Sheet("cpanel\\buttons\\scrapright.bmp", 614, 442, 28, 46, null,
                S144(9, "Next Photo"), font);
            LastBtn = Sheet("cpanel\\buttons\\scrapend.bmp", 643, 442, 37, 46, null,
                S144(13, "Last Photo"), font);

            DoneBtn.OnButtonClick += (b) => Close();
            DeleteBtn.OnButtonClick += (b) => ConfirmDelete();
            FirstBtn.OnButtonClick += (b) => { Go(0); };
            PrevBtn.OnButtonClick += (b) => { Go(OriginalSnapshotAlbum.CurrentPage - 1); };
            NextBtn.OnButtonClick += (b) => { Go(OriginalSnapshotAlbum.CurrentPage + 1); };
            LastBtn.OnButtonClick += (b) => { Go(OriginalSnapshotAlbum.Pages.Count - 1); };

            // cWinScrapbook::Init creates a transparent cTSWinTextEdit2
            // with font[10], unlimited lines and capacity 0x1000. STR# 144
            // [14] supplies the size and [15] supplies its origin.
            Caption = new UIOriginalTextEdit(
                OriginalGlyphFont.LoadByIndex(10, GameFacade.GraphicsDevice), CaptionW, CaptionH)
            {
                Capacity = 4096,
                Position = new Vector2(CaptionX, CaptionY)
            };
            Add(Caption);

            // Init @0x29fcd4..0x29fce0 explicitly hides this label; Update
            // replaces its text without making it visible.
            FileNameLabel = new UIOriginalText("", OriginalGlyphFont.LoadByIndex(8, GameFacade.GraphicsDevice))
            {
                Position = new Vector2(FileNameX, FileNameY),
                Size = new Vector2(FileNameW, FileNameH),
                Visible = false
            };
            Add(FileNameLabel);

            EmptyLabel = new UIOriginalText(S144(22, "Empty Photo Album"), font)
            {
                Position = new Vector2(PhotoCenterX - 60, PhotoCenterY - 7)
            };
            Add(EmptyLabel);

            DialogsMounted++;
            Refresh();
        }

        private UIOriginalSheetButton Sheet(string member, int x, int y, int cellW, int cellH,
            string label, string tooltip, OriginalGlyphFont font)
        {
            var b = new UIOriginalSheetButton(member)
            {
                Position = new Vector2(x, y),
                Tooltip = tooltip
            };
            Add(b);
            // UIButton is not a container: the Done/Delete captions are
            // dialog children centered on the button's first cell.
            if (label != null)
            {
                var text = new UIOriginalText(label, font)
                {
                    Color = new Color(195, 205, 205),
                    Position = new Vector2(x + (cellW - font.Measure(label)) / 2,
                        y + font.ButtonCaptionY(cellH))
                };
                ButtonCaptions.Add(b, text);
                Add(text);
            }
            return b;
        }

        /// <summary>STR# 144 'ScrapStrs' English block getter (set 0x90
        /// registered by Init @0x29f6bc).</summary>
        public static string S144(int idx, string fallback)
        {
            try
            {
                var s = GameFacade.Strings.GetString("144", idx.ToString());
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            catch { }
            return fallback;
        }

        internal void Go(int page)
        {
            if (OriginalSnapshotAlbum.Pages.Count == 0 || ConfirmOpen) return;
            // Navigation commits the outgoing page in memory. The original
            // calls SaveScrapbook only on Done or confirmed Delete.
            OriginalSnapshotAlbum.SetCurrentDescription(Caption.Text, false);
            if (page < 0) page = 0;
            if (page > OriginalSnapshotAlbum.Pages.Count - 1) page = OriginalSnapshotAlbum.Pages.Count - 1;
            OriginalSnapshotAlbum.CurrentPage = page;
            Refresh();
        }

        internal void ConfirmDelete()
        {
            if (OriginalSnapshotAlbum.Pages.Count == 0 || ConfirmOpen) return;
            ConfirmOpen = true;
            // Native MessageDialog is modal. The port continues updating the
            // underlying tree, so explicitly suspend caption input until its
            // Yes/No callback completes.
            Caption.Blur();
            Caption.Disabled = true;
            // cSimsApp::MessageDialog(title, message, flags, bool) — the port's
            // original-law confirm surface (same chrome as the STR# 133
            // empty-lot refusal, R174).
            DeleteConfirmation = new UIMobileAlert(new UIAlertOptions()
            {
                Title = S144(20, "Delete Photo?"),
                Message = S144(21, "Are you sure you want to delete this photo?"),
                Buttons = UIAlertButton.YesNo(
                    (b) => ResolveDeleteConfirmation(true),
                    (b) => ResolveDeleteConfirmation(false))
            });
            UIScreen.GlobalShowDialog(DeleteConfirmation, true);
        }

        internal void ResolveDeleteConfirmation(bool delete)
        {
            if (!ConfirmOpen) return;
            DeleteConfirmation.Close();
            DeleteConfirmation = null;
            ConfirmOpen = false;
            if (delete)
            {
                // TSOnCommand: DeleteCurrentPage + SaveScrapbook.
                OriginalSnapshotAlbum.DeleteCurrent();
                if (OriginalSnapshotAlbum.Pages.Count == 0) { Dismiss(); return; }
            }
            // Native No also calls Update (0x29f058..0x29f05c), restoring
            // the stored caption and clearing its pending edits/undo buffer.
            Refresh();
        }

        public void Refresh()
        {
            var count = OriginalSnapshotAlbum.Pages.Count;
            var has = count > 0;
            var page = has ? OriginalSnapshotAlbum.Pages[OriginalSnapshotAlbum.CurrentPage] : null;

            EmptyLabel.Visible = !has;
            Caption.Visible = has;
            Caption.Disabled = !has;
            Caption.Text = has ? OriginalSnapshotAlbum.CurrentDescription() : "";
            // Original Update sets the stored filename on the hidden label;
            // STR#144[19] is its tooltip, not a filename format.
            FileNameLabel.Text = has ? page.FileName : "";

            // cScrapbook::Update (0x29ec40) disables the navigation and
            // delete controls when no page exists or an edge is reached.
            DeleteBtn.Disabled = !has;
            FirstBtn.Disabled = !has || OriginalSnapshotAlbum.CurrentPage == 0;
            PrevBtn.Disabled = FirstBtn.Disabled;
            LastBtn.Disabled = !has || OriginalSnapshotAlbum.CurrentPage == count - 1;
            NextBtn.Disabled = LastBtn.Disabled;
        }

        public void Close()
        {
            if (ConfirmOpen) return;
            OriginalSnapshotAlbum.SetCurrentDescription(Caption.Text, false);
            OriginalSnapshotAlbum.Save();
            Dismiss();
        }

        private void Dismiss()
        {
            Caption.Blur();
            UIScreen.RemoveDialog(this);
        }

        public void UpdatePosition()
        {
            ScaleX = ScaleY = 1;
            X = (GlobalSettings.Default.GraphicsWidth - BoardW) / 2;
            Y = (GlobalSettings.Default.GraphicsHeight - BoardH) / 2;
        }

        public override void GameResized()
        {
            UpdatePosition();
            base.GameResized();
        }

        public override void Update(FSO.Common.Rendering.Framework.Model.UpdateState state)
        {
            base.Update(state);
            // ORIG-02 scrapbook key law (symbol census): cWinScrapbook
            // registers NO key handler natively (methods are TSEndModal/
            // TSBeginModal/Update/TSOnCommand/TSPaint/Shutdown only) — ESC
            // is INERT; closing is the Done/Delete buttons (TSOnCommand).
            // The B-11 ESC=Done convenience is retired for native parity.
            foreach (var entry in ButtonCaptions)
            {
                var button = entry.Key;
                var text = entry.Value;
                var entered = ((UIButton)button).Hovered;
                var active = button.State != 0 || button.IsDown;
                text.Color = button.Disabled ? new Color(64, 93, 95)
                    : active ? Color.Cyan : entered ? Color.White : new Color(195, 205, 205);
                // Init @0x29f854..0x29f884 overrides the four caption
                // offsets with [(0,0),(2,2),(0,0),(0,0)]. CalcRowCol's
                // four-column path maps active to state 1 regardless of hover.
                var offset = !button.Disabled && active ? 2 : 0;
                text.Position = button.Position + new Vector2(
                    (button.CellWidth - text.Font.Measure(text.Text)) / 2 + offset,
                    text.Font.ButtonCaptionY((int)button.Size.Y) + offset);
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            if (Board != null) DrawLocalTexture(batch, Board, new Rectangle(0, 0, Board.Width, Board.Height), Vector2.Zero);

            // cWinScrapbook::TSPaint (0x29f1a0): the current page's photo is
            // drawn centered on the (350,220) anchor at its captured size.
            var count = OriginalSnapshotAlbum.Pages.Count;
            if (count > 0)
            {
                var page = OriginalSnapshotAlbum.Pages[OriginalSnapshotAlbum.CurrentPage];
                if (page.Photo != null)
                {
                    var pos = new Vector2(PhotoCenterX - page.Photo.Width / 2f, PhotoCenterY - page.Photo.Height / 2f);
                    DrawLocalTexture(batch, page.Photo, new Rectangle(0, 0, page.Photo.Width, page.Photo.Height), pos);
                }
            }

            base.Draw(batch);
        }
    }
}
