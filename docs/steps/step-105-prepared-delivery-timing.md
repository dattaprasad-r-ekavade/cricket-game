# Step 105 — Measure timing from the prepared delivery

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

## Scope

Correct the existing live batting guide and result timing after Rookie/first-match pacing and CPU bowling variation. A stock delivery name cannot identify the timing of a physically different ball.

## Changes

- Added a contact-only timing measurement against the actual prepared delivery, exported humanoid clips, and selected footwork position. It scans the same 0.025 s input grid as the analyzer without simulating outgoing flight.
- Normal play prepares measurements in the background during the run-up. Verification and explicitly requested capture workflows measure synchronously for repeatability.
- Replacing a delivery or footwork target cancels its previous request and hides the old guide. A shot retains its own timing request for the result card. Footwork transitions do not receive a static-position timing assessment.
- Background failures are observed and logged; pending, cancelled, failed, and unreachable measurements return no timing rather than a guessed stock target.
- Added regression cases for slowed stock deliveries, CPU variations, unreachable balls, wide-ball footwork, cancellation, stale results, and failed background work.

## Verification

- `dotnet test SuperCricket.sln -c Release --no-restore`: all 145 tests passed (Content 9, Simulation 32, Game 104).
- The targeted timing filter passed all 19 tests.
- Slowed standard pace measures defence at 0.550 s and drive/loft at 0.525 s. The previous stock drive target of 0.225 s misses this slowed ball.
- Rookie CPU seed 491 cannot be contacted from centered feet, but becomes reachable at +0.45 m. The new test explicitly checks both cases instead of assuming every variation can be hit at the center.
- `pwsh -NoProfile -File tools/review.ps1`: passed in default headless mode, including both builds, content validation, match batches, analyzer calibration, and repeatability checks.
- Code committed as `f93cc38c77d23ca4b30019593cc8477e17d5c208`; hosted Windows validation passed in [run 37608721560](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37608721560).
- No game window or visual capture was started.

## Remaining

Human keyboard/GamePad validation remains open. Beginner delivery reachability requires a separate correction: normal human batting has no automatic footwork yet. Running stoppage, physical turn-back, correct run-out batter identity, and independent non-striker animation remain separate work items. This step does not close those gates.
