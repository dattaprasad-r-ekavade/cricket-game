"""Add repeatable fielding actions to the open Super Cricket player rig."""

from __future__ import annotations

import bpy


FPS = 30

NEUTRAL = {
    "spine": (0.025, 0.0, 0.0),
    "head": (0.0, 0.0, 0.0),
    "upper_arm.L": (0.0, 0.0, -0.04),
    "forearm.L": (0.0, 0.0, 0.0),
    "upper_arm.R": (0.0, 0.0, 0.04),
    "forearm.R": (0.0, 0.0, 0.0),
    "thigh.L": (0.0, 0.0, 0.015),
    "shin.L": (0.0, 0.0, 0.0),
    "foot.L": (0.0, 0.0, 0.0),
    "thigh.R": (0.0, 0.0, -0.015),
    "shin.R": (0.0, 0.0, 0.0),
    "foot.R": (0.0, 0.0, 0.0),
}


def pose(**overrides: tuple[float, float, float]) -> dict[str, tuple[float, float, float]]:
    normalized = {
        name.replace("_L", ".L").replace("_R", ".R"): rotation
        for name, rotation in overrides.items()
    }
    return {**NEUTRAL, **normalized}


FIELDING_ACTIONS = [
    (
        "fielder-catch",
        [
            (1, pose()),
            (5, pose(spine=(-0.08, 0.0, 0.0), thigh_L=(0.08, 0.0, 0.0), thigh_R=(0.08, 0.0, 0.0))),
            (10, pose(
                spine=(-0.12, 0.0, 0.0), head=(0.10, 0.0, 0.0),
                upper_arm_L=(-2.35, 0.0, -0.18), forearm_L=(-0.30, 0.0, 0.0),
                upper_arm_R=(2.35, 0.0, 0.18), forearm_R=(-0.30, 0.0, 0.0),
                thigh_L=(0.13, 0.0, 0.08), thigh_R=(0.13, 0.0, -0.08),
            )),
            (15, pose(
                spine=(-0.16, 0.0, 0.0), head=(0.12, 0.0, 0.0),
                upper_arm_L=(-2.50, 0.0, -0.12), forearm_L=(-0.55, 0.0, 0.0),
                upper_arm_R=(2.50, 0.0, 0.12), forearm_R=(-0.55, 0.0, 0.0),
                thigh_L=(0.12, 0.0, 0.06), thigh_R=(0.12, 0.0, -0.06),
            )),
            (22, pose(
                spine=(0.04, 0.0, 0.0), head=(-0.03, 0.0, 0.0),
                upper_arm_L=(-0.66, 0.0, -0.20), forearm_L=(-0.92, 0.0, 0.0),
                upper_arm_R=(0.66, 0.0, 0.20), forearm_R=(-0.92, 0.0, 0.0),
            )),
            (31, pose(
                spine=(0.025, 0.0, 0.0),
                upper_arm_L=(-0.48, 0.0, -0.16), forearm_L=(-0.86, 0.0, 0.0),
                upper_arm_R=(0.48, 0.0, 0.16), forearm_R=(-0.86, 0.0, 0.0),
            )),
        ],
        {"catch-secured": 15},
    ),
    (
        "fielder-pickup",
        [
            (1, pose()),
            (5, pose(
                spine=(-0.12, 0.0, 0.0), head=(0.12, 0.0, 0.0),
                upper_arm_L=(-0.28, 0.0, -0.14), forearm_L=(-0.62, 0.0, 0.0),
                upper_arm_R=(-0.28, 0.0, 0.14), forearm_R=(-0.62, 0.0, 0.0),
                thigh_L=(0.32, 0.0, 0.07), shin_L=(-0.48, 0.0, 0.0),
                thigh_R=(0.32, 0.0, -0.07), shin_R=(-0.48, 0.0, 0.0),
            )),
            (11, pose(
                spine=(-0.24, 0.0, 0.0), head=(0.18, 0.0, 0.0),
                upper_arm_L=(-0.34, 0.0, -0.20), forearm_L=(-0.90, 0.0, 0.0),
                upper_arm_R=(-0.34, 0.0, 0.20), forearm_R=(-0.90, 0.0, 0.0),
                thigh_L=(0.52, 0.0, 0.08), shin_L=(-0.82, 0.0, 0.0),
                thigh_R=(0.45, 0.0, -0.08), shin_R=(-0.72, 0.0, 0.0),
            )),
            (13, pose(
                spine=(-0.18, 0.0, 0.0), head=(0.10, 0.0, 0.0),
                upper_arm_L=(-0.42, 0.0, -0.16), forearm_L=(-0.75, 0.0, 0.0),
                upper_arm_R=(-0.42, 0.0, 0.16), forearm_R=(-0.75, 0.0, 0.0),
                thigh_L=(0.40, 0.0, 0.06), shin_L=(-0.56, 0.0, 0.0),
                thigh_R=(0.36, 0.0, -0.06), shin_R=(-0.52, 0.0, 0.0),
            )),
            (16, pose(
                spine=(-0.03, 0.0, 0.0),
                upper_arm_L=(-0.34, 0.0, -0.10), forearm_L=(-0.52, 0.0, 0.0),
                upper_arm_R=(-0.34, 0.0, 0.10), forearm_R=(-0.52, 0.0, 0.0),
                thigh_L=(0.08, 0.0, 0.02), shin_L=(-0.10, 0.0, 0.0),
                thigh_R=(0.08, 0.0, -0.02), shin_R=(-0.10, 0.0, 0.0),
            )),
        ],
        {"ball-secured": 13},
    ),
    (
        "fielder-throw",
        [
            (1, pose()),
            (4, pose(
                spine=(0.0, 0.24, 0.0), head=(0.0, -0.12, 0.0),
                upper_arm_L=(-0.18, 0.0, -0.10), upper_arm_R=(-0.72, 0.0, 0.52),
                forearm_R=(-0.92, 0.0, 0.0), thigh_L=(0.12, 0.0, 0.08),
                thigh_R=(-0.08, 0.0, -0.04),
            )),
            (7, pose(
                spine=(0.02, 0.34, 0.0), head=(0.04, -0.18, 0.0),
                upper_arm_L=(-0.45, 0.0, -0.12), forearm_L=(-0.28, 0.0, 0.0),
                upper_arm_R=(-1.32, 0.0, 0.46), forearm_R=(-0.58, 0.0, 0.0),
                thigh_L=(0.22, 0.0, 0.10), thigh_R=(-0.16, 0.0, -0.05),
            )),
            (10, pose(
                spine=(-0.12, -0.12, 0.0), head=(0.0, 0.16, 0.0),
                upper_arm_L=(-0.72, 0.0, -0.14), forearm_L=(-0.24, 0.0, 0.0),
                upper_arm_R=(-0.56, 0.0, -0.16), forearm_R=(-1.10, 0.0, 0.0),
                thigh_L=(0.16, 0.0, 0.08), thigh_R=(-0.10, 0.0, -0.05),
            )),
            (13, pose(
                spine=(-0.16, -0.08, 0.0), head=(0.02, 0.12, 0.0),
                upper_arm_L=(-0.44, 0.0, -0.10), forearm_L=(-0.12, 0.0, 0.0),
                upper_arm_R=(0.12, 0.0, 0.02), forearm_R=(-0.20, 0.0, 0.0),
                thigh_L=(0.08, 0.0, 0.02), thigh_R=(0.0, 0.0, -0.02),
            )),
            (16, pose(
                spine=(-0.07, 0.0, 0.0), head=(0.0, 0.0, 0.0),
                upper_arm_L=(-0.22, 0.0, -0.06), upper_arm_R=(0.08, 0.0, 0.02),
                forearm_R=(-0.08, 0.0, 0.0),
            )),
            (19, pose()),
        ],
        {"ball-release": 10},
    ),
]


