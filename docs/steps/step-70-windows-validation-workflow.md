# Step 70 — Windows validation workflow

**Status:** complete; hosted run passed on 7 October 2026.

## Change

Added a Windows GitHub Actions workflow for pushes, pull requests, and manual runs. It selects the repository SDK from `global.json`, installs Python for the GLB structure validators, and runs `tools/review.ps1 -SkipGame -SkipCaptures`.

The job compiles the shipping solution, validates humanoid GLBs and game content, and runs simulation, calibration, and seeded-batch checks. It skips the game host and renderer captures while the owner retests the latest player-facing changes.

## Verification

- The same headless review passed locally on the Step 69 commit with zero build warnings or errors.
- The first hosted attempt completed the full review but returned the stale exit code from an intentional invalid-input check. The workflow now invokes the review in a child PowerShell process so the script's final success or thrown failure controls the job result.
- The first hosted attempt completed the full review but returned a stale exit code from an intentional invalid-input check. The child PowerShell invocation fixed the workflow result handling.
- Hosted retry passed all steps: [run 37568952887](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37568952887).
