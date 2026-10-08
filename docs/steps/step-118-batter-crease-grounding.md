# Step 118 — Batter grounding at both popping creases

**Branch:** `codex/godot-trial`

**Date:** 8 October 2026

## Trigger

Step 116 centralized the crease and batter-anchor positions, but its test only confirmed the coordinates. A probe of the shipped `practice-stance` GLB found the grounded shoe soles stopped 0.084 m inside the popping crease at both ends, while run completion treated the anchor as safe.

## Changes

- Move the shared batter/contact anchor from 0.12 m inside each crease to 0.01 m inside it. This translates the batter body and batting-contact plane together, preserving their relative alignment.
- Use the shared batter anchors in the behind-striker and bowler-end camera presets.
- Add a headless, asset-backed test that skins the batter mesh in its practice stance and requires both shoe soles to have grounded vertices at least 0.02 m beyond each crease.
- Update the slower-delivery drive/loft timing regression to the recalculated 0.55 s target. Keep the authored stock-delivery calibration unchanged; its regression still matches the analyzer.

## Verification

- Asset-backed crease test: both ends pass. Both soles reach 0.026 m beyond the crease, with the nearest grounded vertices 0.003 m above the pitch surface.
- Slower-delivery calibration test: all 3 cases pass, including measured contact quality for drive and loft.
- Full Release suite: all 392 tests passed (Content 9, Simulation 222, Game 161).
- `tools/review.ps1 -SkipGame -SkipCaptures`: passed, including asset validation, timing calibration, deterministic match batches, batting reachability, and field coverage.
- Hosted Windows validation passed in [run 37739259382](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37739259382).
- No game window or visual capture was started.

## Acceptance still open

The geometry now gives the run-out model a grounded shoe beyond each crease in the shipped stance mesh. A human still needs to review bat/body grounding and turn-back appearance in motion, then retest the current controls and feedback. GamePad acceptance remains open.
