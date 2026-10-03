using FSO.SimAntics.Model.Platform;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics.Primitives;
using FSO.SimAntics.Utils;

namespace FSO.SimAntics.Model.TS1Platform
{
    public class VMTS1LotState : VMAbstractLotState
    {
        public SIMI SimulationInfo;
        public FAMI CurrentFamily;
        public short TutorialObjectID;
        /// <summary>
        /// ENG-05 mode-43 latch: the ChunkID of the family whose spell
        /// inventory generic call 43 (Family::LoadSpellsForFamily, PPC
        /// 0x75b70) has loaded. The native loads idempotently into the
        /// global spell list the spellbook reads; the port's list
        /// population into the spellbook data source is the named follow-up
        /// (the CWinMagicBook availability card) — the latch records the
        /// load so the two MagicMasterSpells callers' boolean contract is
        /// exact. Transient, never serialized.
        /// </summary>
        public int FamilySpellsLoadedFor = 0;
        /// <summary>
        /// The residential house number this lot was loaded from — the port's
        /// equivalent of the native Neighborhood+0x164 "current house" field
        /// (r247). Transient: re-derived on every house load by
        /// VMTS1ActivatorNew, never serialized.
        /// </summary>
        public short CurrentHouse;
        // Transient report from an original-IFF import. These continuations were incompatible
        // with the currently mounted expansion resources and were reset before the first tick.
        // It is intentionally absent from save/network serialization.
        public short[] SanitizedThreadObjectIDs = new short[0];

        // SIM-09: household budget ledger. Category order follows the native
        // SIMI.SIMIBudgetDay day record (r252 decode/SIMI.cs reader), which the
        // original Budget dialog consumed via cSimulator::GetTodaysExpenses /
        // GetExpensesHistory: an 8-int report for "Today" and an 8-int aggregate
        // for the 3-day history column. Accumulators are positive magnitudes;
        // Cash Flow = income sum - expense sum.
        public const int BudgetCatCount = 8;
        public static readonly string[] BudgetCategories = new string[] {
            "MiscIncome", "JobIncome", "ServiceExpense", "FoodExpense",
            "BillsExpense", "MiscExpense", "HouseholdExpense", "ArchitectureExpense" };
        public int[] TodayReport = new int[BudgetCatCount];
        public int[] HistoryReport = new int[BudgetCatCount];
        private int[][] DayRing = new int[3][];
        private int DayRingCount;
        private int LastClockHour = -1;
        public int DaysRunning;
        /// <summary>The lot state of the VM that ticked last — the read path for
        /// UI surfaces (Budget dialog) outside the VM stack.</summary>
        public static VMTS1LotState Active;

        // TRV-02: mirror of TS1GameState's travel transit fields, persisted with
        // the lot so "save away → quit → reload on the destination lot" keeps the
        // return path (LotTransitInfo ≥ 1 re-activates the whole family on
        // return; DowntownSimGUID is the control-sim handle). In memory the live
        // copy remains Content Neighborhood GameState.
        public short LotTransitInfo;
        public uint DowntownSimGUID;
        private bool TransitRestored;

        /// <summary>
        /// AUD-03/SIM-09 note: category indices into TodayReport/HistoryReport,
        /// in native SIMI day-record order.
        /// </summary>
        public enum BudgetCat { MiscIncome = 0, JobIncome = 1, ServiceExpense = 2, FoodExpense = 3,
            BillsExpense = 4, MiscExpense = 5, HouseholdExpense = 6, ArchitectureExpense = 7 }

