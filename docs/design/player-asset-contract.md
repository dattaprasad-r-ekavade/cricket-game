# Blender player asset contract

MonoGame now loads the 61-joint batter and bowler GLBs through `PlayerGlbLoader` in `SuperCricket.Content`, using SharpGLTF.Core for glTF 2.0 parsing. `PlayerAsset.Load` dispatches by file extension; its validated v1 `.scplayer.json` path remains the source contract for simulation analyzers and the Blender metadata exporter while fielder-specific content is still being migrated.

## GLB rig pilot

Step 63 adds `assets/characters/practice-batter-humanoid.blend` and `practice-batter-humanoid.glb` as an editable 61-joint humanoid pilot. Its hierarchy retains the named batting bones used by the v1 clips and adds pelvis/spine/chest/neck segments, clavicles and scapulae, jaw/eyes, hands, three articulated segments for each of five digits, and toe/heel joints. Glove details are weighted across the finger chains; the bat and glove are attached at the hands. The added `finger-grip-preview` action exercises those joints. Step 68 adds the matching `practice-bowler-humanoid.blend` and `.glb` with the bowler's 10 gameplay clips; it keeps the shared 61-joint skeleton but does not add a batter-only grip-preview action.

`tools/blender/validate_humanoid_glb.py` checks both GLBs' headers, 61-joint skins, required names, skinned primitives, and role-specific clips; it also verifies that every clip's `extras.superCricket` metadata preserves source events and sampled root motion. The batter validation checks all grip-preview channels. `PlayerGlbLoader` reads those animation extras, imports triangle primitives, material base-colour factors, skin weights and inverse-bind matrices, then samples local joint poses at 30 Hz. `PlayerAnimator` rebuilds the joint hierarchy before producing the skin palette; root translation is removed from the pose and applied separately through the embedded root-motion track. The runtime profile currently requires one identity-transformed skin, identity mesh nodes, opaque materials without image textures, and triangle primitives. The Blender exporter syncs source diffuse colours into unlinked default Principled shaders so the GLB retains the intended kit/gear colours. The Godot presentation trial still imports the batter GLB independently. The MonoGame game now loads these batter/bowler GLBs, while fielder-specific content and retirement of `.scplayer.json` remain open.

## Coordinate and skeleton rules

- `coordinateSystem` is `right-handed-y-up-metres`; all geometry, translations, event times, and root motion use metres and seconds.
- Blender coordinates convert to game coordinates as `(x, z, -y)`. Exported transforms also convert rotation and scale into that basis.
- Version 1 has one named skeleton root. Bone names are unique and each non-root parent appears earlier in the bone list. The loader permits 1–72 bones.
- Bind-pose and animation-sample scales must be positive and between 0.5 and 2.0 per axis. Apply object scale in Blender before export.
- A complete player mesh set must be 0.5–4.0 m tall on the game Y axis. This catches common centimetre/metre and object-scale mistakes while allowing plausible player rigs.
- The game batter needs the `forearm.R` attachment bone and a `Bat Blade` mesh. Shared crowd, bowler, and fielder assets use the same versioned skeleton format.

## Mesh and material rules

- Each mesh stores triangle positions, finite normals, UV coordinates, triangle indices, four bone-index/weight slots per vertex, and one diffuse RGB colour in the 0–1 range.
- Bone indices must exist in the asset skeleton; non-negative weights must sum to 1.0 within 0.002.
- Version 1 supports per-mesh diffuse colour only. UVs are retained, but the current player renderer uses a neutral texture and does not sample image maps. Texture-backed materials are not supported by this contract. Unknown JSON members are rejected rather than silently interpreted.

## Animation rules

- An asset contains at least two uniquely named clips. Each clip has a positive duration, at least two strictly increasing sample times within that duration, and one finite transform per bone in every sample.
- Clip events have unique non-empty names and times inside the clip duration. Gameplay reads named release and secured-ball events from the same exported data used for preview.
- `rootMotion` is the player-local root displacement from the first clip frame. The exporter extracts horizontal travel from sampled poses; the game applies it along the player's facing direction.
- The Blender scene remains the editable source. The JSON export is deterministic runtime data; do not edit its large sampled arrays by hand.

## Validation and import checks

`PlayerAsset.Load` and `validate-player` reject unsupported versions/coordinate systems, unknown JSON members, missing or duplicate bones, broken hierarchy, non-finite or non-invertible transforms, invalid mesh arrays/influences, out-of-range colours, implausible player height or rig scale, malformed clip samples, and invalid events. `tools/review.ps1` validates both shipped player assets and confirms that a 10x bind-pose scale is rejected.

```powershell
dotnet run --project src/SuperCricket.Tools -- validate-player assets/characters/practice-batter.scplayer.json
dotnet run --project src/SuperCricket.Tools -- validate-player assets/characters/practice-bowler.scplayer.json
```

The short-match gate also loads both exported rigs in the actual game and exercises their authored animation clips. Changes to texture support, skeleton limits, coordinate conversion, or sampled-pose representation require a format-version decision and a clean export/playback regression.

