# Step 144 — WP18 dive and stumping poses

## Finding

The 31 October multi-frame render audit found two clips that did not communicate their names: `fielder-dive-left/right` stayed upright throughout their short arcs, and `keeper-stumping` showed an arm motion without a clear crouch or reach. These are automated preview observations; the required in-game human review remains open.

## Changes

- Replaced the small whole-clip offsets with smooth pose-key sequences. The dive clips bank the full rig laterally, extend toward the ground-contact side, and return to the source pickup pose by the end of the 16-frame clip.
- Added a pelvis lowering/forward shift, bent knees, forward torso lean, and low reach to the keeper stumping sequence around its existing frame-12 `bails-broken` event.
- Added `--replace-existing` to the WP18 authoring script so existing action and contract entries can be regenerated in place while ordinary authoring still refuses duplicate names.
- Kept root translation at zero for all three clips; only the pelvis pose uses local translation.
- The first render exposed two local-rig details: root Euler Y twists around the bone instead of banking the body, and pelvis local translation Y/Z maps to world height/depth in the opposite order from world axes. The dive now uses the lateral root Euler axis. Stumping uses corrected local translations, adds to the pickup's knee bend instead of canceling it, and turns the forearms opposite the upper arms so the gloves reach down and forward.
- Added an optional `--ortho-scale` to the clip preview renderer so the full width of a dive can be inspected without changing the existing default framing.
- The wider second render confirmed the dives read clearly but also showed floor penetration on the trailing leg; bone-position checks found the stumping feet below the floor at contact. The dive now tucks the trailing leg and uses a slightly shallower bank; stumping increases the knee fold so both shoes stay at floor height while the pelvis remains low.
- Parity comparison caught the replacement path resetting existing root-motion metadata to zero. Replacements now retain each clip's existing root-motion samples while refreshing its events. A second parity check showed that copying source root-location tracks into every variant also contradicted the zero-root-motion contract on spin variants; location tracks are now authored only for bones explicitly moved by pose keys (the stumping pelvis). The final parity contract matches all 32 clips exactly.

## Verification

- Rebuilt the 61-bone bowler rig, replaced the 22 WP18 bowler clips, and refreshed the runtime `.blend`, GLB, compact contract, and legacy parity export in `assets/characters/`.
- Rendered six frames for each dive direction and stumping with `--ortho-scale 3.4`. The dives bank laterally, reach toward the ground, and return to stance by frame 16; stumping reaches a low crouch with shoes at ground height and returns by frame 16. This is an automated preview inspection, not a human sign-off.
- `python tools/blender/validate_humanoid_glb.py assets/characters/practice-bowler-humanoid.glb --role bowler --animation-contract assets/characters/practice-bowler.animation-contract.json` — passed: 61 humanoid joints, 32 clips, 38 skinned primitives, preserved events and root-motion metadata.
- The rebuilt 32-clip legacy parity contract exactly matches the compact runtime contract. `python -m unittest discover -s tests/tools -p test_player_animation_contract.py` — passed all 4 tests.
- `dotnet test SuperCricket.sln -c Release` — passed all 544 tests (308 Simulation, 203 Game, 33 Content; 0 failed, 0 skipped).
- `pwsh tools/review.ps1 -RunGameChecks -CaptureVisuals` — exit 0; Release solution and Godot builds had 0 warnings/errors, and all automated content, gameplay/live-match, capture, Standard-balance, and five Human-profile gates passed.
- Pending: person-led in-game review of WP18, WP19, and WP20 remains in [the A5 acceptance worksheet](../playtests/2026-10-16-pending-gates.md).
