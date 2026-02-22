using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Risiko3D.Runtime.Bootstrap;
using Risiko3D.Runtime.Configuration;
using UnityEngine;

namespace Risiko3D.Runtime.Steam
{
    public sealed class SteamLobbyService : MonoBehaviour
    {
#pragma warning disable CS0067
        public event Action<LobbyOperationResult> LobbyCreated;
        public event Action<LobbyOperationResult> LobbyJoined;
        public event Action<LobbyOperationResult> MatchStarted;
#pragma warning restore CS0067

        public ulong CurrentLobbyId { get; private set; }
        public string CurrentRoomCode { get; private set; } = string.Empty;
        public bool IsInLobby => CurrentLobbyId != 0;
        public bool IsLocalReady { get; private set; }
        public bool IsLocalHost { get; private set; }

        private GameRuntimeConfig _config;
        private SteamRuntime _runtime;
        private ulong _simLocalSteamId;
        private string _simLocalDisplayName = string.Empty;
        private bool _verboseLogs;
        private string _lastLobbyTraceSignature = string.Empty;

#if !DISABLESTEAMWORKS
        private Steamworks.CallResult<Steamworks.LobbyCreated_t> _lobbyCreatedCallResult;
        private Steamworks.CallResult<Steamworks.LobbyEnter_t> _lobbyEnterCallResult;
#endif

        public void Initialize(GameRuntimeConfig config, SteamRuntime runtime)
        {
            _config = config;
            _runtime = runtime;
            _verboseLogs = _config != null && _config.EnableVerboseRuntimeLogs;
            EnsureSimLocalIdentity();

#if !DISABLESTEAMWORKS
            _lobbyCreatedCallResult ??= Steamworks.CallResult<Steamworks.LobbyCreated_t>.Create(OnLobbyCreatedInternal);
            _lobbyEnterCallResult ??= Steamworks.CallResult<Steamworks.LobbyEnter_t>.Create(OnLobbyEnteredInternal);
#endif
        }

        private void OnDestroy()
        {
#if DISABLESTEAMWORKS
            if (CurrentLobbyId != 0)
            {
                SimLobbyRegistry.RemoveMember(CurrentLobbyId, _simLocalSteamId);
            }
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

#if DISABLESTEAMWORKS
            CurrentLobbyId = SimLobbyRegistry.CreateLobby(requestedPlayers, _simLocalSteamId, _simLocalDisplayName);
            CurrentRoomCode = BuildRoomCode(CurrentLobbyId);
            IsLocalHost = true;
            IsLocalReady = false;
            LobbyCreated?.Invoke(LobbyOperationResult.FromSuccess(CurrentLobbyId, "Editor simulation lobby created."));
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
            if (!SimLobbyRegistry.TryJoinLobby(lobbyId, _simLocalSteamId, _simLocalDisplayName, out error))
            {
                return false;
            }

            CurrentLobbyId = lobbyId;
            CurrentRoomCode = BuildRoomCode(CurrentLobbyId);
            IsLocalHost = SimLobbyRegistry.IsHost(CurrentLobbyId, _simLocalSteamId);
            IsLocalReady = false;
            LobbyJoined?.Invoke(LobbyOperationResult.FromSuccess(CurrentLobbyId, "Editor simulation lobby joined."));
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
            foreach (var lobby in SimLobbyRegistry.ListJoinableLobbies(_simLocalSteamId))
            {
                lobbies.Add(new FriendLobbyInfo(
                    lobby.HostDisplayName,
                    lobby.HostSteamId,
                    lobby.LobbyId,
                    lobby.RoomCode));
            }

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
            if (!SimLobbyRegistry.CanStartMatch(CurrentLobbyId, _simLocalSteamId, out error))
            {
                return false;
            }

            SimLobbyRegistry.MarkMatchStarted(CurrentLobbyId);
            MatchStarted?.Invoke(LobbyOperationResult.FromSuccess(CurrentLobbyId, "Editor simulation match started."));
            return true;
#else
            var lobby = new Steamworks.CSteamID(CurrentLobbyId);
            if (!ApplyLobbyMetadata(lobby, "in_match"))
            {
                error = "Failed to apply in_match metadata.";
                return false;
            }

            MatchStarted?.Invoke(LobbyOperationResult.FromSuccess(CurrentLobbyId, "Match marked as in_match."));
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

        public bool TryGetCurrentLobbySnapshot(out CurrentLobbySnapshot snapshot, out string error)
        {
            snapshot = default;
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
            if (!SimLobbyRegistry.TryGetSnapshot(CurrentLobbyId, out var simSnapshot))
            {
                error = "Lobby no longer exists.";
                return false;
            }

            IsLocalHost = simSnapshot.HostSteamId == _simLocalSteamId;
            if (simSnapshot.TryGetMember(_simLocalSteamId, out var me))
            {
                IsLocalReady = me.Ready;
            }

            snapshot = new CurrentLobbySnapshot(
                CurrentLobbyId,
                CurrentRoomCode,
                simSnapshot.CurrentPlayers,
                simSnapshot.MaxPlayers,
                simSnapshot.MatchStarted ? "in_match" : "open");
            return true;
#else
            var lobby = new Steamworks.CSteamID(CurrentLobbyId);
            var currentPlayers = Steamworks.SteamMatchmaking.GetNumLobbyMembers(lobby);
            var maxPlayers = Steamworks.SteamMatchmaking.GetLobbyMemberLimit(lobby);
            var matchState = Steamworks.SteamMatchmaking.GetLobbyData(lobby, "match_state");
            if (string.IsNullOrWhiteSpace(matchState))
            {
                matchState = "open";
            }

            snapshot = new CurrentLobbySnapshot(CurrentLobbyId, CurrentRoomCode, currentPlayers, maxPlayers, matchState);
            return true;
#endif
        }

        public bool TryGetCurrentLobbyMembers(out List<LobbyMemberInfo> members, out string error)
        {
            members = new List<LobbyMemberInfo>();
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
            if (!SimLobbyRegistry.TryGetMembers(CurrentLobbyId, out var simMembers))
            {
                error = "Lobby no longer exists.";
                return false;
            }

            foreach (var member in simMembers)
            {
                members.Add(new LobbyMemberInfo(
                    member.SteamId,
                    member.DisplayName,
                    member.SteamId == _simLocalSteamId));
            }

            TraceLobbyMembers("sim", _simLocalSteamId, members);

            return true;
#else
            var lobby = new Steamworks.CSteamID(CurrentLobbyId);
            var count = Steamworks.SteamMatchmaking.GetNumLobbyMembers(lobby);
            var localId = Steamworks.SteamUser.GetSteamID().m_SteamID;
            var seen = new HashSet<ulong>();
            for (var i = 0; i < count; i++)
            {
                var memberId = Steamworks.SteamMatchmaking.GetLobbyMemberByIndex(lobby, i);
                if (memberId == Steamworks.CSteamID.Nil)
                {
                    continue;
                }

                if (!seen.Add(memberId.m_SteamID))
                {
                    continue;
                }

                var displayName = Steamworks.SteamFriends.GetFriendPersonaName(memberId);
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = memberId.m_SteamID.ToString();
                }

                members.Add(new LobbyMemberInfo(
                    memberId.m_SteamID,
                    displayName,
                    memberId.m_SteamID == localId));
            }

            TraceLobbyMembers("steam", localId, members);

            return true;
#endif
        }

