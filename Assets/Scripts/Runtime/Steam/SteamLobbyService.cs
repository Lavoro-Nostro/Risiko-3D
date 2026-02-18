using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Risiko3D.Runtime.Bootstrap;
using Risiko3D.Runtime.Configuration;
using UnityEngine;

namespace Risiko3D.Runtime.Steam
{
    public sealed class SteamLobbyService : MonoBehaviour
    {
        public event Action<LobbyOperationResult> LobbyCreated;
        public event Action<LobbyOperationResult> LobbyJoined;
        public event Action<LobbyOperationResult> MatchStarted;
        public event Action LobbyStateUpdated;
        public event Action<string> MatchStateChanged;

        public ulong CurrentLobbyId { get; private set; }
        public string CurrentRoomCode { get; private set; } = string.Empty;
        public bool IsInLobby => CurrentLobbyId != 0;

        private GameRuntimeConfig _config;
        private SteamRuntime _runtime;
        private int _pendingLobbyMaxPlayers;
#if DISABLESTEAMWORKS
        private ulong _fakeLocalSteamId;
        private string _fakePersonaName = string.Empty;
#endif

#if !DISABLESTEAMWORKS
        private Steamworks.CallResult<Steamworks.LobbyCreated_t> _lobbyCreatedCallResult;
        private Steamworks.CallResult<Steamworks.LobbyEnter_t> _lobbyEnterCallResult;
        private Steamworks.Callback<Steamworks.LobbyDataUpdate_t> _lobbyDataUpdateCallback;
        private Steamworks.Callback<Steamworks.LobbyChatUpdate_t> _lobbyChatUpdateCallback;
#endif

        public void Initialize(GameRuntimeConfig config, SteamRuntime runtime)
        {
            _config = config;
            _runtime = runtime;

#if !DISABLESTEAMWORKS
            _lobbyCreatedCallResult ??= Steamworks.CallResult<Steamworks.LobbyCreated_t>.Create(OnLobbyCreatedInternal);
            _lobbyEnterCallResult ??= Steamworks.CallResult<Steamworks.LobbyEnter_t>.Create(OnLobbyEnteredInternal);
            _lobbyDataUpdateCallback ??= Steamworks.Callback<Steamworks.LobbyDataUpdate_t>.Create(OnLobbyDataUpdatedInternal);
            _lobbyChatUpdateCallback ??= Steamworks.Callback<Steamworks.LobbyChatUpdate_t>.Create(OnLobbyChatUpdatedInternal);
#else
            FakeSteamBackend.RegisterService(this, out _fakeLocalSteamId, out _fakePersonaName);
#endif
        }

        public bool TryGetLocalPlayerIdentity(out string personaName, out ulong steamId, out string error)
        {
            personaName = string.Empty;
            steamId = 0;
            error = string.Empty;
            if (!EnsureReady(out error))
            {
                return false;
            }

#if DISABLESTEAMWORKS
            if (_fakeLocalSteamId == 0)
            {
                _ = FakeSteamBackend.RegisterService(this, out _fakeLocalSteamId, out _fakePersonaName);
            }

            steamId = _fakeLocalSteamId;
            personaName = string.IsNullOrWhiteSpace(_fakePersonaName) ? $"EditorPlayer{steamId}" : _fakePersonaName;
            return true;
#else
            var self = Steamworks.SteamUser.GetSteamID();
            steamId = self.m_SteamID;
            if (steamId == 0)
            {
                error = "Steam identity unavailable.";
                return false;
            }

            personaName = Steamworks.SteamFriends.GetPersonaName();
            if (string.IsNullOrWhiteSpace(personaName))
            {
                personaName = $"SteamUser:{steamId}";
            }

            return true;
#endif
        }

        public bool CreateLobby(int requestedPlayers, out string error)
        {
            error = string.Empty;
            if (!EnsureReady(out error))
            {
                return false;
            }

            if (!StartupHealthChecks.ValidateLobbyPlayerRange(requestedPlayers, _config, out error))
            {
                return false;
            }

            _pendingLobbyMaxPlayers = requestedPlayers;

#if DISABLESTEAMWORKS
            if (!FakeSteamBackend.CreateLobby(this, requestedPlayers, out var lobbyId, out error))
            {
                return false;
            }

            CurrentLobbyId = lobbyId;
            CurrentRoomCode = BuildRoomCode(lobbyId);
            LobbyCreated?.Invoke(LobbyOperationResult.FromSuccess(CurrentLobbyId, "Lobby created (editor simulated)."));
            LobbyStateUpdated?.Invoke();
            MatchStateChanged?.Invoke("open");
            return true;
#else
            var call = Steamworks.SteamMatchmaking.CreateLobby(
                Steamworks.ELobbyType.k_ELobbyTypeFriendsOnly,
                requestedPlayers);
            _lobbyCreatedCallResult.Set(call);
            return true;
#endif
        }

