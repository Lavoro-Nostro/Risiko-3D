using System;

namespace Risiko3D.Runtime.Board
{
    [Serializable]
    public sealed class TerritoryPositionManifest
    {
        public string mapId;
        public string version;
        public TerritoryPositionEntry[] positions;
    }

    [Serializable]
    public sealed class TerritoryPositionEntry
    {
        public string territoryId;
        public float x;
        public float y;
        public float z;
    }
}

