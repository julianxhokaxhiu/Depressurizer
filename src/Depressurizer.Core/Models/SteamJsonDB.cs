using Depressurizer.Core.Helpers;
using Depressurizer.Core.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using static Depressurizer.Core.Helpers.SteamJsonCollectionHelper;

namespace Depressurizer.Core.Models
{
    public class SteamJsonDB : ISteamCollectionSaveManager
    {
        private readonly string filePath;
        private JArray parsedCatalog = null;
        private readonly string steamID3;
        private Encoding catalogEncoding = Encoding.UTF8;

        public SteamJsonDB(string steamID3) {
            this.steamID3 = steamID3;
            this.filePath = string.Format(CultureInfo.InvariantCulture, Constants.CloudStorageNamespace1, Settings.Instance.SteamPath, steamID3);
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
                        steamCollections.Add(
                            new DepressurizerSteamCollectionValue()
                            {
                                name = item.key,
                                steamCollectionValue = item.collectionValue
                            });
                    }
                }
            }

            return steamCollections;
        }

        public void setSteamCollections(Dictionary<long, GameInfo> Games, List<Category> Categories)
        {
            if (parsedCatalog == null)
                setParsedCatalog();

            var res = MergeData(parsedCatalog, Games, Categories, false);

            //Backup old file
            File.Copy(filePath, Path.GetDirectoryName(filePath) + "Backup-" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            // Save the new categories in file
            File.WriteAllBytes(filePath, res);
        }

        private void setParsedCatalog()
        {
            parsedCatalog = new();
            using (StreamReader file = System.IO.File.OpenText(filePath))
            {
                parsedCatalog = JArray.Parse(file.ReadToEnd());
            }
        }

        public bool IsSupported()
        {
            if (File.Exists(filePath))
                return true;

            return false;
        }
    }
}