        public bool JoinLobby(ulong lobbyId, out string error)
        {
            error = string.Empty;
            if (!EnsureReady(out error))
            {
                return false;
            }

            if (lobbyId == 0)
            {
                error = "lobbyId cannot be 0.";
                return false;
            }

#if DISABLESTEAMWORKS
            if (!FakeSteamBackend.JoinLobby(this, lobbyId, out error))
            {
                return false;
            }

            CurrentLobbyId = lobbyId;
            CurrentRoomCode = BuildRoomCode(lobbyId);
            LobbyJoined?.Invoke(LobbyOperationResult.FromSuccess(CurrentLobbyId, "Lobby joined (editor simulated)."));
            LobbyStateUpdated?.Invoke();
            MatchStateChanged?.Invoke(FakeSteamBackend.GetMatchState(lobbyId));
            return true;
#else
            var call = Steamworks.SteamMatchmaking.JoinLobby(new Steamworks.CSteamID(lobbyId));
            _lobbyEnterCallResult.Set(call);
            return true;
#endif
        }

        public bool JoinByRoomCode(string roomCode, out string error)
        {
            error = string.Empty;
            if (!TryParseRoomCode(roomCode, out var lobbyId))
            {
                error = "Invalid room code.";
                return false;
            }

            return JoinLobby(lobbyId, out error);
        }

        public bool TryGetFriendJoinableLobbies(out List<FriendLobbyInfo> lobbies, out string error)
        {
            lobbies = new List<FriendLobbyInfo>();
            error = string.Empty;
            if (!EnsureReady(out error))
            {
                return false;
            }

#if DISABLESTEAMWORKS
            lobbies = FakeSteamBackend.GetJoinableLobbies(this);
            return true;
#else
            var flags = Steamworks.EFriendFlags.k_EFriendFlagImmediate;
            var friendCount = Steamworks.SteamFriends.GetFriendCount(flags);
            for (var i = 0; i < friendCount; i++)
            {
                var friendId = Steamworks.SteamFriends.GetFriendByIndex(i, flags);
                if (friendId == Steamworks.CSteamID.Nil)
                {
                    continue;
                }

                if (!Steamworks.SteamFriends.GetFriendGamePlayed(friendId, out var gameInfo))
                {
                    continue;
                }

                var lobbyId = gameInfo.m_steamIDLobby.m_SteamID;
                if (lobbyId == 0)
                {
                    continue;
                }

                var friendName = Steamworks.SteamFriends.GetFriendPersonaName(friendId);
                var lobby = new Steamworks.CSteamID(lobbyId);
                var roomCode = Steamworks.SteamMatchmaking.GetLobbyData(lobby, "room_code");
                lobbies.Add(new FriendLobbyInfo(
                    friendName,
                    friendId.m_SteamID,
                    lobbyId,
                    roomCode));
            }

            return true;
#endif
        }

        public bool StartMatch(out string error)
        {
            error = string.Empty;
            if (!EnsureReady(out error))
            {
                return false;
            }

            if (!IsInLobby)
            {
                error = "No active lobby.";
                return false;
            }

#if DISABLESTEAMWORKS
            if (!FakeSteamBackend.StartMatch(this, out error))
            {
                return false;
            }

            MatchStarted?.Invoke(LobbyOperationResult.FromSuccess(CurrentLobbyId, "Match marked as in_match (editor simulated)."));
            MatchStateChanged?.Invoke("in_match");
            LobbyStateUpdated?.Invoke();
            return true;
#else
            var lobby = new Steamworks.CSteamID(CurrentLobbyId);
            if (!IsLocalPlayerHost(lobby))
            {
                error = "Only lobby host can start the match.";
                return false;
            }

            var maxPlayersRaw = Steamworks.SteamMatchmaking.GetLobbyData(lobby, "max_players");
            var maxPlayers = ParseInt(maxPlayersRaw, _config != null ? _config.MaxPlayers : 6);
            if (!ApplyLobbyMetadata(lobby, "in_match", maxPlayers))
            {
                error = "Failed to apply in_match metadata.";
                return false;
            }

            MatchStarted?.Invoke(LobbyOperationResult.FromSuccess(CurrentLobbyId, "Match marked as in_match."));
            MatchStateChanged?.Invoke("in_match");
            LobbyStateUpdated?.Invoke();
            return true;
#endif
        }

        public bool OpenInviteOverlay(out string error)
        {
            error = string.Empty;
            if (!EnsureReady(out error))
            {
                return false;
            }

            if (!IsInLobby)
            {
                error = "No active lobby.";
                return false;
            }

#if DISABLESTEAMWORKS
            return true;
#else
            Steamworks.SteamFriends.ActivateGameOverlayInviteDialog(new Steamworks.CSteamID(CurrentLobbyId));
            return true;
#endif
        }

#if DISABLESTEAMWORKS
        internal ulong EditorLocalSteamId => _fakeLocalSteamId;
        internal string EditorPersonaName => _fakePersonaName;

