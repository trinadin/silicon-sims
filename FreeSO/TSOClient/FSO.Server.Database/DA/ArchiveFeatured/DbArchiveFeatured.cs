namespace FSO.Server.Database.DA.ArchiveFeatured
{
    public class DbArchiveFeatured
    {
        public int id { get; set; }
        public string name { get; set; }
        public int lot_id { get; set; }
        public int category { get; set; }
        public string description { get; set; }
        public int shard_id { get; set; }
    }

    public class DbArchiveFeaturedWithLocation : DbArchiveFeatured
    {
        public uint location { get; set; }
    }
}
