using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using FSO.LotView;
using FSO.LotView.Components;
using FSO.LotView.Model;
using FSO.Common.Rendering.Framework.Model;
using FSO.HIT;
using FSO.SimAntics;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay.Model.Commands;
using FSO.Client.UI.Model;
using FSO.UI.Panels.LotControls;

namespace FSO.Client.UI.Panels.LotControls
{
    public class UITerrainFlatten : UICustomLotControl
    {
        VMMultitileGroup WallCursor;
        VMMultitileGroup WallCursor2;
        VM vm;
        LotView.World World;
        ILotControl Parent;

        private bool Drawing;
        private Point StartPosition;
        private Point EndPosition;

        private short StartTerrainHeight;
        private int StartMousePosition;
        private Point EndMousePosition;

        private VMArchitectureCommand LastCmd;
        private bool WasDown;
        // AUD-15: the native per-tool denied-sound rate limit — one fire per
        // 400 VM ticks (cTool::DoDeniedSound 0x192070 keys its limiter the
        // same way: a global tick stamp vs this tool instance).
        // AUD-17 G-2: long.MinValue underflows the `ticks - last >= 400`
        // limiter in unchecked arithmetic (the difference wraps to a huge
        // negative), so the AUD-15 denied sound could never fire. Seed at
        // -400: the first denial at clock ~0 lands exactly on the 400-tick
        // threshold — the native `last + 400U < now` first-fire intent.
        private long LastDeniedTick = -400;

        public UITerrainFlatten(VM vm, LotView.World world, ILotControl parent, List<int> parameters)
        {
            this.vm = vm;
            World = parent.World;
            Parent = parent;
            WallCursor = vm.Context.CreateObjectInstance(0x2F39B7A6, LotTilePos.OUT_OF_WORLD, FSO.LotView.Model.Direction.NORTH, true);
            WallCursor2 = vm.Context.CreateObjectInstance(0x2F39B7A6, LotTilePos.OUT_OF_WORLD, FSO.LotView.Model.Direction.NORTH, true);

            ((ObjectComponent)WallCursor.Objects[0].WorldUI).ForceDynamic = true;
            ((ObjectComponent)WallCursor2.Objects[0].WorldUI).ForceDynamic = true;
        }

        //0: up
        //1: down
        //2: error
        //3: anchor
        //4: level
        public void SetCursorGraphic(short id, VMMultitileGroup group)
        {
            group.Objects[0].SetValue(VMStackObjectVariable.Graphic, id);
            ((VMGameObject)group.Objects[0]).RefreshGraphic();
        }

        public override void MouseDown(UpdateState state)
        {
            if (!Drawing)
            {
                HITVM.Get().PlaySoundEvent(UISounds.BuildDragToolDown);

                var tilePos = World.EstTileAtPosWithScroll(new Vector2(MousePosition.X, MousePosition.Y));
                StartPosition = new Point((int)Math.Round(tilePos.X), (int)Math.Round(tilePos.Y));
                var terrain = vm.Context.Architecture.Terrain;

                if (StartPosition.Y >= terrain.Height || StartPosition.X >= terrain.Width || StartPosition.X < 0 || StartPosition.Y < 0) return;
                Drawing = true;
                StartTerrainHeight = terrain.Heights[StartPosition.Y*terrain.Width + StartPosition.X];
                StartMousePosition = (int)(MousePosition.Y - World.State.WorldSpace.GetScreenOffset().Y);
            }
        }

        public override void MouseUp(UpdateState state)
        {
            if (Drawing)
            {
                var cmds = new List<VMArchitectureCommand>();

                var cursor = EndPosition;
                var smallX = Math.Min(StartPosition.X, cursor.X);
                var smallY = Math.Min(StartPosition.Y, cursor.Y);
                var bigX = Math.Max(StartPosition.X, cursor.X);
                var bigY = Math.Max(StartPosition.Y, cursor.Y);

                if (smallX != bigX || bigY != smallY || (Modifiers.IsSet(UILotControlModifiers.CTRL)))
                {
                    cmds.Add(new VMArchitectureCommand
                    {
                        Type = VMArchitectureCommandType.TERRAIN_FLATTEN,
                        x = smallX,
                        y = smallY,
                        x2 = bigX - smallX,
                        y2 = bigY - smallY,
                        style = (ushort)StartTerrainHeight,
                        pattern = (ushort)((Modifiers.IsSet(UILotControlModifiers.CTRL)) ? 1 : 0)
                    });
                }

                if (cmds.Count > 0 && (Parent.ActiveEntity == null || vm.Context.Architecture.LastTestCost <= Parent.Budget))
                {
                    vm.SendCommand(new VMNetArchitectureCmd
                    {
                        Commands = new List<VMArchitectureCommand>(cmds)
                    });
                    
                    HITVM.Get().PlaySoundEvent(UISounds.BuildDragToolPlace);
                }
                else HITVM.Get().PlaySoundEvent(UISounds.BuildDragToolUp);
            }
            Drawing = false;
        }

