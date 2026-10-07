# Step 78 — Slow CPU pace for Rookie and the first match

## Change

Added `DeliveryPaceModel` for CPU deliveries faced by a human batter. Rookie matches and the first match use 82% of the chosen release velocity; later Standard and Pro matches retain the selected pace. The model deep-copies and validates each delivery so it never changes an authored preset. `Game1` tracks completion of the first full match, including both innings, before using the normal pace on later matches.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 21 Simulation tests and 31 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build and headless asset/simulation review.
- No game host, window, or renderer capture was started; in-game feel remains for the planned human retest.
- Hosted Windows validation will be recorded after the push.
