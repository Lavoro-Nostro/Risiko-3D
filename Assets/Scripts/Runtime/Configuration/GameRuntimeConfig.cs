using UnityEngine;

namespace Risiko3D.Runtime.Configuration
{
    public enum BoardVisualMode
    {
        DebugOnly = 0,
        VisualWithDebug = 1,
        VisualOnly = 2
    }

    [CreateAssetMenu(
        fileName = "GameRuntimeConfig",
        menuName = "Risiko3D/Runtime Config",
        order = 1)]
    public sealed class GameRuntimeConfig : ScriptableObject
    {
        [Header("Build and Protocol")]
        public string BuildVersion = "0.1.0";
        public string ProtocolVersion = "1.0.0";
        public string ContractMinVersion = "1.0.0";
        public string ContractMaxVersion = "1.0.0";
        public string RuntimeContractVersion = "1.0.0";
        public bool EnableRuntimeDebugOverlay = false;

        [Header("Scenes")]
        public string MainMenuSceneName = "MainMenu";
        public string GameplaySceneName = "SampleScene";

        [Header("Rules Profile")]
        public string MapId = "world-classic";
        public string RulesProfileId = "RisiKo!_OBJECTIVE_CLASSICO_IT_V1";
        public int MinPlayers = 3;
        public int MaxPlayers = 6;
        public bool SvgOnlyCardPipeline = true;

        [Header("Steam")]
        public bool EnableSteamSdkHealthChecks = true;
        public string SteamAppIdPath = "steam_appid.txt";
        public uint ExpectedDevSteamAppId = 480;

        [Header("Content Paths (project-relative)")]
        public string MapJsonPath = "Assets/GameData/Map/map.json";
        public string TerritorySymbolManifestPath = "Assets/GameData/Cards/world-classic-territory-symbol-manifest.json";
        public string TerritoryCardsPath = "Assets/cards/territory";
        public string ObjectiveCardsPath = "Assets/cards/generated/objective_it";
        public string RuleCardsPath = "Assets/cards/generated/rule_it";
        public string MapLocalizationItPath = "Assets/GameData/Map/i18n/it.json";
        public string MapLocalizationEnPath = "Assets/GameData/Map/i18n/en.json";
        public string TerritoryPositionsPath = "Assets/GameData/Map/world-classic-territory-positions.json";

        [Header("Board Visual")]
        public BoardVisualMode VisualMode = BoardVisualMode.VisualOnly;
        public string BoardMapSpriteResourcePath = "Map/world_classic_map";
        public Vector3 BoardVisualPosition = new Vector3(0f, 0.02f, 2f);
        public Vector3 BoardVisualRotation = new Vector3(90f, 0f, 0f);
        public Vector2 BoardVisualWorldSize = new Vector2(24f, 16f);
        public Vector2 TerritoryShapeWorldNudge = new Vector2(0f, 0.22f);
        public Vector3 TerritoryPositionOffset = Vector3.zero;
        public float TerritoryPositionScale = 1f;
    }
}
