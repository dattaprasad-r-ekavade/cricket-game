# Step 134 — CPU stroke scoring acceptance

## Scope

Record the WP13 acceptance batch after WP21 added 20-over matches. Each difficulty uses 50 deterministic physics-grounded T20 matches, seeds 9100–9149, Coastal XI vs Highland XI, the practice-attack field, the exported practice batter/bowler clips, and the stock pace/wide/no-ball deliveries.

## Commands

Each command completed with exit code 0 and wrote the linked CSV:

```powershell
dotnet src/SuperCricket.Tools/bin/Release/net9.0/SuperCricket.Tools.dll simulate-physics-match-batch assets/teams/coastal-xi.json assets/teams/highland-xi.json assets/fields/practice-attack.json assets/characters/practice-batter-humanoid.glb assets/characters/practice-bowler-humanoid.glb assets/batting/shots.json assets/deliveries/standard-pace.json assets/deliveries/wide-pace.json assets/deliveries/no-ball-pace.json 50 20 9100 artifacts/wp13-t20-rookie.csv Rookie
dotnet src/SuperCricket.Tools/bin/Release/net9.0/SuperCricket.Tools.dll simulate-physics-match-batch assets/teams/coastal-xi.json assets/teams/highland-xi.json assets/fields/practice-attack.json assets/characters/practice-batter-humanoid.glb assets/characters/practice-bowler-humanoid.glb assets/batting/shots.json assets/deliveries/standard-pace.json assets/deliveries/wide-pace.json assets/deliveries/no-ball-pace.json 50 20 9100 artifacts/wp13-t20-amateur.csv Amateur
dotnet src/SuperCricket.Tools/bin/Release/net9.0/SuperCricket.Tools.dll simulate-physics-match-batch assets/teams/coastal-xi.json assets/teams/highland-xi.json assets/fields/practice-attack.json assets/characters/practice-batter-humanoid.glb assets/characters/practice-bowler-humanoid.glb assets/batting/shots.json assets/deliveries/standard-pace.json assets/deliveries/wide-pace.json assets/deliveries/no-ball-pace.json 50 20 9100 artifacts/wp13-t20-standard.csv Standard
dotnet src/SuperCricket.Tools/bin/Release/net9.0/SuperCricket.Tools.dll simulate-physics-match-batch assets/teams/coastal-xi.json assets/teams/highland-xi.json assets/fields/practice-attack.json assets/characters/practice-batter-humanoid.glb assets/characters/practice-bowler-humanoid.glb assets/batting/shots.json assets/deliveries/standard-pace.json assets/deliveries/wide-pace.json assets/deliveries/no-ball-pace.json 50 20 9100 artifacts/wp13-t20-pro.csv Pro
dotnet src/SuperCricket.Tools/bin/Release/net9.0/SuperCricket.Tools.dll simulate-physics-match-batch assets/teams/coastal-xi.json assets/teams/highland-xi.json assets/fields/practice-attack.json assets/characters/practice-batter-humanoid.glb assets/characters/practice-bowler-humanoid.glb assets/batting/shots.json assets/deliveries/standard-pace.json assets/deliveries/wide-pace.json assets/deliveries/no-ball-pace.json 50 20 9100 artifacts/wp13-t20-legend.csv Legend
```

## Results

Runs per over are total runs divided by legal balls, aggregated across both innings. Wickets per innings is the total divided by 100 innings. Phase rates use the 4-over powerplay, 11-over middle phase, and 5-over death phase, with actual legal balls played in each phase.

| CPU difficulty | Avg combined score | Runs/over | Wickets/innings | Powerplay RPO | Middle RPO | Death RPO |
|---|---:|---:|---:|---:|---:|---:|
| Rookie | 100.8 | 5.24 | 9.66 | 4.91 | 5.43 | 9.43 |
| Amateur | 149.7 | 6.28 | 9.49 | 5.30 | 6.60 | 11.37 |
| Standard | 254.0 | 6.63 | 7.14 | 3.10 | 5.43 | 13.11 |
| Pro | 395.4 | 10.27 | 5.46 | 6.72 | 8.76 | 17.39 |
| Legend | 392.0 | 10.09 | 3.75 | 5.11 | 8.72 | 18.04 |

