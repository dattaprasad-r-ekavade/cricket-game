# Step 77 — Test HUD phase resolution

## Change

Moved match-phase selection for the primary control hint into `MatchHudPresenter.ResolvePhase`. `Game1` now supplies the current completion, bowling, delivery, run, and batting flags; the presenter applies one explicit precedence order before formatting text. Added cases for all eight display phases, including completion priority when several flags are set.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 14 Simulation tests and 31 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build and headless asset/simulation review.
- Hosted Windows validation passed: [run 37575757582](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37575757582).
- No game host, window, or renderer capture was started.
