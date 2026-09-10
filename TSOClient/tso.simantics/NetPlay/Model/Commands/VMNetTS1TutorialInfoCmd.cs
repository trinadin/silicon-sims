namespace FSO.SimAntics.NetPlay.Model.Commands
{
    /// <summary>The original tutorial icon's ObjectModule::ShowTutorialInfo action.</summary>
    public class VMNetTS1TutorialInfoCmd : VMNetCommandBodyAbstract
    {
        public override bool Verify(VM vm, VMAvatar caller)
        {
            return vm.TS1;
        }

        public override bool Execute(VM vm, VMAvatar caller)
        {
            if (!vm.TS1) return false;
            var owner = vm.Context.TutorialObject;
            if (owner == null || owner.Dead || owner.Thread == null) return false;
            if (owner.TreeByName == null) owner.FetchTreeByName(vm.Context);
            // RunCheckTree in the original runs this private named tree without
            // replacing the owner's main stack or responding to its dialog.
            return owner.ExecuteNamedEntryPoint("show situation info", vm.Context,
                true, null, new short[4]);
        }
    }
}