        internal void NotifyEditorLobbyStateChanged(string matchState)
        {
            LobbyStateUpdated?.Invoke();
            if (!string.IsNullOrWhiteSpace(matchState))
            {
                MatchStateChanged?.Invoke(matchState);
            }
        }

        private void OnDestroy()
        {
            FakeSteamBackend.UnregisterService(this);
        }
#endif

        public bool LeaveLobby(out string error)
        {
            error = string.Empty;
            if (!EnsureReady(out error))
            {
                return false;
            }

            if (!IsInLobby)
            {
                error = "No active lobby.";
                return false;
            }

#if DISABLESTEAMWORKS
            if (!FakeSteamBackend.LeaveLobby(this, out error))
            {
                return false;
            }

            CurrentLobbyId = 0;
            CurrentRoomCode = string.Empty;
            LobbyStateUpdated?.Invoke();
            MatchStateChanged?.Invoke("open");
            return true;
#else
            Steamworks.SteamMatchmaking.LeaveLobby(new Steamworks.CSteamID(CurrentLobbyId));
            CurrentLobbyId = 0;
            CurrentRoomCode = string.Empty;
            LobbyStateUpdated?.Invoke();
            MatchStateChanged?.Invoke("open");
            return true;
#endif
        }

        public bool SetLocalReady(bool ready, out string error)
        {
            error = string.Empty;
            if (!EnsureReady(out error))
            {
                return false;
            }

            if (!IsInLobby)
            {
                error = "No active lobby.";
                return false;
            }

#if DISABLESTEAMWORKS
            if (!FakeSteamBackend.SetReady(this, ready, out error))
            {
                return false;
            }

            LobbyStateUpdated?.Invoke();
            return true;
#else
            Steamworks.SteamMatchmaking.SetLobbyMemberData(
                new Steamworks.CSteamID(CurrentLobbyId),
                "ready",
                ready ? "1" : "0");

            LobbyStateUpdated?.Invoke();
            return true;
#endif
        }

        public bool TryGetCurrentLobbySnapshot(out SteamLobbySnapshot snapshot, out string error)
        {
            snapshot = SteamLobbySnapshot.Empty;
            error = string.Empty;
            if (!EnsureReady(out error))
            {
                return false;
            }

            if (!IsInLobby)
            {
                error = "No active lobby.";
                return false;
            }

#if DISABLESTEAMWORKS
            if (!FakeSteamBackend.TryGetSnapshot(this, out snapshot, out error))
            {
                return false;
            }

            return true;
#else
            var lobby = new Steamworks.CSteamID(CurrentLobbyId);
            var roomCode = Steamworks.SteamMatchmaking.GetLobbyData(lobby, "room_code");
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                roomCode = CurrentRoomCode;
            }

            var matchState = Steamworks.SteamMatchmaking.GetLobbyData(lobby, "match_state");
            if (string.IsNullOrWhiteSpace(matchState))
            {
                matchState = "open";
            }

            var maxPlayersRaw = Steamworks.SteamMatchmaking.GetLobbyData(lobby, "max_players");
            var maxPlayers = ParseInt(maxPlayersRaw, _config != null ? _config.MaxPlayers : 6);
            var currentPlayers = Steamworks.SteamMatchmaking.GetNumLobbyMembers(lobby);
            var localSteamId = Steamworks.SteamUser.GetSteamID().m_SteamID;
            var localName = Steamworks.SteamFriends.GetPersonaName();
            if (string.IsNullOrWhiteSpace(localName))
            {
                localName = $"SteamUser:{localSteamId}";
            }

            var hostSteamId = ResolveHostSteamId(lobby);
            var isLocalHost = hostSteamId != 0 && hostSteamId == localSteamId;
            var members = new List<SteamLobbyMemberInfo>(Mathf.Max(0, currentPlayers));
            var localReady = false;

            for (var i = 0; i < currentPlayers; i++)
            {
                var memberSteam = Steamworks.SteamMatchmaking.GetLobbyMemberByIndex(lobby, i);
                if (memberSteam == Steamworks.CSteamID.Nil)
                {
                    continue;
                }

                var memberId = memberSteam.m_SteamID;
                var memberName = Steamworks.SteamFriends.GetFriendPersonaName(memberSteam);
                if (string.IsNullOrWhiteSpace(memberName))
                {
                    memberName = $"SteamUser:{memberId}";
                }

                var readyRaw = Steamworks.SteamMatchmaking.GetLobbyMemberData(lobby, memberSteam, "ready");
                var ready = ParseReady(readyRaw);
                var isLocal = memberId == localSteamId;
                var isHost = memberId == hostSteamId;
                if (isLocal)
                {
                    localReady = ready;
                }

                members.Add(new SteamLobbyMemberInfo(memberId, memberName, ready, isLocal, isHost));
            }

