# Step 106 — Correct running credit at dead ball

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

## Scope

Remove invented run-outs when the prototype declares a ball dead, and credit the run in progress when the batters have crossed. Apply the same rule to human play, CPU running decisions, and deterministic physics match batches.

The rule follows MCC Law 18.9: completed runs stand, and a crossed run in progress is credited when the ball becomes dead other than at a dismissal. A run-out requires the separate conditions of Law 38. See the [official 2026 Laws, pages 56–57 and 112–115](https://www.lords.org/getmedia/a4ef9f77-2a25-4f5b-a286-4601f08e6334/Laws-of-Cricket-2017-Code-4th-Edition-%282026%29_5.pdf).

## Changes

- Replaced `ResolveRunAtStoppage`, which could dismiss a runner on a legal ball, with `ResolveDeadBall`. This operation only credits a crossed run and rejects use after a dismissal.
- Added `RunningScoringModel` to share the halfway crossing rule and capped dead-ball run count for the current equal-speed, straight-line runner model.
- The live host now retains earlier runs, credits a crossed run, and reports a dot/run result instead of inventing a wicket.
- CPU decisions and physics match batches use the same crossing rule instead of the old 72% cutoff.
- Added headless session, strike/over transition, CPU ball-flight, and batch-trajectory regression tests, including legal/no-ball cases and the formerly incorrect 60% crossing case.
- Added live-host review cases for legal/no-ball stoppage before and after crossing. They remain opt-in and were not executed because the game must stay closed.

## Verification

- `dotnet test SuperCricket.sln -c Release --no-restore`: all 180 tests passed (Content 9, Simulation 67, Game 104), including 35 new dead-ball regression cases.
- `pwsh -NoProfile -File tools/review.ps1`: passed in default headless mode, including both builds, content validation, physics match replay, and batting repeatability.
- Code committed as `18d288d6e2a50b11a1ecb0d1a1692bca8c51a3da`; hosted Windows validation passed in [run 37610316749](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37610316749).
- No game window or visual capture was started.

## Remaining

This fixes scoring after a declared dead ball. The prototype still conflates a physically resting ball or simulation timeout with cricket Dead ball; fielders must be allowed to collect a resting ball in a subsequent correction. Physical turn-back, broken-wicket/crease detection, dismissal identity after crossing, and the batch throw's absolute-versus-relative time error remain open. Human keyboard/GamePad validation remains open.
