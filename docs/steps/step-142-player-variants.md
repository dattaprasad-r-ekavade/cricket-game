# Step 142 — WP19 player variants

## Scope

Built three MakeHuman-based player physiques on the project's existing 61-bone batter skeleton, with fitted cricket kit, team-color masks, and three runtime LODs per physique.

## Changes

- Added `tools/blender/build_wp19_characters.py`. It loads the official MPFB2 2.0.18 extension from an external extracted directory, generates three macro profiles, bakes their active shape keys, removes hidden MakeHuman helper geometry, and binds the visible bodies to the existing 61-bone rig.
- Added `assets/characters/wp19/a5-character-variants.blend`, `character-variants.json`, and nine GLBs (`agile`, `balanced`, `power` × LOD0/LOD1/LOD2).
- Reused this repository's rigged pads, gloves, helmet, bat, and shoes. Generated jersey and trouser surfaces from the baked body topology and added 128×128 team-color masks and base-color textures with symmetric UV projection.
- Added `tools/blender/validate_wp19_characters.py` to verify each GLB's 61-joint skin, skinned meshes, triangle ordering across LODs, and the mask images.
- Integrated all nine mesh-only GLBs into runtime using the practice batter's matching 61-bone skeleton and WP18 animation clips. The loader checks bone names, parent indices and bind matrices before sharing those clips.
- Added roster `bodyVariant` selections and a runtime appearance renderer that selects agile/balanced/power and LOD0/LOD1/LOD2 from player identity and camera distance.
- Applied the jersey and trouser team-color masks to every loaded LOD and added content tests for all nine variants, mask slots and LOD thresholds.
- Recorded source and licensing in `assets/ASSET-ORIGINS.md`. The MakeHuman base mesh is CC0 1.0; the MPFB2 add-on remains an external GPL-3.0-or-later build dependency and is not vendored.

## Verification

- Blender 5.2.0 build command completed with `WP19_CHARACTER_BUILD_OK`:

  ```powershell
  blender --background --factory-startup --python tools/blender/build_wp19_characters.py -- --mpfb-root "$env:TEMP\mpfb2-v2.0.18" --source-blend assets/characters/practice-batter-humanoid.blend --output-dir assets/characters/wp19
  ```

- `python tools/blender/validate_wp19_characters.py --asset-dir assets/characters/wp19` passed all nine GLBs. Every file has 61 joints and 66 skinned mesh nodes; baked body widths are 0.977 m, 1.004 m, and 1.091 m.
- Exported triangle counts descend at every LOD:

  | Variant | LOD0 | LOD1 | LOD2 |
  | --- | ---: | ---: | ---: |
  | Agile | 41,328 | 11,156 | 5,874 |
  | Balanced | 41,326 | 11,156 | 5,875 |
  | Power | 41,322 | 11,155 | 5,875 |

- Both 128×128 garment masks passed the two-tone/image check. The LOD0 GLBs embed the jersey and trouser base-color textures. The player variants share WP18 animation clips by matching skeleton bone names; animation actions remain in `practice-batter-humanoid.glb`.
- Inspected the preview render at `artifacts/wp19/character-variants.png`.
- `dotnet build SuperCricket.sln -c Release` — passed, zero warnings and errors.
- `dotnet test tests/SuperCricket.Content.Tests/SuperCricket.Content.Tests.csproj -c Release` — passed, 33 tests, including all nine variant/LOD imports and team-mask checks.
- `dotnet src/SuperCricket.Tools/bin/Release/net9.0/SuperCricket.Tools.dll validate-team assets/teams/coastal-xi.json` and the same command for `highland-xi.json` — passed; both 11-player rosters contain valid agile/balanced/power body variants.
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --capture-frame artifacts/wp19-runtime-behind-striker.png --camera behind-striker` — passed; confirmed runtime rendering and non-green team-kit colors.

## Human visual review

The art-track gate still requires a person to review body proportions, kit fit, clipping, team colors and LOD transitions in-game. The runtime capture is `artifacts/wp19-runtime-behind-striker.png`; the generated preview is `artifacts/wp19/character-variants.png`.
