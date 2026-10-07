# Step 62 — Live batting timing cue

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

**Scope:** Give a new human batter a clear, real-time answer to “when should I press?” using the measured best-contact delay already reported after a shot.

## Changes

After a delivery is released, the human batter sees a top-right timing card. A moving white marker crosses a short timeline; its green band is the overlap of the calibrated on-time ranges for the currently available ground/loft shots. That makes the green band safe whichever of those buttons the player chooses. The prompt changes from `WATCH THE MARKER`, to `SWING NOW`, to `LATE SHOT POSSIBLE`. Its detail line names the keyboard buttons or GamePad buttons. A late prompt still tells the player to swing, since contact can remain possible outside the ideal window.

The live timing window uses the same per-delivery/per-shot calibration and on-time tolerance as the result feedback. Neutral aim compares defence with loft; a directional aim compares drive with loft. The prompt stops after the shot input, delivery completion, or when the CPU is batting; the existing contact/timing result banner then takes over. A real bounce marker and result card continue to report where the ball pitched.

The pure timing model returns a waiting/on-time/passed state, a normalized marker position, the calibrated green-band bounds, and time remaining until the band. Capture review now creates three paused delivery frames to inspect the waiting, on-time, and late states.

## Verification

- `dotnet build SuperCricket.sln -c Release` — passed with zero warnings and errors.
- `--verify-gameplay` — checks calibrated timing states, gauge bounds/progress, waiting time, finite input validation, existing shot timing classification, and match controls.
- `tools/review.ps1` — full Release review, including generated timing-state captures and deterministic live-match traces.
- Captures inspected at 1440×900: `artifacts/step62-timing-wait.png`, `artifacts/step62-timing-swing.png`, and `artifacts/step62-timing-late.png`. The marker reaches the green target band in the swing state and progresses beyond it in the late state; labels and keyboard prompts are legible.
- The review renderer profile measured 40.98 ms average and 46.41 ms p95 frame intervals; it excludes GPU execution time and does not establish a 60 FPS result.
- `git diff --check` — passed.

These captures verify the display state, not how helpful the cue feels during live play. Owner keyboard retest and GamePad validation remain part of the open Milestone A gate.
