"""Add missing shin silhouettes to the open Super Cricket player rig."""

from __future__ import annotations

import math

import bpy


RING_COUNT = 10
RINGS = [
    (0.065, 0.060, 0.078),
    (0.095, 0.072, 0.096),
    (0.150, 0.078, 0.105),
    (0.310, 0.080, 0.108),
    (0.445, 0.090, 0.128),
    (0.505, 0.088, 0.125),
    (0.535, 0.075, 0.108),
]


def create_lower_leg(armature: bpy.types.Object, collection: bpy.types.Collection, side: str) -> bpy.types.Object:
    object_name = f"Lower Leg {side}"
    existing = bpy.data.objects.get(object_name)
    if existing is not None:
        if not existing.get("sc_player_part"):
            raise RuntimeError(f"Existing object '{object_name}' is not marked for player export.")
        return existing

    center_x = -0.14 if side == "L" else 0.14
    vertices = []
    for height, radius_x, radius_y in RINGS:
        for segment in range(RING_COUNT):
            angle = math.tau * segment / RING_COUNT
            vertices.append((
                center_x + math.cos(angle) * radius_x,
                -0.015 + math.sin(angle) * radius_y,
                height,
            ))

    faces = [(tuple(reversed(range(RING_COUNT))))]
    for ring in range(len(RINGS) - 1):
        first = ring * RING_COUNT
        second = first + RING_COUNT
        for segment in range(RING_COUNT):
            next_segment = (segment + 1) % RING_COUNT
            faces.append((first + segment, first + next_segment, second + next_segment, second + segment))
    top = (len(RINGS) - 1) * RING_COUNT
    faces.append(tuple(top + segment for segment in range(RING_COUNT)))

    mesh = bpy.data.meshes.new(f"{object_name} Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(bpy.data.materials["trouser"])
    mesh.update()
    for polygon in mesh.polygons:
        polygon.use_smooth = True

    leg = bpy.data.objects.new(object_name, mesh)
    collection.objects.link(leg)
    leg.parent = armature
    leg["sc_player_part"] = True
    group = leg.vertex_groups.new(name=f"shin.{side}")
    group.add(list(range(len(vertices))), 1.0, "REPLACE")
    modifier = leg.modifiers.new("Armature", "ARMATURE")
    modifier.object = armature
    return leg


def add_fielder_lower_legs() -> None:
    armature = next((obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"), None)
    collection = bpy.data.collections.get("Player Mesh")
    if armature is None or collection is None:
        raise RuntimeError("Open a Super Cricket player scene with an armature and Player Mesh collection.")
    if "trouser" not in bpy.data.materials:
        raise RuntimeError("The player scene has no trouser material.")
    for side in ("L", "R"):
        leg = create_lower_leg(armature, collection, side)
        print(f"Ready: {leg.name} ({len(leg.data.vertices)} vertices, shin.{side} weighted)")
    if not bpy.data.filepath:
        raise RuntimeError("Save the player rig once before adding the lower-leg assets.")
    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
    print(f"Saved Blender source: {bpy.data.filepath}")


add_fielder_lower_legs()
