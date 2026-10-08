# Step 117 — Stable run-out ownership at frame-rounded ties

**Branch:** `codex/godot-trial`

**Date:** 8 October 2026

## Trigger

The 7 October run-out fix retained the previous ground owner when batters were exactly level at halfway. `BetweenWicketsState` stores progress as a double, but receives frame durations as floats. Repeating `1f / 30`, `1f / 60`, or `1f / 120` for half a run produces progress `0.500000026...`; the exact-equality check incorrectly treated that rounding error as a crossing and could assign a run-out to the wrong batter.

## Changes

- Reproduced the issue with a focused xUnit theory at 30, 60, and 120 Hz; all three cases failed before the fix.
- Added a `1e-7` normalized-progress tie tolerance, equivalent to under 2 micrometres across the current 17.44 m runner path. The state now retains its prior ground owner inside that tolerance and changes ownership after a clear crossing.
- Covered level ties on both the outward and return legs.

## Verification

- Focused frame-rate theory: all 3 cases pass after the fix.
- Full Release suite: all 390 tests passed (Content 9, Simulation 222, Game 159).
- `tools/review.ps1 -SkipGame -SkipCaptures`: passed, including deterministic physics batches and batting reachability checks.
- Hosted Windows validation: passed in [run 37736538957](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37736538957).
- No game window or visual capture was started.

## Acceptance still open

This corrects numeric tie classification. Bat/body contact at the popping creases and visual turn-back still require human review.
