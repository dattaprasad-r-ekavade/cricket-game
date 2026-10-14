# Step 120 — Throws and run-outs

## Behaviour

- Added rating-scaled throw speed from 19 m/s at 5 m to 28 m/s at 30 m, with low trajectories below 2 m and a seeded direct-hit roll using the distance and accuracy formula.
- Misses add the 0.35 s gather delay before the wicket break. The physics match return-throw path chooses the closer wicket end and uses the fielder's rating.
- Added popping-crease checks for grounded bat/foot ownership and a near-run-out result within 2 m. The batch CSV reports run-outs, near run-outs, LBWs, and other delivery metrics.
- Updated the review gate to check that the run-out share remains in the planned 5–12% range.

## Files

- `src/SuperCricket.Simulation/Fielding/ThrowModel.cs`
- `src/SuperCricket.Simulation/Fielding/RunOutModel.cs`
- `src/SuperCricket.Simulation/PhysicsAutomatedMatchBatchSimulator.cs`
- `src/SuperCricket.Simulation/AutomatedMatchBatchSimulator.cs`
- `src/SuperCricket.Tools/Program.cs`
- `tools/review.ps1`
- `tests/SuperCricket.Simulation.Tests/ThrowAndRunOutModelTests.cs`
- `docs/build/a5-build-plan.md`

## Verification

- Tests cover speed by distance and arm rating, apex height, seeded direct-hit frequency and accuracy scaling, threatened-end selection, both popping creases, run-out edge resolution, and near-run-out detection.
- A seeded batch of 50 ten-over matches produced 21 run-outs among 375 dismissals (5.6%), inside the 5–12% target; it also produced 57 near-run-out events.
- The six-match review batch produced a 7.7% run-out share across 52 dismissals.
- `dotnet build SuperCricket.sln -c Release` — passed, 0 warnings and 0 errors.
- `dotnet test SuperCricket.sln -c Release --no-build` — passed, 443 tests.
- `pwsh tools/review.ps1` — passed.

## Open questions

- The physics batch derives the closest runner distance from between-wickets progress; live pose-based grounded bat/foot ownership is part of the later game-host integration.

## Playtest checklist

- Check a short throw stays low and a deep throw travels faster without an arc over 2 m.
- Check that the closer wicket is targeted and a missed throw adds the keeper/bowler gather delay.
- Check a runner grounded behind the popping crease is safe, and a runner short of the crease is out when the wicket breaks.
- Check a safe runner within 2 m gets the near-run-out cue.
