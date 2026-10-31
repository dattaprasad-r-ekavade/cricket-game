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
    parser.add_argument(
        "--replace-existing",
        action="store_true",
        help="replace generated clip actions and contract entries with matching names",
    )
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
         "offsets": {}, "pose_keys": _dive_pose_keys(-1.0)},
        {"name": "fielder-dive-right", "source": "fielder-pickup", "events": {"ground-contact": 12},
         "offsets": {}, "pose_keys": _dive_pose_keys(1.0)},
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
         "offsets": {}, "pose_keys": _keeper_stumping_pose_keys()},
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


def _dive_pose_keys(direction: float) -> list[dict]:
    leading_side = "R" if direction > 0 else "L"
    contact_rotations = {
        "root": (0.0, 0.0, direction * 1.20),
        "spine": (0.28, 0.0, 0.0),
        "head": (-0.14, 0.0, 0.0),
    }
    tucked_side = "R" if direction < 0 else "L"
    contact_rotations[f"thigh.{tucked_side}"] = (1.0, 0.0, 0.0)
    contact_rotations[f"shin.{tucked_side}"] = (-1.8, 0.0, 0.0)
    for side in ("L", "R"):
        leading = side == leading_side
        side_sign = -1.0 if side == "L" else 1.0
        contact_rotations[f"upper_arm.{side}"] = (-0.42, 0.0, side_sign * 0.12)
        contact_rotations[f"forearm.{side}"] = (1.75 if leading else 1.62, 0.0, 0.0)

    return [
        {"frame": 1},
        {"frame": 4, "rotation": {"root": (0.0, 0.0, direction * 0.20)}},
        {"frame": 8, "rotation": {
            "root": (0.0, 0.0, direction * 0.82),
            "spine": (0.14, 0.0, 0.0),
            "head": (-0.07, 0.0, 0.0),
        }},
        {"frame": 12, "rotation": contact_rotations},
        {"frame": 14, "rotation": {
            "root": (0.0, 0.0, direction * 0.86),
            "spine": (0.18, 0.0, 0.0),
            "head": (-0.08, 0.0, 0.0),
        }},
        {"frame": 16},
    ]


def _keeper_stumping_pose_keys() -> list[dict]:
    crouch_rotations = {
        "spine": (0.48, 0.0, 0.0),
        "head": (-0.16, 0.0, 0.0),
        "thigh.L": (0.71, 0.0, 0.0),
        "thigh.R": (0.71, 0.0, 0.0),
        "shin.L": (-1.46, 0.0, 0.0),
        "shin.R": (-1.46, 0.0, 0.0),
        "upper_arm.L": (-0.42, 0.0, -0.08),
        "upper_arm.R": (-0.42, 0.0, 0.08),
        "forearm.L": (1.65, 0.0, 0.0),
        "forearm.R": (1.65, 0.0, 0.0),
    }
    return [
        {"frame": 1},
        {"frame": 4, "location": {"pelvis": (0.0, -0.10, 0.03)}, "rotation": {
            "spine": (0.14, 0.0, 0.0),
            "thigh.L": (0.12, 0.0, 0.0),
            "thigh.R": (0.12, 0.0, 0.0),
        }},
        {"frame": 8, "location": {"pelvis": (0.0, -0.28, 0.10)}, "rotation": {
            "spine": (0.32, 0.0, 0.0),
            "head": (-0.10, 0.0, 0.0),
            "thigh.L": (0.41, 0.0, 0.0),
            "thigh.R": (0.41, 0.0, 0.0),
            "shin.L": (-0.74, 0.0, 0.0),
            "shin.R": (-0.74, 0.0, 0.0),
            "upper_arm.L": (-0.28, 0.0, -0.04),
            "upper_arm.R": (-0.28, 0.0, 0.04),
            "forearm.L": (1.15, 0.0, 0.0),
            "forearm.R": (1.15, 0.0, 0.0),
        }},
        {"frame": 12, "location": {"pelvis": (0.0, -0.50, 0.16)}, "rotation": crouch_rotations},
        {"frame": 14, "location": {"pelvis": (0.0, -0.32, 0.10)}, "rotation": {
            "spine": (0.30, 0.0, 0.0),
            "head": (-0.10, 0.0, 0.0),
            "thigh.L": (0.64, 0.0, 0.0),
            "thigh.R": (0.64, 0.0, 0.0),
            "shin.L": (-1.35, 0.0, 0.0),
            "shin.R": (-1.35, 0.0, 0.0),
            "upper_arm.L": (-0.22, 0.0, -0.03),
            "upper_arm.R": (-0.22, 0.0, 0.03),
            "forearm.L": (0.9, 0.0, 0.0),
            "forearm.R": (0.9, 0.0, 0.0),
        }},
        {"frame": 16},
    ]


def _curve_bones(action: bpy.types.Action, channel: str = "rotation_euler") -> set[str]:
    names: set[str] = set()
    pattern = re.compile(rf'pose\.bones\["([^"]+)"\]\.{re.escape(channel)}')
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


