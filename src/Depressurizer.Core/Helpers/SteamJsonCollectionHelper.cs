using Depressurizer.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static Depressurizer.Core.Helpers.SteamJsonCollectionHelper.CloudStorageNamespace.Element;

namespace Depressurizer.Core.Helpers
{
    public static class SteamJsonCollectionHelper
    {
        private static Encoding catalogEncoding = Encoding.UTF8;
        private static Logger Logger => Logger.Instance;

        public static byte[] MergeData(JArray parsedCatalog, Dictionary<long, GameInfo> Games, List<Category> Categories, bool isLevelDB)
        {
            var categoryData = new Dictionary<string, List<long>>();
            var hiddenData = new List<long>();
            var favoriteData = new List<long>();

            // Create all categories
            foreach (var cat in Categories)
            {
                if (!categoryData.ContainsKey(cat.Name))
                {
                    categoryData[cat.Name] = new List<long>();
                }
            }

            // Prepare games in categories
            foreach (GameInfo game in Games.Values)
            {
                if (game.IsHidden)
                    hiddenData.Add(game.Id);

                if (game.IsFavorite())
                    favoriteData.Add(game.Id);

                foreach (Category c in game.Categories)
                {
                    string categoryName = c.Name.ToUpper();

                    if (!categoryData.ContainsKey(categoryName))
                    {
                        categoryData[categoryName] = new List<long>();
                    }

                    categoryData[categoryName].Add(game.Id);
                }
            }

            //Clear old added data
            foreach (JToken item in parsedCatalog.Children())
            {
                if (item?[0]?.ToString()?.StartsWith("user-collections") == true)
                {
                    try
                    {
                        var valueToken = item[1]?["value"];
                        if (valueToken != null)
                        {
                            var collectionData = JObject.Parse(valueToken.ToString());
                            var data = collectionData["added"];
                            collectionData["added"] = new JArray();
                            item[1]["value"] = collectionData.ToString(Formatting.None);
                        }
                    }catch(Exception ex)
                    {
                        Logger.Error(nameof(MergeData), ex);
                    }
                }
            }

            var newCatdata = new Dictionary<string, List<long>>();
            newCatdata["user-collections.hidden"] = hiddenData;
            newCatdata["user-collections.favorite"] = favoriteData;
            foreach (var d in categoryData)
                newCatdata[d.Key] = d.Value;

            var newArray = GenerateCategories(newCatdata);

            JObject existingObj = ToObjectByKey(parsedCatalog);

            JObject newObj = ToObjectByKey(newArray);
            existingObj.Merge(newObj, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Union
            });

            byte[] encodedArray = catalogEncoding.GetBytes(ToArrayFromKeyedObject(existingObj).ToString(Formatting.None));
            if (isLevelDB)
            {
                byte[] res = new byte[encodedArray.Length + 1];
                res[0] = (byte)(catalogEncoding.CodePage == Encoding.Unicode.CodePage ? 0x01 : 0x00);
                Buffer.BlockCopy(encodedArray, 0, res, 1, encodedArray.Length);
                return res;
            }
            else
            {
                return encodedArray;
            }
        }


        public static JArray GenerateCategories(Dictionary<string, List<long>> categoryData)
        {
            var result = new JArray();
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string version = DateTime.UtcNow.ToString("yyyyMMdd");

            foreach (var entry in categoryData)
            {
                string categoryName = entry.Key;
                List<long> gameIds = entry.Value;

                string id = string.Empty;
                string key = string.Empty;
                switch (entry.Key)
                {
                    case "user-collections.hidden":
                        id = "user-collections.hidden";
                        key = "user-collections.hidden";
                        break;
                    case "user-collections.favorite":
                        id = "user-collections.favorite";
                        key = "user-collections.favorite";
                        break;
                    default:
                        id = "uc-" + GetDeterministicId(categoryName);
                        key = "user-collections." + id;
                        break;
                }

                var inner = new JObject
                {
                    ["key"] = key,
                    ["timestamp"] = timestamp,
                    ["value"] = JsonConvert.SerializeObject(new
                    {
                        id = id,
                        name = categoryName.ToUpper(),
                        added = gameIds,
                        removed = new List<int>()
                    }),
                    ["version"] = version,
                    ["conflictResolutionMethod"] = "custom",
                    ["strMethodId"] = "union-collections"
                };

                result.Add(new JArray { key, inner });
            }

            return result;
        }

        public static JProperty GetListItem(JObject jsonObject, string key)
        {
            var targetProperty = jsonObject.Properties().FirstOrDefault(p => p.Value is JObject obj && obj["key"]?.ToString() == key);
            return targetProperty;
        }

        public static void ModifyAddedList(JObject jsonObject, string key, List<long> newAddedList)
        {
            var targetProperty = jsonObject.Properties().FirstOrDefault(p => p.Value is JObject obj && obj["key"]?.ToString() == key);
            if (targetProperty?.Value is JObject targetObject && targetObject["value"] != null)
            {
                var valueJson = JObject.Parse(targetObject["value"].ToString());
                valueJson["added"] = JArray.FromObject(newAddedList);
                targetObject["value"] = valueJson.ToString(Newtonsoft.Json.Formatting.None);
            }
        }

        public static JObject ToObjectByKey(JArray array)
        {
            var obj = new JObject();
            foreach (var item in array)
            {
                if (item is JArray arr && arr.Count == 2)
                {
                    obj[arr[0]?.ToString()] = arr[1];
                }
            }
            return obj;
        }

        public static JArray ToArrayFromKeyedObject(JObject obj)
        {
            var array = new JArray();
            foreach (var prop in obj)
            {
                array.Add(new JArray { prop.Key, prop.Value });
            }
            return array;
        }

        public static string GetDeterministicId(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(catalogEncoding.GetBytes(input.ToLowerInvariant()));
                return Convert.ToBase64String(hash).Replace("+", "").Replace("/", "").Replace("=", "").Substring(0, 12);
            }
        }

        public class DepressurizerSteamCollectionValue{
            public string name { get; set; }
            public SteamCollectionValue steamCollectionValue { get; set; }

        }

        public class CloudStorageNamespace
        {
            public Dictionary<string, Element> children { get; } = new Dictionary<string, Element>();

            public class Element
            {
                private SteamCollectionValue collectionValue1;

                public string key { get; set; }
                public int timestamp { get; set; }
                public bool is_deleted { get; set; }
                public string value { get; set; }

                public bool collection_Hidden { get; set; }
                public bool collection_Favorite { get; set; }

                public SteamCollectionValue collectionValue
                {
                    get => collectionValue1 ?? (collectionValue1 = JsonConvert.DeserializeObject<SteamCollectionValue>(value));
                    set => collectionValue1 = value;
                }

                public class SteamCollectionValue
                {
                    public string id { get; set; }
                    public string name { get; set; }
                    public long[] added { get; set; }
                    public long[] removed { get; set; }

                    public SteamDynamicCollectionFilerValue? filterSpec { get; set; }

                    public class SteamDynamicCollectionFilerValue
                    {
                        public long nFormatVersion { get; set; }
                        public string strSearchText { get; set; }
                    }
                } 
            }
        }
    }
}
