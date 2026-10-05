"""Generate a low-poly practice batter and export Super Cricket's v1 player format.

Run from the repository root:
    blender --background --factory-startup --python tools/blender/build_practice_batter.py -- \
        --blend-output assets/characters/practice-batter.blend \
        --asset-output assets/characters/practice-batter.scplayer.json
"""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector


FPS = 30
BASIS_3 = Matrix(((1.0, 0.0, 0.0), (0.0, 0.0, 1.0), (0.0, -1.0, 0.0)))
BASIS_4 = BASIS_3.to_4x4()

BONES = [
    ("root", None, (0.0, 0.0, 0.00), (0.0, 0.0, 0.82)),
    ("spine", "root", (0.0, 0.0, 0.77), (0.0, 0.0, 1.33)),
    ("head", "spine", (0.0, 0.0, 1.32), (0.0, 0.0, 1.76)),
    ("upper_arm.L", "spine", (-0.20, 0.0, 1.27), (-0.39, -0.01, 1.04)),
    ("forearm.L", "upper_arm.L", (-0.39, -0.01, 1.04), (-0.42, -0.12, 0.89)),
    ("upper_arm.R", "spine", (0.20, 0.0, 1.27), (0.35, -0.01, 1.08)),
    ("forearm.R", "upper_arm.R", (0.35, -0.01, 1.08), (0.43, -0.16, 0.94)),
    ("thigh.L", "root", (-0.105, 0.0, 0.82), (-0.14, 0.0, 0.46)),
    ("shin.L", "thigh.L", (-0.14, 0.0, 0.46), (-0.14, -0.025, 0.08)),
    ("foot.L", "shin.L", (-0.14, -0.025, 0.08), (-0.14, -0.18, 0.055)),
    ("thigh.R", "root", (0.105, 0.0, 0.82), (0.14, 0.0, 0.46)),
    ("shin.R", "thigh.R", (0.14, 0.0, 0.46), (0.14, -0.025, 0.08)),
    ("foot.R", "shin.R", (0.14, -0.025, 0.08), (0.14, -0.18, 0.055)),
]

MATERIALS = {
    "shirt": (0.08, 0.28, 0.58, 1.0),
    "helmet": (0.025, 0.10, 0.22, 1.0),
    "skin": (0.72, 0.42, 0.26, 1.0),
    "glove": (0.90, 0.89, 0.81, 1.0),
    "trouser": (0.86, 0.87, 0.84, 1.0),
    "pad": (0.95, 0.94, 0.88, 1.0),
    "shoe": (0.09, 0.11, 0.13, 1.0),
    "bat": (0.63, 0.36, 0.13, 1.0),
    "grille": (0.74, 0.78, 0.80, 1.0),
}


def parse_args() -> argparse.Namespace:
    forwarded = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--blend-output", default="assets/characters/practice-batter.blend")
    parser.add_argument("--asset-output", default="assets/characters/practice-batter.scplayer.json")
    return parser.parse_args(forwarded)


def convert_vector(value: Vector) -> list[float]:
    return [float(value.x), float(value.z), float(-value.y)]


def transform_data(matrix: Matrix) -> dict:
    translation, rotation, scale = matrix.decompose()
    converted_rotation = (BASIS_4 @ rotation.to_matrix().to_4x4() @ BASIS_4.inverted()).to_quaternion()
    translation_game = convert_vector(translation)
    scale_game = [float(scale.x), float(scale.z), float(scale.y)]
    return {
        "translation": {"x": translation_game[0], "y": translation_game[1], "z": translation_game[2]},
        "rotation": {"x": float(converted_rotation.x), "y": float(converted_rotation.y), "z": float(converted_rotation.z), "w": float(converted_rotation.w)},
        "scale": {"x": scale_game[0], "y": scale_game[1], "z": scale_game[2]},
    }


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.armatures, bpy.data.actions, bpy.data.materials):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)


