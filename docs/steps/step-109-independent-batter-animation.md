# Step 109 — Independent batter animation

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

## Scope

Correct the non-striker copying the striker's shot and footwork pose. Use the current humanoid asset and authored clips; preserve the existing batting collider and input controls.

## Changes

- Added graphics-device-free `BatterAnimationController`, owning separate `PlayerAnimator` instances, clocks, transition state, and bone palettes. Both read the same asset data.
- The striker retains its existing shot, footwork, debug clip, and collider paths. The non-striker stays in practice stance during those actions and is drawn with its own skin matrices.
- Starting a run plays the running clip for both batters. Returning home, completing the last requested run, or ending a running delivery changes both to stance. Turning back retains both running clocks; queued follow-up runs no longer restart the gait at each completed crossing.
- Beginning a delivery resets both clocks. Pause and deterministic capture freeze both animations through the same controller update path.
- Extended opt-in host checks for non-striker footwork isolation, running/return transitions, and pause behavior. These checks were not started.

## Verification

- Added 22 headless tests against the actual 61-bone batter GLB, covering independent palettes and clocks; defence, drive, loft, both footwork clips, and debug clip isolation; running, turn-back, queued-run continuity, delivery reset, and freeze/resume; 30/60/120 Hz updates; finite matrices and unchanged shared bind poses; invalid input.
- `dotnet test SuperCricket.sln -c Release --no-restore`: all 324 tests passed (Content 9, Simulation 186, Game 129).
- Default headless review: passed, including both builds, content validation, seeded physics batch repeatability, and batting calibration.
- Hosted validation: pending.
- No game window, host check, capture, or runtime profile was started.

## Remaining

Visual acceptance remains open: the existing practice stance and running clips still need human review for a non-striker, foot planting, turn-back, and bat-hand alignment. The extra independent skin palette has not been runtime-profiled. Beginner delivery reachability, exact crease grounding, dedicated fielder/character assets, broader rules completeness, and the deferred keyboard/GamePad retest remain open.
