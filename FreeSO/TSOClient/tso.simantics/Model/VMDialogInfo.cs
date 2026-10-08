using FSO.Content;
using FSO.SimAntics.Primitives;

namespace FSO.SimAntics.Model
{
    public class VMDialogInfo
    {
        public bool Block;
        public VMEntity Caller;
        public VMEntity Icon;
        // TS1 ObjectDialog icon provenance. Indexed/private and named/private
        // icons are resources of the behavior's code owner, not necessarily
        // the object currently in Stack Object. Neighbor icons likewise use
        // Stack Object ID as an NBRS id and may have no live VM entity.
        public GameIffResource IconResource;
        public short IconNeighborID = -1;
        public VMDialogOperand Operand;
        public string Message;
        public string IconName;
        public string Title;

        public string Yes;
        public string No;
        public string Cancel;

        public ulong DialogID; //what primitive this dialog belongs to. (GUID<<32) | (BHAVID<<16) | (pointer) informs ui of duplicates.
    }
}
