# Step 70 — Windows validation workflow

**Status:** workflow added; hosted run pending.

## Change

Added a Windows GitHub Actions workflow for pushes, pull requests, and manual runs. It selects the repository SDK from `global.json`, installs Python for the GLB structure validators, and runs `tools/review.ps1 -SkipGame -SkipCaptures`.

The job compiles the shipping solution, validates humanoid GLBs and game content, and runs simulation, calibration, and seeded-batch checks. It skips the game host and renderer captures while the owner retests the latest player-facing changes.

## Verification

- The same headless review passed locally on the Step 69 commit with zero build warnings or errors.
- Verify the first hosted run after pushing this workflow before marking the plan item complete.
