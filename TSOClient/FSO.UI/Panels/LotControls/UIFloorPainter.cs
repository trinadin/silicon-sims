using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using FSO.LotView;
using FSO.Common.Rendering.Framework.Model;
using FSO.HIT;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using FSO.SimAntics.Utils;
using FSO.SimAntics.NetPlay.Model.Commands;
using FSO.Client.UI.Model;
using FSO.UI.Panels.LotControls;

namespace FSO.Client.UI.Panels.LotControls
{
    public class UIFloorPainter : UICustomLotControl
    {
        VM vm;
        LotView.World World;
        ILotControl Parent;

        bool Drawing;
        private List<VMArchitectureCommand> Commands;
        int CursorDir = 0;

        int StartX = -1;
        int StartY = -1;

        ushort Pattern;

        public UIFloorPainter (VM vm, LotView.World world, ILotControl parent, List<int> parameters)
        {
            Pattern = (ushort)parameters[0];

            this.vm = vm;
            World = parent.World;
            Parent = parent;

            Commands = new List<VMArchitectureCommand>();

        }


        public override void MouseDown(UpdateState state)
        {
            HITVM.Get().PlaySoundEvent(UISounds.BuildDragToolDown);
            Drawing = true;
        }

        public override void MouseUp(UpdateState state)
        {
            HITVM.Get().PlaySoundEvent(UISounds.BuildDragToolUp);

            vm.SendCommand(new VMNetArchitectureCmd
            {
                Commands = new List<VMArchitectureCommand>(Commands)
            });

            Commands.Clear();
            Drawing = false;
        }

        public override void Release()
        {
            vm.Context.Architecture.Commands.Clear();
            vm.Context.Architecture.SignalRedraw();
        }

        public override void Update(UpdateState state, bool scrolled)
        {
            ushort pattern = (Modifiers.IsSet(UILotControlModifiers.CTRL)) ? (ushort)0 : Pattern;

            var tilePos = World.EstTileAtPosWithScroll(new Vector2(MousePosition.X, MousePosition.Y));
            Point cursor = new Point((int)tilePos.X, (int)tilePos.Y);

            /*if (!Drawing && Commands.Count > 0)
            {
                vm.Context.Architecture.SignalRedraw();
                Commands.Clear();
            }*/
            if (Modifiers.IsSet(UILotControlModifiers.SHIFT) && pattern < 65534)
            {
                // R216: the fill seed obeys the same rim law as the rect (the
                // engine's fill walks TileIsFloorable tiles only; the port
                // clamps the seed — the walk itself stops at pattern changes,
                // and the rim carries no placeable floors).
                var seed = VMArchitectureTools.ClipFloorRectToFloorable(vm.Context.Architecture,
                    new Rectangle(cursor.X, cursor.Y, 0, 0));
                if (seed == null)
                {
                    if (Commands.Count > 0)
                    {
                        Commands.Clear();
                        vm.Context.Architecture.SignalRedraw();
                    }
                    vm.Context.Architecture.Commands.Clear();
                    return;
                }
                if (Commands.Count == 0 || Commands[0].Type != VMArchitectureCommandType.FLOOR_FILL)
                {
                    Commands.Clear();
                    vm.Context.Architecture.SignalRedraw();
                    Commands.Add(new VMArchitectureCommand
                    {
                        Type = VMArchitectureCommandType.FLOOR_FILL,
                        level = World.State.Level,
                        pattern = pattern,
                        style = 0,
                        x = seed.Value.X,
                        y = seed.Value.Y,
                    });
                }
            } else
            {
                if (Commands.Count > 0 && Commands[0].Type == VMArchitectureCommandType.FLOOR_FILL)
                {
                    Commands.Clear();
                    vm.Context.Architecture.SignalRedraw();
                }

                if (!Drawing || Commands.Count == 0)
                {
                    StartX = cursor.X;
                    StartY = cursor.Y;
                }

                int dir = 0;
                Vector2 fract = new Vector2(tilePos.X - cursor.X, tilePos.Y - cursor.Y);
                if (fract.X-fract.Y > 0)
                {
                    dir = (fract.X + fract.Y > 1) ? 2 : 1;
                } else
                {
                    dir = (fract.X + fract.Y > 1) ? 3 : 0;
                }

                int smallX = Math.Min(StartX, cursor.X);
                int smallY = Math.Min(StartY, cursor.Y);
                int bigX = Math.Max(StartX, cursor.X);
                int bigY = Math.Max(StartY, cursor.Y);

                // R216 engine law (cNewFloorTool::TileIsFloorable @0x17a6f0):
                // the floor tool never covers the lot's outer tile ring. Clip
                // the pending rect BEFORE the command exists so the preview
                // and the placement agree; an entirely-rim rect shows nothing.
                var floorable = VMArchitectureTools.ClipFloorRectToFloorable(vm.Context.Architecture,
                    new Rectangle(smallX, smallY, bigX - smallX, bigY - smallY));
                if (floorable == null)
                {
                    if (Commands.Count > 0)
                    {
                        Commands.Clear();
                        vm.Context.Architecture.SignalRedraw();
                    }
                    vm.Context.Architecture.Commands.Clear();
                    return;
                }
                smallX = floorable.Value.X;
                smallY = floorable.Value.Y;
                bigX = floorable.Value.X + floorable.Value.Width;
                bigY = floorable.Value.Y + floorable.Value.Height;

                var cmd = new VMArchitectureCommand
                {
                    Type = VMArchitectureCommandType.FLOOR_RECT,
                    level = World.State.Level,
                    pattern = pattern,
                    style = (ushort)dir,
                    x = smallX,
                    y = smallY,
                    x2 = bigX-smallX,
                    y2 = bigY-smallY
                };
                if (!Commands.Contains(cmd))
                {
                    Commands.Clear();
                    vm.Context.Architecture.SignalRedraw();
                    Commands.Add(cmd);
                }
            }

            var cmds = vm.Context.Architecture.Commands;
            cmds.Clear();
            foreach (var cmd in Commands)
            {
                cmds.Add(cmd);
            }

            if (cmds.Count > 0)
            {
                var cost = vm.Context.Architecture.LastTestCost;
                if (cost != 0)
                {
                    var disallowed = Parent.ActiveEntity != null && cost > Parent.Budget;
                    state.UIState.TooltipProperties.Show = true;
                    state.UIState.TooltipProperties.Color = disallowed ? Color.DarkRed : Color.Black;
                    state.UIState.TooltipProperties.Opacity = 1;
                    state.UIState.TooltipProperties.Position = new Vector2(MousePosition.X, MousePosition.Y);
                    state.UIState.Tooltip = (cost < 0) ? ("-$" + (-cost)) : ("$" + cost);
                    state.UIState.TooltipProperties.UpdateDead = false;

                    if (disallowed) HITVM.Get().PlaySoundEvent(UISounds.Error);
                }
                else
                {
                    state.UIState.TooltipProperties.Show = false;
                    state.UIState.TooltipProperties.Opacity = 0;
                }
            }

        }
    }
}
