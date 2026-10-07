"""Create a 61-bone humanoid GLB continuation of a practice player asset.

Run from the repository root with the installed Blender:
    blender --background --python tools/blender/build_humanoid_batter.py -- \
        --role batter
    blender --background --python tools/blender/build_humanoid_batter.py -- \
        --role bowler

The original Blender scene supplies editable geometry and actions. A compact
animation-contract JSON supplies the events and root motion embedded in the GLB.
"""

from __future__ import annotations

import argparse
from pathlib import Path
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from embed_player_metadata import embed_player_metadata  # noqa: E402
from player_animation_contract import load_animation_contract  # noqa: E402


ROOT = Path(__file__).resolve().parents[2]
ASSET_PATHS = {
    "batter": {
        "source": ROOT / "assets" / "characters" / "practice-batter.blend",
        "contract": ROOT / "assets" / "characters" / "practice-batter.animation-contract.json",
        "blend": ROOT / "assets" / "characters" / "practice-batter-humanoid.blend",
        "glb": ROOT / "assets" / "characters" / "practice-batter-humanoid.glb",
    },
    "bowler": {
        "source": ROOT / "assets" / "characters" / "practice-bowler.blend",
        "contract": ROOT / "assets" / "characters" / "practice-bowler.animation-contract.json",
        "blend": ROOT / "assets" / "characters" / "practice-bowler-humanoid.blend",
        "glb": ROOT / "assets" / "characters" / "practice-bowler-humanoid.glb",
    },
}
EXPECTED_BONE_COUNT = 61


def parse_args() -> argparse.Namespace:
    forwarded = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--role", choices=tuple(ASSET_PATHS), default="batter")
    parser.add_argument("--source-blend", type=Path)
    parser.add_argument("--animation-contract", "--player-asset", dest="animation_contract", type=Path)
    parser.add_argument("--blend-output", type=Path)
    parser.add_argument("--glb-output", type=Path)
    parser.add_argument("--preview-dir", type=Path, default=ROOT / "artifacts")
    return parser.parse_args(forwarded)


def old_bone_spec(old_bones: dict, name: str, parent: str | None) -> tuple:
    bone = old_bones[name]
    # The source was generated with Blender's default zero roll. Bone exposes
    # matrix axes but not roll in current Blender versions.
    return name, parent, bone.head_local.copy(), bone.tail_local.copy(), 0.0


