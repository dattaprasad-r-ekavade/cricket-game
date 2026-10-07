# Step 101 — Default the review to headless mode

## Change

`tools/review.ps1` now defaults to build, asset validation, analyzers, and simulation checks without starting the game host or creating renderer captures. The host-dependent checks require `-RunGameChecks`; renderer profiling and captures require `-CaptureVisuals`. The existing `-SkipGame` and `-SkipCaptures` switches remain accepted as opt-outs for current automation.

Updated the tool reference with the default behavior and explicit opt-in commands.

## Verification

- `dotnet test SuperCricket.sln -c Release`: all 127 tests passed (22 Simulation, 98 Game, 7 Content).
- `pwsh -NoProfile -File tools/review.ps1`: passed in the default headless mode, including both Release builds, asset validation, timing calibration, deterministic match batches, and six 10-over physics matches.
- The review reported that game checks and captures are opt-in; neither the game host nor renderer captures were started.

## Remaining

Visual captures still need a person to review them when explicitly generated. Human playtests and the art/animation gates in `plan.md` remain open.
