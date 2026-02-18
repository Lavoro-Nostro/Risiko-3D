# Executive Brief: Unity 3D Rebuild

## Objective

Rebuild RisiKo! as a Unity 3D experience with:

- physical table board presentation
- physical-feel dice and tanks
- Steam-distributed Windows EXE
- Steam lobby/invite flow
- host-authoritative multiplayer
- deterministic rules and replay correctness

## Recommendation

- Keep `Risk.Engine` as the gameplay source of truth.
- Build a Unity EXE with Steam SDK integration and host-authoritative session flow.
- Do not rewrite core rules in Unity unless unavoidable.

## Why

- Rule parity risk drops sharply if `Risk.Engine` remains authoritative.
- Existing tests in `Risk.Engine` and `Risk.Host.Tests` stay valuable.
- Multiplayer correctness is easier with one authoritative simulation.

## Delivery Envelope

- Vertical slice: 4-6 months (small experienced team)
- Production release: 10-16 months (2-4 engineers + content + QA)

## Non-Negotiable Constraints

- No client-side authoritative combat resolution.
- Reconnect must recover match state and sequence safely.
- Match timeline must remain deterministic and auditable.
- Input latency must not break turn flow clarity.

## Key Decision Needed

Choose one hosting mode inside Steam ecosystem:

1. Listen-host (lobby owner authoritative, recommended first release)
2. Dedicated authoritative server + Steam lobby discovery (higher ops cost)
