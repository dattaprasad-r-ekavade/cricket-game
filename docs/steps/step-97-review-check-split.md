# Step 97 — Split the gameplay review checks

## Change

Split the oversized gameplay review checks into focused files: the main check file is 234 lines and the advanced checks are 445 lines. The extracted check bodies were preserved, and every Game source file is now below roughly 600 lines. This completes the planned match-controller, HUD-presentation, input-router, and camera-director extraction target while keeping the remaining host rendering responsibilities in `Game1`.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 21 Simulation tests and 96 Game helper tests passed; 0 failed.
- Hosted Windows validation passed: [run 37586570979](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37586570979).
- No game host, window, or renderer capture was started.
