using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using Risiko3D.Runtime.Board;
using Risiko3D.Runtime.Configuration;
using Risiko3D.Runtime.Contracts;
using Risiko3D.Runtime.Steam;
using UnityEngine;
using System.Globalization;

namespace Risiko3D.Runtime.Match
{
    public sealed class HostAuthoritativeMatchLoop : MonoBehaviour
    {
        private enum MatchPhase
        {
            SetupClaim,
            SetupDeploy,
            Reinforce,
            Attack,
            Fortify
        }

        private enum CardSymbol
        {
            Infantry,
            Cavalry,
            Artillery,
            Joker
        }

        private sealed class TerritoryState
        {
            public int OwnerIndex;
            public int Armies;
        }

        private sealed class PlayerState
        {
            public int Index;
            public string PlayerId;
            public ulong SteamId;
            public string DisplayName;
            public Color Color;
            public int ReinforcementPool;
            public int SetupArmiesRemaining;
        }

        private sealed class TerritoryCard
        {
            public string TerritoryId;
            public string DisplayName;
            public CardSymbol Symbol;
            public bool IsJoker;
            public string CardId;
        }

        public readonly struct HandCardUiData
        {
            public HandCardUiData(string cardId, string displayName, string symbol, bool isJoker)
            {
                CardId = cardId;
                DisplayName = displayName;
                Symbol = symbol;
                IsJoker = isJoker;
            }

            public string CardId { get; }
            public string DisplayName { get; }
            public string Symbol { get; }
            public bool IsJoker { get; }
        }

        public readonly struct PlayerVisualData
        {
            public PlayerVisualData(int index, string playerId, string displayName, Color color)
            {
                Index = index;
                PlayerId = playerId;
                DisplayName = displayName;
                Color = color;
            }

            public int Index { get; }
            public string PlayerId { get; }
            public string DisplayName { get; }
            public Color Color { get; }
        }

        private sealed class TradeSetCandidate
        {
            public int FirstIndex;
            public int SecondIndex;
            public int ThirdIndex;
            public int BaseReinforcement;
            public int OwnedTerritoryBonus;

            public int TotalReinforcement => BaseReinforcement + OwnedTerritoryBonus;
        }

        [Serializable]
        private sealed class TerritorySymbolManifestData
        {
            public TerritorySymbolAssignment[] assignments = Array.Empty<TerritorySymbolAssignment>();
        }

        [Serializable]
        private sealed class TerritorySymbolAssignment
        {
            public string territoryId = string.Empty;
            public string symbol = string.Empty;
        }

        [Serializable]
        private sealed class AuthoritativeSnapshotData
        {
            public int version = 1;
            public int activePlayerIndex;
            public int turnIndex;
            public int roundIndex;
            public string phase = string.Empty;
            public SnapshotPlayerData[] players = Array.Empty<SnapshotPlayerData>();
            public SnapshotTerritoryData[] territories = Array.Empty<SnapshotTerritoryData>();
        }

        [Serializable]
        private sealed class SnapshotPlayerData
        {
            public int index;
            public int setupArmiesRemaining;
            public int reinforcementPool;
        }

        [Serializable]
        private sealed class SnapshotTerritoryData
        {
            public string territoryId = string.Empty;
            public int ownerIndex;
            public int armies;
        }

        [Serializable]
        private sealed class TurnIntentData
        {
            public string intentId = string.Empty;
            public ulong senderSteamId;
            public string action = string.Empty;
            public string territoryId = string.Empty;
        }

        private readonly List<AuthoritativeEventEnvelope> _eventLog = new();
        private readonly List<GameplayCommandEnvelope> _commandLog = new();
        private readonly Dictionary<string, TerritoryState> _territories = new();
        private readonly List<PlayerState> _players = new();
        private readonly Dictionary<int, List<string>> _playerAssignedTerritoryCards = new();
        private readonly Dictionary<int, string> _playerObjectiveCards = new();
        private readonly Dictionary<string, CardSymbol> _territorySymbolById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TerritoryCard> _territoryCardsById = new(StringComparer.Ordinal);
        private readonly Dictionary<int, List<TerritoryCard>> _playerTerritoryHands = new();
        private readonly Dictionary<int, string> _playerColorIdByIndex = new();
        private readonly HashSet<int> _eliminatedPlayerIndices = new();
        private readonly HashSet<int> _setupConfirmedPlayers = new();
        private readonly List<TerritoryCard> _territoryDeck = new();
        private readonly List<TerritoryCard> _territoryDiscard = new();
        private readonly List<string> _actionFeed = new();
        private static readonly IReadOnlyList<string> EmptyCards = Array.Empty<string>();
        private static readonly IReadOnlyList<string> EmptyHandCards = Array.Empty<string>();
        private static readonly Dictionary<string, string> ObjectiveTextById = new(StringComparer.Ordinal)
        {
            ["obj-24"] = "Conquista 24 territori.",
            ["obj-18-2"] = "Conquista 18 territori con almeno 2 armate ciascuno.",
            ["obj-eu-au-plus1"] = "Conquista Europa, Oceania e un altro continente.",
            ["obj-eu-sa-plus1"] = "Conquista Europa, Sud America e un altro continente.",
            ["obj-na-af"] = "Conquista Nord America e Africa.",
            ["obj-na-au"] = "Conquista Nord America e Oceania.",
            ["obj-as-sa"] = "Conquista Asia e Sud America.",
            ["obj-as-af"] = "Conquista Asia e Africa.",
            ["obj-elim-red"] = "Distruggi totalmente l'armata rossa. Se impossibile: conquista 24 territori.",
            ["obj-elim-blue"] = "Distruggi totalmente l'armata blu. Se impossibile: conquista 24 territori.",
            ["obj-elim-green"] = "Distruggi totalmente l'armata verde. Se impossibile: conquista 24 territori.",
            ["obj-elim-yellow"] = "Distruggi totalmente l'armata gialla. Se impossibile: conquista 24 territori.",
            ["obj-elim-purple"] = "Distruggi totalmente l'armata viola. Se impossibile: conquista 24 territori.",
            ["obj-elim-black"] = "Distruggi totalmente l'armata nera. Se impossibile: conquista 24 territori.",
            ["obj-fallback-24"] = "Conquista 24 territori."
        };
        private static readonly Dictionary<string, string> ObjectiveTitleById = new(StringComparer.Ordinal)
        {
            ["obj-24"] = "Conquista 24 Territori",
            ["obj-18-2"] = "Conquista 18 con 2 Armate",
            ["obj-eu-au-plus1"] = "Europa + Oceania + 1",
            ["obj-eu-sa-plus1"] = "Europa + Sud America + 1",
            ["obj-na-af"] = "Nord America + Africa",
            ["obj-na-au"] = "Nord America + Oceania",
            ["obj-as-sa"] = "Asia + Sud America",
            ["obj-as-af"] = "Asia + Africa",
            ["obj-elim-red"] = "Distruggi Rosso",
            ["obj-elim-blue"] = "Distruggi Blu",
            ["obj-elim-green"] = "Distruggi Verde",
            ["obj-elim-yellow"] = "Distruggi Giallo",
            ["obj-elim-purple"] = "Distruggi Viola",
            ["obj-elim-black"] = "Distruggi Nero",
            ["obj-fallback-24"] = "Conquista 24 Territori"
        };
        private static readonly string[] PlayerColorCycle =
        {
            "red",
            "blue",
            "green",
            "yellow",
            "purple",
            "black"
        };
        private static readonly Color[] PlayerColorPalette =
        {
            new(0.92f, 0.26f, 0.27f),
            new(0.20f, 0.50f, 0.96f),
            new(0.23f, 0.80f, 0.30f),
            new(0.90f, 0.82f, 0.23f),
            new(0.60f, 0.36f, 0.86f),
            new(0.18f, 0.18f, 0.18f)
        };
        private static readonly HashSet<string> TraceCategories = new(StringComparer.Ordinal)
        {
            "Authority",
            "Turn",
            "TurnSync",
            "Setup",
            "Command"
        };

        private GameRuntimeConfig _config;
        private BoardBootstrap _board;
        private SteamLobbyService _lobby;
        private MapData _map;

        private string _matchId;
        private int _sequence;
        private int _turnIndex;
        private int _roundIndex = 1;
        private int _rngSeed;
        private int _shuffleNonce;
        private int _rngCounter;
        private string _lastChecksum = string.Empty;
        private string _lastMessage = "ready";
        private string _lastLoggedMessage = string.Empty;
        private MatchPhase _phase = MatchPhase.Reinforce;
        private int _activePlayerIndex;
        private int _winnerPlayerIndex = -1;
        private int _localPlayerIndex = 0;
        private ulong _localSteamId;
        private int _setupPlacementsThisTurn;
        private int _turnSetupPlacements;
        private int _turnReinforcementsPlaced;
        private int _turnAttacksResolved;
        private int _turnTerritoriesCaptured;
        private int _turnFortifyArmiesMoved;
        private int _turnCardsDrawn;

        private string _pendingSourceTerritory = string.Empty;
        private int _pendingAttackDice = 1;
        private int _pendingFortifyArmies = 1;
        private string _pendingCaptureFromTerritory = string.Empty;
        private string _pendingCaptureToTerritory = string.Empty;
        private int _pendingCaptureMinArmies;
        private int _pendingCaptureMaxArmies;
        private int _pendingCaptureArmiesToMove;
        private bool _capturedTerritoryThisTurn;
        private bool _fortifyUsedThisTurn;
        private int _setupFirstPlayerIndex;
        private bool _verboseLogs;
        private bool _inputTraceInitialized;
        private bool _lastTracedLocalCanAct;
        private int _lastTracedLocalPlayerIndex = -1;
        private int _lastTracedActivePlayerIndex = -1;
        private MatchPhase _lastTracedPhase;
        private bool _matchInitialized;
        private string _lastPublishedAuthoritativeSnapshot = string.Empty;
        private string _lastAppliedAuthoritativeSnapshot = string.Empty;
        private string _lastProcessedTurnIntentId = string.Empty;
        private int _localTurnIntentSequence;
        private StateSnapshotEnvelope _lastSnapshot;
        private ReconnectResponse _lastReconnect;
        private const int MaxAttackDice = 3;
        private const int MaxDefendDice = 3;
        private const int ForcedTradeThreshold = 6;

