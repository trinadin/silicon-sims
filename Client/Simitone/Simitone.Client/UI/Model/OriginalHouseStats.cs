using System;
using System.Collections.Generic;

namespace Simitone.Client.UI.Model
{
    /// <summary>
    /// R133: the ORIGINAL HouseStats computation (House::GetHouseStats in The Sims
    /// Complete, entry 0x8c190, + ObjectModule::FillInObjectStats 0xe2630).
    ///
    /// Engine decode (see tools/iff-dump/r133/r133-housestats.md):
    ///   * the 44-byte HouseStats struct (ctor 0x8c130 zeroes this+0..40):
    ///     +0 squareFeet, +4 numSims (the SizeScore guard), +8 numBedrooms,
    ///     +12 numBathrooms, +16 (bool), +20 lotSize 0/1/2, +24 layoutScore,
    ///     +28/+32 object-module aggregates, +36 upkeep dividend, +40 objectCount.
    ///   * GetHouseStats scans the room list: squareFeet accumulates a per-room
    ///     helper x9 (engine-internal area units); a room with flag+92 set counts
    ///     as a BATHROOM, flag+88 as a BEDROOM.
    ///   * lotSize ladder (0x8c29c-0x8c2cc): lot dimension v: v &lt; 40 -&gt; 0,
    ///     40 &lt;= v &lt; 50 -&gt; 1, v &gt;= 50 -&gt; 2 — the Small/Medium/Large of
    ///     Live.iff STR# 138 'HouseSubpanelSize'.
    ///   * SizeScore evaluates squareFeet / Family::CountMembers through the
    ///     first PiecewiseFn loaded by House::Initialize from Global.iff STR#
    ///     505 'HouseScoreCurves': (0;0), (150;25), (300;50), (600;75),
    ///     (1200;100). The native getter truncates the interpolated float.
    ///   * Furnishings uses the ObjectModule::ComputeStats value pair cached at
    ///     ObjectModule+0xa0/+0xa4 when Live mode is entered: movable-object
    ///     value and fixed/architectural value. cFixedWorld::ComputeArchValue is
    ///     included in the latter. The score curve input is fixed / movable;
    ///     a zero movable-value denominator substitutes 100.0f.
    ///   * Yard is exact: for every outside Room, Room::CollectObjectStats adds
    ///     each negative RoomImpact to a signed sum and counts every nonnegative
    ///     RoomImpact object. Room::ComputeRoom evaluates count + 2*negativeSum;
    ///     RoomManager sums the outside-room values and House::GetYardScore
    ///     multiplies by 0.330000013f, clamps to [0,100], and truncates.
    ///
    /// PORT MAPPING (disclosed where engine-internal): the corpus's own help
    /// text (STR# 135) states the classification rules the engine implements —
    /// [11] 'This is the area inside the house--the area enclosed by walls',
    /// [13] 'A bedroom is any room that has a bed', [15] 'Any room that contains
    /// a toilet, tub, or shower is considered a bathroom'. The port computes:
    ///   * SquareFeet = 9 * sum of enclosed (non-outside) room areas in the VM's
    ///     room map. House::GetHouseStats applies this factor before exposing
    ///     the value to the panel; leaving it internal made the UI read 9x low.
    ///   * Bedrooms/Bathrooms = enclosed rooms whose entity list holds an object
    ///     with the OBJD RoomFlags bit 1 (Bedroom) / bit 2 (Bathroom) — the
    ///     R122-established STR# 150 room order's internal flags;
    ///   * LotSize = the decoded ladder over the lot dimension.
    /// </summary>
    public static class OriginalHouseStats
    {
        public struct Result
        {
            public int SquareFeet;
            public int Bedrooms;
            public int Bathrooms;
            public int LotSizeIndex;   // 0 Small / 1 Medium / 2 Large (STR# 138)
            public int FamilyMemberCount;
            public int ObjectCount;
            // R134: the ObjectModule::FillInObjectStats aggregates (0xe2630) —
            // the engine's per-object walk adds the object's VALUE (0xc9dc0)
            // to stats+28 when its room is INSIDE and to stats+32 when OUTSIDE,
            // and accumulates the stats+36 upkeep dividend as a per-object
            // condition weight (globalB when the object's +104 flag is clear,
            // globalA when set with +152 > 0). The two TOC-global weights are
            // statically unrecoverable — the port counts the two conditions so
            // the dividend = B*Working + A*Broken when the constants decode.
            public int FurnishingsValue;   // stats+28: sum of indoor object prices
            public int YardValue;          // stats+32: sum of outdoor object prices
            public int WorkingObjects;     // objects with RepairState < 600
            public int BrokenObjects;      // RepairState >= 600 (the engine's own
                                           // repair threshold, VMFindBestObjectForFunction)
            public bool HasPool;           // corpus: yard rating mentions the pool
            public int YardGoodObjects;    // outside RoomImpact >= 0 count
            public int YardNegativeImpact; // outside RoomImpact < 0 signed sum
            public float YardRoomScore;    // RoomManager+0x4c native operand
            public int FurnishingsObjectValue; // ObjectModule+0xa0: movable objects
            public int FurnishingsFixedValue;  // ObjectModule+0xa4: fixed + architecture

