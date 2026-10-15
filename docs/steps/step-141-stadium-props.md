# Step 141 — WP20 stadium prop kit

## Scope

Created the plan's first original modular stadium-dressing set in Blender, then integrated the reusable GLBs into the match renderer through a validated placement manifest.

## Changes

- Added 11 prop groups: separate breakable stumps and bails, bat, helmet and guard, modular sight screen, two fictional boundary boards, stand seating bay, electronic scoreboard, pitch roller, boundary umbrella and crowd impostor card set.
- Added the editable Blender gallery/source and deterministic build, preview and validation scripts.
- Added `assets/stadium/stadium-layout.json` with 15 field placements using all 11 props.
- Added graphics-free GLB import and manifest validation in `SuperCricket.Content`, plus a GPU renderer in `SuperCricket.Game`. Meshes are cached by asset, use authored material colors and normals, and are copied with the game output.
- Added `validate-stadium-layout` to Tools and routed it through `tools/review.ps1`.
- Added content tests covering all 11 GLBs, mesh data, and rejection of paths escaping the prop directory.
- Recorded generator, date, source and license details in `assets/ASSET-ORIGINS.md`.

## Verification

- `blender --background --python tools/blender/build_wp20_stadium_props.py -- --blend-output assets/stadium/a5-stadium-props.blend --gallery-output assets/stadium/a5-props-gallery.glb --asset-dir assets/stadium/props --preview artifacts/wp20/stadium-props.png` — passed; rendered the gallery and exported 11 individual prop GLBs plus the gallery GLB.
- `python tools/blender/validate_wp20_props.py --asset-dir assets/stadium/props --gallery assets/stadium/a5-props-gallery.glb` — passed; all 11 props have non-empty GLB meshes/nodes and required semantic parts, including the separately named bails; gallery has 136 meshes and 147 nodes.
- `dotnet src/SuperCricket.Tools/bin/Release/net9.0/SuperCricket.Tools.dll validate-stadium-layout assets/stadium/stadium-layout.json` — passed; 15 placements and all 11 static props loaded (8–48 meshes per prop).
- `dotnet test tests/SuperCricket.Content.Tests/SuperCricket.Content.Tests.csproj -c Release` — passed, 33 tests.
- `dotnet build SuperCricket.sln -c Release` — passed, zero warnings and errors.
- `dotnet run --project src/SuperCricket.Game -c Release --no-build -- --capture-frame artifacts/review-field-overview.png --camera field-overview` — passed; rendered the match scene with the manifest-driven props.
- `pwsh tools/review.ps1 -RunGameChecks -CaptureVisuals` — passed with exit code 0 after WP19/WP20 runtime integration; Release and Godot trial builds, gameplay/live-match checks, renderer captures and the Standard 10/20-over balance gates passed.
- Inspected the 1600×1000 gallery render at `artifacts/wp20/stadium-props.png`.

## Human visual review

The art-track plan requires a person to review each package in-game. Runtime placement is visible in `artifacts/review-field-overview.png`; inspect scale, placement, readability and clipping in the game before closing this gate.
