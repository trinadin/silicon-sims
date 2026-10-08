using System.Collections.Generic;

namespace FSO.Common.Security
{
    public interface ISecurityContext
    {
        bool HasModerationLevel(int threshold);
        void DemandAvatar(uint id, AvatarPermissions permission);
        void DemandAvatars(IEnumerable<uint> id, AvatarPermissions permission);
        void DemandInternalSystem();
    }
}
