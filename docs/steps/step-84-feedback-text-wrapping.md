# Step 84 — Extract feedback-card text wrapping

## Change

Moved measured word wrapping from `Game1` into the graphics-device-free `MatchHudPresenter`. The game still supplies font measurements, while wrapping and continuation indentation can be checked without starting the host.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 21 Simulation tests and 47 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build and headless asset/simulation review.
- Hosted Windows validation passed: [run 37580959271](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37580959271).
- No game host, window, or renderer capture was started.
