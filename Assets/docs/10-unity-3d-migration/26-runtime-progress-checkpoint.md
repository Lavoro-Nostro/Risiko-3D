# Runtime Progress Checkpoint

Last updated: `2026-02-18`

## Current State

- Unity project initialized and content imported.
- Steam runtime bootstrap integrated (dev app id flow verified).
- Board runtime is playable with territory selection, labels, and selection overlay.
- Match loop is running with setup -> reinforce -> attack -> fortify turn flow.

## Rules/Flow Parity (2D -> Unity)

- Setup uses objective + territory dealing, then setup placements with per-turn cap.
- Reinforcement rules include continent bonuses and forced trade gate.
- Attack uses deterministic dice resolution with attacker/defender limits.
- Capture flow includes pending captured-armies movement before phase advance.
- Fortify enforces adjacent-owned movement and variable move amount.
- End-turn draw grants one card on capture turns.
- Objective checks and winner lock are integrated in runtime.

## Cards/UI

- Objective and hand dock implemented in UI Toolkit.
- Card widgets are rendered from uploaded SVG fronts.
- Runtime card asset loading is wired through `Resources/Cards/*`.
- Trade-in control and hand visibility are active for local runtime.

## Multiplayer/Menu

- Main menu runtime scaffold exists for host/join flow.
- Steam lobby service integration exists.
- LAN discovery scaffold exists.
- Full multiplayer gameplay sync hardening remains in progress.

## Next Priority

1. Final compile/runtime pass in Unity Editor after asset import refresh.
2. Tight 2D parity pass for any remaining edge-case command validation.
3. Multiplayer authority/session integration tests (2+ clients).
4. Objective/elimination edge-case QA matrix run.
