# Step 87 — Extract feedback style mappings

## Change

Moved ball-trail speed colors and difficulty-scaled batting contact-zone opacity into `MatchHudPresenter`, retaining the current thresholds and fallbacks.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 21 Simulation tests and 72 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build and headless asset/simulation review.
- No game host, window, or renderer capture was started.
