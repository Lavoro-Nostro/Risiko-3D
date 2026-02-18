# Repository Strategy: Two Repos

## Decision

Use two repositories:

1. `Risiko` (current): deterministic engine, host/contracts, docs
2. `Risiko-Unity` (new): Unity EXE + Steam SDK + client presentation/runtime

## Why Two Repos

- Unity binary assets should not pollute engine/backend history.
- Separate CI/CD stacks (Unity build vs .NET build/test).
- Cleaner ownership boundaries for gameplay core vs client app.
- Faster clone and onboarding for each contributor type.

## Folder Expectations

- Keep `Risiko` and `Risiko-Unity` as sibling folders:
  - `.../Dev/Risiko`
  - `.../Dev/Risiko-Unity`

## Contract Ownership Model

- Contracts are owned in `Risiko` (source of truth).
- Unity client consumes generated/packaged contract artifacts.
- Every contract change requires:
  1. contract version bump
  2. changelog entry
  3. compatibility tests against `Risiko-Unity`

## Branching Model

## `Risiko`

- `main` (or `dev` as your primary integration branch)
- feature branches: `feat/contracts-*`, `feat/rules-*`
- release tags for contract milestones: `contracts-vX.Y.Z`

## `Risiko-Unity`

- `main` (or `dev`) for active integration
- feature branches: `feat/steam-*`, `feat/gameplay-ui-*`
- release tags aligned to Steam builds: `client-vX.Y.Z`

## Versioning Policy

- Contract version: semantic versioning (`X.Y.Z`)
- Client declares min/max supported contract versions.
- Match creation blocked if version compatibility fails.

## CI Requirements

- `Risiko` pipeline:
  - unit/integration tests
  - contract package publish (artifact)
- `Risiko-Unity` pipeline:
  - Unity tests + headless smoke
  - consume latest compatible contract artifact
- nightly matrix:
  - latest `Risiko` x latest `Risiko-Unity`
  - latest `Risiko` x last released `Risiko-Unity`
  - last released `Risiko` x latest `Risiko-Unity`

## Release Coordination

1. Freeze contract changes for release window.
2. Build and validate Unity client against frozen contract.
3. Tag both repos with linked release notes.
4. Promote Steam branch (`dev` -> `beta` -> `default`).

## Asset Versioning Policy (`Risiko-Unity`)

- Start without Git LFS.
- Keep text serialization enabled where possible (`.unity`, `.prefab`, `.asset`).
- Add a periodic repo-size check and enable LFS only if thresholds are exceeded.
- Suggested trigger: repository size over 3 GB or clone/pull performance becoming a blocker.

## Minimal Bootstrap Checklist

1. Create `Risiko-Unity` repo.
2. Add Unity `.gitignore` and `.gitattributes` (text/merge settings only).
3. Add CI workflow for Unity build/test.
4. Add contract-consumer package/step from `Risiko`.
5. Add compatibility check in startup handshake.
6. Add release template linking both repo tags.

Bootstrap starter files are available in:

- `templates/risiko-unity-bootstrap/README.md`