            snapshot = new SteamLobbySnapshot(
                CurrentLobbyId,
                roomCode,
                matchState,
                hostSteamId,
                currentPlayers,
                maxPlayers,
                localSteamId,
                localName,
                isLocalHost,
                localReady,
                members);
            return true;
#endif
        }

        private bool EnsureReady(out string error)
        {
            error = string.Empty;
            if (_config == null)
            {
                error = "SteamLobbyService is not initialized with config.";
                return false;
            }

            if (_runtime == null || !_runtime.IsInitialized)
            {
                error = "Steam runtime is not initialized.";
                return false;
            }

            return true;
        }

#if !DISABLESTEAMWORKS
        private void OnLobbyCreatedInternal(Steamworks.LobbyCreated_t callback, bool ioFailure)
        {
            if (ioFailure)
            {
                LobbyCreated?.Invoke(LobbyOperationResult.FromFailure("CreateLobby IO failure."));
                return;
            }

            if (callback.m_eResult != Steamworks.EResult.k_EResultOK)
            {
                LobbyCreated?.Invoke(LobbyOperationResult.FromFailure($"CreateLobby failed: {callback.m_eResult}."));
                return;
            }

            var lobbyId = callback.m_ulSteamIDLobby;
            CurrentLobbyId = lobbyId;
            CurrentRoomCode = BuildRoomCode(lobbyId);
            var lobby = new Steamworks.CSteamID(lobbyId);
            var requestedMaxPlayers = _pendingLobbyMaxPlayers > 0
                ? _pendingLobbyMaxPlayers
                : (_config != null ? _config.MaxPlayers : 6);
            var metadataOk = ApplyLobbyMetadata(lobby, "open", requestedMaxPlayers);
            if (!metadataOk)
            {
                LobbyCreated?.Invoke(LobbyOperationResult.FromFailure("Lobby created but metadata write failed."));
                return;
            }

            Steamworks.SteamMatchmaking.SetLobbyMemberData(lobby, "ready", "0");
            LobbyCreated?.Invoke(LobbyOperationResult.FromSuccess(CurrentLobbyId, "Lobby created."));
            LobbyStateUpdated?.Invoke();
        }

        private void OnLobbyEnteredInternal(Steamworks.LobbyEnter_t callback, bool ioFailure)
        {
            if (ioFailure)
            {
                LobbyJoined?.Invoke(LobbyOperationResult.FromFailure("JoinLobby IO failure."));
                return;
            }

            CurrentLobbyId = callback.m_ulSteamIDLobby;
            var lobby = new Steamworks.CSteamID(CurrentLobbyId);
            CurrentRoomCode = Steamworks.SteamMatchmaking.GetLobbyData(lobby, "room_code");
            if (string.IsNullOrWhiteSpace(CurrentRoomCode))
            {
                CurrentRoomCode = BuildRoomCode(CurrentLobbyId);
            }

            Steamworks.SteamMatchmaking.SetLobbyMemberData(lobby, "ready", "0");
            Steamworks.SteamMatchmaking.SetLobbyData(
                lobby,
                "current_players",
                Steamworks.SteamMatchmaking.GetNumLobbyMembers(lobby).ToString());

            LobbyJoined?.Invoke(LobbyOperationResult.FromSuccess(CurrentLobbyId, "Lobby joined."));
            LobbyStateUpdated?.Invoke();
            MatchStateChanged?.Invoke(GetLobbyMatchState(lobby));
        }

        private void OnLobbyDataUpdatedInternal(Steamworks.LobbyDataUpdate_t callback)
        {
            if (callback.m_ulSteamIDLobby != CurrentLobbyId || callback.m_bSuccess != 1)
            {
                return;
            }

            var lobby = new Steamworks.CSteamID(CurrentLobbyId);
            MatchStateChanged?.Invoke(GetLobbyMatchState(lobby));
            LobbyStateUpdated?.Invoke();
        }

        private void OnLobbyChatUpdatedInternal(Steamworks.LobbyChatUpdate_t callback)
        {
            if (callback.m_ulSteamIDLobby != CurrentLobbyId)
            {
                return;
            }

            var lobby = new Steamworks.CSteamID(CurrentLobbyId);
            if (IsLocalPlayerHost(lobby))
            {
                Steamworks.SteamMatchmaking.SetLobbyData(
                    lobby,
                    "current_players",
                    Steamworks.SteamMatchmaking.GetNumLobbyMembers(lobby).ToString());
            }

            LobbyStateUpdated?.Invoke();
        }