def build_bone_specs(old_bones: dict) -> list[tuple]:
    specs = [
        old_bone_spec(old_bones, "root", None),
        ("pelvis", "root", (0.0, 0.0, 0.77), (0.0, 0.0, 0.98), 0.0),
        ("spine.01", "pelvis", (0.0, 0.0, 0.94), (0.0, 0.0, 1.10), 0.0),
        ("spine.02", "spine.01", (0.0, 0.0, 1.08), (0.0, 0.0, 1.24), 0.0),
        old_bone_spec(old_bones, "spine", "spine.02"),
        ("chest", "spine", (0.0, 0.0, 1.22), (0.0, 0.0, 1.38), 0.0),
        ("neck", "chest", (0.0, 0.0, 1.32), (0.0, 0.0, 1.45), 0.0),
        old_bone_spec(old_bones, "head", "neck"),
        ("jaw", "head", (0.0, -0.105, 1.45), (0.0, -0.105, 1.50), 0.0),
        ("eye.L", "head", (-0.04, -0.12, 1.56), (-0.04, -0.16, 1.56), 0.0),
        ("eye.R", "head", (0.04, -0.12, 1.56), (0.04, -0.16, 1.56), 0.0),
    ]

    for side in ("L", "R"):
        old_upper = old_bones[f"upper_arm.{side}"]
        sign = -1.0 if side == "L" else 1.0
        shoulder = old_upper.head_local.copy()
        specs.extend([
            (f"scapula.{side}", "chest", (0.0, 0.015, 1.31), (shoulder.x * 0.72, shoulder.y + 0.015, 1.30), 0.0),
            (f"clavicle.{side}", "chest", (0.0, 0.0, 1.32), tuple(shoulder), 0.0),
            old_bone_spec(old_bones, f"upper_arm.{side}", f"clavicle.{side}"),
            old_bone_spec(old_bones, f"forearm.{side}", f"upper_arm.{side}"),
        ])

        if side == "L":
            hand_head = Vector((-0.42, -0.12, 0.89))
            hand_tail = Vector((-0.44, -0.18, 0.85))
            offsets = {"index": -0.03, "middle": 0.0, "ring": 0.03, "pinky": 0.065, "thumb": -0.065}
        else:
            hand_head = Vector((0.43, -0.16, 0.94))
            hand_tail = Vector((0.46, -0.22, 0.90))
            offsets = {"index": 0.03, "middle": 0.0, "ring": -0.03, "pinky": -0.065, "thumb": 0.065}

        specs.append((f"hand.{side}", f"forearm.{side}", tuple(hand_head), tuple(hand_tail), 0.0))
        for digit in ("thumb", "index", "middle", "ring", "pinky"):
            x_offset = offsets[digit]
            head = hand_tail + Vector((x_offset, -0.005, 0.020 if digit == "thumb" else 0.0))
            parent = f"hand.{side}"
            for segment in range(1, 4):
                name = f"{digit}.{segment:02d}.{side}"
                lateral = sign * (0.008 if digit == "thumb" else 0.0015)
                tail = head + Vector((lateral, -0.002, -0.023))
                specs.append((name, parent, tuple(head), tuple(tail), 0.0))
                head = tail
                parent = name

    for side in ("L", "R"):
        specs.extend([
            old_bone_spec(old_bones, f"thigh.{side}", "pelvis"),
            old_bone_spec(old_bones, f"shin.{side}", f"thigh.{side}"),
            old_bone_spec(old_bones, f"foot.{side}", f"shin.{side}"),
        ])
        foot = old_bones[f"foot.{side}"]
        heel_x = float(foot.head_local.x)
        specs.append((f"toe.{side}", f"foot.{side}", tuple(foot.tail_local),
                      (heel_x, float(foot.tail_local.y) - 0.09, 0.045), 0.0))
        specs.append((f"heel.{side}", f"foot.{side}", tuple(foot.head_local),
                      (heel_x, float(foot.head_local.y) + 0.065, 0.05), 0.0))

    names = [spec[0] for spec in specs]
    if len(names) != EXPECTED_BONE_COUNT or len(set(names)) != EXPECTED_BONE_COUNT:
        raise RuntimeError(f"Humanoid specification has {len(names)} unique bones, expected {EXPECTED_BONE_COUNT}.")
    known = set()
    for name, parent, *_ in specs:
        if parent is not None and parent not in known:
            raise RuntimeError(f"Bone '{name}' refers to parent '{parent}' before that parent is defined.")
        known.add(name)
    return specs


def replace_vertex_weights(obj: bpy.types.Object, weights_by_bone: list[tuple[str, float]]) -> None:
    for group in list(obj.vertex_groups):
        obj.vertex_groups.remove(group)
    groups = [(obj.vertex_groups.new(name=name), weight) for name, weight in weights_by_bone]
    for vertex in obj.data.vertices:
        height = vertex.co.z
        if len(groups) == 3:
            bounds = obj.data.vertices
            low = min(vertex_item.co.z for vertex_item in bounds)
            high = max(vertex_item.co.z for vertex_item in bounds)
            span = high - low
            along = 0.5 if span < 1e-6 else max(0.0, min(1.0, (high - height) / span))
            segment = along * 2.0
            lower = min(1, int(segment))
            blend = segment - lower
            if lower == 1:
                groups[1][0].add([vertex.index], 1.0 - blend, "REPLACE")
                groups[2][0].add([vertex.index], blend, "REPLACE")
            else:
                groups[0][0].add([vertex.index], 1.0 - blend, "REPLACE")
                groups[1][0].add([vertex.index], blend, "REPLACE")
        else:
            groups[0][0].add([vertex.index], 1.0, "REPLACE")


def add_missing_glove_fingers(armature: bpy.types.Object, collection: bpy.types.Collection) -> None:
    templates = {}
    for side in ("L", "R"):
        template = bpy.data.objects.get(f"Player Detail | Glove Finger {side} 1")
        if template is None:
            raise RuntimeError(f"Could not find the existing {side}-hand glove finger mesh to extend.")
        templates[side] = template
        sign = -1.0 if side == "L" else 1.0
        for digit, delta in (
            ("pinky", (-sign * 0.035, 0.0, -0.003)),
            ("thumb", (sign * 0.055, -0.035, 0.018)),
        ):
            finger = template.copy()
            finger.data = template.data.copy()
            finger.name = f"Player Detail | Glove {digit.title()} {side}"
            collection.objects.link(finger)
            finger.parent = armature
            finger.matrix_world = template.matrix_world.copy()
            finger.location += Vector(delta)
            finger["sc_player_part"] = True
            segment_names = [(f"{digit}.{segment:02d}.{side}", 1.0) for segment in (1, 2, 3)]
            replace_vertex_weights(finger, segment_names)


