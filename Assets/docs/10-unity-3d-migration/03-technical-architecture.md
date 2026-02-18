# Technical Architecture

## Target Architecture

- `Unity.Client.EXE` (3D presentation + input + local prediction only for UX)
- `Unity.HostRuntime` (authoritative room/match orchestration in listen-host mode)
- `Risk.Engine` (deterministic rules)
- `Steamworks Adapter` (lobby, identity, invites, P2P/relay transport)
- optional `DedicatedHost` service for post-MVP scale

## Repository Boundaries

- Repo A: current `Risiko` repository
  - `Risk.Engine`
  - host/backend contracts and deterministic tests
  - gameplay rules and migration docs
- Repo B: new `Risiko-Unity` repository
  - Unity project, scenes, prefabs, Steamworks integration, client runtime
- Cross-repo coupling is allowed only through versioned message contracts.

## Runtime Boundaries

- Unity client never decides authoritative outcomes.
- Host validates and applies commands.
- Engine returns ordered events and next state.
- Client renders events, never invents outcome.

## Recommended Data Flow

1. Client submits command request.
2. Steam transport delivers intent to authoritative host peer.
3. Host validates player/phase/ownership constraints.
4. Host applies command through `Risk.Engine`.
5. Host emits ordered event stream and snapshot updates.
6. Clients reconcile local view to authoritative sequence.

## Unity Layers

- Presentation Layer: cameras, board, animation, audio
- Input Layer: selection and intent building
- UI Layer: turn controls, cards, status, chat
- Steam Layer: auth ticket, lobby metadata, invites, presence
- Networking Layer: Steam messages + reliable ordered match stream
- State Projection Layer: authoritative snapshot to render model

## Persistence/Replay

- Persist ordered event logs by match ID.
- Support deterministic replay from seed + command stream.
- Keep schema versioning explicit for migration safety.

## Performance Targets

- 60 FPS on target desktop spec
- 30 FPS floor on low spec
- < 200 ms command round-trip through Steam relay target
- stable memory without per-turn allocation spikes
