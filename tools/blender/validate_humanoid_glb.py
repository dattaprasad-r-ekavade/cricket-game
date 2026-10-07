"""Validate the structural contract of the practice-batter humanoid GLB."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import struct
import sys


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


def validate(path: Path) -> None:
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
    missing_animations = sorted(REQUIRED_ANIMATIONS - animation_names)
    if missing_animations:
        raise ValueError("The humanoid GLB is missing animations: " + ", ".join(missing_animations))

    nodes_by_index = nodes
    grip = next(animation for animation in animations if animation.get("name") == "finger-grip-preview")
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
    print(
        f"PASS: {path} is glTF 2.0 with {len(joint_names)} humanoid joints, "
        f"{len(animation_names)} clips, all 30 finger segments in the grip animation, "
        f"and {skinned_primitives} skinned primitives."
    )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", type=Path)
    args = parser.parse_args()
    validate(args.path.resolve())


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        raise SystemExit(1) from error