            public int SizeScore
            {
                get { return ComputeSizeScore(SquareFeet, FamilyMemberCount); }
            }

            public int YardScore
            {
                get { return ComputeYardScore(YardRoomScore); }
            }

            public int FurnishingsScore
            {
                get { return ComputeFurnishingsScore(FurnishingsObjectValue, FurnishingsFixedValue); }
            }

            // R135: the decoded UpkeepScore law — int(100.0f * clamp(dividend /
            // 2^31, 0, 1)), guard objectCount != 0 (GetUpkeepScore 0x8bcd0 with
            // the FS pool [TOC-23512] = file 0x5a2fb4: 0.0/1.0/100.0). The
            // engine's dividend uses runtime-normalized weights (B = 2^31/N
            // gives 100 when nothing is broken — the corpus's 'full as long as
            // everything is in working order'); the broken-object relative
            // weight A/B is BSS (runtime-only, proven r135). PORT-SIDE
            // DISCLOSED choice: A = 0 (broken objects contribute nothing) —
            // the r=0 case of the engine shape.
            public int UpkeepScore
            {
                get
                {
                    int n = WorkingObjects + BrokenObjects;
                    if (n == 0) return 0;
                    return (int)(100.0f * WorkingObjects / n);
                }
            }

            // R136: the decoded Layout law over the route history — see
            // Simitone.Client.UI.Model.OriginalRouteHistory (the engine's
            // GetHouseStats tail: (int)(100.0*clamp(ratio,0,1)) with the
            // total-normalized ratio; the engine's seed N0 is BSS-proven
            // unrecoverable, disclosed).
            public int LayoutScore
            {
                get { return OriginalRouteHistory.LayoutScore; }
            }
        }

        // the DECODED lot-size ladder (GetHouseStats 0x8c29c-0x8c2cc):
        // dimension < 40 -> Small(0); < 50 -> Medium(1); >= 50 -> Large(2)
        public static int ComputeLotSize(int lotDimension)
        {
            if (lotDimension < 40) return 0;
            if (lotDimension < 50) return 1;
            return 2;
        }

        /// <summary>
        /// House::GetSizeScore (0x8bf90) and Global.iff STR# 505 entry 0.
        /// PiecewiseFn clamps beyond its end points and the getter uses fctiwz,
        /// so a cast after single-precision interpolation is intentional.
        /// </summary>
        public static int ComputeSizeScore(int squareFeet, int familyMemberCount)
        {
            if (familyMemberCount == 0) return 0;
            float x = (float)squareFeet / familyMemberCount;
            if (x <= 0f) return 0;
            if (x >= 1200f) return 100;
            if (x < 150f) return (int)(x / 6f);
            if (x < 300f) return (int)(25f + (x - 150f) / 6f);
            if (x < 600f) return (int)(50f + (x - 300f) / 12f);
            return (int)(75f + (x - 600f) / 24f);
        }