        private bool ApplyLobbyMetadata(Steamworks.CSteamID lobbyId, string matchState, int maxPlayers)
        {
            var hostSteamId = Steamworks.SteamUser.GetSteamID().m_SteamID.ToString(CultureInfo.InvariantCulture);
            var currentPlayers = Steamworks.SteamMatchmaking.GetNumLobbyMembers(lobbyId).ToString();
            var normalizedMaxPlayers = Mathf.Max(2, maxPlayers);

            var ok = true;
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "build_version", _config.BuildVersion);
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "rules_profile_id", _config.RulesProfileId);
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "map_id", _config.MapId);
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "match_state", matchState);
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "host_steam_id", hostSteamId);
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "max_players", normalizedMaxPlayers.ToString(CultureInfo.InvariantCulture));
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "current_players", currentPlayers);
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "contract_version", _config.RuntimeContractVersion);
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "room_code", CurrentRoomCode);
            return ok;
        }

        private static bool ParseReady(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return string.Equals(value, "1", StringComparison.Ordinal) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "ready", StringComparison.OrdinalIgnoreCase);
        }

        private static int ParseInt(string raw, int fallback)
        {
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        }

        private static string GetLobbyMatchState(Steamworks.CSteamID lobbyId)
        {
            var matchState = Steamworks.SteamMatchmaking.GetLobbyData(lobbyId, "match_state");
            return string.IsNullOrWhiteSpace(matchState) ? "open" : matchState;
        }

        private static ulong ResolveHostSteamId(Steamworks.CSteamID lobbyId)
        {
            var hostRaw = Steamworks.SteamMatchmaking.GetLobbyData(lobbyId, "host_steam_id");
            if (ulong.TryParse(hostRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hostId) && hostId != 0)
            {
                return hostId;
            }

            var owner = Steamworks.SteamMatchmaking.GetLobbyOwner(lobbyId);
            return owner.m_SteamID;
        }

        private static bool IsLocalPlayerHost(Steamworks.CSteamID lobbyId)
        {
            var local = Steamworks.SteamUser.GetSteamID().m_SteamID;
            if (local == 0)
            {
                return false;
            }

            return ResolveHostSteamId(lobbyId) == local;
        }
#endif

        private static string BuildRoomCode(ulong lobbyId)
        {
            return lobbyId.ToString("X").ToUpperInvariant();
        }

        private static bool TryParseRoomCode(string roomCode, out ulong lobbyId)
        {
            lobbyId = 0;
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                return false;
            }

            return ulong.TryParse(roomCode.Trim(), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out lobbyId)
                && lobbyId != 0;
        }
    }

