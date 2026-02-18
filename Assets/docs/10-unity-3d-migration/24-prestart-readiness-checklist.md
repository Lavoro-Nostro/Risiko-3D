# Prestart Readiness Checklist

Date: `2026-02-18`

## Done

- Rules/content freeze approved (`22-rules-freeze-signoff.md`).
- Steamworks.NET dependency added in `Packages/manifest.json`.
- Local dev `steam_appid.txt` created with app id `480`.
- Steam SDK startup probe integrated into runtime health checks:
  - `Assets/Scripts/Runtime/Bootstrap/SteamSdkHealthChecks.cs`
- Editor setup automation added:
  - `Assets/Scripts/Editor/M0SetupTools.cs` (`Risiko3D/Setup/Run Full Editor Setup`)
  - `Assets/Scripts/Editor/ProjectAutoSetup.cs` (auto-run once per project)
- Deterministic territory symbol manifest created:
  - `Assets/GameData/Cards/world-classic-territory-symbol-manifest.json`
- Objective card SVG set generated (`15` files):
  - `Assets/cards/generated/objective_it/*`
- Rule-aid card SVG set generated (`3` files):
  - `Assets/cards/generated/rule_it/*`
- Required map/card core assets organized and active.
- Optional assets archived under `Assets/Archive/Optional`.
- Content validation script added and passing:
  - `Tools/validate_content.ps1`

## Pending Before Gameplay Implementation

1. Confirm Unity imports SVG package and renders all card SVG assets correctly.
2. Run Unity menu health check:
   - `Risiko3D/M0/Run Startup Health Checks`

## M0 Implementation Started

- `U3D-M0-002` baseline repo hygiene files added:
  - `.gitignore`
  - `.gitattributes`
- `U3D-M0-003` startup bootstrap and health checks implemented:
  - `Assets/Scripts/Runtime/Bootstrap/StartupBootstrap.cs`
  - `Assets/Scripts/Runtime/Bootstrap/StartupHealthChecks.cs`
- `U3D-M0-004` protocol v1 skeleton DTOs added:
  - `Assets/Scripts/Runtime/Contracts/SteamProtocolV1Models.cs`
- `U3D-M0-005` contract compatibility handshake implemented:
  - `Assets/Scripts/Runtime/Contracts/ContractHandshake.cs`
  - `Assets/Scripts/Runtime/Contracts/SemanticVersion.cs`

## M1 Lobby Scaffold Started

- `U3D-M1-001` Steam lobby create/join/start baseline implemented:
  - `Assets/Scripts/Runtime/Steam/SteamRuntime.cs`
  - `Assets/Scripts/Runtime/Steam/SteamLobbyService.cs`
  - `Assets/Scripts/Runtime/Steam/SteamRuntimeInstaller.cs`
  - `Assets/Scripts/Editor/SteamLobbyDebugMenu.cs`

## M1 Board Scaffold Started

- `U3D-M1-002` board scene foundation implemented:
  - `Assets/Resources/Input/BoardControls.inputactions`
  - `Assets/Scripts/Runtime/Input/BoardInputActionsAdapter.cs`
  - `Assets/Scripts/Runtime/Board/MapDataModels.cs`
  - `Assets/Scripts/Runtime/Board/TerritoryNode.cs`
  - `Assets/Scripts/Runtime/Board/BoardCameraController.cs`
  - `Assets/Scripts/Runtime/Board/BoardBootstrap.cs`
  - `Assets/Scripts/Runtime/Board/BoardRuntimeInstaller.cs`
  - `Assets/Scripts/Editor/BoardDebugMenu.cs`

## M1 Host Loop Scaffold Started

- `U3D-M1-003` host-authoritative command/event baseline implemented:
  - `Assets/Scripts/Runtime/Match/HostAuthoritativeMatchLoop.cs`
  - `Assets/Scripts/Runtime/Match/MatchRuntimeInstaller.cs`
  - `Assets/Scripts/Runtime/Bootstrap/StartupBootstrap.cs` (installer wiring)
  - `Assets/Resources/Input/BoardControls.inputactions` (Enter/F5/F6 actions)

## M1 Playable Loop In Progress

- `U3D-M1-004` reinforce/attack/fortify playable phase baseline implemented:
  - `Assets/Scripts/Runtime/Match/HostAuthoritativeMatchLoop.cs`
  - `Assets/Scripts/Runtime/Board/TerritoryNode.cs` (owner/army state rendering)
  - `Assets/Scripts/Runtime/Board/BoardBootstrap.cs` (adjacency + territory state API + position manifest + edge lines)
  - `Assets/Scripts/Runtime/Board/TerritoryPositionManifestModels.cs`
  - `Assets/Scripts/Runtime/Board/BoardVisualLayer.cs`
  - `Assets/GameData/Map/world-classic-territory-positions.json`
  - `Assets/Resources/Map/world_classic_map.svg`
  - `Assets/Resources/Input/BoardControls.inputactions` (`Tab` end turn)
  - `Assets/Scripts/Editor/BoardDebugMenu.cs` (export territory positions)

## Start Command

Run after any asset/content change:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/validate_content.ps1
```