        public string PhaseName => _phase.ToString();
        public int ActivePlayerIndex => _players.Count > 0 ? CurrentPlayer.Index : -1;
        public string ActivePlayerId => _players.Count > 0 ? CurrentPlayer.PlayerId : "n/a";
        public string ActivePlayerDisplayName => _players.Count > 0 && !string.IsNullOrWhiteSpace(CurrentPlayer.DisplayName) ? CurrentPlayer.DisplayName : ActivePlayerId;
        public Color ActivePlayerColor => _players.Count > 0 ? CurrentPlayer.Color : Color.white;
        public int LocalPlayerIndex => _localPlayerIndex;
        public string LocalPlayerId => _localPlayerIndex >= 0 && _localPlayerIndex < _players.Count ? _players[_localPlayerIndex].PlayerId : string.Empty;
        public string LocalPlayerColorId => _playerColorIdByIndex.TryGetValue(_localPlayerIndex, out var colorId) ? colorId : string.Empty;
        public int ActiveReinforcementPool => _players.Count > 0 ? CurrentPlayer.ReinforcementPool : 0;
        public int ActiveSetupArmiesRemaining => _players.Count > 0 ? CurrentPlayer.SetupArmiesRemaining : 0;
        public int ActiveSetupPlacementsRemainingThisTurn => Mathf.Max(0, 3 - _setupPlacementsThisTurn);
        public int UnclaimedTerritoryCount
        {
            get
            {
                var count = 0;
                foreach (var territory in _territories.Values)
                {
                    if (territory.OwnerIndex < 0)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
        public string PendingSourceTerritoryId => _pendingSourceTerritory;
        public int PendingAttackDice => _pendingAttackDice;
        public int PendingAttackMaxDice
        {
            get
            {
                if (_phase != MatchPhase.Attack || string.IsNullOrEmpty(_pendingSourceTerritory) || !_territories.TryGetValue(_pendingSourceTerritory, out var source))
                {
                    return 1;
                }

                return Mathf.Clamp(source.Armies - 1, 1, MaxAttackDice);
            }
        }
        public int PendingFortifyArmies => _pendingFortifyArmies;
        public int PendingFortifyMaxArmies
        {
            get
            {
                if (_phase != MatchPhase.Fortify || string.IsNullOrEmpty(_pendingSourceTerritory) || !_territories.TryGetValue(_pendingSourceTerritory, out var source))
                {
                    return 1;
                }

                return Mathf.Max(1, source.Armies - 1);
            }
        }
        public bool HasPendingCaptureMove => !string.IsNullOrEmpty(_pendingCaptureFromTerritory) && !string.IsNullOrEmpty(_pendingCaptureToTerritory);
        public string PendingCaptureFromTerritoryId => _pendingCaptureFromTerritory;
        public string PendingCaptureToTerritoryId => _pendingCaptureToTerritory;
        public int PendingCaptureMinArmies => _pendingCaptureMinArmies;
        public int PendingCaptureMaxArmies => _pendingCaptureMaxArmies;
        public int PendingCaptureCurrentArmies => _pendingCaptureArmiesToMove;
        public bool IsGameEnded => _winnerPlayerIndex >= 0;
        public bool CanLocalPlayerAct => !IsGameEnded && IsLocalPlayersTurn();
        public string WinnerPlayerId => _winnerPlayerIndex >= 0 && _winnerPlayerIndex < _players.Count ? _players[_winnerPlayerIndex].PlayerId : string.Empty;
        public string StatusMessage => _lastMessage;
        public IReadOnlyList<string> RecentActionFeed => _actionFeed;
        public string SelectedTerritoryId => _board?.SelectedTerritory?.TerritoryId ?? string.Empty;
        public string SelectedTerritoryName => _board?.SelectedTerritory?.DisplayName ?? string.Empty;
        public string ActivePlayerObjectiveCardId => _players.Count > 0 && _playerObjectiveCards.TryGetValue(CurrentPlayer.Index, out var objectiveId) ? objectiveId : "none";
        public string ActivePlayerObjectiveCardBaseId => GetObjectiveBaseId(ActivePlayerObjectiveCardId);
        public string ActivePlayerObjectiveCardTitle
        {
            get
            {
                var id = ActivePlayerObjectiveCardId;
                var baseId = GetObjectiveBaseId(id);
                return ObjectiveTitleById.TryGetValue(baseId, out var objectiveTitle) ? objectiveTitle : baseId;
            }
        }
        public string ActivePlayerObjectiveCard
        {
            get
            {
                var id = ActivePlayerObjectiveCardId;
                var baseId = GetObjectiveBaseId(id);
                return ObjectiveTextById.TryGetValue(baseId, out var objectiveText) ? objectiveText : id;
            }
        }
        public IReadOnlyList<string> ActivePlayerAssignedTerritoryCards => _players.Count > 0 && _playerAssignedTerritoryCards.TryGetValue(CurrentPlayer.Index, out var cards) ? cards : EmptyCards;
        public int ActivePlayerHandCount => _players.Count > 0 && _playerTerritoryHands.TryGetValue(CurrentPlayer.Index, out var hand) ? hand.Count : 0;
        public bool CanActivePlayerTradeCards => _players.Count > 0 && TryFindBestTradeSet(CurrentPlayer.Index, out _);
        public int BestActivePlayerTradeInValue => _players.Count > 0 && TryFindBestTradeSet(CurrentPlayer.Index, out var candidate) ? candidate.TotalReinforcement : 0;

        public IReadOnlyList<PlayerVisualData> GetPlayerVisualData()
        {
            var result = new List<PlayerVisualData>(_players.Count);
            for (var i = 0; i < _players.Count; i++)
            {
                var p = _players[i];
                result.Add(new PlayerVisualData(p.Index, p.PlayerId, p.DisplayName, p.Color));
            }

            return result;
        }

        public string GetPlayerObjectiveCardId(int playerIndex)
        {
            return _playerObjectiveCards.TryGetValue(playerIndex, out var objectiveId) ? objectiveId : string.Empty;
        }

        public string GetPlayerObjectiveCardBaseId(int playerIndex)
        {
            var id = GetPlayerObjectiveCardId(playerIndex);
            return GetObjectiveBaseId(id);
        }

        public int GetPlayerHandCount(int playerIndex)
        {
            return _playerTerritoryHands.TryGetValue(playerIndex, out var hand) ? hand.Count : 0;
        }

        public IReadOnlyList<string> GetPlayerHandCardIds(int playerIndex)
        {
            if (!_playerTerritoryHands.TryGetValue(playerIndex, out var hand) || hand.Count == 0)
            {
                return EmptyHandCards;
            }

            var ids = new List<string>(hand.Count);
            for (var i = 0; i < hand.Count; i++)
            {
                ids.Add(hand[i].CardId);
            }

            return ids;
        }

        public IReadOnlyList<string> GetPlayerAssignedTerritoryCardIds(int playerIndex)
        {
            return _playerAssignedTerritoryCards.TryGetValue(playerIndex, out var cards) ? cards : EmptyCards;
        }

        public IReadOnlyList<string> GetActivePlayerAssignedTerritoryCardNames()
        {
            var ids = ActivePlayerAssignedTerritoryCards;
            if (ids == null || ids.Count == 0)
            {
                return EmptyCards;
            }

            var names = new List<string>(ids.Count);
            foreach (var id in ids)
            {
                if (_board != null && _board.Nodes != null && _board.Nodes.TryGetValue(id, out var node) && node != null && !string.IsNullOrWhiteSpace(node.DisplayName))
                {
                    names.Add(node.DisplayName);
                }
                else
                {
                    names.Add(id);
                }
            }

            return names;
        }

        public IReadOnlyList<string> GetActivePlayerHandCardNames()
        {
            if (!_playerTerritoryHands.TryGetValue(CurrentPlayer.Index, out var hand) || hand.Count == 0)
            {
                return EmptyHandCards;
            }

            var labels = new List<string>(hand.Count);
            for (var i = 0; i < hand.Count; i++)
            {
                var card = hand[i];
                if (card.IsJoker)
                {
                    labels.Add("Jolly");
                    continue;
                }

                var territory = ResolveTerritoryDisplayName(card.TerritoryId);
                labels.Add($"{territory} ({card.Symbol.ToString().ToLowerInvariant()})");
            }

            return labels;
        }

        public IReadOnlyList<HandCardUiData> GetActivePlayerHandCardsUi()
        {
            if (!_playerTerritoryHands.TryGetValue(CurrentPlayer.Index, out var hand) || hand.Count == 0)
            {
                return Array.Empty<HandCardUiData>();
            }

            var cards = new List<HandCardUiData>(hand.Count);
            for (var i = 0; i < hand.Count; i++)
            {
                var card = hand[i];
                if (card.IsJoker)
                {
                    cards.Add(new HandCardUiData(card.CardId, "Jolly", "joker", true));
                    continue;
                }

                cards.Add(new HandCardUiData(
                    card.CardId,
                    ResolveTerritoryDisplayName(card.TerritoryId),
                    card.Symbol.ToString().ToLowerInvariant(),
                    false));
            }

            return cards;
        }

        public void Initialize(GameRuntimeConfig config)
        {
            _config = config;
        }

        private void Start()
        {
            _verboseLogs = _config != null && _config.EnableVerboseRuntimeLogs;
            _board = FindFirstObjectByType<BoardBootstrap>();
            _lobby = FindFirstObjectByType<SteamLobbyService>();
            _matchId = $"match-{Guid.NewGuid().ToString("N")[..8]}";

            if (_board == null)
            {
                Debug.LogError("[Risiko3D][MatchLoop] BoardBootstrap not found.");
                enabled = false;
                return;
            }

            _board.TerritorySelected += OnTerritorySelected;
            _board.SetTerritorySelectionGate(() => CanLocalPlayerAct);
            LoadMapData();
            LoadTerritorySymbolManifest();
            TryInitializeMatchState();
        }

        private void OnDestroy()
        {
            if (_board != null)
            {
                _board.TerritorySelected -= OnTerritorySelected;
                _board.SetTerritorySelectionGate(null);
            }
        }

        private void Update()
        {
            var input = _board != null ? _board.InputAdapter : null;
            if (input == null || !input.IsReady)
            {
                CaptureStatusForActionFeed();
                return;
            }

            if (!_matchInitialized)
            {
                TryInitializeMatchState();
                CaptureStatusForActionFeed();
                return;
            }

            if (_lobby != null && _lobby.IsInLobby && !_lobby.IsLocalHost)
            {
                TryApplyAuthoritativeSnapshotFromLobby();
                ProcessClientTurnInputAsIntent();
                CaptureStatusForActionFeed();
                return;
            }

            ProcessTurnIntentAsHost();
            SyncActiveTurnIndexFromLobby();
            var localCanAct = IsLocalPlayersTurn();
            TraceInputStateIfChanged(localCanAct);

            if (localCanAct && input.WasSubmitCommandPressedThisFrame())
            {
                SubmitPhaseCommand();
            }

            if (localCanAct && input.WasEndTurnPressedThisFrame())
            {
                EndTurn();
            }

            if (localCanAct && input.WasDecreaseActionValuePressedThisFrame())
            {
                if (_phase == MatchPhase.Attack)
                {
                    if (HasPendingCaptureMove)
                    {
                        UiCaptureMoveDecrease();
                    }
                    else
                    {
                        UiAttackDiceDecrease();
                    }
                }
                else if (_phase == MatchPhase.Fortify)
                {
                    UiFortifyArmiesDecrease();
                }
            }

            if (localCanAct && input.WasIncreaseActionValuePressedThisFrame())
            {
                if (_phase == MatchPhase.Attack)
                {
                    if (HasPendingCaptureMove)
                    {
                        UiCaptureMoveIncrease();
                    }
                    else
                    {
                        UiAttackDiceIncrease();
                    }
                }
                else if (_phase == MatchPhase.Fortify)
                {
                    UiFortifyArmiesIncrease();
                }
            }

            if (input.WasCaptureSnapshotPressedThisFrame())
            {
                CaptureSnapshot();
            }

            if (input.WasSimulateReconnectPressedThisFrame())
            {
                SimulateReconnect();
            }

            PublishAuthoritativeSnapshotIfHost();
            CaptureStatusForActionFeed();
        }

        private bool TryInitializeMatchState()
        {
            if (_matchInitialized)
            {
                return true;
            }

            if (!IsLobbyRosterReadyForInitialization())
            {
                _lastMessage = "waiting for lobby roster sync";
                return false;
            }

            _rngSeed = ResolveDeterministicMatchSeed();
            _shuffleNonce = 0;

            InitializePlayers();
            if (_players.Count <= 0)
            {
                _lastMessage = "waiting for players";
                return false;
            }

            InitializeTerritories();
            ApplyAllTerritoriesToBoard();
            StartSetupPhase();

            _lastMessage = $"Host loop initialized ({_config.ProtocolVersion}).";
            LogAuthoritativeState("match-initialized");
            _matchInitialized = true;
            PublishAuthoritativeSnapshotIfHost();
            return true;
        }

        private bool IsLobbyRosterReadyForInitialization()
        {
            if (_lobby == null || !_lobby.IsInLobby)
            {
                return true;
            }

            if (!_lobby.TryGetCurrentLobbyMembers(out var members, out _) || members == null || members.Count < 2)
            {
                return false;
            }

            for (var i = 0; i < members.Count; i++)
            {
                if (members[i].IsLocal)
                {
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<string> GetRecentActionFeed(int maxCount)
        {
            if (maxCount <= 0 || _actionFeed.Count == 0)
            {
                return Array.Empty<string>();
            }

            var count = Mathf.Min(maxCount, _actionFeed.Count);
            var start = _actionFeed.Count - count;
            var copy = new List<string>(count);
            for (var i = start; i < _actionFeed.Count; i++)
            {
                copy.Add(_actionFeed[i]);
            }

            return copy;
        }

        private void CaptureStatusForActionFeed()
        {
            if (string.IsNullOrWhiteSpace(_lastMessage))
            {
                return;
            }

            if (string.Equals(_lastLoggedMessage, _lastMessage, StringComparison.Ordinal))
            {
                return;
            }

            _lastLoggedMessage = _lastMessage;
            var playerId = _players.Count > 0 ? CurrentPlayer.PlayerId : "system";
            var line = $"R{_roundIndex} T{_turnIndex} [{_phase}] {playerId}: {_lastMessage}";
            _actionFeed.Add(line);
            if (_actionFeed.Count > 80)
            {
                _actionFeed.RemoveAt(0);
            }
        }

        private void PushActionFeedEntry(string playerId, MatchPhase phase, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _lastLoggedMessage = message;
            var line = $"R{_roundIndex} T{_turnIndex} [{phase}] {playerId}: {message}";
            _actionFeed.Add(line);
            if (_actionFeed.Count > 80)
            {
                _actionFeed.RemoveAt(0);
            }
        }

        private void OnGUI()
        {
            // Debug overlay removed.
        }

        private PlayerState CurrentPlayer => _players[_activePlayerIndex];

        private void OnTerritorySelected(TerritoryNode node)
        {
            if (node == null)
            {
                return;
            }

            if (IsGameEnded)
            {
                _lastMessage = $"game ended, winner: {WinnerPlayerId}";
            }
        }

        private void InitializePlayers()
        {
            _players.Clear();
            _playerAssignedTerritoryCards.Clear();
            _playerObjectiveCards.Clear();
            _playerColorIdByIndex.Clear();
            _eliminatedPlayerIndices.Clear();
            _setupConfirmedPlayers.Clear();

            var roster = ResolveLobbyRoster();
            var colorOrder = BuildColorOrder(roster.Count);
            _localPlayerIndex = 0;
            _localSteamId = 0UL;

            for (var i = 0; i < roster.Count; i++)
            {
                var colorId = colorOrder[i];
                var playerId = $"player_{colorId}";
                _players.Add(new PlayerState
                {
                    Index = i,
                    PlayerId = playerId,
                    SteamId = roster[i].SteamId,
                    DisplayName = roster[i].DisplayName,
                    Color = ResolveColorForColorId(colorId),
                    ReinforcementPool = 0,
                    SetupArmiesRemaining = 0
                });
                _playerColorIdByIndex[i] = colorId;
                if (roster[i].IsLocal)
                {
                    _localPlayerIndex = i;
                    _localSteamId = roster[i].SteamId;
                }

                Trace("Roster", $"slot={i} playerId={playerId} steamId={roster[i].SteamId} local={roster[i].IsLocal} display='{roster[i].DisplayName}'");
            }

            foreach (var p in _players)
            {
                _playerAssignedTerritoryCards[p.Index] = new List<string>(16);
                _playerObjectiveCards[p.Index] = string.Empty;
            }

            _activePlayerIndex = 0;
            _phase = MatchPhase.SetupDeploy;
            _turnIndex = 0;
            _roundIndex = 1;
            _winnerPlayerIndex = -1;
            LogAuthoritativeState("roster-initialized");
        }

        private void ProcessClientTurnInputAsIntent()
        {
            var input = _board != null ? _board.InputAdapter : null;
            if (input == null || !IsLocalPlayersTurn())
            {
                return;
            }

            if (input.WasSubmitCommandPressedThisFrame())
            {
                var selected = _board != null ? _board.SelectedTerritory : null;
                if (selected != null)
                {
                    TrySendTurnIntent("submit", selected.TerritoryId);
                }
            }

            if (input.WasEndTurnPressedThisFrame())
            {
                TrySendTurnIntent("endturn", string.Empty);
            }
        }

        private void TrySendTurnIntent(string action, string territoryId)
        {
            if (_lobby == null || !_lobby.IsInLobby || _lobby.IsLocalHost || _localSteamId == 0UL)
            {
                return;
            }

            _localTurnIntentSequence++;
            var intent = new TurnIntentData
            {
                intentId = $"{_localSteamId}:{_localTurnIntentSequence}",
                senderSteamId = _localSteamId,
                action = action ?? string.Empty,
                territoryId = territoryId ?? string.Empty
            };

            var payload = JsonUtility.ToJson(intent);
            _lobby.TrySetTurnIntent(payload, out _);
        }

        private void ProcessTurnIntentAsHost()
        {
            if (_lobby == null || !_lobby.IsInLobby || !_lobby.IsLocalHost)
            {
                return;
            }

            if (!_lobby.TryGetTurnIntent(out var payload, out _) || string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            TurnIntentData intent;
            try
            {
                intent = JsonUtility.FromJson<TurnIntentData>(payload);
            }
            catch
            {
                return;
            }

            if (intent == null || string.IsNullOrWhiteSpace(intent.intentId) || string.Equals(intent.intentId, _lastProcessedTurnIntentId, StringComparison.Ordinal))
            {
                return;
            }

            _lastProcessedTurnIntentId = intent.intentId;

            var senderIndex = FindPlayerIndexBySteamId(intent.senderSteamId);
            if (senderIndex < 0 || senderIndex != _activePlayerIndex)
            {
                return;
            }

            if (string.Equals(intent.action, "submit", StringComparison.Ordinal))
            {
                ExecutePhaseCommandForTerritory(intent.territoryId);
                PublishAuthoritativeSnapshotIfHost();
                return;
            }

            if (string.Equals(intent.action, "endturn", StringComparison.Ordinal))
            {
                ExecuteEndTurnAsHostAuthoritative();
                PublishAuthoritativeSnapshotIfHost();
            }
        }

        private int FindPlayerIndexBySteamId(ulong steamId)
        {
            if (steamId == 0UL)
            {
                return -1;
            }

            for (var i = 0; i < _players.Count; i++)
            {
                if (_players[i].SteamId == steamId)
                {
                    return i;
                }
            }

            return -1;
        }

        private void ExecutePhaseCommandForTerritory(string territoryId)
        {
            if (IsGameEnded || string.IsNullOrWhiteSpace(territoryId))
            {
                return;
            }

            if (_phase == MatchPhase.Attack && HasPendingCaptureMove)
            {
                ResolvePendingCaptureMove();
                return;
            }

            switch (_phase)
            {
                case MatchPhase.SetupClaim:
                    SubmitSetupClaim(territoryId);
                    break;
                case MatchPhase.SetupDeploy:
                    SubmitSetupDeploy(territoryId);
                    break;
                case MatchPhase.Reinforce:
                    SubmitReinforce(territoryId);
                    break;
                case MatchPhase.Attack:
                    SubmitAttack(territoryId);
                    break;
                case MatchPhase.Fortify:
                    SubmitFortify(territoryId);
                    break;
            }
        }

        private void ExecuteEndTurnAsHostAuthoritative()
        {
            var previousLocalIndex = _localPlayerIndex;
            _localPlayerIndex = _activePlayerIndex;
            EndTurn();
            _localPlayerIndex = previousLocalIndex;
        }

        private List<RosterPlayer> ResolveLobbyRoster()
        {
            var roster = new List<RosterPlayer>(6);
            if (_lobby != null &&
                _lobby.IsInLobby &&
                _lobby.TryGetCurrentLobbyMembers(out var members, out _) &&
                members != null &&
                members.Count > 0)
            {
                var hasHostSteamId = _lobby.TryGetHostSteamId(out var hostSteamId, out _);
                members.Sort((a, b) =>
                {
                    if (hasHostSteamId)
                    {
                        var aIsHost = a.SteamId == hostSteamId;
                        var bIsHost = b.SteamId == hostSteamId;
                        if (aIsHost != bIsHost)
                        {
                            return aIsHost ? -1 : 1;
                        }
                    }

                    var bySteamId = a.SteamId.CompareTo(b.SteamId);
                    if (bySteamId != 0)
                    {
                        return bySteamId;
                    }

                    return string.CompareOrdinal(a.DisplayName, b.DisplayName);
                });

                var capped = Mathf.Clamp(members.Count, 2, 6);
                for (var i = 0; i < capped; i++)
                {
                    var m = members[i];
                    var display = string.IsNullOrWhiteSpace(m.DisplayName) ? $"Player {i + 1}" : m.DisplayName.Trim();
                    roster.Add(new RosterPlayer
                    {
                        SteamId = m.SteamId,
                        DisplayName = display,
                        IsLocal = m.IsLocal
                    });
                }

                return roster;
            }

            var fallback = Mathf.Clamp(_config != null ? _config.MinPlayers : 3, 2, 6);
            const ulong baseId = 11000000000000000UL;
            for (var i = 0; i < fallback; i++)
            {
                roster.Add(new RosterPlayer
                {
                    SteamId = baseId + (ulong)(i + 1),
                    DisplayName = $"EditorPlayer{i + 1:000}",
                    IsLocal = i == 0
                });
            }

            return roster;
        }

        private void TraceInputStateIfChanged(bool localCanAct)
        {
            if (!_inputTraceInitialized ||
                _lastTracedLocalCanAct != localCanAct ||
                _lastTracedLocalPlayerIndex != _localPlayerIndex ||
                _lastTracedActivePlayerIndex != _activePlayerIndex ||
                _lastTracedPhase != _phase)
            {
                _inputTraceInitialized = true;
                _lastTracedLocalCanAct = localCanAct;
                _lastTracedLocalPlayerIndex = _localPlayerIndex;
                _lastTracedActivePlayerIndex = _activePlayerIndex;
                _lastTracedPhase = _phase;
                Trace("Input", $"localCanAct={localCanAct} local={_localPlayerIndex} active={_activePlayerIndex} phase={_phase}");
            }
        }

        private List<string> BuildColorOrder(int playerCount)
        {
            var count = Mathf.Clamp(playerCount, 2, PlayerColorCycle.Length);
            var colors = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                colors.Add(PlayerColorCycle[i]);
            }

            var lobbyId = _lobby != null ? _lobby.CurrentLobbyId : 0UL;
            var seed = lobbyId != 0
                ? unchecked((int)(lobbyId ^ (lobbyId >> 32)))
                : Environment.TickCount;
            var rng = new System.Random(seed);
            for (var i = colors.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (colors[i], colors[j]) = (colors[j], colors[i]);
            }

            return colors;
        }

        private static Color ResolveColorForColorId(string colorId)
        {
            for (var i = 0; i < PlayerColorCycle.Length && i < PlayerColorPalette.Length; i++)
            {
                if (string.Equals(PlayerColorCycle[i], colorId, StringComparison.Ordinal))
                {
                    return PlayerColorPalette[i];
                }
            }

            return Color.white;
        }

        private sealed class RosterPlayer
        {
            public ulong SteamId;
            public string DisplayName;
            public bool IsLocal;
        }

        private void InitializeTerritories()
        {
            _territories.Clear();
            var ids = new List<string>(_board.Nodes.Keys);
            ids.Sort(StringComparer.Ordinal);
            for (var i = 0; i < ids.Count; i++)
            {
                _territories[ids[i]] = new TerritoryState
                {
                    OwnerIndex = -1,
                    Armies = 0
                };
            }
        }

        private void ApplyAllTerritoriesToBoard()
        {
            foreach (var kv in _territories)
            {
                if (kv.Value.OwnerIndex < 0 || kv.Value.OwnerIndex >= _players.Count)
                {
                    _board.TrySetTerritoryState(kv.Key, -1, new Color(0.48f, 0.48f, 0.48f), kv.Value.Armies);
                    continue;
                }

                var owner = _players[kv.Value.OwnerIndex];
                _board.TrySetTerritoryState(kv.Key, owner.Index, owner.Color, kv.Value.Armies);
            }
        }

        private void SubmitPhaseCommand()
        {
            if (!IsLocalPlayersTurn())
            {
                _lastMessage = "wait for your turn";
                Trace("Command", $"blocked submit phase={_phase} local={_localPlayerIndex} active={_activePlayerIndex}");
                return;
            }

            if (IsGameEnded)
            {
                _lastMessage = $"game ended, winner: {WinnerPlayerId}";
                return;
            }

            if (_phase == MatchPhase.Attack && HasPendingCaptureMove)
            {
                ResolvePendingCaptureMove();
                return;
            }

            var selected = _board.SelectedTerritory;
            if (selected == null)
            {
                _lastMessage = "no territory selected";
                Trace("Command", $"submit phase={_phase} without selection");
                return;
            }

            Trace("Command", $"submit phase={_phase} territory={selected.TerritoryId}");

            switch (_phase)
            {
                case MatchPhase.SetupClaim:
                    SubmitSetupClaim(selected.TerritoryId);
                    break;
                case MatchPhase.SetupDeploy:
                    SubmitSetupDeploy(selected.TerritoryId);
                    break;
                case MatchPhase.Reinforce:
                    SubmitReinforce(selected.TerritoryId);
                    break;
                case MatchPhase.Attack:
                    SubmitAttack(selected.TerritoryId);
                    break;
                case MatchPhase.Fortify:
                    SubmitFortify(selected.TerritoryId);
                    break;
            }
        }

        public void UiSubmitAction()
        {
            if (!IsLocalPlayersTurn())
            {
                _lastMessage = "wait for your turn";
                Trace("UI", "submit blocked (not local turn)");
                return;
            }

            SubmitPhaseCommand();
        }

        public void UiAdvanceAction()
        {
            if (!IsLocalPlayersTurn())
            {
                _lastMessage = "wait for your turn";
                Trace("UI", "advance blocked (not local turn)");
                return;
            }

            EndTurn();
        }

        public void UiTradeCards()
        {
            if (!IsLocalPlayersTurn())
            {
                _lastMessage = "wait for your turn";
                Trace("UI", "trade blocked (not local turn)");
                return;
            }

            if (_phase != MatchPhase.Reinforce)
            {
                _lastMessage = "trade-in disponibile solo in fase Reinforce";
                return;
            }

            if (!TryTradeCardsForPlayer(CurrentPlayer.Index, true))
            {
                _lastMessage = "nessun tris valido da scambiare";
            }
        }

        public void UiCaptureMoveIncrease()
        {
            if (!HasPendingCaptureMove)
            {
                return;
            }

            _pendingCaptureArmiesToMove = Mathf.Min(_pendingCaptureMaxArmies, _pendingCaptureArmiesToMove + 1);
        }

        public void UiCaptureMoveDecrease()
        {
            if (!HasPendingCaptureMove)
            {
                return;
            }

            _pendingCaptureArmiesToMove = Mathf.Max(_pendingCaptureMinArmies, _pendingCaptureArmiesToMove - 1);
        }

        public void UiAttackDiceIncrease()
        {
            if (_phase != MatchPhase.Attack || string.IsNullOrEmpty(_pendingSourceTerritory) || !_territories.TryGetValue(_pendingSourceTerritory, out var source))
            {
                return;
            }

            var max = Mathf.Clamp(source.Armies - 1, 1, MaxAttackDice);
            _pendingAttackDice = Mathf.Clamp(_pendingAttackDice + 1, 1, max);
        }

        public void UiAttackDiceDecrease()
        {
            if (_phase != MatchPhase.Attack || string.IsNullOrEmpty(_pendingSourceTerritory) || !_territories.TryGetValue(_pendingSourceTerritory, out var source))
            {
                return;
            }

            var max = Mathf.Clamp(source.Armies - 1, 1, MaxAttackDice);
            _pendingAttackDice = Mathf.Clamp(_pendingAttackDice - 1, 1, max);
        }

        public void UiFortifyArmiesIncrease()
        {
            if (_phase != MatchPhase.Fortify || string.IsNullOrEmpty(_pendingSourceTerritory) || !_territories.TryGetValue(_pendingSourceTerritory, out var source))
            {
                return;
            }

            var max = Mathf.Max(1, source.Armies - 1);
            _pendingFortifyArmies = Mathf.Clamp(_pendingFortifyArmies + 1, 1, max);
        }

        public void UiFortifyArmiesDecrease()
        {
            if (_phase != MatchPhase.Fortify || string.IsNullOrEmpty(_pendingSourceTerritory) || !_territories.TryGetValue(_pendingSourceTerritory, out var source))
            {
                return;
            }

            var max = Mathf.Max(1, source.Armies - 1);
            _pendingFortifyArmies = Mathf.Clamp(_pendingFortifyArmies - 1, 1, max);
        }

        private void SubmitSetupClaim(string territoryId)
        {
            if (!_territories.TryGetValue(territoryId, out var state))
            {
                _lastMessage = "setup claim invalid: unknown territory";
                return;
            }

            if (state.OwnerIndex >= 0)
            {
                _lastMessage = "setup claim invalid: territory already claimed";
                return;
            }

            state.OwnerIndex = CurrentPlayer.Index;
            state.Armies = 1;
            CurrentPlayer.SetupArmiesRemaining = Mathf.Max(0, CurrentPlayer.SetupArmiesRemaining - 1);
            ApplyTerritory(territoryId);

            if (AllTerritoriesClaimed())
            {
                _phase = MatchPhase.SetupDeploy;
                _activePlayerIndex = _setupFirstPlayerIndex;
                AdvanceToNextPlayerWithSetupArmies();
                _lastMessage = "setup claim complete -> setup deploy";
                return;
            }

            AdvanceToNextPlayerTurnOrder();
            _lastMessage = $"claimed {ResolveTerritoryDisplayName(territoryId)}; next player setup claim";
        }

        private void SubmitSetupDeploy(string territoryId)
        {
            if (!IsOwnedByCurrentPlayer(territoryId))
            {
                _lastMessage = "setup deploy invalid: territory must be owned by active player";
                return;
            }

            if (CurrentPlayer.SetupArmiesRemaining <= 0)
            {
                _lastMessage = "setup deploy invalid: no setup armies remaining";
                Trace("Setup", $"deploy invalid player={CurrentPlayer.PlayerId} remaining={CurrentPlayer.SetupArmiesRemaining}");
                AdvanceToNextPlayerWithSetupArmies();
                return;
            }

            _territories[territoryId].Armies += 1;
            CurrentPlayer.SetupArmiesRemaining -= 1;
            _setupPlacementsThisTurn += 1;
            _turnSetupPlacements += 1;
            ApplyTerritory(territoryId);
            Trace("Setup", $"deploy player={CurrentPlayer.PlayerId} territory={territoryId} remaining={CurrentPlayer.SetupArmiesRemaining} placementsThisTurn={_setupPlacementsThisTurn}");

            if (AllSetupArmiesPlaced())
            {
                _activePlayerIndex = _setupFirstPlayerIndex;
                StartTurnForCurrentPlayer();
                _lastMessage = $"setup complete -> turn starts for {CurrentPlayer.PlayerId}";
                return;
            }

            if (CurrentPlayer.SetupArmiesRemaining <= 0 || _setupPlacementsThisTurn >= 3)
            {
                Trace("Setup", $"advance setup player={CurrentPlayer.PlayerId} remaining={CurrentPlayer.SetupArmiesRemaining} placementsThisTurn={_setupPlacementsThisTurn}");
                AdvanceToNextPlayerWithSetupArmies();
                _setupPlacementsThisTurn = 0;
            }

            _lastMessage = "setup deploy applied (max 3 per setup turn)";
        }

        private void SubmitReinforce(string territoryId)
        {
            if (ActivePlayerHandCount >= ForcedTradeThreshold)
            {
                _lastMessage = $"card trade required before reinforcements (hand >= {ForcedTradeThreshold})";
                return;
            }

            if (!IsOwnedByCurrentPlayer(territoryId))
            {
                _lastMessage = "reinforce invalid: territory not owned by active player";
                return;
            }

            if (CurrentPlayer.ReinforcementPool <= 0)
            {
                _lastMessage = "reinforce invalid: no reinforcement points left";
                return;
            }

            var cmdJson = $"{{\"phase\":\"Reinforce\",\"territoryId\":\"{territoryId}\",\"delta\":1}}";
            var command = BuildCommand("ReinforceCommand", cmdJson);
            if (!TryApplyCommand(command, "Reinforced"))
            {
                return;
            }

            _territories[territoryId].Armies += 1;
            CurrentPlayer.ReinforcementPool -= 1;
            _turnReinforcementsPlaced += 1;
            ApplyTerritory(territoryId);
            TryCheckObjectiveCompletion(CurrentPlayer.Index);

            if (CurrentPlayer.ReinforcementPool == 0)
            {
                _phase = MatchPhase.Attack;
                _lastMessage = "reinforce complete -> phase Attack";
            }
        }

        private void SubmitAttack(string territoryId)
        {
            if (HasPendingCaptureMove)
            {
                ResolvePendingCaptureMove();
                return;
            }

            if (string.IsNullOrEmpty(_pendingSourceTerritory))
            {
                if (!IsOwnedByCurrentPlayer(territoryId))
                {
                    _lastMessage = "attack source must be owned by active player";
                    return;
                }

                if (_territories[territoryId].Armies <= 1)
                {
                    _lastMessage = "attack source needs at least 2 armies";
                    return;
                }

                _pendingSourceTerritory = territoryId;
                var max = Mathf.Clamp(_territories[territoryId].Armies - 1, 1, MaxAttackDice);
                _pendingAttackDice = max;
                _lastMessage = $"attack source set: {ResolveTerritoryDisplayName(_pendingSourceTerritory)}";
                return;
            }

            var source = _pendingSourceTerritory;
            var target = territoryId;
            if (source == target)
            {
                _lastMessage = "attack target must differ from source";
                return;
            }

            if (!_board.AreAdjacent(source, target))
            {
                _lastMessage = "attack invalid: territories are not adjacent";
                return;
            }

            if (IsOwnedByCurrentPlayer(target))
            {
                _lastMessage = "attack invalid: target owned by active player";
                return;
            }

            var cmdJson = $"{{\"phase\":\"Attack\",\"source\":\"{source}\",\"target\":\"{target}\"}}";
            var command = BuildCommand("AttackCommand", cmdJson);
            if (!TryApplyCommand(command, "AttackResolved"))
            {
                return;
            }

            ResolveAttack(source, target, command.CommandId, _pendingAttackDice);
            _pendingSourceTerritory = string.Empty;
            _pendingAttackDice = 1;
        }

        private void ResolveAttack(string source, string target, string commandId, int attackerDiceRequested)
        {
            var sourceState = _territories[source];
            var targetState = _territories[target];
            if (sourceState.Armies <= 1 || targetState.Armies <= 0)
            {
                _lastMessage = "attack skipped: invalid armies";
                return;
            }

            var maxAttackDice = Mathf.Clamp(sourceState.Armies - 1, 1, MaxAttackDice);
            var attackDiceCount = Mathf.Clamp(attackerDiceRequested, 1, maxAttackDice);
            var defendDiceCount = Mathf.Clamp(targetState.Armies, 1, MaxDefendDice);
            var rng = BuildDeterministicRng(commandId);
            var attackRolls = RollDiceDescending(rng, attackDiceCount);
            var defendRolls = RollDiceDescending(rng, defendDiceCount);

            var comparisons = Mathf.Min(attackRolls.Length, defendRolls.Length);
            _turnAttacksResolved += 1;
            var attackerLosses = 0;
            var defenderLosses = 0;
            for (var i = 0; i < comparisons; i++)
            {
                if (attackRolls[i] > defendRolls[i])
                {
                    defenderLosses++;
                }
                else
                {
                    attackerLosses++;
                }
            }

            sourceState.Armies = Mathf.Max(1, sourceState.Armies - attackerLosses);
            targetState.Armies = Mathf.Max(0, targetState.Armies - defenderLosses);

            if (targetState.Armies > 0)
            {
                _territories[source] = sourceState;
                _territories[target] = targetState;
                ApplyTerritory(source);
                ApplyTerritory(target);
                TryCheckObjectiveCompletion(CurrentPlayer.Index);
                _lastMessage = $"attack: A[{string.Join(",", attackRolls)}] vs D[{string.Join(",", defendRolls)}], losses A-{attackerLosses} D-{defenderLosses}";
                return;
            }

            var previousOwnerIndex = targetState.OwnerIndex;
            targetState.OwnerIndex = CurrentPlayer.Index;
            var minCaptureMove = Mathf.Clamp(attackDiceCount - attackerLosses, 1, sourceState.Armies - 1);
            sourceState.Armies -= minCaptureMove;
            targetState.Armies = minCaptureMove;

            _territories[source] = sourceState;
            _territories[target] = targetState;
            var firstCaptureThisTurn = !_capturedTerritoryThisTurn;
            _capturedTerritoryThisTurn = true;
            _pendingCaptureFromTerritory = source;
            _pendingCaptureToTerritory = target;
            _pendingCaptureMinArmies = minCaptureMove;
            _pendingCaptureMaxArmies = minCaptureMove + Mathf.Max(0, sourceState.Armies - 1);
            _pendingCaptureArmiesToMove = minCaptureMove;
            ApplyTerritory(source);
            ApplyTerritory(target);

            var eliminatedOwner = FindEliminatedOwnerAfterCapture(previousOwnerIndex, CurrentPlayer.Index);
            if (eliminatedOwner >= 0)
            {
                _eliminatedPlayerIndices.Add(eliminatedOwner);
                TransferAllCards(eliminatedOwner, CurrentPlayer.Index);
            }

            if (firstCaptureThisTurn)
            {
                DrawTerritoryCardForPlayer(CurrentPlayer.Index);
                _turnCardsDrawn += 1;
            }
            _turnTerritoriesCaptured += 1;

            TryCheckObjectiveCompletion(CurrentPlayer.Index);
            _lastMessage = firstCaptureThisTurn
                ? $"capture: {ResolveTerritoryDisplayName(target)} captured by {CurrentPlayer.PlayerId} (+1 card, move min {minCaptureMove}, max {_pendingCaptureMaxArmies})"
                : $"capture: {ResolveTerritoryDisplayName(target)} captured by {CurrentPlayer.PlayerId} (move min {minCaptureMove}, max {_pendingCaptureMaxArmies})";
        }

        private void ResolvePendingCaptureMove()
        {
            if (!HasPendingCaptureMove)
            {
                return;
            }

            if (!_territories.TryGetValue(_pendingCaptureFromTerritory, out var fromState) ||
                !_territories.TryGetValue(_pendingCaptureToTerritory, out var toState))
            {
                ClearPendingCaptureMove();
                _lastMessage = "pending capture move invalidated";
                return;
            }

            var desiredTotal = Mathf.Clamp(_pendingCaptureArmiesToMove, _pendingCaptureMinArmies, _pendingCaptureMaxArmies);
            var additionalToMove = desiredTotal - toState.Armies;
            if (additionalToMove < 0 || additionalToMove >= fromState.Armies)
            {
                _lastMessage = "capture move invalid amount";
                return;
            }

            fromState.Armies -= additionalToMove;
            toState.Armies += additionalToMove;
            _territories[_pendingCaptureFromTerritory] = fromState;
            _territories[_pendingCaptureToTerritory] = toState;
            ApplyTerritory(_pendingCaptureFromTerritory);
            ApplyTerritory(_pendingCaptureToTerritory);
            ClearPendingCaptureMove();
            TryCheckObjectiveCompletion(CurrentPlayer.Index);
            _lastMessage = "captured armies moved";
        }

        private void SubmitFortify(string territoryId)
        {
            if (string.IsNullOrEmpty(_pendingSourceTerritory))
            {
                if (!IsOwnedByCurrentPlayer(territoryId))
                {
                    _lastMessage = "fortify source must be owned by active player";
                    return;
                }

                if (_territories[territoryId].Armies <= 1)
                {
                    _lastMessage = "fortify source needs at least 2 armies";
                    return;
                }

                _pendingSourceTerritory = territoryId;
                _pendingFortifyArmies = 1;
                _lastMessage = $"fortify source set: {ResolveTerritoryDisplayName(_pendingSourceTerritory)}";
                return;
            }

            var source = _pendingSourceTerritory;
            var target = territoryId;
            if (source == target)
            {
                _lastMessage = "fortify target must differ from source";
                return;
            }

            if (!AreConnectedByOwnedPath(source, target, CurrentPlayer.Index))
            {
                _lastMessage = "fortify invalid: source/target must be connected by owned path";
                return;
            }

            if (!IsOwnedByCurrentPlayer(target))
            {
                _lastMessage = "fortify invalid: target must be owned by active player";
                return;
            }

            var sourceArmies = _territories[source].Armies;
            var armiesToMove = Mathf.Clamp(_pendingFortifyArmies, 1, Mathf.Max(1, sourceArmies - 1));
            if (armiesToMove <= 0 || armiesToMove >= sourceArmies)
            {
                _lastMessage = "fortify invalid: amount must leave at least 1 army in source";
                return;
            }

            var cmdJson = $"{{\"phase\":\"Fortify\",\"source\":\"{source}\",\"target\":\"{target}\",\"delta\":{armiesToMove}}}";
            var command = BuildCommand("FortifyCommand", cmdJson);
            if (!TryApplyCommand(command, "Fortified"))
            {
                return;
            }

            _territories[source].Armies -= armiesToMove;
            _territories[target].Armies += armiesToMove;
            _turnFortifyArmiesMoved += armiesToMove;
            ApplyTerritory(source);
            ApplyTerritory(target);
            _pendingSourceTerritory = string.Empty;
            _pendingFortifyArmies = 1;
            _fortifyUsedThisTurn = true;
            TryCheckObjectiveCompletion(CurrentPlayer.Index);
            _lastMessage = "fortify applied -> ending turn";
            AdvanceToNextPlayerTurn();
        }

        private void EndTurn()
        {
            if (!IsLocalPlayersTurn())
            {
                _lastMessage = "wait for your turn";
                Trace("Turn", "end-turn blocked (not local turn)");
                return;
            }

            if (IsGameEnded)
            {
                _lastMessage = $"game ended, winner: {WinnerPlayerId}";
                return;
            }

            if (_phase == MatchPhase.SetupClaim || _phase == MatchPhase.SetupDeploy)
            {
                _lastMessage = "cannot skip setup action";
                return;
            }

            if (_phase == MatchPhase.Reinforce && CurrentPlayer.ReinforcementPool > 0)
            {
                _lastMessage = "cannot advance: reinforcement points remaining";
                return;
            }

            if (_phase == MatchPhase.Reinforce && ActivePlayerHandCount >= ForcedTradeThreshold)
            {
                _lastMessage = $"card trade required before leaving reinforcement phase (hand >= {ForcedTradeThreshold})";
                return;
            }

            if (_phase == MatchPhase.Attack)
            {
                if (HasPendingCaptureMove)
                {
                    _lastMessage = "resolve captured armies movement before ending attack phase";
                    return;
                }

                _phase = MatchPhase.Fortify;
                _pendingSourceTerritory = string.Empty;
                _lastMessage = "phase -> Fortify (optional one move; press N again to skip/end)";
                return;
            }

            AdvanceToNextPlayerTurn();
        }

        private GameplayCommandEnvelope BuildCommand(string type, string commandJson)
        {
            return new GameplayCommandEnvelope
            {
                MatchId = _matchId,
                CommandId = $"cmd-{_commandLog.Count + 1:D8}",
                PlayerId = CurrentPlayer.PlayerId,
                ExpectedSequence = _sequence,
                CommandType = type,
                CommandJson = commandJson
            };
        }

        private bool TryApplyCommand(GameplayCommandEnvelope command, string eventType)
        {
            if (command.ExpectedSequence != _sequence)
            {
                _lastMessage = $"sequence mismatch: expected {_sequence}, got {command.ExpectedSequence}";
                return false;
            }

            if (command.PlayerId != CurrentPlayer.PlayerId)
            {
                _lastMessage = $"player mismatch: active {CurrentPlayer.PlayerId}, command {command.PlayerId}";
                return false;
            }

            _commandLog.Add(command);
            var ev = ApplyAuthoritativeCommand(command, eventType);
            _eventLog.Add(ev);
            return true;
        }

        private AuthoritativeEventEnvelope ApplyAuthoritativeCommand(GameplayCommandEnvelope command, string eventType)
        {
            _sequence++;
            _rngCounter++;
            _lastChecksum = ComputeChecksum($"{_matchId}|{_sequence}|{command.CommandId}|{command.CommandJson}");

            return new AuthoritativeEventEnvelope
            {
                MatchId = _matchId,
                Sequence = _sequence,
                CommandId = command.CommandId,
                EventType = eventType,
                EventJson = command.CommandJson,
                StateChecksum = _lastChecksum,
                RngCounter = _rngCounter
            };
        }

        private void CaptureSnapshot()
        {
            _lastSnapshot = new StateSnapshotEnvelope
            {
                MatchId = _matchId,
                Sequence = _sequence,
                Seed = 17823741,
                ActivePlayerId = CurrentPlayer.PlayerId,
                Phase = _phase.ToString(),
                SnapshotJson = $"{{\"territories\":{_territories.Count},\"active\":\"{CurrentPlayer.PlayerId}\"}}",
                StateChecksum = _lastChecksum
            };

            _lastMessage = $"snapshot captured at seq {_sequence}";
        }

        private void SimulateReconnect()
        {
            if (_lastSnapshot == null)
            {
                CaptureSnapshot();
            }

            var missedEventsJson = _eventLog.Count == 0
                ? "[]"
                : $"[{string.Join(",", _eventLog.ConvertAll(e => $"{{\"sequence\":{e.Sequence},\"eventType\":\"{e.EventType}\"}}"))}]";

            _lastReconnect = new ReconnectResponse
            {
                MatchId = _matchId,
                SnapshotJson = _lastSnapshot.SnapshotJson,
                MissedEventsJson = missedEventsJson
            };

            _lastMessage = "reconnect simulated";
        }

        private bool IsOwnedByCurrentPlayer(string territoryId)
        {
            return _territories.TryGetValue(territoryId, out var t) && t.OwnerIndex == CurrentPlayer.Index;
        }

        private bool IsLocalPlayersTurn()
        {
            if (_players == null || _players.Count == 0)
            {
                return true;
            }

            return _localPlayerIndex == _activePlayerIndex;
        }

        private void SyncActiveTurnIndexFromLobby()
        {
            if (_players == null || _players.Count == 0 || _lobby == null || !_lobby.IsInLobby || _lobby.IsLocalHost)
            {
                return;
            }

            if (!_lobby.TryGetActiveTurnIndex(out var syncedIndex, out _) || syncedIndex < 0 || syncedIndex >= _players.Count)
            {
                return;
            }

            if (syncedIndex == _activePlayerIndex)
            {
                return;
            }

            Trace("TurnSync", $"lobby active changed {_activePlayerIndex} -> {syncedIndex}");
            _activePlayerIndex = syncedIndex;
            _pendingSourceTerritory = string.Empty;
            _pendingAttackDice = 1;
            _pendingFortifyArmies = 1;
            ClearPendingCaptureMove();
        }

        private void ApplyTerritory(string territoryId)
        {
            var t = _territories[territoryId];
            var owner = _players[t.OwnerIndex];
            _board.TrySetTerritoryState(territoryId, owner.Index, owner.Color, t.Armies);
        }

        private void StartTurnForCurrentPlayer()
        {
            _phase = MatchPhase.Reinforce;
            _pendingSourceTerritory = string.Empty;
            ClearPendingCaptureMove();
            _capturedTerritoryThisTurn = false;
            _fortifyUsedThisTurn = false;
            _turnSetupPlacements = 0;
            _turnReinforcementsPlaced = 0;
            _turnAttacksResolved = 0;
            _turnTerritoriesCaptured = 0;
            _turnFortifyArmiesMoved = 0;
            _turnCardsDrawn = 0;
            CurrentPlayer.ReinforcementPool = ComputeReinforcementFor(CurrentPlayer.Index);
            var mandatoryTrades = 0;
            while (ActivePlayerHandCount >= ForcedTradeThreshold && TryTradeCardsForPlayer(CurrentPlayer.Index, false))
            {
                mandatoryTrades++;
            }

            _lastMessage = mandatoryTrades > 0
                ? $"turn -> {CurrentPlayer.PlayerId}, reinforce={CurrentPlayer.ReinforcementPool}, mandatory trade-in x{mandatoryTrades}"
                : $"turn -> {CurrentPlayer.PlayerId}, reinforce={CurrentPlayer.ReinforcementPool}";
            Trace("Turn", $"start player={CurrentPlayer.PlayerId} idx={_activePlayerIndex} reinforce={CurrentPlayer.ReinforcementPool} phase={_phase}");
            SyncActiveTurnIndexToLobby();
        }

        private int ComputeReinforcementFor(int playerIndex)
        {
            var ownedTerritories = 0;
            foreach (var t in _territories.Values)
            {
                if (t.OwnerIndex == playerIndex)
                {
                    ownedTerritories++;
                }
            }

            var baseReinforcement = Mathf.Max(3, ownedTerritories / 3);
            var continentBonus = 0;
            if (_map?.continents != null)
            {
                foreach (var continent in _map.continents)
                {
                    if (continent == null || continent.territories == null || continent.territories.Length == 0)
                    {
                        continue;
                    }

                    var controlsAll = true;
                    foreach (var territoryId in continent.territories)
                    {
                        if (!_territories.TryGetValue(territoryId, out var state) || state.OwnerIndex != playerIndex)
                        {
                            controlsAll = false;
                            break;
                        }
                    }

                    if (controlsAll)
                    {
                        continentBonus += Mathf.Max(0, continent.bonus);
                    }
                }
            }

            return baseReinforcement + continentBonus;
        }

        private void AdvanceToNextPlayerTurn()
        {
            if (IsGameEnded)
            {
                return;
            }

            var endingPlayer = CurrentPlayer;
            PushActionFeedEntry(
                endingPlayer.PlayerId,
                _phase,
                BuildTurnSummaryForPlayer(endingPlayer));

            var previousIndex = _activePlayerIndex;
            _activePlayerIndex = FindNextActivePlayerIndex(_activePlayerIndex);
            _turnIndex++;
            if (_activePlayerIndex <= previousIndex)
            {
                _roundIndex++;
            }

            Trace("Turn", $"advance previous={previousIndex} next={_activePlayerIndex} round={_roundIndex} turn={_turnIndex}");
            StartTurnForCurrentPlayer();
        }

        private string BuildTurnSummaryForPlayer(PlayerState player)
        {
            var parts = new List<string>(6);
            if (_turnSetupPlacements > 0)
            {
                parts.Add($"setup +{_turnSetupPlacements}");
            }

            if (_turnReinforcementsPlaced > 0)
            {
                parts.Add($"reinforce +{_turnReinforcementsPlaced}");
            }

            if (_turnAttacksResolved > 0)
            {
                parts.Add($"attacks {_turnAttacksResolved}");
            }

            if (_turnTerritoriesCaptured > 0)
            {
                parts.Add($"captures {_turnTerritoriesCaptured}");
            }

            if (_turnFortifyArmiesMoved > 0)
            {
                parts.Add($"fortify moved {_turnFortifyArmiesMoved}");
            }

            if (_turnCardsDrawn > 0)
            {
                parts.Add($"cards +{_turnCardsDrawn}");
            }

            if (parts.Count == 0)
            {
                return $"{player.PlayerId} ended turn with no major actions";
            }

            return $"turn summary -> {string.Join(", ", parts)}";
        }

        private int FindNextActivePlayerIndex(int fromIndex)
        {
            if (_players.Count == 0)
            {
                return 0;
            }

            for (var i = 1; i <= _players.Count; i++)
            {
                var idx = (fromIndex + i) % _players.Count;
                if (!_eliminatedPlayerIndices.Contains(idx))
                {
                    return idx;
                }
            }

            return fromIndex;
        }

        private void StartSetupPhase()
        {
            var initialArmies = GetInitialArmiesPerPlayer(_players.Count);
            foreach (var player in _players)
            {
                player.SetupArmiesRemaining = initialArmies;
            }

            DealObjectiveCards();
            DealTerritoryCardsAndPlaceInitialArmies();
            BuildTerritoryDrawDeck();

            _setupFirstPlayerIndex = ResolveHostPlayerIndex();
            _setupConfirmedPlayers.Clear();
            _phase = MatchPhase.SetupDeploy;
            _activePlayerIndex = _setupFirstPlayerIndex;
            _setupPlacementsThisTurn = 0;
            _lastMessage = $"setup dealt: objectives + territories ({initialArmies} armies/player), setup deploy started";
            LogAuthoritativeState("setup-started");
            SyncActiveTurnIndexToLobby();
        }

        private int ResolveHostPlayerIndex()
        {
            if (_players.Count == 0)
            {
                return 0;
            }

            if (_lobby != null && _lobby.IsInLobby && _lobby.TryGetHostSteamId(out var hostSteamId, out _))
            {
                for (var i = 0; i < _players.Count; i++)
                {
                    if (_players[i].SteamId == hostSteamId)
                    {
                        return i;
                    }
                }
            }

            return 0;
        }

        private void SyncActiveTurnIndexToLobby()
        {
            if (_lobby == null || !_lobby.IsInLobby || !_lobby.IsLocalHost)
            {
                return;
            }

            _lobby.TrySetActiveTurnIndex(_activePlayerIndex, out _);
            Trace("TurnSync", $"host pushed active={_activePlayerIndex}");
        }

        private static int GetInitialArmiesPerPlayer(int playerCount)
        {
            return playerCount switch
            {
                2 => 40,
                3 => 35,
                4 => 30,
                5 => 25,
                6 => 20,
                _ => 20
            };
        }

        private bool AllTerritoriesClaimed()
        {
            foreach (var territory in _territories.Values)
            {
                if (territory.OwnerIndex < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private bool AllSetupArmiesPlaced()
        {
            foreach (var player in _players)
            {
                if (player.SetupArmiesRemaining > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void AdvanceToNextPlayerTurnOrder()
        {
            _activePlayerIndex = (_activePlayerIndex + 1) % _players.Count;
            _setupPlacementsThisTurn = 0;
            Trace("Setup", $"turn-order advance active={_activePlayerIndex}");
            SyncActiveTurnIndexToLobby();
        }

        private void AdvanceToNextPlayerWithSetupArmies()
        {
            for (var i = 0; i < _players.Count; i++)
            {
                _activePlayerIndex = (_activePlayerIndex + 1) % _players.Count;
                if (CurrentPlayer.SetupArmiesRemaining > 0)
                {
                    _setupPlacementsThisTurn = 0;
                    Trace("Setup", $"advance-to-player active={_activePlayerIndex} remaining={CurrentPlayer.SetupArmiesRemaining}");
                    SyncActiveTurnIndexToLobby();
                    return;
                }
            }

            _setupPlacementsThisTurn = 0;
            Trace("Setup", "advance-to-player no eligible player found (all zero)");
            SyncActiveTurnIndexToLobby();
        }

        private void Trace(string category, string message)
        {
            if (!_verboseLogs)
            {
                return;
            }

            if (!TraceCategories.Contains(category))
            {
                return;
            }

            Debug.Log($"[Risiko3D][Trace][{category}] {message}");
        }

        private void DealTerritoryCardsAndPlaceInitialArmies()
        {
            var territoryDeck = BuildSetupTerritoryDeck();
            Shuffle(territoryDeck, "setup-territory-deal");
            var territoryCountByPlayer = new int[_players.Count];

            var receiver = _setupFirstPlayerIndex;
            foreach (var territoryId in territoryDeck)
            {
                var owner = receiver;
                var state = _territories[territoryId];
                state.OwnerIndex = owner;
                state.Armies = 1;
                _players[owner].SetupArmiesRemaining = Mathf.Max(0, _players[owner].SetupArmiesRemaining - 1);
                _playerAssignedTerritoryCards[owner].Add(territoryId);
                territoryCountByPlayer[owner] += 1;
                receiver = (receiver + 1) % _players.Count;
            }

            ApplyAllTerritoriesToBoard();
            ValidateSetupTerritoryDistribution(territoryCountByPlayer);
        }

        private void ValidateSetupTerritoryDistribution(int[] territoryCountByPlayer)
        {
            if (territoryCountByPlayer == null || territoryCountByPlayer.Length == 0)
            {
                return;
            }

            var min = int.MaxValue;
            var max = int.MinValue;
            for (var i = 0; i < territoryCountByPlayer.Length; i++)
            {
                var value = territoryCountByPlayer[i];
                if (value < min)
                {
                    min = value;
                }

                if (value > max)
                {
                    max = value;
                }
            }

            if (max - min > 1)
            {
                Debug.LogError($"[Risiko3D][MatchLoop] Setup territory deal is unbalanced. min={min}, max={max}");
                return;
            }

            Debug.Log($"[Risiko3D][MatchLoop] Setup territory deal balanced. min={min}, max={max}");
            LogSetupOwnershipByPlayer();
        }

        private void DealObjectiveCards()
        {
            var objectiveDeck = BuildObjectiveDeck();
            Shuffle(objectiveDeck, "setup-objective-deal");
            for (var i = 0; i < _players.Count; i++)
            {
                var dealt = objectiveDeck[i];
                var ownerIndex = _players[i].Index;
                if (TryMapEliminationObjective(dealt, ownerIndex, out var mappedObjective))
                {
                    _playerObjectiveCards[ownerIndex] = mappedObjective;
                }
                else
                {
                    _playerObjectiveCards[ownerIndex] = dealt;
                }
            }
        }

        private void Shuffle<T>(IList<T> values, string context)
        {
            if (values == null || values.Count <= 1)
            {
                return;
            }

            var seed = HashCode.Combine(_rngSeed, _shuffleNonce++, context ?? string.Empty, values.Count);
            var rng = new System.Random(seed);
            for (var i = values.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        private void LoadMapData()
        {
            _map = null;
            if (_config == null || string.IsNullOrWhiteSpace(_config.MapJsonPath))
            {
                return;
            }

            var json = ReadProjectFile(_config.MapJsonPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                _map = JsonUtility.FromJson<MapData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Risiko3D][MatchLoop] Failed reading map data for bonuses: {ex.Message}");
            }
        }

        private static string ReadProjectFile(string relativePath)
        {
            var root = Directory.GetParent(Application.dataPath);
            if (root == null)
            {
                return string.Empty;
            }

            var fullPath = Path.Combine(root.FullName, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            return File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
        }

        private static string ComputeChecksum(string source)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(source);
            var hash = sha.ComputeHash(bytes);
            var hex = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            return $"sha256:{hex[..16]}";
        }

        private void PublishAuthoritativeSnapshotIfHost()
        {
            if (!_matchInitialized || _players == null || _players.Count == 0 || _lobby == null || !_lobby.IsInLobby || !_lobby.IsLocalHost)
            {
                return;
            }

            var snapshotJson = BuildAuthoritativeSnapshotJson();
            if (string.IsNullOrWhiteSpace(snapshotJson) || string.Equals(snapshotJson, _lastPublishedAuthoritativeSnapshot, StringComparison.Ordinal))
            {
                return;
            }

            if (_lobby.TrySetAuthoritativeSnapshot(snapshotJson, out _))
            {
                _lastPublishedAuthoritativeSnapshot = snapshotJson;
            }
        }

        private void TryApplyAuthoritativeSnapshotFromLobby()
        {
            if (_lobby == null || !_lobby.IsInLobby || _lobby.IsLocalHost)
            {
                return;
            }

            if (!_lobby.TryGetAuthoritativeSnapshot(out var snapshotJson, out _) || string.IsNullOrWhiteSpace(snapshotJson))
            {
                return;
            }

            if (string.Equals(snapshotJson, _lastAppliedAuthoritativeSnapshot, StringComparison.Ordinal))
            {
                return;
            }

            if (!TryApplyAuthoritativeSnapshot(snapshotJson))
            {
                return;
            }

            _lastAppliedAuthoritativeSnapshot = snapshotJson;
        }

        private string BuildAuthoritativeSnapshotJson()
        {
            var snapshot = new AuthoritativeSnapshotData
            {
                activePlayerIndex = _activePlayerIndex,
                turnIndex = _turnIndex,
                roundIndex = _roundIndex,
                phase = _phase.ToString(),
                players = new SnapshotPlayerData[_players.Count],
                territories = new SnapshotTerritoryData[_territories.Count]
            };

            for (var i = 0; i < _players.Count; i++)
            {
                var p = _players[i];
                snapshot.players[i] = new SnapshotPlayerData
                {
                    index = p.Index,
                    setupArmiesRemaining = p.SetupArmiesRemaining,
                    reinforcementPool = p.ReinforcementPool
                };
            }

            var territoryIds = new List<string>(_territories.Keys);
            territoryIds.Sort(StringComparer.Ordinal);
            for (var i = 0; i < territoryIds.Count; i++)
            {
                var id = territoryIds[i];
                var t = _territories[id];
                snapshot.territories[i] = new SnapshotTerritoryData
                {
                    territoryId = id,
                    ownerIndex = t.OwnerIndex,
                    armies = t.Armies
                };
            }

            return JsonUtility.ToJson(snapshot);
        }

        private bool TryApplyAuthoritativeSnapshot(string snapshotJson)
        {
            AuthoritativeSnapshotData snapshot;
            try
            {
                snapshot = JsonUtility.FromJson<AuthoritativeSnapshotData>(snapshotJson);
            }
            catch
            {
                return false;
            }

            if (snapshot == null)
            {
                return false;
            }

            if (snapshot.players != null)
            {
                for (var i = 0; i < snapshot.players.Length; i++)
                {
                    var ps = snapshot.players[i];
                    if (ps == null || ps.index < 0 || ps.index >= _players.Count)
                    {
                        continue;
                    }

                    _players[ps.index].SetupArmiesRemaining = ps.setupArmiesRemaining;
                    _players[ps.index].ReinforcementPool = ps.reinforcementPool;
                }
            }

            if (snapshot.territories != null)
            {
                for (var i = 0; i < snapshot.territories.Length; i++)
                {
                    var ts = snapshot.territories[i];
                    if (ts == null || string.IsNullOrWhiteSpace(ts.territoryId) || !_territories.TryGetValue(ts.territoryId, out var t))
                    {
                        continue;
                    }

                    t.OwnerIndex = ts.ownerIndex;
                    t.Armies = ts.armies;
                }
            }

            _activePlayerIndex = Mathf.Clamp(snapshot.activePlayerIndex, 0, Mathf.Max(0, _players.Count - 1));
            _turnIndex = Mathf.Max(0, snapshot.turnIndex);
            _roundIndex = Mathf.Max(1, snapshot.roundIndex);
            if (!string.IsNullOrWhiteSpace(snapshot.phase) && Enum.TryParse(snapshot.phase, out MatchPhase parsed))
            {
                _phase = parsed;
            }

            _pendingSourceTerritory = string.Empty;
            _pendingAttackDice = 1;
            _pendingFortifyArmies = 1;
            ClearPendingCaptureMove();
            ApplyAllTerritoriesToBoard();
            return true;
        }

        private int[] RollDiceDescending(System.Random rng, int count)
        {
            var rolls = new int[count];
            for (var i = 0; i < count; i++)
            {
                rolls[i] = rng.Next(1, 7);
            }

            Array.Sort(rolls);
            Array.Reverse(rolls);
            _rngCounter += count;
            return rolls;
        }

        private System.Random BuildDeterministicRng(string commandId)
        {
            var seed = HashCode.Combine(_rngSeed, _turnIndex, _roundIndex, commandId);
            return new System.Random(seed);
        }

        private void ClearPendingCaptureMove()
        {
            _pendingCaptureFromTerritory = string.Empty;
            _pendingCaptureToTerritory = string.Empty;
            _pendingCaptureMinArmies = 0;
            _pendingCaptureMaxArmies = 0;
            _pendingCaptureArmiesToMove = 0;
        }

        private int FindEliminatedOwnerAfterCapture(int previousOwner, int newOwnerIndex)
        {
            if (previousOwner < 0 || previousOwner == newOwnerIndex)
            {
                return -1;
            }

            foreach (var territory in _territories.Values)
            {
                if (territory.OwnerIndex == previousOwner)
                {
                    return -1;
                }
            }

            return previousOwner;
        }

        private void TransferAllCards(int fromPlayerIndex, int toPlayerIndex)
        {
            if (!_playerTerritoryHands.TryGetValue(fromPlayerIndex, out var fromHand) ||
                !_playerTerritoryHands.TryGetValue(toPlayerIndex, out var toHand))
            {
                return;
            }

            if (fromHand.Count == 0)
            {
                return;
            }

            toHand.AddRange(fromHand);
            fromHand.Clear();
        }

        private bool TryCheckObjectiveCompletion(int playerIndex)
        {
            if (_winnerPlayerIndex >= 0)
            {
                return true;
            }

            if (!_playerObjectiveCards.TryGetValue(playerIndex, out var objectiveId) || string.IsNullOrWhiteSpace(objectiveId))
            {
                return false;
            }

            var objectiveBaseId = GetObjectiveBaseId(objectiveId);
            var ownsTerritories = new List<TerritoryState>(42);
            foreach (var territory in _territories.Values)
            {
                if (territory.OwnerIndex == playerIndex)
                {
                    ownsTerritories.Add(territory);
                }
            }

            var ownedCount = ownsTerritories.Count;
            var completed = objectiveBaseId switch
            {
                "obj-24" => ownedCount >= 24,
                "obj-fallback-24" => ownedCount >= 24,
                "obj-18-2" => ownsTerritories.Count(t => t.Armies >= 2) >= 18,
                "obj-na-af" => ControlsContinents(playerIndex, "north_america", "africa"),
                "obj-na-au" => ControlsContinents(playerIndex, "north_america", "australia"),
                "obj-as-sa" => ControlsContinents(playerIndex, "asia", "south_america"),
                "obj-as-af" => ControlsContinents(playerIndex, "asia", "africa"),
                "obj-eu-au-plus1" => ControlsContinents(playerIndex, "europe", "australia") && ControlledContinentCount(playerIndex, "europe", "australia") >= 3,
                "obj-eu-sa-plus1" => ControlsContinents(playerIndex, "europe", "south_america") && ControlledContinentCount(playerIndex, "europe", "south_america") >= 3,
                _ when objectiveBaseId.StartsWith("obj-elim-", StringComparison.Ordinal) => IsEliminationObjectiveCompleted(objectiveId, playerIndex),
                _ => false
            };

            if (!completed)
            {
                return false;
            }

            _winnerPlayerIndex = playerIndex;
            _lastMessage = $"objective completed -> winner {CurrentPlayer.PlayerId}";
            return true;
        }

        private bool IsEliminationObjectiveCompleted(string objectiveId, int ownerIndex)
        {
            var parts = objectiveId.Split(':');
            if (parts.Length < 2 || !int.TryParse(parts[1], out var targetIndex))
            {
                return CountOwnedTerritories(ownerIndex) >= 24;
            }

            foreach (var territory in _territories.Values)
            {
                if (territory.OwnerIndex == targetIndex)
                {
                    return false;
                }
            }

            return true;
        }

        private int CountOwnedTerritories(int playerIndex)
        {
            var count = 0;
            foreach (var territory in _territories.Values)
            {
                if (territory.OwnerIndex == playerIndex)
                {
                    count++;
                }
            }

            return count;
        }

        private bool ControlsContinents(int playerIndex, params string[] requiredContinentIds)
        {
            if (_map?.continents == null)
            {
                return false;
            }

            foreach (var id in requiredContinentIds)
            {
                var found = false;
                foreach (var continent in _map.continents)
                {
                    if (continent == null || !string.Equals(continent.id, id, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    found = true;
                    if (!ControlsContinent(playerIndex, continent))
                    {
                        return false;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        private int ControlledContinentCount(int playerIndex, params string[] requiredContinentIds)
        {
            if (_map?.continents == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var continent in _map.continents)
            {
                if (continent == null)
                {
                    continue;
                }

                if (ControlsContinent(playerIndex, continent))
                {
                    count++;
                }
            }

            return count;
        }

        private bool ControlsContinent(int playerIndex, ContinentData continent)
        {
            if (continent?.territories == null || continent.territories.Length == 0)
            {
                return false;
            }

            foreach (var territoryId in continent.territories)
            {
                if (!_territories.TryGetValue(territoryId, out var territory) || territory.OwnerIndex != playerIndex)
                {
                    return false;
                }
            }

            return true;
        }

        private static string GetObjectiveBaseId(string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(objectiveId))
            {
                return string.Empty;
            }

            var idx = objectiveId.IndexOf(':');
            return idx > 0 ? objectiveId[..idx] : objectiveId;
        }

        private bool AreConnectedByOwnedPath(string source, string target, int ownerIndex)
        {
            if (source == target)
            {
                return true;
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(source);
            visited.Add(source);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbor in GetNeighbors(current))
                {
                    if (visited.Contains(neighbor))
                    {
                        continue;
                    }

                    if (!_territories.TryGetValue(neighbor, out var state) || state.OwnerIndex != ownerIndex)
                    {
                        continue;
                    }

                    if (neighbor == target)
                    {
                        return true;
                    }

                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            return false;
        }

        private IEnumerable<string> GetNeighbors(string territoryId)
        {
            if (_map?.territories == null || string.IsNullOrWhiteSpace(territoryId))
            {
                yield break;
            }

            TerritoryData territory = null;
            foreach (var t in _map.territories)
            {
                if (t != null && t.id == territoryId)
                {
                    territory = t;
                    break;
                }
            }

            if (territory?.neighbors == null)
            {
                yield break;
            }

            foreach (var n in territory.neighbors)
            {
                if (!string.IsNullOrWhiteSpace(n))
                {
                    yield return n;
                }
            }
        }

        private void LoadTerritorySymbolManifest()
        {
            _territorySymbolById.Clear();
            if (_config == null || string.IsNullOrWhiteSpace(_config.TerritorySymbolManifestPath))
            {
                return;
            }

            var json = ReadProjectFile(_config.TerritorySymbolManifestPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                var manifest = JsonUtility.FromJson<TerritorySymbolManifestData>(json);
                if (manifest?.assignments == null)
                {
                    return;
                }

                foreach (var assignment in manifest.assignments)
                {
                    if (assignment == null || string.IsNullOrWhiteSpace(assignment.territoryId) || string.IsNullOrWhiteSpace(assignment.symbol))
                    {
                        continue;
                    }

                    _territorySymbolById[NormalizeId(assignment.territoryId)] = ParseSymbol(assignment.symbol);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Risiko3D][MatchLoop] Failed parsing territory symbol manifest: {ex.Message}");
            }
        }

        private void BuildTerritoryDrawDeck()
        {
            _territoryDeck.Clear();
            _territoryDiscard.Clear();
            _territoryCardsById.Clear();
            foreach (var player in _players)
            {
                _playerTerritoryHands[player.Index] = new List<TerritoryCard>(8);
            }

            var territoryIds = BuildSetupTerritoryDeck();
            for (var i = 0; i < territoryIds.Count; i++)
            {
                var territoryId = territoryIds[i];
                var card = new TerritoryCard
                {
                    TerritoryId = territoryId,
                    DisplayName = ResolveTerritoryDisplayName(territoryId),
                    Symbol = _territorySymbolById.TryGetValue(territoryId, out var symbol) ? symbol : GetFallbackSymbol(i),
                    IsJoker = false,
                    CardId = territoryId
                };

                _territoryCardsById[territoryId] = card;
                _territoryDeck.Add(card);
            }

            _territoryDeck.Add(new TerritoryCard { TerritoryId = "joker:1", DisplayName = "Jolly", Symbol = CardSymbol.Joker, IsJoker = true, CardId = "joker:1" });
            _territoryDeck.Add(new TerritoryCard { TerritoryId = "joker:2", DisplayName = "Jolly", Symbol = CardSymbol.Joker, IsJoker = true, CardId = "joker:2" });
            Shuffle(_territoryDeck, "territory-draw-deck");
        }

        private List<string> BuildSetupTerritoryDeck()
        {
            var fromCards = new List<string>();
            if (_config != null && !string.IsNullOrWhiteSpace(_config.TerritoryCardsPath))
            {
                var fullPath = ResolveProjectPath(_config.TerritoryCardsPath);
                if (!string.IsNullOrWhiteSpace(fullPath) && Directory.Exists(fullPath))
                {
                    var files = Directory.GetFiles(fullPath, "*.svg", SearchOption.TopDirectoryOnly);
                    foreach (var file in files)
                    {
                        var id = NormalizeId(Path.GetFileNameWithoutExtension(file));
                        if (_territories.ContainsKey(id) && !fromCards.Contains(id))
                        {
                            fromCards.Add(id);
                        }
                    }
                }
            }

            if (fromCards.Count > 0)
            {
                fromCards.Sort(StringComparer.Ordinal);
                foreach (var id in _territories.Keys)
                {
                    if (!fromCards.Contains(id))
                    {
                        fromCards.Add(id);
                    }
                }

                return fromCards;
            }

            var fallback = new List<string>(_territories.Keys);
            fallback.Sort(StringComparer.Ordinal);
            return fallback;
        }

        private int ResolveDeterministicMatchSeed()
        {
            if (_lobby != null && _lobby.IsInLobby && _lobby.CurrentLobbyId != 0UL)
            {
                var lobbySeed = unchecked((int)(_lobby.CurrentLobbyId ^ (_lobby.CurrentLobbyId >> 32)));
                if (lobbySeed != 0)
                {
                    return Mathf.Abs(lobbySeed);
                }
            }

            return Mathf.Abs(Environment.TickCount);
        }

        private List<string> BuildObjectiveDeck()
        {
            var deck = new List<string>();
            if (_config != null && !string.IsNullOrWhiteSpace(_config.ObjectiveCardsPath))
            {
                var fullPath = ResolveProjectPath(_config.ObjectiveCardsPath);
                if (!string.IsNullOrWhiteSpace(fullPath) && Directory.Exists(fullPath))
                {
                    var files = Directory.GetFiles(fullPath, "*.svg", SearchOption.TopDirectoryOnly);
                    foreach (var file in files)
                    {
                        var id = NormalizeId(Path.GetFileNameWithoutExtension(file));
                        if (id == "obj_fallback_24")
                        {
                            continue;
                        }

                        id = id.Replace('_', '-');
                        if (ObjectiveTextById.ContainsKey(id) && !deck.Contains(id))
                        {
                            deck.Add(id);
                        }
                    }
                }
            }

            if (deck.Count >= _players.Count)
            {
                return deck;
            }

            return new List<string>
            {
                "obj-24",
                "obj-18-2",
                "obj-eu-au-plus1",
                "obj-eu-sa-plus1",
                "obj-na-af",
                "obj-na-au",
                "obj-as-sa",
                "obj-as-af",
                "obj-elim-red",
                "obj-elim-blue",
                "obj-elim-green",
                "obj-elim-yellow",
                "obj-elim-purple",
                "obj-elim-black"
            };
        }

        private bool TryMapEliminationObjective(string objectiveId, int ownerIndex, out string mappedObjectiveId)
        {
            mappedObjectiveId = objectiveId;
            if (string.IsNullOrWhiteSpace(objectiveId) || !objectiveId.StartsWith("obj-elim-", StringComparison.Ordinal))
            {
                return false;
            }

            var colorId = objectiveId["obj-elim-".Length..];
            if (!_playerColorIdByIndex.TryGetValue(ownerIndex, out var ownerColor))
            {
                mappedObjectiveId = "obj-fallback-24";
                return true;
            }

            if (string.Equals(ownerColor, colorId, StringComparison.Ordinal))
            {
                mappedObjectiveId = "obj-fallback-24";
                return true;
            }

            foreach (var player in _players)
            {
                if (player.Index == ownerIndex)
                {
                    continue;
                }

                if (_playerColorIdByIndex.TryGetValue(player.Index, out var playerColor) &&
                    string.Equals(playerColor, colorId, StringComparison.Ordinal))
                {
                    mappedObjectiveId = $"{objectiveId}:{player.Index}";
                    return true;
                }
            }

            mappedObjectiveId = "obj-fallback-24";
            return true;
        }

        private void LogAuthoritativeState(string reason)
        {
            if (!_verboseLogs || _players == null || _players.Count == 0)
            {
                return;
            }

            var players = new StringBuilder(160);
            for (var i = 0; i < _players.Count; i++)
            {
                var p = _players[i];
                if (i > 0)
                {
                    players.Append(" | ");
                }

                players.Append($"idx={p.Index} id={p.PlayerId} steam={p.SteamId}");
                if (_playerColorIdByIndex.TryGetValue(p.Index, out var colorId))
                {
                    players.Append($" color={colorId}");
                }

                if (p.Index == _localPlayerIndex)
                {
                    players.Append(" local=1");
                }
            }

            Trace("Authority", $"{reason} localIdx={_localPlayerIndex} activeIdx={_activePlayerIndex} phase={_phase} seed={_rngSeed} players=[{players}]");
        }

        private void LogSetupOwnershipByPlayer()
        {
            if (!_verboseLogs || _players == null || _players.Count == 0)
            {
                return;
            }

            var byPlayer = new int[_players.Count];
            foreach (var t in _territories.Values)
            {
                if (t.OwnerIndex >= 0 && t.OwnerIndex < byPlayer.Length)
                {
                    byPlayer[t.OwnerIndex]++;
                }
            }

            var parts = new StringBuilder(128);
            for (var i = 0; i < _players.Count; i++)
            {
                if (i > 0)
                {
                    parts.Append(" | ");
                }

                parts.Append($"{_players[i].PlayerId}:territories={byPlayer[i]} setupRemaining={_players[i].SetupArmiesRemaining}");
            }

            Trace("Authority", $"setup-ownership {parts}");
        }

        private void DrawTerritoryCardForPlayer(int playerIndex)
        {
            if (!_playerTerritoryHands.TryGetValue(playerIndex, out var hand))
            {
                return;
            }

            if (_territoryDeck.Count == 0)
            {
                return;
            }

            var card = _territoryDeck[0];
            _territoryDeck.RemoveAt(0);
            hand.Add(card);
        }

        private bool TryTradeCardsForPlayer(int playerIndex, bool userInitiated)
        {
            if (!TryFindBestTradeSet(playerIndex, out var best))
            {
                return false;
            }

            if (!_playerTerritoryHands.TryGetValue(playerIndex, out var hand))
            {
                return false;
            }

            var indices = new[] { best.FirstIndex, best.SecondIndex, best.ThirdIndex };
            Array.Sort(indices);
            Array.Reverse(indices);
            foreach (var index in indices)
            {
                if (index < 0 || index >= hand.Count)
                {
                    continue;
                }

                var removed = hand[index];
                _territoryDeck.Add(removed);
                hand.RemoveAt(index);
            }

            _players[playerIndex].ReinforcementPool += best.TotalReinforcement;
            var trigger = userInitiated ? "manual trade-in" : "mandatory trade-in";
            _lastMessage = $"{trigger}: +{best.BaseReinforcement} (+{best.OwnedTerritoryBonus} owned territory bonus)";
            return true;
        }

        private bool TryFindBestTradeSet(int playerIndex, out TradeSetCandidate best)
        {
            best = null;
            if (!_playerTerritoryHands.TryGetValue(playerIndex, out var hand) || hand.Count < 3)
            {
                return false;
            }

            for (var i = 0; i < hand.Count - 2; i++)
            {
                for (var j = i + 1; j < hand.Count - 1; j++)
                {
                    for (var k = j + 1; k < hand.Count; k++)
                    {
                        if (!TryEvaluateTradeSet(playerIndex, hand[i], hand[j], hand[k], out var baseReinforcement, out var ownedBonus))
                        {
                            continue;
                        }

                        var candidate = new TradeSetCandidate
                        {
                            FirstIndex = i,
                            SecondIndex = j,
                            ThirdIndex = k,
                            BaseReinforcement = baseReinforcement,
                            OwnedTerritoryBonus = ownedBonus
                        };

                        if (best == null || candidate.TotalReinforcement > best.TotalReinforcement)
                        {
                            best = candidate;
                        }
                    }
                }
            }

            return best != null;
        }

        private bool TryEvaluateTradeSet(int playerIndex, TerritoryCard a, TerritoryCard b, TerritoryCard c, out int baseReinforcement, out int ownedBonus)
        {
            baseReinforcement = 0;
            ownedBonus = 0;
            var cards = new[] { a, b, c };
            var jokerCount = cards.Count(card => card.Symbol == CardSymbol.Joker);
            var infantryCount = cards.Count(card => card.Symbol == CardSymbol.Infantry);
            var cavalryCount = cards.Count(card => card.Symbol == CardSymbol.Cavalry);
            var artilleryCount = cards.Count(card => card.Symbol == CardSymbol.Artillery);

            if (jokerCount == 0)
            {
                if (artilleryCount == 3)
                {
                    baseReinforcement = 4;
                }
                else if (infantryCount == 3)
                {
                    baseReinforcement = 6;
                }
                else if (cavalryCount == 3)
                {
                    baseReinforcement = 8;
                }
                else if (infantryCount == 1 && cavalryCount == 1 && artilleryCount == 1)
                {
                    baseReinforcement = 10;
                }
            }
            else if (jokerCount == 1 && (infantryCount == 2 || cavalryCount == 2 || artilleryCount == 2))
            {
                baseReinforcement = 12;
            }

            if (baseReinforcement <= 0)
            {
                return false;
            }

            foreach (var card in cards)
            {
                if (card.IsJoker)
                {
                    continue;
                }

                if (_territories.TryGetValue(card.TerritoryId, out var territory) && territory.OwnerIndex == playerIndex)
                {
                    ownedBonus += 2;
                }
            }

            return true;
        }

        private static string NormalizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim().Replace(" ", "_").Replace("-", "_").ToLowerInvariant();
            return normalized;
        }

        private static CardSymbol ParseSymbol(string symbol)
        {
            return NormalizeId(symbol) switch
            {
                "infantry" => CardSymbol.Infantry,
                "cavalry" => CardSymbol.Cavalry,
                "artillery" => CardSymbol.Artillery,
                "joker" => CardSymbol.Joker,
                _ => CardSymbol.Infantry
            };
        }

        private static CardSymbol GetFallbackSymbol(int index)
        {
            return (index % 3) switch
            {
                0 => CardSymbol.Infantry,
                1 => CardSymbol.Cavalry,
                _ => CardSymbol.Artillery
            };
        }

        private string ResolveTerritoryDisplayName(string territoryId)
        {
            if (_board != null && _board.Nodes != null && _board.Nodes.TryGetValue(territoryId, out var node) && node != null && !string.IsNullOrWhiteSpace(node.DisplayName))
            {
                return node.DisplayName;
            }

            if (string.IsNullOrWhiteSpace(territoryId))
            {
                return string.Empty;
            }

            var value = territoryId.Replace('_', ' ').Replace('-', ' ').Trim();
            var parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                parts[i] = p.Length > 1
                    ? char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant()
                    : p.ToUpperInvariant();
            }

            return string.Join(" ", parts);
        }

        private static string ResolveProjectPath(string relativePath)
        {
            var root = Directory.GetParent(Application.dataPath);
            if (root == null)
            {
                return string.Empty;
            }

            return Path.Combine(root.FullName, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        }
    }
}
