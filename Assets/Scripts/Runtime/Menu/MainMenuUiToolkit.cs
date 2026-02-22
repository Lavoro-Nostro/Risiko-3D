using System.Collections.Generic;
using Risiko3D.Runtime.Configuration;
using Risiko3D.Runtime.Steam;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Risiko3D.Runtime.Menu
{
    public sealed class MainMenuUiToolkit : MonoBehaviour
    {
        private const string PanelSettingsResourcePath = "UI/BoardLegend/BoardLegendPanelSettings";

        private GameRuntimeConfig _config;
        private LanDiscoveryService _lan;
        private SteamLobbyService _steamLobby;
        private UIDocument _document;

        private Label _statusLabel;
        private IntegerField _playersField;
        private TextField _roomCodeField;
        private ScrollView _friendList;
        private ScrollView _lanList;
        private ScrollView _assignmentList;
        private Button _readyButton;
        private Button _startMatchButton;
        private Button _leaveLobbyButton;
        private float _nextAssignmentRefreshTime;
        private float _nextFriendRefreshTime;
        private float _nextLanRefreshTime;
        private float _nextLobbyStatePollTime;
        private bool _isLoadingGameplayScene;

        private static readonly string[] PlayerColorCycle =
        {
            "red",
            "blue",
            "green",
            "yellow",
            "purple",
            "black"
        };

        public void Initialize(GameRuntimeConfig config, LanDiscoveryService lan)
        {
            _config = config;
            _lan = lan;
        }

        private void Start()
        {
            _steamLobby = FindFirstObjectByType<SteamLobbyService>();
            if (_steamLobby != null)
            {
                _steamLobby.LobbyCreated += OnLobbyCreated;
                _steamLobby.LobbyJoined += OnLobbyJoined;
                _steamLobby.MatchStarted += OnMatchStarted;
            }

            if (_lan != null)
            {
                _lan.StartDiscovery();
            }

            EnsureDocument();
            BuildUi();
            RefreshFriendLobbies();
            RefreshLanAnnouncements();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextAssignmentRefreshTime)
            {
                RefreshLobbyAssignments();
                _nextAssignmentRefreshTime = Time.unscaledTime + 0.50f;
            }

            if (Time.unscaledTime >= _nextFriendRefreshTime)
            {
                RefreshFriendLobbies();
                _nextFriendRefreshTime = Time.unscaledTime + 1.00f;
            }

            if (Time.unscaledTime >= _nextLanRefreshTime)
            {
                RefreshLanAnnouncements();
                _nextLanRefreshTime = Time.unscaledTime + 1.00f;
            }

            if (Time.unscaledTime >= _nextLobbyStatePollTime)
            {
                PollLobbyMatchState();
                _nextLobbyStatePollTime = Time.unscaledTime + 0.50f;
            }
        }

        private void OnDestroy()
        {
            if (_steamLobby != null)
            {
                _steamLobby.LobbyCreated -= OnLobbyCreated;
                _steamLobby.LobbyJoined -= OnLobbyJoined;
                _steamLobby.MatchStarted -= OnMatchStarted;
            }
        }

        private void EnsureDocument()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null)
            {
                _document = gameObject.AddComponent<UIDocument>();
            }

            if (_document.panelSettings == null)
            {
                var settings = Resources.Load<PanelSettings>(PanelSettingsResourcePath);
                if (settings != null)
                {
                    _document.panelSettings = settings;
                }
                else
                {
                    var runtimeSettings = ScriptableObject.CreateInstance<PanelSettings>();
                    runtimeSettings.clearColor = false;
                    runtimeSettings.sortingOrder = 700;
                    runtimeSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                    runtimeSettings.referenceResolution = new Vector2Int(1920, 1080);
                    _document.panelSettings = runtimeSettings;
                }
            }
        }

        private void BuildUi()
        {
            var root = _document.rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1f;
            root.style.backgroundColor = new Color(0.07f, 0.08f, 0.10f, 0.95f);

            var panel = new VisualElement();
            panel.style.width = 760f;
            panel.style.maxWidth = 760f;
            panel.style.alignSelf = Align.Center;
            panel.style.marginTop = 26f;
            panel.style.paddingLeft = 16f;
            panel.style.paddingRight = 16f;
            panel.style.paddingTop = 14f;
            panel.style.paddingBottom = 14f;
            panel.style.backgroundColor = new Color(0.04f, 0.06f, 0.09f, 0.94f);
            panel.style.borderTopLeftRadius = 12f;
            panel.style.borderTopRightRadius = 12f;
            panel.style.borderBottomLeftRadius = 12f;
            panel.style.borderBottomRightRadius = 12f;
            panel.style.borderTopWidth = 1f;
            panel.style.borderRightWidth = 1f;
            panel.style.borderBottomWidth = 1f;
            panel.style.borderLeftWidth = 1f;
            panel.style.borderTopColor = new Color(1f, 1f, 1f, 0.12f);
            panel.style.borderRightColor = new Color(1f, 1f, 1f, 0.12f);
            panel.style.borderBottomColor = new Color(1f, 1f, 1f, 0.12f);
            panel.style.borderLeftColor = new Color(1f, 1f, 1f, 0.12f);
            root.Add(panel);

            var title = new Label("Risiko 3D Multiplayer");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 24f;
            title.style.color = new Color(1f, 0.94f, 0.77f, 1f);
            title.style.marginBottom = 12f;
            panel.Add(title);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            panel.Add(row);

            var left = new VisualElement();
            left.style.flexGrow = 1f;
            left.style.marginRight = 10f;
            row.Add(left);

            var right = new VisualElement();
            right.style.flexGrow = 1f;
            row.Add(right);

            BuildHostSection(left);
            BuildRoomSection(left);
            BuildLobbyControlsSection(left);
            BuildAssignmentSection(left);
            BuildFriendSection(right);
            BuildLanSection(right);

            _statusLabel = new Label("Ready");
            _statusLabel.style.marginTop = 12f;
            _statusLabel.style.color = new Color(0.86f, 0.90f, 0.96f, 1f);
            _statusLabel.style.fontSize = 12f;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(_statusLabel);
        }

        private void BuildHostSection(VisualElement parent)
        {
            parent.Add(BuildSectionHeader("Host"));
            _playersField = new IntegerField("Players");
            _playersField.value = Mathf.Max(_config != null ? _config.MinPlayers : 3, 3);
            _playersField.style.marginBottom = 6f;
            parent.Add(_playersField);

            var hostButton = new Button(OnHostSteamClicked) { text = "Host Steam Lobby" };
            hostButton.style.height = 32f;
            hostButton.style.marginBottom = 12f;
            parent.Add(hostButton);
        }

        private void BuildRoomSection(VisualElement parent)
        {
            parent.Add(BuildSectionHeader("Room Code"));

            _roomCodeField = new TextField("Code");
            _roomCodeField.style.marginBottom = 6f;
            parent.Add(_roomCodeField);

            var joinButton = new Button(OnJoinRoomCodeClicked) { text = "Join by Room Code" };
            joinButton.style.height = 32f;
            joinButton.style.marginBottom = 12f;
            parent.Add(joinButton);
        }

        private void BuildAssignmentSection(VisualElement parent)
        {
            parent.Add(BuildSectionHeader("Lobby Color Assignments"));

            _assignmentList = new ScrollView(ScrollViewMode.Vertical);
            _assignmentList.style.height = 170f;
            _assignmentList.style.marginTop = 6f;
            _assignmentList.style.marginBottom = 12f;
            _assignmentList.style.backgroundColor = new Color(0.03f, 0.04f, 0.07f, 0.65f);
            parent.Add(_assignmentList);

            RefreshLobbyAssignments();
        }

        private void BuildLobbyControlsSection(VisualElement parent)
        {
            parent.Add(BuildSectionHeader("Lobby Room"));
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 10f;
            parent.Add(row);

            _readyButton = new Button(OnToggleReadyClicked) { text = "Set Ready" };
            _readyButton.style.height = 30f;
            _readyButton.style.width = 110f;
            _readyButton.style.marginRight = 6f;
            row.Add(_readyButton);

            _startMatchButton = new Button(OnStartMatchClicked) { text = "Start Match (Host)" };
            _startMatchButton.style.height = 30f;
            _startMatchButton.style.width = 140f;
            _startMatchButton.style.marginRight = 6f;
            row.Add(_startMatchButton);

            _leaveLobbyButton = new Button(OnLeaveLobbyClicked) { text = "Leave Lobby" };
            _leaveLobbyButton.style.height = 30f;
            _leaveLobbyButton.style.width = 110f;
            row.Add(_leaveLobbyButton);
        }

        private void BuildFriendSection(VisualElement parent)
        {
            parent.Add(BuildSectionHeader("Steam Friends"));

            var refresh = new Button(RefreshFriendLobbies) { text = "Refresh Friend Lobbies" };
            refresh.style.height = 28f;
            parent.Add(refresh);

            _friendList = new ScrollView(ScrollViewMode.Vertical);
            _friendList.style.height = 190f;
            _friendList.style.marginTop = 6f;
            _friendList.style.marginBottom = 12f;
            _friendList.style.backgroundColor = new Color(0.03f, 0.04f, 0.07f, 0.65f);
            parent.Add(_friendList);
        }

        private void BuildLanSection(VisualElement parent)
        {
            parent.Add(BuildSectionHeader("LAN Discovery"));

            var refresh = new Button(RefreshLanAnnouncements) { text = "Refresh LAN Hosts" };
            refresh.style.height = 28f;
            parent.Add(refresh);

            _lanList = new ScrollView(ScrollViewMode.Vertical);
            _lanList.style.height = 190f;
            _lanList.style.marginTop = 6f;
            _lanList.style.backgroundColor = new Color(0.03f, 0.04f, 0.07f, 0.65f);
            parent.Add(_lanList);
        }

        private static Label BuildSectionHeader(string text)
        {
            var header = new Label(text);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 14f;
            header.style.color = new Color(0.94f, 0.95f, 0.98f, 1f);
            header.style.marginBottom = 4f;
            return header;
        }

        private void OnHostSteamClicked()
        {
            if (_steamLobby == null)
            {
                SetStatus("Steam lobby service not available.");
                return;
            }

            var minPlayers = _config != null ? _config.MinPlayers : 3;
            var maxPlayers = _config != null ? _config.MaxPlayers : 6;
            var players = Mathf.Clamp(_playersField.value, minPlayers, maxPlayers);
            if (!_steamLobby.CreateLobby(players, out var error))
            {
                SetStatus($"Host failed: {error}");
                return;
            }

            SetStatus("Creating lobby...");
            RefreshFriendLobbies();
        }

        private void OnJoinRoomCodeClicked()
        {
            if (_steamLobby == null)
            {
                SetStatus("Steam lobby service not available.");
                return;
            }

            if (!_steamLobby.JoinByRoomCode(_roomCodeField.value, out var error))
            {
                SetStatus($"Join failed: {error}");
                return;
            }

            SetStatus("Joining lobby...");
            RefreshFriendLobbies();
        }

        private void OnToggleReadyClicked()
        {
            if (_steamLobby == null)
            {
                SetStatus("Steam lobby service not available.");
                return;
            }

            var next = !_steamLobby.IsLocalReady;
            if (!_steamLobby.SetLocalReady(next, out var error))
            {
                SetStatus($"Ready failed: {error}");
                return;
            }

            SetStatus(next ? "You are READY." : "You are NOT READY.");
            RefreshLobbyAssignments();
        }

        private void OnStartMatchClicked()
        {
            if (_steamLobby == null)
            {
                SetStatus("Steam lobby service not available.");
                return;
            }

            if (!_steamLobby.StartMatch(out var error))
            {
                SetStatus($"Start failed: {error}");
                return;
            }

            SetStatus("Starting match...");
        }

        private void OnLeaveLobbyClicked()
        {
            if (_steamLobby == null)
            {
                SetStatus("Steam lobby service not available.");
                return;
            }

            if (!_steamLobby.LeaveLobby(out var error))
            {
                SetStatus($"Leave failed: {error}");
                return;
            }

            SetStatus("Left lobby.");
            RefreshFriendLobbies();
            RefreshLobbyAssignments();
        }

        private void RefreshFriendLobbies()
        {
            if (_friendList == null)
            {
                return;
            }

            _friendList.Clear();
            if (_steamLobby == null)
            {
                AddSmallText(_friendList, "Steam lobby service unavailable.");
                return;
            }

            if (!_steamLobby.TryGetFriendJoinableLobbies(out var lobbies, out var error))
            {
                AddSmallText(_friendList, $"Friend discovery failed: {error}");
                return;
            }

            if (lobbies.Count == 0)
            {
                AddSmallText(_friendList, "No joinable friend lobbies.");
                return;
            }

            foreach (var lobby in lobbies)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 4f;
                _friendList.Add(row);

                var label = new Label($"{lobby.FriendName}  ({(string.IsNullOrWhiteSpace(lobby.RoomCode) ? "no-code" : lobby.RoomCode)})");
                label.style.color = new Color(0.90f, 0.93f, 0.98f, 1f);
                label.style.fontSize = 11f;
                row.Add(label);

                var join = new Button(() => JoinFriendLobby(lobby.LobbyId)) { text = "Join" };
                join.style.height = 22f;
                join.style.width = 56f;
                row.Add(join);
            }
        }

        private void RefreshLanAnnouncements()
        {
            if (_lanList == null)
            {
                return;
            }

            _lanList.Clear();
            if (_lan == null)
            {
                AddSmallText(_lanList, "LAN service unavailable.");
                return;
            }

            IReadOnlyList<LanLobbyAnnouncement> entries = _lan.GetAnnouncements();
            if (entries.Count == 0)
            {
                AddSmallText(_lanList, "No LAN hosts found.");
                return;
            }

            foreach (var entry in entries)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 4f;
                _lanList.Add(row);

                var label = new Label($"{entry.HostName} [{entry.Address}] ({entry.RoomCode})");
                label.style.color = new Color(0.86f, 0.92f, 1f, 1f);
                label.style.fontSize = 11f;
                row.Add(label);

                var join = new Button(() =>
                {
                    SetStatus($"Attempting LAN join: room={entry.RoomCode} lobby={entry.LobbyId}");
                    _roomCodeField.value = entry.RoomCode;
                    if (_steamLobby == null)
                    {
                        SetStatus("Steam lobby service not available.");
                        return;
                    }

                    if (entry.LobbyId != 0)
                    {
                        if (!_steamLobby.JoinLobby(entry.LobbyId, out var joinError))
                        {
                            SetStatus($"LAN join failed: {joinError}. Trying room code...");
                            OnJoinRoomCodeClicked();
                            return;
                        }

                        SetStatus("Joining LAN lobby...");
                        return;
                    }

                    OnJoinRoomCodeClicked();
                })
                { text = "Join" };
                join.style.height = 22f;
                join.style.width = 56f;
                row.Add(join);
            }
        }

        private void JoinFriendLobby(ulong lobbyId)
        {
            if (_steamLobby == null)
            {
                SetStatus("Steam lobby service not available.");
                return;
            }

            if (!_steamLobby.JoinLobby(lobbyId, out var error))
            {
                SetStatus($"Join failed: {error}");
                return;
            }

            SetStatus("Joining friend lobby...");
        }

        private void RefreshLobbyAssignments()
        {
            if (_assignmentList == null)
            {
                return;
            }

            _assignmentList.Clear();
            if (_steamLobby == null)
            {
                AddSmallText(_assignmentList, "Steam lobby service unavailable.");
                return;
            }

            if (!_steamLobby.IsInLobby)
            {
                AddSmallText(_assignmentList, "Join or host a lobby to preview assigned colors.");
                RefreshLobbyButtons();
                return;
            }

            if (!_steamLobby.TryGetCurrentLobbyMembers(out var members, out var error))
            {
                AddSmallText(_assignmentList, $"Assignment preview unavailable: {error}");
                RefreshLobbyButtons();
                return;
            }

            if (members == null || members.Count == 0)
            {
                AddSmallText(_assignmentList, "No lobby members found.");
                RefreshLobbyButtons();
                return;
            }

            var count = Mathf.Clamp(members.Count, 1, PlayerColorCycle.Length);
            var colors = BuildColorOrder(_steamLobby.CurrentLobbyId, count);
            _steamLobby.TryGetLobbyReadyStates(out var readyStates, out _);
            for (var i = 0; i < count; i++)
            {
                var member = members[i];
                var colorId = colors[i];
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 4f;
                _assignmentList.Add(row);

                var label = new Label($"{member.DisplayName} {(member.IsLocal ? "(You)" : string.Empty)}");
                label.style.color = new Color(0.90f, 0.93f, 0.98f, 1f);
                label.style.fontSize = 11f;
                row.Add(label);

                var ready = readyStates != null ? readyStates.Find(value => value.SteamId == member.SteamId) : default;
                var readySuffix = ready.SteamId != 0 ? (ready.IsReady ? " [Ready]" : " [Not Ready]") : string.Empty;
                var hostSuffix = ready.SteamId != 0 && ready.IsHost ? " [Host]" : string.Empty;
                var colorLabel = new Label($"{ToFriendlyColorName(colorId)}{hostSuffix}{readySuffix}");
                colorLabel.style.fontSize = 11f;
                colorLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                colorLabel.style.color = ToColor(colorId);
                row.Add(colorLabel);
            }

            RefreshLobbyButtons();
        }

        private static List<string> BuildColorOrder(ulong lobbyId, int playerCount)
        {
            var count = Mathf.Clamp(playerCount, 2, PlayerColorCycle.Length);
            var colors = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                colors.Add(PlayerColorCycle[i]);
            }

            var seed = lobbyId != 0
                ? unchecked((int)(lobbyId ^ (lobbyId >> 32)))
                : System.Environment.TickCount;
            var rng = new System.Random(seed);
            for (var i = colors.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (colors[i], colors[j]) = (colors[j], colors[i]);
            }

            return colors;
        }

        private static string ToFriendlyColorName(string colorId)
        {
            return colorId switch
            {
                "red" => "Red",
                "blue" => "Blue",
                "green" => "Green",
                "yellow" => "Yellow",
                "purple" => "Purple",
                "black" => "Black",
                _ => colorId
            };
        }

        private static Color ToColor(string colorId)
        {
            return colorId switch
            {
                "red" => new Color(0.92f, 0.26f, 0.27f, 1f),
                "blue" => new Color(0.20f, 0.50f, 0.96f, 1f),
                "green" => new Color(0.23f, 0.80f, 0.30f, 1f),
                "yellow" => new Color(0.90f, 0.82f, 0.23f, 1f),
                "purple" => new Color(0.60f, 0.36f, 0.86f, 1f),
                "black" => new Color(0.30f, 0.30f, 0.30f, 1f),
                _ => new Color(0.85f, 0.90f, 1f, 1f)
            };
        }

        private static void AddSmallText(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.style.fontSize = 11f;
            label.style.color = new Color(0.74f, 0.79f, 0.88f, 0.98f);
            label.style.marginBottom = 3f;
            parent.Add(label);
        }

        private void OnLobbyCreated(LobbyOperationResult result)
        {
            if (!result.Success)
            {
                SetStatus($"Create failed: {result.Message}");
                return;
            }

            if (_lan != null && _steamLobby != null)
            {
                _lan.StartHostBroadcast(_steamLobby.CurrentRoomCode, _steamLobby.CurrentLobbyId, System.Environment.UserName);
            }

            if (_roomCodeField != null)
            {
                _roomCodeField.value = _steamLobby?.CurrentRoomCode ?? string.Empty;
            }

            SetStatus($"Lobby created. room={_steamLobby?.CurrentRoomCode}");
            RefreshLobbyAssignments();
            RefreshFriendLobbies();
        }

        private void OnLobbyJoined(LobbyOperationResult result)
        {
            if (!result.Success)
            {
                SetStatus($"Join failed: {result.Message}");
                return;
            }

            SetStatus($"Lobby joined. room={_steamLobby?.CurrentRoomCode}");
            RefreshLobbyAssignments();
            RefreshFriendLobbies();
        }

        private void OnMatchStarted(LobbyOperationResult result)
        {
            if (!result.Success)
            {
                SetStatus($"Start failed: {result.Message}");
                return;
            }

            SetStatus("Match started.");
            LoadGameplayScene();
        }

        private void PollLobbyMatchState()
        {
            if (_isLoadingGameplayScene || _steamLobby == null || !_steamLobby.IsInLobby)
            {
                return;
            }

            if (!_steamLobby.TryGetCurrentLobbySnapshot(out var snapshot, out _))
            {
                return;
            }

            if (string.Equals(snapshot.MatchState, "in_match", System.StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("Match started by host. Loading...");
                LoadGameplayScene();
            }
        }

        private void RefreshLobbyButtons()
        {
            if (_readyButton == null || _startMatchButton == null || _leaveLobbyButton == null || _steamLobby == null)
            {
                return;
            }

            var inLobby = _steamLobby.IsInLobby;
            _readyButton.SetEnabled(inLobby);
            _leaveLobbyButton.SetEnabled(inLobby);
            _startMatchButton.SetEnabled(inLobby && _steamLobby.IsLocalHost);
            _readyButton.text = _steamLobby.IsLocalReady ? "Unready" : "Set Ready";
        }

        private void LoadGameplayScene()
        {
            if (_isLoadingGameplayScene)
            {
                return;
            }

            _isLoadingGameplayScene = true;
            var scene = _config != null && !string.IsNullOrWhiteSpace(_config.GameplaySceneName)
                ? _config.GameplaySceneName
                : "SampleScene";
            SceneManager.LoadScene(scene);
        }

        private void SetStatus(string text)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = text;
            }
        }
    }
}
