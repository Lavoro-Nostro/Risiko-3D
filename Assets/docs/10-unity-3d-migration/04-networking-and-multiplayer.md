# Networking And Multiplayer

## Authority Model

- Host-authoritative, turn-based command processing.
- Clients send intents only.
- Host broadcasts accepted ordered events via Steam transport.

## Session Model

- Lobby lifecycle: create, join, leave, start.
- Match lifecycle: initialize, command loop, end.
- Reconnect lifecycle: Steam session rejoin + last known sequence.

## Steam Transport Strategy

- Use Steam Lobby APIs for discovery/invites/presence.
- Use Steam Networking Sockets (reliable + ordered channels) for match traffic.
- Prefer Steam relay path by default to avoid manual router port forwarding.
- Keep message schema versioned and deterministic.

## Required Message Contracts

- `LobbyCreateRequest`
- `LobbyJoinRequest`
- `LobbyMetadataSync` (map, mode, version, host id)
- `MatchStartRequest`
- `GameplayCommandEnvelope` (command id, player id, payload, sequence hint)
- `AuthoritativeEventEnvelope` (sequence, event kind, payload, checksum)
- `StateSnapshotEnvelope` (full snapshot, sequence, seed metadata)
- `ReconnectRequest` (last sequence, player token)
- `ReconnectResponse` (snapshot + missed events)

## Reconnect Rules

- Client stores `lobbyId`, `matchId`, `steamId`, reconnect token.
- On reconnect, request snapshot + missed events from last sequence through host peer.
- Apply events in order, idempotently.

## Anti-Cheat Baseline

- Reject commands outside active phase/turn.
- Reject invalid ownership/adjacency/dice constraints server-side.
- Never trust client-reported state.

## Failure Handling

- Exponential backoff on Steam socket reconnect.
- Explicit user-facing state: reconnecting, desynced, recovered.
- Timeout rules for host disconnect and lobby closure.
- Define host handoff policy:
  - MVP: lobby closes if host leaves
  - Post-MVP option: deterministic host migration with state handover