#if DISABLESTEAMWORKS
    internal static class FakeSteamBackend
    {
        private static readonly Dictionary<SteamLobbyService, ulong> ServiceLocalIds = new();
        private static readonly string StateFilePath = Path.Combine(Application.temporaryCachePath, "risiko3d_fake_steam_state.json");
        private const string MutexName = "Global\\Risiko3D_FakeSteamBackend";
        private const ulong InitialLocalId = 11000000000000001;
        private const ulong InitialLobbyId = 88000000000000001;

        public static bool RegisterService(SteamLobbyService service, out ulong localSteamId, out string personaName)
        {
            using (var guard = AcquireStateLock())
            {
                if (ServiceLocalIds.TryGetValue(service, out localSteamId))
                {
                    personaName = BuildPersonaName(localSteamId);
                    return true;
                }

                var state = LoadState();
                localSteamId = state.NextLocalId;
                state.NextLocalId += 1;
                SaveState(state);
                ServiceLocalIds[service] = localSteamId;
                personaName = BuildPersonaName(localSteamId);
                return true;
            }
        }

        public static void UnregisterService(SteamLobbyService service)
        {
            using (var guard = AcquireStateLock())
            {
                if (!ServiceLocalIds.TryGetValue(service, out var localId))
                {
                    return;
                }

                ServiceLocalIds.Remove(service);
                if (service.CurrentLobbyId == 0)
                {
                    return;
                }

                _ = LeaveLobbyInternal(localId, service.CurrentLobbyId, out _);
            }
        }

        public static bool CreateLobby(SteamLobbyService service, int requestedPlayers, out ulong lobbyId, out string error)
        {
            lobbyId = 0;
            error = string.Empty;
            using (var guard = AcquireStateLock())
            {
                if (!TryGetLocalId(service, out var localId, out error))
                {
                    return false;
                }

                if (service.CurrentLobbyId != 0)
                {
                    error = "Already in a lobby.";
                    return false;
                }

                var state = LoadState();
                lobbyId = state.NextLobbyId;
                state.NextLobbyId += 1;
                var roomCode = lobbyId.ToString("X", CultureInfo.InvariantCulture).ToUpperInvariant();
                var lobby = new FakeLobbyStateData
                {
                    LobbyId = lobbyId,
                    RoomCode = roomCode,
                    HostSteamId = localId,
                    MaxPlayers = Math.Max(1, requestedPlayers),
                    MatchState = "open"
                };
                lobby.Members.Add(new FakeLobbyMemberData
                {
                    SteamId = localId,
                    DisplayName = service.EditorPersonaName,
                    IsReady = false
                });
                state.Lobbies.Add(lobby);
                SaveState(state);
                return true;
            }
        }

        public static bool JoinLobby(SteamLobbyService service, ulong lobbyId, out string error)
        {
            error = string.Empty;
            using (var guard = AcquireStateLock())
            {
                if (!TryGetLocalId(service, out var localId, out error))
                {
                    return false;
                }

                var state = LoadState();
                var lobby = FindLobby(state, lobbyId);
                if (lobby == null)
                {
                    error = "Lobby not found.";
                    return false;
                }

                if (service.CurrentLobbyId != 0 && service.CurrentLobbyId != lobbyId)
                {
                    error = "Already in another lobby.";
                    return false;
                }

                if (FindMember(lobby, localId) == null)
                {
                    if (lobby.Members.Count >= lobby.MaxPlayers)
                    {
                        error = "Lobby is full.";
                        return false;
                    }

                    lobby.Members.Add(new FakeLobbyMemberData
                    {
                        SteamId = localId,
                        DisplayName = service.EditorPersonaName,
                        IsReady = false
                    });
                }

                SaveState(state);
                return true;
            }
        }

        public static List<FriendLobbyInfo> GetJoinableLobbies(SteamLobbyService _)
        {
            using (var guard = AcquireStateLock())
            {
                var state = LoadState();
                var result = new List<FriendLobbyInfo>();
                foreach (var entry in state.Lobbies)
                {
                    if (entry.MatchState.Equals("in_match", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var host = FindMember(entry, entry.HostSteamId);
                    if (host == null)
                    {
                        continue;
                    }

                    result.Add(new FriendLobbyInfo(host.DisplayName, host.SteamId, entry.LobbyId, entry.RoomCode));
                }

                return result;
            }
        }

        public static bool StartMatch(SteamLobbyService service, out string error)
        {
            error = string.Empty;
            using (var guard = AcquireStateLock())
            {
                if (!TryGetLocalAndLobby(service, out var localId, out var lobbyId, out error))
                {
                    return false;
                }

                var state = LoadState();
                var lobby = FindLobby(state, lobbyId);
                if (lobby == null)
                {
                    error = "Lobby not found.";
                    return false;
                }

                if (lobby.HostSteamId != localId)
                {
                    error = "Only lobby host can start the match.";
                    return false;
                }

                lobby.MatchState = "in_match";
                SaveState(state);
                return true;
            }
        }

        public static bool LeaveLobby(SteamLobbyService service, out string error)
        {
            error = string.Empty;
            using (var guard = AcquireStateLock())
            {
                if (!TryGetLocalAndLobby(service, out var localId, out var lobbyId, out error))
                {
                    return false;
                }

                return LeaveLobbyInternal(localId, lobbyId, out error);
            }
        }

        public static bool SetReady(SteamLobbyService service, bool ready, out string error)
        {
            error = string.Empty;
            using (var guard = AcquireStateLock())
            {
                if (!TryGetLocalAndLobby(service, out var localId, out var lobbyId, out error))
                {
                    return false;
                }

                var state = LoadState();
                var lobby = FindLobby(state, lobbyId);
                if (lobby == null)
                {
                    error = "Lobby not found.";
                    return false;
                }

                var member = FindMember(lobby, localId);
                if (member == null)
                {
                    error = "Member not found.";
                    return false;
                }

                member.IsReady = ready;
                SaveState(state);
                return true;
            }
        }

        public static bool TryGetSnapshot(SteamLobbyService service, out SteamLobbySnapshot snapshot, out string error)
        {
            snapshot = SteamLobbySnapshot.Empty;
            error = string.Empty;
            using (var guard = AcquireStateLock())
            {
                if (!TryGetLocalAndLobby(service, out var localId, out var lobbyId, out error))
                {
                    return false;
                }

                var state = LoadState();
                var lobby = FindLobby(state, lobbyId);
                if (lobby == null)
                {
                    error = "Lobby not found.";
                    return false;
                }

                var members = new List<SteamLobbyMemberInfo>(lobby.Members.Count);
                foreach (var member in lobby.Members)
                {
                    members.Add(new SteamLobbyMemberInfo(
                        member.SteamId,
                        member.DisplayName,
                        member.IsReady,
                        member.SteamId == localId,
                        member.SteamId == lobby.HostSteamId));
                }

                var localMember = FindMember(lobby, localId);
                var localName = localMember != null ? localMember.DisplayName : service.EditorPersonaName;
                var localReady = localMember != null && localMember.IsReady;
                snapshot = new SteamLobbySnapshot(
                    lobby.LobbyId,
                    lobby.RoomCode,
                    lobby.MatchState,
                    lobby.HostSteamId,
                    lobby.Members.Count,
                    lobby.MaxPlayers,
                    localId,
                    localName,
                    localId == lobby.HostSteamId,
                    localReady,
                    members);
                return true;
            }
        }

        public static string GetMatchState(ulong lobbyId)
        {
            using (var guard = AcquireStateLock())
            {
                var state = LoadState();
                var lobby = FindLobby(state, lobbyId);
                return lobby != null ? lobby.MatchState : "open";
            }
        }

        private static bool TryGetLocalId(SteamLobbyService service, out ulong localId, out string error)
        {
            localId = 0;
            error = string.Empty;
            if (!ServiceLocalIds.TryGetValue(service, out localId))
            {
                error = "Local editor player is not registered.";
                return false;
            }

            return true;
        }

        private static bool TryGetLocalAndLobby(SteamLobbyService service, out ulong localId, out ulong lobbyId, out string error)
        {
            localId = 0;
            lobbyId = 0;
            error = string.Empty;
            if (!TryGetLocalId(service, out localId, out error))
            {
                return false;
            }

            lobbyId = service.CurrentLobbyId;
            if (lobbyId == 0)
            {
                error = "No active lobby.";
                return false;
            }

            return true;
        }

        private static bool LeaveLobbyInternal(ulong localId, ulong lobbyId, out string error)
        {
            error = string.Empty;
            var state = LoadState();
            var lobby = FindLobby(state, lobbyId);
            if (lobby == null)
            {
                error = "Lobby not found.";
                return false;
            }

            RemoveMember(lobby, localId);
            if (lobby.Members.Count == 0)
            {
                state.Lobbies.RemoveAll(x => x.LobbyId == lobbyId);
                SaveState(state);
                return true;
            }

            if (lobby.HostSteamId == localId)
            {
                foreach (var member in lobby.Members)
                {
                    lobby.HostSteamId = member.SteamId;
                    break;
                }
            }

            SaveState(state);
            return true;
        }

        private static string BuildPersonaName(ulong steamId) =>
            $"EditorPlayer{(steamId % 1000).ToString("D3", CultureInfo.InvariantCulture)}";

        private static IDisposable AcquireStateLock()
        {
            var mutex = new System.Threading.Mutex(false, MutexName);
            mutex.WaitOne();
            return new MutexGuard(mutex);
        }

        private static FakeSteamStateFile LoadState()
        {
            if (!File.Exists(StateFilePath))
            {
                return new FakeSteamStateFile
                {
                    NextLocalId = InitialLocalId,
                    NextLobbyId = InitialLobbyId,
                    Lobbies = new List<FakeLobbyStateData>()
                };
            }

            try
            {
                var json = File.ReadAllText(StateFilePath);
                var state = JsonUtility.FromJson<FakeSteamStateFile>(json);
                if (state == null)
                {
                    return new FakeSteamStateFile
                    {
                        NextLocalId = InitialLocalId,
                        NextLobbyId = InitialLobbyId,
                        Lobbies = new List<FakeLobbyStateData>()
                    };
                }

                state.Lobbies ??= new List<FakeLobbyStateData>();
                if (state.NextLocalId == 0)
                {
                    state.NextLocalId = InitialLocalId;
                }

                if (state.NextLobbyId == 0)
                {
                    state.NextLobbyId = InitialLobbyId;
                }

                return state;
            }
            catch
            {
                return new FakeSteamStateFile
                {
                    NextLocalId = InitialLocalId,
                    NextLobbyId = InitialLobbyId,
                    Lobbies = new List<FakeLobbyStateData>()
                };
            }
        }

        private static void SaveState(FakeSteamStateFile state)
        {
            state ??= new FakeSteamStateFile();
            state.Lobbies ??= new List<FakeLobbyStateData>();
            var json = JsonUtility.ToJson(state, true);
            Directory.CreateDirectory(Path.GetDirectoryName(StateFilePath) ?? Application.temporaryCachePath);
            File.WriteAllText(StateFilePath, json);
        }

        private static FakeLobbyStateData FindLobby(FakeSteamStateFile state, ulong lobbyId)
        {
            if (state?.Lobbies == null)
            {
                return null;
            }

            for (var i = 0; i < state.Lobbies.Count; i++)
            {
                if (state.Lobbies[i].LobbyId == lobbyId)
                {
                    state.Lobbies[i].Members ??= new List<FakeLobbyMemberData>();
                    return state.Lobbies[i];
                }
            }

            return null;
        }

        private static FakeLobbyMemberData FindMember(FakeLobbyStateData lobby, ulong steamId)
        {
            if (lobby?.Members == null)
            {
                return null;
            }

            for (var i = 0; i < lobby.Members.Count; i++)
            {
                if (lobby.Members[i].SteamId == steamId)
                {
                    return lobby.Members[i];
                }
            }

            return null;
        }

        private static void RemoveMember(FakeLobbyStateData lobby, ulong steamId)
        {
            if (lobby?.Members == null)
            {
                return;
            }

            lobby.Members.RemoveAll(m => m.SteamId == steamId);
        }

        [Serializable]
        private sealed class FakeSteamStateFile
        {
            public ulong NextLocalId;
            public ulong NextLobbyId;
            public List<FakeLobbyStateData> Lobbies = new();
        }

        [Serializable]
        private sealed class FakeLobbyStateData
        {
            public ulong LobbyId;
            public string RoomCode = string.Empty;
            public ulong HostSteamId;
            public int MaxPlayers;
            public string MatchState = "open";
            public List<FakeLobbyMemberData> Members = new();
        }

        [Serializable]
        private sealed class FakeLobbyMemberData
        {
            public ulong SteamId;
            public string DisplayName = string.Empty;
            public bool IsReady;
        }

        private sealed class MutexGuard : IDisposable
        {
            private readonly System.Threading.Mutex _mutex;

            public MutexGuard(System.Threading.Mutex mutex)
            {
                _mutex = mutex;
            }

            public void Dispose()
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                finally
                {
                    _mutex.Dispose();
                }
            }
        }
    }
#endif

    public readonly struct SteamLobbySnapshot
    {
        public static SteamLobbySnapshot Empty { get; } = new(
            0,
            string.Empty,
            "open",
            0,
            0,
            0,
            0,
            string.Empty,
            false,
            false,
            Array.Empty<SteamLobbyMemberInfo>());

        public ulong LobbyId { get; }
        public string RoomCode { get; }
        public string MatchState { get; }
        public ulong HostSteamId { get; }
        public int CurrentPlayers { get; }
        public int MaxPlayers { get; }
        public ulong LocalPlayerSteamId { get; }
        public string LocalPlayerName { get; }
        public bool IsLocalPlayerHost { get; }
        public bool IsLocalPlayerReady { get; }
        public IReadOnlyList<SteamLobbyMemberInfo> Members { get; }

        public SteamLobbySnapshot(
            ulong lobbyId,
            string roomCode,
            string matchState,
            ulong hostSteamId,
            int currentPlayers,
            int maxPlayers,
            ulong localPlayerSteamId,
            string localPlayerName,
            bool isLocalPlayerHost,
            bool isLocalPlayerReady,
            IReadOnlyList<SteamLobbyMemberInfo> members)
        {
            LobbyId = lobbyId;
            RoomCode = roomCode ?? string.Empty;
            MatchState = string.IsNullOrWhiteSpace(matchState) ? "open" : matchState;
            HostSteamId = hostSteamId;
            CurrentPlayers = Mathf.Max(0, currentPlayers);
            MaxPlayers = Mathf.Max(0, maxPlayers);
            LocalPlayerSteamId = localPlayerSteamId;
            LocalPlayerName = localPlayerName ?? string.Empty;
            IsLocalPlayerHost = isLocalPlayerHost;
            IsLocalPlayerReady = isLocalPlayerReady;
            Members = members ?? Array.Empty<SteamLobbyMemberInfo>();
        }
    }

    public readonly struct SteamLobbyMemberInfo
    {
        public ulong SteamId { get; }
        public string DisplayName { get; }
        public bool IsReady { get; }
        public bool IsLocalPlayer { get; }
        public bool IsHost { get; }

        public SteamLobbyMemberInfo(ulong steamId, string displayName, bool isReady, bool isLocalPlayer, bool isHost)
        {
            SteamId = steamId;
            DisplayName = displayName ?? string.Empty;
            IsReady = isReady;
            IsLocalPlayer = isLocalPlayer;
            IsHost = isHost;
        }
    }

    public readonly struct FriendLobbyInfo
    {
        public string FriendName { get; }
        public ulong FriendSteamId { get; }
        public ulong LobbyId { get; }
        public string RoomCode { get; }

        public FriendLobbyInfo(string friendName, ulong friendSteamId, ulong lobbyId, string roomCode)
        {
            FriendName = friendName ?? string.Empty;
            FriendSteamId = friendSteamId;
            LobbyId = lobbyId;
            RoomCode = roomCode ?? string.Empty;
        }
    }

    public readonly struct LobbyOperationResult
    {
        public bool Success { get; }
        public ulong LobbyId { get; }
        public string Message { get; }

        private LobbyOperationResult(bool success, ulong lobbyId, string message)
        {
            Success = success;
            LobbyId = lobbyId;
            Message = message ?? string.Empty;
        }

        public static LobbyOperationResult FromSuccess(ulong lobbyId, string message)
        {
            return new LobbyOperationResult(true, lobbyId, message);
        }

        public static LobbyOperationResult FromFailure(string message)
        {
            return new LobbyOperationResult(false, 0, message);
        }
    }
}
