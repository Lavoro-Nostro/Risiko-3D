# Definition Of Done And Release Gates

## Story/Feature DoD

- implemented and reviewed
- automated tests added/updated
- contract docs updated if behavior changed
- telemetry/logging updated if needed
- no open P0/P1 defects for changed area

## Milestone Gates

## Gate `U3D-G1` (Vertical Slice)

- full match playable in Unity with 2+ players
- host-authoritative flow operational
- Steam lobby create/join/start flow operational
- reconnect recovers state in test cases
- no critical desync in nightly runs

## Gate `U3D-G2` (Alpha)

- feature complete for MVP scope
- performance baseline met on target hardware
- accessibility baseline implemented

## Gate `U3D-G3` (Beta/RC)

- no open P0 and no unresolved systemic P1
- release runbooks complete
- staging soak tests pass
- Steam branch promotion dry-run passes

## Gate `U3D-G4` (Launch)

- production monitoring/alerts active
- rollback validated
- support and incident response on-call ready

## Exit Criteria For Migration Success

- parity with core gameplay rules
- stable multiplayer sessions in real-world networks
- player readability and interaction quality accepted by QA/product