        private void TraceLobbyMembers(string backend, ulong localId, List<LobbyMemberInfo> members)
        {
            // Lobby trace logs intentionally disabled to reduce runtime spam.
        }

        private bool EnsureReady(out string error)
        {
            error = string.Empty;
            if (_config == null)
            {
                error = "SteamLobbyService is not initialized with config.";
                return false;
            }

#if DISABLESTEAMWORKS
            return true;
#else
            if (_runtime == null || !_runtime.IsInitialized)
            {
                error = "Steam runtime is not initialized.";
                return false;
            }

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
            if (!SimLobbyRegistry.TrySetReady(CurrentLobbyId, _simLocalSteamId, ready, out error))
            {
                return false;
            }

            IsLocalReady = ready;
            IsLocalHost = SimLobbyRegistry.IsHost(CurrentLobbyId, _simLocalSteamId);
            return true;
#else
            error = "Ready-state API not implemented for Steam backend yet.";
            return false;
#endif
        }

        public bool LeaveLobby(out string error)
        {
            error = string.Empty;
            if (!IsInLobby)
            {
                return true;
            }

#if DISABLESTEAMWORKS
            SimLobbyRegistry.RemoveMember(CurrentLobbyId, _simLocalSteamId);
            CurrentLobbyId = 0;
            CurrentRoomCode = string.Empty;
            IsLocalReady = false;
            IsLocalHost = false;
            return true;
#else
            error = "LeaveLobby not implemented for Steam backend yet.";
            return false;
#endif
        }

        public bool TryGetLobbyReadyStates(out List<LobbyReadyStateInfo> readyStates, out string error)
        {
            readyStates = new List<LobbyReadyStateInfo>();
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
            if (!SimLobbyRegistry.TryGetMembers(CurrentLobbyId, out var simMembers))
            {
                error = "Lobby no longer exists.";
                return false;
            }

            foreach (var m in simMembers)
            {
                readyStates.Add(new LobbyReadyStateInfo(
                    m.SteamId,
                    m.DisplayName,
                    m.Ready,
                    SimLobbyRegistry.IsHost(CurrentLobbyId, m.SteamId),
                    m.SteamId == _simLocalSteamId));
            }

            return true;
#else
            error = "Ready-state API not implemented for Steam backend yet.";
            return false;
#endif
        }