        public override void Update(UpdateState state, bool scrolled)
        {
            var tilePos = World.EstTileAtPosWithScroll(new Vector2(MousePosition.X, MousePosition.Y));
            Point cursor = new Point((int)Math.Round(tilePos.X), (int)Math.Round(tilePos.Y));

            var cmds = vm.Context.Architecture.Commands;
            cmds.Clear();
            if (Drawing)
            {
                EndPosition = cursor;
                var smallX = Math.Min(StartPosition.X, cursor.X);
                var smallY = Math.Min(StartPosition.Y, cursor.Y);
                var bigX = Math.Max(StartPosition.X, cursor.X);
                var bigY = Math.Max(StartPosition.Y, cursor.Y);

                cmds.Add(new VMArchitectureCommand
                {
                    Type = VMArchitectureCommandType.TERRAIN_FLATTEN,
                    x = smallX,
                    y = smallY,
                    x2 = bigX-smallX,
                    y2 = bigY-smallY,
                    style = (ushort)StartTerrainHeight,
                    pattern = (ushort)((Modifiers.IsSet(UILotControlModifiers.CTRL)) ? 1 : 0)
                });
                WallCursor2.SetVisualPosition(new Vector3(StartPosition.X, StartPosition.Y, (World.State.Level - 1) * 2.95f), Direction.NORTH, vm.Context);
            } else
            {
                WallCursor2.SetVisualPosition(new Vector3(-2048, -2048, 0), Direction.NORTH, vm.Context);
            }

            if (cmds.Count > 0)
            {
                if (!WasDown || !cmds[0].Equals(LastCmd))
                {
                    vm.Context.Architecture.SignalRedraw();
                    WasDown = true;
                }

                var cost = vm.Context.Architecture.LastTestCost;
                if (cost != 0)
                {
                    var disallowed = Parent.ActiveEntity != null && cost > Parent.Budget;
                    // R203 dirt-tool law: every dirt-tool message rides the shared
                    // tooltip window and never recolors (BLACK). An unaffordable drag
                    // is STR# 149[5] 'Insufficient funds'; a normal drag is the
                    // engine's plain "$N"/"-$N" cost readout. The old DarkRed cost
                    // tooltip had no engine counterpart (tso.client copy carries the
                    // same law; see tools/iff-dump/r203/r203-dirt-tool-law.md).
                    state.UIState.TooltipProperties.Show = true;
                    state.UIState.TooltipProperties.Color = Color.Black;
                    state.UIState.TooltipProperties.Opacity = 1;
                    state.UIState.TooltipProperties.Position = new Vector2(MousePosition.X, MousePosition.Y);
                    state.UIState.Tooltip = disallowed ? TerrainToolErrors.Text(TerrainToolErrors.CodeInsufficientFunds)
                        : TerrainToolErrors.CostText(cost);
                    state.UIState.TooltipProperties.UpdateDead = false;

                    // ORIG-01 (2026-10-06 re-decode, supersedes AUD-15):
                    // cTool::DoDeniedSound loads TOC-22024 — a C STRING
                    // TABLE at raw 0x605978, not the id table EXP-12 read —
                    // and adds +90, landing exactly on "UI_error"
                    // (4/4 offset-exact across the slide/explode/refund/
                    // kaching/denied siblings). denied.xa never shipped
                    // (absent from every FAR and the retail ISO; the hot
                    // row is vestigial). The native denied feedback is
                    // therefore ui_error.xa — exactly what R203 originally
                    // wired; AUD-15's "denied" lookup was a guaranteed
                    // silence. Rate limit: one fire per 400 VM ticks.
                    var deniedTicks = vm?.Context?.Clock?.Ticks ?? 0;
                    if (!cmds[0].Equals(LastCmd) && disallowed
                        && deniedTicks - LastDeniedTick >= 400)
                    {
                        LastDeniedTick = deniedTicks;
                        HITVM.Get().PlaySoundEvent(UISounds.Error);
                    }
                }
                else
                {
                    state.UIState.TooltipProperties.Show = false;
                    state.UIState.TooltipProperties.Opacity = 0;
                }
                LastCmd = cmds[0];
            }
            else
            {
                if (WasDown)
                {
                    vm.Context.Architecture.Commands.Clear();
                    vm.Context.Architecture.SignalTerrainRedraw();
                    vm.Context.Architecture.SignalRedraw();
                    WasDown = false;
                }
            }

            WallCursor.SetVisualPosition(new Vector3(cursor.X, cursor.Y, (World.State.Level - 1) * 2.95f), Direction.NORTH, vm.Context);

            SetCursorGraphic(3, WallCursor);
            SetCursorGraphic(3, WallCursor2);
        }

        public override void Release()
        {
            WallCursor.Delete(vm.Context);
            vm.Context.Architecture.Commands.Clear();
            vm.Context.Architecture.SignalTerrainRedraw();
            vm.Context.Architecture.SignalRedraw();
        }
    }
}
