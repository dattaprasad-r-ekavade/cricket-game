# Step 99 — Correct GLB pose-space analysis

## Change

Persisted the root-pose translation after subtracting extracted GLB root motion, so the run-up displacement is not retained in both the actor motion and the skeletal root pose. Updated batting-practice analysis to compose local humanoid joint transforms through their parent chain before evaluating the bat forearm. The legacy global-pose path remains supported.

Added regressions for the bowler run-up root-motion separation and legacy-versus-GLB batting summaries. The parity check requires contact counts within one sample, identical best outcomes and footwork offsets, and best input delay/contact quality within 0.055.

## Verification

- `dotnet test SuperCricket.sln -c Release --no-restore`: 22 Simulation, 98 Game, and 7 Content tests passed; 0 failed (127 total).
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed the Release build, GLB and content validation, repeated seeded scenarios, and headless simulation checks.
- Hosted Windows validation passed: [run 37590706510](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37590706510).
- No game host, window, or renderer capture was started.

## Remaining

This verifies analyzer compatibility, not final animation calibration. Step C6 remains open for human review of contact windows against authored clips. The batter and bowler GLBs also still need authored textures and visual review.
