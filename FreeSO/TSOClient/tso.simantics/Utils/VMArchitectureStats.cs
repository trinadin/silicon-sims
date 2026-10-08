using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView.Model;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FSO.SimAntics.Utils
{
    public static class VMArchitectureStats
    {
        public static WorldFloorProvider Floors;
        public static WorldWallProvider Walls;

        public static int GetArchValue(VMArchitecture arch)
        {
            // R137 engine law (cFixedWorld::ComputeArchValue 0x15ea70-0x15eee0
            // in The Sims Complete, decoded in tools/iff-dump/r137/): the walk
            // accumulates wall STRUCTURE prices (the style table lookups) in
            // one sum and everything else — wallpaper patterns at FULL price,
            // flooring at HALF price, roof tiles at [TOC-28568] = 100 each —
            // in another, then returns patterns + trunc(styles / 2): wall
            // structure and flooring count at HALF value, wallpaper at full.
            // The engine's roof-tile layer has no port-side storage (only the
            // RoofStyle/RoofPitch globals), so the roof contribution is 0 —
            // disclosed residual.
            Floors = Content.Content.Get().WorldFloors;
            Walls = Content.Content.Get().WorldWalls;

            int value = 0;
            int wallStyleValue = 0;
            for (int level = 0; level < arch.Stories; level++)
            {
                var walls = arch.Walls[level];
                var floors = arch.Floors[level];
                int index = 0;
                for (int y=0; y<arch.Height; y++)
                {
                    for (int x=0; x<arch.Width; x++)
                    {
                        if (arch.FineBuildableArea[index])
                        {
                            var floor = floors[index];
                            var wall = walls[index];

                            if (floor.Pattern > 0)
                            {
                                value += GetFloorPrice(floor.Pattern) / 2;
                            }
                            if (wall.Segments > 0)
                            {
                                if ((wall.Segments & WallSegments.AnyDiag) > 0)
                                {
                                    wallStyleValue += GetWallPrice(wall.TopRightStyle);

                                    if (wall.TopLeftPattern != 0) value += GetPatternPrice(wall.TopLeftPattern);
                                    if (wall.TopLeftStyle != 0) wallStyleValue += GetWallPrice(wall.TopLeftStyle);

                                    if (wall.BottomLeftPattern != 0) value += GetPatternPrice(wall.BottomLeftPattern);
                                    if (wall.BottomRightPattern != 0) value += GetPatternPrice(wall.BottomRightPattern);
                                }
                                else
                                {
                                    if ((wall.Segments & WallSegments.TopLeft) > 0)
                                    {
                                        wallStyleValue += GetWallPrice(wall.TopLeftStyle);
                                        value += GetPatternPrice(wall.TopLeftPattern);
                                        var wall2 = walls[index - 1];
                                        value += GetPatternPrice(wall2.BottomRightPattern);
                                    }
                                    if ((wall.Segments & WallSegments.TopRight) > 0)
                                    {
                                        wallStyleValue += GetWallPrice(wall.TopRightStyle);
                                        value += GetPatternPrice(wall.TopRightPattern);
                                        var wall2 = walls[index - arch.Width];
                                        value += GetPatternPrice(wall2.BottomLeftPattern);
                                    }
                                }
                            }
                        }
                        index++;
                    }
                }
            }
            return CombineArchitectureValue(value, wallStyleValue);
        }

        /// <summary>
        /// cFixedWorld::ComputeArchValue's final operation (0x15eea8-0x15eed8):
        /// wall structure is accumulated at full price and divided once, with
        /// signed truncation toward zero.  Dividing each segment separately is
        /// observably different when two odd-priced wall styles are present.
        /// </summary>
        public static int CombineArchitectureValue(int nonWallValue, int wallStyleValue)
        {
            return nonWallValue + wallStyleValue / 2;
        }

        public static Tuple<int, int> GetObjectValue(VM vm)
        {
            // The Sims Complete ObjectModule::ComputeStats value phase
            // (0xe29a0-0xe2b10). It visits one representative per multitile,
            // splits movable/eviction value from fixed build-mode value, and
            // deliberately excludes BuildModeType 4 (trees/plants) from both.
            var value = 0;
            var archValue = 0;
            var seen = new HashSet<VMMultitileGroup>();
            var arch = vm?.Context?.Architecture;
            if (vm == null || arch == null) return Tuple.Create(0, 0);
            foreach (var entity in vm.Entities)
            {
                var group = entity?.MultitileGroup;
                if (group == null || !seen.Add(group)) continue;
                var bObj = group.BaseObject;
                if (bObj == null || bObj.Dead || bObj.GhostImage
                    || bObj.Object?.OBJ == null) continue;

                var type = bObj.Object.OBJ.ObjectType;
                if (type == OBJDType.SimType) continue;
                var master = bObj.MasterDefinition ?? bObj.Object.OBJ;
                if (master == null) continue;

                int current = bObj.GetValue(VMStackObjectVariable.CurrentValue);
                if (group.Objects.Any(x => x != null
                    && x.GetValue(VMStackObjectVariable.RepairState) != 0))
                {
                    // cXObject::GetCurrentValue halves the signed current value
                    // toward zero if any member of the multitile is damaged.
                    current /= 2;
                }

                var pos = bObj.Position;
                bool inWorld = pos.TileX >= 1 && pos.TileY >= 1
                    && pos.TileX < arch.Width - 1 && pos.TileY < arch.Height - 1;
                var movement = (VMMovementFlags)bObj.GetValue(VMStackObjectVariable.MovementFlags);
                bool deletedByEvict = (movement & VMMovementFlags.StaysAfterEvict) == 0
                    && (type == OBJDType.Person || master.BuildModeType == 0);

                if (inWorld && deletedByEvict) value += current;
                else if (master.BuildModeType != 0 && master.BuildModeType != 4)
                    archValue += current;
            }
            return new Tuple<int, int>(value, archValue);
        }

        private static int GetWallPrice(ushort id)
        {
            return Walls.GetWallStyle(id)?.Price ?? 0;
        }

        private static int GetPatternPrice(ushort id)
        {
            var pref = GetPatternRef(id);
            return (pref == null) ? 0 : pref.Price;
        }

        private static int GetFloorPrice(ushort id)
        {
            if (id == 1) return 0;
            var fref = GetFloorRef(id);
            return (fref == null) ? 0 : fref.Price;
        }

        private static WallReference GetPatternRef(ushort id)
        {
            WallReference result;
            Walls.Entries.TryGetValue(id, out result);
            return result;
        }
        private static FloorReference GetFloorRef(ushort id)
        {
            FloorReference result;
            Floors.Entries.TryGetValue(id, out result);
            return result;
        }
    }
}
