# Step 116 — Distance-based timing window

## Behaviour

- Added a seven-band `TimingWindow` in metres from the contact plane. The ranges follow the plan's centre/window/ideal formulas, and exact cutoffs resolve into the nearer band.
- Added `IdealWidth` with loft reduction, a piecewise settle curve, confidence scaling, a capped skill bonus, and the configured minimum.
- Added `BatterTimingState`, starting at 0.6 confidence and registering faced balls with clamped confidence adjustments for Ideal timing, beaten balls, and body hits.
- Added `DistanceToContactPlane`, which measures along the current ball velocity and returns a signed distance.
- Added the reusable `TimingDifficulty` data shape and default settle curve to the graphics-free Contracts assembly for WP15's difficulty assets.
- Added beginner assistance that promotes Early and Late to Ideal.

## Files

- `src/SuperCricket.Contracts/Batting/TimingDifficulty.cs`
- `src/SuperCricket.Simulation/Batting/TimingWindowModel.cs`
- `tests/SuperCricket.Simulation.Tests/TimingWindowModelTests.cs`
- `docs/build/a5-build-plan.md`

## Verification

- `dotnet build SuperCricket.sln -c Release` — passed, 0 warnings and 0 errors.
- `dotnet test SuperCricket.sln -c Release` — passed, 400 tests.
- `pwsh tools/review.ps1` — passed.

## Open questions

- None.

## Playtest checklist

- Once WP3 and WP6 wire the model into live batting and the gauge, repeat identical inputs at different delivery speeds and confirm they classify from distance consistently.
- Check all six zone boundaries, the narrowing Ideal band across difficulty levels, its widening as the batter settles, the loft reduction, and the Rookie assist.
