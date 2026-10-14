# Step 117 — Ball category and stroke selection

## Behaviour

- Added a deterministic classifier that simulates the prepared delivery to the striker's crease and reads its first bounce and crease crossing.
- Classified bounce length from distance in front of the crease, with FullToss when there is no bounce before arrival. Classified line relative to the batter's shifted stance and mirrored off/leg sides for left-handed batters.
- Added automatic foot choice for short/high, full, and good-length balls; included LegFine in the back-foot good-length group to complete the direction list.
- Added stroke selection by direction, intent, loft, and foot. When a direction has no matching stroke, it selects the closest same-side stroke that suits the ball, then breaks ties by ID.
- Added pitch wicket width to `CricketPitchGeometry` and used its wicket coordinates for the crease calculation.

## Files

- `src/SuperCricket.Simulation/Batting/BallCategoryClassifier.cs`
- `src/SuperCricket.Simulation/Batting/StrokeSelector.cs`
- `src/SuperCricket.Simulation/CricketPitchGeometry.cs`
- `tests/SuperCricket.Simulation.Tests/BallCategoryAndStrokeSelectorTests.cs`
- `docs/build/a5-build-plan.md`

## Verification

- Tests cover all four delivery presets, length and line cutoffs, full toss, stance offset, handedness mirroring, automatic foot choice, suitability ranking, and stable selector fallback.
- `dotnet build SuperCricket.sln -c Release` — passed, 0 warnings and 0 errors.
- `dotnet test SuperCricket.sln -c Release` — passed, 425 tests.
- `pwsh tools/review.ps1` — passed.

## Open questions

- None.

## Playtest checklist

- Once WP3 connects selection to batting, check short balls and high arrivals choose the back foot, full/yorker balls choose the front foot, and good-length direction choices match the plan.
- Check that a wide outside-off ball mirrors to the same off-side category for a left-hander and that fallback stroke choice stays on the requested side.