def reweight_existing_parts(armature: bpy.types.Object) -> None:
    for side in ("L", "R"):
        glove = bpy.data.objects.get(f"Glove {side}")
        cuff = bpy.data.objects.get(f"Player Detail | Glove Cuff {side}")
        if glove is None or cuff is None:
            raise RuntimeError(f"The source {side}-hand glove/cuff geometry is incomplete.")
        replace_vertex_weights(glove, [(f"hand.{side}", 1.0)])
        replace_vertex_weights(cuff, [(f"hand.{side}", 1.0)])

        finger_names = [f"Player Detail | Glove Finger {side} {index}" for index in range(3)]
        if any(bpy.data.objects.get(name) is None for name in finger_names):
            raise RuntimeError(f"The source {side}-hand finger geometry is incomplete.")
        digit_order = ("index", "middle", "ring") if side == "L" else ("ring", "middle", "index")
        for name, digit in zip(finger_names, digit_order, strict=True):
            finger = bpy.data.objects[name]
            replace_vertex_weights(finger, [(f"{digit}.{segment:02d}.{side}", 1.0) for segment in (1, 2, 3)])

    for accessory_name in ("Bat Blade", "Bat Handle"):
        accessory = bpy.data.objects.get(accessory_name)
        if accessory is None:
            raise RuntimeError(f"The source batter is missing '{accessory_name}'.")
        replace_vertex_weights(accessory, [("hand.R", 1.0)])


def add_grip_preview(armature: bpy.types.Object) -> bpy.types.Action:
    if "finger-grip-preview" in bpy.data.actions:
        bpy.data.actions.remove(bpy.data.actions["finger-grip-preview"], do_unlink=True)
    action = bpy.data.actions.new("finger-grip-preview")
    action.use_fake_user = True
    animation = armature.animation_data_create()
    previous_action = animation.action
    previous_slot = animation.action_slot if hasattr(animation, "action_slot") else None
    animation.action = action
    if hasattr(action, "slots") and len(action.slots) > 0:
        animation.action_slot = action.slots[0]

    scene = bpy.context.scene
    stance_pose = {
        "spine": (0.055, 0.015, 0.0),
        "head": (-0.025, 0.0, 0.0),
        "upper_arm.L": (0.0, 0.0, -0.04),
        "forearm.L": (0.0, 0.0, 0.0),
        "upper_arm.R": (0.0, 0.0, 0.04),
        "forearm.R": (0.0, 0.0, 0.0),
        "thigh.L": (0.0, 0.0, 0.015),
        "thigh.R": (0.0, 0.0, -0.015),
    }
    for frame, curl in ((1, 0.0), (8, 0.0), (14, 1.0), (22, 1.0), (29, 0.0)):
        scene.frame_set(frame)
        for bone_name, rotation in stance_pose.items():
            pose_bone = armature.pose.bones[bone_name]
            pose_bone.rotation_mode = "XYZ"
            pose_bone.rotation_euler = rotation
            pose_bone.keyframe_insert(data_path="rotation_euler", frame=frame, group=bone_name)
        for side in ("L", "R"):
            wrist = armature.pose.bones[f"hand.{side}"]
            wrist.rotation_mode = "XYZ"
            wrist.rotation_euler = (0.0, 0.0, -0.06 * curl if side == "L" else 0.06 * curl)
            wrist.keyframe_insert(data_path="rotation_euler", frame=frame, group=wrist.name)
            for digit in ("thumb", "index", "middle", "ring", "pinky"):
                for segment in (1, 2, 3):
                    pose_bone = armature.pose.bones[f"{digit}.{segment:02d}.{side}"]
                    pose_bone.rotation_mode = "XYZ"
                    proximal = 0.30 if segment == 1 else (0.52 if segment == 2 else 0.40)
                    if digit == "thumb":
                        proximal *= 0.72
                    pose_bone.rotation_euler = (proximal * curl, 0.0, 0.0)
                    pose_bone.keyframe_insert(data_path="rotation_euler", frame=frame, group=pose_bone.name)

    for curve in getattr(action, "fcurves", []):
        for key in curve.keyframe_points:
            key.interpolation = "BEZIER"

    animation.action = previous_action
    if previous_action is not None and previous_slot is not None and hasattr(animation, "action_slot"):
        animation.action_slot = previous_slot
    scene.frame_set(1)
    return action


