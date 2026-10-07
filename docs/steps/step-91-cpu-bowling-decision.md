# Step 91 — Move CPU bowling decisions into MatchController

## Change

Moved deterministic CPU bowling delivery selection out of `Game1` and into `MatchController`. The controller now builds the bowling situation from its current match, uses the active bowler and striker ratings, and supplies the same seeded decision to the existing simulation model.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 21 Simulation tests and 85 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build and headless asset/simulation review.
- Hosted Windows validation passed: [run 37584065702](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37584065702).
- No game host, window, or renderer capture was started.
