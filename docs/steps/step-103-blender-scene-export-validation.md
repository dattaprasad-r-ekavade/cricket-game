# Step 103 — Validate Blender scene exports headlessly

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

## Scope

Exercise the actual batter and bowler Blender source-scene exporters after Step 102 and verify that they produce the same compact animation contracts already embedded in the humanoid GLBs. Keep generated outputs in temporary storage and do not launch the game or a visual capture.

## Changes

Added the Blender script directory to `sys.path` before importing `player_animation_contract` in `build_practice_batter.py`. Running the scene-export command with Blender 5.2.2 had exposed that Blender does not automatically put this adjacent script directory on the Python module search path.

## Verification

- Downloaded the official Blender 5.2.2 Windows portable archive to temporary storage and verified it against Blender's published SHA-256 manifest.
- Ran the batter scene export with its two batting-step clips and the bowler scene export with its run-up, delivery, and three fielder clips. Both completed in background mode and wrote their `.blend`, `.scplayer.json`, and compact contract outputs to the temporary directory.
- Exported batter: 13 bones, 85 meshes, 7 clips. Exported bowler: 13 bones, 38 meshes, 10 clips.
- Both generated compact contracts compare exactly with the checked-in contracts, including all events and root-motion samples.
- `validate-player` accepted both temporary `.scplayer.json` outputs.
- `pwsh -NoProfile -File tools/review.ps1` passed in the default headless mode. Its xUnit, GLB, asset, calibration, deterministic-match, and build checks passed; game-host checks and renderer captures remained off.
- No tracked Blender source or player asset was overwritten. No game window or capture was started.
- Hosted Windows validation passed in [run 37601970419](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37601970419).

## Remaining

This validates the exporter path; it does not complete C1. A dedicated fielder rig and textured character assets remain open, as do human visual review and the keyboard/GamePad playtests. Step 104 separately retires `.scplayer.json` from runtime loading while keeping parity fixtures for the migration test.
