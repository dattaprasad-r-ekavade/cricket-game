# Step 138 — Live umpiring and run-out integration

## Scope

Completes WP9b/WP10b by connecting LBW and run-out simulation decisions to the live match, controller input, camera direction, HUD signals, and replay events.

## Changes

- Added a live pad-contact detector that shares the authored contact geometry with the physics batch simulator. Live contact records the delivery, ball path and shot-offered state for `LbwModel`.
- CPU fielders appeal automatically on hitting and umpire's-call candidates. When the player controls the bowling side, A/X opens a short appeal window; a timed-out appeal resumes play. A successful appeal publishes the projected stump impact and decision band, and resolves an LBW dismissal when the rules say out.
- Added the Appeal controller action and keyboard input, plus HOWZAT / OUT / NOT OUT presentation and prompts for the bowling player.
- Added the umpire close-up camera preset and use it for LBW, run-out and near-run-out reviews.
- Connected live wicket breaks to `RunOutModel.ResolveAtBrokenWicket`; near misses publish their crease distance and can trigger the safe signal and replay.
- Added replay event capture for live LBW and run-out decisions. The replay work is documented in [Step 137](step-137-replays.md).

## Verification

- `dotnet build SuperCricket.sln -c Release` — passed with 0 warnings and 0 errors.
- `dotnet test SuperCricket.sln -c Release --no-build` — passed, 534 tests (306 Simulation, 198 Game, 30 Content), including appeal input routing, umpire camera framing, HUD signals, and run-out resolution.
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --verify-gameplay` — passed. The live LBW review constructs pad contact, verifies the projected stump impact and decision band, resolves the wicket, and checks the umpire camera.
- `pwsh tools/review.ps1 -RunGameChecks -CaptureVisuals` — passed, including all balance gates, live CPU traces, and gameplay review checks.
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --capture-frame artifacts/review-lbw-replay.png --lbw-replay-preview` — passed; inspected the resulting stump-line overlay and impact marker.

## Human playtest checklist

- Bowl a delivery that hits the pad while controlling the bowling side; confirm the A/X appeal prompt appears only during the window, a pressed appeal shows the correct signal, and no appeal resumes the ball.
- Bowl both an out and not-out LBW trajectory; confirm the close-up, decision banner, replay and stump-line strip explain each result.
- Play a run-out and a near miss; confirm OUT / SAFE, the close-up camera and crease replay communicate whether the grounded bat was behind the line.

## Artifact

- [LBW replay preview](../../artifacts/review-lbw-replay.png)
