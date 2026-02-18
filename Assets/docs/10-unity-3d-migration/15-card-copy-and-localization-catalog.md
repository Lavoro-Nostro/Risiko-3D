# Card Copy And Localization Catalog

## Purpose

Provide implementation-ready card text with stable IDs for Unity UI, card rendering, QA snapshots, and localization workflows.

Status: `LOCKED` for `U3D-M0`/`U3D-M1` implementation baseline (decision date `2026-02-18`).

## Naming Convention

- Objective title key: `objective.<id>.title`
- Objective body key: `objective.<id>.body`
- Objective fallback key (when applicable): `objective.<id>.fallback`
- Rule aid key: `rule_card.<id>.*`

## Localization Authority

- Canonical language: `it-IT`
- Maintained translation: `en-US`
- If translation conflicts with gameplay semantics, `it-IT` intent wins and `en-US` is revised.

## Project Localization Files

- `Assets/GameData/Map/i18n/it.json`
- `Assets/GameData/Map/i18n/en.json`
- Current status: locked M0 baseline files. Territory naming adjustments can be versioned later if needed.

## Objective Cards (IT Canonical + EN Working Translation)

## `obj-24`

- IT title: `Conquista 24 Territori`
- IT body: `Conquista 24 territori.`
- EN title: `Conquer 24 Territories`
- EN body: `Conquer 24 territories.`

## `obj-18-2`

- IT title: `Conquista 18 con 2 Armate`
- IT body: `Conquista 18 territori e occupali con almeno 2 armate ciascuno.`
- EN title: `Conquer 18 With 2 Armies`
- EN body: `Conquer 18 territories and occupy each with at least 2 armies.`

## `obj-eu-au-plus1`

- IT title: `Europa + Oceania + 1`
- IT body: `Conquista Europa, Oceania e un altro continente a scelta.`
- EN title: `Europe + Oceania + 1`
- EN body: `Conquer Europe, Oceania, and one additional continent of your choice.`

## `obj-eu-sa-plus1`

- IT title: `Europa + Sud America + 1`
- IT body: `Conquista Europa, Sud America e un altro continente a scelta.`
- EN title: `Europe + South America + 1`
- EN body: `Conquer Europe, South America, and one additional continent of your choice.`

## `obj-na-af`

- IT title: `Nord America + Africa`
- IT body: `Conquista Nord America e Africa.`
- EN title: `North America + Africa`
- EN body: `Conquer North America and Africa.`

## `obj-na-au`

- IT title: `Nord America + Oceania`
- IT body: `Conquista Nord America e Oceania.`
- EN title: `North America + Oceania`
- EN body: `Conquer North America and Oceania.`

## `obj-as-sa`

- IT title: `Asia + Sud America`
- IT body: `Conquista Asia e Sud America.`
- EN title: `Asia + South America`
- EN body: `Conquer Asia and South America.`

## `obj-as-af`

- IT title: `Asia + Africa`
- IT body: `Conquista Asia e Africa.`
- EN title: `Asia + Africa`
- EN body: `Conquer Asia and Africa.`

## `obj-elim-red`

- IT title: `Distruggi Rosso`
- IT body: `Distruggi totalmente l'armata rossa.`
- IT fallback: `Se impossibile, conquista 24 territori.`
- EN title: `Eliminate Red`
- EN body: `Completely eliminate the red army.`
- EN fallback: `If impossible, conquer 24 territories.`

## `obj-elim-blue`

- IT title: `Distruggi Blu`
- IT body: `Distruggi totalmente l'armata blu.`
- IT fallback: `Se impossibile, conquista 24 territori.`
- EN title: `Eliminate Blue`
- EN body: `Completely eliminate the blue army.`
- EN fallback: `If impossible, conquer 24 territories.`

## `obj-elim-green`

- IT title: `Distruggi Verde`
- IT body: `Distruggi totalmente l'armata verde.`
- IT fallback: `Se impossibile, conquista 24 territori.`
- EN title: `Eliminate Green`
- EN body: `Completely eliminate the green army.`
- EN fallback: `If impossible, conquer 24 territories.`

## `obj-elim-yellow`

- IT title: `Distruggi Giallo`
- IT body: `Distruggi totalmente l'armata gialla.`
- IT fallback: `Se impossibile, conquista 24 territori.`
- EN title: `Eliminate Yellow`
- EN body: `Completely eliminate the yellow army.`
- EN fallback: `If impossible, conquer 24 territories.`

## `obj-elim-purple`

- IT title: `Distruggi Viola`
- IT body: `Distruggi totalmente l'armata viola.`
- IT fallback: `Se impossibile, conquista 24 territori.`
- EN title: `Eliminate Purple`
- EN body: `Completely eliminate the purple army.`
- EN fallback: `If impossible, conquer 24 territories.`

## `obj-elim-black`

- IT title: `Distruggi Nero`
- IT body: `Distruggi totalmente l'armata nera.`
- IT fallback: `Se impossibile, conquista 24 territori.`
- EN title: `Eliminate Black`
- EN body: `Completely eliminate the black army.`
- EN fallback: `If impossible, conquer 24 territories.`

## Fallback Objective

- ID: `obj-fallback-24`
- IT title: `Conquista 24 Territori`
- IT body: `Conquista 24 territori.`
- EN title: `Conquer 24 Territories`
- EN body: `Conquer 24 territories.`

## Rule Aid Cards Copy

## `rule_card.turn_sequence`

- IT title: `Sequenza Turno`
- IT body:
  - `1. Rinforza`
  - `2. Attacca (opzionale, ripetibile)`
  - `3. Fortifica (opzionale, una volta)`
  - `4. Fine turno e pesca carta se hai conquistato`
- EN title: `Turn Sequence`
- EN body:
  - `1. Reinforce`
  - `2. Attack (optional, repeatable)`
  - `3. Fortify (optional, once)`
  - `4. End turn and draw if you conquered`

## `rule_card.combat_resolution`

- IT title: `Risoluzione Combattimento`
- IT body:
  - `Attaccante: 1-3 dadi`
  - `Difensore: fino al massimo profilo`
  - `Confronta dadi in ordine decrescente`
  - `Pareggio: vince il difensore`
- EN title: `Combat Resolution`
- EN body:
  - `Attacker: 1-3 dice`
  - `Defender: up to profile max`
  - `Compare dice in descending order`
  - `Tie: defender wins`

## `rule_card.trade_in`

- IT title: `Valori Tris`
- IT body:
  - `3 artiglierie = 4`
  - `3 fanti = 6`
  - `3 cavalieri = 8`
  - `1 per tipo = 10`
  - `1 jolly + 2 uguali = 12`
  - `+2 per territorio posseduto scambiato`
- EN title: `Trade-In Values`
- EN body:
  - `3 artillery = 4`
  - `3 infantry = 6`
  - `3 cavalry = 8`
  - `1 of each = 10`
  - `1 joker + 2 matching = 12`
  - `+2 per owned traded territory`

## Territory Card Copy Rules

- Territory display name comes from:
  - `Assets/GameData/Map/i18n/it.json`
  - `Assets/GameData/Map/i18n/en.json`
- Always reference territory ID in metadata for debugging/export.
- Long names must use multi-line wrapping, never truncation.

## Localization QA Matrix

For each card type verify:

1. `it-IT` at default UI scale
2. `it-IT` at max accessibility scale
3. `en-US` at default UI scale
4. `en-US` at max accessibility scale

Pass criteria:

- no clipping
- no overlap with symbols/icons
- minimum contrast ratio met
- fallback lines visible for eliminate objectives
