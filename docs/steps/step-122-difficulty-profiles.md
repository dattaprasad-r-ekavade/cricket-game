# Step 122 — Difficulty profiles

## Behaviour

- Added versioned, validated difficulty profile assets for Rookie, Amateur, Standard, Pro, and Legend, each with Human and CPU variants for limited-overs cricket.
- Human profiles narrow the timing window and reduce assistance as the level rises. CPU profiles invert that progression: stronger batting weights, smaller commit sigma, and stronger CPU tuning at higher levels.
- Added the Content loader/catalog, completeness and duplicate checks, a `validate-difficulty` command, and game startup loading from the copied profile assets.
- `CpuDifficultyModel` now reads its tuning and profile selection from the catalog and cycles through all five levels.
- Added profile adapters for timing windows and assistance, bowling target radius/scatter and player accuracy/stamina, pace-meter fill time, and umpire's-call probability. The physics batch uses the Standard umpiring profile.
- Updated the difficulty contact-zone cue to derive from the selected Human timing window.

## Files

- `src/SuperCricket.Content/DifficultyProfileAsset.cs`
- `src/SuperCricket.Content/DifficultyProfileCatalog.cs`
- `assets/difficulty/limited-*.json` (10 profiles)
- `src/SuperCricket.Simulation/CpuDifficultyModel.cs`
- `src/SuperCricket.Simulation/Batting/TimingWindowModel.cs`
- `src/SuperCricket.Simulation/Bowling/BowlingAccuracyModel.cs`
- `src/SuperCricket.Simulation/Umpiring/LbwModel.cs`
- `src/SuperCricket.Simulation/PhysicsAutomatedMatchBatchSimulator.cs`
- `src/SuperCricket.Game/Game1.ContentLifecycle.cs`
- `src/SuperCricket.Game/Game1.Feedback.cs`
- `src/SuperCricket.Game/MatchHudPresenter.cs`
- `src/SuperCricket.Game/SuperCricket.Game.csproj`
- `src/SuperCricket.Tools/Program.cs`
- `tests/SuperCricket.Content.Tests/DifficultyProfileAssetTests.cs`
- `tests/SuperCricket.Simulation.Tests/BowlingDecisionReviewChecks.cs`
- `tests/SuperCricket.Simulation.Tests/BowlingModelTests.cs`
- `tests/SuperCricket.Simulation.Tests/LbwModelTests.cs`
- `tests/SuperCricket.Simulation.Tests/TimingWindowModelTests.cs`
- `tests/SuperCricket.Simulation.Tests/SuperCricket.Simulation.Tests.csproj`
- `tests/SuperCricket.Game.Tests/MatchHudPresenterTests.cs`
- `docs/build/a5-build-plan.md`

## Verification

- `dotnet build -c Release` — passed with 0 warnings and 0 errors.
- `dotnet test -c Release` — passed, 453 tests.
- `dotnet run -c Release --no-build --project src/SuperCricket.Tools -- validate-difficulty assets/difficulty` — passed; all ten profiles loaded.
- `pwsh tools/review.ps1` — passed, including seeded physics balance and replay checks.