        /// <summary>
        /// HouseStats::GetFurnishingsScore (0x08be30) and Global.iff STR# 505
        /// entry 1. ObjectModule::ComputeStats supplies the movable value at
        /// +0xa0 and the fixed-object value at +0xa4; House adds
        /// cFixedWorld::ComputeArchValue to +0xa4. Native rounds both integers
        /// to single precision, divides with fdivs, evaluates the piecewise
        /// curve, then truncates with fctiwz.
        /// </summary>
        public static int ComputeFurnishingsScore(int movableObjectValue, int fixedArchitectureValue)
        {
            float x = movableObjectValue == 0
                ? 100.0f
                : (float)fixedArchitectureValue / (float)movableObjectValue;
            if (x <= 0f) return 100;
            if (x >= 5f) return 0;
            if (x < 1f) return (int)(100f - x * 40f);
            if (x < 2f) return (int)(60f - (x - 1f) * 25f);
            if (x < 3f) return (int)(35f - (x - 2f) * 15f);
            if (x < 4f) return (int)(20f - (x - 3f) * 10f);
            return (int)(10f - (x - 4f) * 10f);
        }

        /// <summary>
        /// Room::ComputeRoom's outside branch (0x126988-0x1269dc). The two
        /// RoomScoreConstants are loaded by name as "outdoor good object count
        /// factor" = 1 and "outdoor room impact factor" = 2.
        /// </summary>
        public static float ComputeOutdoorRoomScore(int goodObjectCount, int negativeRoomImpact)
        {
            return goodObjectCount * 1.0f + negativeRoomImpact * 2.0f;
        }

        /// <summary>
        /// House::GetYardScore (0x08bda0): the owned Global.iff FCNS 1024 and
        /// executable fallback both encode the single-precision multiplier
        /// 0.330000013f. Native fctiwz truncation is represented by the cast.
        /// </summary>
        public static int ComputeYardScore(float outsideRoomScoreSum)
        {
            float score = outsideRoomScoreSum * 0.33000001311302185f;
            if (score <= 0f) return 0;
            if (score >= 100f) return 100;
            return (int)score;
        }