        public bool TrySetActiveTurnIndex(int activeTurnIndex, out string error)
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
            return SimLobbyRegistry.TrySetActiveTurnIndex(CurrentLobbyId, activeTurnIndex, out error);
#else
            var lobby = new Steamworks.CSteamID(CurrentLobbyId);
            if (!Steamworks.SteamMatchmaking.SetLobbyData(lobby, "active_player_index", activeTurnIndex.ToString()))
            {
                error = "Failed to set active player index.";
                return false;
            }

            return true;
#endif
        }

        public bool TryGetActiveTurnIndex(out int activeTurnIndex, out string error)
        {
            activeTurnIndex = -1;
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
            return SimLobbyRegistry.TryGetActiveTurnIndex(CurrentLobbyId, out activeTurnIndex, out error);
#else
            var lobby = new Steamworks.CSteamID(CurrentLobbyId);
            var raw = Steamworks.SteamMatchmaking.GetLobbyData(lobby, "active_player_index");
            if (!int.TryParse(raw, out activeTurnIndex))
            {
                activeTurnIndex = -1;
            }

            return true;
#endif
        }

        public bool TrySetAuthoritativeSnapshot(string snapshotJson, out string error)
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
            return SimLobbyRegistry.TrySetAuthoritativeSnapshot(CurrentLobbyId, snapshotJson ?? string.Empty, out error);
#else
            var lobby = new Steamworks.CSteamID(CurrentLobbyId);
            if (!Steamworks.SteamMatchmaking.SetLobbyData(lobby, "authoritative_snapshot", snapshotJson ?? string.Empty))
            {
                error = "Failed to set authoritative snapshot.";
                return false;
            }

            return true;
#endif
        }

        public bool TryGetAuthoritativeSnapshot(out string snapshotJson, out string error)
        {
            snapshotJson = string.Empty;
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
            return SimLobbyRegistry.TryGetAuthoritativeSnapshot(CurrentLobbyId, out snapshotJson, out error);
#else
            var lobby = new Steamworks.CSteamID(CurrentLobbyId);
            snapshotJson = Steamworks.SteamMatchmaking.GetLobbyData(lobby, "authoritative_snapshot") ?? string.Empty;
            return true;
#endif
        }

        public bool TrySetTurnIntent(string intentJson, out string error)
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
            return SimLobbyRegistry.TrySetTurnIntent(CurrentLobbyId, intentJson ?? string.Empty, out error);
#else
            var lobby = new Steamworks.CSteamID(CurrentLobbyId);
            if (!Steamworks.SteamMatchmaking.SetLobbyData(lobby, "turn_intent", intentJson ?? string.Empty))
            {
                error = "Failed to set turn intent.";
                return false;
            }

            return true;
#endif
        }

        public bool TryGetTurnIntent(out string intentJson, out string error)
        {
            intentJson = string.Empty;
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
            return SimLobbyRegistry.TryGetTurnIntent(CurrentLobbyId, out intentJson, out error);
#else
            var lobby = new Steamworks.CSteamID(CurrentLobbyId);
            intentJson = Steamworks.SteamMatchmaking.GetLobbyData(lobby, "turn_intent") ?? string.Empty;
            return true;
#endif
        }

        public bool TryGetHostSteamId(out ulong hostSteamId, out string error)
        {
            hostSteamId = 0UL;
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
            if (SimLobbyRegistry.TryGetSnapshot(CurrentLobbyId, out var snapshot))
            {
                hostSteamId = snapshot.HostSteamId;
                return hostSteamId != 0UL;
            }

            error = "Lobby no longer exists.";
            return false;
#else
            var lobby = new Steamworks.CSteamID(CurrentLobbyId);
            var raw = Steamworks.SteamMatchmaking.GetLobbyData(lobby, "host_steam_id");
            if (!ulong.TryParse(raw, out hostSteamId) || hostSteamId == 0UL)
            {
                error = "Host steam id unavailable.";
                hostSteamId = 0UL;
                return false;
            }

            return true;
#endif
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
            var metadataOk = ApplyLobbyMetadata(lobby, "open");
            if (!metadataOk)
            {
                LobbyCreated?.Invoke(LobbyOperationResult.FromFailure("Lobby created but metadata write failed."));
                return;
            }

            LobbyCreated?.Invoke(LobbyOperationResult.FromSuccess(CurrentLobbyId, "Lobby created."));
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
            Steamworks.SteamMatchmaking.SetLobbyData(
                lobby,
                "current_players",
                Steamworks.SteamMatchmaking.GetNumLobbyMembers(lobby).ToString());

            LobbyJoined?.Invoke(LobbyOperationResult.FromSuccess(CurrentLobbyId, "Lobby joined."));
        }

        private bool ApplyLobbyMetadata(Steamworks.CSteamID lobbyId, string matchState)
        {
            var hostSteamId = Steamworks.SteamUser.GetSteamID().m_SteamID.ToString();
            var currentPlayers = Steamworks.SteamMatchmaking.GetNumLobbyMembers(lobbyId).ToString();

            var ok = true;
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "build_version", _config.BuildVersion);
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "rules_profile_id", _config.RulesProfileId);
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "map_id", _config.MapId);
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "match_state", matchState);
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "host_steam_id", hostSteamId);
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "max_players", _config.MaxPlayers.ToString());
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "current_players", currentPlayers);
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "contract_version", _config.RuntimeContractVersion);
            ok &= Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "room_code", CurrentRoomCode);
            return ok;
        }
