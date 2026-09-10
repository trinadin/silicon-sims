using FSO.Client;
using FSO.Client.UI.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Model;
using System;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// The original cWinCamPanel camera-mode toolbar. The panel occupies the
    /// same 804x100 band as the other cWinCPanel children; PanelBack is owned
    /// by UIMainPanel, so this class mounts only the eight child buttons and
    /// the two painted separator strips.
    ///
    /// Geometry comes directly from UIText.iff STR# 140 coordinate entries
    /// and cWinCamPanel::TSPaint (0x2667a0): size controls at x=82, quality at
    /// x=154, Custom at (11,11), Album at (226,35), and the 2x89 toothpicks at
    /// (133,6)/(206,6). Every button follows SetImage(sheet, 4, 1).
    /// </summary>
    public class UIOriginalCameraPanel : UIContainer
    {
        public UIOriginalSheetButton CamFrameSmall;
        public UIOriginalSheetButton CamFrameMed;
        public UIOriginalSheetButton CamFrameLarge;
        public UIOriginalSheetButton CamFrameCust;
        public UIOriginalSheetButton CamViewAlbum;
        public UIOriginalSheetButton CamQualLow;
        public UIOriginalSheetButton CamQualMed;
        public UIOriginalSheetButton CamQualHigh;

        public UIOriginalSheetButton[] SizeButtons;
        public UIOriginalSheetButton[] QualityButtons;

        public Texture2D ToothpickLeft;
        public Texture2D ToothpickRight;

        /// CPState defaults both fields to Medium (enum value 1).
        public int SnapshotSize { get; private set; } = 1;
        public int SnapshotQuality { get; private set; } = 1;

        public event Action<int> OnSnapshotSizeChanged;
        public event Action<int> OnSnapshotQualityChanged;
        public event Action OnViewAlbum;

        public UIOriginalCameraPanel()
        {
            Size = new Vector2(804, 100);

            CamFrameSmall = CameraButton("cpanel\\Buttons\\CamFrameSmall.bmp", 82, 70, 1);
            CamFrameMed = CameraButton("cpanel\\Buttons\\CamFrameMed.bmp", 82, 36, 3);
            CamFrameLarge = CameraButton("cpanel\\Buttons\\CamFrameLarge.bmp", 82, 4, 5);
            CamFrameCust = CameraButton("cpanel\\Buttons\\CamFrameCust.bmp", 11, 11, 7);
            CamViewAlbum = CameraButton("cpanel\\Buttons\\CamViewAlbum.bmp", 226, 35, 9);
            CamQualLow = CameraButton("cpanel\\Buttons\\CamQualLow.bmp", 154, 70, 11);
            CamQualMed = CameraButton("cpanel\\Buttons\\CamQualMed.bmp", 154, 36, 13);
            CamQualHigh = CameraButton("cpanel\\Buttons\\CamQualHigh.bmp", 154, 4, 15);

            SizeButtons = new[] { CamFrameSmall, CamFrameMed, CamFrameLarge, CamFrameCust };
            QualityButtons = new[] { CamQualLow, CamQualMed, CamQualHigh };

            for (int i = 0; i < SizeButtons.Length; i++)
            {
                var index = i;
                SizeButtons[i].OnButtonClick += (b) => SelectSnapshotSize(index);
            }
            for (int i = 0; i < QualityButtons.Length; i++)
            {
                var index = i;
                QualityButtons[i].OnButtonClick += (b) => SelectSnapshotQuality(index);
            }
            CamViewAlbum.OnButtonClick += (b) => OnViewAlbum?.Invoke();

            ToothpickLeft = Resolve("cpanel\\Backgrounds\\CameraToothpkLeft.bmp");
            ToothpickRight = Resolve("cpanel\\Backgrounds\\CameraToothpkRight.bmp");

            RefreshSelection();
        }

        private UIOriginalSheetButton CameraButton(string member, int x, int y, int tooltipIndex)
        {
            var button = new UIOriginalSheetButton(member)
            {
                Position = new Vector2(x, y),
                Tooltip = GameFacade.Strings.GetString("140", tooltipIndex.ToString()),
            };
            Add(button);
            return button;
        }

        private static Texture2D Resolve(string member)
        {
            try { return UIOriginal.EnsureResolved(member)?.Get(GameFacade.GraphicsDevice); }
            catch { return null; }
        }

        public void SelectSnapshotSize(int size)
        {
            if (size < 0 || size >= SizeButtons.Length) return;
            SnapshotSize = size;
            RefreshSelection();
            OnSnapshotSizeChanged?.Invoke(size);
        }

        public void SelectSnapshotQuality(int quality)
        {
            if (quality < 0 || quality >= QualityButtons.Length) return;
            SnapshotQuality = quality;
            RefreshSelection();
            OnSnapshotQualityChanged?.Invoke(quality);
        }

        public void RefreshSelection()
        {
            if (SizeButtons != null)
                for (int i = 0; i < SizeButtons.Length; i++)
                    SizeButtons[i].State = (byte)(i == SnapshotSize ? 1 : 0);
            if (QualityButtons != null)
                for (int i = 0; i < QualityButtons.Length; i++)
                    QualityButtons[i].State = (byte)(i == SnapshotQuality ? 1 : 0);
            if (CamViewAlbum != null) CamViewAlbum.State = 0;
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;

            // cWinCamPanel::TSPaint draws the seams into the parent buffer
            // before cTSWin composes the child buttons.
            if (ToothpickLeft != null)
                DrawLocalTexture(batch, ToothpickLeft, null, new Vector2(133, 6), Vector2.One);
            if (ToothpickRight != null)
                DrawLocalTexture(batch, ToothpickRight, null, new Vector2(206, 6), Vector2.One);

            base.Draw(batch);
        }
    }
}
