# Step 121 — Player ratings and identity

## Behaviour

- Moved team rosters to version 2 and added batting, bowling, and fielding rating groups, batting style, handedness, and bowler style.
- Version-1 rosters migrate at load time. Batting ratings derive from legacy timing and power; bowling and fielding ratings derive from their matching legacy skills.
- Added validator warnings for recommended rating bands, batting spread, specialist fielding bands, and unknown style values. The `validate-team` tool prints those warnings and the version-2 ratings.
- Connected stroke-specific batting ratings to the timing-window skill bonus, bowling accuracy to delivery decisions, fielding speed to pursuit, catching to seeded catch holds, and throwing arm/accuracy to return throws.
- Regenerated the Coastal XI and Highland XI assets with version-2 ratings. Their regular fielding ratings are 60–90; wicketkeepers use the specialist allowance.

## Files

- `src/SuperCricket.Content/TeamRosterAsset.cs`
- `src/SuperCricket.Simulation/Batting/PlayerBattingRatingModel.cs`
- `src/SuperCricket.Simulation/Batting/TimingWindowModel.cs`
- `src/SuperCricket.Simulation/Fielding/CatchingModel.cs`
- `src/SuperCricket.Simulation/BattedBallFieldingModel.cs`
- `src/SuperCricket.Simulation/PhysicsAutomatedMatchBatchSimulator.cs`
- `src/SuperCricket.Simulation/CpuBattingOutcomeModel.cs`
- `src/SuperCricket.Simulation/CpuLiveBattingPlanModel.cs`
- `src/SuperCricket.Simulation/LimitedOversMatch.cs`
- `src/SuperCricket.Game/MatchController.cs`
- `src/SuperCricket.Game/Game1.MatchLoop.cs`
- `src/SuperCricket.Tools/Program.cs`
- `assets/teams/coastal-xi.json`
- `assets/teams/highland-xi.json`
- `tests/SuperCricket.Content.Tests/TeamRosterMigrationTests.cs`
- `tests/SuperCricket.Simulation.Tests/TimingWindowModelTests.cs`
- `tests/SuperCricket.Simulation.Tests/CpuBattingOutcomeReviewChecks.cs`
- `tests/SuperCricket.Simulation.Tests/CpuLiveBattingPlanReviewChecks.cs`
- `tests/SuperCricket.Simulation.Tests/LimitedOversMatchReviewChecks.cs`
- `tests/SuperCricket.Game.Tests/MatchControllerTests.cs`
- `docs/build/a5-build-plan.md`

## Verification

- Version-1 migration and version-2 rating-band/spread validator tests passed. Both team assets load and validate without warnings.
- `dotnet build -c Release` — passed with 0 warnings and 0 errors.
- `dotnet test -c Release` — passed, 447 tests.
- `dotnet run -c Release --no-build --project src/SuperCricket.Tools -- validate-team assets/teams/coastal-xi.json` — passed.
- `dotnet run -c Release --no-build --project src/SuperCricket.Tools -- validate-team assets/teams/highland-xi.json` — passed.
- `pwsh tools/review.ps1` — passed. The six seeded 10-over matches averaged 111.5 runs per innings, with scores from 79 to 136 and 4 run-outs among 52 dismissals (7.7%).

## Follow-on integration

- The overload that takes a resolved foot and shot direction supplies WP2's timing-window skill bonus to WP3's stroke-resolution path.
