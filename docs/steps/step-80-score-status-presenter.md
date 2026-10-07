# Step 80 — Extract score-status presentation

## Change

Moved scoreboard status formatting out of `Game1` into the graphics-device-free `MatchHudPresenter`. The presenter now formats first-innings, chase-target, and completed-match status from an explicit state record.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 21 Simulation tests and 37 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build and headless asset/simulation review.
- Hosted Windows validation will be recorded after the push.
- No game host, window, or renderer capture was started.
