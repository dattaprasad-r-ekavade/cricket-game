# Asset origins

## WP20 stadium prop kit

- **Created:** 2026-10-15
- **Generator:** `tools/blender/build_wp20_stadium_props.py`, run with Blender 5.2.0 LTS.
- **Input:** Original procedural geometry, materials and fictional Coastal/Highland signage authored in this repository. No third-party models, textures, reference-game assets or AI-generated objects were used.
- **License:** Original Super Cricket project content, distributed with this repository.
- **Prompt:** None; these are deterministic Blender primitives and mesh assemblies, not AI-generated assets.
- **Editable source:** `assets/stadium/a5-stadium-props.blend`.
- **Gallery:** `assets/stadium/a5-props-gallery.glb`.
- **Modular exports:** `assets/stadium/props/*.glb` (11 files).
- **Parts:** removable stumps and separate breakable bails; bat; helmet with face guard; modular sight screen; two fictional boundary boards; modular seating bay; scoreboard; pitch roller; umbrella; crowd impostor cards.

## WP19 player variants

- **Created:** 2026-10-16
- **Generator:** `tools/blender/build_wp19_characters.py`, Blender 5.2.0 LTS with MPFB2 2.0.18 loaded from the official Blender extension package.
- **Base bodies:** MPFB2's bundled MakeHuman base mesh. MakeHuman's official license page identifies its core assets and exported assets as CC0 1.0: [MakeHuman license](https://static.makehumancommunity.org/about/license.html). MPFB2's Blender add-on code has its own GPL-3.0-or-later license; it is used as a build dependency and is not copied into this repository. See the [MPFB2 2.0.18 release](https://static.makehumancommunity.org/mpfb/releases/release_2018.html).
- **Other inputs:** The repository's existing 61-bone practice batter rig, clips, and low-poly cricket equipment. Those repository assets retain their project license and are not MakeHuman content.
- **License:** Base body geometry is CC0 1.0; original Super Cricket kit assembly, masks, and rigged variants are distributed with this repository.
- **Prompt:** None; profiles, fitting, UVs, and team-mask textures are deterministic local Blender operations. No generative AI or third-party models/textures were used.
- **Editable source:** `assets/characters/wp19/a5-character-variants.blend`.
- **Exports:** Three 61-joint variants (agile, balanced, power), each with LOD0/LOD1/LOD2 GLBs under `assets/characters/wp19/`.
- **Masks:** Team-color mask and base-color PNG pairs for jersey and trousers under `assets/characters/wp19/textures/`; each mask is 128×128 and uses the garment's generated UVs.
- **Preview:** `artifacts/wp19/character-variants.png`.

