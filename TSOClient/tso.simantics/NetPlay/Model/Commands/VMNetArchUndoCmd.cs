using System.IO;

namespace FSO.SimAntics.NetPlay.Model.Commands
{
    /// <summary>
    /// UI-22 tranche 1: undo/redo one build-mode architecture batch. Body
    /// pattern copied from VMNetSetRoofCmd. Executing this through the normal
    /// command stream keeps undo/redo sequenced with other architecture
    /// commands (which must run in order) and leaves a future TS1 multiplayer
    /// story intact. TS1-only for tranche 1 (terrain/objects are later
    /// tranches; TSO build permissions are not in scope here).
    /// </summary>
    public class VMNetArchUndoCmd : VMNetCommandBodyAbstract
    {
        public bool Redo;

        public override bool Execute(VM vm)
        {
            if (!vm.TS1) return false;
            var arch = vm.Context.Architecture;
            return Redo ? arch.UndoStack.Redo(arch) : arch.UndoStack.Undo(arch);
        }

        #region VMSerializable Members

        public override void SerializeInto(BinaryWriter writer)
        {
            base.SerializeInto(writer);
            writer.Write(Redo);
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Redo = reader.ReadBoolean();
        }

        #endregion
    }
}
