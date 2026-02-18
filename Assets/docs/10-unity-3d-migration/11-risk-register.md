# Unity 3D Migration Risk Register

## Legend

- Probability: Low / Medium / High
- Impact: Low / Medium / High

## Risks

1. `U3D-RSK-001` Rule divergence between Unity and `Risk.Engine`
   - Probability: Medium
   - Impact: High
   - Mitigation: keep rules in engine only, replay parity tests.

2. `U3D-RSK-002` Multiplayer desync under reconnect/load
   - Probability: Medium
   - Impact: High
   - Mitigation: strict sequence model, snapshot + missed event recovery tests.

3. `U3D-RSK-003` Scope expansion in 3D polish
   - Probability: High
   - Impact: High
   - Mitigation: lock scope at `U3D-M1`, gate polish behind stability.

4. `U3D-RSK-004` Performance regression on low-tier hardware
   - Probability: Medium
   - Impact: Medium
   - Mitigation: early profiling budgets, asset LOD/optimization.

5. `U3D-RSK-005` Asset pipeline bottlenecks
   - Probability: Medium
   - Impact: Medium
   - Mitigation: naming/version standards, early pipeline rehearsal.

6. `U3D-RSK-006` Inadequate QA depth for network edge cases
   - Probability: Medium
   - Impact: High
   - Mitigation: automated soak tests and dedicated network fault scenarios.

## Review Cadence

- Weekly risk review in production sync.
- Re-score each risk at milestone boundaries.

