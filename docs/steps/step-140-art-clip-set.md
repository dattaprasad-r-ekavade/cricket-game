# Step 140 — WP18 player clip set

## Scope

Expanded the practice batter and bowler rigs with the WP18 animation families, keeping every clip authored from this project's own Blender actions. The 61-bone GLBs carry the event metadata from the companion contracts.

## Changes

- Added 27 batter clips: 16 directional front/back attacks, four defensive directions, four loft variants, leave, beaten and body-hit reactions.
- Added 22 bowler/fielding clips: spin run-up and delivery, run, left/right dive, six catch-grid clips, two throw styles, wicket break, three keeper clips and five umpire signals.
- Updated the 40-row stroke table to select the directional attack, defensive and loft clips.
- The shared `strokes.json` also carries WP21's balance tuning; Step 133 records its final Ideal-speed medians and seeded match results.
- Added reproducible Blender authoring and preview scripts: `tools/blender/author_wp18_clip_set.py` and `tools/blender/render_wp18_clip_previews.py`.

## Verification

- `blender --background --python tools/blender/author_wp18_clip_set.py ...` — authored 27 batter clips and 22 bowler clips on the editable 13-bone source rigs.
- `blender --background --python tools/blender/build_humanoid_batter.py ...` — rebuilt the 61-bone runtime rigs and GLBs through the existing retarget/export path.
- Refreshed both legacy `.scplayer.json` parity exports from the authored scenes with `tools/blender/build_practice_batter.py --from-scene`; for the bowler, `--only-included-clips` exports the requested non-batter action set without rebuilding batter defaults. The compact contracts now match these full sampled parity sources.
- `python tools/blender/validate_humanoid_glb.py assets/characters/practice-batter-humanoid.glb --role batter --animation-contract assets/characters/practice-batter.animation-contract.json` — passed: 61 joints, 37 clips, 89 skinned primitives, finger-grip contract intact, 36 gameplay clips with metadata.
- `python tools/blender/validate_humanoid_glb.py assets/characters/practice-bowler-humanoid.glb --role bowler --animation-contract assets/characters/practice-bowler.animation-contract.json` — passed: 61 joints, 32 clips, 38 skinned primitives, 32 gameplay clips with metadata.
- `dotnet run --project src/SuperCricket.Tools -c Release --no-build -- validate-strokes assets/batting/strokes.json` — passed: 40 strokes.
- `dotnet run --project src/SuperCricket.Tools -c Release --no-build -- analyze-batting-practice assets/characters/practice-batter-humanoid.glb assets/characters/practice-bowler-humanoid.glb artifacts/wp18/check-shots.json assets/deliveries/standard-pace.json artifacts/wp18/final-batting-practice.csv 0.025` — passed; all 24 contact-marked attack/defend/loft clips produced analyzer contact. Contact windows ranged from 20/42 to 30/42 input timings per clip.
- Rendered selected attack, loft, body-hit, dive, keeper and umpire poses into `artifacts/wp18/previews/` for visual review.
- `python -m unittest discover -s tests/tools -p test_player_animation_contract.py` — passed all four tests after regenerating the parity exports and rebuilding the humanoid GLBs. The tests cover contract parity, metadata round-trip, and validation for both players.
- Updated the gameplay review assertion for the default directional `S` attack to require the new `batting-straight-front-attack` clip and explicitly set the Directional scheme. Classic Chords checks continue to cover the legacy `front-foot-drive` alias. This keeps the review independent of saved user settings while checking the WP18 stroke-table mapping.
- `pwsh tools/review.ps1 -RunGameChecks -CaptureVisuals` — passed on 16 October 2026 with exit code 0, including gameplay checks and the capture matrix. This verifies integration; human review of the clip previews and movements in-game remains open.

## Human visual review

WP18 requires a person to inspect the rendered clip previews and confirm the movements read clearly in-game. The renders exist at `artifacts/wp18/previews/`; the automated structural and contact checks above do not replace that review.