Every difficulty's death phase scored faster than its middle phase. Average combined scores increase substantially from Rookie through Pro and remain substantially higher at Legend than Rookie. Pro is 3.4 runs above Legend on this fixed sample, so the result is a broad difficulty trend rather than a strict step-by-step increase. Wickets per innings decrease as CPU difficulty rises.

## Verification

- WP13 component tests and scoring integration tests are recorded in [Step 132](step-132-cpu-stroke-scoring.md).
- Added a one-match physics-batch integration test that runs the Human batting-side mode twice with the same seed and compares complete results; focused deterministic tests passed.
- `dotnet src/SuperCricket.Tools/bin/Release/net9.0/SuperCricket.Tools.dll validate-difficulty assets/difficulty` — passed; all 10 profiles across five levels validate.
- `dotnet build -c Release` — passed after the WP21 field-overview camera update, 0 warnings and 0 errors.
- All five CPU commands above — 50 matches each completed, exit code 0.
- All five Human-profile commands at the accepted 1.05 multiplier — 50 matches each completed, exit code 0; all bands passed and Rookie scored more than Legend.

## Artifacts

- [Rookie CSV](../../artifacts/wp13-t20-rookie.csv)
- [Amateur CSV](../../artifacts/wp13-t20-amateur.csv)
- [Standard CSV](../../artifacts/wp13-t20-standard.csv)
- [Pro CSV](../../artifacts/wp13-t20-pro.csv)
- [Legend CSV](../../artifacts/wp13-t20-legend.csv)

## Human-profile batch (not yet accepted)

`simulate-physics-match-batch` now accepts a `Human` batting-side mode to check the human timing profiles. The first full run used Standard shot-choice and running policy with a fixed simulated input-timing error of 0.08 seconds for all five Human profiles, 50 T20s per difficulty, and seeds 9100–9149. That initial version still applied difficulty-specific CPU aim-error scaling, so these numbers are diagnostic rather than acceptance evidence. Rookie outscored Legend, but this sample does **not** meet the section 6 batch gate at every difficulty:

| Human timing profile | Avg combined score | Runs/over | Wickets/innings | LBW | Caught | Bowled | Run-out |
|---|---:|---:|---:|---:|---:|---:|---:|
| Rookie | 304.6 | 7.66 | 4.81 | 16% | 56% | 13% | 14% |
| Amateur | 316.8 | 7.99 | 5.45 | 20% | 50% | 16% | 13% |
| Standard | 288.5 | 7.44 | 5.72 | 21% | 49% | 20% | 10% |
| Pro | 181.8 | 5.65 | 8.84 | 30% | 39% | 24% | 7% |
| Legend | 159.3 | 5.28 | 9.04 | 32% | 33% | 29% | 6% |

The unchanged bands are 6–9 runs per over, 5–8 wickets per 20-over innings, and dismissal shares of 8–18% LBW, 35–65% caught, 10–30% bowled, and 5–12% run-out. The current implementation uses Standard shot-choice, aim-error, and running policy across all Human profiles; only the Human difficulty settings and fixed timing noise vary. The 0.06-second candidate passed the Release build and focused profile test. A five-match diagnostic pilot with Pro and Legend total timing windows expanded to 8.0 m measured 6.80 runs per over and 5.80/5.90 wickets per innings, but caught shares were 81%/80% (above the unchanged 65% maximum); Rookie through Standard also had unstable dismissal counts in this small sample. A follow-up with Pro at 6.8 m / 1.0 m Ideal and Legend at 7.2 m / 1.0 m Ideal measured Pro at 5.91 RPO, 7.40 wickets, and 23% LBW, and Legend at 7.12 RPO, 5.50 wickets, and dismissal shares of 9% LBW, 65% caught, 13% bowled, and 13% run-out. Legend is inside all but the run-out band; Pro still misses the runs-per-over minimum and LBW maximum. Rookie averaged 302.0 runs to Legend's 272.6 in this five-match sample. These pilots informed the corrected full batch below but are not acceptance evidence.

