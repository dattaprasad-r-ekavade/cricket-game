# Step 107 — Continuous running and correct run-out identity

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

## Scope

Correct instant turn-back, batter replacement after crossing, and the batch return-throw clock. Preserve the current controls and authored clips.

## Changes

- Added graphics-free `BetweenWicketsState`. Turning back preserves both positions and reverses progress; returning home scores no additional run. Completed runs remain credited, and queued runs consume the unused part of a frame.
- Runner positions freeze when a delivery ends and reset at the next delivery, rather than snapping to the original ends at result time.
- Added a graphics-device-free placement presenter used by the renderer, with tests for movement, facing, completed runs, and frozen result positions.
- Added a return-target stump-contact check. The host requires prior fielder possession, ball release, and a target within the stump bounds before resolving a run-out.
- Run-out resolution supports either wicket and retains the previous ground owner when both runners draw level. Delivery results separately record an end change during an uncompleted run; the scorecard applies this before replacing the batter and applying any over-end swap.
- Fixed physics batch throw arrival to use elapsed time since bat contact. It now uses the runner ground model to select the dismissed batter instead of always replacing the striker.
- Live running advances before terminal ball events, starts only after bat contact within a tick, and is capped at the authored throw's arrival time when a render frame extends past it.
- Updated opt-in host review cases to check turn-back positions and queued-run cancellation. No game-host checks were started.

Ground identity and end placement follow Laws 30.2.3, 38.4, and 18.12.1 of the [official 2026 MCC Laws](https://www.lords.org/getmedia/a4ef9f77-2a25-4f5b-a286-4601f08e6334/Laws-of-Cricket-2017-Code-4th-Edition-%282026%29_5.pdf).

## Verification

- `dotnet test SuperCricket.sln -c Release --no-restore`: all 258 tests passed (Content 9, Simulation 142, Game 107), including 78 new cases.
- The run-out matrix covers both wickets, before/after crossing, zero/one/two completed runs, legal/no-ball deliveries, and over-end transitions.
- Movement tests cover repeated turn-back requests, return-home safety, 30/60/120 Hz updates, queued-run frame remainder, and delivery-reset behavior.
- Batch pickup/throw fixtures reproduce the old absolute-clock error and cover the dismissed batter on either side of the crossing point.
- `pwsh -NoProfile -File tools/review.ps1`: passed in default headless mode, including both builds, content validation, seeded physics batch repeatability, and batting calibration.
- Hosted validation: code commit `9df19502089c1522cfebc5b6b3d8065a55273926` passed in [run 37612758525](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37612758525).
- No game window or visual capture was started.

## Remaining

The ground model uses normalized straight-line endpoints to represent a grounded batter; exact bat/body grounding at the authored popping crease and visual turn-back quality still need validation. Current live return throws target the near wicket, while the shared resolver supports either end. Resting-ball fielding, independent non-striker animation, beginner delivery reachability, and the human keyboard/GamePad gate remain open.
