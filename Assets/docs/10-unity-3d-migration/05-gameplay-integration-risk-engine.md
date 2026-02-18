# Gameplay Integration With Risk.Engine

## Integration Goal

Use `Risk.Engine` as deterministic source of truth for:

- phase transitions
- combat resolution
- objective evaluation
- card and reinforcement logic

## Mapping Strategy

- Unity intent -> host command DTO
- host command DTO -> `Risk.Engine` command
- engine event -> host event payload -> Unity render event

## Rule Ownership

- Rule changes happen in `Risk.Engine` only.
- Unity reflects rules through contracts and view projections.

## Determinism Requirements

- Seeded RNG managed in host/engine layer.
- Command IDs included in every command.
- No Unity-side random gameplay outcomes.

## Compatibility Tests

- Contract tests for command schema and event schema.
- Golden-match replay tests: Unity vs host snapshots at checkpoints.
- Regression suite for objective assignment and combat outcomes.

## Migration Tasks

1. Define and version Steam message contracts for commands/events/snapshots.
2. Build Unity Steam networking adapter against those contracts.
3. Build state projection layer from host snapshot to Unity view model.
4. Validate parity with automated simulation matches.
