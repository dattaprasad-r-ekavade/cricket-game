# Step 74 — Split the Game1 source by responsibility

## Change

Moved `Game1` lifecycle, update loop, rendering, interface/settings, match lifecycle/actions, scene rendering, and feedback into focused partial source files. Every `Game1` implementation part is below 500 lines. The methods still share `Game1`'s private state; this makes each domain smaller to work on but does not finish the planned component extraction. `GameplayReviewChecks.cs` also remains over 600 lines pending xUnit migration.

## Verification

- `dotnet test tests/SuperCricket.Game.Tests/SuperCricket.Game.Tests.csproj -c Release --no-restore`: 6 passed, 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build, asset validation, and deterministic simulation checks.
- No game host, window, or renderer capture was started.