def render_pose_previews(
    armature: bpy.types.Object,
    role: str,
    output_dir: Path,
    animation_contract_path: Path,
    grip_action: bpy.types.Action | None = None,
) -> None:
    scene = bpy.context.scene
    output_dir.mkdir(parents=True, exist_ok=True)
    previous_camera = scene.camera
    previous_filepath = scene.render.filepath
    previous_resolution = (scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage)
    previous_format = scene.render.image_settings.file_format
    previous_action = armature.animation_data.action if armature.animation_data else None
    previous_slot = armature.animation_data.action_slot if armature.animation_data and hasattr(armature.animation_data, "action_slot") else None
    temporary_objects = []

    camera_data = bpy.data.cameras.new("Step 63 Preview Camera")
    camera = bpy.data.objects.new("Step 63 Preview Camera", camera_data)
    scene.collection.objects.link(camera)
    camera.location = Vector((2.5, -4.0, 2.15))
    target = Vector((0.0, -0.06, 0.90))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 2.25
    scene.camera = camera
    temporary_objects.append(camera)

    for name, location, energy, size in (
        ("Step 63 Key Light", (1.5, -3.0, 4.0), 500.0, 3.0),
        ("Step 63 Fill Light", (-3.0, -1.5, 2.2), 260.0, 2.5),
    ):
        light_data = bpy.data.lights.new(name, type="AREA")
        light_data.energy = energy
        light_data.shape = "DISK"
        light_data.size = size
        light = bpy.data.objects.new(name, light_data)
        scene.collection.objects.link(light)
        light.location = Vector(location)
        light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()
        temporary_objects.append(light)

    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    animation = armature.animation_data_create()
    if role == "batter":
        if grip_action is None:
            raise RuntimeError("The batter preview requires its finger-grip-preview action.")
        preview_action = grip_action
        poses = ((1, "step68-humanoid-batter-open-grip.png"), (14, "step68-humanoid-batter-closed-grip.png"))
    else:
        preview_action = bpy.data.actions.get("overarm-delivery")
        if preview_action is None:
            raise RuntimeError("The bowler preview requires the overarm-delivery action.")
        animation_contract = load_animation_contract(animation_contract_path)
        release_clip = next(
            (clip for clip in animation_contract.get("animations", []) if clip.get("name") == "overarm-delivery"),
            None,
        )
        release_event = next(
            (event for event in (release_clip or {}).get("events", []) if event.get("name") == "ball-release"),
            None,
        )
        if release_event is None:
            raise RuntimeError("The bowler asset has no ball-release event for its preview frame.")
        release_frame = int(round(preview_action.frame_range[0] + release_event["timeSeconds"] * scene.render.fps))
        poses = ((release_frame, "step68-humanoid-bowler-release.png"),)

    animation.action = preview_action
    if hasattr(preview_action, "slots") and len(preview_action.slots) > 0:
        animation.action_slot = preview_action.slots[0]

    for frame, filename in poses:
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        scene.render.filepath = str((output_dir / filename).resolve())
        bpy.ops.render.render(write_still=True)

    animation.action = previous_action
    if previous_action is not None and previous_slot is not None and hasattr(animation, "action_slot"):
        animation.action_slot = previous_slot
    scene.frame_set(1)
    scene.camera = previous_camera
    for obj in temporary_objects:
        data = obj.data
        bpy.data.objects.remove(obj, do_unlink=True)
        if data.users == 0:
            if isinstance(data, bpy.types.Camera):
                bpy.data.cameras.remove(data)
            elif isinstance(data, bpy.types.Light):
                bpy.data.lights.remove(data)
    scene.render.filepath = previous_filepath
    scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = previous_resolution
    scene.render.image_settings.file_format = previous_format


