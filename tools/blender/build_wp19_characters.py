#!/usr/bin/env python3
"""Build three CC0 MakeHuman player bodies on the Super Cricket 61-bone rig.

Run with Blender's bundled Python:
  blender --background --factory-startup --python tools/blender/build_wp19_characters.py -- \
    --mpfb-root <extracted MPFB2 extension directory> \
    --source-blend assets/characters/practice-batter-humanoid.blend \
    --output-dir assets/characters/wp19

The MPFB2 add-on is imported in-process and its extension/user paths are redirected
to the temporary location supplied on the command line. It is not installed or
copied into this repository.
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import math
import os
import struct
import sys
import zlib
from pathlib import Path

import bpy
from mathutils import Vector


PROFILES = [
    {
        "id": "agile",
        "label": "Agile",
        "height": 0.57,
        "weight": 0.34,
        "muscle": 0.60,
        "proportions": 0.36,
        "age": 0.38,
    },
    {
        "id": "balanced",
        "label": "Balanced",
        "height": 0.50,
        "weight": 0.50,
        "muscle": 0.52,
        "proportions": 0.50,
        "age": 0.44,
    },
    {
        "id": "power",
        "label": "Power",
        "height": 0.60,
        "weight": 0.70,
        "muscle": 0.68,
        "proportions": 0.58,
        "age": 0.46,
    },
]

TEAM_RGB = (0.025, 0.30, 0.235)
TRIM_RGB = (0.88, 0.91, 0.86)
MASK_RES = 128


def parse_args():
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mpfb-root", type=Path, required=True)
    parser.add_argument("--source-blend", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    return parser.parse_args(raw)


def load_mpfb(root: Path):
    root = root.resolve()
    init_path = root / "__init__.py"
    if not init_path.is_file():
        raise FileNotFoundError(f"MPFB2 extension root does not contain __init__.py: {root}")
    spec = importlib.util.spec_from_file_location(
        "mpfb", init_path, submodule_search_locations=[str(root)]
    )
    module = importlib.util.module_from_spec(spec)
    sys.modules["mpfb"] = module
    spec.loader.exec_module(module)
    module.MPFB_CONTEXTUAL_INFORMATION = {
        "__package__": "bl_ext.blender_org.mpfb",
        "__package_short__": "mpfb",
        "__file__": str(init_path),
    }
    temp_user_root = Path(os.environ.get("TEMP", str(root.parent))) / "mpfb-wp19-user"
    bpy.utils.extension_path_user = lambda _package: str(temp_user_root)
    # The add-on is not registered in Blender preferences in this headless run.
    # These preferences only affect optional panel labels and user storage paths.
    module.get_preference = lambda _name: None
    from mpfb.services.humanservice import HumanService
    from mpfb.services.targetservice import TargetService

    return HumanService, TargetService


def clear_selection():
    for obj in bpy.context.selected_objects:
        obj.select_set(False)


def new_collection(name: str, parent=None):
    collection = bpy.data.collections.new(name)
    (parent or bpy.context.scene.collection).children.link(collection)
    return collection


def write_png(path: Path, width: int, height: int, color_type: int, rows: list[bytes]):
    channels = 1 if color_type == 0 else 3
    if len(rows) != height or any(len(row) != width * channels for row in rows):
        raise ValueError(f"Invalid PNG pixel rows for {path}")

    def chunk(tag: bytes, payload: bytes):
        return struct.pack(">I", len(payload)) + tag + payload + struct.pack(">I", zlib.crc32(tag + payload) & 0xFFFFFFFF)

    raw = b"".join(b"\x00" + row for row in rows)
    data = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">2I5B", width, height, 8, color_type, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(raw, 9))
        + chunk(b"IEND", b"")
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def set_material(name: str, color, image=None):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = (*color, 1.0)
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = next((n for n in nodes if n.type == "BSDF_PRINCIPLED"), None)
    if principled is None:
        principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.inputs["Base Color"].default_value = (*color, 1.0)
    principled.inputs["Roughness"].default_value = 0.78
    if image is not None:
        texture = nodes.new("ShaderNodeTexImage")
        texture.image = image
        texture.interpolation = "Linear"
        links.new(texture.outputs["Color"], principled.inputs["Base Color"])
    return material


def write_team_textures(out_dir: Path):
    texture_dir = out_dir / "textures"
    texture_dir.mkdir(parents=True, exist_ok=True)
    records = {}
    for garment in ("jersey", "trouser"):
        mask_rows = []
        base_rows = []
        for y in range(MASK_RES):
            v = y / (MASK_RES - 1)
            mask_row = bytearray()
            base_row = bytearray()
            for x in range(MASK_RES):
                u = x / (MASK_RES - 1)
                if garment == "jersey":
                    trim = v > 0.955
                else:
                    trim = (u < 0.035 or u > 0.965 or v > 0.965)
                mask_row.append(0 if trim else 255)
                color = tuple(round(value * 255) for value in (TRIM_RGB if trim else TEAM_RGB))
                base_row.extend(color)
            mask_rows.append(bytes(mask_row))
            base_rows.append(bytes(base_row))
        write_png(texture_dir / f"{garment}-team-mask.png", MASK_RES, MASK_RES, 0, mask_rows)
        write_png(texture_dir / f"{garment}-base-color.png", MASK_RES, MASK_RES, 2, base_rows)
        records[garment] = {
            "mask": f"textures/{garment}-team-mask.png",
            "base_color": f"textures/{garment}-base-color.png",
        }
    return records


def vertex_group_weights(obj, vertex_index):
    return [(obj.vertex_groups[item.group].name, item.weight) for item in obj.data.vertices[vertex_index].groups]


def weight_for_group(obj, vertex_index, group_name):
    group = obj.vertex_groups.get(group_name)
    if group is None:
        return 0.0
    for item in obj.data.vertices[vertex_index].groups:
        if item.group == group.index:
            return item.weight
    return 0.0


def average_bone_weight(obj, polygon, bone_names):
    return sum(
        weight_for_group(obj, vertex_index, bone_name)
        for vertex_index in polygon.vertices
        for bone_name in bone_names
    ) / max(1, len(polygon.vertices))


def create_garment(body, rig, collection, name, face_predicate, image, mask_path, output_dir):
    source = body.data
    source.update()
    accepted_faces = []
    used_vertices = set()
    for poly in source.polygons:
        xyz = [source.vertices[index].co for index in poly.vertices]
        center = Vector((0.0, 0.0, 0.0))
        for point in xyz:
            center += point
        center /= len(xyz)
        if weight_for_group(body, poly.vertices[0], "body") <= 0.5:
            continue
        if face_predicate(center, poly):
            accepted_faces.append(tuple(poly.vertices))
            used_vertices.update(poly.vertices)
    if not accepted_faces:
        raise RuntimeError(f"No source faces selected for {name}")
    source_indices = sorted(used_vertices)
    remap = {old: new for new, old in enumerate(source_indices)}
    coords = []
    for index in source_indices:
        vertex = source.vertices[index]
        coords.append(vertex.co + vertex.normal * 0.006)
    faces = [tuple(remap[index] for index in face) for face in accepted_faces]
    mesh = bpy.data.meshes.new(name + " Mesh")
    mesh.from_pydata(coords, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = rig
    obj.matrix_parent_inverse = rig.matrix_world.inverted()
    obj.location = (0.0, 0.0, 0.0)
    for group in body.vertex_groups:
        new_group = obj.vertex_groups.new(name=group.name)
        for new_index, old_index in enumerate(source_indices):
            weight = weight_for_group(body, old_index, group.name)
            if weight > 0.0001:
                new_group.add([new_index], weight, "REPLACE")
    uv_layer = mesh.uv_layers.new(name="TeamMaskUV")
    z_values = [point.z for point in coords]
    x_values = [point.x for point in coords]
    z_min, z_max = min(z_values), max(z_values)
    z_span = max(0.001, z_max - z_min)
    x_min, x_max = min(x_values), max(x_values)
    x_span = max(0.001, x_max - x_min)
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            point = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            # A front/back orthographic unwrap keeps sleeve masks symmetric.
            u = (point.x - x_min) / x_span
            v = (point.z - z_min) / z_span
            uv_layer.data[loop_index].uv = (u, v)
    material = set_material(name + " Material", (0.05, 0.3, 0.23), image)
    mesh.materials.append(material)
    armature = obj.modifiers.new("WP19 61-bone skin", "ARMATURE")
    armature.object = rig
    obj["sc_player_part"] = name.lower()
    obj["sc_garment_face_count"] = len(faces)
    obj["sc_garment_vertex_count"] = len(coords)
    obj["sc_team_color_mask"] = mask_path
    obj["sc_variant_body_source"] = "MPFB2 MakeHuman CC0 base"
    return obj


def gear_source_names():
    exact = {
        "Pad L", "Pad R", "Glove L", "Glove R", "Bat Blade", "Bat Handle",
        "Shoe L", "Shoe R", "Helmet Shell", "Helmet Peak", "Helmet Grille",
        "Grille Side L", "Grille Side R",
    }
    prefixes = (
        "Player Detail | Pad ", "Player Detail | Glove ", "Player Detail | Helmet ",
        "Player Detail | Bat ", "Player Detail | Shoe ",
    )
    return exact, prefixes


def duplicate_source_mesh(source_obj, rig, collection, name_suffix=""):
    obj = source_obj.copy()
    if source_obj.data:
        obj.data = source_obj.data.copy()
    collection.objects.link(obj)
    world = source_obj.matrix_world.copy()
    obj.name = source_obj.name + name_suffix
    obj.parent = rig
    obj.matrix_parent_inverse = rig.matrix_world.inverted()
    obj.matrix_world = world
    for modifier in obj.modifiers:
        if modifier.type == "ARMATURE":
            modifier.object = rig
    if not any(mod.type == "ARMATURE" for mod in obj.modifiers):
        modifier = obj.modifiers.new("WP19 61-bone skin", "ARMATURE")
        modifier.object = rig
    obj["sc_lod_source"] = source_obj.name
    return obj


def add_armature_modifier(obj, rig):
    for modifier in list(obj.modifiers):
        obj.modifiers.remove(modifier)
    modifier = obj.modifiers.new("WP19 61-bone skin", "ARMATURE")
    modifier.object = rig


def duplicate_lod_object(source_obj, rig, collection, lod, simplify_ratio):
    obj = source_obj.copy()
    obj.data = source_obj.data.copy()
    collection.objects.link(obj)
    obj.name = source_obj.name.rsplit(" LOD", 1)[0] + f" LOD{lod}"
    obj.parent = rig
    obj.matrix_parent_inverse = rig.matrix_world.inverted()
    obj.matrix_world = source_obj.matrix_world.copy()
    if source_obj.name.startswith("WP19 Body"):
        for modifier in list(obj.modifiers):
            obj.modifiers.remove(modifier)
        decimate = obj.modifiers.new("LOD reduction", "DECIMATE")
        decimate.ratio = simplify_ratio
        armature = obj.modifiers.new("WP19 61-bone skin", "ARMATURE")
        armature.object = rig
    elif source_obj.name.startswith("WP19 Jersey") or source_obj.name.startswith("WP19 Trouser"):
        add_armature_modifier(obj, rig)
        decimate = obj.modifiers.new("LOD reduction", "DECIMATE")
        decimate.ratio = simplify_ratio
        # Keep reduction before deformation so the exported skin weights remain stable.
        obj.modifiers.move(len(obj.modifiers) - 1, 0)
    else:
        add_armature_modifier(obj, rig)
    obj["sc_lod"] = lod
    obj["sc_character_variant"] = rig.get("sc_character_variant", "")
    return obj


def new_rig(source_rig, name, collection):
    rig = source_rig.copy()
    rig.data = source_rig.data.copy()
    collection.objects.link(rig)
    rig.name = name
    rig.data.name = name + " Skeleton"
    rig.location = (0.0, 0.0, 0.0)
    rig["sc_skeleton_bones"] = len(rig.data.bones)
    rig["sc_skeleton_id"] = "super-cricket-humanoid-61"
    return rig


def assign_body_skin(body, rig):
    macro_values = None
    if body.data.shape_keys:
        macro_values = [key.value for key in body.data.shape_keys.key_blocks]
        for key in body.data.shape_keys.key_blocks[1:]:
            key.value = 0.0
        bpy.context.view_layer.update()
    clear_selection()
    body.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    if macro_values is not None:
        for key, value in zip(body.data.shape_keys.key_blocks, macro_values):
            key.value = value
        bpy.context.view_layer.update()
    if len(rig.data.bones) != 61:
        raise RuntimeError(f"Expected 61 rig bones, got {len(rig.data.bones)}")
    bone_names = {bone.name for bone in rig.data.bones}
    body_group = body.vertex_groups.get("body")
    unweighted = sum(
        1 for vertex in body.data.vertices
        if (body_group is None or weight_for_group(body, vertex.index, "body") > 0.5)
        and not any(body.vertex_groups[item.group].name in bone_names for item in vertex.groups)
    )
    if unweighted:
        raise RuntimeError(f"Auto-weighting left {unweighted} body vertices without rig weights")
    # glTF skinning and common game runtimes use at most four influences per vertex.
    removable = [[] for _ in body.vertex_groups]
    for vertex in body.data.vertices:
        ranked = sorted(
            (item for item in vertex.groups if body.vertex_groups[item.group].name in bone_names),
            key=lambda item: item.weight,
            reverse=True,
        )
        for membership in ranked[4:]:
            removable[membership.group].append(vertex.index)
    for group_index, indices in enumerate(removable):
        if indices:
            body.vertex_groups[group_index].remove(indices)


def bake_mpfb_shape_keys(body):
    """Bake MPFB macro keys and strip helper geometry without losing skin weights."""
    if not body.data.shape_keys:
        raise RuntimeError("MPFB did not create macro shape keys")
    original_mesh = body.data
    for modifier in body.modifiers:
        modifier.show_viewport = False
        modifier.show_render = False
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = body.evaluated_get(depsgraph)
    baked_mesh = bpy.data.meshes.new_from_object(
        evaluated,
        preserve_all_data_layers=True,
        depsgraph=depsgraph,
    )
    if baked_mesh is None or len(baked_mesh.vertices) != len(body.data.vertices):
        raise RuntimeError("Could not bake MPFB macro shape keys while preserving mesh topology")
    body_group = body.vertex_groups.get("body")
    if body_group is None:
        raise RuntimeError("MPFB body mesh has no helper mask vertex group")
    kept_polygon_indices = []
    used_vertices = set()
    for polygon in baked_mesh.polygons:
        body_weight = sum(
            weight_for_group(body, vertex_index, "body") for vertex_index in polygon.vertices
        ) / max(1, len(polygon.vertices))
        if body_weight > 0.5:
            kept_polygon_indices.append(polygon.index)
            used_vertices.update(polygon.vertices)
    if not kept_polygon_indices:
        raise RuntimeError("Could not find visible body faces while stripping MPFB helpers")
    source_indices = sorted(used_vertices)
    remap = {old: new for new, old in enumerate(source_indices)}
    clean_mesh = bpy.data.meshes.new(original_mesh.name + " Visible Body")
    clean_mesh.from_pydata(
        [baked_mesh.vertices[index].co.copy() for index in source_indices],
        [],
        [tuple(remap[index] for index in baked_mesh.polygons[poly_index].vertices) for poly_index in kept_polygon_indices],
    )
    for material in baked_mesh.materials:
        clean_mesh.materials.append(material)
    clean_mesh.update()
    for new_polygon, old_polygon_index in zip(clean_mesh.polygons, kept_polygon_indices):
        old_polygon = baked_mesh.polygons[old_polygon_index]
        new_polygon.material_index = old_polygon.material_index
        new_polygon.use_smooth = old_polygon.use_smooth
    for source_layer in baked_mesh.uv_layers:
        target_layer = clean_mesh.uv_layers.new(name=source_layer.name)
        for new_polygon, old_polygon_index in zip(clean_mesh.polygons, kept_polygon_indices):
            old_polygon = baked_mesh.polygons[old_polygon_index]
            for new_loop, old_loop in zip(new_polygon.loop_indices, old_polygon.loop_indices):
                target_layer.data[new_loop].uv = source_layer.data[old_loop].uv

    group_names = [group.name for group in body.vertex_groups]
    group_weights = [[] for _ in group_names]
    for new_index, old_index in enumerate(source_indices):
        for membership in original_mesh.vertices[old_index].groups:
            group_weights[membership.group].append((new_index, membership.weight))
    body.data = clean_mesh
    for group_name in group_names:
        body.vertex_groups.new(name=group_name)
    for group_index, memberships in enumerate(group_weights):
        group = body.vertex_groups[group_index]
        for vertex_index, weight in memberships:
            if weight > 0.0001:
                group.add([vertex_index], weight, "REPLACE")
    for modifier in list(body.modifiers):
        if modifier.type != "ARMATURE":
            body.modifiers.remove(modifier)
        else:
            modifier.show_viewport = True
            modifier.show_render = True
    bpy.data.meshes.remove(original_mesh)
    bpy.data.meshes.remove(baked_mesh)
    bone_names = {modifier.object.data.bones[index].name for modifier in body.modifiers if modifier.type == "ARMATURE" for index in range(len(modifier.object.data.bones))}
    unweighted_visible = sum(
        1 for vertex in body.data.vertices
        if not any(body.vertex_groups[item.group].name in bone_names for item in vertex.groups)
    )
    if unweighted_visible:
        raise RuntimeError(f"Baked visible body has {unweighted_visible} vertices without rig weights")


def make_variant(profile, source_rig, gear_sources, image_records, out_dir, texture_images, HumanService, TargetService):
    variant_id = profile["id"]
    lod0 = new_collection(f"WP19 {variant_id} LOD0")
    rig = new_rig(source_rig, f"WP19 {variant_id} Rig LOD0", lod0)
    rig["sc_character_variant"] = variant_id
    macro = TargetService.get_default_macro_info_dict()
    for key in ("gender", "age", "muscle", "weight", "height", "proportions"):
        if key in profile:
            macro[key] = profile[key]
    body = HumanService.create_human(
        mask_helpers=True,
        detailed_helpers=False,
        extra_vertex_groups=True,
        feet_on_ground=True,
        scale=0.1,
        macro_detail_dict=macro,
    )
    body.name = f"WP19 Body {profile['label']} LOD0"
    body.data.name = body.name + " Mesh"
    body["sc_player_part"] = "body"
    body["sc_character_variant"] = variant_id
    skin = set_material("WP19 MakeHuman skin", (0.48, 0.25, 0.16))
    body.data.materials.clear()
    body.data.materials.append(skin)
    # Move the newly generated body out of its temporary collection and into LOD0.
    for collection in list(body.users_collection):
        collection.objects.unlink(body)
    lod0.objects.link(body)
    assign_body_skin(body, rig)
    bake_mpfb_shape_keys(body)

    jersey = create_garment(
        body, rig, lod0, f"WP19 Jersey {profile['label']} LOD0",
        lambda _p, poly: average_bone_weight(body, poly, (
            "spine.01", "spine.02", "spine", "chest", "clavicle.L", "clavicle.R", "upper_arm.L", "upper_arm.R"
        )) >= 0.45,
        texture_images["jersey_base"], image_records["jersey"]["mask"], out_dir,
    )
    trouser = create_garment(
        body, rig, lod0, f"WP19 Trouser {profile['label']} LOD0",
        lambda _p, poly: average_bone_weight(body, poly, (
            "pelvis", "thigh.L", "thigh.R", "shin.L", "shin.R", "foot.L", "foot.R"
        )) >= 0.45,
        texture_images["trouser_base"], image_records["trouser"]["mask"], out_dir,
    )
    for source_obj in gear_sources:
        duplicate_source_mesh(source_obj, rig, lod0, f" {variant_id} LOD0")

    lod_records = []
    lod_collections = {0: lod0}
    lod_rigs = {0: rig}
    simplification = {1: 0.20, 2: 0.06}
    for lod in (1, 2):
        collection = new_collection(f"WP19 {variant_id} LOD{lod}")
        new_rig_obj = new_rig(rig, f"WP19 {variant_id} Rig LOD{lod}", collection)
        new_rig_obj["sc_character_variant"] = variant_id
        lod_rigs[lod] = new_rig_obj
        for obj in list(lod0.objects):
            if obj == rig:
                continue
            duplicate_lod_object(obj, new_rig_obj, collection, lod, simplification[lod])
        lod_collections[lod] = collection

    for lod, collection in lod_collections.items():
        mesh_objects = [obj for obj in collection.objects if obj.type == "MESH"]
        lod_records.append({
            "lod": lod,
            "collection": collection.name,
            "mesh_objects": len(mesh_objects),
            "rig_bones": len(lod_rigs[lod].data.bones),
        })
    bounds = [
        [round(min(vertex.co[axis] for vertex in body.data.vertices), 4), round(max(vertex.co[axis] for vertex in body.data.vertices), 4)]
        for axis in range(3)
    ]
    return {
        "id": variant_id,
        "label": profile["label"],
        "macro_profile": {k: profile[k] for k in ("height", "weight", "muscle", "proportions", "age")},
        "body_vertex_count": len(body.data.vertices),
        "body_face_count": len(body.data.polygons),
        "body_bounds_xyz": bounds,
        "weighted_body_vertices": sum(bool(vertex.groups) for vertex in body.data.vertices),
        "jersey_vertex_count": jersey.get("sc_garment_vertex_count"),
        "jersey_face_count": jersey.get("sc_garment_face_count"),
        "trouser_vertex_count": trouser.get("sc_garment_vertex_count"),
        "trouser_face_count": trouser.get("sc_garment_face_count"),
        "team_mask_files": image_records,
        "lods": lod_records,
        "collections": lod_collections,
        "rigs": lod_rigs,
        "objects": {"body": body, "jersey": jersey, "trouser": trouser},
    }


def setup_render(preview_path: Path, variants):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1050
    scene.render.resolution_y = 700
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.world.color = (0.10, 0.12, 0.16)
    scene.view_settings.view_transform = "AgX"
    for obj in bpy.data.objects:
        if obj.type in {"MESH", "ARMATURE"}:
            obj.hide_render = True
    for index, variant in enumerate(variants):
        rig = variant["rigs"][0]
        rig.hide_render = False
        rig.location.x = (index - 1) * 1.55
        for obj in variant["collections"][0].objects:
            obj.hide_render = False
    # The object copies are parented to their rig; render all three with one key/fill setup.
    def aim(obj, target):
        direction = Vector(target) - obj.location
        obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera_data = bpy.data.cameras.new("WP19 Preview Camera")
    camera = bpy.data.objects.new("WP19 Preview Camera", camera_data)
    scene.collection.objects.link(camera)
    camera.location = (0.0, -5.8, 2.15)
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 5.2
    aim(camera, (0.0, 0.0, 0.9))
    scene.camera = camera
    for name, location, power, size in (
        ("WP19 Key", (-3.0, -4.0, 5.0), 650.0, 4.0),
        ("WP19 Fill", (3.5, -2.5, 3.3), 400.0, 3.5),
        ("WP19 Rim", (0.0, 2.5, 4.0), 750.0, 3.0),
    ):
        light_data = bpy.data.lights.new(name, "AREA")
        light_data.energy = power
        light_data.shape = "DISK"
        light_data.size = size
        light = bpy.data.objects.new(name, light_data)
        scene.collection.objects.link(light)
        light.location = location
        aim(light, (0.0, 0.0, 0.95))
    floor_mesh = bpy.data.meshes.new("WP19 Preview Floor Mesh")
    floor_mesh.from_pydata([(-10, -10, -0.035), (10, -10, -0.035), (10, 10, -0.035), (-10, 10, -0.035)], [], [(0, 1, 2, 3)])
    floor = bpy.data.objects.new("WP19 Preview Floor", floor_mesh)
    scene.collection.objects.link(floor)
    floor.data.materials.append(set_material("WP19 Preview Ground", (0.055, 0.075, 0.10)))
    floor.hide_viewport = True
    scene.render.filepath = str(preview_path)
    scene.frame_set(1)
    bpy.context.view_layer.update()
    scene.render.image_settings.color_mode = "RGBA"
    bpy.ops.render.render(write_still=True)
    for variant in variants:
        variant["rigs"][0].location.x = 0.0
    for obj in bpy.data.objects:
        if obj.name.startswith("WP19 Preview") or obj.name.startswith("WP19 Key") or obj.name.startswith("WP19 Fill") or obj.name.startswith("WP19 Rim"):
            obj.hide_render = True


def export_lod_glbs(out_dir: Path, variants):
    for variant in variants:
        for lod, collection in variant["collections"].items():
            rig = variant["rigs"][lod]
            clear_selection()
            for obj in collection.objects:
                obj.select_set(True)
            bpy.context.view_layer.objects.active = rig
            filepath = out_dir / f"{variant['id']}-lod{lod}.glb"
            result = bpy.ops.export_scene.gltf(
                filepath=str(filepath),
                export_format="GLB",
                use_selection=True,
                export_skins=True,
                export_animations=False,
                export_yup=True,
                export_apply=True,
                export_extras=True,
                export_cameras=False,
                export_lights=False,
                export_leaf_bone=False,
            )
            if "FINISHED" not in result or not filepath.is_file():
                raise RuntimeError(f"GLB export failed for {filepath}: {result}")
            print(f"EXPORTED {filepath} {filepath.stat().st_size} bytes")


def exported_glb_stats(path: Path):
    data = path.read_bytes()
    json_length = struct.unpack_from("<I", data, 12)[0]
    document = json.loads(data[20 : 20 + json_length].decode("utf-8").rstrip(" \0"))
    accessors = document.get("accessors", [])
    triangles = 0
    for mesh in document.get("meshes", []):
        for primitive in mesh.get("primitives", []):
            if primitive.get("mode", 4) != 4:
                continue
            accessor = accessors[primitive["indices"]] if "indices" in primitive else accessors[primitive["attributes"]["POSITION"]]
            triangles += accessor["count"] // 3
    return {"glb_bytes": len(data), "exported_triangles": triangles}


def main():
    args = parse_args()
    source_path = args.source_blend.resolve()
    out_dir = args.output_dir.resolve()
    out_dir.mkdir(parents=True, exist_ok=True)
    textures = write_team_textures(out_dir)
    texture_images = {
        "jersey_base": bpy.data.images.load(str(out_dir / textures["jersey"]["base_color"]), check_existing=True),
        "trouser_base": bpy.data.images.load(str(out_dir / textures["trouser"]["base_color"]), check_existing=True),
    }
    for image in texture_images.values():
        image.pack()

    bpy.ops.wm.open_mainfile(filepath=str(source_path))
    # Recreate image datablocks after opening the source scene.
    texture_images = {
        "jersey_base": bpy.data.images.load(str(out_dir / textures["jersey"]["base_color"]), check_existing=True),
        "trouser_base": bpy.data.images.load(str(out_dir / textures["trouser"]["base_color"]), check_existing=True),
    }
    for image in texture_images.values():
        image.pack()
    HumanService, TargetService = load_mpfb(args.mpfb_root)
    source_rig = bpy.data.objects.get("Practice Batter")
    if source_rig is None or source_rig.type != "ARMATURE" or len(source_rig.data.bones) != 61:
        raise RuntimeError("Source blend is missing the expected 61-bone Practice Batter armature")
    source_collection = bpy.data.collections.get("Player Mesh")
    if source_collection is None:
        raise RuntimeError("Source blend is missing its Player Mesh collection")
    exact, prefixes = gear_source_names()
    gear_sources = [
        obj for obj in source_collection.objects
        if obj.type == "MESH" and (obj.name in exact or obj.name.startswith(prefixes))
    ]
    missing = sorted(exact - {obj.name for obj in gear_sources})
    if missing:
        raise RuntimeError("Source blend is missing requested gear: " + ", ".join(missing))
    if len(gear_sources) < 25:
        raise RuntimeError(f"Expected complete glove/pad/helmet/bat/shoe kit, got {len(gear_sources)} parts")
    source_rig.hide_render = True
    for obj in source_collection.objects:
        obj.hide_render = True

    variants = [
        make_variant(profile, source_rig, gear_sources, textures, out_dir, texture_images, HumanService, TargetService)
        for profile in PROFILES
    ]
    preview_dir = out_dir.parents[2] / "artifacts" / "wp19"
    preview_dir.mkdir(parents=True, exist_ok=True)
    preview_path = preview_dir / "character-variants.png"
    setup_render(preview_path, variants)

    # Keep source geometry and nonzero LODs in the editable file, but show only LOD0 on open.
    for variant in variants:
        for lod, collection in variant["collections"].items():
            for obj in collection.objects:
                obj.hide_render = lod != 0
    manifest = {
        "schema": "super-cricket.player-variants.v1",
        "generated": "2026-10-16",
        "generator": "tools/blender/build_wp19_characters.py, Blender 5.2, MPFB2 2.0.18",
        "source_body": {
            "asset": "MPFB2 bundled MakeHuman base mesh",
            "license": "CC0 1.0",
            "license_url": "https://static.makehumancommunity.org/about/license.html",
            "mpfb_release": "https://static.makehumancommunity.org/mpfb/releases/release_2018.html",
        },
        "skeleton": {"id": "super-cricket-humanoid-61", "bone_count": 61, "source": "assets/characters/practice-batter-humanoid.blend"},
        "lod_switch_meters": [18.0, 42.0],
        "team_color_masks": textures,
        "kit_parts": ["jersey", "trousers", "pads", "gloves", "helmet", "bat", "shoes"],
        "variants": [
            {
                "id": variant["id"],
                "label": variant["label"],
                "macro_profile": variant["macro_profile"],
                "body_vertex_count": variant["body_vertex_count"],
                "body_face_count": variant["body_face_count"],
                "body_bounds_xyz": variant["body_bounds_xyz"],
                "jersey_vertex_count": variant["jersey_vertex_count"],
                "jersey_face_count": variant["jersey_face_count"],
                "trouser_vertex_count": variant["trouser_vertex_count"],
                "trouser_face_count": variant["trouser_face_count"],
                "weighted_body_vertices": variant["weighted_body_vertices"],
                "lods": [
                    {
                        **lod_record,
                        "glb": f"{variant['id']}-lod{lod_record['lod']}.glb",
                    }
                    for lod_record in variant["lods"]
                ],
            }
            for variant in variants
        ],
        "animation_note": "Skinned 61-bone GLBs bind to the shared WP18 clip set by bone name; clip actions are authored in practice-batter-humanoid.glb.",
        "visual_review": "Preview generated. Human in-game review is pending.",
    }
    manifest_path = out_dir / "character-variants.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    blend_path = out_dir / "a5-character-variants.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    export_lod_glbs(out_dir, variants)
    for variant_record in manifest["variants"]:
        for lod_record in variant_record["lods"]:
            lod_record.update(exported_glb_stats(out_dir / lod_record["glb"]))
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"PREVIEW {preview_path}")
    print(f"MANIFEST {manifest_path}")
    print(f"BLEND {blend_path}")
    print("WP19_CHARACTER_BUILD_OK")


if __name__ == "__main__":
    main()
