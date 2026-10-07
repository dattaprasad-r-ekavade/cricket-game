"""Validate the structural contract of a practice batter or bowler humanoid GLB."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import struct
import sys

from player_animation_contract import load_animation_contract


EXPECTED_BONE_COUNT = 61
REQUIRED_CORE_BONES = {
    "root", "pelvis", "spine.01", "spine.02", "spine", "chest", "neck", "head",
    "jaw", "eye.L", "eye.R", "scapula.L", "scapula.R", "clavicle.L", "clavicle.R",
    "upper_arm.L", "upper_arm.R", "forearm.L", "forearm.R", "hand.L", "hand.R",
    "thigh.L", "thigh.R", "shin.L", "shin.R", "foot.L", "foot.R", "toe.L", "toe.R",
    "heel.L", "heel.R",
}
REQUIRED_FINGER_BONES = {
    f"{digit}.{segment:02d}.{side}"
    for digit in ("thumb", "index", "middle", "ring", "pinky")
    for segment in (1, 2, 3)
    for side in ("L", "R")
}
REQUIRED_ANIMATIONS = {
    "batting-step-legside", "batting-step-offside", "between-wickets", "defensive-block",
    "finger-grip-preview", "front-foot-drive", "lofted-drive", "practice-stance",
    "back-foot-drive", "back-foot-loft",
}


def load_glb_json(path: Path) -> dict:
    data = path.read_bytes()
    if len(data) < 20:
        raise ValueError("GLB is too short to contain a header and JSON chunk.")
    magic, version, declared_length = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF" or version != 2 or declared_length != len(data):
        raise ValueError("File is not a complete glTF 2.0 binary (GLB).")

    offset = 12
    while offset + 8 <= len(data):
        chunk_length, chunk_type = struct.unpack_from("<II", data, offset)
        offset += 8
        chunk = data[offset : offset + chunk_length]
        if len(chunk) != chunk_length:
            raise ValueError("GLB chunk extends beyond the declared file length.")
        offset += chunk_length
        if chunk_type == 0x4E4F534A:
            return json.loads(chunk.decode("utf-8").rstrip("\x00 \t\r\n"))
    raise ValueError("GLB has no JSON chunk.")


def _close(left: float, right: float, tolerance: float = 1e-6) -> bool:
    return abs(float(left) - float(right)) <= tolerance


def _animation_duration(document: dict, animation: dict) -> float:
    accessors = document.get("accessors", [])
    input_times = []
    for sampler in animation.get("samplers", []):
        accessor_index = sampler.get("input", -1)
        if 0 <= accessor_index < len(accessors):
            accessor_max = accessors[accessor_index].get("max", [])
            if accessor_max:
                input_times.append(float(accessor_max[0]))
    if not input_times:
        raise ValueError(f"Animation '{animation.get('name', '')}' has no readable input-time range.")
    return max(input_times)


def _validate_player_metadata(document: dict, animation_contract_path: Path) -> None:
    animation_contract = load_animation_contract(animation_contract_path)
    source_clips = {clip["name"]: clip for clip in animation_contract["animations"]}
    exported = {animation.get("name", ""): animation for animation in document.get("animations", [])}
    missing = sorted(set(source_clips) - set(exported))
    if missing:
        raise ValueError("The GLB is missing source gameplay clips: " + ", ".join(missing))

    for name, animation in exported.items():
        metadata = animation.get("extras", {}).get("superCricket")
        if not isinstance(metadata, dict):
            raise ValueError(f"GLB animation '{name}' has no Super Cricket metadata in extras.")
        if metadata.get("version") != 1 or metadata.get("assetName") != animation_contract["assetName"] or \
                metadata.get("coordinateSystem") != animation_contract["coordinateSystem"] or metadata.get("animationName") != name:
            raise ValueError(f"GLB animation '{name}' has mismatched Super Cricket metadata identity.")

        source = source_clips.get(name)
        expected_events = source.get("events", []) if source else []
        events = metadata.get("events")
        if not isinstance(events, list) or len(events) != len(expected_events):
            raise ValueError(f"GLB animation '{name}' lost or added gameplay events.")
        duration = _animation_duration(document, animation)
        for expected, actual in zip(expected_events, events, strict=True):
            if actual.get("name") != expected.get("name") or \
                    not _close(actual.get("timeSeconds", -1), expected.get("timeSeconds", -2)):
                raise ValueError(f"GLB animation '{name}' changed event '{expected.get('name', '')}'.")
            if float(actual["timeSeconds"]) < 0 or float(actual["timeSeconds"]) > duration + 1e-4:
                raise ValueError(f"GLB animation '{name}' has an event outside its exported clip duration.")

        root_motion = metadata.get("rootMotion", {})
        if source is None:
            if root_motion.get("mode") != "zero" or root_motion.get("samples") != []:
                raise ValueError(f"Non-gameplay animation '{name}' must declare zero root motion.")
            continue

        expected_root_motion = source["rootMotion"]
        expected_samples = expected_root_motion["samples"]
        samples = root_motion.get("samples")
        if root_motion.get("mode") != expected_root_motion["mode"] or not isinstance(samples, list) or len(samples) != len(expected_samples):
            raise ValueError(f"GLB animation '{name}' lost or changed its root-motion sample count.")
        for expected, actual in zip(expected_samples, samples, strict=True):
            position = actual.get("positionMeters", [])
            wanted = expected.get("positionMeters", [])
            if len(position) != 3 or not _close(actual.get("timeSeconds", -1), expected.get("timeSeconds", -2)) or \
                    any(not _close(value, target) for value, target in zip(position, wanted, strict=True)):
                raise ValueError(f"GLB animation '{name}' changed a root-motion sample.")


def validate(path: Path, animation_contract_path: Path | None = None, role: str = "batter") -> None:
    document = load_glb_json(path)
    nodes = document.get("nodes", [])
    node_names = [node.get("name", "") for node in nodes]
    if len(node_names) != len(set(node_names)):
        raise ValueError("The humanoid GLB has duplicate node names.")

    skins = document.get("skins", [])
    if not skins:
        raise ValueError("The humanoid GLB has no skin definition.")
    joint_sets = [
        {node_names[index] for index in skin.get("joints", []) if 0 <= index < len(node_names)}
        for skin in skins
    ]
    joint_names = max(joint_sets, key=len)
    required_bones = REQUIRED_CORE_BONES | REQUIRED_FINGER_BONES
    missing_bones = sorted(required_bones - joint_names)
    if missing_bones:
        raise ValueError("The humanoid skin is missing required joints: " + ", ".join(missing_bones))
    if len(joint_names) != EXPECTED_BONE_COUNT:
        raise ValueError(f"The humanoid skin has {len(joint_names)} joints; expected {EXPECTED_BONE_COUNT}.")
    if any(names != joint_names for names in joint_sets):
        raise ValueError("The GLB contains skins with different joint sets.")

    animations = document.get("animations", [])
    animation_names = {animation.get("name", "") for animation in animations}
    required_animations = REQUIRED_ANIMATIONS if role == "batter" else set()
    if animation_contract_path is not None:
        animation_contract = load_animation_contract(animation_contract_path)
        required_animations = {clip.get("name", "") for clip in animation_contract.get("animations", [])}
        if role == "batter":
            required_animations.add("finger-grip-preview")
    missing_animations = sorted(required_animations - animation_names)
    if missing_animations:
        raise ValueError("The humanoid GLB is missing animations: " + ", ".join(missing_animations))

    nodes_by_index = nodes
    grip = next((animation for animation in animations if animation.get("name") == "finger-grip-preview"), None)
    if role == "batter":
        if grip is None:
            raise ValueError("The batter GLB has no finger-grip-preview animation.")
        grip_targets = {
            nodes_by_index[channel.get("target", {}).get("node", -1)].get("name", "")
            for channel in grip.get("channels", [])
            if 0 <= channel.get("target", {}).get("node", -1) < len(nodes_by_index)
        }
        missing_grip_targets = sorted((REQUIRED_FINGER_BONES | {"hand.L", "hand.R"}) - grip_targets)
        if missing_grip_targets:
            raise ValueError("The grip preview does not animate all hand joints: " + ", ".join(missing_grip_targets))

    skinned_primitives = sum(
        "JOINTS_0" in primitive.get("attributes", {}) and "WEIGHTS_0" in primitive.get("attributes", {})
        for mesh in document.get("meshes", [])
        for primitive in mesh.get("primitives", [])
    )
    if skinned_primitives == 0:
        raise ValueError("The GLB contains no skinned mesh primitives.")
    if animation_contract_path is not None:
        _validate_player_metadata(document, animation_contract_path.resolve())
    details = [f"{len(joint_names)} humanoid joints", f"{len(animation_names)} clips", f"{skinned_primitives} skinned primitives"]
    if role == "batter":
        details.append("all 30 finger segments in the grip animation")
    if animation_contract_path is not None:
        gameplay_count = len(load_animation_contract(animation_contract_path).get("animations", []))
        details.append(f"preserved events/root-motion for {gameplay_count} gameplay clips")
    print(f"PASS: {path} is glTF 2.0 with " + ", ".join(details) + ".")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", type=Path)
    parser.add_argument("--role", choices=("batter", "bowler"), default="batter")
    parser.add_argument("--animation-contract", "--player-asset", dest="animation_contract", type=Path,
                        help="Validate event/root-motion extras against this animation contract (or legacy player asset).")
    args = parser.parse_args()
    validate(args.path.resolve(), args.animation_contract.resolve() if args.animation_contract else None, args.role)


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        raise SystemExit(1) from error
