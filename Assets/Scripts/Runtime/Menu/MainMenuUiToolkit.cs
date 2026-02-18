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
            RefreshLanAnnouncements();
        }

        private void OnDestroy()
        {
            if (_steamLobby != null)
            {
                _steamLobby.LobbyCreated -= OnLobbyCreated;
                _steamLobby.LobbyJoined -= OnLobbyJoined;
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

            SetStatus($"Lobby created. room={_steamLobby?.CurrentRoomCode}");
            LoadGameplayScene();
        }

        private void OnLobbyJoined(LobbyOperationResult result)
        {
            if (!result.Success)
            {
                SetStatus($"Join failed: {result.Message}");
                return;
            }

            SetStatus($"Lobby joined. room={_steamLobby?.CurrentRoomCode}");
            LoadGameplayScene();
        }

        private void LoadGameplayScene()
        {
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