def create_action(armature: bpy.types.Object, name: str, keyframes: list, events: dict) -> bpy.types.Action:
    action = bpy.data.actions.get(name)
    if action is not None:
        bpy.data.actions.remove(action, do_unlink=True)

    animation = armature.animation_data_create()
    animation.action = None
    action = bpy.data.actions.new(name)
    animation.action = action
    if hasattr(action, "slots") and len(action.slots) > 0:
        animation.action_slot = action.slots[0]

    scene = bpy.context.scene
    for frame, rotations in keyframes:
        scene.frame_set(frame)
        for bone_name, pose_bone in armature.pose.bones.items():
            pose_bone.rotation_mode = "XYZ"
            pose_bone.rotation_euler = rotations.get(bone_name, (0.0, 0.0, 0.0))
            pose_bone.keyframe_insert(data_path="rotation_euler", frame=frame, group=bone_name)

    action["sc_events"] = events
    action.use_fake_user = True
    return action


def author_fielding_animations() -> None:
    armature = next((obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"), None)
    if armature is None:
        raise RuntimeError("The open scene has no armature to receive fielding actions.")

    missing_bones = set(NEUTRAL) - {bone.name for bone in armature.pose.bones}
    if missing_bones:
        raise RuntimeError("Player rig is missing expected bones: " + ", ".join(sorted(missing_bones)))

    original_action = armature.animation_data.action if armature.animation_data else None
    original_action_name = original_action.name if original_action else None
    created = [create_action(armature, name, keyframes, events) for name, keyframes, events in FIELDING_ACTIONS]

    if original_action_name in bpy.data.actions:
        restored = bpy.data.actions[original_action_name]
        armature.animation_data.action = restored
        if hasattr(restored, "slots") and len(restored.slots) > 0:
            armature.animation_data.action_slot = restored.slots[0]
    else:
        armature.animation_data.action = created[0]
        if hasattr(created[0], "slots") and len(created[0].slots) > 0:
            armature.animation_data.action_slot = created[0].slots[0]

    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    print("Authored fielding clips:")
    for action in created:
        print(f"  {action.name}: {action.frame_range[0]:g}-{action.frame_range[1]:g} frames, events={dict(action['sc_events'])}")
    if not bpy.data.filepath:
        raise RuntimeError("Save the player rig once before running the fielding animation author.")
    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
    print(f"Saved Blender source: {bpy.data.filepath}")


author_fielding_animations()