        public static Result Compute(FSO.SimAntics.VM vm)
        {
            var res = new Result();
            if (vm == null || vm.Context == null || vm.Context.Architecture == null) return res;
            var arch = vm.Context.Architecture;

            // House::GetHouseStats writes Family::CountMembers at stats+4.
            // FAMI.FamilyGUIDs is the port's persistent family-member vector;
            // RuntimeSubset is only the transient travel/selection subset.
            var family = vm.TS1State != null ? vm.TS1State.CurrentFamily : null;
            res.FamilyMemberCount = family != null && family.FamilyGUIDs != null
                ? family.FamilyGUIDs.Length : 0;

            // Native panel units: each enclosed room helper unit contributes
            // nine square feet (GetHouseStats 0x8c190).
            if (arch.RoomData != null)
            {
                foreach (var room in arch.RoomData)
                {
                    if (room.IsOutside) continue;
                    res.SquareFeet += room.Area * 9;
                }
            }

            // bedrooms / bathrooms + the FillInObjectStats value split: walk
            // every room INCLUDING the outside room — indoor object prices
            // accumulate to FurnishingsValue (stats+28), outdoor to YardValue
            // (stats+32); the upkeep condition counts alongside (stats+36's
            // inputs). (The engine's level-volume check [1, stories-1] is
            // implicit in the port's room map: objects live on a story.)
            var roomInfo = vm.Context.RoomInfo;
            if (roomInfo != null)
            {
                var seenParts = new HashSet<FSO.SimAntics.Entities.VMMultitileGroup>();
                for (int r = 0; r < roomInfo.Length; r++)
                {
                    var info = roomInfo[r];
                    bool outside = info.Room.IsOutside;
                    if (info.Room.IsPool) res.HasPool = true;
                    bool hasBed = false, hasBath = false;
                    int yardGood = 0, yardNegative = 0;
                    var ents = info.Entities;
                    if (ents != null)
                    {
                        foreach (var e in ents)
                        {
                            // Room::CollectObjectStats (0x125e30-0x1261a0)
                            // excludes hidden/out-of-world entities, then uses
                            // stack-object variable 7 (RoomImpact at cXObject
                            // +0x58) for every remaining object. This walk is
                            // intentionally per entity/part, unlike the value
                            // aggregates below which are per multitile group.
                            if (outside && e != null && !e.Dead && !e.GhostImage)
                            {
                                try
                                {
                                    if (e.GetValue(FSO.SimAntics.Model.VMStackObjectVariable.Hidden) == 0
                                        && e.GetValue(FSO.SimAntics.Model.VMStackObjectVariable.Room) >= 0)
                                    {
                                        int impact = e.GetValue(FSO.SimAntics.Model.VMStackObjectVariable.RoomImpact);
                                        if (impact < 0) yardNegative += impact;
                                        else yardGood++;
                                    }
                                }
                                catch { }
                            }

                            var go = e as FSO.SimAntics.VMGameObject;
                            if (go == null || go.Dead || go.GhostImage) continue;
                            if (go.MultitileGroup != null && !seenParts.Add(go.MultitileGroup)) continue;
                            int rf = 0;
                            try { rf = (go.Object != null && go.Object.OBJ != null) ? go.Object.OBJ.RoomFlags : 0; }
                            catch { continue; }
                            if (!outside)
                            {
                                if ((rf & (1 << 1)) != 0) hasBed = true;
                                if ((rf & (1 << 2)) != 0) hasBath = true;
                            }
                            // FillInObjectStats: zero/negative-valued objects skip
                            // the value routing entirely
                            int price = 0;
                            try { price = go.MultitileGroup != null ? go.MultitileGroup.InitialPrice : 0; }
                            catch { }
                            if (price > 0)
                            {
                                if (outside) res.YardValue += price;
                                else res.FurnishingsValue += price;
                            }
                            // the upkeep condition: the engine's own broken test
                            int repair = 0;
                            try { repair = go.GetValue(FSO.SimAntics.Model.VMStackObjectVariable.RepairState); }
                            catch { }
                            if (repair >= 600) res.BrokenObjects++;
                            else res.WorkingObjects++;
                        }
                    }
                    if (outside)
                    {
                        res.YardGoodObjects += yardGood;
                        res.YardNegativeImpact += yardNegative;
                        // Preserve the native per-room float accumulation order.
                        res.YardRoomScore += ComputeOutdoorRoomScore(yardGood, yardNegative);
                    }
                    if (!outside)
                    {
                        if (hasBed) res.Bedrooms++;
                        if (hasBath) res.Bathrooms++;
                    }
                }
            }

            res.LotSizeIndex = ComputeLotSize(arch.Width);

            // ObjectModule::ComputeStats' two furnishing operands map directly
            // to the same split already used by TS1's SIMI save path:
            // ObjectsValue (movable) and ArchitectureValue (fixed build-mode
            // objects + cFixedWorld architecture). Keep this distinct from the
            // HouseStats+28 indoor-room aggregate above; that is not an operand
            // of GetFurnishingsScore.
            try
            {
                var valueSplit = FSO.SimAntics.Utils.VMArchitectureStats.GetObjectValue(vm);
                res.FurnishingsObjectValue = valueSplit.Item1;
                res.FurnishingsFixedValue = valueSplit.Item2
                    + FSO.SimAntics.Utils.VMArchitectureStats.GetArchValue(arch);
            }
            catch { }

            // object count: one per multitile group, live objects only
            var seen = new HashSet<FSO.SimAntics.Entities.VMMultitileGroup>();
            foreach (var e in vm.Entities)
            {
                var go = e as FSO.SimAntics.VMGameObject;
                if (go == null || go.Dead || go.GhostImage) continue;
                var grp = go.MultitileGroup;
                if (grp != null && !seen.Add(grp)) continue;
                res.ObjectCount++;
            }
            return res;
        }
    }
}
