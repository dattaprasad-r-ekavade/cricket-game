# Step 52 — Calibrated batting timing feedback

**Status:** implementation and automated review complete; human keyboard retest pending; GamePad untested.

## Change

The delivery result card now appends a batting timing label to the existing shot/contact description: `PERFECT`, `EARLY N ms`, or `LATE N ms`. The input delay is measured from the bowler's ball-release event. The target delay comes from the highest-contact-quality sample in the batting-practice analyzer for that shot and stock delivery; inputs within 75 ms of the target are labelled `PERFECT`. CPU bowling variations keep the base preset identity, so a variation such as "outswing" still uses the correct calibration. CPU-controlled batting does not present the human timing label.

Calibration is stored in `assets/batting/timing-calibration.json` for defence, drive, and loft across standard, wide, no-ball, and yorker deliveries. `tools/review.ps1` re-runs the analyzer and fails if a saved target drifts more than 13 ms from the highest-quality sample. The timing classifier also checks early, perfect, and late bands and their offsets.

## Review

- `dotnet build SuperCricket.sln -c Release` — passed with zero warnings or errors.
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --verify-gameplay` — passed, including the delivery-variation calibration case and feedback-card content.
- `pwsh -File tools/review.ps1` — passed, including all four analyzer calibration checks, 20 deterministic live-match trace replays, and renderer captures.
- Visually inspected `artifacts/review-batting-feedback.png`: the card displays `timing PERFECT`. The bowling capture continues to show target and landing accuracy.

## Follow-up

Step 54 has since added the pitch map to the result card. The owner still needs to replay with keyboard and confirm that the camera, timing label, and map are understandable during a live over; GamePad retest remains open. Guided nets and the in-world timing/contact aids remain open under A4b/A4c.
