"""Expand the practice humanoid rigs with the A5 art-track animation set.

Run from the repository root with Blender 4.2+:
    blender --background --python tools/blender/author_wp18_clip_set.py -- \
        --role batter --input-blend assets/characters/practice-batter-humanoid.blend \
        --output-blend artifacts/wp18/practice-batter.blend \
        --output-glb artifacts/wp18/practice-batter.glb \
        --contract assets/characters/practice-batter.animation-contract.json \
        --output-contract artifacts/wp18/practice-batter.animation-contract.json

The clips are authored as edits/variations of this project's own practice poses.
No reference-game animation data is imported.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import re
import sys

import bpy

ROOT = Path(__file__).resolve().parents[2]
FPS = 30


def parse_args() -> argparse.Namespace:
    forwarded = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--role", choices=("batter", "bowler"), required=True)
    parser.add_argument("--input-blend", type=Path, required=True)
    parser.add_argument("--output-blend", type=Path, required=True)
    parser.add_argument("--output-glb", type=Path, required=True)
    parser.add_argument("--contract", type=Path, required=True)
    parser.add_argument("--output-contract", type=Path, required=True)
    return parser.parse_args(forwarded)


def clip_specs(role: str) -> list[dict]:
    if role == "batter":
        direction_twist = {
            "straight": 0.0,
            "offdrive": -0.025,
            "cover": -0.06,
            "square": -0.09,
            "fine": -0.12,
            "legfine": 0.12,
            "legsquare": 0.09,
            "midwicket": 0.06,
        }
        specs = []
        for direction, twist in direction_twist.items():
            for foot in ("front", "back"):
                source = "front-foot-drive" if foot == "front" else "back-foot-drive"
                specs.append({
                    "name": f"batting-{direction}-{foot}-attack",
                    "source": source,
                    "events": {"bat-contact": 31},
                    "offsets": {"spine": (0, 0, twist), "head": (0, 0, twist * -0.12)},
                })

        for name, source, twist in (
            ("batting-defend-straight", "defensive-block", 0.0),
            ("batting-defend-offside", "defensive-block", -0.06),
            ("batting-defend-square", "defensive-block", -0.09),
            ("batting-defend-legside", "defensive-block", 0.08),
            ("batting-loft-front-offside", "lofted-drive", -0.07),
            ("batting-loft-front-legside", "lofted-drive", 0.07),
            ("batting-loft-back-offside", "back-foot-loft", -0.07),
            ("batting-loft-back-legside", "back-foot-loft", 0.07),
        ):
            specs.append({
                "name": name,
                "source": source,
                "events": {"bat-contact": 31},
                "offsets": {"spine": (0, 0, twist)},
            })

        specs.extend([
            {"name": "batting-leave", "source": "batting-step-offside", "events": {"ball-passed": 12},
             "offsets": {"head": (-0.08, 0.0, -0.08), "spine": (0.05, 0.0, -0.03)}},
            {"name": "batting-beaten", "source": "defensive-block", "events": {"ball-passed": 29},
             "offsets": {"head": (0.18, 0.0, 0.08), "spine": (-0.12, 0.0, 0.03)}},
            {"name": "batting-body-hit", "source": "defensive-block", "events": {"body-contact": 25},
             "offsets": {"head": (0.24, 0.0, -0.08), "spine": (-0.18, 0.0, 0.04),
                         "upper_arm.L": (-0.16, 0.0, -0.10), "upper_arm.R": (-0.16, 0.0, 0.10)}},
        ])
        return specs

    return [
        {"name": "bowling-spin-run-up", "source": "bowling-run-up", "events": {},
         "offsets": {"spine": (0.03, 0.0, -0.08), "upper_arm.R": (0.02, 0.0, 0.04)}},
        {"name": "bowling-spin-delivery", "source": "overarm-delivery", "events": {"ball-release": 10},
         "offsets": {"spine": (-0.03, 0.0, 0.10), "upper_arm.R": (0.0, 0.0, -0.05)}},
        {"name": "fielder-run", "source": "between-wickets", "events": {}, "offsets": {}},
        {"name": "fielder-dive-left", "source": "fielder-pickup", "events": {"ground-contact": 12},
         "offsets": {"spine": (0.30, 0.0, -0.30), "upper_arm.L": (-0.18, 0.0, -0.12), "upper_arm.R": (-0.18, 0.0, 0.12)}},
        {"name": "fielder-dive-right", "source": "fielder-pickup", "events": {"ground-contact": 12},
         "offsets": {"spine": (0.30, 0.0, 0.30), "upper_arm.L": (-0.18, 0.0, 0.12), "upper_arm.R": (-0.18, 0.0, -0.12)}},
        {"name": "fielder-catch-low-stationary", "source": "fielder-catch", "events": {"catch-secured": 15},
         "offsets": {"spine": (0.18, 0.0, 0.0), "upper_arm.L": (0.14, 0.0, 0.0), "upper_arm.R": (0.14, 0.0, 0.0)}},
        {"name": "fielder-catch-chest-stationary", "source": "fielder-catch", "events": {"catch-secured": 15},
         "offsets": {}},
        {"name": "fielder-catch-high-stationary", "source": "fielder-catch", "events": {"catch-secured": 15},
         "offsets": {"upper_arm.L": (-0.18, 0.0, -0.05), "upper_arm.R": (-0.18, 0.0, 0.05)}},
        {"name": "fielder-catch-low-running", "source": "fielder-catch", "events": {"catch-secured": 15},
         "offsets": {"spine": (0.22, 0.0, -0.08), "thigh.L": (0.10, 0.0, 0.0), "thigh.R": (-0.10, 0.0, 0.0)}},
        {"name": "fielder-catch-chest-running", "source": "fielder-catch", "events": {"catch-secured": 15},
         "offsets": {"spine": (0.10, 0.0, -0.08), "thigh.L": (0.10, 0.0, 0.0), "thigh.R": (-0.10, 0.0, 0.0)}},
        {"name": "fielder-catch-high-running", "source": "fielder-catch", "events": {"catch-secured": 15},
         "offsets": {"spine": (0.08, 0.0, -0.08), "upper_arm.L": (-0.15, 0.0, -0.04),
                     "upper_arm.R": (-0.15, 0.0, 0.04), "thigh.L": (0.10, 0.0, 0.0), "thigh.R": (-0.10, 0.0, 0.0)}},
        {"name": "fielder-throw-flat", "source": "fielder-throw", "events": {"ball-release": 10},
         "offsets": {"spine": (-0.08, 0.0, -0.04), "upper_arm.R": (-0.08, 0.0, 0.0)}},
        {"name": "fielder-throw-underarm", "source": "fielder-throw", "events": {"ball-release": 10},
         "offsets": {"spine": (0.10, 0.0, 0.0), "upper_arm.R": (0.42, 0.0, 0.0), "forearm.R": (0.10, 0.0, 0.0)}},
        {"name": "fielder-wicket-break", "source": "fielder-pickup", "events": {"bails-broken": 13},
         "offsets": {"spine": (-0.12, 0.0, 0.0), "upper_arm.R": (-0.26, 0.0, 0.08), "forearm.R": (-0.20, 0.0, 0.0)}},
        {"name": "keeper-stand", "source": "practice-stance", "events": {},
         "offsets": {"spine": (0.10, 0.0, 0.0), "thigh.L": (0.16, 0.0, 0.0), "thigh.R": (0.16, 0.0, 0.0)}},
        {"name": "keeper-take", "source": "fielder-catch", "events": {"catch-secured": 13},
         "offsets": {"spine": (0.12, 0.0, 0.0), "upper_arm.L": (0.18, 0.0, -0.04), "upper_arm.R": (0.18, 0.0, 0.04)}},
        {"name": "keeper-stumping", "source": "fielder-pickup", "events": {"bails-broken": 12},
         "offsets": {"spine": (-0.10, 0.0, 0.0), "thigh.L": (0.08, 0.0, 0.0), "thigh.R": (0.08, 0.0, 0.0),
                     "upper_arm.L": (0.22, 0.0, -0.08), "upper_arm.R": (0.22, 0.0, 0.08)}},
        {"name": "umpire-out", "source": "practice-stance", "events": {"signal": 20},
         "offsets": {"upper_arm.R": (-2.3, 0.0, 0.0), "forearm.R": (-0.20, 0.0, 0.0)}},
        {"name": "umpire-four", "source": "practice-stance", "events": {"signal": 20},
         "offsets": {"upper_arm.R": (0.15, 0.0, -1.55), "forearm.R": (-0.10, 0.0, 0.0)}},
        {"name": "umpire-six", "source": "practice-stance", "events": {"signal": 20},
         "offsets": {"upper_arm.L": (-1.65, 0.0, -0.08), "upper_arm.R": (-1.65, 0.0, 0.08)}},
        {"name": "umpire-wide", "source": "practice-stance", "events": {"signal": 20},
         "offsets": {"upper_arm.L": (0.05, 0.0, 1.10), "upper_arm.R": (0.05, 0.0, -1.10)}},
        {"name": "umpire-no-ball", "source": "practice-stance", "events": {"signal": 20},
         "offsets": {"upper_arm.R": (-0.30, 0.0, -1.00)}},
    ]


def _curve_bones(action: bpy.types.Action) -> set[str]:
    names: set[str] = set()
    pattern = re.compile(r'pose\.bones\["([^"]+)"\]\.rotation_euler')
    for slot in action.slots:
        for layer in action.layers:
            for strip in layer.strips:
                bag = strip.channelbag(slot)
                if bag is None:
                    continue
                for curve in bag.fcurves:
                    match = pattern.fullmatch(curve.data_path)
                    if match:
                        names.add(match.group(1))
    return names


def _assign_action(armature: bpy.types.Object, action: bpy.types.Action) -> None:
    animation = armature.animation_data_create()
    animation.action = action
    if hasattr(animation, "action_slot") and len(action.slots) > 0:
        animation.action_slot = action.slots[0]


def _bake_variant(armature: bpy.types.Object, spec: dict) -> bpy.types.Action:
    base = bpy.data.actions.get(spec["source"])
    if base is None:
        raise RuntimeError(f"Missing source animation '{spec['source']}' for '{spec['name']}'.")
    if bpy.data.actions.get(spec["name"]) is not None:
        raise RuntimeError(f"Animation '{spec['name']}' already exists; refusing to overwrite an authored clip.")

    animation = armature.animation_data_create()
    start = int(round(base.frame_range[0]))
    end = int(round(base.frame_range[1]))
    frames = list(range(start, end + 1))
    bones = _curve_bones(base) | set(spec.get("offsets", {}))
    bones &= set(armature.pose.bones.keys())
    if not bones:
        raise RuntimeError(f"Source animation '{base.name}' has no usable pose channels.")

    sampled: dict[int, dict[str, tuple[float, float, float]]] = {}
    for frame in frames:
        for pose_bone in armature.pose.bones:
            pose_bone.rotation_mode = "XYZ"
            pose_bone.rotation_euler = (0.0, 0.0, 0.0)
        _assign_action(armature, base)
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        sampled[frame] = {
            name: tuple(float(v) for v in armature.pose.bones[name].rotation_euler)
            for name in bones
        }

    action = bpy.data.actions.new(spec["name"])
    action.use_fake_user = True
    _assign_action(armature, action)
    for frame in frames:
        normalized = (frame - start) / max(1, end - start)
        envelope = 0.55 + 0.45 * __import__("math").sin(__import__("math").pi * normalized)
        for name in bones:
            values = list(sampled[frame][name])
            offset = spec.get("offsets", {}).get(name, (0.0, 0.0, 0.0))
            for axis in range(3):
                values[axis] += float(offset[axis]) * envelope
            pose_bone = armature.pose.bones[name]
            pose_bone.rotation_euler = values
            pose_bone.keyframe_insert(data_path="rotation_euler", frame=frame, group=name)

    # The keyed neutral baseline is the same as the source stance. Keep every
    # new action independently inspectable and preserve its gameplay markers.
    action["sc_events"] = spec.get("events", {})
    return action


def _make_contract(source_contract: dict, specs: list[dict]) -> dict:
    contract = dict(source_contract)
    existing = {clip["name"] for clip in contract["animations"]}
    for spec in specs:
        if spec["name"] in existing:
            raise ValueError(f"Animation contract already contains '{spec['name']}'.")
        start = 1
        events = [
            {"name": name, "timeSeconds": (float(frame) - start) / FPS}
            for name, frame in spec.get("events", {}).items()
        ]
        events.sort(key=lambda item: item["timeSeconds"])
        contract["animations"].append({
            "name": spec["name"],
            "events": events,
            "rootMotion": {"mode": "zero", "samples": []},
        })
    return contract


def main() -> None:
    args = parse_args()
    if not args.input_blend.is_file() or not args.contract.is_file():
        raise FileNotFoundError("The input Blender file and animation contract must exist.")
    bpy.ops.wm.open_mainfile(filepath=str(args.input_blend.resolve()))
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(f"Expected one player armature, found {len(armatures)}.")
    armature = armatures[0]
    specs = clip_specs(args.role)
    contract = _make_contract(json.loads(args.contract.read_text(encoding="utf-8")), specs)

    for spec in specs:
        _bake_variant(armature, spec)

    # Keep the source stance selected when opening the editable Blender file.
    _assign_action(armature, bpy.data.actions["practice-stance"])
    bpy.context.scene.frame_set(1)
    bpy.context.scene.render.fps = FPS
    args.output_blend.parent.mkdir(parents=True, exist_ok=True)
    args.output_glb.parent.mkdir(parents=True, exist_ok=True)
    args.output_contract.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(args.output_blend.resolve()))

    result = bpy.ops.export_scene.gltf(
        filepath=str(args.output_glb.resolve()),
        export_format="GLB",
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_nla_strips=True,
        export_yup=True,
        export_apply=True,
        export_force_sampling=True,
        export_leaf_bone=False,
        export_cameras=False,
        export_lights=False,
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender did not finish the GLB export: {result}")

    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from embed_player_metadata import embed_player_metadata
    from player_animation_contract import validate_animation_contract

    validated = validate_animation_contract(contract)
    args.output_contract.write_text(json.dumps(validated, ensure_ascii=False, allow_nan=False, indent=2) + "\n", encoding="utf-8")
    metadata_count = embed_player_metadata(args.output_glb, args.output_contract, args.output_glb)
    print(f"Authored {len(specs)} new {args.role} clips; contract now has {len(contract['animations'])} clips.")
    print(f"Saved Blender rig: {args.output_blend.resolve()}")
    print(f"Exported GLB: {args.output_glb.resolve()} ({args.output_glb.stat().st_size} bytes; {metadata_count} clips)")
    print("New clips: " + ", ".join(spec["name"] for spec in specs))


if __name__ == "__main__":
    main()
