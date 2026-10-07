# Step 100 — Promote humanoid GLBs in batting analysis

## Change

Moved the review workflow, CPU-batting regression inputs, Godot-trial simulation, analyzer examples, and physics-match batches to the humanoid batter and bowler GLBs. The legacy `.scplayer.json` files remain Blender authoring sources and migration-comparison fixtures. Updated CLI usage text and the player asset contract to describe the supported GLB pose, root-motion, material, and texture profile.

Recalibrated `idealInputDelaySeconds` from the GLB analyzer's highest-quality samples:

| Delivery | Defence | Drive | Loft |
| --- | ---: | ---: | ---: |
| Standard pace | 0.150 s | 0.225 s | 0.225 s |
| Wide pace | 0.275 s | 0.325 s | 0.325 s |
| No-ball pace | 0.150 s | 0.225 s | 0.225 s |
| Yorker pace | 0.175 s | 0.250 s | 0.250 s |

The analyzer now composes only the forearm's ancestor chain and reuses sampled matrices across footwork candidates for each input delay. The 0.075 s live timing-window width remains unchanged for the pending human retest.

## Verification

- `dotnet test SuperCricket.sln -c Release --no-restore`: 22 Simulation, 98 Game, and 7 Content tests passed; 0 failed (127 total).
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with GLB analyzer inputs, calibration checks, repeated outputs, CPU batting, footwork, the Godot trial build, and the six-match 10-over physics review.
- Hosted Windows validation passed: [run 37594628184](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37594628184).
- No game host, window, or renderer capture was started.

## Remaining

The analyzer-derived input delays are calibrated against the current clips. Human confirmation of the timing-window feel, visual review of authored textures, a dedicated fielder rig, and retirement of the legacy authoring files remain open.
