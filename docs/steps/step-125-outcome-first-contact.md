# Step 125 — Outcome-first contact

## Behaviour

- Batting inputs now resolve through the authored 40-stroke table using the ball's distance from the contact plane at input time. Timing zones determine miss/contact, authored yaw/elevation/speed ranges determine the outgoing velocity, edge chances add a seeded deflection, and left-handed batters mirror yaw.
- The live game and physics batch use the same outcome resolver. The live ball resolves at the fixed contact plane, snaps to the bat sweet spot within 0.35 m, and keeps swept collision available as a `--debug` cross-check. The batting-practice analyzer still measures the swept animation path.
- CPU plans select their input distance from their own timing window. The runtime game package reads `strokes.json`; it no longer packages or loads `shots.json`.
- Offensive stroke outcome ranges were tuned for outcome-first contact. CPU run safety retains its margin for ground pickups while authored no-ball catch-and-return timing can still produce the safe runs supported by its collection window.

## Files

- `assets/batting/strokes.json`
- `src/SuperCricket.Simulation/Batting/StrokeDirectionModel.cs`
- `src/SuperCricket.Simulation/Batting/StrokeOutcomeResolver.cs`
- `src/SuperCricket.Simulation/BattingPracticeAnalyzer.cs`
- `src/SuperCricket.Simulation/ContactPlaneCrossingModel.cs`
- `src/SuperCricket.Simulation/CpuBattingOutcomeModel.cs`
- `src/SuperCricket.Simulation/CpuLiveBattingPlanModel.cs`
- `src/SuperCricket.Simulation/CpuLiveRunningDecisionModel.cs`
- `src/SuperCricket.Simulation/PhysicsAutomatedMatchBatchSimulator.cs`
- `src/SuperCricket.Game/Game1.ContentLifecycle.cs`
- `src/SuperCricket.Game/Game1.MatchActions.cs`
- `src/SuperCricket.Game/Game1.MatchLifecycle.cs`
- `src/SuperCricket.Game/Game1.MatchLoop.cs`
- `src/SuperCricket.Game/Game1.cs`
- `src/SuperCricket.Game/GameplayAdvancedReviewChecks.cs`
- `src/SuperCricket.Game/SuperCricket.Game.csproj`
- `tests/SuperCricket.Simulation.Tests/ContactPlaneCrossingModelTests.cs`
- `tests/SuperCricket.Simulation.Tests/StrokeOutcomeResolverTests.cs`
- `tools/review.ps1`
- `docs/build/a5-build-plan.md`

## Verification

- `dotnet build -c Release` — passed with 0 warnings and 0 errors.
- `dotnet test -c Release --no-restore` — passed: 285 Simulation, 155 Game, and 25 Content tests (465 total).
- `pwsh tools/review.ps1 -RunGameChecks` — passed, including seeded deterministic physics batches, balance checks, batting checks, 30 live CPU traces, and 30/60/120 fps contact and footwork checks.
- Resolver tests repeat seeded outcomes, exercise every stroke over 200 seeds in the Ideal band, verify early/late misses, and check left-handed mirroring.
- A 50-match, 10-over-per-innings seeded comparison (100 innings, seeds 5103–5152) moved median runs per over from 12.20 to 15.12 (+23.9%); mean runs per over moved from 12.44 to 14.57. Mean runs per innings rose from 109.38 to 116.98. Contacts increased from 4,511 to 4,732 and misses fell from 895 to 301.

## Playtest checklist

- “same timing on same ball gives the same kind of shot” — confirm the feel in the next human playtest.

## Open questions

- None for this work package. The human feel check remains part of the wave 3 playtest gate.
