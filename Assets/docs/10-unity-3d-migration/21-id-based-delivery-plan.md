# ID-Based Delivery Plan

## Scope

Execution plan for Unity 3D migration using existing backlog IDs (`U3D-M*`), gate IDs (`U3D-G*`), and risk IDs (`U3D-RSK-*`).

## Rule Baseline Lock

Locked on `2026-02-18`:

1. Deterministic territory-symbol manifest (no runtime random symbol assignment).
2. `it-IT` canonical card/rule text with `en-US` maintained translation.
3. Strict private-objective visibility during active matches.
4. Target player count `3-6` with host start validation minimum `3`.

## Sequencing Rules

1. Complete all `P0` items in milestone before `P1`.
2. Do not promote milestone status until matching gate criteria are met.
3. Every task touching protocol/contracts must follow workflow in `19-contract-versioning-and-changelog-workflow.md`.

## Milestone Plan

## `U3D-M0` Preproduction

Order:
1. `U3D-M0-001`
2. `U3D-M0-002`
3. `U3D-M0-003`
4. `U3D-M0-004`
5. `U3D-M0-005`
6. `U3D-M0-006`
7. `U3D-M0-007`

Primary outputs:
- Two-repo baseline and CI ready.
- Steam bootstrap health checks working.
- Protocol `risiko.steam.protocol.v1` frozen.
- Contract handshake and compatibility checks active.

Risk focus:
- `U3D-RSK-001`, `U3D-RSK-002`, `U3D-RSK-005`

## `U3D-M1` Vertical Slice

Order:
1. `U3D-M1-001`
2. `U3D-M1-002`
3. `U3D-M1-003`
4. `U3D-M1-004`
5. `U3D-M1-005`
6. `U3D-M1-006`
7. `U3D-M1-007`
8. `U3D-M1-008`

Primary outputs:
- End-to-end playable Steam-hosted match loop.
- Host-authoritative command/event flow.
- Reconnect + snapshot restore demonstrated.

Gate:
- `U3D-G1`

Risk focus:
- `U3D-RSK-001`, `U3D-RSK-002`, `U3D-RSK-003`, `U3D-RSK-006`

## `U3D-M2` Alpha

Order:
1. `U3D-M2-001`
2. `U3D-M2-003`
3. `U3D-M2-004`
4. `U3D-M2-002`
5. `U3D-M2-006`
6. `U3D-M2-005`
7. `U3D-M2-007`

Primary outputs:
- Objective/card correctness complete.
- Stability and memory targets validated.
- Accessibility baseline and telemetry in place.

Gate:
- `U3D-G2`

Risk focus:
- `U3D-RSK-003`, `U3D-RSK-004`, `U3D-RSK-006`

## `U3D-M3` Beta/RC

Order:
1. `U3D-M3-001`
2. `U3D-M3-003`
3. `U3D-M3-002`
4. `U3D-M3-004`
5. `U3D-M3-005`

Primary outputs:
- Regression-locked RC candidate.
- Steam promotion dry run completed.
- Runbooks/support/legal package complete.

Gate:
- `U3D-G3`

Risk focus:
- `U3D-RSK-002`, `U3D-RSK-004`, `U3D-RSK-006`

## `U3D-M4` Launch

Order:
1. `U3D-M4-001`
2. `U3D-M4-002`
3. `U3D-M4-003`
4. `U3D-M4-004`

Primary outputs:
- Steam `default` launch.
- Monitoring + alerts + rollback readiness.
- Initial post-launch patch plan.

Gate:
- `U3D-G4`

Risk focus:
- `U3D-RSK-002`, `U3D-RSK-004`

## Critical Path IDs

1. `U3D-M0-003` -> `U3D-M1-001` -> `U3D-M1-003` -> `U3D-M1-005`
2. `U3D-M0-004` -> `U3D-M0-005` -> `U3D-M1-008` -> `U3D-M3-003`
3. `U3D-M1-004` -> `U3D-M2-001` -> `U3D-M3-001` -> `U3D-M4-001`

## Weekly Control Loop

1. Re-score risks `U3D-RSK-001` to `U3D-RSK-006`.
2. Verify DoD checklist from `12-definition-of-done-and-gates.md` for completed IDs.
3. Validate contract/schema changes against `18` + `19` before merge.
4. Confirm gate evidence is attached before milestone close.
