# Step 90 — Move delivery-pace eligibility into MatchController

## Change

Moved the first-match human-batting pace decision from `Game1` into the graphics-free `MatchController`. CPU batting, developer mode, and verification runs keep their bypasses. Eligible deliveries still use `DeliveryPaceModel`, which adjusts a copy and preserves the authored preset.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 21 Simulation tests and 84 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build and headless asset/simulation review.
- Hosted Windows validation passed: [run 37583449939](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37583449939).
- No game host, window, or renderer capture was started.
