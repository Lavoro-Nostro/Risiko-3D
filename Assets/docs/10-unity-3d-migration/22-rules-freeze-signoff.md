# Rules Freeze Signoff

Date: `2026-02-18`
Status: `APPROVED FOR IMPLEMENTATION START`

## Frozen Baseline

1. Canonical gameplay profile: `RisiKo!_OBJECTIVE_CLASSICO_IT_V1`
2. Player count: `3-6`; host match-start minimum is `3`
3. Objective deck baseline: `14` objective cards + fallback objective logic
4. Territory deck baseline: `42` territory cards + `2` jokers
5. Territory symbols: deterministic manifest per map (`14/14/14` split)
6. Objective visibility: private to owner; no public reveal during active match
7. Localization authority: `it-IT` canonical, `en-US` maintained translation

## Rule Sources

1. `Assets/docs/10-unity-3d-migration/14-rules-cards-and-components-bible.md`
2. `Assets/docs/10-unity-3d-migration/15-card-copy-and-localization-catalog.md`
3. Editrice Giochi product baseline (`Risiko! Classico`): `https://editricegiochi.it/prodotto/risiko-classico/`
4. EGCommunity product-variant reference (`RISIKO! Challenge`): `https://www.egcommunity.it/risiko-challenge/`

## Start Gate

Implementation can begin on `U3D-M0` and `U3D-M1` scope using this freeze. Any rule-impacting change after this point must follow:

- `Assets/docs/10-unity-3d-migration/19-contract-versioning-and-changelog-workflow.md`