        /// <summary>
        /// Record a committed budget mutation into the Today report. Positive
        /// deltas are income, negative deltas are expenditures (magnitude
        /// accumulated). ExpenseType mappings follow the STR# 146 category
        /// semantics ("Job bonuses and windfalls", "paid to service people",
        /// etc.); unmapped kinds fall back to sign-based Miscellaneous.
        /// </summary>
        public void RecordTransaction(int budgetDelta, Primitives.VMTransferFundsExpenseType type)
        {
            if (budgetDelta == 0) return;
            var cat = BudgetCat.MiscExpense;
            var income = budgetDelta > 0;
            switch (type)
            {
                case Primitives.VMTransferFundsExpenseType.IncomeJob:
                case Primitives.VMTransferFundsExpenseType.IncomeRobotJob:
                case Primitives.VMTransferFundsExpenseType.IncomeRestaurantJob:
                case Primitives.VMTransferFundsExpenseType.IncomeClubJob:
                    cat = BudgetCat.JobIncome; break;
                case Primitives.VMTransferFundsExpenseType.ExpenseNPC:
                case Primitives.VMTransferFundsExpenseType.ExpenseNPCGardener:
                case Primitives.VMTransferFundsExpenseType.ExpenseNPCMaid:
                case Primitives.VMTransferFundsExpenseType.ExpenseNPCRepairman:
                case Primitives.VMTransferFundsExpenseType.ExpenseNPCButler:
                    cat = BudgetCat.ServiceExpense; break;
                case Primitives.VMTransferFundsExpenseType.ExpenseFridgeRefill:
                case Primitives.VMTransferFundsExpenseType.ExpenseFoodCounter:
                case Primitives.VMTransferFundsExpenseType.ExpenseBuffet:
                    cat = BudgetCat.FoodExpense; break;
                case Primitives.VMTransferFundsExpenseType.ExpenseFromObjects:
                case Primitives.VMTransferFundsExpenseType.ExpenseObjectRefillsAndMaintinance:
                    cat = BudgetCat.HouseholdExpense; break;
                case Primitives.VMTransferFundsExpenseType.IncomeFromObjects:
                case Primitives.VMTransferFundsExpenseType.IncomeEasel:
                case Primitives.VMTransferFundsExpenseType.IncomeCheat:
                case Primitives.VMTransferFundsExpenseType.IncomeMisc:
                    cat = BudgetCat.MiscIncome; break;
                default:
                    cat = income ? BudgetCat.MiscIncome : BudgetCat.MiscExpense; break;
            }
            if ((int)cat < 0 || (int)cat >= BudgetCatCount) return;
            if (!income && (int)cat <= (int)BudgetCat.JobIncome)
            {
                // an expense landing on an income slot would corrupt the sign law
                cat = BudgetCat.MiscExpense;
            }
            TodayReport[(int)cat] += Math.Abs(budgetDelta);
        }

        /// <summary>
        /// Advance the accounting day: Today joins the 3-day history ring and
        /// resets; the aggregate history report is recomputed. Called from Tick
        /// on a clock-day wrap and exposed for deterministic tests.
        /// </summary>
        public void RollDay()
        {
            var oldest = DayRingCount < 3 ? null : DayRing[0];
            if (oldest != null)
            {
                for (int i = 0; i < BudgetCatCount; i++) HistoryReport[i] -= oldest[i];
            }
            for (int i = Math.Min(DayRingCount, 2); i > 0; i--) DayRing[i] = DayRing[i - 1];
            DayRing[0] = TodayReport;
            if (DayRingCount < 3) DayRingCount++;
            for (int i = 0; i < BudgetCatCount; i++) HistoryReport[i] += DayRing[0][i];
            TodayReport = new int[BudgetCatCount];
            DaysRunning++;
        }

        public VMTS1LotState() : base() { }
        public VMTS1LotState(int version) : base(version) { }

        public void ActivateFamily(VM vm, FAMI family)
        {
            if (family == null) return;
            vm.SetGlobalValue(9, (short)family.ChunkID);
            CurrentFamily = family;
        }