def sync_gltf_material_colors() -> None:
    """Carry source diffuse colors into unlinked default Principled shaders."""
    default_shader_color = (0.8, 0.8, 0.8, 1.0)
    for material in bpy.data.materials:
        if material.node_tree is None:
            continue
        principled = next((node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
        if principled is None:
            continue
        base_color = principled.inputs.get("Base Color")
        if base_color is None or base_color.is_linked:
            continue
        shader_color = tuple(float(value) for value in base_color.default_value)
        diffuse_color = tuple(float(value) for value in material.diffuse_color)
        if all(abs(value - default) <= 1e-5 for value, default in zip(shader_color, default_shader_color)):
            base_color.default_value = diffuse_color


def main() -> None:
    args = parse_args()
    defaults = ASSET_PATHS[args.role]
    source = (args.source_blend or defaults["source"]).resolve()
    animation_contract_path = (args.animation_contract or defaults["contract"]).resolve()
    blend_output = (args.blend_output or defaults["blend"]).resolve()
    glb_output = (args.glb_output or defaults["glb"]).resolve()
    if not source.is_file():
        raise FileNotFoundError(f"Practice {args.role} source not found: {source}")
    if not animation_contract_path.is_file():
        raise FileNotFoundError(f"Practice {args.role} animation contract not found: {animation_contract_path}")

    bpy.ops.wm.open_mainfile(filepath=str(source))
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(f"Expected one source {args.role} armature, found {len(armatures)}.")
    armature = armatures[0]
    old_bones = {bone.name: bone for bone in armature.data.bones}
    required_old_bones = {
        "root", "spine", "head", "upper_arm.L", "forearm.L", "upper_arm.R", "forearm.R",
        "thigh.L", "shin.L", "foot.L", "thigh.R", "shin.R", "foot.R",
    }
    if set(old_bones) != required_old_bones:
        raise RuntimeError(f"Expected the untouched 13-bone practice rig; found {len(old_bones)} bones.")
    required_actions = {"practice-stance", "defensive-block", "front-foot-drive", "lofted-drive", "back-foot-drive", "back-foot-loft"} if args.role == "batter" else {
        "practice-stance", "bowling-run-up", "overarm-delivery"
    }
    if not required_actions.issubset({action.name for action in bpy.data.actions}):
        raise RuntimeError(f"The source {args.role} is missing required actions: {', '.join(sorted(required_actions))}.")

    specs = build_bone_specs(old_bones)
    armature_data = bpy.data.armatures.new(f"Practice {args.role.title()} Humanoid Rig 61")
    armature.data = armature_data
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    edit_bones = armature.data.edit_bones
    bones_by_name = {}
    for name, parent_name, head, tail, roll in specs:
        bone = edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        bone.roll = roll
        bone.use_deform = True
        if parent_name is not None:
            bone.parent = bones_by_name[parent_name]
        bone.use_connect = False
        bones_by_name[name] = bone
    bpy.ops.object.mode_set(mode="OBJECT")
    armature.show_in_front = True
    armature.data.display_type = "STICK"

    mesh_collection = bpy.data.collections.get("Player Mesh")
    if mesh_collection is None:
        raise RuntimeError("The source scene is missing its 'Player Mesh' collection.")
    if args.role == "batter":
        reweight_existing_parts(armature)
        add_missing_glove_fingers(armature, mesh_collection)
    actions = [bpy.data.actions[name] for name in sorted(bpy.data.actions.keys())]
    for action in actions:
        action.use_fake_user = True
    grip_action = add_grip_preview(armature) if args.role == "batter" else None
    if grip_action is not None:
        actions.append(grip_action)
    for pose_bone in armature.pose.bones:
        pose_bone.rotation_mode = "XYZ"

    sync_gltf_material_colors()
    scene = bpy.context.scene
    scene.render.fps = 30
    scene.frame_set(1)
    bpy.context.view_layer.update()
    render_pose_previews(armature, args.role, args.preview_dir.resolve(), animation_contract_path, grip_action)
    blend_output.parent.mkdir(parents=True, exist_ok=True)
    glb_output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_output))
    result = bpy.ops.export_scene.gltf(
        filepath=str(glb_output),
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

    animation_count = embed_player_metadata(glb_output, animation_contract_path, glb_output)

    print(f"Standard humanoid {args.role} source: {blend_output}")
    print(f"Exported GLB: {glb_output} ({glb_output.stat().st_size} bytes)")
    print(f"Rig: {armature.name}, {len(armature.data.bones)} bones")
    print(f"Meshes: {sum(obj.type == 'MESH' and obj.get('sc_player_part') for obj in mesh_collection.objects)}")
    print(f"Actions: {', '.join(action.name for action in sorted(actions, key=lambda item: item.name))}")
    print(f"Embedded gameplay metadata in {animation_count} GLB animations from {animation_contract_path}")


if __name__ == "__main__":
    main()
