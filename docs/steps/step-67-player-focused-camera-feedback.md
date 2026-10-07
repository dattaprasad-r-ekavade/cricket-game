# Step 67 — Player-focused cameras and visible delivery feedback

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

## Trigger

The project owner's keyboard retest after Step 66 still found batting and bowling camera angles very poor and the live batting/bowling feedback invisible. The Step 66 review log passed, but its camera tests checked preset values and a banner rectangle rather than whether play read well.

## Changes

- The behind-striker and bowler-end views now focus on the active player's crease-height position instead of the middle of the pitch. They start at 8 m, with Page Down explicitly documented as zoom-in and Page Up as zoom-out; the closest distance is 4.5 m.
- In human bowling, the bowler-end view smoothly tracks the bowler through the run-up. At ball release it hands the view to the existing ball-follow camera, so the delivery can be followed toward the bounce. The camera cycle remains available to choose broadcast or square-leg views.
- The live batting/bowling result is now a wider, centered, high-contrast strip below the scoreboard, with a colored top and side accent. The existing lower-right result card and pitch map retain the detailed delivery summary.
- The Cricket 07 reference check found that EA offered a deliberately wide default approach view as well as reverse Behind Batsman and Flip views. The close view used selective player transparency to keep the pitch marker visible; picture-in-picture supplied bowler, stroke, and running context. The installed copy has the readme, controller-detection CSV, and packed gameplay archives, but no plain-text keyboard map or configurable camera table. See [the research note](../reviews/cricket07-research.md).

## Verification

- `dotnet build SuperCricket.sln -c Release` — passed, zero warnings and errors.
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --verify-gameplay` — passed, including focus positions, run-up tracking, zoom direction/limits, feedback placement, and existing match checks.
- Reviewed the fresh 1440×900 captures `artifacts/step67-batting-camera.png` and `artifacts/step67-bowling-camera.png`. The new feedback strip is centered below the scoreboard in both; the role views put the active player closer to the center of the composition.
- `pwsh -File tools/review.ps1` — passed: Release build, asset and gameplay gates, 20 deterministic live-match trace replays, and the full capture suite.
- 1440×900 VSync profile: 38.01 ms average and 46.66 ms p95 frame interval; CPU update 0.17 ms average and draw submission 1.01 ms average. GPU execution time is not included.

Keyboard confirmation of the new view and feedback placement is still required; controller readability and the separate control-learning question remain open.