#endif

        private static string BuildRoomCode(ulong lobbyId)
        {
            return lobbyId.ToString("X").ToUpperInvariant();
        }

        private void EnsureSimLocalIdentity()
        {
            if (_simLocalSteamId != 0)
            {
                return;
            }

            var guidBytes = Guid.NewGuid().ToByteArray();
            _simLocalSteamId = BitConverter.ToUInt64(guidBytes, 0);
            if (_simLocalSteamId == 0)
            {
                _simLocalSteamId = 11000000000000000UL + (ulong)Mathf.Abs(Environment.TickCount);
            }

            var tail = (_simLocalSteamId % 1000UL).ToString("000");
            _simLocalDisplayName = $"EditorPlayer{tail}";
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

#if DISABLESTEAMWORKS
        private static class SimLobbyRegistry
        {
            private static readonly Mutex FileMutex = new(false, @"Global\Risiko3D_SimLobbyRegistry");
            private static readonly string StorePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Risiko3D",
                "sim_lobbies_v1.txt");

            public static ulong CreateLobby(int maxPlayers, ulong hostSteamId, string hostDisplayName)
            {
                var acquired = false;
                try
                {
                    acquired = FileMutex.WaitOne(TimeSpan.FromSeconds(2));
                    EnsureStoreDirectory();
                    var lobbies = LoadLobbies(StorePath);
                    var lobbyId = 10000000000000000UL + (ulong)Mathf.Abs(Environment.TickCount) + (ulong)lobbies.Count + 1UL;
                    while (lobbies.ContainsKey(lobbyId))
                    {
                        lobbyId += 1UL;
                    }

                    var lobby = new SimLobbyState
                    {
                        LobbyId = lobbyId,
                        RoomCode = BuildRoomCode(lobbyId),
                        MaxPlayers = Mathf.Clamp(maxPlayers, 2, 6),
                        HostSteamId = hostSteamId,
                        MatchStarted = false,
                        ActivePlayerIndex = 0
                    };
                    lobby.Members[hostSteamId] = new SimLobbyMember(hostSteamId, hostDisplayName, false);
                    lobbies[lobbyId] = lobby;
                    SaveLobbies(StorePath, lobbies);
                    return lobbyId;
                }
                finally
                {
                    if (acquired)
                    {
                        FileMutex.ReleaseMutex();
                    }
                }
            }

            public static bool TryJoinLobby(ulong lobbyId, ulong steamId, string displayName, out string error)
            {
                error = string.Empty;
                var acquired = false;
                try
                {
                    acquired = FileMutex.WaitOne(TimeSpan.FromSeconds(2));
                    EnsureStoreDirectory();
                    var lobbies = LoadLobbies(StorePath);
                    if (!lobbies.TryGetValue(lobbyId, out var lobby))
                    {
                        error = "Lobby not found.";
                        return false;
                    }

                    if (lobby.MatchStarted)
                    {
                        error = "Lobby already in match.";
                        return false;
                    }

                    if (lobby.Members.ContainsKey(steamId))
                    {
                        return true;
                    }

                    if (lobby.Members.Count >= lobby.MaxPlayers)
                    {
                        error = "Lobby is full.";
                        return false;
                    }

                    lobby.Members[steamId] = new SimLobbyMember(steamId, displayName, false);
                    SaveLobbies(StorePath, lobbies);
                    return true;
                }
                finally
                {
                    if (acquired)
                    {
                        FileMutex.ReleaseMutex();
                    }
                }
            }

            public static void RemoveMember(ulong lobbyId, ulong steamId)
            {
                var acquired = false;
                try
                {
                    acquired = FileMutex.WaitOne(TimeSpan.FromSeconds(2));
                    EnsureStoreDirectory();
                    var lobbies = LoadLobbies(StorePath);
                    if (!lobbies.TryGetValue(lobbyId, out var lobby))
                    {
                        return;
                    }

                    lobby.Members.Remove(steamId);
                    if (lobby.Members.Count == 0)
                    {
                        lobbies.Remove(lobbyId);
                        SaveLobbies(StorePath, lobbies);
                        return;
                    }

                    if (lobby.HostSteamId == steamId)
                    {
                        lobby.HostSteamId = lobby.Members.Keys.OrderBy(value => value).First();
                    }
                    SaveLobbies(StorePath, lobbies);
                }
                finally
                {
                    if (acquired)
                    {
                        FileMutex.ReleaseMutex();
                    }
                }
            }

            public static bool TrySetReady(ulong lobbyId, ulong steamId, bool ready, out string error)
            {
                error = string.Empty;
                var acquired = false;
                try
                {
                    acquired = FileMutex.WaitOne(TimeSpan.FromSeconds(2));
                    EnsureStoreDirectory();
                    var lobbies = LoadLobbies(StorePath);
                    if (!lobbies.TryGetValue(lobbyId, out var lobby))
                    {
                        error = "Lobby not found.";
                        return false;
                    }

                    if (!lobby.Members.TryGetValue(steamId, out var member))
                    {
                        error = "Member not in lobby.";
                        return false;
                    }

                    member.Ready = ready;
                    lobby.Members[steamId] = member;
                    SaveLobbies(StorePath, lobbies);
                    return true;
                }
                finally
                {
                    if (acquired)
                    {
                        FileMutex.ReleaseMutex();
                    }
                }
            }

            public static bool CanStartMatch(ulong lobbyId, ulong steamId, out string error)
            {
                error = string.Empty;
                var acquired = false;
                try
                {
                    acquired = FileMutex.WaitOne(TimeSpan.FromSeconds(2));
                    EnsureStoreDirectory();
                    var lobbies = LoadLobbies(StorePath);
                    if (!lobbies.TryGetValue(lobbyId, out var lobby))
                    {
                        error = "Lobby not found.";
                        return false;
                    }

                    if (lobby.HostSteamId != steamId)
                    {
                        error = "Only host can start.";
                        return false;
                    }

                    if (lobby.Members.Count < 2)
                    {
                        error = "Need at least 2 players.";
                        return false;
                    }

                    foreach (var member in lobby.Members.Values)
                    {
                        if (!member.Ready)
                        {
                            error = "All players must be ready.";
                            return false;
                        }
                    }

                    return true;
                }
                finally
                {
                    if (acquired)
                    {
                        FileMutex.ReleaseMutex();
                    }
                }
            }

            public static void MarkMatchStarted(ulong lobbyId)
            {
                var acquired = false;
                try
                {
                    acquired = FileMutex.WaitOne(TimeSpan.FromSeconds(2));
                    EnsureStoreDirectory();
                    var lobbies = LoadLobbies(StorePath);
                    if (lobbies.TryGetValue(lobbyId, out var lobby))
                    {
                        lobby.MatchStarted = true;
                        SaveLobbies(StorePath, lobbies);
                    }
                }
                finally
                {
                    if (acquired)
                    {
                        FileMutex.ReleaseMutex();
                    }
                }
            }

            public static bool TryGetSnapshot(ulong lobbyId, out SimLobbySnapshot snapshot)
            {
                var acquired = false;
                snapshot = default;
                try
                {
                    acquired = FileMutex.WaitOne(TimeSpan.FromSeconds(2));
                    EnsureStoreDirectory();
                    var lobbies = LoadLobbies(StorePath);
                    if (!lobbies.TryGetValue(lobbyId, out var lobby))
                    {
                        return false;
                    }

                    snapshot = new SimLobbySnapshot(
                        lobby.LobbyId,
                        lobby.RoomCode,
                        lobby.Members.Count,
                        lobby.MaxPlayers,
                        lobby.HostSteamId,
                        lobby.MatchStarted,
                        lobby.Members);
                    return true;
                }
                finally
                {
                    if (acquired)
                    {
                        FileMutex.ReleaseMutex();
                    }
                }
            }

            public static bool TryGetMembers(ulong lobbyId, out List<SimLobbyMember> members)
            {
                var acquired = false;
                members = new List<SimLobbyMember>();
                try
                {
                    acquired = FileMutex.WaitOne(TimeSpan.FromSeconds(2));
                    EnsureStoreDirectory();
                    var lobbies = LoadLobbies(StorePath);
                    if (!lobbies.TryGetValue(lobbyId, out var lobby))
                    {
                        return false;
                    }

                    foreach (var member in lobby.Members.Values.OrderByDescending(m => m.SteamId == lobby.HostSteamId).ThenBy(m => m.SteamId))
                    {
                        members.Add(member);
                    }

                    return true;
                }
                finally
                {
                    if (acquired)
                    {
                        FileMutex.ReleaseMutex();
                    }
                }
            }

            public static IEnumerable<SimJoinableLobbyInfo> ListJoinableLobbies(ulong localSteamId)
            {
                var acquired = false;
                try
                {
                    acquired = FileMutex.WaitOne(TimeSpan.FromSeconds(2));
                    EnsureStoreDirectory();
                    var lobbies = LoadLobbies(StorePath);
                    var rows = new List<SimJoinableLobbyInfo>();
                    foreach (var lobby in lobbies.Values)
                    {
                        if (lobby.Members.ContainsKey(localSteamId))
                        {
                            continue;
                        }

                        if (lobby.MatchStarted || lobby.Members.Count >= lobby.MaxPlayers)
                        {
                            continue;
                        }

                        var hostName = lobby.Members.TryGetValue(lobby.HostSteamId, out var host)
                            ? host.DisplayName
                            : "EditorHost";
                        rows.Add(new SimJoinableLobbyInfo(lobby.LobbyId, lobby.RoomCode, lobby.HostSteamId, hostName));
                    }

                    return rows;
                }
                finally
                {
                    if (acquired)
                    {
                        FileMutex.ReleaseMutex();
                    }
                }
            }

            public static bool IsHost(ulong lobbyId, ulong steamId)
            {
                var acquired = false;
                try
                {
                    acquired = FileMutex.WaitOne(TimeSpan.FromSeconds(2));
                    EnsureStoreDirectory();
                    var lobbies = LoadLobbies(StorePath);
                    return lobbies.TryGetValue(lobbyId, out var lobby) && lobby.HostSteamId == steamId;
                }
                finally
                {
                    if (acquired)
                    {
                        FileMutex.ReleaseMutex();
                    }
                }
            }

            public static bool TrySetActiveTurnIndex(ulong lobbyId, int activeTurnIndex, out string error)
            {
                error = string.Empty;
                var acquired = false;
                try
                {
                    acquired = FileMutex.WaitOne(TimeSpan.FromSeconds(2));
                    EnsureStoreDirectory();
                    var lobbies = LoadLobbies(StorePath);
                    if (!lobbies.TryGetValue(lobbyId, out var lobby))
                    {
                        error = "Lobby not found.";
                        return false;
                    }

                    lobby.ActivePlayerIndex = Mathf.Max(-1, activeTurnIndex);
                    SaveLobbies(StorePath, lobbies);
                    return true;
                }
                finally
                {
                    if (acquired)
                    {
                        FileMutex.ReleaseMutex();
                    }
                }
            }

            public static bool TryGetActiveTurnIndex(ulong lobbyId, out int activeTurnIndex, out string error)
            {
                activeTurnIndex = -1;
                error = string.Empty;
                var acquired = false;
                try
                {
                    acquired = FileMutex.WaitOne(TimeSpan.FromSeconds(2));
                    EnsureStoreDirectory();
                    var lobbies = LoadLobbies(StorePath);
                    if (!lobbies.TryGetValue(lobbyId, out var lobby))
                    {
                        error = "Lobby not found.";
                        return false;
                    }

                    activeTurnIndex = lobby.ActivePlayerIndex;
                    return true;
                }
                finally
                {
                    if (acquired)
                    {
                        FileMutex.ReleaseMutex();
                    }
                }
            }

            public static bool TrySetAuthoritativeSnapshot(ulong lobbyId, string snapshotJson, out string error)
            {
                error = string.Empty;
                var acquired = false;
                try
                {
                    acquired = FileMutex.WaitOne(TimeSpan.FromSeconds(2));
                    EnsureStoreDirectory();
                    var lobbies = LoadLobbies(StorePath);
                    if (!lobbies.TryGetValue(lobbyId, out var lobby))
                    {
                        error = "Lobby not found.";
                        return false;
                    }

                    lobby.SnapshotJson = snapshotJson ?? string.Empty;
                    SaveLobbies(StorePath, lobbies);
                    return true;
                }
                finally
                {
                    if (acquired)
                    {
                        FileMutex.ReleaseMutex();
                    }
                }
            }

            public static bool TryGetAuthoritativeSnapshot(ulong lobbyId, out string snapshotJson, out string error)
            {
                snapshotJson = string.Empty;
                error = string.Empty;
                var acquired = false;
                try
                {
                    acquired = FileMutex.WaitOne(TimeSpan.FromSeconds(2));
                    EnsureStoreDirectory();
                    var lobbies = LoadLobbies(StorePath);
                    if (!lobbies.TryGetValue(lobbyId, out var lobby))
                    {
                        error = "Lobby not found.";
                        return false;
                    }

                    snapshotJson = lobby.SnapshotJson ?? string.Empty;
                    return true;
                }
                finally
                {
                    if (acquired)
                    {
                        FileMutex.ReleaseMutex();
                    }
                }
            }

            public static bool TrySetTurnIntent(ulong lobbyId, string intentJson, out string error)
            {
                error = string.Empty;
                var acquired = false;
                try
                {
                    acquired = FileMutex.WaitOne(TimeSpan.FromSeconds(2));
                    EnsureStoreDirectory();
                    var lobbies = LoadLobbies(StorePath);
                    if (!lobbies.TryGetValue(lobbyId, out var lobby))
                    {
                        error = "Lobby not found.";
                        return false;
                    }

                    lobby.TurnIntentJson = intentJson ?? string.Empty;
                    SaveLobbies(StorePath, lobbies);
                    return true;
                }
                finally
                {
                    if (acquired)
                    {
                        FileMutex.ReleaseMutex();
                    }
                }
            }

            public static bool TryGetTurnIntent(ulong lobbyId, out string intentJson, out string error)
            {
                intentJson = string.Empty;
                error = string.Empty;
                var acquired = false;
                try
                {
                    acquired = FileMutex.WaitOne(TimeSpan.FromSeconds(2));
                    EnsureStoreDirectory();
                    var lobbies = LoadLobbies(StorePath);
                    if (!lobbies.TryGetValue(lobbyId, out var lobby))
                    {
                        error = "Lobby not found.";
                        return false;
                    }

                    intentJson = lobby.TurnIntentJson ?? string.Empty;
                    return true;
                }
                finally
                {
                    if (acquired)
                    {
                        FileMutex.ReleaseMutex();
                    }
                }
            }

            private static void EnsureStoreDirectory()
            {
                var directory = Path.GetDirectoryName(StorePath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            private static Dictionary<ulong, SimLobbyState> LoadLobbies(string path)
            {
                var result = new Dictionary<ulong, SimLobbyState>();
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return result;
                }

                SimLobbyState currentLobby = null;
                foreach (var raw in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        continue;
                    }

                    var parts = raw.Split('|');
                    if (parts.Length == 0)
                    {
                        continue;
                    }

                    if (parts[0] == "L" && parts.Length >= 6)
                    {
                        if (!ulong.TryParse(parts[1], out var lobbyId))
                        {
                            currentLobby = null;
                            continue;
                        }

                        _ = int.TryParse(parts[3], out var maxPlayers);
                        _ = ulong.TryParse(parts[4], out var hostSteamId);
                        var matchStarted = parts[5] == "1";
                        var activePlayerIndex = -1;
                        if (parts.Length >= 7)
                        {
                            _ = int.TryParse(parts[6], out activePlayerIndex);
                        }
                        currentLobby = new SimLobbyState
                        {
                            LobbyId = lobbyId,
                            RoomCode = parts[2],
                            MaxPlayers = Mathf.Clamp(maxPlayers, 2, 6),
                            HostSteamId = hostSteamId,
                            MatchStarted = matchStarted,
                            ActivePlayerIndex = activePlayerIndex,
                            Members = new Dictionary<ulong, SimLobbyMember>()
                        };
                        result[lobbyId] = currentLobby;
                        continue;
                    }

                    if (parts[0] == "M" && parts.Length >= 5 && currentLobby != null)
                    {
                        if (!ulong.TryParse(parts[1], out var lobbyId) ||
                            !ulong.TryParse(parts[2], out var steamId))
                        {
                            continue;
                        }

                        if (!result.TryGetValue(lobbyId, out var lobby))
                        {
                            continue;
                        }

                        var ready = parts[3] == "1";
                        var name = Uri.UnescapeDataString(parts[4]);
                        lobby.Members[steamId] = new SimLobbyMember(steamId, name, ready);
                        continue;
                    }

                    if (parts[0] == "S" && parts.Length >= 3 && currentLobby != null)
                    {
                        if (!ulong.TryParse(parts[1], out var lobbyId))
                        {
                            continue;
                        }

                        if (!result.TryGetValue(lobbyId, out var lobby))
                        {
                            continue;
                        }

                        lobby.SnapshotJson = Uri.UnescapeDataString(parts[2]);
                        continue;
                    }

                    if (parts[0] == "T" && parts.Length >= 3 && currentLobby != null)
                    {
                        if (!ulong.TryParse(parts[1], out var lobbyId))
                        {
                            continue;
                        }

                        if (!result.TryGetValue(lobbyId, out var lobby))
                        {
                            continue;
                        }

                        lobby.TurnIntentJson = Uri.UnescapeDataString(parts[2]);
                    }
                }

                return result;
            }

            private static void SaveLobbies(string path, Dictionary<ulong, SimLobbyState> lobbies)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }

                var lines = new List<string>();
                foreach (var lobby in lobbies.Values.OrderBy(value => value.LobbyId))
                {
                    lines.Add($"L|{lobby.LobbyId}|{lobby.RoomCode}|{lobby.MaxPlayers}|{lobby.HostSteamId}|{(lobby.MatchStarted ? "1" : "0")}|{lobby.ActivePlayerIndex}");
                    foreach (var member in lobby.Members.Values.OrderBy(value => value.SteamId))
                    {
                        lines.Add($"M|{lobby.LobbyId}|{member.SteamId}|{(member.Ready ? "1" : "0")}|{Uri.EscapeDataString(member.DisplayName ?? string.Empty)}");
                    }

                    lines.Add($"S|{lobby.LobbyId}|{Uri.EscapeDataString(lobby.SnapshotJson ?? string.Empty)}");
                    lines.Add($"T|{lobby.LobbyId}|{Uri.EscapeDataString(lobby.TurnIntentJson ?? string.Empty)}");
                }

                File.WriteAllLines(path, lines);
            }
        }

        private sealed class SimLobbyState
        {
            public ulong LobbyId;
            public string RoomCode = string.Empty;
            public int MaxPlayers;
            public ulong HostSteamId;
            public bool MatchStarted;
            public int ActivePlayerIndex = -1;
            public string SnapshotJson = string.Empty;
            public string TurnIntentJson = string.Empty;
            public Dictionary<ulong, SimLobbyMember> Members = new();
        }

        private readonly struct SimJoinableLobbyInfo
        {
            public SimJoinableLobbyInfo(ulong lobbyId, string roomCode, ulong hostSteamId, string hostDisplayName)
            {
                LobbyId = lobbyId;
                RoomCode = roomCode;
                HostSteamId = hostSteamId;
                HostDisplayName = hostDisplayName;
            }

            public ulong LobbyId { get; }
            public string RoomCode { get; }
            public ulong HostSteamId { get; }
            public string HostDisplayName { get; }
        }

        private readonly struct SimLobbySnapshot
        {
            private readonly Dictionary<ulong, SimLobbyMember> _members;

            public SimLobbySnapshot(ulong lobbyId, string roomCode, int currentPlayers, int maxPlayers, ulong hostSteamId, bool matchStarted, Dictionary<ulong, SimLobbyMember> members)
            {
                LobbyId = lobbyId;
                RoomCode = roomCode;
                CurrentPlayers = currentPlayers;
                MaxPlayers = maxPlayers;
                HostSteamId = hostSteamId;
                MatchStarted = matchStarted;
                _members = new Dictionary<ulong, SimLobbyMember>(members);
            }

            public ulong LobbyId { get; }
            public string RoomCode { get; }
            public int CurrentPlayers { get; }
            public int MaxPlayers { get; }
            public ulong HostSteamId { get; }
            public bool MatchStarted { get; }

            public bool TryGetMember(ulong steamId, out SimLobbyMember member)
            {
                return _members.TryGetValue(steamId, out member);
            }
        }

        private struct SimLobbyMember
        {
            public SimLobbyMember(ulong steamId, string displayName, bool ready)
            {
                SteamId = steamId;
                DisplayName = displayName;
                Ready = ready;
            }

            public ulong SteamId;
            public string DisplayName;
            public bool Ready;
        }
