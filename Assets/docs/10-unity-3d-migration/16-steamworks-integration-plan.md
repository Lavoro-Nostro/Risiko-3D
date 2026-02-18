# Steamworks Integration Plan

## Objective

Ship as a native Windows Unity EXE on Steam with:

- Steam login identity
- Steam lobby matchmaking
- Steam friend invites and join
- Steam networking transport (P2P + relay)
- Steam branch/depot release workflow

## SDK Scope

- Steamworks.NET (or Facepunch.Steamworks; choose one and standardize)
- APIs required:
  - `ISteamUser` (identity/auth)
  - `ISteamFriends` (presence/invites)
  - `ISteamMatchmaking` (lobbies)
  - `ISteamNetworkingSockets` (reliable messages)
  - `ISteamUtils` (overlay, callbacks)

## Lobby Data Contract

Mandatory metadata:

- `build_version`
- `rules_profile_id`
- `map_id`
- `match_state` (`open|in_match|closed`)
- `host_steam_id`
- `max_players`
- `current_players`

## Session Flow

1. Player launches EXE and initializes Steam.
2. Host creates lobby and sets metadata.
3. Invited/friend players join lobby.
4. Host validates ready state and starts match.
5. Host peer becomes authoritative runtime.
6. Clients send commands, receive authoritative events.

## Authentication And Trust

- Use SteamID as primary online player identity.
- Keep per-match reconnect token in addition to SteamID.
- Validate command sender matches expected player seat.
- Reject stale or replayed command IDs.

## Networking Channels

- Channel A: reliable ordered gameplay commands/events.
- Channel B: snapshot/reconnect payloads.
- Channel C: optional chat/non-critical metadata.

## Host Lifecycle Policy

MVP policy:

- if host leaves or disconnects beyond timeout, match ends.

Post-MVP option:

- deterministic host migration with state snapshot transfer.

## Build And Release Integration

- Steam depots:
  - game executable/content depot
  - optional symbols/debug depot (restricted)
- Steam branches:
  - `dev`
  - `beta`
  - `default`
- Each promotion requires migration gate pass + smoke test.

## QA Requirements

- create/join/invite from friends list
- relay path matchmaking in different NAT types
- reconnect after transient disconnect
- host drop behavior messaging
- version mismatch handling in lobby join

## Compliance Checklist

- legal text/privacy links in launcher/menu
- crash report consent flow
- age/content rating checks per target region
- Steam overlay behavior test