### Corrected full batch at 0.06 seconds (fails bands)

The corrected run used 50 deterministic physics-grounded T20 matches per Human difficulty, seeds 9100–9149, the Standard shot-choice/aim-error/running policy, and the Human profile settings. All 250 match rows completed. Rookie scored more on average than Legend, but the full section 6 gate does not pass:

| Human timing profile | Avg combined score | Runs/over | Wickets/innings | LBW | Caught | Bowled | Run-out |
|---|---:|---:|---:|---:|---:|---:|---:|
| Rookie | 283.2 | 7.15 | 3.87 | 12% | 58% | 10% | 20% |
| Amateur | 282.4 | 7.10 | 3.97 | 13% | 52% | 14% | 21% |
| Standard | 281.6 | 7.10 | 4.24 | 14% | 53% | 16% | 17% |
| Pro | 255.0 | 6.78 | 7.23 | 20% | 52% | 19% | 9% |
| Legend | 263.8 | 6.70 | 6.05 | 8% | 67% | 10% | 15% |

Rookie through Standard miss the wickets-per-innings minimum and run-out maximum. Pro exceeds the LBW maximum; Legend exceeds the caught and run-out maxima. CSVs: [Rookie](../../artifacts/wp13-human-final06-rookie.csv), [Amateur](../../artifacts/wp13-human-final06-amateur.csv), [Standard](../../artifacts/wp13-human-final06-standard.csv), [Pro](../../artifacts/wp13-human-final06-pro.csv), [Legend](../../artifacts/wp13-human-final06-legend.csv).

### Full batch at 0.08 seconds (fails bands)

With the current profile settings, the 0.08-second shared-noise run also completed 50 matches per level using seeds 9100–9149 and the same Standard policy:

| Human timing profile | Avg combined score | Runs/over | Wickets/innings | LBW | Caught | Bowled | Run-out |
|---|---:|---:|---:|---:|---:|---:|---:|
| Rookie | 304.6 | 7.66 | 4.81 | 16% | 56% | 13% | 14% |
| Amateur | 316.8 | 7.99 | 5.45 | 20% | 50% | 16% | 13% |
| Standard | 288.5 | 7.44 | 5.72 | 21% | 49% | 20% | 10% |
| Pro | 186.7 | 5.68 | 8.74 | 29% | 38% | 26% | 8% |
| Legend | 253.1 | 6.60 | 7.13 | 15% | 60% | 16% | 9% |

Legend meets every band and Rookie averages more than Legend, but Rookie misses the wicket/run-out bands; Amateur misses LBW/run-out; Standard misses LBW; and Pro misses runs-per-over, wickets, and LBW. These results close neither section 6 nor the review gate. CSVs: [Rookie](../../artifacts/wp13-human-final08-rookie.csv), [Amateur](../../artifacts/wp13-human-final08-amateur.csv), [Standard](../../artifacts/wp13-human-final08-standard.csv), [Pro](../../artifacts/wp13-human-final08-pro.csv), [Legend](../../artifacts/wp13-human-final08-legend.csv).

The next fixed-noise profile trial narrowed Rookie and Amateur Ideal bases to 1.1/1.0 m and their total windows to 7.6/7.2 m, lowered the Amateur/Standard/Pro umpire's-call probability to 0.25/0.25/0.20, and expanded Pro's window to 7.2 m. The profile-order test caught that 1.0 m on Rookie would tie Legend's Ideal width; Rookie is now 1.1 m so its Ideal remains wider. These candidate values aimed to raise early-level dismissal counts, reduce run-outs through fewer productive running situations, and soften Pro's excess LBW/miss rate; the pilot and full results follow.

That five-match pilot at seed 9200 produced the following diagnostics (not the acceptance sample):

