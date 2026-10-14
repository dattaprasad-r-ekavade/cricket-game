# Step 115 — Zone-based stroke table

## Behaviour

- Added a versioned 40-entry stroke table: eight directions with front/back defend and attack strokes, plus eight loft strokes.
- Each stroke defines authored Start, Early, Ideal, Late, and End outcomes. TooEarly and TooLate remain global beaten results.
- Early and Late outcomes lose speed, gain elevation, and skew across or toward the fine/keeper side. Ideal attack outcomes stay low and fast; Ideal defence outcomes stay under 12 m/s.
- Added Content loading and validation for zone completeness and ranges, animation-contract clip names, suitability filters, unique IDs, and the direction/intent/foot matrix. Unusual direction/line pairs report warnings without failing validation.
- Added `Mirror()` to create a left-handed table with yaw negated.
- Added a graphics-free Contracts assembly shared by Content and Simulation so the JSON asset can use the WP0 enums without introducing a circular project reference.
- Added the `validate-strokes` CLI command and included it in the headless review workflow. The legacy `shots.json` remains in place for the current runtime until WP3 migrates its uses.

## Files

- `src/SuperCricket.Contracts/SuperCricket.Contracts.csproj`
- `src/SuperCricket.Content/StrokeTableAsset.cs`
- `src/SuperCricket.Content/SuperCricket.Content.csproj`
- `src/SuperCricket.Simulation/SuperCricket.Simulation.csproj`
- `SuperCricket.sln`
- `assets/batting/strokes.json`
- `src/SuperCricket.Tools/Program.cs`
- `tools/review.ps1`
- `tests/SuperCricket.Content.Tests/StrokeTableAssetTests.cs`
- `docs/build/a5-build-plan.md`

## Verification

- `dotnet run --project src/SuperCricket.Tools/SuperCricket.Tools.csproj -c Release -- validate-strokes assets/batting/strokes.json` — passed; 40 strokes validated.
- `dotnet build SuperCricket.sln -c Release` — passed, 0 warnings and 0 errors.
- `dotnet test SuperCricket.sln -c Release` — passed, 380 tests.
- `pwsh tools/review.ps1` — passed, including the new `validate-strokes` route.

## Open questions

- None. WP5's planned same-side selector fallback can preserve the requested foot when a direction has no matching loft-foot entry.

## Playtest checklist

- Once WP3 wires this table into live batting, check the 8 directions across front/back attack, defence, and loft inputs.
- Confirm Ideal strokes are the fastest and lowest; Early/Late are softer and skew across or finer; Start/End are high and catchable; TooEarly/TooLate are beaten.
- Check the mirrored yaw for left-handed batting.
