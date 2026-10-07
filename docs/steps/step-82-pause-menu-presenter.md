# Step 82 — Extract pause-menu presentation

## Change

Moved pause-menu text assembly out of `Game1` into the graphics-device-free `MatchHudPresenter`. Keyboard and GamePad control help, developer-only hints, accessibility settings, audio fallback, and settings status now come from an explicit state record; drawing and layout remain in `Game1`.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 21 Simulation tests and 43 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build and headless asset/simulation review.
- Hosted Windows validation passed: [run 37579437868](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37579437868).
- No game host, window, or renderer capture was started.
