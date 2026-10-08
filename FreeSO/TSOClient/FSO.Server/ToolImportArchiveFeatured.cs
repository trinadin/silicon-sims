using FSO.Server.Database.DA;
using FSO.Server.Utils;
using System;
using System.Collections.Generic;
using System.IO;

namespace FSO.Server
{
    internal class ToolImportArchiveFeatured : ITool
    {
        private IDAFactory DAFactory;
        private ImportArchiveFeaturedOptions Options;

        public ToolImportArchiveFeatured(ImportArchiveFeaturedOptions options, IDAFactory factory)
        {
            this.Options = options;
            this.DAFactory = factory;
        }

        public int Run()
        {
            if (Options.JSON == null)
            {
                Console.WriteLine("Please pass: <shard id> <featured json path>");
                return 1;
            }
            Console.WriteLine("Starting archive featured import...");

            List<ArchiveFeaturedJSON> data = null;
            //first load the JSON
            try
            {
                data = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ArchiveFeaturedJSON>>(File.ReadAllText(Options.JSON));
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("The JSON file specified could not be found! ");
                return 1;
            }
            catch (Exception)
            {
                Console.WriteLine("An unknown error occurred loading your JSON file. ");
                return 1;
            }

            Console.WriteLine("Found " + data.Count + " featured lots.");

            using (var da = (SqlDA)DAFactory.Get())
            {
                da.ArchiveFeatured.Clear(Options.ShardId);

                foreach (var item in data)
                {
                    da.ArchiveFeatured.Create(new Database.DA.ArchiveFeatured.DbArchiveFeatured()
                    {
                        name = item.name,
                        lot_id = item.lot_id,
                        category = item.category,
                        description = item.description,
                        shard_id = Options.ShardId,
                    });
                }

                Console.WriteLine($"Imported {data.Count} featured lots for shard {Options.ShardId}");
            }

            return 0;
        }
    }
}