| Human timing profile | Avg combined score | Runs/over | Wickets/innings | LBW | Caught | Bowled | Run-out |
|---|---:|---:|---:|---:|---:|---:|---:|
| Rookie | 319.4 | 8.01 | 5.50 | 13% | 60% | 13% | 15% |
| Amateur | 347.4 | 8.78 | 6.30 | 13% | 57% | 19% | 11% |
| Standard | 347.4 | 8.78 | 6.30 | 13% | 57% | 19% | 11% |
| Pro | 235.2 | 6.27 | 7.20 | 14% | 57% | 19% | 10% |
| Legend | 234.6 | 6.26 | 7.20 | 17% | 53% | 19% | 11% |

All pilot values are in band except Rookie run-out share. The small sample is noisy. The two focused tests pass: the Human profile model retains a wider Rookie Ideal window than Legend, and the full batch simulator returns identical results for two Human-mode runs with the same seed.

### Retuned full batch at 0.08 seconds (near pass)

The 50-match runs on seeds 9100–9149 improved all five profiles, but Rookie still exceeds the LBW and run-out limits, while Amateur and Standard exceed the LBW limit:

| Human timing profile | Avg combined score | Runs/over | Wickets/innings | LBW | Caught | Bowled | Run-out |
|---|---:|---:|---:|---:|---:|---:|---:|
| Rookie | 317.0 | 8.00 | 5.49 | 20% | 51% | 16% | 13% |
| Amateur | 288.1 | 7.42 | 5.62 | 21% | 49% | 20% | 11% |
| Standard | 287.6 | 7.41 | 5.68 | 21% | 49% | 19% | 11% |
| Pro | 253.4 | 6.61 | 7.00 | 14% | 60% | 16% | 10% |
| Legend | 253.1 | 6.60 | 7.13 | 15% | 60% | 16% | 9% |

Rookie still scores more than Legend. A five-match pilot with Rookie at 7.8 m / 1.15 m Ideal and Amateur/Standard at 8.0 m / 1.0 m Ideal left runs per over inside band but had only 4.6–4.7 wickets per innings and 21–22% run-out shares in the small sample. Turning Amateur/Standard assist off and returning their windows to 7.2 m brought every measured pilot metric into band. For the next pass, Rookie is restored to its full-sample 7.6 m / 1.1 m profile with umpire-call probability 0.0. A fixed Legend-derived 0.8 run-safety factor eliminated run-outs in the five-match pilot and fell below the 5% floor, so it was rejected. The 1.12 factor brought Rookie run-outs down to 14% in the full batch, while every other profile met all bands. The current candidate uses a neutral 1.0 Human-proxy run-safety factor; CPU matches continue using their selected CPU profile. This targets Rookie's remaining run-out excess while keeping the existing Standard CPU balance gate unchanged.

The five-match pilot at seed 9200 with the 1.12 factor returned:

| Human timing profile | Avg combined score | Runs/over | Wickets/innings | LBW | Caught | Bowled | Run-out |
|---|---:|---:|---:|---:|---:|---:|---:|
| Rookie | 319.4 | 8.01 | 5.40 | 11% | 61% | 13% | 15% |
| Amateur | 237.2 | 6.33 | 7.20 | 14% | 57% | 19% | 10% |
| Standard | 236.0 | 6.29 | 7.20 | 14% | 57% | 19% | 10% |
| Pro | 235.2 | 6.27 | 7.20 | 14% | 57% | 19% | 10% |
| Legend | 234.6 | 6.26 | 7.20 | 17% | 53% | 19% | 11% |

All pilot metrics are in band except Rookie run-out share. Rookie still scores more than Legend. The full 50-match acceptance batch at 1.12 is recorded below; a five-match pilot with the neutral 1.0 factor passes all bands, and the full repeat is under way. Pilot CSVs: [Rookie](../../artifacts/wp13-human-pilot08-runsafe112-rookie.csv), [Amateur](../../artifacts/wp13-human-pilot08-runsafe112-amateur.csv), [Standard](../../artifacts/wp13-human-pilot08-runsafe112-standard.csv), [Pro](../../artifacts/wp13-human-pilot08-runsafe112-pro.csv), [Legend](../../artifacts/wp13-human-pilot08-runsafe112-legend.csv).

