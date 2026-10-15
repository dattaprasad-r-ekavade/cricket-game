# Step 136 — Pace-meter bands

## Scope

WP24 shows the Slow, Normal, Fast, and no-ball ranges on the stride meter. Presenter layout is derived from the pace model and remains graphics-free; drawing remains in the game HUD.

## Changes in progress

- Added model-owned pace bands in elapsed seconds for each difficulty's fill time; `PaceMeterModel.Resolve` uses the same threshold constants.
- Added a presenter layout with normalized edges, band labels, selected label, progress, and fill time.
- Updated the bowling HUD to draw labeled colored bands, a moving white marker, and the accurate no-ball start time. The selected band remains visible after the press.
- Expanded the bowling control row to fit the extra band labels.
- Added a presenter test that compares every displayed edge to the model ranges for all five human difficulty profiles and checks the selected labels.
- Added a review capture at `artifacts/review-bowling-meter.png`.
- The first capture showed the pace ranges but the band labels were too small at 1440×900; increased their text size to match the other control labels.

## Verification

- `dotnet test tests/SuperCricket.Game.Tests/SuperCricket.Game.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~BowlingPaceMeterBands` — passed, 1 targeted test covering all five human difficulty profiles.
- `dotnet test tests/SuperCricket.Game.Tests/SuperCricket.Game.Tests.csproj -c Release --no-restore` — passed, 193 tests, including the delivery feedback line's release speed in km/h.
- `dotnet build -c Release` — passed, 0 warnings and 0 errors.
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --capture-frame artifacts/review-bowling-meter.png --bowling-meter-preview` — passed.
- Inspected the 1440×900 capture after increasing label size: Slow, Normal, Fast, and NB labels are legible, all four colored ranges and the moving marker fit inside the bowling panel, and the no-ball start time is shown.

## Artifact

- [Pace-meter capture](../../artifacts/review-bowling-meter.png)
