# Step 115 — Humanoid animation sampling cost

**Branch:** `codex/godot-trial`

**Date:** 8 October 2026

## Trigger

The engineering checklist called for measuring current GLB animation sampling and allocation cost. `PlayerAnimator.GetSkinMatrices` searched the clip timeline once per bone and built heap-backed `TransformData` objects for interpolated poses.

## Changes

- Added `--profile-animation <frames>` to profile the actual batter and bowler GLBs without starting the MonoGame window or creating a graphics device.
- The sampler now finds the current and previous clip sample ranges once per skin palette, then samples every bone from those ranges.
- Replaced per-bone heap-backed interpolation results with value-type pose transforms. Existing skin-palette, pose, and inverse-bind arrays remain reused.
- Added reference-parity and zero-managed-allocation tests for both shipped humanoid roles.
- Documented the profiler in [the tool reference](../tool-reference.md).

## Measurement

The headless profiler ran 1,800 measured 60 Hz frames per role on .NET 9.0.19 with 8 logical processors. In the before/after runs, measured sampling allocation fell from **41,103.7 B/frame** to **0 B/frame** for both roles.

| Role | Before mean / p95 | After mean / p95 |
| --- | ---: | ---: |
| Batter | 0.0222 / 0.0261 ms | 0.0135 / 0.0146 ms |
| Bowler | 0.0233 / 0.0267 ms | 0.0144 / 0.0178 ms |

These CPU timings are indicative: repeated profiles varied with host load. They do not establish full-game frame rate or include GPU execution.

## Verification

- Reference-pose parity and zero-allocation tests passed for both the batter and bowler GLBs.
- Full Release suite: all 386 tests passed (Content 9, Simulation 218, Game 159).
- `tools/review.ps1 -SkipGame -SkipCaptures`: passed; the game remained closed.
- Hosted Windows validation: passed in [run 37733767897](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37733767897).
- The complete-over CPU/GPU profile on the named hardware remains open for Milestone D.
