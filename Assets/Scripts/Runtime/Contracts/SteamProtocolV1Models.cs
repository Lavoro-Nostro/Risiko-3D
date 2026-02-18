using System;

namespace Risiko3D.Runtime.Contracts
{
    public static class SteamProtocolV1
    {
        public const string ProtocolId = "risiko.steam.protocol.v1";
        public const string ProtocolVersion = "1.0.0";
    }

    [Serializable]
    public sealed class MessageEnvelope<TPayload>
    {
        public string ProtocolVersion = SteamProtocolV1.ProtocolVersion;
        public string MessageType = string.Empty;
        public string TimestampUtc = string.Empty;
        public string LobbyId = string.Empty;
        public string MatchId = string.Empty;
        public string SenderSteamId = string.Empty;
        public TPayload Payload;
    }

    [Serializable]
    public sealed class LobbyCreateRequest
    {
        public string HostSteamId = string.Empty;
        public string MapId = "world-classic";
        public string RulesProfileId = "RisiKo!_OBJECTIVE_CLASSICO_IT_V1";
        public int MaxPlayers = 6;
        public string BuildVersion = "0.1.0";
    }

    [Serializable]
    public sealed class LobbyJoinRequest
    {
        public string LobbyId = string.Empty;
        public string PeerSteamId = string.Empty;
        public string DisplayName = string.Empty;
        public string BuildVersion = "0.1.0";
    }

    [Serializable]
    public sealed class LobbyMetadataSync
    {
        public string LobbyId = string.Empty;
        public string HostSteamId = string.Empty;
        public string MapId = "world-classic";
        public string RulesProfileId = "RisiKo!_OBJECTIVE_CLASSICO_IT_V1";
        public string MatchState = "open";
        public int CurrentPlayers = 0;
        public int MaxPlayers = 6;
        public string BuildVersion = "0.1.0";
        public string ContractVersion = "1.0.0";
    }

    [Serializable]
    public sealed class MatchStartRequest
    {
        public string LobbyId = string.Empty;
        public string RequestedBySteamId = string.Empty;
        public string MapId = "world-classic";
        public string RulesProfileId = "RisiKo!_OBJECTIVE_CLASSICO_IT_V1";
        public string SeedMode = "random_host_generated";
    }

    [Serializable]
    public sealed class GameplayCommandEnvelope
    {
        public string MatchId = string.Empty;
        public string CommandId = string.Empty;
        public string PlayerId = string.Empty;
        public int ExpectedSequence = 0;
        public string CommandType = string.Empty;
        public string CommandJson = "{}";
    }

    [Serializable]
    public sealed class AuthoritativeEventEnvelope
    {
        public string MatchId = string.Empty;
        public int Sequence = 0;
        public string CommandId = string.Empty;
        public string EventType = string.Empty;
        public string EventJson = "{}";
        public string StateChecksum = string.Empty;
        public int RngCounter = 0;
    }

    [Serializable]
    public sealed class StateSnapshotEnvelope
    {
        public string MatchId = string.Empty;
        public int Sequence = 0;
        public int Seed = 0;
        public string ActivePlayerId = string.Empty;
        public string Phase = string.Empty;
        public string SnapshotJson = "{}";
        public string StateChecksum = string.Empty;
    }

    [Serializable]
    public sealed class ReconnectRequest
    {
        public string MatchId = string.Empty;
        public string PlayerId = string.Empty;
        public string ReconnectToken = string.Empty;
        public int LastAppliedSequence = 0;
    }

    [Serializable]
    public sealed class ReconnectResponse
    {
        public string MatchId = string.Empty;
        public string SnapshotJson = "{}";
        public string MissedEventsJson = "[]";
    }

    [Serializable]
    public sealed class ErrorEnvelope
    {
        public string MatchId = string.Empty;
        public string CommandId = string.Empty;
        public string Code = string.Empty;
        public string Message = string.Empty;
        public int CurrentSequence = 0;
        public bool Recoverable = true;
    }
}

