# Step 98 — Import GLB base-color textures

## Change

Extended the graphics-free player asset model and SharpGLTF loader to import base-color PNG/JPEG images, glTF sampler settings, and `KHR_texture_transform` UV transforms. The MonoGame skinned renderer now binds the corresponding texture per material batch and restores the caller's sampler state after drawing. Untextured materials keep the white-texture fallback.

The current profile accepts opaque base-color materials with `TEXCOORD_0`; unsupported alpha modes, UV sets, and image formats fail with an explicit validation error. The batter and bowler assets are still untextured, so visual output from an authored textured character has not yet been reviewed.

## Verification

- `dotnet test SuperCricket.sln -c Release`: 21 Simulation, 98 Game, and 6 Content tests passed; 0 failed.
- `pwsh -NoProfile -File tools/review.ps1 -SkipGame -SkipCaptures`: passed the Release build, asset validators, and headless simulation checks.
- Hosted Windows validation passed: [run 37588775711](https://github.com/dattaprasad-r-ekavade/cricket-game/actions/runs/37588775711).
- No game host, window, or renderer capture was started.
