# Step 114 — Shared gameplay contracts

## Behaviour

- Added the shared batting enums and immutable records for ball categories, stroke requests, zone outcomes, and resolved strokes.
- Added the base match event and the named event records reserved for later event-bus wiring.
- Locked `TimingZone` numeric values to the ordering required by the build plan.

## Files

- `src/SuperCricket.Simulation/Batting/StrokeContracts.cs`
- `src/SuperCricket.Simulation/Events/MatchEvent.cs`
- `tests/SuperCricket.Simulation.Tests/TimingZoneContractTests.cs`
- `docs/build/a5-build-plan.md`

## Verification

- `dotnet build SuperCricket.sln -c Release --no-restore` — passed, 0 warnings and 0 errors.
- `dotnet test SuperCricket.sln -c Release --no-restore` — passed, 372 tests.
- `pwsh tools/review.ps1` — passed.

## Open questions

- Event records intentionally carry only event time for now; WP17 will add payloads when it wires the event bus.

## Playtest checklist

- No visual or feel changes in this step; no human playtest item is needed.