        /// <summary>
        /// Ensure all members of the family are present on the lot.
        /// Spawns missing family members at the mailbox.
        /// </summary>
        public void VerifyFamily(VM vm)
        {
            if (CurrentFamily == null)
            {
                vm.SetGlobalValue(32, 1);
                return;
            }
            vm.SetGlobalValue(32, 0);
            vm.SetGlobalValue(9, (short)CurrentFamily.ChunkID);
            var missingMembers = new HashSet<uint>(CurrentFamily.RuntimeSubset);
            foreach (var avatar in vm.Context.ObjectQueries.Avatars)
            {
                missingMembers.Remove(avatar.Object.OBJ.GUID);
            }

            int unresolvable = 0;
            foreach (var member in missingMembers)
            {
                // SAV-11: a dead family member must not re-spawn; the tombstone stands.
                if (IsDeadInStore(member)) continue;
                // EXP-09 (defect banked in coordination/evidence/EXP-09/
                // exp09-io-runtime-20260924.md): an imported family member whose
                // template does not resolve must SKIP, not crash the house load
                // (NullReferenceException on Objects[0] when CreateObjectInstance
                // returns no group — reproduced with the imported Strays_4000
                // family). Bounded log so missing members stay diagnosable.
                var group = vm.Context.CreateObjectInstance(member, LotView.Model.LotTilePos.OUT_OF_WORLD, LotView.Model.Direction.NORTH);
                if (group == null || group.Objects == null || group.Objects.Count == 0)
                {
                    if (unresolvable++ < 20)
                        Console.WriteLine("[FAMILY-SKIP] member 0x" + member.ToString("x8") + " did not resolve (imported/gone template)");
                    continue;
                }
                var sim = group.Objects[0];
                ((VMAvatar)sim).SetPersonData(VMPersonDataVariable.TS1FamilyNumber, (short)CurrentFamily.ChunkID);
                var mailbox = vm.Entities.FirstOrDefault(x => (x.Object.OBJ.GUID == 0xEF121974 || x.Object.OBJ.GUID == 0x1D95C9B0));
                if (mailbox != null) VMFindLocationFor.FindLocationFor(sim, mailbox, vm.Context, VMPlaceRequestFlags.Default);
                ((VMAvatar)sim).AvatarState.Permissions = Model.TSOPlatform.VMTSOAvatarPermissions.Owner;

                vm.Scheduler.RescheduleInterrupt(sim);
            }

        }


        public override void Deserialize(BinaryReader reader)
        {
            if (reader.ReadBoolean())
            {
                SimulationInfo = new SIMI() { ChunkID = 1, ChunkLabel = "", ChunkType = "SIMI" };
                SimulationInfo.Read(null, reader.BaseStream);
                RestoreLedgerFromSIMI();
            }

            //this is really only here for future networking. families should be activated (see abover) when joining lots for the first time
            var famID = reader.ReadUInt16();
            if (famID < 65535)
            {
                CurrentFamily = new FAMI() { ChunkID = famID, ChunkLabel = "", ChunkType = "FAMI" };
                CurrentFamily.Read(null, reader.BaseStream);
            }
            TutorialObjectID = Version >= 39 ? reader.ReadInt16() : (short)0;
            if (Version >= 40)
            {
                for (int i = 0; i < BudgetCatCount; i++) TodayReport[i] = reader.ReadInt32();
                for (int i = 0; i < BudgetCatCount; i++) HistoryReport[i] = reader.ReadInt32();
                DaysRunning = reader.ReadInt32();
                LastClockHour = reader.ReadInt32();
                LotTransitInfo = reader.ReadInt16();
                DowntownSimGUID = reader.ReadUInt32();
            }
            Active = this;
        }

        public override void SerializeInto(BinaryWriter writer)
        {
            writer.Write(SimulationInfo != null);
            MirrorLedgerIntoSIMI();
            SimulationInfo?.Write(null, writer.BaseStream);
            writer.Write(CurrentFamily?.ChunkID ?? 65535);
            if (CurrentFamily != null) CurrentFamily.Write(null, writer.BaseStream);
            writer.Write(TutorialObjectID);
            if (Version >= 40)
            {
                for (int i = 0; i < BudgetCatCount; i++) writer.Write(TodayReport[i]);
                for (int i = 0; i < BudgetCatCount; i++) writer.Write(HistoryReport[i]);
                writer.Write(DaysRunning);
                writer.Write(LastClockHour);
                writer.Write(LotTransitInfo);
                writer.Write(DowntownSimGUID);
            }
        }

        public override void Tick(VM vm, object owner)
        {
            Active = this;
            // TRV-02: a lot-state restored from a vacation-lot save re-seeds the
            // live travel transit fields once, so the return path (modes 21/26)
            // survives a quit/relaunch mid-trip.
            if (!TransitRestored)
            {
                TransitRestored = true;
                var gameState = FSO.Content.Content.Get().Neighborhood?.GameState;
                if (gameState != null && LotTransitInfo != 0 && gameState.LotTransitInfo == 0)
                {
                    gameState.LotTransitInfo = LotTransitInfo;
                    gameState.DowntownSimGUID = DowntownSimGUID;
                }
            }
            var hour = vm.Context.Clock.Hours;
            if (LastClockHour >= 0 && hour < LastClockHour) RollDay();
            LastClockHour = hour;
        }

