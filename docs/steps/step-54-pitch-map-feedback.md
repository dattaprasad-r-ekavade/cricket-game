# Step 54 — Pitch-map feedback

**Status:** implementation, full review, and capture review complete; human keyboard retest pending; physical GamePad retest pending.

## Change

The persistent delivery result card now contains a compact pitch map. The bowler's wicket is at the top and the batter's wicket at the bottom. A yellow dot marks the actual first bounce; when the player bowled, a cyan ring marks the intended target. The legend distinguishes both points, while the existing text continues to report line, length, and distance from aim. The map reserves room on the card and long text wraps beside it.

The projection includes lateral and length margins so wide or overpitched locations remain visible near the map edge. Full-toss deliveries can still show an intended target without inventing a bounce point.

## Review

- `dotnet build SuperCricket.sln -c Release` — passed with zero warnings or errors.
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --verify-gameplay` — passed, including projection checks for bowler/batter orientation, map center, and wide-ball clamping.
- Full `pwsh -File tools/review.ps1` — passed, including gameplay checks, 20 deterministic live-match replays, renderer measurement, and both feedback-card captures.
- Captured and visually inspected `artifacts/review-batting-feedback.png` and `artifacts/review-bowling-feedback.png` at 1440x900. The batting map shows the bounce dot; the bowling map distinguishes aim and bounce with the cyan ring and yellow dot.

## Follow-up

The owner still needs to confirm the map and text are readable during keyboard play. GamePad feedback remains untested. Step 55 adds the separate live pitch spot and contact flash; contact replay and guided batting practice remain open.
