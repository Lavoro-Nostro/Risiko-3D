# Quality And Test Strategy

## Testing Layers

1. Engine determinism and rules regression
2. Steam message contract tests
3. Unity integration tests (state projection, command flow)
4. End-to-end multiplayer scenario tests
5. Manual exploratory and UX quality passes

## Critical Test Areas

- phase correctness across full matches
- attack resolution parity with `Risk.Engine`
- reconnect after network interruption
- host migration/failure policy behavior
- objective assignment correctness and variability
- Steam lobby/invite/join correctness

## Non-Functional Targets

- no memory growth over long sessions
- stable FPS in target scenes
- acceptable network behavior under packet delay/loss simulation

## Automation Matrix

- deterministic replay tests on every PR
- nightly multiplayer soak test
- contract compatibility tests between Unity client peers and authoritative host runtime

## Manual Passes

- camera and board readability by zoom level
- accessibility modes
- Steam onboarding/lobby/invite flows
- card readability and localization QA

## Defect Prioritization

- P0: desync, crash, data loss, security
- P1: rule mismatch, reconnect failure, blocking UI
- P2: animation glitches, readability issues, non-blocking UX defects