def create_armature() -> bpy.types.Object:
    armature_data = bpy.data.armatures.new("Practice Batter Rig")
    armature = bpy.data.objects.new("Practice Batter", armature_data)
    bpy.context.scene.collection.objects.link(armature)
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    bones = {}
    for name, parent_name, head, tail in BONES:
        bone = armature_data.edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        if parent_name:
            bone.parent = bones[parent_name]
        bone.use_connect = False
        bones[name] = bone

    bpy.ops.object.mode_set(mode="OBJECT")
    armature.show_in_front = True
    armature.data.display_type = "STICK"
    return armature


def material_for(name: str) -> bpy.types.Material:
    material = bpy.data.materials.get(name)
    if material is None:
        material = bpy.data.materials.new(name)
    material.diffuse_color = MATERIALS[name]
    return material


def add_sphere_part(
    armature: bpy.types.Object,
    name: str,
    material_name: str,
    bone_name: str,
    location: tuple[float, float, float],
    scale: tuple[float, float, float],
    segments: int = 10,
    rings: int = 6,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1.0, location=location)
    part = bpy.context.object
    part.name = name
    part.scale = scale
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    part.data.materials.append(material_for(material_name))
    for polygon in part.data.polygons:
        polygon.use_smooth = True

    group = part.vertex_groups.new(name=bone_name)
    if part.data.vertices:
        group.add([vertex.index for vertex in part.data.vertices], 1.0, "REPLACE")
    modifier = part.modifiers.new("Practice Batter Skin", "ARMATURE")
    modifier.object = armature
    part.parent = armature
    part["sc_player_part"] = True
    return part


