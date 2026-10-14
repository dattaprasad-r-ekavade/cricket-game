# Step 119 — LBW decision model

## Behaviour

- Added a deterministic LBW projection from pad/body impact to the stumps, including one pitch bounce when the ball has not yet pitched.
- Applied the pitched-outside-leg and offered-shot/outside-off rules, hitting and umpire's-call bands, missing width, and going-over height. Umpire's-call outcomes use a seeded probability.
- Added LBW dismissal accounting to match sessions and physics batch results. The batch path checks a fixed stance pad capsule before the wicket line; no-ball and wide deliveries cannot produce LBW dismissals.

## Files

- `src/SuperCricket.Simulation/Umpiring/LbwModel.cs`
- `src/SuperCricket.Simulation/DeliveryResult.cs`
- `src/SuperCricket.Simulation/DeliverySession.cs`
- `src/SuperCricket.Simulation/AutomatedMatchBatchSimulator.cs`
- `src/SuperCricket.Simulation/PhysicsAutomatedMatchBatchSimulator.cs`
- `src/SuperCricket.Tools/Program.cs`
- `tests/SuperCricket.Simulation.Tests/LbwModelTests.cs`
- `docs/build/a5-build-plan.md`

## Verification

- Tests cover hitting, umpire's call, missing, going over, pitched outside leg, offered shot outside off, one-bounce projection, seeded call frequency, and illegal-delivery restrictions.
- A seeded batch of 50 ten-over matches produced 58 LBWs among 324 dismissals (17.9%), inside the 8–18% target.
- `dotnet build SuperCricket.sln -c Release` — passed, 0 warnings and 0 errors.
- `dotnet test SuperCricket.sln -c Release --no-build` — passed, 438 tests.
- `pwsh tools/review.ps1` — passed.

## Open questions

- The batch path uses a fixed pad capsule at the exported batter's stance. The live host, appeal/decision events, umpire presentation, and replay strip remain in WP9b/WP11.

## Playtest checklist

- Confirm a center-line pad impact under the stumps is given out and the verdict matches the projection.
- Confirm hitting the ball outside the off-stump line is not out, while leaving that same ball can be given out.
- Confirm pitching outside leg is always not out and an umpire's-call verdict varies within the selected difficulty probability.
- Confirm wide and no-ball deliveries never produce an LBW dismissal.
