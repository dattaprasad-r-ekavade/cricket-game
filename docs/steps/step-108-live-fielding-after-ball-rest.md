# Step 108 — Live fielding after ball rest and no-ball collection

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

## Scope

Correct delivery completion that cut off fielding, running, and return throws when a batted ball stopped moving or a fielder held a no-ball. Keep the existing controls and authored collection/throw clips.

## Changes

- Added graphics-free `BattedBallFieldingModel`, used by the live host and actual batch simulator for collection decisions and by all three fielding paths for continued chase/contact detection. Physical rest does not manufacture dead-ball scoring or a dismissal.
- The live fixed-step loop continues chasing and running around a resting ball, stops physics stepping during the collection/return sequence, and avoids advancing runners twice in a render frame. Frozen ball positions do not grow the trajectory vertex list.
- Live batted-ball physics, outgoing trajectory analysis, and CPU running lookahead continue past the preset's laboratory simulation limit. The analyzer has a separate diagnostic budget; exhaustion reports failure without completing a delivery.
- Batch trajectories extend physically resting frames until a fielder collects the ball. Their collection budget is a diagnostic failure guard rather than a scoring event.
- CPU running uses elapsed fielding time, which continues after the ball's physical clock stops. Runs must complete before the return; a resting ball no longer grants a partially crossed run automatically.
- Fielders use the actual delivery boundary radius, replacing the fixed 40 m movement limit that could strand resting balls inside the 42 m boundary.
- A catch off a no-ball retains completed runs and chooses the return sequence while runners move. Legal catches still dismiss and void runs. The host uses the catch clip and its secured event before throwing; CPU and batch paths receive the actual catch duration.
- Shared collection decisions and complete batch pickup/return paths have headless regression coverage. Opt-in host checks were updated but were not started.

The dead-ball decision and no-ball behavior follow Laws 20.1.2, 21.13, and 21.17 of the [official 2026 MCC Laws](https://www.lords.org/getmedia/a4ef9f77-2a25-4f5b-a286-4601f08e6334/Laws-of-Cricket-2017-Code-4th-Edition-%282026%29_5.pdf).

## Verification

- `dotnet test SuperCricket.sln -c Release --no-restore`: all 302 tests passed (Content 9, Simulation 186, Game 107).
- Added 44 cases: 24 resting-ball cases and 20 no-ball collection cases. They cover the live advance gate, fixed-step continuation at 30/60/120 Hz, actual batch scoring/run-outs after rest, boundary collection, laboratory cutoff bypass, diagnostic exhaustion, legal/no-ball catch routing, authored catch/return windows, and invalid inputs.
- The Step 106 fixture expectations now require physical collection and completed runs; explicit dead-ball scoring tests remain unchanged.
- Default headless review: passed on the final source, including both builds, content validation, seeded physics batch repeatability, and batting calibration.
- Hosted validation: pending.
- No game window, game-host check, capture, or runtime profiling session was started.

## Remaining

Beginner delivery reachability, independent non-striker animation, exact bat/body grounding at popping creases, visual collection/turn-back quality, and human keyboard/GamePad acceptance remain open. Live returns still target the near wicket and use the existing authored throw arc. Incoming unbatted wides/no-balls still finish at the wicket line; byes/leg-byes and broader cricket-law completeness are not established by this correction.
