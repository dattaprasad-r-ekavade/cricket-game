# Blender player asset contract

`PlayerAsset.Load` accepts glTF 2.0 `.glb` files only. The MonoGame runtime and batting review workflow use the 61-joint batter and bowler GLBs through `PlayerGlbLoader` in `SuperCricket.Content`, using SharpGLTF.Core. Blender scenes author mesh and animation data. Compact `*.animation-contract.json` files carry gameplay events and sampled root motion for the GLB exporter; the Blender scene exporter writes them directly alongside a legacy parity artifact. The old `.scplayer.json` files are test-only migration fixtures and are not accepted by the runtime, C# player tools, or current analyzer inputs; the one-off Python bootstrap utility may still read an archived file.

## GLB rig pilot

Step 63 adds `assets/characters/practice-batter-humanoid.blend` and `practice-batter-humanoid.glb` as an editable 61-joint humanoid pilot. Its hierarchy retains the named batting bones used by the v1 clips and adds pelvis/spine/chest/neck segments, clavicles and scapulae, jaw/eyes, hands, three articulated segments for each of five digits, and toe/heel joints. Glove details are weighted across the finger chains; the bat and glove are attached at the hands. The added `finger-grip-preview` action exercises those joints. Step 68 adds the matching `practice-bowler-humanoid.blend` and `.glb` with the bowler's 10 gameplay clips; it keeps the shared 61-joint skeleton but does not add a batter-only grip-preview action.

`tools/blender/validate_humanoid_glb.py` checks both GLBs' headers, 61-joint skins, required names, skinned primitives, and role-specific clips; it also verifies that each clip's `extras.superCricket` metadata preserves the animation contract's events and sampled root motion. The batter validation checks all grip-preview channels. `PlayerGlbLoader` reads those animation extras, imports triangle primitives, base-colour factors and embedded PNG/JPEG textures, sampler settings, UV transforms, skin weights, and inverse-bind matrices; animation samples are local joint poses at 30 Hz. `PlayerAnimator` rebuilds the joint hierarchy before producing the skin palette. The importer subtracts the embedded root-motion sample from the animated root pose, then the runtime applies that motion separately along the player's facing direction. The current GLB profile requires one identity-transformed skin, identity mesh nodes, opaque materials, `TEXCOORD_0`, triangle primitives, and supported PNG/JPEG base-colour images. The Blender exporter syncs source diffuse colours into unlinked default Principled shaders so untextured GLBs retain their intended kit/gear colours. Both the Godot presentation and shared batting simulation now load the same GLB files. Fielder-specific content remains open; `.scplayer.json` runtime loading is retired.

## Coordinate and skeleton rules

- `coordinateSystem` is `right-handed-y-up-metres`; all geometry, translations, event times, and root motion use metres and seconds.
- Blender coordinates convert to game coordinates as `(x, z, -y)`. Exported transforms also convert rotation and scale into that basis.
- Archived legacy JSON v1 parity fixtures have one named skeleton root. Bone names are unique and each non-root parent appears earlier in the bone list; this format is no longer loaded by the game or tools. The humanoid GLB pilot has 61 joints.
- Humanoid GLB pose samples are local to each joint. Compose a joint with its parent as `localMatrix * parentGlobalMatrix`; do not treat sampled local transforms as globals.
- Bind-pose and animation-sample scales must be positive and between 0.5 and 2.0 per axis. Apply object scale in Blender before export.
- A complete player mesh set must be 0.5–4.0 m tall on the game Y axis. This catches common centimetre/metre and object-scale mistakes while allowing plausible player rigs.
- The game batter needs the `forearm.R` attachment bone and a `Bat Blade` mesh. Shared crowd, bowler, and fielder assets use the same versioned skeleton format.

## Mesh and material rules

- Archived legacy JSON v1 parity fixtures stored triangle positions, finite normals, UV coordinates, triangle indices, four bone-index/weight slots per vertex, and one diffuse RGB colour in the 0–1 range. They are not accepted as runtime player assets.
- GLB player meshes use triangle primitives, four joint influences per vertex, identity mesh-node transforms, and one skin. Materials must use opaque alpha mode and `TEXCOORD_0`. Embedded PNG/JPEG base-colour images, glTF sampler settings, and `KHR_texture_transform` are supported. Other alpha modes, UV sets, image formats, or unsupported sampler values fail explicitly.

## Animation rules

- An asset contains at least two uniquely named clips. Each clip has a positive duration, at least two strictly increasing sample times within that duration, and one finite transform per bone in every sample.
- Clip events have unique non-empty names and times inside the clip duration. Gameplay reads named release and secured-ball events from the same exported data used for preview.
- Archived legacy JSON `rootMotion` samples recorded player-local displacement from the first clip frame. Current GLB clips carry the corresponding values in the compact animation contract and embedded metadata.
- Each GLB animation stores `extras.superCricket` with metadata version, asset/animation names, coordinate system, gameplay events, and a sampled root-motion track (`timeSeconds` plus `positionMeters`). The loader subtracts each root-motion sample from the sampled root-joint translation so motion is not applied twice.
- The Blender scene remains the editable source for meshes and actions. `build_practice_batter.py` derives the compact `*.animation-contract.json` from the in-memory scene export and writes it alongside a full `.scplayer.json` parity artifact. Use `tools/blender/extract_player_animation_contract.py` only to bootstrap a contract for an archived legacy asset. Do not edit sampled root-motion arrays by hand.

## Validation and import checks

`PlayerAsset.Load` and `validate-player` accept GLB only and reject missing or duplicate bones, broken hierarchy, non-finite or non-invertible transforms, invalid mesh arrays/influences, out-of-range colours, implausible player height or rig scale, malformed clip samples, invalid events, and unsupported GLB profile features. `tools/review.ps1` validates both shipped GLBs and their animation-contract metadata. The simulation parity test reads archived legacy fixtures only inside the test assembly and compares their batting results with the migrated GLBs.

```powershell
dotnet run --project src/SuperCricket.Tools -- validate-player assets/characters/practice-batter-humanoid.glb
dotnet run --project src/SuperCricket.Tools -- validate-player assets/characters/practice-bowler-humanoid.glb
```

The short-match gate still requires human review of both exported rigs in the actual game and authored animation clips. Changes to texture support, skeleton limits, coordinate conversion, or sampled-pose representation require a format-version decision and a clean export/playback regression.

