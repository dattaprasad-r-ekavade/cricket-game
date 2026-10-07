# Step 94 — Consolidate delivery preparation

## Change

Moved the existing delivery-selection branches and human-batting pace preparation behind one `MatchController` method. CPU pitch aiming still takes precedence, seeded CPU bowling remains limited to the first preset outside verification, and developer and verification bypasses retain their existing behavior.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 21 Simulation tests and 90 Game helper tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed with a zero-warning Release build and headless asset/simulation review.
- Hosted Windows validation passed: [run 37585446857](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37585446857).
- No game host, window, or renderer capture was started.
