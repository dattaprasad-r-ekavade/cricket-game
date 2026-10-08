# Step 119 — Grounded running pose at run completion

**Branch:** `codex/godot-trial`

**Date:** 8 October 2026

## Trigger

Step 118 verified that the stationary practice stance grounded beyond both creases. A follow-up measurement found the default 1.35 s running clip could complete a run at 60/120 Hz with the batter's grounded shoe about 1 mm short of the far popping crease. The run-out state had already marked the runner safe.

## Changes

- Move the shared near/far batter and contact anchors 0.016 m beyond their popping creases. The root, shoe geometry, and batting-contact plane retain one shared position.
- Add an asset-backed regression that advances `BetweenWicketsState` and both batter animators together, verifies both runners at run completion, then advances the full 0.12 s running-to-stance transition and checks the final stance at 30, 60, and 120 Hz.
- Update pitch-geometry assertions to the shared wicket-side offset.

## Verification

- Focused stance and run-completion grounding checks: all 5 cases pass.
- Full Release suite: all 395 tests passed (Content 9, Simulation 222, Game 164).
- `tools/review.ps1 -SkipGame -SkipCaptures`: passed, including asset validation, batting reachability, timing calibration, deterministic match batches, and field coverage.
- Intermediate transition samples do not maintain the 0.02 m grounded-shoe margin (far runner measured 0.000 m, 0.010 m, and 0.018 m beyond the crease at 30, 60, and 120 Hz respectively). The human visual gate remains open to judge this stop animation in motion.
- Hosted Windows validation passed in [run 37741784872](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37741784872).
- No game window or visual capture was started.

## Acceptance still open

A human still needs to inspect the turn-back and shoe/bat grounding in motion, then retest the current keyboard controls and feedback. GamePad acceptance remains open.
