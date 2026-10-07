# Step 96 — Move compact overlay text into the HUD presenter

## Change

Moved compact match-overlay line selection, batting-side labels, match-phase priority, and event truncation into the graphics-free HUD presenter. The game host still measures and draws the returned lines.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 21 Simulation tests and 96 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build and headless asset/simulation review.
- Hosted Windows validation passed: [run 37586064602](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37586064602).
- No game host, window, or renderer capture was started.
