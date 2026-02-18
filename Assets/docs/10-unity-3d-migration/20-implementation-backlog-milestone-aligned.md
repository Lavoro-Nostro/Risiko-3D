# Implementation Backlog (Milestone-Aligned)

## Purpose

Execution backlog for the two-repo migration, aligned to milestones `U3D-M0` through `U3D-M4`.

## Priority Legend

- `P0`: required to ship milestone
- `P1`: strongly recommended
- `P2`: polish/follow-up

## M0 Preproduction (`U3D-M0`)

1. `U3D-M0-001` (`P0`) Create `Risiko-Unity` repository with Unity baseline project.
2. `U3D-M0-002` (`P0`) Add `.gitignore` and `.gitattributes` (no LFS initially).
3. `U3D-M0-003` (`P0`) Implement Steam SDK bootstrap and startup health checks.
4. `U3D-M0-004` (`P0`) Freeze protocol v1 skeleton from `18-steam-message-schema.md`.
5. `U3D-M0-005` (`P0`) Implement cross-repo contract version handshake.
6. `U3D-M0-006` (`P1`) Stand up Unity CI build/test workflow.
7. `U3D-M0-007` (`P1`) Define art style guide + technical budgets.

Exit evidence:

- Repo bootstrapped, CI green, protocol versioned, Steam init validated.

## M1 Vertical Slice (`U3D-M1`)

1. `U3D-M1-001` (`P0`) Steam lobby create/join/invite/start flow.
2. `U3D-M1-002` (`P0`) Unity board scene + territory selection + camera controls.
3. `U3D-M1-003` (`P0`) Host-authoritative command loop via Steam transport.
4. `U3D-M1-004` (`P0`) Reinforcement/attack/fortify basic playable loop.
5. `U3D-M1-005` (`P0`) Snapshot + reconnect restore path.
6. `U3D-M1-006` (`P1`) Objective card private visibility and territory card draw.
7. `U3D-M1-007` (`P1`) Basic physical animation pass (tanks/dice/cards).
8. `U3D-M1-008` (`P1`) Contract compatibility pipeline between repos.

Exit evidence:

- 2+ Steam users complete a match with reconnect and no critical desync.

## M2 Alpha (`U3D-M2`)

1. `U3D-M2-001` (`P0`) Full objective deck correctness with fallback rules.
2. `U3D-M2-002` (`P0`) Combat choreography polish + fast/reduced-motion modes.
3. `U3D-M2-003` (`P0`) Card/trade UI with localization-safe layouts.
4. `U3D-M2-004` (`P0`) Long-session stability and memory profiling.
5. `U3D-M2-005` (`P1`) Telemetry pipeline for events/errors/reconnect metrics.
6. `U3D-M2-006` (`P1`) Accessibility baseline completion.
7. `U3D-M2-007` (`P1`) Nightly soak tests with packet delay/loss simulation.

Exit evidence:

- Feature complete MVP scope, stable perf/memory on target hardware.

## M3 Beta/RC (`U3D-M3`)

1. `U3D-M3-001` (`P0`) P0/P1 bug burn-down and regression lock.
2. `U3D-M3-002` (`P0`) Steam branch/depot promotion dry run.
3. `U3D-M3-003` (`P0`) Compatibility matrix pass for current and previous client.
4. `U3D-M3-004` (`P1`) Support runbooks + incident playbooks finalized.
5. `U3D-M3-005` (`P1`) Store page assets and legal/compliance verification.

Exit evidence:

- RC passes release gates including Steam promotion rehearsal.

## M4 Launch (`U3D-M4`)

1. `U3D-M4-001` (`P0`) Release to Steam `default` branch.
2. `U3D-M4-002` (`P0`) Live monitoring dashboards and alerts active.
3. `U3D-M4-003` (`P0`) Hotfix branch workflow validated.
4. `U3D-M4-004` (`P1`) Post-launch telemetry review + patch plan.

Exit evidence:

- Production launch stable with rollback and on-call readiness.

## Suggested First Sprint (for you, Unity-heavy focus)

1. `U3D-M0-001`
2. `U3D-M0-002`
3. `U3D-M0-003`
4. `U3D-M0-004`
5. `U3D-M1-001`
6. `U3D-M1-002`
