# Step 135 — Spin turn after the bounce

## Scope

WP23 applies signed delivery side-spin at the first pitch bounce, uses the authored pitch multiplier, and changes vertical bounce response for no-turn deliveries. The projected bounce remains on the pre-turn line; later ball and pad frames use the post-turn velocity.

## Changes

- `BallFlightSimulator` adds lateral velocity at the first delivery bounce; `ApplyBatContact` disables this delivery spin for the struck ball.
- Side-spin direction comes from the signed values in `assets/bowling/deliveries.json`; `PitchSpinMultiplier` is the authored green-to-dusty pitch turn/grip multiplier.
- No-turn deliveries use top-spin to change first-bounce restitution by up to 10%. The flipper data now has no lateral-spin component and a negative vertical-spin value for a low skid.
- Added simulation tests for break direction and reversals, the normal/dry pitch response, vertical-spin bounce response, and batted-ball isolation. The current 1.2 m/s-per-spin coefficient passes all six targeted cases.
- Added the `capture-spin-paths` Tools command, which writes an SVG comparing the off-break and leg-break trajectories after bounce. `tools/review.ps1 -CaptureVisuals` now creates this comparison.
- The first calibration test failed its magnitudes: a 3.0 m/s-per-spin coefficient produced 0.97 m for a normal off-break and 2.48 m for the dusty flighted ball. Reducing the coefficient to 1.2 produced 0.388 m for a normal off-break; the normal leg-break moves 0.446 m in the opposite direction. Dusty flighted turn remains within the ≤1 m target.

## Verification

- `dotnet build src/SuperCricket.Tools/SuperCricket.Tools.csproj -c Release --no-restore` — passed, 0 warnings and 0 errors.
- `dotnet test tests/SuperCricket.Simulation.Tests/SuperCricket.Simulation.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~BallFlightSpinTests` — passed, 6 tests.
- `dotnet src/SuperCricket.Tools/bin/Release/net9.0/SuperCricket.Tools.dll verify-delivery-envelopes` — passed, 1 envelope test.
- `dotnet src/SuperCricket.Tools/bin/Release/net9.0/SuperCricket.Tools.dll capture-spin-paths assets/bowling/deliveries.json assets/deliveries/standard-pace.json artifacts/wp23-spin-path-comparison.svg` — wrote the SVG with +0.388 m off-break and −0.446 m leg-break at the striker's wicket line; PowerShell XML parsing confirmed a 1080×720 SVG with two paths.
- `pwsh -File tools/review.ps1 -SkipGame -CaptureVisuals` — passed. The 50-match Standard balance samples again met all bands at 10 and 20 overs, including 6.11/6.63 RPO and 3.38/7.14 wickets per innings.

## Artifact

- [Off-break and leg-break path comparison](../../artifacts/wp23-spin-path-comparison.svg)
