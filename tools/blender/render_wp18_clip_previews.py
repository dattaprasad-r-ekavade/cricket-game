"""Render a small review set from the WP18 humanoid animation pack."""

from __future__ import annotations

import argparse
from pathlib import Path
import sys

import bpy
from mathutils import Vector


def parse_args() -> argparse.Namespace:
    forwarded = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--blend", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--take", action="append", default=[], help="Animation name and Blender frame, e.g. umpire-out:20")
    return parser.parse_args(forwarded)


def setup_camera_and_lights(scene: bpy.types.Scene) -> list[bpy.types.Object]:
    target = Vector((0.0, -0.06, 0.90))
    created = []
    camera_data = bpy.data.cameras.new("WP18 Preview Camera")
    camera = bpy.data.objects.new("WP18 Preview Camera", camera_data)
    scene.collection.objects.link(camera)
    camera.location = Vector((2.5, -4.0, 2.15))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 2.25
    scene.camera = camera
    created.append(camera)

    for name, location, energy, size in (
        ("WP18 Key Light", (1.5, -3.0, 4.0), 500.0, 3.0),
        ("WP18 Fill Light", (-3.0, -1.5, 2.2), 260.0, 2.5),
    ):
        light_data = bpy.data.lights.new(name, type="AREA")
        light_data.energy = energy
        light_data.shape = "DISK"
        light_data.size = size
        light = bpy.data.objects.new(name, light_data)
        scene.collection.objects.link(light)
        light.location = Vector(location)
        light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()
        created.append(light)
    return created


def main() -> None:
    args = parse_args()
    bpy.ops.wm.open_mainfile(filepath=str(args.blend.resolve()))
    armature = next((obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"), None)
    if armature is None:
        raise RuntimeError("The Blender scene has no player armature.")
    animation = armature.animation_data_create()
    scene = bpy.context.scene
    scene.render.fps = 30
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    engine_values = bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items
    scene.render.engine = next(item.identifier for item in engine_values if "EEVEE" in item.identifier)
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "Medium High Contrast"
    scene.camera = None
    setup_camera_and_lights(scene)
    args.output_dir.mkdir(parents=True, exist_ok=True)

    takes = args.take or ["practice-stance:1"]
    for take in takes:
        name, frame_text = take.rsplit(":", 1)
        action = bpy.data.actions.get(name)
        if action is None:
            raise RuntimeError(f"Animation '{name}' is not in the Blender file.")
        animation.action = action
        if hasattr(animation, "action_slot") and action.slots:
            animation.action_slot = action.slots[0]
        frame = int(frame_text)
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        scene.render.filepath = str((args.output_dir / f"{name}-f{frame:02d}.png").resolve())
        result = bpy.ops.render.render(write_still=True)
        if "FINISHED" not in result:
            raise RuntimeError(f"Render failed for {name} at frame {frame}.")
        print(f"Rendered {name} frame {frame}: {scene.render.filepath}")


if __name__ == "__main__":
    main()
