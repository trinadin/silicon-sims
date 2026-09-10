using System.Collections.Generic;
using FSO.Client.UI.Panels.LotControls;
using FSO.Common.Rendering.Framework.Model;
using FSO.SimAntics;
using FSO.SimAntics.NetPlay.Model.Commands;

namespace Simitone.Client.UI.Panels.LiveSubpanels.Catalog
{
    /// <summary>
    /// R130: the roof PITCH sub-tool — the engine's cWinRoofPanel pairs each
    /// kBldSbTlRoof* plaque with a SetPitch call only (the pattern is kept),
    /// so this sends VMNetSetRoofCmd with the engine-mapped pitch (see
    /// UIOriginalRoofPitchResProvider.FSO_PITCH) and the CURRENT roof style.
    /// </summary>
    public class UIRoofPitcher : UICustomLotControl
    {
        public UIRoofPitcher(VM vm, FSO.LotView.World world, UILotControl parent, List<int> parameters)
        {
            vm.SendCommand(new VMNetSetRoofCmd()
            {
                Pitch = UIOriginalRoofPitchResProvider.PitchForRtId(parameters[0]),
                Style = vm.Context.Architecture.RoofStyle
            });
        }

        public override void MouseDown(UpdateState state)
        {
        }

        public override void MouseUp(UpdateState state)
        {
        }

        public override void Release()
        {
        }

        public override void Update(UpdateState state, bool scrolled)
        {
        }
    }
}
