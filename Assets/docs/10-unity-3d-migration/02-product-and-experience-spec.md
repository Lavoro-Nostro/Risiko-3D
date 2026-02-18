# Product And Experience Spec

## Experience Goals

- Feel like playing RisiKo! around a real table.
- Make state readable at a glance from any camera distance.
- Keep actions fast and clear during online turns.

## Player Journeys

1. Host creates Steam lobby and selects map.
2. Players join via Steam lobby and ready up.
3. Match starts with objective reveal and territory assignment.
4. Turn loop: reinforcement, attack, fortify.
5. Endgame objective completion and match summary.

## 3D UX Principles

- Board readability first, spectacle second.
- Camera controls must never block core actions.
- All animations must preserve state clarity.
- Every state mutation should have a visible, understandable cue.

## Core Interaction Model

- Left click: select territory or action control.
- Drag / middle mouse: pan.
- Wheel / triggers: zoom.
- Context action panel for command validation and submit.

## Camera Modes

- Strategic top view (default)
- Focus mode on selected territory/combat
- Cinematic short replays for attacks (optional and skippable)

## Accessibility Baseline

- Colorblind-safe owner palettes
- Adjustable text scale and icon scale
- Reduced motion mode
- High contrast mode for territory borders and labels
