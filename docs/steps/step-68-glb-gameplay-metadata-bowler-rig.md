# Step 68 — GLB gameplay metadata and humanoid bowler

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

## Scope

Extend the Step 63 Blender-to-GLB pilot from a batter-only structural export to gameplay clips with portable event/root-motion metadata, and build the corresponding bowler rig without overwriting the v1 source assets.

## Changes

- Added `tools/blender/embed_player_metadata.py`. It adds `extras.superCricket` to each GLB animation with version, asset identity, coordinate system, animation name, timed events, and root-motion samples in metres/seconds. Non-gameplay actions such as the batter grip preview declare zero root motion.
- Generalized `build_humanoid_batter.py` to build either role. Batter-only glove/finger reweighting and the grip-preview action are skipped for bowlers; the bowler gets a rendered pose at the source `ball-release` event.
- Added `practice-bowler-humanoid.blend` and `.glb`: 61 joints, 38 skinned primitives, and 10 gameplay clips. The `overarm-delivery` metadata retains `ball-release` at about 0.667 s and the authored 0.45 m root displacement; `bowling-run-up` retains its 15 m sampled displacement.
- Updated the humanoid GLB validator and `tools/review.ps1` to compare both roles' embedded events and root-motion samples with the existing `.scplayer.json` source assets. Updated the README and [player asset contract](../design/player-asset-contract.md).

## Verification

- `python -m py_compile tools/blender/build_humanoid_batter.py tools/blender/embed_player_metadata.py tools/blender/validate_humanoid_glb.py` — passed.
- Batter GLB validation — passed: 61 joints, 8 clips, 89 skinned primitives, seven gameplay clips checked against their source metadata.
- Bowler GLB validation — passed: 61 joints, 10 clips, 38 skinned primitives, ten gameplay clips checked against their source metadata.
- Inspected `artifacts/step68-bowler-previews/step68-humanoid-bowler-release.png` in Blender output. It shows the release pose, though the bowler remains a low-detail practice model.
- Full `pwsh -File tools/review.ps1` — passed: both GLB metadata checks, Release build, gameplay gates, 20 deterministic live-match trace replays, and the full capture suite.

The metadata is now portable inside the GLBs, but MonoGame does not consume it yet. The GLBs remain migration pilots; `.scplayer.json` and its validators/analyzers remain the runtime contract until the importer and all player-role migrations are completed.
