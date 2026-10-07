# Step 63 — Humanoid rig and GLB pilot

**Branch:** `codex/godot-trial`

**Date:** 7 October 2026

**Scope:** Start the character-pipeline milestone with an editable, 61-bone cricket batter and a repeatable GLB export, without replacing the shipping MonoGame assets yet.

## Changes

`tools/blender/build_humanoid_batter.py` copies the original practice batter into `practice-batter-humanoid.blend` and creates `practice-batter-humanoid.glb`. It preserves the 13 named bones used by the existing clips, builds a topologically ordered 61-joint hierarchy with hands, five three-segment digit chains per hand, torso/face joints, and toes/heels, reweights the glove and bat hand meshes, and adds a finger-curl preview action. The original Blender sources and MonoGame `.scplayer.json` files remain unchanged.

`tools/blender/validate_humanoid_glb.py` reads the binary GLB directly and verifies the 61-joint skin, required joint names, eight named clips, skinned geometry, and all 30 finger-segment animation channels. The Godot trial now exports this source, imports it, and fails startup unless it finds one 61-bone skeleton, named hand/finger joints, and the existing batting clips. `--capture=grip` produces a deterministic capture of the rig pilot. The current Godot scene still obtains batting contact from the original MonoGame-format player data.

## Verification

- Blender 5.2 background rig build and GLB export — completed without warnings or errors; 61 bones, 89 mesh objects, eight actions; GLB is about 624 KB.
- `python tools/blender/validate_humanoid_glb.py assets/characters/practice-batter-humanoid.glb` — passed with 61 skin joints and 89 skinned primitives.
- Godot trial `Debug` and `Release` builds — passed with zero warnings and errors.
- Godot 4.7.2 GLB import and runtime captures — passed; startup found 61 bones and the expected names/actions; both `contact` and `grip` captures completed on the RTX 4060 Laptop GPU.
- A temporary transitional `.scplayer.json` export validated as 61 bones, 89 meshes, and eight clips; `verify-batting-practice` passed.
- Batting-practice analyzers compared the original and humanoid-export paths over 135 input timings. They produced zero outcome mismatches; maximum numeric differences were below 0.0002 in the CSV's measured units.
- `tools/review.ps1` — full Release review passed, including 20 exact live-match trace replays, gameplay checks, and the existing visual capture suite. The 1440×900 VSync profile measured 36.84 ms average and 46.69 ms p95 frame intervals on this run; CPU update averaged 0.21 ms and draw submission 1.01 ms (GPU time excluded).
- `git diff --check` — passed.

The rig pilot is not the final character art, and MonoGame still uses its existing `.scplayer.json` assets. GLB animation-event/root-motion preservation, bowler/fielder conversion, and the MonoGame GLB runtime importer remain open parts of C1. Human keyboard confirmation of the Step 61/62 camera and feedback changes, plus GamePad testing, also remain open.
