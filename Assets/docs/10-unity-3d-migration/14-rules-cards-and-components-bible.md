# Rules, Cards, And Components Bible

## Purpose

Single planning source for all tabletop-style game components in Unity 3D, tied to current canonical rules:

- `Assets/docs/10-unity-3d-migration/22-rules-freeze-signoff.md`
- `Assets/GameData/Map/map.json`
- `Assets/GameData/Cards/world-classic-territory-symbol-manifest.json`

## Canonical Rules Profile

- Profile ID: `RisiKo!_OBJECTIVE_CLASSICO_IT_V1`
- Player count: `3-6` (enforce minimum `3` in digital host flow before `U3D-G1`)
- Core phases: reinforcement, attack, fortify, end turn
- Objective mode with private secret objectives
- Territory deck + 2 jokers, one draw max after at least one capture

## Setup Phase Rules (Classic Baseline)

Checked: `2026-02-18`

- Initial armies by player count:
  - `3 players => 35`
  - `4 players => 30`
  - `5 players => 25`
  - `6 players => 20`
- Setup sequence:
  1. Players claim one unoccupied territory at a time in turn order until all territories are owned.
  2. Players continue in turn order placing one additional army at a time on territories they own.
  3. Continue until each player's full starting army count is exhausted.

## Setup Variant Used In Runtime (Current)

- Runtime currently uses the mission-style dealt setup flow:
  1. Objective card dealt privately to each player.
  2. Territory cards dealt to players (wild/joker excluded for setup assignment).
  3. Each dealt territory starts with `1` army from the player's starting pool.
  4. Remaining starting armies are placed one-by-one in turn order.

Reason:

- This matches the requested objective-mode flow and keeps digital setup fast.

Reference documents:

- Hasbro Risk Rules (2018 edition PDF):
  - https://instructions.hasbro.com/api/download/00044_en-us_risk-board-game.pdf
- Text mirror containing setup section (same baseline values/sequence):
  - https://manualsnet.com/hasbro/risk-2018
- Mission-style setup reference (objective + dealt territories flow):
  - https://www.risiko.sourceforge.net/doc/rules_en.php

## Official Baseline References

Checked: `2026-02-18`

- Editrice Giochi "Risiko! Classico" product page:
  - 42 territories
  - 6 armies/colors
  - 3-6 players
  - URL: `https://editricegiochi.it/prodotto/risiko-classico/`
- EGCommunity "RISIKO! Challenge" page:
  - challenge product variant exists with different envelope
  - URL: `https://www.egcommunity.it/risiko-challenge/`
- This migration targets classic objective-style digital play aligned to current in-repo canonical profile.

## Physical/Digital Component Inventory

## Board

- 1 world-classic board (`42` territories, `6` continents)
- territory adjacency from `Assets/GameData/Map/map.json`

## Army Pieces

- 6 player colors: `red`, `purple`, `yellow`, `green`, `blue`, `black`
- piece families:
  - tank mesh (primary)
  - optional fallback proxy for high counts
- per-color recommended inventory for physical-feel parity:
  - `40` base pieces minimum
  - additional pooled overflow markers for very long matches

## Dice

- Attacker dice set: at least `3`
- Defender dice set: at least `3` (profile supports defender max 3)
- Distinct materials/colors by side

## Card Decks

- Objective deck: `14` cards
- Territory deck: `42` territory cards
- Joker deck entries: `2` jokers (`joker:1`, `joker:2`)
- Total draw pile during match: `44` cards

## Player Aids (new mandatory content)

- Turn sequence reference card
- Combat resolution reference card
- Trade-in values reference card
- Objective fallback reference line for eliminate objectives

## Objective Deck (Current Official In-Repo)

Source: `Risk.Host/Gameplay/InMemoryRoomSessionService.cs` (`BuildOfficialObjectiveDeck`)

1. `obj-24`: Conquista 24 territori.
2. `obj-18-2`: Conquista 18 territori con almeno 2 armate ciascuno.
3. `obj-eu-au-plus1`: Conquista Europa, Oceania, e 1 altro continente.
4. `obj-eu-sa-plus1`: Conquista Europa, Sud America, e 1 altro continente.
5. `obj-na-af`: Conquista Nord America + Africa.
6. `obj-na-au`: Conquista Nord America + Oceania.
7. `obj-as-sa`: Conquista Asia + Sud America.
8. `obj-as-af`: Conquista Asia + Africa.
9. `obj-elim-red`: Distruggi armata rossa.
10. `obj-elim-blue`: Distruggi armata blu.
11. `obj-elim-green`: Distruggi armata verde.
12. `obj-elim-yellow`: Distruggi armata gialla.
13. `obj-elim-purple`: Distruggi armata viola.
14. `obj-elim-black`: Distruggi armata nera.

