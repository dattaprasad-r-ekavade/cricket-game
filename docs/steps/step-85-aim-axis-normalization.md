# Step 85 — Extract analog aim normalization

## Change

Moved controller aim clamping, the existing 0.2 dead zone, and rescaling into `MatchInputRouter`. Batting and bowling use the same tested normalization; non-finite values remain neutral.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 21 Simulation tests and 57 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build and headless asset/simulation review.
- Hosted Windows validation passed: [run 37581445032](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37581445032).
- No game host, window, or renderer capture was started.
