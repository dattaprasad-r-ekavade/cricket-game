# Step 118 — Bowling model

## Behaviour

- Added a typed delivery catalogue with eight pace options, four off-spin options, and five leg-spin options. Every entry carries its speed range, movement parameters, and spinners carry pitch-specific spin multipliers.
- Added seeded target scatter and radius from accuracy and spell rhythm. Rookie and Amateur have zero landing scatter; the target tightens over the first ten balls and expands as stamina drops below 50.
- Added stamina loss per over and a three-band stride meter with the final eight percent treated as a front-foot no-ball. A missing press uses normal pace.
- Catalogue selections produce a `DeliveryPreset` aimed through `BowlingAimModel.AimForPitchTarget` with the chosen speed and pitch spin metadata.

## Files

- `src/SuperCricket.Content/Bowling/DeliveryCatalogue.cs`
- `assets/bowling/deliveries.json`
- `src/SuperCricket.Content/DeliveryPreset.cs`
- `src/SuperCricket.Simulation/Bowling/BowlingAccuracyModel.cs`
- `src/SuperCricket.Tools/Program.cs`
- `tools/review.ps1`
- `tests/SuperCricket.Content.Tests/DeliveryCatalogueTests.cs`
- `tests/SuperCricket.Simulation.Tests/BowlingModelTests.cs`
- `docs/build/a5-build-plan.md`

## Verification

- Catalogue validator accepts all 17 entries; tests reject invalid speed ranges and missing pitch multipliers.
- Tests cover deterministic scatter, no scatter on Rookie and Amateur, rhythm ramp, stamina decay and radius growth, all meter bands, no-ball threshold, and no-press default.
- A seeded batch of 100 speed choices per entry (1,700 deliveries total) bounced inside the pitch for centered targets.
- `dotnet build SuperCricket.sln -c Release` — passed, 0 warnings and 0 errors.
- `dotnet test SuperCricket.sln -c Release --no-build` — passed, 432 tests.
- `pwsh tools/review.ps1` — passed.

## Open questions

- Spin parameters and pitch multipliers are carried by generated delivery presets; the current flight simulator does not yet model spin-induced turn after the bounce.

## Playtest checklist

- Confirm Rookie and Amateur target markers have no random landing offset.
- Confirm the target radius tightens during the first ten balls and expands after stamina falls below 50.
- Confirm the pace meter reads Slow / Normal / Fast in the lower, middle, and upper bands, and the final 8% displays NO BALL.
- Confirm no press selects Normal pace.
