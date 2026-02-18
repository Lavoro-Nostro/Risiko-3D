# Physical Animation And Tabletop Choreography

## Goal

Define exact visual behavior for physical-feel gameplay:

- tanks are placed on territories as physical pieces
- attack/defense actions spawn and roll dice physically
- tanks move across territory paths for captures/fortify
- cards are dealt from a deck, private in hand, face-down to others

This document is implementation-ready and aligned with host-authoritative state.

## Design Principles

- Gameplay truth comes from host events, never from physics outcomes.
- Physics is presentation only and must be corrected to host results.
- Every state mutation has one clear visual cue.
- Animations must be skippable/accelerated in multiplayer.
- Readability beats realism when they conflict.

## Table Scene Components

- `BoardRoot`: map mesh/collider, territory anchors, border highlight layer.
- `TerritoryAnchor[42]`: fixed transforms for unit stacks and labels.
- `ArmyStackController`: owns per-territory tank piece layout.
- `DiceTray`: local roll arena with walls/camera focus.
- `CardDeckController`: draw pile, discard pile, player hands.
- `CombatLaneRenderer`: temporary path from source territory to target.
- `GraveyardTray`: eliminated tank pieces for short-lived feedback.

## Tank Piece Behavior

## Placement

- Reinforcement place:
  1. spawn tank from active player reserve pool
  2. arc-move to selected territory anchor
  3. settle into stack slot
- Duration target: `0.28s` per tank in normal speed, `0.12s` fast mode.

## Stack Layout

- Use deterministic slot offsets per territory (ring + inner fill).
- Max one visible mesh per army up to `N=20`, then switch to grouped proxy mesh.
- Always keep unit-count badge visible above stack.

## Capture Movement

- On `TerritoryCaptured` event:
  1. losing stack performs collapse animation (brief tip + fade)
  2. captured territory color ring flips owner
  3. attacker required move count marches from source to target along spline
  4. both stacks auto-reorganize to neat final layout

## Fortify Movement

- On `Fortified` event:
  1. selected count detaches from source stack
  2. convoy movement across path (single flow, not per-neighbor teleport)
  3. destination merges and restacks

## Combat Dice Choreography

## Intent

Visualize "tanks prop up and shoot the dice" without affecting deterministic results.

## Sequence Per Attack Roll

1. `AttackResolved` event received with attacker/defender dice values.
2. Attacker and defender front tanks enter "fire stance" (`0.18s`).
3. Dice are launched into tray with impulse direction based on side.
4. Physics sim runs for `0.85s` (normal) / `0.45s` (fast).
5. Dice faces are snapped to authoritative values if needed (`<= 1 frame` correction).
6. Losing side tanks are removed from stacks with knockback-to-tray motion.
7. Remaining stacks reorder.

## Dice Rules

- Attacker dice color: active player accent.
- Defender dice color: neutral dark or defender accent (configurable).
- Dice count indicators always shown in HUD even during animation.
- In reduced-motion mode: skip physics, use deterministic flip animation.

## Card Choreography

## Deck And Dealing

- Match start:
  - objective cards dealt one per player from objective deck
  - card physically moves from deck to player hand area
- Territory draw on end turn after capture:
  - top card lifts, rotates, slides to active player's hand

## Privacy Rules

- Local player hand: front face visible.
- Remote players: face-down backs only + card count chip.
- Spectator mode: configurable policy (`none` by default, no private info).

## Trade-In (`tris`) Animation

1. Player selects 3 cards.
2. Selected cards fan forward and glow.
3. Cards move to discard pile.
4. Reinforcement bonus number pops near reserve tray.
5. If owned traded territory bonus applies, affected territories pulse +2 indicator.

## Turn-Phase Presentation Timeline

## Reinforcement

- Enter phase banner (`0.4s`)
- available armies counter animates in
- each placement animates from reserve to territory

## Attack

- source and target borders pulse
- combat lane appears
- dice/tank choreography runs per roll
- capture transition runs when defense reaches zero

## Fortify

- selectable owned territories highlight
- transfer convoy animation

## End Turn

- capture-earned card draw animation (if applicable)
- next player indicator rotates table focus

## Camera And Animation Coordination

- Combat auto-focus camera may trigger only if player enabled cinematic mode.
- Camera moves use eased curves, max `0.6s`, always interruptible by skip.
- Manual camera input instantly cancels cinematic camera path.

## Performance Targets

- Main gameplay scene: `60 FPS` target on recommended spec.
- Animation frame budget: `< 4 ms` CPU + `< 2 ms` GPU average.
- Max live tank objects before proxying: configurable, default `500`.

## Networking And Sync Contracts

Animation drivers consume existing events:

- `ReinforcementsPlaced`
- `AttackResolved`
- `TerritoryCaptured`
- `Fortified`
- `CardsTraded`
- `CardDrawn`
- `PlayerEliminated`
- `TurnStarted` / `TurnEnded`

Rules:

- Never block state apply while animation is running.
- Keep an animation queue keyed by event sequence.
- On reconnect snapshot, flush pending animations and rebuild scene from state.

## Controls For UX Modes

- `Normal`: full animations.
- `Fast`: shortened timings by ~45%.
- `Reduced Motion`: no physics rolls, minimal transitions.
- `Instant`: no non-essential transitions (competitive mode).

## Acceptance Criteria

- Tank/dice/card animations never change gameplay outcome.
- Any roll can be skipped without desync.
- Capture/fortify movement always ends in host-authoritative counts.
- Private cards are never leaked to other clients.
- Reconnect during animation restores correct state within `<= 2s`.
