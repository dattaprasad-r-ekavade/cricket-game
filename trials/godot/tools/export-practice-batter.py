"""Export the authored practice batter and all Blender actions as one Godot-ready GLB."""

from pathlib import Path
import bpy


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
SOURCE_BLEND = REPOSITORY_ROOT / "assets" / "characters" / "practice-batter-humanoid.blend"
OUTPUT_GLB = REPOSITORY_ROOT / "trials" / "godot" / "assets" / "practice-batter.glb"
EXPECTED_BONE_COUNT = 61


if not SOURCE_BLEND.is_file():
    raise FileNotFoundError(f"Practice batter source not found: {SOURCE_BLEND}")

bpy.ops.wm.open_mainfile(filepath=str(SOURCE_BLEND))
armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
if len(armatures) != 1:
    raise RuntimeError(f"Expected one batter armature, found {len(armatures)}.")
if len(armatures[0].data.bones) != EXPECTED_BONE_COUNT:
    raise RuntimeError(f"Expected the standard {EXPECTED_BONE_COUNT}-bone humanoid rig, found {len(armatures[0].data.bones)}.")

action_names = sorted(action.name for action in bpy.data.actions)
required_actions = {"practice-stance", "front-foot-drive", "finger-grip-preview"}
if not required_actions.issubset(action_names):
    raise RuntimeError("The source batter must include its stance, drive, and finger-grip preview actions.")

OUTPUT_GLB.parent.mkdir(parents=True, exist_ok=True)
result = bpy.ops.export_scene.gltf(
    filepath=str(OUTPUT_GLB),
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

print(f"Exported {OUTPUT_GLB}")
print(f"Rig: {armatures[0].name}, {len(armatures[0].data.bones)} bones")
print(f"Animations: {', '.join(action_names)}")
print(f"Size: {OUTPUT_GLB.stat().st_size} bytes")
