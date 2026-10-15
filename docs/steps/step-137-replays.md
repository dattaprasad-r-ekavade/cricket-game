# Step 137 — Match replays

## Scope

WP11 records rendered match state for scripted replays. Playback consumes the recorded frames and never advances the match simulation.

## Changes

- Added `ReplayRecorder`, a time-windowed ring buffer with monotonic timestamps and ordered snapshots. Frames retain ball state, recorded camera pose, actor world transforms, clip names and times, sampled skin matrices, and the ball trail.
- Added script assets for bowled, caught, LBW, run-out, six, and near-run-out events. Scripts validate camera names, timing, playback speed, and overlay/event compatibility.
- Added event capture for wickets, sixes, LBW decision coordinates, and near-run-out crease distance.
- Added playback sampling at authored speeds and replay overlay rendering for stump-line and crease reviews.
- Added recorder, playback, script validation, timestamp, and skip tests; added a `validate-replay-scripts` tools command.
- Connected replay capture to the live LBW, run-out, near-run-out, dismissal, and six events. Innings wicket bookmarks queue the fall-of-wickets reel with captions.

## Verification

- `dotnet build SuperCricket.sln -c Release` — passed with 0 warnings and 0 errors.
- `dotnet test SuperCricket.sln -c Release --no-build` — passed, 534 tests (306 Simulation, 198 Game, 30 Content).
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --verify-gameplay` — passed, including live LBW projection, dismissal, and umpire review framing.
- `dotnet src/SuperCricket.Tools/bin/Release/net9.0/SuperCricket.Tools.dll validate-replay-scripts assets/replays/replay-scripts.json` — passed for all six authored event types.
- `pwsh tools/review.ps1 -RunGameChecks -CaptureVisuals` — passed. This includes the full Release and Godot builds, validators, balance gates (10-over: 6.11 RPO; 20-over: 6.63 RPO), deterministic live-match checks, game checks, and requested captures.
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --capture-frame artifacts/review-lbw-replay.png --lbw-replay-preview` — passed; inspected the capture and confirmed the replay banner, skip hint, stump-line strip, wicket markers, and projected-impact marker are visible. The skip path is also covered by the replay playback tests.

WP9b/WP10b live-host dependencies are complete in [Step 138](step-138-live-umpiring-and-run-outs.md).

## Artifact

- Script catalog: [replay-scripts.json](../../assets/replays/replay-scripts.json)
- [LBW replay preview](../../artifacts/review-lbw-replay.png)
