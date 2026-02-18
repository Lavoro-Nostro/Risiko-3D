# Steam Message Schema (Authoritative Protocol)

## Purpose

Define concrete packet formats for Steam lobby + gameplay networking between Unity peers.

Protocol ID: `risiko.steam.protocol.v1`

## Transport Rules

- Transport: Steam Networking Sockets.
- Encoding: UTF-8 JSON for MVP.
- Reliability:
  - gameplay commands/events: reliable ordered
  - snapshots/reconnect: reliable
  - chat/presence: reliable (can be split later)
- Every packet includes protocol version and match version.

## Common Envelope

```json
{
  "protocolVersion": "1.0.0",
  "messageType": "GameplayCommandEnvelope",
  "timestampUtc": "2026-02-18T15:20:30Z",
  "lobbyId": "109775241234567890",
  "matchId": "match-4a3f2d1c",
  "senderSteamId": "76561198000000001",
  "payload": {}
}
```

## Lobby Messages

## `LobbyCreateRequest`

```json
{
  "protocolVersion": "1.0.0",
  "messageType": "LobbyCreateRequest",
  "payload": {
    "hostSteamId": "76561198000000001",
    "mapId": "world-classic",
    "rulesProfileId": "RisiKo!_OBJECTIVE_CLASSICO_IT_V1",
    "maxPlayers": 6,
    "buildVersion": "0.1.0"
  }
}
```

## `LobbyJoinRequest`

```json
{
  "protocolVersion": "1.0.0",
  "messageType": "LobbyJoinRequest",
  "payload": {
    "lobbyId": "109775241234567890",
    "peerSteamId": "76561198000000002",
    "displayName": "PlayerTwo",
    "buildVersion": "0.1.0"
  }
}
```

## `LobbyMetadataSync`

```json
{
  "protocolVersion": "1.0.0",
  "messageType": "LobbyMetadataSync",
  "payload": {
    "lobbyId": "109775241234567890",
    "hostSteamId": "76561198000000001",
    "mapId": "world-classic",
    "rulesProfileId": "RisiKo!_OBJECTIVE_CLASSICO_IT_V1",
    "matchState": "open",
    "currentPlayers": 3,
    "maxPlayers": 6,
    "buildVersion": "0.1.0",
    "contractVersion": "1.0.0"
  }
}
```

## Match Messages

## `MatchStartRequest`

```json
{
  "protocolVersion": "1.0.0",
  "messageType": "MatchStartRequest",
  "payload": {
    "lobbyId": "109775241234567890",
    "requestedBySteamId": "76561198000000001",
    "mapId": "world-classic",
    "rulesProfileId": "RisiKo!_OBJECTIVE_CLASSICO_IT_V1",
    "seedMode": "random_host_generated"
  }
}
```

## `GameplayCommandEnvelope`

```json
{
  "protocolVersion": "1.0.0",
  "messageType": "GameplayCommandEnvelope",
  "payload": {
    "matchId": "match-4a3f2d1c",
    "commandId": "cmd-00000125",
    "playerId": "76561198000000002",
    "expectedSequence": 241,
    "commandType": "AttackCommand",
    "command": {
      "sourceTerritoryId": "alberta",
      "targetTerritoryId": "ontario",
      "attackerDice": 3
    }
  }
}
```

Validation rules:

- `commandId` must be globally unique per match.
- `playerId` must match active player for phase.
- `expectedSequence` mismatch is not fatal but used for desync detection.

## `AuthoritativeEventEnvelope`

```json
{
  "protocolVersion": "1.0.0",
  "messageType": "AuthoritativeEventEnvelope",
  "payload": {
    "matchId": "match-4a3f2d1c",
    "sequence": 242,
    "commandId": "cmd-00000125",
    "eventType": "AttackResolved",
    "event": {
      "attackerDice": [6, 4, 2],
      "defenderDice": [5, 3],
      "attackerLosses": 0,
      "defenderLosses": 2,
      "sourceTerritoryId": "alberta",
      "targetTerritoryId": "ontario"
    },
    "stateChecksum": "sha256:38f7...",
    "rngCounter": 558
  }
}
```

Rules:

- `sequence` strictly increments by 1.
- clients apply only next expected sequence.
- out-of-order events are buffered.

## `StateSnapshotEnvelope`

```json
{
  "protocolVersion": "1.0.0",
  "messageType": "StateSnapshotEnvelope",
  "payload": {
    "matchId": "match-4a3f2d1c",
    "sequence": 242,
    "seed": 17823741,
    "activePlayerId": "76561198000000003",
    "phase": "Attack",
    "territories": [
      { "territoryId": "alberta", "ownerPlayerId": "76561198000000002", "armies": 8 },
      { "territoryId": "ontario", "ownerPlayerId": "76561198000000002", "armies": 3 }
    ],
    "playerHands": [
      { "playerId": "76561198000000002", "cardCount": 4 }
    ],
    "stateChecksum": "sha256:38f7..."
  }
}
```

## Reconnect Messages

## `ReconnectRequest`

```json
{
  "protocolVersion": "1.0.0",
  "messageType": "ReconnectRequest",
  "payload": {
    "matchId": "match-4a3f2d1c",
    "playerId": "76561198000000002",
    "reconnectToken": "c1f9686c40f2460ea5d7f10826f2f249",
    "lastAppliedSequence": 229
  }
}
```

## `ReconnectResponse`

```json
{
  "protocolVersion": "1.0.0",
  "messageType": "ReconnectResponse",
  "payload": {
    "matchId": "match-4a3f2d1c",
    "snapshot": { "sequence": 242, "stateChecksum": "sha256:38f7..." },
    "missedEvents": [
      { "sequence": 230, "eventType": "ReinforcementsPlaced" },
      { "sequence": 231, "eventType": "AttackResolved" }
    ]
  }
}
```

## Error Envelope

```json
{
  "protocolVersion": "1.0.0",
  "messageType": "ErrorEnvelope",
  "payload": {
    "matchId": "match-4a3f2d1c",
    "commandId": "cmd-00000125",
    "code": "invalid_phase",
    "message": "Command not valid in current phase.",
    "currentSequence": 242,
    "recoverable": true
  }
}
```

## Compatibility Rules

- `major` mismatch => block lobby join.
- `minor` mismatch => allow only if host marks backward-compatible.
- `patch` mismatch => allow.

## Required Telemetry Fields

Each received/sent packet must log:

- `matchId`
- `sequence` (if present)
- `commandId` (if present)
- `senderSteamId`
- `messageType`
- `latencyMs` (computed where possible)
