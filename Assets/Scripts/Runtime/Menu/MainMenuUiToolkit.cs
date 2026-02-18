using System;
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
        private const float LobbyRefreshIntervalSeconds = 0.5f;
        private const float FriendRefreshIntervalSeconds = 1.0f;

        private GameRuntimeConfig _config;
        private LanDiscoveryService _lan;
        private SteamLobbyService _steamLobby;
        private UIDocument _document;

        private Label _steamIdentityLabel;
        private Label _statusLabel;
        private Label _lobbyInfoLabel;
        private IntegerField _playersField;
        private TextField _roomCodeField;
        private ScrollView _friendList;
        private ScrollView _lanList;
        private ScrollView _lobbyMembersList;
        private Button _readyButton;
        private Button _startMatchButton;
        private Button _inviteButton;
        private Button _leaveLobbyButton;
        private Button _quickJoinButton;
        private bool _gameplayLoadingTriggered;
        private float _nextLobbyRefreshAt;
        private float _nextFriendRefreshAt;

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
                _steamLobby.LobbyStateUpdated += OnLobbyStateUpdated;
                _steamLobby.MatchStateChanged += OnMatchStateChanged;
            }

#if !DISABLESTEAMWORKS
            if (_lan != null)
            {
                _lan.StartDiscovery();
            }
