# Step 104 — Retire legacy player loading from runtime

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

## Scope

Remove `.scplayer.json` loading from the shipping C# runtime and player CLI now that the batter, bowler, fielders, and review workflows use humanoid GLBs. Keep the archived JSON files only as migration-parity fixtures.

## Changes

- `PlayerAsset.Load` accepts humanoid `.glb` assets only and reports a clear unsupported-format error for legacy JSON inputs.
- Added a content test that guards the GLB-only runtime contract.
- The legacy-versus-GLB batting parity test now reads the two archived JSON fixtures inside the test assembly, validates them, and compares their batting summaries with GLB results. Production loading remains GLB-only.
- The default review validates only the two shipped humanoid GLBs; the dedicated parity test retains coverage for historical legacy data.
- Updated the player-asset contract and tool reference to identify legacy JSON as test-only compatibility data. The one-off Python bootstrap extractor remains available for old archives.

## Verification

- `dotnet test tests/SuperCricket.Content.Tests/SuperCricket.Content.Tests.csproj -c Release --no-restore`: all 9 tests passed, including the new legacy-input rejection and GLB bind-pose scale checks.
- `dotnet test tests/SuperCricket.Simulation.Tests/SuperCricket.Simulation.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~PlayerAssetParityTests`: the legacy-versus-GLB parity test passed.
- `dotnet test SuperCricket.sln -c Release --no-restore`: all 129 tests passed across Content (9), Simulation (22), and Game (98).
- `pwsh -NoProfile -File tools/review.ps1`: passed in default headless mode after moving the rig-scale rejection check to xUnit.
- Hosted Windows validation passed in [run 37603348527](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37603348527).
- No game window or renderer capture was started.

## Remaining

C1 remains open for a dedicated fielder rig and textured character assets. The archived `.scplayer.json` files are test fixtures, and the Blender exporter may write a parity artifact; neither is consumed by the game or C# player tools. Human visual review and keyboard/GamePad playtests are still open.
