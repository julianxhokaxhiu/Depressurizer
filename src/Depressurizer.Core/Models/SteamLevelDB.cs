using Depressurizer.Core.Interfaces;
using LevelDB;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static Depressurizer.Core.Helpers.SteamJsonCollectionHelper;

namespace Depressurizer.Core.Models
{
    public class SteamLevelDB : ISteamCollectionSaveManager
    {
        private readonly string databasePath;
        private readonly string steamID3;

        private string KeyPrefix => $"_https://steamloopback.host\u0000\u0001U{steamID3}-cloud-storage-namespace-1";
        private JArray parsedCatalog = null;
        private Encoding catalogEncoding = Encoding.UTF8;

        public SteamLevelDB(string steamID3)
        {
            this.databasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Steam", "htmlcache", "Local Storage", "leveldb");
            this.steamID3 = steamID3;
        }

        public List<DepressurizerSteamCollectionValue> getSteamCollections()
        {

            setParsedCatalog();

            CloudStorageNamespace collections = new CloudStorageNamespace();
            foreach (JToken item in parsedCatalog.Children())
            {
                collections.children.Add(item[0].ToString(), JsonConvert.DeserializeObject<CloudStorageNamespace.Element>(item[1].ToString(), new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore
                }));
            }

            List<DepressurizerSteamCollectionValue> steamCollections = new List<DepressurizerSteamCollectionValue>();
            foreach (var item in collections.children.Values)
            {
                if (item.key.StartsWith("user-collections") && !item.is_deleted)
                {
                    if (item.collectionValue != null)
                    {
                        steamCollections.Add(new DepressurizerSteamCollectionValue()
                            {
                                name = item.key,
                                steamCollectionValue = item.collectionValue
                            });
                    }
                }
            }

            return steamCollections;
        }

        public void setSteamCollections(Dictionary<long, GameInfo> Games)
        {
            if (parsedCatalog == null)
                setParsedCatalog();
            var res = MergeData(parsedCatalog, Games, true);

            // Save the new categories in leveldb
            var options = new Options()
            {
                ParanoidChecks = true,
            };
            var db = new DB(options, this.databasePath);
            db.Put(Encoding.UTF8.GetBytes(KeyPrefix), res);
            db.Close();
        }

        private void setParsedCatalog()
        {
            parsedCatalog = new();
            var options = new Options()
            {
                ParanoidChecks = true,
            };

            var db = new DB(options, this.databasePath);
            byte[] dataBytes = db.Get(Encoding.UTF8.GetBytes(KeyPrefix));

            if (dataBytes[0] == 0x0) catalogEncoding = Encoding.Unicode;
            else catalogEncoding = Encoding.UTF8;

            string data = catalogEncoding.GetString(dataBytes.Skip(1).ToArray());

            db.Close();

            parsedCatalog = JArray.Parse(data);
        }

        public bool IsSupported()
        {
            var options = new Options()
            {
                ParanoidChecks = true,
            };

            using (var db = new DB(options, this.databasePath))
            {
                foreach (var t in db)
                {
                    if (t.Key == Encoding.UTF8.GetBytes(KeyPrefix))
                        return true;

                }
            }
            return false;
        }
    }
}
