# Step 110 — Shared incoming wicket resolution

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

## Scope

Correct physics batches resolving an unbatted delivery at the batter's position instead of the physical stumps. Preserve the current live delivery outcomes, batting position, contact calibration, and input controls.

## Reproduction

With the shipped standard delivery, bowling rating 91, striker power 65, Standard difficulty, and CPU seed 25, the ball crosses z = -8.72 m at x = -0.1438 m and z = -10.06 m at x = -0.1715 m. The first lies within the current stump-width allowance; the second misses it. The old batch scored bowled while the live calculation scored a dot. Seeds 42, 54, 62, and 69 reproduce the same disagreement.

## Changes

- Added shared pitch/wicket dimensions and connected the rendered pitch and wickets to those constants.
- Added graphics-free `IncomingDeliveryModel` to interpolate the incoming ball at the physical near wicket, then apply shared line and stump checks through `DeliverySession`.
- Routed both the live physics loop and the batch missed-delivery path through that model. Removed their duplicated plane crossing and dismissal calculations.
- Shared stump bounds between incoming deliveries and return throws. Return run-outs still require possession and release.
- Recorded Step 109's successful hosted validation and technical completion separately from its open visual acceptance.

## Verification

- Added 20 Simulation tests and one Game geometry test. Coverage includes the five reported seeds, an independent physical-stump calculation across 250 CPU variations, stock/wide/no-ball scores, crossings at frame endpoints, non-crossings, interpolated height, 30/60/120 Hz sampling, invalid inputs, and matching rendered dimensions.
- Full Release suite: all 345 tests passed (Content 9, Simulation 206, Game 130).
- Default headless review: passed, including both builds, content validators, seeded physics-batch repeatability, wide-ball footwork, batting calibration, and invalid-input rejection.
- Hosted validation: pending.
- No game window, capture, host check, or runtime profile was started.

## Remaining

This corrects the wicket plane used by physics batches. The current wide-line simplification, completion of unbatted wides/no-balls at the wicket, and broader extras/rules completeness remain open. Beginner reachability, exact grounded bat/body checks at popping creases, and visual/human acceptance remain separate gates.