        /// <summary>
        /// Seed the ledger from the SIMI day records the loaded house already
        /// carries (native per-day budget history), most recent first.
        /// </summary>
        private void RestoreLedgerFromSIMI()
        {
            var days = SimulationInfo.BudgetDays;
            if (days == null) return;
            var valid = days.Where(d => d != null && d.Valid != 0)
                .Select(d => new int[] { d.MiscIncome, d.JobIncome, d.ServiceExpense, d.FoodExpense,
                    d.BillsExpense, d.MiscExpense, d.HouseholdExpense, d.ArchitectureExpense })
                .ToList();
            if (valid.Count == 0) return;
            TodayReport = valid[0];
            for (int i = 0; i < BudgetCatCount; i++) HistoryReport[i] = 0;
            DayRing = new int[3][];
            DayRingCount = 0;
            for (int d = 1; d < valid.Count && d <= 3; d++)
            {
                DayRing[DayRingCount++] = valid[d];
                for (int i = 0; i < BudgetCatCount; i++) HistoryReport[i] += valid[d][i];
            }
        }

        /// <summary>
        /// Write Today + the history ring back into the SIMI day records so the
        /// canonical house IFF carries the native budget history.
        /// </summary>
        private void MirrorLedgerIntoSIMI()
        {
            var simi = SimulationInfo;
            if (simi == null) return;
            // SIMI.Write dereferences all six day slots; a freshly built SIMI has
            // neither the array nor its entries.
            if (simi.BudgetDays == null || simi.BudgetDays.Length == 0)
                simi.BudgetDays = new SIMI.SIMIBudgetDay[6];
            var days = simi.BudgetDays;
            var sources = new List<int[]> { TodayReport };
            for (int i = 0; i < DayRingCount && i < 3; i++) sources.Add(DayRing[i]);
            for (int i = 0; i < days.Length; i++)
            {
                if (days[i] == null) days[i] = new SIMI.SIMIBudgetDay();
                if (i >= sources.Count) { days[i].Valid = 0; continue; }
                var src = sources[i];
                days[i].Valid = 1;
                days[i].MiscIncome = src[(int)BudgetCat.MiscIncome];
                days[i].JobIncome = src[(int)BudgetCat.JobIncome];
                days[i].ServiceExpense = src[(int)BudgetCat.ServiceExpense];
                days[i].FoodExpense = src[(int)BudgetCat.FoodExpense];
                days[i].BillsExpense = src[(int)BudgetCat.BillsExpense];
                days[i].MiscExpense = src[(int)BudgetCat.MiscExpense];
                days[i].HouseholdExpense = src[(int)BudgetCat.HouseholdExpense];
                days[i].ArchitectureExpense = src[(int)BudgetCat.ArchitectureExpense];
            }
        }

        // SAV-11: true when the member's Neighbour store record carries the death flag
        // (person data word 68, mirrored there by VMAvatar.MirrorDeadFlagToStore).
        private static bool IsDeadInStore(uint guid)
        {
            try
            {
                var neigh = FSO.Content.Content.Get().Neighborhood;
                var nid = neigh?.GetNeighborIDForGUID(guid);
                if (nid == null) return false;
                var rec = neigh.GetNeighborByID(nid.Value);
                var pd = rec?.PersonData;
                return pd != null && pd.Length > 68 && pd[68] > 0;
            }
            catch { return false; }
        }

        public void UpdateSIMI(VM vm)
        {
            if (SimulationInfo == null) return;

            var objValue = VMArchitectureStats.GetObjectValue(vm);
            SimulationInfo.ArchitectureValue = VMArchitectureStats.GetArchValue(vm.Context.Architecture) + objValue.Item2;
            SimulationInfo.ObjectsValue = objValue.Item1;
            SimulationInfo.Version = 0x3E;
            SimulationInfo.GlobalData = vm.GlobalState;
            MirrorLedgerIntoSIMI();
        }

        public override void ActivateValidator(VM vm)
        {
            Validator = new VMDefaultValidator(vm);
        }
    }
}