def _pose_offsets_at(spec: dict, frame: int) -> dict[str, dict[str, tuple[float, float, float]]]:
    anchors = sorted(spec.get("pose_keys", []), key=lambda item: int(item["frame"]))
    if not anchors:
        return {"rotation": {}, "location": {}}

    before = anchors[0]
    after = anchors[-1]
    for anchor in anchors:
        if int(anchor["frame"]) <= frame:
            before = anchor
        if int(anchor["frame"]) >= frame:
            after = anchor
            break

    start = int(before["frame"])
    end = int(after["frame"])
    amount = 0.0 if end == start else (frame - start) / (end - start)
    amount = amount * amount * (3.0 - 2.0 * amount)
    result = {"rotation": {}, "location": {}}
    for channel in result:
        names = set(before.get(channel, {})) | set(after.get(channel, {}))
        for name in names:
            left = before.get(channel, {}).get(name, (0.0, 0.0, 0.0))
            right = after.get(channel, {}).get(name, (0.0, 0.0, 0.0))
            result[channel][name] = tuple(
                float(a) + (float(b) - float(a)) * amount for a, b in zip(left, right)
            )
    return result


def _set_linear_interpolation(action: bpy.types.Action) -> None:
    for slot in action.slots:
        for layer in action.layers:
            for strip in layer.strips:
                bag = strip.channelbag(slot)
                if bag is None:
                    continue
                for curve in bag.fcurves:
                    for key in curve.keyframe_points:
                        key.interpolation = "LINEAR"


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
    rotation_bones = _curve_bones(base) | set(spec.get("offsets", {}))
    location_bones: set[str] = set()
    for pose_key in spec.get("pose_keys", []):
        rotation_bones.update(pose_key.get("rotation", {}))
        location_bones.update(pose_key.get("location", {}))
    rig_bones = set(armature.pose.bones.keys())
    rotation_bones &= rig_bones
    location_bones &= rig_bones
    if not rotation_bones:
        raise RuntimeError(f"Source animation '{base.name}' has no usable pose channels.")

    sampled_rotation: dict[int, dict[str, tuple[float, float, float]]] = {}
    sampled_location: dict[int, dict[str, tuple[float, float, float]]] = {}
    for frame in frames:
        for pose_bone in armature.pose.bones:
            pose_bone.rotation_mode = "XYZ"
            pose_bone.rotation_euler = (0.0, 0.0, 0.0)
            pose_bone.location = (0.0, 0.0, 0.0)
        _assign_action(armature, base)
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        sampled_rotation[frame] = {
            name: tuple(float(v) for v in armature.pose.bones[name].rotation_euler)
            for name in rotation_bones
        }
        sampled_location[frame] = {
            name: tuple(float(v) for v in armature.pose.bones[name].location)
            for name in location_bones
        }

    action = bpy.data.actions.new(spec["name"])
    action.use_fake_user = True
    _assign_action(armature, action)
    for frame in frames:
        normalized = (frame - start) / max(1, end - start)
        envelope = 0.55 + 0.45 * __import__("math").sin(__import__("math").pi * normalized)
        pose_offsets = _pose_offsets_at(spec, frame)
        for name in rotation_bones:
            values = list(sampled_rotation[frame][name])
            offset = spec.get("offsets", {}).get(name, (0.0, 0.0, 0.0))
            for axis in range(3):
                values[axis] += float(offset[axis]) * envelope
                values[axis] += pose_offsets["rotation"].get(name, (0.0, 0.0, 0.0))[axis]
            pose_bone = armature.pose.bones[name]
            pose_bone.rotation_euler = values
            pose_bone.keyframe_insert(data_path="rotation_euler", frame=frame, group=name)
        for name in location_bones:
            values = list(sampled_location[frame][name])
            offset = pose_offsets["location"].get(name, (0.0, 0.0, 0.0))
            pose_bone = armature.pose.bones[name]
            pose_bone.location = tuple(float(value) + float(offset[axis]) for axis, value in enumerate(values))
            pose_bone.keyframe_insert(data_path="location", frame=frame, group=name)

    # The keyed neutral baseline is the same as the source stance. Keep every
    # new action independently inspectable and preserve its gameplay markers.
    action["sc_events"] = spec.get("events", {})
    _set_linear_interpolation(action)
    return action


def _make_contract(source_contract: dict, specs: list[dict], replace_existing: bool = False) -> dict:
    contract = dict(source_contract)
    replacements = {}
    for spec in specs:
        existing = next((clip for clip in contract["animations"] if clip["name"] == spec["name"]), None)
        if existing is not None and not replace_existing:
            raise ValueError(f"Animation contract already contains '{spec['name']}'.")
        start = 1
        events = [
            {"name": name, "timeSeconds": (float(frame) - start) / FPS}
            for name, frame in spec.get("events", {}).items()
        ]
        events.sort(key=lambda item: item["timeSeconds"])
        replacement = dict(existing) if existing is not None and replace_existing else {}
        replacement.update({"name": spec["name"], "events": events})
        replacement.setdefault("rootMotion", {"mode": "zero", "samples": []})
        replacements[spec["name"]] = replacement
    contract["animations"] = [
        replacements.pop(clip["name"], clip)
        for clip in contract["animations"]
    ]
    contract["animations"].extend(replacements.values())
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
    contract = _make_contract(
        json.loads(args.contract.read_text(encoding="utf-8")), specs, replace_existing=args.replace_existing
    )

    for spec in specs:
        if args.replace_existing:
            previous = bpy.data.actions.get(spec["name"])
            if previous is not None:
                bpy.data.actions.remove(previous, do_unlink=True)
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
