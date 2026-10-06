using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Content;
using Microsoft.Xna.Framework;
using Simitone.Client.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.UI.Panels
{
    // Original cWinBudgetDlg / cWinBudgetRow law, independently recovered in
    // tools/iff-dump/r238-budget/. Twenty font-height rows, measured columns,
    // native PopupInfoTiles frame, and a separate 100px button footer.
    // SIM-09: value rows and the 3-day column derive from the household budget
    // ledger (VMTS1LotState Today/History reports fed by committed budget
    // transactions); salaries render as today's daily rates. Composition is
    // unchanged from the r238 restoration.
    public class UIOriginalBudgetDialog : UIContainer
    {
        public int BoardW { get; private set; }
        public int BoardH { get; private set; }
        public override Vector2 Size { get => new Vector2(BoardW, BoardH); set { } }
        public override Rectangle GetBounds() => new Rectangle(0, 0, BoardW, BoardH);
        public static readonly int[] RowLabelIdx = {3, -1, 6, 8, 10, -1, 12, 14, 16, 18, 20, 22, 24, 26, -1, 28, -1, 30, 34, 32};
        public static readonly int[] RowFontIndices = {16,10,12,10,10,10,12,10,10,10,10,9,9,10,10,12,12,10,10,10};
        public static readonly int[] RowIndents = {0,0,0,1,1,0,0,1,1,1,1,2,2,1,0,0,0,0,0,0};
        public static readonly HashSet<int> HeaderRows = new HashSet<int> {2,6,10};
        public const int ListX = 40, ListY = 25, RowIndent = 12, WidthMargin = 120;
        public const int OkCellW = 217, OkCellH = 52;
        // InitSimsColors applies this ink to every font-table slot; ordinary
        // cWinBudgetRow::TSPaint inherits it (only negative values override it).
        public static readonly Color RowTextColor = new Color(195, 205, 205);
        public static int DialogsMounted, RowsMounted, BudgetButtonOpens;
        public static int LastLabelW, LastCurW, LastDay3W;
        public static string LastAccountTotalText;
        public Simitone.Client.UI.Screens.TS1GameScreen Game;
        public UIOriginalNavbarButton OkButton;
        public UIOriginalText OkCaption;
        private readonly Vector2 OkCaptionOrigin;
        public readonly List<UIOriginalText> RowLabels = new List<UIOriginalText>();
        public readonly List<UIOriginalText> RowToday = new List<UIOriginalText>();
        public readonly List<UIOriginalText> Row3Day = new List<UIOriginalText>();
        public static bool CenteredRow(int i) => i == 0 || i >= 17;

        public UIOriginalBudgetDialog(Simitone.Client.UI.Screens.TS1GameScreen game)
        {
            Game = game;
            // AUD-17 B-12: native SetBlockSimulator — the sim pauses while the
            // budget is open (released in Removed).
            Simitone.Client.UI.Model.UIModalSimPause.Pause(this, game?.vm);
            var fam = game?.ActiveFamily;
            var fonts = RowFontIndices.Select(i => OriginalGlyphFont.LoadByIndex(i, GameFacade.GraphicsDevice)).ToArray();
            var labels = RowLabelIdx.Select(i => i < 0 ? "" : S146(i, "")).ToArray();
            var today = new string[20];
            var day3 = new string[20];
            today[1] = S146(5, "Today"); day3[1] = S146(4, "3 Days");
            var vals = ComputeValues();
            for (int i = 2; i <= 15; i++)
            {
                if (HeaderRows.Contains(i) || RowLabelIdx[i] < 0) continue;
                int v; vals.TryGetValue(i, out v);
                today[i] = "§" + v;
                // SIM-09: the 3-day column reads the history aggregate; rows the
                // ledger does not track keep the pre-ledger fallback.
                // AUD-17 B-1: the old `i == 3 || i == 15` guard rendered only
                // Job/Cash-Flow — the seven other rows ComputeValues stages in
                // day3Hist were dead stores and their column drew blank. The
                // r238 decode clears only heading rows (2/6/10, skipped above),
                // so EVERY value row carries both columns natively.
                int d3;
                day3[i] = "§" + (day3Hist.TryGetValue(i, out d3) ? d3 : v * 3);
            }
            labels[17] = labels[17].Replace("%s", "§" + (fam?.Budget ?? 0));
            labels[18] = labels[18].Replace("%s", "§" + ((fam?.Budget ?? 0) + (fam?.ValueInArch ?? 0)));
            // SIM-09: days since move-in is now tracked by the budget ledger.
            labels[19] = labels[19].Replace("%s", (FSO.SimAntics.Model.TS1Platform.VMTS1LotState.Active?.DaysRunning ?? 0).ToString());
            int labelW = 0, curW = 0, day3W = 0;
            for (int i = 0; i < 20; i++)
            {
                if (CenteredRow(i)) continue;
                labelW = Math.Max(labelW, fonts[i].Measure(labels[i]) + RowIndents[i] * RowIndent);
                curW = Math.Max(curW, fonts[i].Measure(today[i] ?? ""));
                day3W = Math.Max(day3W, fonts[i].Measure(day3[i] ?? ""));
            }
            LastLabelW = labelW; LastCurW = curW; LastDay3W = day3W;
            int rowW = labelW + curW + day3W + WidthMargin;
            BoardW = ListX + rowW + 40;
            int y = ListY;
            for (int i = 0; i < 20; i++)
            {
                var font = fonts[i];
                var label = new UIOriginalText(labels[i], font) {
                    Color = RowTextColor,
                    Position = new Vector2(CenteredRow(i) ? ListX + (rowW-font.Measure(labels[i]))/2 : ListX+30+RowIndents[i]*RowIndent, y),
                    Tooltip = RowLabelIdx[i] >= 6 ? S146(RowLabelIdx[i]+1, "") : null
                };
                RowLabels.Add(label); Add(label);
                if (today[i] != null) {
                    var text = new UIOriginalText(today[i], font) { Color = RowTextColor, Position = new Vector2(ListX+60+labelW, y) };
                    RowToday.Add(text); Add(text);
                }
                if (day3[i] != null) {
                    var text = new UIOriginalText(day3[i], font) { Color = RowTextColor, Position = new Vector2(ListX+90+labelW+curW, y) };
                    Row3Day.Add(text); Add(text);
                }
                y += font.LineHeight;
                RowsMounted++;
            }
            BoardH = y + 100;
            LastAccountTotalText = labels[17];
            OkButton = new UIOriginalNavbarButton("cpanel\\buttons\\budgetok.bmp", 4, 1, S146(2, "OK")) {
                Position = new Vector2((BoardW-OkCellW)/2, BoardH-100+(100-OkCellH)/2)
            };
            OkButton.OnButtonClick += b => Close();
            Add(OkButton);
            var okFont = OriginalGlyphFont.LoadByIndex(20, GameFacade.GraphicsDevice);
            OkCaption = new UIOriginalText(S146(2, "OK"), okFont) {
                Color = RowTextColor,
                Position = OkButton.Position + new Vector2((OkCellW-okFont.Measure(S146(2,"OK")))/2, okFont.ButtonCaptionY(OkCellH))
            };
            OkCaptionOrigin = OkCaption.Position;
            Add(OkCaption);
            DialogsMounted++;
            UpdatePosition();
        }

        public void UpdatePosition()
        {
            ScaleX = ScaleY = 1;
            X = (GlobalSettings.Default.GraphicsWidth-BoardW)/2;
            Y = (GlobalSettings.Default.GraphicsHeight-BoardH)/2;
        }
        public override void GameResized() { UpdatePosition(); }
        public void Close() { UIScreen.RemoveDialog(this); }

        public override void Removed()
        {
            Simitone.Client.UI.Model.UIModalSimPause.Resume(this); // AUD-17 B-12
            base.Removed();
        }
        public override void Update(FSO.Common.Rendering.Framework.Model.UpdateState state)
        {
            base.Update(state);
            // cTSWinBtn caption paint at 0x50ca34–80 selects the controller's
            // default four-state palette for SetImage(...,4,1).
            OkCaption.Color = OkButton.Disabled ? new Color(64, 93, 95)
                : OkButton.IsDown || OkButton.Selected ? Color.Cyan
                : OkButton.Hovered ? Color.White : RowTextColor;
            OkCaption.Position = OkCaptionOrigin + (OkButton.IsDown || OkButton.Selected ? new Vector2(2, 2) : Vector2.Zero);
            if (state.NewKeys.Contains(Microsoft.Xna.Framework.Input.Keys.Enter) || state.NewKeys.Contains(Microsoft.Xna.Framework.Input.Keys.Escape)) Close();
        }
        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            UIOriginalDialogChrome.DrawPictureWindow(this, batch, 0, 0, BoardW, BoardH);
            base.Draw(batch);
        }

        // Row values now derive from the SIM-09 household budget ledger
        // (VMTS1LotState.TodayReport/HistoryReport, fed by committed budget
        // transactions) — the composition is unchanged. Rows without a tracked
        // category (the two salary sub-rows 3/4 derive from family members'
        // daily job rates today; history aggregates only actual transactions)
        // render §0 as before.
        private Dictionary<int, int> ComputeValues()
        {
            var result = new Dictionary<int, int>();
            var fam = Game?.ActiveFamily;
            int salary = 0;
            try
            {
                var guids = fam?.FamilyGUIDs;
                var set = guids == null ? null : new HashSet<uint>(guids.Select(g => (uint)g));
                var vm = Game.vm;
                if (set != null && vm != null)
                {
                    foreach (var av in vm.Entities.OfType<FSO.SimAntics.VMAvatar>())
                    {
                        if (!set.Contains(av.PersistID)) continue;
                        var type = av.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobType);
                        var job = Content.Get().Jobs.GetJob((ushort)type);
                        if (job == null) continue;
                        var level = av.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.JobPromotionLevel);
                        var lv = (level >= 0 && level < job.JobLevels.Length) ? job.JobLevels[level] : null;
                        if (lv != null) salary += lv.Salary;
                    }
                }
            }
            catch { }

            var ledger = FSO.SimAntics.Model.TS1Platform.VMTS1LotState.Active;
            var today = ledger?.TodayReport;
            var hist = ledger?.HistoryReport;
            int todayCat(int cat) { return today == null ? 0 : today[cat]; }
            int histCat(int cat) { return hist == null ? 0 : hist[cat]; }
            int todaySum() { int s = 0; for (int c = 0; c < 8; c++) s += todayCat(c); return s; }
            int histSum() { int s = 0; for (int c = 0; c < 8; c++) s += histCat(c); return s; }
            int C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat cat) { return (int)cat; }

            // rows (r238-budget layout law): 3 Job, 4 Misc income, 7 Bills Paid,
            // 8 Food, 9 Repair/Cleaning/Gardening (service), 11 Household Items,
            // 12 Architecture/Landscaping, 13 Misc Expenses, 15 Cash Flow.
            // ORIG-01 D-4 (settled): the B-12 double-count conditional is
            // resolved — CarPortal.iff's salary writes carry ExpenseType 1
            // (pay) and 2 (bonus), neither IncomeJob(30), so adding today's
            // salary here does NOT double-count. Open residual: the native
            // byte→row attribution law (1/2 currently land in Misc via
            // VMTS1LotState) — carded.
            result[3] = todayCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.JobIncome)) + salary;
            result[4] = todayCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.MiscIncome));
            result[7] = todayCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.BillsExpense));
            result[8] = todayCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.FoodExpense));
            result[9] = todayCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.ServiceExpense));
            result[11] = todayCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.HouseholdExpense));
            result[12] = todayCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.ArchitectureExpense));
            result[13] = todayCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.MiscExpense));
            var todayIncome = todayCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.MiscIncome))
                + todayCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.JobIncome)) + salary;
            var todayExpense = todaySum() - todayIncome;
            result[15] = todayIncome - todayExpense;

            // 3-day column: the native history report (last 3 completed days).
            day3Hist[3] = histCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.JobIncome));
            day3Hist[4] = histCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.MiscIncome));
            day3Hist[7] = histCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.BillsExpense));
            day3Hist[8] = histCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.FoodExpense));
            day3Hist[9] = histCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.ServiceExpense));
            day3Hist[11] = histCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.HouseholdExpense));
            day3Hist[12] = histCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.ArchitectureExpense));
            day3Hist[13] = histCat(C(FSO.SimAntics.Model.TS1Platform.VMTS1LotState.BudgetCat.MiscExpense));
            var histIncome = day3Hist[4] + day3Hist[3];
            var histExpense = histSum() - histIncome;
            day3Hist[15] = histIncome - histExpense;
            return result;
        }

        // 3-day column values staged by ComputeValues for the row loop.
        private readonly Dictionary<int, int> day3Hist = new Dictionary<int, int>();

        // STR# 146 with the R159 literal-fallback idiom.
        private static string S146(int idx, string fallback)
        {
            var t = GameFacade.Strings.GetString("146", idx.ToString());
            if (string.IsNullOrEmpty(t) || t.Contains("MISSING")) return fallback;
            return t;
        }
    }
}
