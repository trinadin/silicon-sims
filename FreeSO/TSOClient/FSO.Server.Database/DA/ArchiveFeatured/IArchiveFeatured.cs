using FSO.Common.Enum;
using System.Collections.Generic;

namespace FSO.Server.Database.DA.ArchiveFeatured
{
    public interface IArchiveFeatured
    {
        IEnumerable<DbArchiveFeatured> All(int shard_id);
        IEnumerable<DbArchiveFeaturedWithLocation> GetByCategory(int shard_id, LotCategory category);
        int Create(DbArchiveFeatured featured);
        bool Clear(int shard_id);
    }
}
