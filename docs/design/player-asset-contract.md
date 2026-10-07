# Blender player asset contract

`PlayerAsset.Load` dispatches between legacy v1 `.scplayer.json` and glTF 2.0 `.glb`. The MonoGame runtime and batting review workflow use the 61-joint batter and bowler GLBs through `PlayerGlbLoader` in `SuperCricket.Content`, using SharpGLTF.Core. Legacy files remain Blender authoring sources and parity fixtures while fielder-specific content is migrated; they are not the current analyzer inputs.

## GLB rig pilot

Step 63 adds `assets/characters/practice-batter-humanoid.blend` and `practice-batter-humanoid.glb` as an editable 61-joint humanoid pilot. Its hierarchy retains the named batting bones used by the v1 clips and adds pelvis/spine/chest/neck segments, clavicles and scapulae, jaw/eyes, hands, three articulated segments for each of five digits, and toe/heel joints. Glove details are weighted across the finger chains; the bat and glove are attached at the hands. The added `finger-grip-preview` action exercises those joints. Step 68 adds the matching `practice-bowler-humanoid.blend` and `.glb` with the bowler's 10 gameplay clips; it keeps the shared 61-joint skeleton but does not add a batter-only grip-preview action.

`tools/blender/validate_humanoid_glb.py` checks both GLBs' headers, 61-joint skins, required names, skinned primitives, and role-specific clips; it also verifies that each clip's `extras.superCricket` metadata preserves source events and sampled root motion. The batter validation checks all grip-preview channels. `PlayerGlbLoader` reads those animation extras, imports triangle primitives, base-colour factors and embedded PNG/JPEG textures, sampler settings, UV transforms, skin weights, and inverse-bind matrices; animation samples are local joint poses at 30 Hz. `PlayerAnimator` rebuilds the joint hierarchy before producing the skin palette. The importer subtracts the embedded root-motion sample from the animated root pose, then the runtime applies that motion separately along the player's facing direction. The current GLB profile requires one identity-transformed skin, identity mesh nodes, opaque materials, `TEXCOORD_0`, triangle primitives, and supported PNG/JPEG base-colour images. The Blender exporter syncs source diffuse colours into unlinked default Principled shaders so untextured GLBs retain their intended kit/gear colours. The Godot presentation trial still imports the batter GLB independently. Fielder-specific content and retirement of `.scplayer.json` remain open.

## Coordinate and skeleton rules

- `coordinateSystem` is `right-handed-y-up-metres`; all geometry, translations, event times, and root motion use metres and seconds.
- Blender coordinates convert to game coordinates as `(x, z, -y)`. Exported transforms also convert rotation and scale into that basis.
- Legacy JSON version 1 has one named skeleton root. Bone names are unique and each non-root parent appears earlier in the bone list; its loader permits 1–72 bones. The humanoid GLB pilot has 61 joints.
- Humanoid GLB pose samples are local to each joint. Compose a joint with its parent as `localMatrix * parentGlobalMatrix`; do not treat sampled local transforms as globals.
- Bind-pose and animation-sample scales must be positive and between 0.5 and 2.0 per axis. Apply object scale in Blender before export.
- A complete player mesh set must be 0.5–4.0 m tall on the game Y axis. This catches common centimetre/metre and object-scale mistakes while allowing plausible player rigs.
- The game batter needs the `forearm.R` attachment bone and a `Bat Blade` mesh. Shared crowd, bowler, and fielder assets use the same versioned skeleton format.

## Mesh and material rules

- Legacy JSON version 1 stores triangle positions, finite normals, UV coordinates, triangle indices, four bone-index/weight slots per vertex, and one diffuse RGB colour in the 0–1 range. Bone indices must exist in the asset skeleton; non-negative weights must sum to 1.0 within 0.002. Unknown JSON members are rejected rather than silently interpreted.
- GLB player meshes use triangle primitives, four joint influences per vertex, identity mesh-node transforms, and one skin. Materials must use opaque alpha mode and `TEXCOORD_0`. Embedded PNG/JPEG base-colour images, glTF sampler settings, and `KHR_texture_transform` are supported. Other alpha modes, UV sets, image formats, or unsupported sampler values fail explicitly.

## Animation rules

- An asset contains at least two uniquely named clips. Each clip has a positive duration, at least two strictly increasing sample times within that duration, and one finite transform per bone in every sample.
- Clip events have unique non-empty names and times inside the clip duration. Gameplay reads named release and secured-ball events from the same exported data used for preview.
- Legacy JSON `rootMotion` is the player-local root displacement from the first clip frame. The exporter extracts horizontal travel from sampled poses; the game applies it along the player's facing direction.
- Each GLB animation stores `extras.superCricket` with metadata version, asset/animation names, coordinate system, gameplay events, and a sampled root-motion track (`timeSeconds` plus `positionMeters`). The loader subtracts each root-motion sample from the sampled root-joint translation so motion is not applied twice.
- The Blender scene remains the editable source. The JSON export is deterministic runtime data; do not edit its large sampled arrays by hand.

## Validation and import checks

`PlayerAsset.Load` and `validate-player` reject unsupported versions/coordinate systems, missing or duplicate bones, broken hierarchy, non-finite or non-invertible transforms, invalid mesh arrays/influences, out-of-range colours, implausible player height or rig scale, malformed clip samples, invalid events, and unsupported GLB profile features. `tools/review.ps1` validates both legacy authoring assets and shipped GLBs, checks GLB metadata against its source, and confirms that a 10x legacy bind-pose scale is rejected.

```powershell
dotnet run --project src/SuperCricket.Tools -- validate-player assets/characters/practice-batter.scplayer.json
dotnet run --project src/SuperCricket.Tools -- validate-player assets/characters/practice-bowler.scplayer.json
```

The short-match gate still requires human review of both exported rigs in the actual game and authored animation clips. Changes to texture support, skeleton limits, coordinate conversion, or sampled-pose representation require a format-version decision and a clean export/playback regression.