#endif
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

    public readonly struct CurrentLobbySnapshot
    {
        public ulong LobbyId { get; }
        public string RoomCode { get; }
        public int CurrentPlayers { get; }
        public int MaxPlayers { get; }
        public string MatchState { get; }

        public CurrentLobbySnapshot(ulong lobbyId, string roomCode, int currentPlayers, int maxPlayers, string matchState)
        {
            LobbyId = lobbyId;
            RoomCode = roomCode ?? string.Empty;
            CurrentPlayers = currentPlayers;
            MaxPlayers = maxPlayers;
            MatchState = matchState ?? string.Empty;
        }
    }

    public readonly struct LobbyMemberInfo
    {
        public ulong SteamId { get; }
        public string DisplayName { get; }
        public bool IsLocal { get; }

        public LobbyMemberInfo(ulong steamId, string displayName, bool isLocal)
        {
            SteamId = steamId;
            DisplayName = displayName ?? string.Empty;
            IsLocal = isLocal;
        }
    }

    public readonly struct LobbyReadyStateInfo
    {
        public ulong SteamId { get; }
        public string DisplayName { get; }
        public bool IsReady { get; }
        public bool IsHost { get; }
        public bool IsLocal { get; }

        public LobbyReadyStateInfo(ulong steamId, string displayName, bool isReady, bool isHost, bool isLocal)
        {
            SteamId = steamId;
            DisplayName = displayName ?? string.Empty;
            IsReady = isReady;
            IsHost = isHost;
            IsLocal = isLocal;
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
