# Step 114 — Automatic batting reachability

**Branch:** `codex/godot-trial`

**Date:** 8 October 2026

## Trigger

The prepared Rookie delivery from CPU seed 491 cannot be contacted from the centered stance. The player currently has to make a debug-only footwork step, and early keyboard/GamePad shot input could be lost while the feet moved.

## Changes

- Added `CalibrateReachableFootwork`, which measures the centered stance first. If no authored shot reaches the ball, it tests signed 0.45 m lateral steps toward the measured wicket-line position, then up to a second step. It chooses the nearest stance with a real contact profile and reuses that profile for timing feedback.
- Kept the batter centered when it already has a contact, and kept it centered without a fabricated prompt when no tested stance within 0.9 m can make contact. Existing developer and capture calibration paths keep their requested stance.
- The live human batting path applies the measured position with the existing step clips. A stroke entered before calibration or footwork finishes is held with its shot, aim, animation, and original keyboard/GamePad label, then played after the batter completes the step animation and returns to stance.
- Added the `SETTING FEET` action status during calibration. The CPU batting plan and delivered ball physics are unchanged.

## Verification

- Added 11 Simulation tests against the current 61-bone batter GLB, including Rookie seed 491, seeded standard deliveries that retain the centered stance, seed 54's measured step, repeated calibration, and an out-of-reach wide that gets no invented timing cue.
- Updated six Game timing-coordinator tests to preserve measured footwork together with the timing profile.
- Full Release suite: all 382 tests passed (Content 9, Simulation 218, Game 155).
- Code review fix: a queued GamePad stroke now retains its original input label even if keyboard input arrives while the feet settle.
- Default headless review: passed, including both builds, content validators, seeded physics-batch repeatability, batter-footwork authoring checks and batting-timing repeatability.
- Hosted Windows validation: pending.
- No game window, capture, or runtime profile was started.

## Acceptance still open

The calibration chooses a lateral position from the measured ball line. It does not yet choose front/back foot from delivery length or automate the stroke. Step 112's manual front/back keyboard chords continue to choose those shots. Very wide or otherwise unreachable deliveries remain without an automatic timing cue. The owner still needs to judge how automatic repositioning, early stroke buffering and the resulting animation feel in Release, and GamePad batting remains untested.
