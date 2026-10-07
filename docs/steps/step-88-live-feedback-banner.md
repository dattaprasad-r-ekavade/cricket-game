# Step 88 — Extract live feedback banner content

## Change

Moved live feedback banner selection and copy into `MatchHudPresenter`. The banner content is now built from explicit bowling, contact, timing, and bounce state; drawing and font layout remain in `Game1`.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 21 Simulation tests and 76 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build and headless asset/simulation review.
- Hosted Windows validation passed: [run 37582632356](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37582632356).
- No game host, window, or renderer capture was started.
