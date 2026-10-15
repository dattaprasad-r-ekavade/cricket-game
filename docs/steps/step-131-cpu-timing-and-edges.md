# Step 131 — CPU timing state and edge direction (WP22)

Date: 2026-10-15

## Changes

- CPU batting plans now receive a persistent `BatterTimingState`. The live match stores one state per batter ID and resets the map between innings. The physics batch simulator does the same, updates the faced batter after each non-wide delivery, and passes that state into the next plan.
- The live stroke resolver uses the same per-batter state for the timing window. A new batter late in an innings starts at the settle curve's zero-balls value instead of inheriting the innings ball count.
- Edges now deflect behind the batter toward the keeper line. Off-side strokes are biased to the off side, leg-side strokes to the leg side, and left-handed batters mirror the result. Edge speed is 40–60% of the sampled stroke speed and edge elevation remains 5–20 degrees.
- `BatContact` now carries an `Edged` flag. Audio, controller feedback and the result card use that flag; the result card labels an edged contact `EDGE`.

## Checks

| Command | Result |
| --- | --- |
| `dotnet test SuperCricket.sln -c Release` | Passed: Simulation 290, Game 192, Content 28; 510 total. The first run caught an invalid 20-over test fixture; the fixture now uses the supported 10-over format, and the targeted and full reruns passed. |
| `dotnet build SuperCricket.sln -c Release` | Passed with 0 warnings and 0 errors. |
| `pwsh tools/review.ps1` | Exit 0; complete output: [step-131-review-output.txt](step-131-review-output.txt). The full review ended `Review checks passed.` |
| `git diff --check` | Passed. |

The seeded 10-over smoke sample averaged 113.7 runs per innings (11.37 runs per over), with an 8.3% run-out share across 24 dismissals. Run scoring remains above the plan's target band; this is recorded for WP21 and the band was left unchanged.

## Acceptance

- A new CPU batter uses the settle-curve start width with 54 legal balls already bowled in the innings.
- Cover-drive edges travel behind square on the off side for right-handed batters and mirror for left-handed batters.
- Replaying the same seeded edge produces the same resolution.
