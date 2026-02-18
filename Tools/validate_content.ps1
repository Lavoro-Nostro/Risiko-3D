$ErrorActionPreference = "Stop"

function Assert-Exists {
    param([string]$Path)
    if (!(Test-Path $Path)) {
        throw "Missing required path: $Path"
    }
}

function To-CardFileName {
    param([string]$TerritoryId)
    return (($TerritoryId -split "_") | ForEach-Object {
        if ($_.Length -gt 0) { $_.Substring(0, 1).ToUpper() + $_.Substring(1) } else { $_ }
    }) -join "_"
}

Write-Host "Validating content baseline..."

$mapPath = "Assets/GameData/Map/map.json"
$manifestPath = "Assets/GameData/Cards/world-classic-territory-symbol-manifest.json"
$territoryCardsDir = "Assets/cards/territory"
$objectiveCardsDir = "Assets/cards/generated/objective_it"
$ruleCardsDir = "Assets/cards/generated/rule_it"
$backs = @(
    "Assets/Art/Cards/Backs/territory_card_back.svg",
    "Assets/Art/Cards/Backs/objective_card_back.svg"
)
$symbols = @(
    "Assets/Art/Cards/Symbols/infantry.png",
    "Assets/Art/Cards/Symbols/cavalry.png",
    "Assets/Art/Cards/Symbols/artillery.png"
)

Assert-Exists $mapPath
Assert-Exists $manifestPath
Assert-Exists $territoryCardsDir
Assert-Exists $objectiveCardsDir
Assert-Exists $ruleCardsDir
$backs | ForEach-Object { Assert-Exists $_ }
$symbols | ForEach-Object { Assert-Exists $_ }

$map = Get-Content $mapPath -Raw | ConvertFrom-Json
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

if ($map.territories.Count -ne 42) {
    throw "Expected 42 territories in map.json, got $($map.territories.Count)"
}

if ($manifest.mapId -ne $map.id) {
    throw "Manifest mapId ($($manifest.mapId)) does not match map id ($($map.id))"
}

if ($manifest.assignments.Count -ne $map.territories.Count) {
    throw "Manifest assignments count ($($manifest.assignments.Count)) does not match territory count ($($map.territories.Count))"
}

$allowed = @("infantry", "cavalry", "artillery")
$invalid = $manifest.assignments | Where-Object { $allowed -notcontains $_.symbol }
if ($invalid.Count -gt 0) {
    throw "Manifest contains invalid symbols."
}

$counts = $manifest.assignments | Group-Object symbol | ForEach-Object { @{ key = $_.Name; value = $_.Count } }
$countMap = @{}
foreach ($c in $counts) { $countMap[$c.key] = $c.value }
foreach ($k in $allowed) {
    if (-not $countMap.ContainsKey($k) -or $countMap[$k] -ne 14) {
        throw "Symbol distribution must be 14 each. Found: infantry=$($countMap['infantry']) cavalry=$($countMap['cavalry']) artillery=$($countMap['artillery'])"
    }
}

$territoryIds = @($map.territories | ForEach-Object { $_.id })
$manifestIds = @($manifest.assignments | ForEach-Object { $_.territoryId })

$missingInManifest = Compare-Object $territoryIds $manifestIds -PassThru | Where-Object { $_ -in $territoryIds }
$extraInManifest = Compare-Object $territoryIds $manifestIds -PassThru | Where-Object { $_ -in $manifestIds }

if ($missingInManifest.Count -gt 0) {
    throw "Manifest missing territories: $($missingInManifest -join ', ')"
}
if ($extraInManifest.Count -gt 0) {
    throw "Manifest has unknown territories: $($extraInManifest -join ', ')"
}

foreach ($tid in $territoryIds) {
    $cardName = To-CardFileName $tid
    $cardPath = Join-Path $territoryCardsDir "$cardName.svg"
    if (!(Test-Path $cardPath)) {
        throw "Missing territory card SVG for '$tid' at '$cardPath'"
    }
}

$requiredObjectiveIds = @(
    "obj-24", "obj-18-2", "obj-eu-au-plus1", "obj-eu-sa-plus1", "obj-na-af", "obj-na-au",
    "obj-as-sa", "obj-as-af", "obj-elim-red", "obj-elim-blue", "obj-elim-green",
    "obj-elim-yellow", "obj-elim-purple", "obj-elim-black", "obj-fallback-24"
)

foreach ($oid in $requiredObjectiveIds) {
    $path = Join-Path $objectiveCardsDir "$oid.svg"
    if (!(Test-Path $path)) {
        throw "Missing generated objective SVG: $path"
    }
}

$requiredRuleIds = @(
    "rule_card.turn_sequence",
    "rule_card.combat_resolution",
    "rule_card.trade_in"
)

foreach ($rid in $requiredRuleIds) {
    $path = Join-Path $ruleCardsDir "$rid.svg"
    if (!(Test-Path $path)) {
        throw "Missing generated rule SVG: $path"
    }
}

Write-Host "OK: map=42 territories, manifest=42 assignments, symbols=14/14/14, territory card coverage complete, objective/rule SVG generated set complete."
