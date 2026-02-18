# Production Plan And Roadmap

## Phase Plan

## Phase 0: Preproduction (4-6 weeks)

- lock architecture and integration approach
- bootstrap two-repo model (`Risiko` + `Risiko-Unity`) and CI
- finalize technical spike on Steam lobby + Steam networking adapter
- define asset style guide and performance budgets
- produce backlog and acceptance criteria

## Phase 1: Vertical Slice (10-14 weeks)

- board scene, camera controls, territory selection
- Steam lobby create/join/invite/start turn loop integration
- core reinforce/attack/fortify gameplay in 3D
- basic cards/objective UI and audio cues

Exit gate:

- full match playable with 2+ Steam users through lobby
- reconnect works
- no critical desync in test matrix

## Phase 2: Alpha (12-16 weeks)

- polished combat/dice/tank animation pass
- UX improvements, accessibility options
- stability and reconnect hardening
- telemetry and crash reporting

Exit gate:

- feature complete for MVP target
- performance and memory targets met

## Phase 3: Beta/Release (8-12 weeks)

- large-scale QA, bug burn-down
- release candidate hardening
- Steam store/depot/release process hardening
- docs and support runbooks

Exit gate:

- release gates from `12-definition-of-done-and-gates.md` satisfied

## Milestone IDs

- `U3D-M0` Preproduction signoff
- `U3D-M1` Vertical slice complete
- `U3D-M2` Alpha complete
- `U3D-M3` Beta/RC complete
- `U3D-M4` Production launch

## Current Implementation Status

Last updated: `2026-02-18`

## `U3D-M0` Preproduction

- `Done` architecture/bootstrap wiring in Unity runtime (`Startup`, `Steam`, `Board`, `Match` installers)
- `Done` runtime config and startup health checks
- `Done` initial map/rules/card data import and canonical profile wiring

## `U3D-M1` Vertical Slice

- `Done` board scene foundation:
  - board visual layer
  - camera controls
  - territory spawn/selection
  - territory labels/army counters
- `Done` Steam runtime foundation:
  - Steam runtime install path
  - lobby create/join/invite/start metadata flow
- `In Progress` multiplayer entry flow:
  - scene-aware startup bootstrap (`MainMenu` vs gameplay)
  - runtime main menu HUD scaffold (host, friend discovery, room code join)
  - LAN announcement/discovery scaffold for local network lobby visibility
- `In Progress` territory overlay polish:
  - SVG/sprite-space territory overlay and selection effects integrated
  - final per-territory perfect alignment still open
- `In Progress` authoritative match loop:
  - command/event log, checksum, snapshot/reconnect simulation present
  - setup dealing flow + starting army placement implemented
  - attack/defend dice resolution with RNG implemented
  - fortify connected-path validation implemented
  - territory card deck/hand/discard + capture draw implemented
  - `tris` trade-in rules (including owned-territory `+2`) implemented
- `Pending` rules-complete gameplay:
  - full reinforce/attack/fortify rules parity
  - objective victory checks
  - attacker/defender manual dice-count choice UX
- `Pending` vertical-slice exit gate hardening:
  - full 2+ Steam user playable match loop
  - reconnect validation across real multiplayer sessions
  - no-critical-desync test matrix run
