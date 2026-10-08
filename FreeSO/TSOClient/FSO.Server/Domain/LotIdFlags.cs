using System;

namespace FSO.Server.Domain
{
    [Flags]
    internal enum LotIdFlags : uint
    {
        None = 0,

        /// <summary>
        /// Unowned lots can be opened in archive mode.
        /// The lot is generated without a visible phone booth, trash and terrain marks for the vehicle portal.
        /// When the lot is closed, it is not saved. 
        /// It shouldn't be possible to purchase a lot while it's opened as an unowned lot.
        /// (future, when placing objects is allowed) If any objects were placed, they are returned to inventory.
        /// </summary>
        Unowned = 0x20000000,

        /// <summary>
        /// Job Lot instances are managed by the job matchmaker.
        /// It dynamically creates instances when players from different job types and levels go to work,
        /// and can create more when instances are full or when players are blocked.
        /// </summary>
        JobLot = 0x40000000,

        Reserved = 0x80000000,

        SpecialMask = Unowned | JobLot,
        NormalMask = ~(SpecialMask | Reserved)
    }
}