Elimination fallback rule (mandatory):

- if target color is impossible for owner, assign fallback objective:
  - `obj-fallback-24` => "Conquista 24 territori."

## Territory Card Deck Spec

## Card Count

- Exactly one card per territory ID in `map.json` => `42`.
- Plus `2` jokers.

## Symbol System

- Symbols: `infantry`, `cavalry`, `artillery`, `joker`.
- Trade validation combinations are already canonical in engine rules.

## Critical Migration Decision

Current host behavior assigns territory card symbols from shuffled territory order, so symbol tied to territory is not fixed between matches.

For Unity 3D + tabletop feel, this is locked:

- create a stable symbol manifest file per map pack (`world-classic`)
- each territory has one deterministic symbol
- total distribution stays balanced (`14` each infantry/cavalry/artillery)
- randomized per-match symbol assignment is disabled for Unity migration profile

Without this, printed/planned card sets and UI previews cannot be consistent.

## Card Visibility Rules

- Objective cards:
  - owner sees front
  - others see back only
- Territory/joker hand:
  - owner sees front
  - others see back only + count
- Discard pile:
  - top and history visible to all players

## Card State Machine

1. `in_deck`
2. `dealt_to_player`
3. `in_hand`
4. `selected_for_trade`
5. `discarded`
6. `returned_to_deck` (only if reshuffle rules are introduced)

## Rule Reference Card Copy (Authoritative)

## Turn Card

1. Rinforza: piazza tutte le armate disponibili.
2. Attacca: ripeti attacchi validi, opzionale.
3. Fortifica: un solo spostamento valido, opzionale.
4. Fine turno: pesca 1 carta solo se hai conquistato almeno 1 territorio.

## Combat Card

1. Scegli territorio attaccante e bersaglio adiacente.
2. Attaccante tira 1-3 dadi (in base alle armate disponibili).
3. Difensore tira fino al massimo consentito dal profilo.
4. Ordina i dadi in modo decrescente e confronta a coppie.
5. Il dado minore perde 1 armata, pareggio vince il difensore.

## Trade-In Card

- 3 artiglierie => 4 armate
- 3 fanti => 6 armate
- 3 cavalieri => 8 armate
- 1 fante + 1 cavaliere + 1 artiglieria => 10 armate
- 1 jolly + 2 carte stesso simbolo => 12 armate
- +2 armate per ciascuna carta territorio scambiata di territorio posseduto

## Territory Card Layout Requirements

- Front must include:
  - territory localized name
  - territory image/icon
  - symbol badge
  - continent color stripe
- Back must use pack card-back asset:
  - `Assets/Art/Cards/Backs/territory_card_back.svg`

## Objective Card Layout Requirements

- Front must include:
  - objective title
  - objective body text
  - fallback line for eliminate objectives
  - objective ID (small, for QA/debug only)
- Back must use:
  - `Assets/Art/Cards/Backs/objective_card_back.svg`

## Text Overflow And Localization Rules

- No clipping at any supported language scale.
- Minimum font size for card body in 1080p reference: `20 px`.
- If text exceeds area:
  - step 1: reduce tracking
  - step 2: reduce font to minimum
  - step 3: wrap to extra line
  - never truncate gameplay-critical text.

## Content Production Checklist

1. Finalize objective text catalog in IT and EN.
2. Finalize deterministic territory-symbol manifest.
3. Generate/validate all 42 territory fronts from manifest.
4. Generate objective fronts for all 14 objectives.
5. Validate card backs and print-safe margins.
6. Run localization overflow QA at min/max UI scale.
7. Sign off with gameplay + QA + localization owners.

## Locked Decisions

Decision date: `2026-02-18`

1. Territory symbols are deterministic by manifest, not randomized at runtime.
2. Italian (`it-IT`) is canonical source text for objectives/rule cards; English (`en-US`) is maintained translation.
3. Objective secrecy is strict: no public objective reveal/discard history during active match.
4. Spectator mode does not reveal private cards/objectives in standard lobbies.

## Implementation Notes

1. Add min-player guard (`>=3`) in host start validation before `U3D-G1`.
2. Add `world-classic` territory-symbol manifest as versioned data asset.
3. Add spectator privacy toggle only as a non-default custom-lobby setting post-MVP.
