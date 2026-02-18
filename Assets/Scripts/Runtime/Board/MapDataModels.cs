using System;

namespace Risiko3D.Runtime.Board
{
    [Serializable]
    public sealed class MapData
    {
        public string id;
        public string name;
        public TerritoryData[] territories;
        public ContinentData[] continents;
    }

    [Serializable]
    public sealed class TerritoryData
    {
        public string id;
        public string continent;
        public string[] neighbors;
    }

    [Serializable]
    public sealed class ContinentData
    {
        public string id;
        public int bonus;
        public string[] territories;
    }
}

