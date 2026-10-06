# Step 51 — Camera framing and delivery feedback

## User signal

The keyboard follow-up reported that the batting and bowling camera angle was very poor and needed zooming, and that batting/bowling feedback could not be seen. The tester did not report whether Step 50's controls had become understandable. Research notes for the installed Cricket 07 copy and its design references are in [cricket07-research.md](../reviews/cricket07-research.md).

## Changes

- Tightened the default broadcast camera from 54 m to 28 m, with a lower angle and slight square-leg offset; behind-striker is 21 m, bowler-end is 24 m, and square-leg is 34 m.
- Selects the broadcast view when the human bats and bowler-end view when the human bowls. After a bat contact, ball-follow tracks the shot; the next delivery restores the player-role view. Manual views remain available through the existing camera cycle.
- Adds a persistent result card after each completed delivery with release speed, measured pitch line/length and distance from the striker, shot/contact quality, score or dismissal, and human bowling target/landing error.
- Adds a capture-only feedback preview and Release review captures for batting and bowling so the result card can be checked without hand-authoring test state.

## Research findings

The installed readme prioritizes nets, timing practice, run assist, supported analog controls, and keyboard rollover. The title screen could not be advanced in this session, so live in-match camera or HUD behavior was not directly verified. Producer Justin Forrest's diary describes a deliberately wider default batting camera for the run-up, a marker-readable behind-batsman camera, a wider fielding camera, and a Picture-in-Picture/timing HUD used for gameplay. A PC review describes the Century Stick's placement/shot/power split and the pace-meter plus pitch-point bowling loop. See [research notes](../reviews/cricket07-research.md).

## Review

- `dotnet build SuperCricket.sln -c Release` — passed, zero warnings.
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --verify-gameplay` — passed, including camera distance and delivery-card content checks.
- `pwsh -File tools/review.ps1` — passed; 20 live match traces replayed exactly, renderer captures generated, and saved preferences stayed unchanged.
- Visually checked `review-start.png`, `review-behind-striker.png`, `review-batting-feedback.png`, `review-bowling-feedback.png`, and `review-ball-follow.png` from the local ignored `artifacts/` folder. Pitch/player scale is visibly larger; both result cards are readable at 1440x900.

## Remaining validation

This is automated and screenshot review, not a human retest. Confirm keyboard batting, keyboard bowling, target-marker visibility, and the result card after several deliveries. GamePad play remains untested. The card gives contact quality, not a calibrated early/perfect/late label; the optional timing aid, pitch-map dot, replay, and in-world cues remain open under A4b/A4c.
