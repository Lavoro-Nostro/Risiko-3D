# Asset Reorg And Usage

Date: `2026-02-18`

## Reorganized Structure

- `Assets/GameData/Map/map.json`: map topology/source data.
- `Assets/GameData/Map/i18n/{it,en}.json`: territory localization files (locked M0 baseline).
- `Assets/GameData/Map/world-classic-territory-positions.json`: board visual placement manifest.
- `Assets/GameData/Cards/world-classic-territory-symbol-manifest.json`: deterministic symbol assignment.
- `Assets/Art/Map/map.svg`: master vector map artwork.
- `Assets/Art/Map/world_map_reference.png`: high-res raster reference.
- `Assets/Art/Cards/Backs/*`: shared card back art.
- `Assets/Art/Cards/Symbols/*`: infantry/cavalry/artillery icons.
- `Assets/Art/Cards/TerritoryPNG/*`: 42 territory raster assets.
- `Assets/cards/territory/*`: 42 territory card SVG fronts (existing set).
- `Assets/cards/objective/objective_card_blank.svg`: objective template.
- `Assets/cards/joker/joker_card.svg`: joker card art.
- `Assets/cards/player/*`: colored player cards.
- `Assets/Art/Pieces/Tanks/Carrar*.png`: tank color images.

## Active Vs Archived

- Active assets remain in `Assets/Art`, `Assets/GameData`, and `Assets/cards`.
- Optional assets were moved to `Assets/Archive/Optional` (not deleted).
- Validation script: `Tools/validate_content.ps1`
- Card generation script: `Tools/generate_card_svgs.py`

## Coverage Check

- Territory PNG set: `42/42`
- Territory SVG card set: `42/42`
- Card symbols: `3/3` (`infantry`, `cavalry`, `artillery`)

## Required For MVP

- `Assets/GameData/Map/map.json`
- `Assets/GameData/Cards/world-classic-territory-symbol-manifest.json`
- `Assets/Art/Map/map.svg`
- `Assets/Art/Cards/Backs/territory_card_back.svg`
- `Assets/Art/Cards/Backs/objective_card_back.svg`
- `Assets/Art/Cards/Symbols/*`
- `Assets/cards/territory/*`
- `Assets/cards/objective/objective_card_blank.svg`
- `Assets/cards/joker/joker_card.svg`
- `Assets/cards/generated/objective_it/*` (generated objective fronts)
- `Assets/cards/generated/rule_it/*` (generated rule-aid fronts)

## Optional / Nice To Have

- `Assets/Archive/Optional/MapReference/world_map_reference.png` (design/reference only)
- `Assets/Archive/Optional/Cards/TerritoryPNG/*` (useful for previews/thumbnails; not required if SVG-only pipeline)
- `Assets/Archive/Optional/Cards/player/*` (useful for elimination objective UX; optional for first playable)
- `Assets/Archive/Optional/Pieces/Tanks/*` (optional if 3D mesh/material system replaces 2D icons)
- `Assets/Archive/Optional/SourceDrop/Carrar*.png` (source drop duplicates outside Unity asset tree)

## Not Needed Right Now (Safe To Ignore)

- No files are invalid; all uploaded assets are usable.
- The only likely future cleanup is duplicate-format territory fronts:
  - keep both PNG + SVG during production
  - remove one format later once the final rendering pipeline is locked

## Important Path Change

- `map.json` moved from `Assets/map.json` to `Assets/GameData/Map/map.json`.

## Generation Commands

```powershell
python Tools/generate_card_svgs.py
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/validate_content.ps1
```
