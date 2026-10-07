# Step 65 — Projected bounce and batting contact guide

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

**Scope:** Make batting timing and contact easier to read using the current delivery simulation and bat collision bounds.

## Changes

At release, the game simulates the active delivery once and marks its predicted first pitch point with a cyan ring until the ball actually bounces; the existing gold ring then marks the measured impact point. The timing prompt explains the pitch-point ring and the bat outline.

During human batting, a wireframe contact volume follows the animated bat blade. It uses the same blade bounds, ball radius, and shot-set collision padding as the real contact resolver, without changing hit outcomes. Rookie draws it strongly, Standard draws it faintly, and Pro hides it. Bowling and batting physics are unchanged.

`--contact-zone-preview Rookie|Standard|Pro` makes the three visual states reproducible from the capture CLI. `tools/review.ps1` now generates all three tier captures.

## Verification

- Release solution build — passed with zero warnings or errors.
- `--verify-gameplay` — passed; it verifies the projected point matches `BowlingAimModel.FindFirstBounce` and that assist prominence scales Rookie > Standard > hidden Pro.
- Release captures at 1440×900 — `artifacts/step65-contact-rookie.png`, `...standard.png`, and `...pro.png`; all three were reviewed.
- Full `tools/review.ps1` — passed, including 20 exact live-match trace replays and the three difficulty-tier captures.
- Performance remains an open issue: this 1440×900 VSync run measured 39.81 ms average and 46.32 ms p95 frame intervals; GPU execution time is not included in the CPU timings.

The visual aids still need a keyboard retest. Controller legibility and the broader control-learning question remain unconfirmed.
