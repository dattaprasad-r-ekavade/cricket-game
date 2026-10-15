# Step 133 — Match balance

## Baseline

Measured before the WP21 boundary, catching, LBW, and wicket tuning. Both runs used the physics batch simulator, Standard CPU, teams Coastal XI vs Highland XI, seeds 9100–9149, and the existing 42 m boundary.

| Format | Runs/over | Wickets/innings | Boundaries/innings | LBW | Caught | Bowled | Run-out | Powerplay runs/over | Middle runs/over | Death runs/over |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 10 overs | 12.56 | 2.43 | 26.02 | 37.4% | 16.0% | 33.7% | 12.8% | 5.57 | 13.13 | 17.80 |
| 20 overs | 13.15 | 4.45 | 55.63 | 38.4% | 17.8% | 34.2% | 9.7% | 6.47 | 14.01 | 18.06 |

The target bands are 6–9 runs per over; 3–6 wickets per 10-over innings and 5–8 per 20-over innings; LBW 8–18%, caught 35–65%, bowled 10–30%, and run-out 5–12%. The baseline misses most ranges: scoring and boundaries are high, wicket counts are low, and LBW/bowled shares dominate while catches are rare.

Baseline CSVs:

- [10-over Standard baseline](../../artifacts/wp21-baseline-10-standard.csv)
- [20-over Standard baseline](../../artifacts/wp21-baseline-20-standard.csv)

## Changes so far

- Added 20-over innings support to the match model, batch simulators, game over selector, and saved settings.
- Added CSV counts for catches resulting in dismissals, edges, aerial contacts, and runs in powerplay/middle/death phases.
- The first 50-match after-tuning pass used seeds 9100–9149. Scoring entered the target band at 10 overs but remained above it at 20; wicket and caught shares stayed below target in both formats.
- A field-position trial exposed the powerplay inner-ring constraint. The named-position catalogue now moves only the previously deep spots; the 62.5 m physics review field has four outfield positions spread between 31 and 46 m. The fielding model allows catches up to 4 m above the ground.
- Isolated the front/back-foot pad-contact regression under `PhysicsAutomatedMatchBatchSimulatorTests` so WP21 test ownership stays separate from WP13 CPU stroke-scoring tests; behavior is unchanged.
- Edged outcomes now carry farther behind the batter: edge speed is 55–75% of the authored stroke speed and launch angle is 15–28°. The 10- and 20-over replay will determine whether this creates the required keeper/slip opportunities.
- The authored attack ground Ideal speed ranges were raised 5%; review measures their median midpoint at 24.66 m/s. The first lofted-speed review found a true median of 27.89 m/s; all eight lofted Ideal speed ranges have since been raised by 0.25 m/s, bringing that median to 28.14 m/s. The physics review field keeps Cover inside the inner ring and spreads four outfielders between 31 and 46 m.
- Five-match pilots after the edge change recorded many more catch attempts. They are diagnostic only, not the acceptance sample.

| Format | Runs/over | Wickets/innings | LBW | Caught | Bowled | Run-out | Catch attempts |
|---|---:|---:|---:|---:|---:|---:|---:|
| 10 overs | 9.66 | 3.60 | 19.4% | 61.1% | 16.7% | 2.8% | 50 |
| 20 overs | 8.88 | 8.60 | 12.8% | 62.8% | 20.9% | 3.5% | 98 |

The 10-over pilot missed the upper RPO and LBW limits and the lower run-out limit. The 20-over pilot met RPO and dismissal-share bands but exceeded the wicket limit and missed the run-out minimum. A zero-distance throw when no runner remained caused the first T20 pilot attempt to stop; the simulator now skips that unnecessary throw and the repeated pilot completed.

## 50-match Standard sample after tuning

These runs use seeds 9100–9149, the 62.5 m boundary, the tuned stroke table, spread outfield positions, and catchable edge carry. This is the acceptance sample; it still fails run-out share in both formats, 10-over RPO, and the 20-over wicket ceiling.

