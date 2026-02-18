using System;
using System.Collections.Generic;
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

        public ulong CurrentLobbyId { get; private set; }
        public string CurrentRoomCode { get; private set; } = string.Empty;
        public bool IsInLobby => CurrentLobbyId != 0;

        private GameRuntimeConfig _config;
        private SteamRuntime _runtime;

#if !DISABLESTEAMWORKS
        private Steamworks.CallResult<Steamworks.LobbyCreated_t> _lobbyCreatedCallResult;
        private Steamworks.CallResult<Steamworks.LobbyEnter_t> _lobbyEnterCallResult;
#endif

        public void Initialize(GameRuntimeConfig config, SteamRuntime runtime)
        {
            _config = config;
            _runtime = runtime;

#if !DISABLESTEAMWORKS
            _lobbyCreatedCallResult ??= Steamworks.CallResult<Steamworks.LobbyCreated_t>.Create(OnLobbyCreatedInternal);
            _lobbyEnterCallResult ??= Steamworks.CallResult<Steamworks.LobbyEnter_t>.Create(OnLobbyEnteredInternal);
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
            error = "Steamworks is disabled.";
            return false;
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
            error = "Steamworks is disabled.";
            return false;
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
            error = "Steamworks is disabled.";
            return false;
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
            error = "Steamworks is disabled.";
            return false;
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
            error = "Steamworks is disabled.";
            return false;
#else
            Steamworks.SteamFriends.ActivateGameOverlayInviteDialog(new Steamworks.CSteamID(CurrentLobbyId));
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
