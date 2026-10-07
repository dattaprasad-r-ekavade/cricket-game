# Step 64 — Role camera and feedback readability

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

**Scope:** Address the latest keyboard playtest report that the batting/bowling view was poorly framed and the delivery/shot feedback was hard to see.

## Changes

The behind-striker and bowler-end defaults now move closer to their active wickets, align more directly with the pitch, and use a tighter lens: 16 m, 38° field of view, 0.24 rad elevation, and a small symmetric side offset. The target shifts 2 m toward the active player's end; Page Up/Down and mouse-wheel zoom still work.

The persistent delivery card moves to the lower-right and uses a compact pitch map. Its short lines call out the result, ball speed, pitch length/line, distance from the striker, batting contact/timing, and bowling aim error. The role-coloured live callout remains at the top-right. This keeps the foreground player visible while the detailed result stays on screen until the next delivery.

## Verification

- Release solution build — passed with zero warnings or errors.
- `--verify-gameplay` — passed, including the closer camera defaults, zoom controls, batting feedback contents, and bowling aim feedback.
- Release captures at 1440×900 — `artifacts/step64-batting-feedback.png` and `artifacts/step64-bowling-feedback.png`; both were reviewed. Players remain unobscured by the result card, and the role/result text and pitch map are legible.
- Full `tools/review.ps1` — passed, including 20 exact live-match trace replays and the complete renderer capture suite. The 1440×900 VSync profile measured 39.65 ms average and 46.53 ms p95 frame intervals on this run, so rendering performance remains outside a 60 FPS budget.

This visual pass still needs the project owner's keyboard retest. GamePad framing/readability and the broader control-learning question remain unconfirmed.
