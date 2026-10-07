# Step 76 — Extract headless HUD presentation logic

## Change

Moved phase-specific keyboard/GamePad control hints and feedback-banner bounds calculation into `MatchHudPresenter`. `Game1` now maps live match state to an explicit HUD phase and supplies the next delivery name. The presenter itself does not create a graphics device or open a window. Added unit coverage for batting, bowling, running, innings/match completion on both input devices, and feedback layout at multiple viewport sizes.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 14 Simulation tests and 23 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build and the headless asset/simulation review.
- No game host, window, or renderer capture was started.
- Hosted Windows validation will be recorded after the push.
