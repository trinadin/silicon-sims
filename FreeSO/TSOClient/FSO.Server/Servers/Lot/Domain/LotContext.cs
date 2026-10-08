using FSO.Server.Domain;
using FSO.Server.Protocol.Gluon.Model;

namespace FSO.Server.Servers.Lot.Domain
{
    public class LotContext
    {
        public uint Id;
        public int DbId;
        public int ShardId;
        public uint ClaimId;
        public ClaimAction Action;
        public bool HighMax;

        public bool SpecialLot
        {
            get
            {
                return (Id & (uint)LotIdFlags.SpecialMask) != 0;
            }
        }

        public bool UnownedLot
        {
            get
            {
                return (Id & (uint)LotIdFlags.Unowned) != 0;
            }
        }

        public bool JobLot
        {
            get
            {
                return (Id & (uint)LotIdFlags.JobLot) != 0;
            }
        }
    }
}
