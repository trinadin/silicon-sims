using Dapper;
using FSO.Common.Enum;
using System.Collections.Generic;
using System.Linq;

namespace FSO.Server.Database.DA.ArchiveFeatured
{
    public class SqlArchiveFeatured : AbstractSqlDA, IArchiveFeatured
    {
        public SqlArchiveFeatured(ISqlContext context) : base(context)
        {
        }

        public int Create(DbArchiveFeatured featured)
        {
            var result = Context.Connection.Query<int>(Context.CompatLayer("INSERT INTO fso_archive_featured (name, lot_id, category, description, shard_id)" +
                                    " VALUES (@name, @lot_id, @category, @description, @shard_id);" +
                                    " SELECT LAST_INSERT_ID();"), featured).First();
            return result;
        }

        public IEnumerable<DbArchiveFeatured> All(int shard_id)
        {
            return Context.Connection.Query<DbArchiveFeatured>("SELECT * FROM fso_archive_featured WHERE shard_id = @shard_id").ToList();
        }

        public bool Clear(int shard_id)
        {
            return Context.Connection.Execute("DELETE FROM fso_archive_featured WHERE shard_id = @shard_id", new { shard_id }) > 0;
        }

        public DbArchiveFeatured Get(int id)
        {
            return Context.Connection.Query<DbArchiveFeatured>("SELECT * FROM fso_archive_featured WHERE id = @id", new { id }).FirstOrDefault();
        }

        public IEnumerable<DbArchiveFeaturedWithLocation> GetByCategory(int shard_id, LotCategory category)
        {
            return Context.Connection.Query<DbArchiveFeaturedWithLocation>(
                "SELECT f.id, f.name, f.lot_id, f.category, f.description, f.shard_id, l.location FROM fso_archive_featured f INNER JOIN fso_lots l ON f.lot_id = l.lot_id WHERE f.shard_id = @shard_id AND f.category = @category",
                new { shard_id, category = (int)category }).ToList();
        }
    }
}
