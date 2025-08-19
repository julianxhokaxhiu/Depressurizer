using Depressurizer.Core.Models;
using System.Collections.Generic;
using static Depressurizer.Core.Helpers.SteamJsonCollectionHelper;

namespace Depressurizer.Core.Interfaces
{
    public interface ISteamCollectionSaveManager
    {
        public List<DepressurizerSteamCollectionValue> getSteamCollections();

        public void setSteamCollections(Dictionary<long, GameInfo> Games);

        public bool IsSupported();
    }
}