| Format | Runs/over | Wickets/innings | Boundaries/innings | LBW | Caught | Bowled | Run-out | Max innings score | Powerplay RPO | Middle RPO | Death RPO | Catch attempts |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 10 overs | 9.31 | 4.86 | 15.96 | 15.2% | 62.8% | 19.8% | 2.3% | 129 | 4.06 | 10.03 | 14.89 | 508 |
| 20 overs | 8.87 | 8.30 | 25.82 | 16.5% | 61.4% | 18.7% | 3.4% | 237 | 4.53 | 10.40 | 13.83 | 881 |

The 50-match T20 phase shape has death overs scoring faster than the middle. Attack ground Ideal median is 24.98 m/s. The first lofted-speed calculation incorrectly used the upper middle value as the median; review measured the actual median at 27.89 m/s and prompted a 0.25 m/s increase for every lofted Ideal range. These first tuning CSVs are diagnostic, not accepted:

- [10-over Standard, 50 matches](../../artifacts/wp21-final-10-standard.csv)
- [20-over Standard, 50 matches](../../artifacts/wp21-final-20-standard.csv)

Standard CPU now commits with σ=0.08 s, attempts fewer runs (0.9×), and uses a dedicated −0.1 pacing aggression offset in middle/death phases while leaving the legacy difficulty aggression setting intact. The 1.25× and 1.2× full repeats missed the 12% run-out ceiling. Standard live run-out risk is now 1.15×; its 50-match repeats pass every measured band.

The 50-match repeats at 1.2× completed with 6.05/6.65 RPO and 3.47/7.42 wickets per innings at 10/20 overs. Run-out shares were 12.39% (43/347) and 12.26% (91/742); both missed the unchanged 12% ceiling. At 1.15×, 36/338 10-over dismissals and 64/713 T20 dismissals were run-outs; every measured band passes.

| Risk | Format | Runs/over | Wickets/innings | LBW | Caught | Bowled | Run-out | Max score |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 1.25×, 50 matches | 10 overs | 6.02 | 3.57 | 14.6% | 57.1% | 14.8% | 13.4% | 103 |
| 1.25×, 50 matches | 20 overs | 6.65 | 7.34 | 16.6% | 54.9% | 14.6% | 13.9% | 182 |
| 1.2×, 50 matches | 10 overs | 6.05 | 3.47 | 14.7% | 57.6% | 15.3% | 12.39% | 103 |
| 1.2×, 50 matches | 20 overs | 6.65 | 7.42 | 15.5% | 56.3% | 15.9% | 12.26% | 182 |
| 1.15×, 50 matches | 10 overs | 6.11 | 3.38 | 15.4% | 58.3% | 15.7% | 10.65% | 103 |
| 1.15×, 50 matches | 20 overs | 6.62 | 7.13 | 15.6% | 59.8% | 15.7% | 8.98% | 182 |
| 1.2×, 5-match pilot | 10 overs | 6.39 | 3.60 | 13.9% | 63.9% | 13.9% | 8.3% | 87 |
| 1.2×, 5-match pilot | 20 overs | 7.14 | 6.90 | 14.5% | 59.4% | 14.5% | 11.6% | 159 |

The 1.25× 10-over sample missed only the run-out share; its 20-over sample missed run-outs and the 5–8 wicket target. Both 1.2× full samples missed run-out share only. The 1.2× pilot samples are diagnostic only. The review thresholds remain unchanged.

## Accepted 50-match Standard balance after loft speed correction

Seeds 9100–9149, 62.5 m boundary, tuned assets, 1.15× Standard running risk. `pwsh -File tools/review.ps1 -RunGameChecks -CaptureVisuals` completed with exit code 0 on 15 October 2026. The corrected final samples are:

| Format | Runs/over | Wickets/innings | Boundaries/innings | LBW | Caught | Bowled | Run-out | Max score | Powerplay RPO | Middle RPO | Death RPO |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 10 overs | 6.11 | 3.36 | 9.65 | 14.88% | 58.63% | 15.77% | 10.71% | 103 | 1.87 | 5.40 | 12.87 |
| 20 overs | 6.58 | 7.07 | 20.80 | 15.13% | 59.83% | 15.70% | 9.34% | 182 | 2.08 | 6.60 | 13.03 |

