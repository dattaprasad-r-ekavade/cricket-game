# Step 89 — Extract batting-timing label formatting

## Change

Moved the `PERFECT`, `EARLY n ms`, and `LATE n ms` text formatting into `MatchHudPresenter`. Calibration lookup and timing assessment remain in the game flow; the presenter receives the resulting band and offset.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 21 Simulation tests and 79 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build and headless asset/simulation review.
- Hosted Windows validation passed: [run 37582822896](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37582822896).
- No game host, window, or renderer capture was started.
