# DevOps, Release, And LiveOps

## Build And CI

- Unity CI build pipeline for Windows EXE (Steam target)
- Steamworks build/depot automation (upload, branch promotion)
- shared gameplay test pipeline using `Risk.Engine` test suite
- artifact signing and version metadata
- cross-repo compatibility pipeline (`Risiko` contracts vs `Risiko-Unity` client)

## Environments

- Local dev
- Shared staging
- Pre-production (release candidate)
- Steam branches (`dev`, `beta`, `default`)

## Release Pipeline

1. Build and test
2. Validate contract compatibility matrix (engine/host version vs client version)
3. Package EXE and content depots
4. Upload to Steam `dev` branch
5. Run release gate suite
6. Promote to `beta` then `default` with rollback plan

## Observability

- host runtime logs with correlation IDs
- command/event audit traces by match ID/sequence
- client crash/error telemetry
- lobby/session metrics (create/join/fail/reconnect)

## LiveOps Basics

- incident severity policy and on-call
- hotfix branch and fast-track validation
- maintenance windows and player messaging
- Steam announcement + branch hotfix procedure

## Backup/Recovery

- match metadata/event log retention policy
- encrypted secrets and token management
- documented recovery procedure per environment
