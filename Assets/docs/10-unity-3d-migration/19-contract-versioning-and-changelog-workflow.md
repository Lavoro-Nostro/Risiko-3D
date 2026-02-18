# Contract Versioning And Changelog Workflow

## Purpose

Define how `Risiko` and `Risiko-Unity` evolve shared gameplay/network contracts without breaking online play.

## Ownership

- Source of truth: `Risiko` repo.
- Consumer: `Risiko-Unity` repo.

## Version Format

Use semantic versioning for contract package:

- `MAJOR.MINOR.PATCH`

Rules:

- `MAJOR`: breaking packet/schema/behavior change.
- `MINOR`: backward-compatible field additions/new event types.
- `PATCH`: typo/docs/non-breaking clarifications.

## Compatibility Matrix

- client declares supported range: `contractMin`, `contractMax`.
- host declares exact runtime contract version.
- join/start allowed only when host version is within client range.

## Change Process

1. Propose change in `Risiko` with impact label (`breaking` or `non-breaking`).
2. Update schema docs and examples.
3. Bump version.
4. Add changelog entry.
5. Publish contract artifact.
6. Update `Risiko-Unity` dependency and run compatibility tests.
7. Tag both repos when release-ready.

## Required Files

- `docs/10-unity-3d-migration/18-steam-message-schema.md`
- `templates/risiko-unity-bootstrap/docs/contracts/CONTRACT_CHANGELOG_TEMPLATE.md`
- `templates/risiko-unity-bootstrap/docs/contracts/CONTRACT_COMPATIBILITY_MATRIX.md`

## Breaking Change Policy

A change is breaking if any of the following happens:

- packet field removed or renamed
- enum/event value removed or semantics changed
- validation behavior changes for existing commands
- event ordering semantics change

Breaking release checklist:

1. bump `MAJOR`
2. update compatibility matrix
3. add migration notes for Unity client
4. perform dual-version smoke test

## Non-Breaking Change Policy

- add optional fields only
- add new event types without changing existing payloads
- default behavior remains stable for old clients

Non-breaking release checklist:

1. bump `MINOR` or `PATCH`
2. add test vectors
3. run backward-compatibility replay test

## Pull Request Checklist Template

- [ ] Schema updated
- [ ] Version bumped
- [ ] Changelog entry added
- [ ] Example payloads updated
- [ ] Compatibility tests passed
- [ ] `Risiko-Unity` impact reviewed

## Release Notes Template (Cross-Repo)

```md
# Contract Release vX.Y.Z

Date: YYYY-MM-DD
Classification: breaking|non-breaking

## Summary
- 

## Added
- 

## Changed
- 

## Removed
- 

## Migration Actions (`Risiko-Unity`)
1. 
2. 

## Compatibility
- Host version: vX.Y.Z
- Min Unity client contract: vA.B.C
- Max Unity client contract: vD.E.F
```
