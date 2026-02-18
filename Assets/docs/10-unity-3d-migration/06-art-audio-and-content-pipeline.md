# Art, Audio, And Content Pipeline

## Asset Scope

- 3D table environment
- 3D world board with territory colliders and labels
- tank/unit prefabs by faction color
- physical dice models/materials
- card models and textures (territory/objective/joker/back)
- UI iconography and effects
- SFX and music loops

## Content Sources

- Existing `packs/` map data remains canonical for territory/adjacency.
- Existing localization assets remain canonical for labels/objectives.

## Pipeline Standards

- Source DCC: Blender/Maya (team choice)
- Exchange format: FBX + PNG/TGA + WAV/OGG
- Versioning: standard Git for now; enable Git LFS only when repository size/performance thresholds are exceeded
- Naming convention: `category_asset_variant_vNN`

## Optimization Rules

- LODs for high-poly assets where needed
- texture atlasing for board/props
- batching and instancing for repeated props
- avoid real-time expensive shaders on low tier

## Visual Readability Checklist

- territory borders readable at min and max zoom
- owner colors distinguishable in low light and colorblind filters
- dice pips clearly visible
- unit counts legible without camera micro-adjustments

## Audio Direction

- Subtle table ambience
- distinct SFX for reinforce/attack/capture/end-turn
- optional announcer cues
- dynamic mix ducking during critical events
