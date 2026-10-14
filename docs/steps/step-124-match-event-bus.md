# Step 124 — Match event bus

## Behaviour

- Expanded the graphics-free event contracts with useful payloads for delivery release, pitch, stroke/contact, misses, pad hits and umpire decisions, wickets, scoring, boundaries, near run-outs, dismissals, and over completion.
- Added `MatchEventBus` with typed subscriptions that can be disposed, event publication, and completed per-delivery logs.
- `MatchState` now owns or shares a bus, starts a delivery log at release, records resolved scoring and wicket events, emits over-end events, and completes each log with its `DeliveryResult`.
- `LimitedOversMatch` shares one bus across both innings and saves each innings’ delivery logs on its scorecard result.
- The physics batch emits timed pitch, stroke/contact, miss, pad/appeal/decision, boundary, and near-run-out events.
- Event logs and innings results use value equality so repeating a seeded batch remains deterministic with the new logs.

## Files

- `src/SuperCricket.Simulation/Events/MatchEvent.cs`
- `src/SuperCricket.Simulation/Events/MatchEventBus.cs`
- `src/SuperCricket.Simulation/Events/DeliveryEventLog.cs`
- `src/SuperCricket.Simulation/DeliverySession.cs`
- `src/SuperCricket.Simulation/MatchState.cs`
- `src/SuperCricket.Simulation/LimitedOversMatch.cs`
- `src/SuperCricket.Simulation/PhysicsAutomatedMatchBatchSimulator.cs`
- `tests/SuperCricket.Simulation.Tests/MatchEventBusTests.cs`
- `docs/build/a5-build-plan.md`

## Verification

- `dotnet build -c Release` — passed with 0 warnings and 0 errors.
- `dotnet test -c Release` — passed, 458 tests.
- `pwsh tools/review.ps1` — passed, including seeded batch replay, physics balance, and delivery-envelope checks.
- Seeded batch tests verify event-log runs and dismissals reconcile with both innings scorecards; focused tests cover subscription disposal, boundaries, and over completion.

**Accept:** completed seeded match event logs reconcile with the scoreboard and remain repeatable for the same seed.
