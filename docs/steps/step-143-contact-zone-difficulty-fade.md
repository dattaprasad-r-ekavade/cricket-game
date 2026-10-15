# Step 143 — Contact-zone difficulty fade

## Finding

The full review on 16 October 2026 completed the Release and Godot builds, content validators, deterministic checks, Standard 10/20-over balance gates, and all five Human-profile balance batches. The gameplay checks then found that the batting contact-zone overlay did not decrease from Rookie through Pro or hide on Legend.

`MatchHudPresenter.GetBattingContactZoneAlpha` derived overlay opacity from the Human timing-window width. WP21's balance tuning widened the Amateur, Standard, Pro, and Legend windows to 7.2 m, so that visual mapping stopped differentiating those levels even though the intended display ladder and its existing test remained in place.

The follow-up review passed that check and all five Human-profile batches, then found that the 120 fps attacking-shot review timed out before the fielding sequence resolved. Its six-second post-contact allowance was too short for the new 62.5 m ground and slower outfield play.

## Change

Make the overlay fade follow the selected difficulty directly: Rookie 220, Amateur 180, Standard 120, Pro 60, and Legend 0. Timing-window values continue to control batting physics; presentation opacity no longer changes when balance tuning changes those windows.

Give the 30/60/120 fps contact review 12 seconds to finish the post-contact fielding sequence before it reports a failure.

## Verification

- `dotnet test tests/SuperCricket.Game.Tests/SuperCricket.Game.Tests.csproj -c Release --filter FullyQualifiedName~BattingContactZoneAlphaStepsDownWithDifficulty` — passed, 1 test.
- `dotnet run --project src/SuperCricket.Game -c Release -- --verify-gameplay` — passed all gameplay review checks, including the 30/60/120 fps contact and resolution cases.
- `pwsh tools/review.ps1 -RunGameChecks -CaptureVisuals` — passed with exit code 0 after this fix. Release and Godot builds had 0 warnings/errors; animation/content validators, both Standard balance runs, all five Human-profile batches, gameplay/live-match checks, and requested renderer captures passed.
- `dotnet test SuperCricket.sln -c Release --no-build` — passed on 16 October 2026: 544 tests (308 Simulation, 203 Game, 33 Content), 0 failed and 0 skipped.
- The review measured 10-over Standard at 6.11 RPO and 3.36 wickets/innings, and T20 Standard at 6.58 RPO and 7.07 wickets/innings. Human-profile batch numbers and CSVs are in [Step 134](step-134-cpu-stroke-acceptance.md).