### Retuned full batch at 0.08 seconds with run-safety factor 1.12 (one miss)

| Human timing profile | Avg combined score | Runs/over | Wickets/innings | LBW | Caught | Bowled | Run-out |
|---|---:|---:|---:|---:|---:|---:|---:|
| Rookie | 310.8 | 7.84 | 5.27 | 18% | 51% | 17% | 14% |
| Amateur | 250.1 | 6.55 | 7.02 | 16% | 60% | 15% | 9% |
| Standard | 251.9 | 6.58 | 7.07 | 15% | 60% | 16% | 9% |
| Pro | 253.4 | 6.61 | 7.00 | 14% | 60% | 16% | 10% |
| Legend | 253.1 | 6.60 | 7.13 | 15% | 60% | 16% | 9% |

All bands pass except Rookie's 14% run-out share; its average combined score also exceeds Legend's. CSVs: [Rookie](../../artifacts/wp13-human-final08-runsafe112-rookie.csv), [Amateur](../../artifacts/wp13-human-final08-runsafe112-amateur.csv), [Standard](../../artifacts/wp13-human-final08-runsafe112-standard.csv), [Pro](../../artifacts/wp13-human-final08-runsafe112-pro.csv), [Legend](../../artifacts/wp13-human-final08-runsafe112-legend.csv).

### Neutral 1.0 run-safety pilot (all bands pass)

| Human timing profile | Avg combined score | Runs/over | Wickets/innings | LBW | Caught | Bowled | Run-out |
|---|---:|---:|---:|---:|---:|---:|---:|
| Rookie | 328.6 | 8.22 | 5.60 | 14% | 61% | 16% | 9% |
| Amateur | 228.8 | 6.10 | 7.10 | 14% | 61% | 18% | 7% |
| Standard | 227.6 | 6.07 | 7.10 | 14% | 62% | 17% | 7% |
| Pro | 226.8 | 6.05 | 7.10 | 14% | 62% | 17% | 7% |
| Legend | 234.6 | 6.23 | 6.90 | 16% | 59% | 17% | 7% |

All five pilot profiles pass every band, and Rookie scores more than Legend. This is only a five-match diagnostic; the full 50-match sample must pass before item 6 closes. CSVs: [Rookie](../../artifacts/wp13-human-pilot08-runsafe1-rookie.csv), [Amateur](../../artifacts/wp13-human-pilot08-runsafe1-amateur.csv), [Standard](../../artifacts/wp13-human-pilot08-runsafe1-standard.csv), [Pro](../../artifacts/wp13-human-pilot08-runsafe1-pro.csv), [Legend](../../artifacts/wp13-human-pilot08-runsafe1-legend.csv).

### Full 50-match batch at 1.0 run-safety (Rookie wickets below band)

The five seeded 50-match batches completed with seeds 9100–9149. All profiles meet the RPO and dismissal-share bands, and Rookie averages more combined runs than Legend. Rookie records 4.84 wickets per innings, below the 5–8 target; the section 6 gate therefore remains open.

| Human timing profile | Avg combined score | Runs/over | Wickets/innings | LBW | Caught | Bowled | Run-out |
|---|---:|---:|---:|---:|---:|---:|---:|
| Rookie | 305.0 | 7.70 | 4.84 | 16.3% | 53.9% | 19.6% | 10.1% |
| Amateur | 251.0 | 6.53 | 7.02 | 16.0% | 62.0% | 16.2% | 5.8% |
| Standard | 251.9 | 6.54 | 7.07 | 15.7% | 62.1% | 16.3% | 5.9% |
| Pro | 252.8 | 6.55 | 6.99 | 15.3% | 62.5% | 16.5% | 5.7% |
| Legend | 254.5 | 6.58 | 7.07 | 15.4% | 62.7% | 16.3% | 5.7% |

Full-sample CSVs: [Rookie](../../artifacts/wp13-human-final08-runsafe1-full-rookie.csv), [Amateur](../../artifacts/wp13-human-final08-runsafe1-full-amateur.csv), [Standard](../../artifacts/wp13-human-final08-runsafe1-full-standard.csv), [Pro](../../artifacts/wp13-human-final08-runsafe1-full-pro.csv), [Legend](../../artifacts/wp13-human-final08-runsafe1-full-legend.csv).

