# Unity 3D Migration Pack

This folder defines the full plan to rebuild RisiKo! as a Steam-distributed Unity 3D EXE (board on table, physical-feel dice/tanks, host-authoritative multiplayer).

## Scope

- Reuse deterministic rules from `Risk.Engine` where possible.
- Replace current web presentation with Unity 3D desktop EXE client.
- Keep host-authoritative multiplayer model.
- Integrate Steam SDK for lobby, invites, identity, and networking.
- Preserve replayability, auditability, and reconnect reliability.

## Documents

1. `01-executive-brief.md`
2. `02-product-and-experience-spec.md`
3. `03-technical-architecture.md`
4. `04-networking-and-multiplayer.md`
5. `05-gameplay-integration-risk-engine.md`
6. `06-art-audio-and-content-pipeline.md`
7. `07-production-plan-roadmap.md`
8. `08-staffing-and-budget.md`
9. `09-quality-test-strategy.md`
10. `10-devops-release-and-liveops.md`
11. `11-risk-register.md`
12. `12-definition-of-done-and-gates.md`
13. `13-physical-animation-and-tabletop-choreography.md`
14. `14-rules-cards-and-components-bible.md`
15. `15-card-copy-and-localization-catalog.md`
16. `16-steamworks-integration-plan.md`
17. `17-repository-strategy-two-repo.md`
18. `18-steam-message-schema.md`
19. `19-contract-versioning-and-changelog-workflow.md`
20. `20-implementation-backlog-milestone-aligned.md`
21. `21-id-based-delivery-plan.md`
22. `22-rules-freeze-signoff.md`
23. `23-asset-reorg-and-usage.md`
24. `24-prestart-readiness-checklist.md`

## How To Use

1. Read `01-executive-brief.md` for constraints and recommendation.
2. Validate technical direction in `03` to `05`.
3. Approve staffing and budget assumptions in `08`.
4. Lock milestones in `07`.
5. Run delivery against `09`, `10`, and `12`.
6. Use `13` to implement physical tank/dice/card presentation.
7. Use `14` and `15` as the source of truth for card/rules content.
8. Use `16` as the Steam SDK implementation baseline.
9. Use `17` to run Unity + engine/host as separate repos safely.
10. Use `18` and `19` to keep host/client protocol changes safe.
11. Execute delivery from `20`.
12. Track milestone sequencing from `21`.
13. Treat `22` as the rule/content freeze baseline before implementation.
14. Use `23` as current asset location and required/optional inventory reference.
15. Use `24` as the immediate go/no-go checklist before gameplay coding.
16. Use `Tools/generate_card_svgs.py` + `Tools/validate_content.ps1` after content changes.
