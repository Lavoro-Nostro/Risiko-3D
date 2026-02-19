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

        [Header("Army Marker Visuals")]
        public GameObject TankMarkerPrefab;
        public float TankMarkerScale = 0.012f;
        public Vector3 TankMarkerLocalEuler = new Vector3(-90f, 0f, 0f);
        public float TankMarkerLift = 0.045f;
        public GameObject FlagMarkerPrefab;
        public float FlagMarkerScale = 0.020f;
        public Vector3 FlagMarkerLocalEuler = new Vector3(-90f, 0f, 0f);
        public float FlagMarkerLift = 0.060f;

        [Header("Board Command Hologram")]
        public bool UseHologramAnchorObject = true;
        public string HologramAnchorObjectName = "BoardCommandHologramAnchor";
        public Vector3 HologramAnchorOffset = new Vector3(0f, 0.016f, 0f);
        public Vector2 HologramBoardAnchorNormalized = new Vector2(0.56f, 0.20f);
        public Vector2 HologramSizeNormalized = new Vector2(0.42f, 0.32f);
        public Vector2 HologramSizeMin = new Vector2(6f, 2.8f);
        public Vector2 HologramSizeMax = new Vector2(14f, 9f);
        public float HologramThickness = 0.020f;
        public float HologramScale = 1.0f;

        [Header("Board Game Log Panel")]
        public bool EnableBoardGameLogPanel = true;
        public bool UseGameLogAnchorObject = true;
        public string GameLogAnchorObjectName = "BoardGameLogAnchor";
        public Vector3 GameLogAnchorOffset = new Vector3(0f, 0.012f, 0f);
        public Vector2 GameLogBoardAnchorNormalized = new Vector2(0.16f, 0.16f);
        public Vector2 GameLogSizeNormalized = new Vector2(0.30f, 0.24f);
        public Vector2 GameLogSizeMin = new Vector2(4.0f, 2.0f);
        public Vector2 GameLogSizeMax = new Vector2(10.0f, 6.0f);
        public float GameLogThickness = 0.016f;
        public float GameLogScale = 1.0f;
        public int GameLogVisibleLines = 8;
    }
}
