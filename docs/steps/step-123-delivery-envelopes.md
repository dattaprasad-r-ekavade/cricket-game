# Step 123 — Delivery envelopes

## Behaviour

- Tuned quick delivery catalogue speed bands to keep quicks within 120–145 km/h while leaving the slower-ball and spin ranges intact.
- Calibrated the standard pace release and delivery bounce response so seeded good-length deliveries pitch 2.5–5 m before the crease, lose about 20% speed at the bounce, and cross the crease at 0.1–0.6 m.
- Separated batted-ball pitch coefficients from delivery coefficients so the delivery-envelope tuning does not change struck-ball behavior.
- Added speed-preserving pitch targeting for quick bowling and calibrated CPU pace decisions against the stock pace reference.
- Recalibrated standard-pace batting timing for the tuned incoming delivery.
- Added `verify-delivery-envelopes` with 352 seeded envelope samples across stock presets, CPU decisions, and quick catalogue families.

## Files

- `assets/batting/timing-calibration.json`
- `assets/bowling/deliveries.json`
- `assets/deliveries/{no-ball,standard,wide,yorker}-pace.json`
- `src/SuperCricket.Content/DeliveryPreset.cs`
- `src/SuperCricket.Simulation/BallFlightSimulator.cs`
- `src/SuperCricket.Simulation/Bowling/BowlingAccuracyModel.cs`
- `src/SuperCricket.Simulation/BowlingAimModel.cs`
- `src/SuperCricket.Simulation/BowlingDecisionModel.cs`
- `src/SuperCricket.Tools/Program.cs`
- `tests/SuperCricket.Simulation.Tests/BattedBallPhysicsTests.cs`
- `tests/SuperCricket.Simulation.Tests/DeliveryEnvelopeTests.cs`
- `tests/SuperCricket.Simulation.Tests/BattingTimingCalibrationTests.cs`
- `tests/SuperCricket.Simulation.Tests/IncomingDeliveryTests.cs`
- `tests/SuperCricket.Simulation.Tests/PlayerAssetParityTests.cs`
- `tools/review.ps1`
- `docs/build/a5-build-plan.md`

## Verification

- `dotnet build -c Release` — passed with 0 warnings and 0 errors.
- `dotnet test -c Release` — passed, 455 tests.
- `pwsh tools/review.ps1` — passed, including `verify-delivery-envelopes`, seeded physics balance, and replay checks.
- Seeded physics balance: 105.2 runs per innings average, 70–135 range, and a 10% run-out share.

**Accept:** sampled quick deliveries stay within the specified pace, pitch, bounce-loss, and good-length crease-height envelopes.