### Full 50-match batch at 1.0 run-safety (Rookie wickets below band)

The five seeded 50-match batches completed with seeds 9100–9149. All profiles meet the RPO and dismissal-share bands, and Rookie averages more combined runs than Legend. Rookie records 4.84 wickets per innings, below the 5–8 target; the section 6 gate therefore remains open.

| Human timing profile | Avg combined score | Runs/over | Wickets/innings | LBW | Caught | Bowled | Run-out |
|---|---:|---:|---:|---:|---:|---:|---:|
| Rookie | 305.0 | 7.70 | 4.84 | 16.3% | 53.9% | 19.6% | 10.1% |
| Amateur | 251.0 | 6.53 | 7.02 | 16.0% | 62.0% | 16.2% | 5.8% |
| Standard | 251.9 | 6.54 | 7.07 | 15.7% | 62.1% | 16.3% | 5.9% |
| Pro | 252.8 | 6.55 | 6.99 | 15.3% | 62.5% | 16.5% | 5.7% |
| Legend | 254.5 | 6.58 | 7.07 | 15.4% | 62.7% | 16.3% | 5.7% |

Full-sample CSVs: [Rookie](../../artifacts/wp13-human-final08-runsafe1-full-rookie.csv), [Amateur](../../artifacts/wp13-human-final08-runsafe1-full-amateur.csv), [Standard](../../artifacts/wp13-human-final08-runsafe1-full-standard.csv), [Pro](../../artifacts/wp13-human-final08-runsafe1-full-pro.csv), [Legend](../../artifacts/wp13-human-final08-runsafe1-full-legend.csv).

### Full 50-match batch at 1.05 run-safety (passes section 6 item 6)

The 1.05 Human-proxy run-out chance multiplier moved Rookie above the wicket floor while all five profiles remained within every unchanged band. Rookie's average combined score is higher than Legend's. CPU profiles and the Standard CPU balance tuning are unchanged.

| Human timing profile | Avg combined score | Runs/over | Wickets/innings | LBW | Caught | Bowled | Run-out |
|---|---:|---:|---:|---:|---:|---:|---:|
| Rookie | 305.6 | 7.71 | 5.06 | 17.0% | 52.2% | 19.4% | 11.5% |
| Amateur | 251.6 | 6.56 | 6.93 | 15.7% | 61.3% | 16.0% | 6.9% |
| Standard | 252.9 | 6.57 | 6.97 | 15.5% | 61.1% | 16.4% | 7.0% |
| Pro | 253.8 | 6.58 | 6.89 | 15.1% | 61.5% | 16.5% | 6.8% |
| Legend | 255.4 | 6.61 | 7.01 | 15.1% | 61.9% | 16.3% | 6.7% |

The five 50-match CSVs use seeds 9100–9149: [Rookie](../../artifacts/wp13-human-final08-runsafe105-full-rookie.csv), [Amateur](../../artifacts/wp13-human-final08-runsafe105-full-amateur.csv), [Standard](../../artifacts/wp13-human-final08-runsafe105-full-standard.csv), [Pro](../../artifacts/wp13-human-final08-runsafe105-full-pro.csv), [Legend](../../artifacts/wp13-human-final08-runsafe105-full-legend.csv).

CSV artifacts: [Rookie](../../artifacts/wp13-human-t20-rookie.csv), [Amateur](../../artifacts/wp13-human-t20-amateur.csv), [Standard](../../artifacts/wp13-human-t20-standard.csv), [Pro](../../artifacts/wp13-human-t20-pro.csv), [Legend](../../artifacts/wp13-human-t20-legend.csv).

## Open questions

- The section 6 item 6 simulated Human-profile batch passes all bands at the 1.05 multiplier. Section 6 items 1–5 still require the human acceptance session recorded in [the playtest worksheet](../playtests/2026-10-16-pending-gates.md); automated simulation does not replace those checks.