def add_box_part(
    armature: bpy.types.Object,
    name: str,
    material_name: str,
    bone_name: str,
    location: tuple[float, float, float],
    scale: tuple[float, float, float],
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    part = bpy.context.object
    part.name = name
    part.scale = scale
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    part.data.materials.append(material_for(material_name))
    group = part.vertex_groups.new(name=bone_name)
    group.add([vertex.index for vertex in part.data.vertices], 1.0, "REPLACE")
    modifier = part.modifiers.new("Practice Batter Skin", "ARMATURE")
    modifier.object = armature
    part.parent = armature
    part["sc_player_part"] = True
    return part


def create_character(armature: bpy.types.Object) -> list[bpy.types.Object]:
    parts = [
        add_sphere_part(armature, "Shirt", "shirt", "spine", (0, 0, 1.08), (0.24, 0.17, 0.31), 12, 8),
        add_sphere_part(armature, "Head", "skin", "head", (0, -0.01, 1.53), (0.13, 0.125, 0.15), 12, 8),
        add_sphere_part(armature, "Helmet Shell", "helmet", "head", (0, 0.0, 1.66), (0.165, 0.16, 0.12), 12, 6),
        add_box_part(armature, "Helmet Peak", "helmet", "head", (0, -0.105, 1.59), (0.17, 0.12, 0.045)),
        add_box_part(armature, "Helmet Grille", "grille", "head", (0, -0.145, 1.51), (0.20, 0.018, 0.015)),
        add_box_part(armature, "Grille Side L", "grille", "head", (-0.085, -0.145, 1.55), (0.012, 0.018, 0.11)),
        add_box_part(armature, "Grille Side R", "grille", "head", (0.085, -0.145, 1.55), (0.012, 0.018, 0.11)),
        add_sphere_part(armature, "Upper Arm L", "shirt", "upper_arm.L", (-0.295, 0, 1.155), (0.11, 0.115, 0.20)),
        add_sphere_part(armature, "Forearm L", "shirt", "forearm.L", (-0.405, -0.065, 0.965), (0.085, 0.095, 0.16)),
        add_sphere_part(armature, "Upper Arm R", "shirt", "upper_arm.R", (0.28, 0, 1.165), (0.11, 0.115, 0.19)),
        add_sphere_part(armature, "Forearm R", "shirt", "forearm.R", (0.395, -0.085, 1.00), (0.085, 0.095, 0.15)),
        add_sphere_part(armature, "Glove L", "glove", "forearm.L", (-0.43, -0.13, 0.88), (0.095, 0.075, 0.075)),
        add_sphere_part(armature, "Glove R", "glove", "forearm.R", (0.45, -0.18, 0.91), (0.09, 0.075, 0.075)),
        add_sphere_part(armature, "Trouser L", "trouser", "thigh.L", (-0.12, 0, 0.65), (0.105, 0.14, 0.23)),
        add_sphere_part(armature, "Trouser R", "trouser", "thigh.R", (0.12, 0, 0.65), (0.105, 0.14, 0.23)),
        add_sphere_part(armature, "Pad L", "pad", "shin.L", (-0.14, -0.105, 0.30), (0.095, 0.07, 0.23)),
        add_sphere_part(armature, "Pad R", "pad", "shin.R", (0.14, -0.105, 0.30), (0.095, 0.07, 0.23)),
        add_sphere_part(armature, "Shoe L", "shoe", "foot.L", (-0.14, -0.09, 0.06), (0.09, 0.16, 0.065)),
        add_sphere_part(armature, "Shoe R", "shoe", "foot.R", (0.14, -0.09, 0.06), (0.09, 0.16, 0.065)),
        add_box_part(armature, "Bat Blade", "bat", "forearm.R", (0.49, -0.25, 0.48), (0.115, 0.055, 0.76)),
        add_box_part(armature, "Bat Handle", "shoe", "forearm.R", (0.48, -0.25, 1.03), (0.048, 0.045, 0.40)),
    ]

    # Keep the generated asset organized for artists opening the source scene.
    collection = bpy.data.collections.new("Player Mesh")
    bpy.context.scene.collection.children.link(collection)
    for part in parts:
        for old_collection in list(part.users_collection):
            old_collection.objects.unlink(part)
        collection.objects.link(part)
    armature_collection = bpy.data.collections.new("Player Rig")
    bpy.context.scene.collection.children.link(armature_collection)
    for old_collection in list(armature.users_collection):
        old_collection.objects.unlink(armature)
    armature_collection.objects.link(armature)
    return parts


def key_pose(armature: bpy.types.Object, frame: int, rotations: dict[str, tuple[float, float, float]]) -> None:
    scene = bpy.context.scene
    scene.frame_set(frame)
    for bone_name, rotation in rotations.items():
        pose_bone = armature.pose.bones[bone_name]
        pose_bone.rotation_mode = "XYZ"
        pose_bone.rotation_euler = rotation
        pose_bone.keyframe_insert(data_path="rotation_euler", frame=frame, group=bone_name)


def create_animation(armature: bpy.types.Object, name: str, keyframes: list[tuple[int, dict]]) -> bpy.types.Action:
    animation = armature.animation_data_create()
    action = bpy.data.actions.new(name)
    animation.action = action
    if hasattr(action, "slots") and len(action.slots) > 0:
        animation.action_slot = action.slots[0]
    for frame, rotations in keyframes:
        key_pose(armature, frame, rotations)
    for curve in getattr(action, "fcurves", []):
        for key in curve.keyframe_points:
            key.interpolation = "BEZIER"
    return action


def create_animations(armature: bpy.types.Object) -> list[bpy.types.Action]:
    neutral = {
        "spine": (0.025, 0.0, 0.0),
        "head": (0.0, 0.0, 0.0),
        "upper_arm.L": (0.0, 0.0, -0.04),
        "forearm.L": (0.0, 0.0, 0.0),
        "upper_arm.R": (0.0, 0.0, 0.04),
        "forearm.R": (0.0, 0.0, 0.0),
        "thigh.L": (0.0, 0.0, 0.015),
        "thigh.R": (0.0, 0.0, -0.015),
    }
    stance = create_animation(
        armature,
        "practice-stance",
        [
            (1, neutral),
            (16, {**neutral, "spine": (0.055, 0.015, 0.0), "head": (-0.025, 0.0, 0.0)}),
            (31, neutral),
            (46, {**neutral, "spine": (0.01, -0.015, 0.0), "head": (0.015, 0.0, 0.0)}),
            (61, neutral),
        ],
    )

    defence = create_animation(
        armature,
        "defensive-block",
        [
            (1, neutral),
            (13, {**neutral, "spine": (0.07, -0.035, 0.0), "upper_arm.L": (-0.10, 0.0, -0.09), "upper_arm.R": (0.06, 0.0, 0.09)}),
            (19, {**neutral, "spine": (0.12, -0.055, 0.0), "upper_arm.L": (-0.16, 0.0, -0.12), "forearm.L": (-0.22, 0.0, 0.0), "upper_arm.R": (-0.08, 0.0, 0.16), "forearm.R": (-0.38, 0.0, 0.0)}),
            (30, {**neutral, "spine": (0.055, -0.025, 0.0), "upper_arm.L": (-0.08, 0.0, -0.06), "upper_arm.R": (0.0, 0.0, 0.08), "forearm.R": (-0.14, 0.0, 0.0)}),
            (46, neutral),
            (61, neutral),
        ],
    )

    drive = create_animation(
        armature,
        "front-foot-drive",
        [
            (1, neutral),
            (13, {**neutral, "spine": (0.03, -0.10, 0.0), "thigh.L": (0.0, 0.0, 0.11), "upper_arm.R": (0.08, 0.0, 0.12)}),
            (19, {**neutral, "spine": (-0.05, 0.30, 0.0), "head": (0.04, 0.0, 0.0), "upper_arm.L": (-0.35, 0.0, -0.16), "forearm.L": (-0.42, 0.0, 0.0), "upper_arm.R": (-0.55, 0.0, 0.38), "forearm.R": (-0.82, 0.0, 0.0), "thigh.L": (0.0, 0.0, 0.18)}),
            (28, {**neutral, "spine": (-0.10, 0.16, 0.0), "upper_arm.L": (-0.18, 0.0, -0.10), "upper_arm.R": (-0.36, 0.0, 0.22), "forearm.R": (-0.40, 0.0, 0.0)}),
            (40, {**neutral, "spine": (0.04, 0.0, 0.0), "thigh.L": (0.0, 0.0, 0.04)}),
            (46, {**neutral, "spine": (0.04, 0.0, 0.0), "thigh.L": (0.0, 0.0, 0.04)}),
            (61, neutral),
        ],
    )

    loft = create_animation(
        armature,
        "lofted-drive",
        [
            (1, neutral),
            (13, {**neutral, "spine": (0.025, -0.12, 0.0), "thigh.L": (0.0, 0.0, 0.10), "upper_arm.R": (0.10, 0.0, 0.15)}),
            (19, {**neutral, "spine": (-0.13, 0.36, 0.0), "head": (0.06, 0.0, 0.0), "upper_arm.L": (-0.42, 0.0, -0.18), "forearm.L": (-0.48, 0.0, 0.0), "upper_arm.R": (-0.70, 0.0, 0.48), "forearm.R": (-1.05, 0.0, 0.0), "thigh.L": (0.0, 0.0, 0.20)}),
            (29, {**neutral, "spine": (-0.16, 0.22, 0.0), "upper_arm.L": (-0.23, 0.0, -0.12), "upper_arm.R": (-0.46, 0.0, 0.28), "forearm.R": (-0.55, 0.0, 0.0)}),
            (42, {**neutral, "spine": (0.04, 0.0, 0.0), "thigh.L": (0.0, 0.0, 0.04)}),
            (61, neutral),
        ],
    )
    return [stance, defence, drive, loft]


def export_mesh(part: bpy.types.Object, armature: bpy.types.Object, bone_indices: dict[str, int]) -> dict:
    mesh = part.data
    mesh.calc_loop_triangles()
    uv_layer = mesh.uv_layers.active
    matrix_to_armature = armature.matrix_world.inverted() @ part.matrix_world
    normal_matrix = matrix_to_armature.to_3x3()

    positions: list[float] = []
    normals: list[float] = []
    texture_coordinates: list[float] = []
    bone_indices_data: list[int] = []
    bone_weights: list[float] = []
    indices: list[int] = []

    for triangle in mesh.loop_triangles:
        for loop_index, vertex_index in zip(triangle.loops, triangle.vertices):
            vertex = mesh.vertices[vertex_index]
            position = matrix_to_armature @ vertex.co
            normal = (normal_matrix @ vertex.normal).normalized()
            positions.extend(convert_vector(position))
            normals.extend(convert_vector(normal))

            uv = uv_layer.data[loop_index].uv if uv_layer else (0.0, 0.0)
            texture_coordinates.extend((float(uv[0]), float(uv[1])))

            influences = []
            for group_weight in vertex.groups:
                group_name = part.vertex_groups[group_weight.group].name
                if group_name in bone_indices and group_weight.weight > 0.0:
                    influences.append((bone_indices[group_name], float(group_weight.weight)))
            influences.sort(key=lambda influence: influence[1], reverse=True)
            influences = influences[:4]
            total_weight = sum(weight for _, weight in influences)
            if total_weight <= 0.0:
                raise RuntimeError(f"Vertex {vertex_index} on {part.name} is not assigned to the player skeleton.")
            influences = [(index, weight / total_weight) for index, weight in influences]
            while len(influences) < 4:
                influences.append((0, 0.0))
            bone_indices_data.extend(index for index, _ in influences)
            bone_weights.extend(weight for _, weight in influences)
            indices.append(len(indices))

    material = mesh.materials[0] if mesh.materials else None
    rgba = material.diffuse_color if material else (0.8, 0.8, 0.8, 1.0)
    color = [float(rgba[0]), float(rgba[1]), float(rgba[2])]
    return {
        "name": part.name,
        "diffuseColor": {"x": color[0], "y": color[1], "z": color[2]},
        "positions": positions,
        "normals": normals,
        "textureCoordinates": texture_coordinates,
        "boneIndices": bone_indices_data,
        "boneWeights": bone_weights,
        "indices": indices,
    }


def export_asset(armature: bpy.types.Object, parts: list[bpy.types.Object], actions: list[bpy.types.Action]) -> dict:
    bones = list(armature.data.bones)
    bone_indices = {bone.name: index for index, bone in enumerate(bones)}
    bone_data = []
    for bone in bones:
        bone_data.append({
            "name": bone.name,
            "parentIndex": bone_indices[bone.parent.name] if bone.parent else -1,
            "bindPose": transform_data(bone.matrix_local),
        })

    animations = []
    scene = bpy.context.scene
    for action in actions:
        armature.animation_data.action = action
        if hasattr(action, "slots") and len(action.slots) > 0:
            armature.animation_data.action_slot = action.slots[0]
        start_frame = int(round(action.frame_range[0]))
        end_frame = int(round(action.frame_range[1]))
        duration = (end_frame - start_frame) / FPS
        sample_count = int(round(duration * FPS))
        samples = []
        for frame_offset in range(sample_count + 1):
            frame = start_frame + frame_offset
            scene.frame_set(frame)
            samples.append({
                "timeSeconds": frame_offset / FPS,
                "bones": [transform_data(armature.pose.bones[bone.name].matrix) for bone in bones],
            })
        animations.append({"name": action.name, "durationSeconds": duration, "samples": samples})

    return {
        "version": 1,
        "name": "Practice Batter",
        "coordinateSystem": "right-handed-y-up-metres",
        "bones": bone_data,
        "meshes": [export_mesh(part, armature, bone_indices) for part in parts],
        "animations": animations,
    }


def main() -> None:
    args = parse_args()
    blend_output = Path(args.blend_output).resolve()
    asset_output = Path(args.asset_output).resolve()
    blend_output.parent.mkdir(parents=True, exist_ok=True)
    asset_output.parent.mkdir(parents=True, exist_ok=True)

    clear_scene()
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.frame_start = 1
    scene.frame_end = 61
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    armature = create_armature()
    parts = create_character(armature)
    actions = create_animations(armature)
    asset = export_asset(armature, parts, actions)

    with asset_output.open("w", encoding="utf-8", newline="\n") as output:
        json.dump(asset, output, separators=(",", ":"), ensure_ascii=False)
        output.write("\n")
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_output))
    print(f"Exported {len(asset['bones'])} bones, {len(asset['meshes'])} meshes, {len(asset['animations'])} clips")
    print(f"  Blender source: {blend_output}")
    print(f"  Game asset:     {asset_output}")


if __name__ == "__main__":
    main()
