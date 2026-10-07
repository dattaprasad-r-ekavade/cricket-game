# Step 75 — Move graphics-free Simulation checks to xUnit

## Change

Moved all 14 graphics-free Simulation review-check files out of the production assembly into `SuperCricket.Simulation.Tests`. Added xUnit cases for match state and rules, CPU decisions, fielding, settings, procedural audio, and deterministic match batches. The existing `verify-match`, `verify-match-batch`, `verify-fielding`, and `verify-cpu-batting` CLI commands remain available and run their corresponding filtered xUnit groups; CPU batting asset paths are passed through an environment variable so paths containing spaces remain intact. Windows CI now tests the full solution, and the tool reference documents the test and CLI entry points.

The two Game-side review suites remain in the game project for a later host-bound migration. This completes the graphics-free Simulation portion of the broader review-check migration.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 14 Simulation tests and 6 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build, asset validation, filtered xUnit simulation checks, and deterministic match batches.
- No game host, window, or renderer capture was started.
- Hosted Windows validation will be recorded after the push.