The current full-review samples use seeds 9100–9149. All measured match-balance bands pass at 10 and 20 overs; T20 death-over RPO exceeds middle-over RPO. Attack ground Ideal median remains 24.66 m/s and loft Ideal median 28.14 m/s. CSVs: [10 overs](../../artifacts/review-physics-balance-10-over-standard.csv), [20 overs](../../artifacts/review-physics-balance-20-over-standard.csv).

The same review run exposed a wide-ball contact check whose assertion included finishing the whole fielding sequence. At 60 fps the batter had reached 2.25 m off-side and made an Ideal, non-missed contact at 1.475 m; delivery completion was still playing out. The check now asserts the stated outcome (contact plus release/contact/sweet-spot markers) without imposing a post-contact fielding timeout. A trial with the analyzer's timing delay was discarded because it fired too early (7.754 m from the contact plane, TooEarly).

The review gate now also runs 50 seeded 20-over batches for each Human difficulty profile and checks the section 6 runs-per-over, wickets-per-innings, dismissal-share bands, and Rookie-over-Legend scoring direction. It collects out-of-band metrics across all five levels, completes the remaining review checks, then fails with the full list if any level misses. The Human proxy holds Standard shot-choice/aim policy constant and uses a 1.05 run-out chance multiplier across profiles; the full 50-match samples pass every band, and Rookie's average combined score exceeds Legend's. CPU matches continue using their selected CPU difficulty. The acceptance numbers and CSVs are recorded in [Step 134](step-134-cpu-stroke-acceptance.md).

The review fixes the live bowling checks to match current input controls and HUD wording. Replay determinism exposed stale fielder positions carrying from the previous match into a new innings; the field now resets to its selected formation on each innings' opening delivery and walks normally after subsequent changes. A field-overview camera preset and capture show the 62.5 m boundary rope and stadium bowl. The inspected [field-overview capture](../../artifacts/review-field-overview.png) keeps the whole rope and stands in frame; the batting map and HUD remain visible without covering the field center.

CSV files:

- [10-over first tuning sample](../../artifacts/wp21-final-10-standard.csv)
- [20-over first tuning sample](../../artifacts/wp21-final-20-standard.csv)
- [10-over 1.25× acceptance attempt](../../artifacts/wp21-acceptance-10-standard.csv)
- [20-over 1.25× acceptance attempt](../../artifacts/wp21-acceptance-20-standard.csv)
- [10-over 1.2× diagnostic pilot](../../artifacts/wp21-risk12-pilot-10.csv)
- [20-over 1.2× diagnostic pilot](../../artifacts/wp21-risk12-pilot-20.csv)
- [10-over 1.2× 50-match attempt](../../artifacts/wp21-acceptance-10-risk12.csv)
- [20-over 1.2× 50-match attempt](../../artifacts/wp21-acceptance-20-risk12.csv)
- [10-over accepted 1.15× sample](../../artifacts/wp21-acceptance-10-risk115.csv)
- [20-over accepted 1.15× sample](../../artifacts/wp21-acceptance-20-risk115.csv)

## Pending

- The 50-match T20 CPU-scoring report for each difficulty is recorded in [Step 134](step-134-cpu-stroke-acceptance.md).
- The full `pwsh tools/review.ps1 -RunGameChecks -CaptureVisuals` run passed after the contact-zone and 120 fps review-timeout fixes; see [Step 143](step-143-contact-zone-difficulty-fade.md). The automated Human-profile batch gate passes. Person-led playtest items remain in [the worksheet](../playtests/2026-10-16-pending-gates.md).

## Verification update

- `dotnet test tests/SuperCricket.Simulation.Tests/SuperCricket.Simulation.Tests.csproj -c Release --no-restore` — passed, 306 tests after separating the pad-contact regression test.
- `dotnet test SuperCricket.sln -c Release --no-build` — passed on 16 October 2026: 544 tests (308 Simulation, 203 Game, 33 Content), 0 failed and 0 skipped.
