# Step 69 — MonoGame GLB player runtime

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

## Scope

Load the batter and bowler GLBs in the shipping MonoGame game, preserving the current animation/event/root-motion behavior while retaining `.scplayer.json` as the analyzer and authoring reference during migration.

## Changes

- Added SharpGLTF.Core 1.0.6 to `SuperCricket.Content` and added `PlayerGlbLoader`. `PlayerAsset.Load` dispatches `.glb` files to the loader and continues validating `.scplayer.json` files as before.
- The loader imports the one-skin humanoid profile: topologically ordered joints, inverse bind matrices, triangle primitives, four joint influences, material base-colour factors, clip events, and root-motion metadata. It samples glTF local transforms at 30 Hz and removes root translation from the pose because `PlayerAnimator` applies the root-motion track separately.
- Added `PoseSpace` to player assets and hierarchy composition to `PlayerAnimator`; legacy JSON assets remain global-space and preserve their previous path.
- Switched the game’s batter, bowler, and shared fielder renderer to the imported humanoid GLBs. Added both GLBs to the game output and runtime import checks to `tools/review.ps1`.
- Updated the Blender GLB exporter to copy source diffuse colours into unlinked default Principled shaders. Runtime captures had exposed white skin/gear from default Blender shader colours despite correct viewport diffuse colours.
- Updated the [player asset contract](../design/player-asset-contract.md) and README. The current runtime importer intentionally rejects non-identity mesh transforms, non-triangle primitives, textured materials, and non-opaque materials rather than silently dropping them.

## Verification

- `dotnet build SuperCricket.sln -c Release` — passed with no warnings or errors.
- `validate-player` loaded both GLBs through the C# SharpGLTF importer: batter 61 bones/89 meshes/8 clips; bowler 61 bones/38 meshes/10 clips. Source event and root-motion preservation is also checked against both `.scplayer.json` references by the Blender validator in the shared review.
- `--verify-gameplay` — passed all gameplay review checks, including batting timing, authored footwork completion, bowling/fielder events, and contact at 30/60/120 fps.
- Captured and inspected the GLB-backed behind-striker view, bowler-end view, off-side footwork, and ball-release pose. Source kit and skin colours now render correctly; the player model remains the existing low-detail practice mannequin.
- Full `pwsh -File tools/review.ps1` — passed. The review completed successfully and reported its CSVs and optional renderer captures in `artifacts/`.

The batter and bowler now use GLB at runtime. Shared fielders currently reuse the bowler GLB. `.scplayer.json` remains in the content/tool workflow, and C1 stays open for a dedicated fielder rig, broader material/texture support, and eventual retirement of the legacy format.
