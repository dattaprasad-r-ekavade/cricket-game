# Step 95 — Move role-based camera presets into CameraDirector

## Change

Moved the mapping from the human-controlled bowling role to a camera preset into `CameraDirector`. `Game1` now asks the camera director to select the default view for the active role.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 21 Simulation tests and 92 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build and headless asset/simulation review.
- Hosted Windows validation passed: [run 37585675426](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37585675426).
- No game host, window, or renderer capture was started.
