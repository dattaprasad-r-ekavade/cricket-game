# Step 132 — CPU stroke scoring

## Scope

- Add normalized fit, intent, reward, and seeded-random scoring weights to each CPU difficulty profile.
- Score authored strokes using the classified delivery, match-phase pacing, batter style, player skill, and CPU difficulty.
- Feed the scored stroke into CPU shot and outcome selection while preserving leave, running, field placement, and timing behavior.
- Verify each scoring term, weight normalization, pacing shifts, and deterministic selection.

## Changes

- Added `CpuStrokeScorer` and `CpuBattingPacingTable` in Simulation.
- Added difficulty-specific scoring weights to the five CPU JSON profiles and Content validation.
- The Standard CPU profile's commit-time sigma is 0.08 s in the shared profile after WP21's balance calibration; the measured tuning and resulting match bands are recorded in [Step 133](step-133-match-balance.md).
- Updated `CpuBattingOutcomeModel` to use the selected authored stroke when the live caller provides a stroke table and classified ball.
- Updated `CpuLiveBattingPlanModel` to resolve the exact scored stroke through timing and contact resolution.
- Added deterministic score-term, pacing, profile, and integration tests.
- Expanded stroke-resolution repeatability coverage to replay one delivery category, input timing, and seed ten times and require the same timing zone and outcome/stroke class on every run. This provides automated coverage for the repeatability portion of section 6 item 2; its human session remains in the pending worksheet.

## Verification

- `dotnet test tests/SuperCricket.Simulation.Tests/SuperCricket.Simulation.Tests.csproj -c Release --no-restore` — passed, 298 tests.
- `dotnet test tests/SuperCricket.Content.Tests/SuperCricket.Content.Tests.csproj -c Release --no-restore` — passed, 28 tests.
- `dotnet build SuperCricket.sln -c Release --no-restore` — passed, 0 warnings and 0 errors.
- `dotnet test tests/SuperCricket.Simulation.Tests/SuperCricket.Simulation.Tests.csproj -c Release --filter FullyQualifiedName~SameDeliveryTimingAndSeedRepeatTheSameZoneAndShotClassTenTimes` — passed, 1 test.
- `dotnet test tests/SuperCricket.Simulation.Tests/SuperCricket.Simulation.Tests.csproj -c Release --no-restore` — passed, 306 tests after the repeatability check was expanded.

## Acceptance

WP21 added 20-over support. The 50-seed T20 report across Rookie, Amateur, Standard, Pro, and Legend is recorded in [Step 134](step-134-cpu-stroke-acceptance.md).
