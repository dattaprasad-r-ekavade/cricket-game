# Step 92 — Move fielding setup into MatchController

## Change

Moved current bowling-situation assembly, field placement, and `FieldingSide` setup into `MatchController`. `Game1` now asks the controller to configure the field from the active match state and receives the selected tactic for presentation.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 21 Simulation tests and 86 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build and headless asset/simulation review.
- Hosted Windows validation passed: [run 37584568456](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37584568456).
- No game host, window, or renderer capture was started.