#endif

            EnsureDocument();
            BuildUi();
            RefreshFriendLobbies();
            RefreshLanAnnouncements();
            RefreshLobbyStateUi();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextLobbyRefreshAt)
            {
                return;
            }

            _nextLobbyRefreshAt = Time.unscaledTime + LobbyRefreshIntervalSeconds;
            RefreshLanAnnouncements();
            RefreshLobbyStateUi();
            if (Time.unscaledTime >= _nextFriendRefreshAt)
            {
                _nextFriendRefreshAt = Time.unscaledTime + FriendRefreshIntervalSeconds;
                RefreshFriendLobbies();
            }
        }

        private void OnDestroy()
        {
            if (_steamLobby != null)
            {
                _steamLobby.LobbyCreated -= OnLobbyCreated;
                _steamLobby.LobbyJoined -= OnLobbyJoined;
                _steamLobby.LobbyStateUpdated -= OnLobbyStateUpdated;
                _steamLobby.MatchStateChanged -= OnMatchStateChanged;
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

            _steamIdentityLabel = new Label("Steam: checking...");
            _steamIdentityLabel.style.color = new Color(0.80f, 0.87f, 0.96f, 1f);
            _steamIdentityLabel.style.fontSize = 12f;
            _steamIdentityLabel.style.marginBottom = 10f;
            panel.Add(_steamIdentityLabel);

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
            BuildFriendSection(right);
            BuildLanSection(right);
            BuildLobbySection(panel);

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
            _playersField.value = 2;
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
            joinButton.style.marginBottom = 6f;
            parent.Add(joinButton);

            _quickJoinButton = new Button(OnQuickJoinClicked) { text = "Quick Join First Open Lobby" };
            _quickJoinButton.style.height = 28f;
            _quickJoinButton.style.marginBottom = 12f;
            parent.Add(_quickJoinButton);
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

        private void BuildLobbySection(VisualElement parent)
        {
            parent.Add(BuildSectionHeader("Lobby Room"));

            _lobbyInfoLabel = new Label("Not in lobby.");
            _lobbyInfoLabel.style.color = new Color(0.88f, 0.92f, 0.98f, 1f);
            _lobbyInfoLabel.style.fontSize = 12f;
            _lobbyInfoLabel.style.marginBottom = 6f;
            parent.Add(_lobbyInfoLabel);

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginBottom = 8f;
            parent.Add(actions);

            _readyButton = new Button(OnToggleReadyClicked) { text = "Ready" };
            _readyButton.style.height = 28f;
            _readyButton.style.marginRight = 6f;
            actions.Add(_readyButton);

            _startMatchButton = new Button(OnStartMatchClicked) { text = "Start Match (Host)" };
            _startMatchButton.style.height = 28f;
            _startMatchButton.style.marginRight = 6f;
            actions.Add(_startMatchButton);

            _inviteButton = new Button(OnInviteClicked) { text = "Invite Friends" };
            _inviteButton.style.height = 28f;
            _inviteButton.style.marginRight = 6f;
            actions.Add(_inviteButton);

            _leaveLobbyButton = new Button(OnLeaveLobbyClicked) { text = "Leave Lobby" };
            _leaveLobbyButton.style.height = 28f;
            actions.Add(_leaveLobbyButton);

            _lobbyMembersList = new ScrollView(ScrollViewMode.Vertical);
            _lobbyMembersList.style.height = 150f;
            _lobbyMembersList.style.marginBottom = 4f;
            _lobbyMembersList.style.backgroundColor = new Color(0.03f, 0.04f, 0.07f, 0.65f);
            parent.Add(_lobbyMembersList);
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

            const int minPlayers = 2;
            var maxPlayers = _config != null ? _config.MaxPlayers : 6;
            var players = Mathf.Clamp(_playersField.value, minPlayers, maxPlayers);
            if (!_steamLobby.CreateLobby(players, out var error))
            {
                SetStatus($"Host failed: {error}");
                return;
            }

            SetStatus("Creating lobby...");
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
                    _roomCodeField.value = entry.RoomCode;
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

        private void OnQuickJoinClicked()
        {
            if (_steamLobby == null)
            {
                SetStatus("Steam lobby service not available.");
                return;
            }

            if (!_steamLobby.TryGetFriendJoinableLobbies(out var lobbies, out var error))
            {
                SetStatus($"Quick join failed: {error}");
                return;
            }

            if (lobbies.Count == 0)
            {
                SetStatus("No open lobbies available to join.");
                return;
            }

            lobbies.Sort((a, b) => a.LobbyId.CompareTo(b.LobbyId));
            var chosen = lobbies[0];
            if (!_steamLobby.JoinLobby(chosen.LobbyId, out error))
            {
                SetStatus($"Quick join failed: {error}");
                return;
            }

            SetStatus($"Joining {chosen.RoomCode}...");
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
                var hostName = Environment.UserName;
                if (_steamLobby.TryGetLocalPlayerIdentity(out var steamName, out _, out _))
                {
                    hostName = steamName;
                }

                _lan.StartHostBroadcast(_steamLobby.CurrentRoomCode, _steamLobby.CurrentLobbyId, hostName);
            }

            SetStatus($"Lobby created. room={_steamLobby?.CurrentRoomCode}");
            RefreshLobbyStateUi();
        }

        private void OnLobbyJoined(LobbyOperationResult result)
        {
            if (!result.Success)
            {
                SetStatus($"Join failed: {result.Message}");
                return;
            }

            SetStatus($"Lobby joined. room={_steamLobby?.CurrentRoomCode}");
            RefreshLobbyStateUi();
        }

        private void LoadGameplayScene()
        {
            if (_gameplayLoadingTriggered)
            {
                return;
            }

            _gameplayLoadingTriggered = true;
            var scene = _config != null && !string.IsNullOrWhiteSpace(_config.GameplaySceneName)
                ? _config.GameplaySceneName
                : "SampleScene";
            SceneManager.LoadScene(scene);
        }

        private void OnLobbyStateUpdated()
        {
            RefreshLobbyStateUi();
        }

        private void OnMatchStateChanged(string matchState)
        {
            if (!string.Equals(matchState, "in_match", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SetStatus("Match started by host. Loading gameplay...");
            LoadGameplayScene();
        }

        private void OnToggleReadyClicked()
        {
            if (_steamLobby == null)
            {
                SetStatus("Steam lobby service not available.");
                return;
            }

            if (!_steamLobby.TryGetCurrentLobbySnapshot(out var snapshot, out var snapshotError))
            {
                SetStatus($"Lobby unavailable: {snapshotError}");
                return;
            }

            var nextReady = !snapshot.IsLocalPlayerReady;
            if (!_steamLobby.SetLocalReady(nextReady, out var error))
            {
                SetStatus($"Ready update failed: {error}");
                return;
            }

            SetStatus(nextReady ? "Ready set." : "Ready removed.");
            RefreshLobbyStateUi();
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

            SetStatus("Start requested. Waiting for lobby match state...");
        }

        private void OnInviteClicked()
        {
            if (_steamLobby == null)
            {
                SetStatus("Steam lobby service not available.");
                return;
            }

            if (!_steamLobby.OpenInviteOverlay(out var error))
            {
                SetStatus($"Invite failed: {error}");
                return;
            }

            SetStatus("Steam invite overlay opened.");
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

            if (_lan != null)
            {
                _lan.StopHostBroadcast();
            }

            SetStatus("Left lobby.");
            RefreshLobbyStateUi();
        }

        private void RefreshLobbyStateUi()
        {
            RefreshSteamIdentityLabel();
            if (_lobbyInfoLabel == null || _lobbyMembersList == null)
            {
                return;
            }

            _lobbyMembersList.Clear();
            if (_steamLobby == null)
            {
                _lobbyInfoLabel.text = "Steam lobby service unavailable.";
                SetLobbyButtonsEnabled(false, false, false, false);
                if (_quickJoinButton != null)
                {
                    _quickJoinButton.SetEnabled(false);
                }
                return;
            }

            if (!_steamLobby.IsInLobby)
            {
                _lobbyInfoLabel.text = "Not in lobby.";
                AddSmallText(_lobbyMembersList, "Host or join a Steam lobby.");
#if DISABLESTEAMWORKS
                AddSmallText(_lobbyMembersList, "Editor simulation works with Multiplayer Play Mode Virtual Players.");
#endif
                SetLobbyButtonsEnabled(false, false, false, false);
                if (_quickJoinButton != null)
                {
                    _quickJoinButton.SetEnabled(true);
                }
                return;
            }

            if (!_steamLobby.TryGetCurrentLobbySnapshot(out var snapshot, out var error))
            {
                _lobbyInfoLabel.text = $"Lobby error: {error}";
                SetLobbyButtonsEnabled(false, false, false, true);
                return;
            }

            _roomCodeField.value = snapshot.RoomCode;
            _lobbyInfoLabel.text =
                $"Room {snapshot.RoomCode} | {snapshot.CurrentPlayers}/{snapshot.MaxPlayers} | state={snapshot.MatchState}";
            _readyButton.text = snapshot.IsLocalPlayerReady ? "Set Not Ready" : "Set Ready";

            var allReady = snapshot.Members.Count > 0;
            for (var i = 0; i < snapshot.Members.Count; i++)
            {
                var member = snapshot.Members[i];
                if (!member.IsReady)
                {
                    allReady = false;
                }

                var readyText = member.IsReady ? "Ready" : "Not Ready";
                var hostTag = member.IsHost ? " [HOST]" : string.Empty;
                var meTag = member.IsLocalPlayer ? " (You)" : string.Empty;
                AddSmallText(_lobbyMembersList, $"{member.DisplayName}{meTag}{hostTag} - {readyText}");
            }

            const int minPlayers = 2;
            var canStart = snapshot.IsLocalPlayerHost &&
                           snapshot.CurrentPlayers >= minPlayers &&
                           allReady &&
                           !string.Equals(snapshot.MatchState, "in_match", StringComparison.OrdinalIgnoreCase);
            SetLobbyButtonsEnabled(true, canStart, true, true);
            if (_quickJoinButton != null)
            {
                _quickJoinButton.SetEnabled(false);
            }

            if (string.Equals(snapshot.MatchState, "in_match", StringComparison.OrdinalIgnoreCase))
            {
                OnMatchStateChanged(snapshot.MatchState);
            }
        }

        private void RefreshSteamIdentityLabel()
        {
            if (_steamIdentityLabel == null)
            {
                return;
            }

            if (_steamLobby == null)
            {
                _steamIdentityLabel.text = "Steam: service unavailable";
                return;
            }

            if (!_steamLobby.TryGetLocalPlayerIdentity(out var personaName, out var steamId, out var error))
            {
                _steamIdentityLabel.text = $"Steam: not available ({error})";
                return;
            }

            _steamIdentityLabel.text = $"Steam: {personaName} ({steamId})";
        }

        private void SetLobbyButtonsEnabled(bool ready, bool start, bool invite, bool leave)
        {
            if (_readyButton != null)
            {
                _readyButton.SetEnabled(ready);
            }

            if (_startMatchButton != null)
            {
                _startMatchButton.SetEnabled(start);
            }

            if (_inviteButton != null)
            {
                _inviteButton.SetEnabled(invite);
            }

            if (_leaveLobbyButton != null)
            {
                _leaveLobbyButton.SetEnabled(leave);
            }
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
