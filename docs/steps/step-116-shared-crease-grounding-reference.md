# Step 116 — Shared popping-crease geometry

**Branch:** `codex/godot-trial`

**Date:** 8 October 2026

## Trigger

The run-out review found that the batting analyzer, far-end batter placement, and drawn pitch markings each carried independent position values. The current batter anchors are 0.12 m inside the drawn popping creases, but this offset was implicit and could drift between the simulation and renderer.

## Changes

- Added shared popping-crease and batter-anchor dimensions to `CricketPitchGeometry`.
- Made the batting analyzer, far-end batter placement, and ground markings use those shared dimensions without changing their existing world positions.
- Updated runner-presentation tests to use the shared pitch geometry and added a simulation test for the measured crease coordinates and 0.12 m inset at both ends.

## Verification

- Full Release test suite: all 387 tests passed (Content 9, Simulation 219, Game 159).
- `tools/review.ps1 -SkipGame -SkipCaptures`: passed, including asset validation, deterministic match batches, field coverage, and batting reachability checks.
- Hosted Windows validation: passed in [run 37735242592](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37735242592).
- The game remained closed; no visual capture was started.

## Acceptance still open

The 0.12 m inset describes the batter anchor, not proven bat or body contact with the ground behind the popping crease. A person still needs to inspect grounded bat/body geometry and the visual turn-back against the running model before this plan item can close.
