# Step 83 — Extract and constrain pause-menu layout

## Change

Moved pause-menu panel sizing and placement from `Game1` into the graphics-device-free `MatchHudPresenter`. Normal desktop sizing stays the same. When a long menu would exceed a 640×480 viewport, the presenter reduces line spacing and text scale so the full panel fits inside the window margins.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 21 Simulation tests and 45 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build and headless asset/simulation review.
- No game host, window, or renderer capture was started.
