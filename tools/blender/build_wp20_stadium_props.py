"""Build the original low-poly WP20 cricket prop and stadium dressing kit."""

from __future__ import annotations

import argparse
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]


def parse_args() -> argparse.Namespace:
    forwarded = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--blend-output", type=Path, default=ROOT / "assets" / "stadium" / "a5-stadium-props.blend")
    parser.add_argument("--gallery-output", type=Path, default=ROOT / "assets" / "stadium" / "a5-props-gallery.glb")
    parser.add_argument("--asset-dir", type=Path, default=ROOT / "assets" / "stadium" / "props")
    parser.add_argument("--preview", type=Path, default=ROOT / "artifacts" / "wp20" / "stadium-props.png")
    return parser.parse_args(forwarded)


def material(name: str, color: tuple[float, float, float, float], metallic: float = 0.0, roughness: float = 0.72) -> bpy.types.Material:
    value = bpy.data.materials.new(name)
    value.diffuse_color = color
    shader = next(node for node in value.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    return value


def assign(obj: bpy.types.Object, mat: bpy.types.Material) -> bpy.types.Object:
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    return obj


def cube(name: str, location: tuple[float, float, float], dimensions: tuple[float, float, float], mat: bpy.types.Material) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return assign(obj, mat)


def cylinder(name: str, location: tuple[float, float, float], radius: float, depth: float,
             mat: bpy.types.Material, rotation: tuple[float, float, float] = (0.0, 0.0, 0.0), vertices: int = 12) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return assign(obj, mat)


def uv_sphere(name: str, location: tuple[float, float, float], scale: tuple[float, float, float], mat: bpy.types.Material) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, radius=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return assign(obj, mat)


def torus(name: str, location: tuple[float, float, float], major: float, minor: float,
          mat: bpy.types.Material) -> bpy.types.Object:
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=20, minor_segments=6, location=location)
    obj = bpy.context.object
    obj.name = name
    return assign(obj, mat)


def mesh_object(name: str, vertices: list[tuple[float, float, float]], faces: list[tuple[int, ...]],
                mats: list[bpy.types.Material], material_indices: list[int] | None = None) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(name + " Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.clear()
    for mat in mats:
        mesh.materials.append(mat)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    if material_indices:
        for polygon, index in zip(mesh.polygons, material_indices, strict=True):
            polygon.material_index = index
    return obj


def text_mesh(name: str, body: str, location: tuple[float, float, float], size: float,
              mat: bpy.types.Material, rotate_to_face: bool = True) -> bpy.types.Object:
    bpy.ops.object.text_add(location=location, rotation=(1.57079632679, 0.0, 0.0) if rotate_to_face else (0.0, 0.0, 0.0))
    obj = bpy.context.object
    obj.name = name
    obj.data.body = body
    obj.data.size = size
    obj.data.align_x = "CENTER"
    obj.data.align_y = "CENTER"
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.object
    obj.name = name
    return assign(obj, mat)


def group_root(name: str, gallery_position: tuple[float, float, float]) -> tuple[bpy.types.Object, set[str]]:
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=(0.0, 0.0, 0.0))
    root = bpy.context.object
    root.name = "A5 | " + name
    root.empty_display_size = 0.24
    before = {obj.name for obj in bpy.context.scene.objects}
    root["sc_prop_group"] = name
    root["sc_origin"] = "Original procedural mesh authored for Super Cricket; no third-party model inputs."
    root["sc_units"] = "metres"
    return root, before


def finish_group(root: bpy.types.Object, before: set[str], gallery_position: tuple[float, float, float]) -> None:
    for obj in list(bpy.context.scene.objects):
        if obj.name not in before and obj != root:
            obj.parent = root
            obj.matrix_parent_inverse = root.matrix_world.inverted()
    root.location = gallery_position


def build_props(mats: dict[str, bpy.types.Material]) -> list[bpy.types.Object]:
    wicket, before = group_root("Breakable stumps and bails", (-8.0, 0.0, 0.0))
    for index, x in enumerate((-0.075, 0.0, 0.075), start=1):
        cylinder(f"Stump {index} | removable", (x, 0.0, 0.355), 0.011, 0.71, mats["willow"], vertices=8)
        torus(f"Stump {index} band", (x, -0.001, 0.60), 0.0115, 0.0022, mats["red"])
    for index, x in enumerate((-0.04, 0.04), start=1):
        cylinder(f"Bail {index} | breakable", (x, 0.0, 0.718), 0.007, 0.075, mats["willow"],
                 rotation=(0.0, 1.57079632679, 0.0), vertices=8)
    finish_group(wicket, before, (-8.0, 0.0, 0.0))

    bat, before = group_root("Willow bat", (-4.0, 0.0, 0.0))
    cylinder("Bat | handle", (0.0, 0.0, 0.735), 0.014, 0.22, mats["grip"], vertices=8)
    cylinder("Bat | grip cap", (0.0, 0.0, 0.852), 0.016, 0.025, mats["blue"], vertices=8)
    blade_vertices = [
        (-0.047, -0.014, 0.44), (0.047, -0.014, 0.44), (0.039, 0.014, 0.44), (-0.039, 0.014, 0.44),
        (-0.054, -0.014, 0.12), (0.054, -0.014, 0.12), (0.046, 0.014, 0.12), (-0.046, 0.014, 0.12),
    ]
    mesh_object("Bat | broad willow blade", blade_vertices,
                [(0, 1, 5, 4), (3, 7, 6, 2), (0, 4, 7, 3), (1, 2, 6, 5), (4, 5, 6, 7), (0, 3, 2, 1)],
                [mats["willow"]])
    finish_group(bat, before, (-4.0, 0.0, 0.0))

    helmet, before = group_root("Batter helmet with face guard", (0.0, 0.0, 0.0))
    uv_sphere("Helmet | shell", (0.0, 0.0, 1.08), (0.145, 0.16, 0.12), mats["navy"])
    cube("Helmet | brow peak", (0.0, -0.13, 1.02), (0.20, 0.08, 0.035), mats["navy"])
    cylinder("Helmet | left guard rail", (-0.06, -0.155, 0.94), 0.006, 0.23, mats["steel"], rotation=(0.22, 0.0, -0.18), vertices=8)
    cylinder("Helmet | right guard rail", (0.06, -0.155, 0.94), 0.006, 0.23, mats["steel"], rotation=(-0.22, 0.0, 0.18), vertices=8)
    cylinder("Helmet | center guard", (0.0, -0.185, 0.935), 0.005, 0.18, mats["steel"], rotation=(0.0, 0.0, 1.57079632679), vertices=8)
    for x in (-0.06, 0.06):
        cylinder("Helmet | side pivot", (x, -0.10, 0.98), 0.018, 0.018, mats["gold"], vertices=10)
    finish_group(helmet, before, (0.0, 0.0, 0.0))

    screen, before = group_root("Modular sight screen", (4.0, 0.0, 0.0))
    cube("Sight screen | panel", (0.0, 0.0, 1.58), (3.8, 0.18, 2.65), mats["screen"])
    cube("Sight screen | top rail", (0.0, 0.0, 2.94), (3.98, 0.24, 0.12), mats["blue"])
    cube("Sight screen | bottom rail", (0.0, 0.0, 0.22), (3.98, 0.24, 0.12), mats["blue"])
    for x in (-1.78, 1.78):
        cube("Sight screen | support post", (x, 0.08, 1.50), (0.12, 0.18, 3.0), mats["steel"])
        cube("Sight screen | foot", (x, 0.06, 0.08), (0.46, 0.64, 0.16), mats["navy"])
    finish_group(screen, before, (4.0, 0.0, 0.0))

    board_a, before = group_root("Fictional boundary board Coastal", (8.0, 0.0, 0.0))
    cube("Coastal board | fascia", (0.0, 0.0, 0.48), (2.4, 0.12, 0.86), mats["navy"])
    cube("Coastal board | inset", (0.0, -0.068, 0.49), (2.20, 0.02, 0.68), mats["blue"])
    cube("Coastal board | accent line", (0.0, -0.084, 0.25), (2.20, 0.012, 0.035), mats["gold"])
    text_mesh("Coastal board | fictional wordmark", "COASTAL", (0.0, -0.092, 0.50), 0.26, mats["white"])
    finish_group(board_a, before, (8.0, 0.0, 0.0))

    board_b, before = group_root("Fictional boundary board Highland", (-8.0, 5.0, 0.0))
    cube("Highland board | fascia", (0.0, 0.0, 0.48), (2.4, 0.12, 0.86), mats["cream"])
    cube("Highland board | inset", (0.0, -0.068, 0.49), (2.20, 0.02, 0.68), mats["green"])
    cube("Highland board | accent line", (0.0, -0.084, 0.25), (2.20, 0.012, 0.035), mats["red"])
    text_mesh("Highland board | fictional wordmark", "HIGHLAND", (0.0, -0.092, 0.50), 0.23, mats["cream"])
    finish_group(board_b, before, (-8.0, 5.0, 0.0))

    stands, before = group_root("Modular stand seating bay", (-4.0, 5.0, 0.0))
    cube("Stand | rear wall", (0.0, 0.22, 0.92), (3.6, 0.22, 1.9), mats["cream"])
    for row in range(4):
        z = 0.22 + row * 0.31
        y = 0.10 + row * 0.34
        cube(f"Stand | bench row {row+1}", (0.0, y, z), (3.5, 0.38, 0.17), mats["steel"])
        for seat in range(10):
            x = -1.50 + seat * 0.333
            seat_mat = mats["blue"] if seat % 3 else mats["gold"]
            cube(f"Stand | seat {row+1}-{seat+1}", (x, y - 0.15, z + 0.15), (0.26, 0.27, 0.16), seat_mat)
    for x in (-1.72, 1.72):
        cylinder("Stand | rail post", (x, -0.16, 0.82), 0.025, 1.5, mats["blue"], vertices=8)
    cylinder("Stand | front rail", (0.0, -0.16, 1.52), 0.025, 3.5, mats["blue"],
             rotation=(0.0, 1.57079632679, 0.0), vertices=8)
    finish_group(stands, before, (-4.0, 5.0, 0.0))

    scoreboard, before = group_root("Portable electronic scoreboard", (0.0, 5.0, 0.0))
    cube("Scoreboard | frame", (0.0, 0.0, 1.35), (2.65, 0.2, 1.62), mats["navy"])
    cube("Scoreboard | display", (0.0, -0.115, 1.39), (2.38, 0.025, 1.34), mats["display"])
    text_mesh("Scoreboard | title", "COASTAL 142/6", (0.0, -0.14, 1.76), 0.25, mats["gold"])
    text_mesh("Scoreboard | overs", "18.4 OVERS", (0.0, -0.14, 1.42), 0.23, mats["green_light"])
    text_mesh("Scoreboard | chase", "TARGET 168", (0.0, -0.14, 1.10), 0.18, mats["cream"])
    for x in (-0.84, 0.0, 0.84):
        cube("Scoreboard | footing", (x, 0.0, 0.28), (0.14, 0.18, 0.56), mats["steel"])
    cube("Scoreboard | ground shoe", (0.0, 0.0, 0.045), (1.95, 0.62, 0.09), mats["navy"])
    finish_group(scoreboard, before, (0.0, 5.0, 0.0))

    roller, before = group_root("Manual pitch roller", (4.0, 5.0, 0.0))
    cylinder("Roller | drum", (0.0, 0.0, 0.22), 0.22, 1.25, mats["steel"], rotation=(0.0, 1.57079632679, 0.0), vertices=20)
    cylinder("Roller | left cap", (-0.64, 0.0, 0.22), 0.20, 0.035, mats["gold"], rotation=(0.0, 1.57079632679, 0.0), vertices=16)
    cylinder("Roller | right cap", (0.64, 0.0, 0.22), 0.20, 0.035, mats["gold"], rotation=(0.0, 1.57079632679, 0.0), vertices=16)
    cube("Roller | axle frame", (0.0, 0.0, 0.42), (0.32, 0.48, 0.32), mats["navy"])
    for x in (-0.36, 0.36):
        cube("Roller | handle upright", (x, 0.12, 0.78), (0.06, 0.06, 0.82), mats["blue"])
    cylinder("Roller | hand grip", (0.0, 0.12, 1.20), 0.035, 0.84, mats["grip"], rotation=(0.0, 1.57079632679, 0.0), vertices=8)
    finish_group(roller, before, (4.0, 5.0, 0.0))

    umbrella, before = group_root("Boundary umbrella", (8.0, 5.0, 0.0))
    cylinder("Umbrella | pole", (0.0, 0.0, 0.98), 0.024, 1.96, mats["steel"], vertices=10)
    ring = []
    vertices = []
    faces = []
    indices = []
    segments = 12
    for i in range(segments):
        angle = 6.28318530718 * i / segments
        ring.append((0.68 * math.cos(angle), 0.68 * math.sin(angle), 1.98))
    vertices.append((0.0, 0.0, 2.24))
    vertices.extend(ring)
    for i in range(segments):
        faces.append((0, 1 + i, 1 + ((i + 1) % segments)))
        indices.append(i % 2)
    mesh_object("Umbrella | alternating canopy panels", vertices, faces, [mats["red"], mats["cream"]], indices)
    for i in range(0, segments, 2):
        angle = 6.28318530718 * i / segments
        cylinder("Umbrella | rib", (0.34 * math.cos(angle), 0.34 * math.sin(angle), 2.11), 0.006, 0.75, mats["steel"], vertices=6)
    finish_group(umbrella, before, (8.0, 5.0, 0.0))

    crowd, before = group_root("Crowd impostor card set", (0.0, 10.0, 0.0))
    cube("Crowd card | backing", (0.0, 0.05, 0.84), (3.6, 0.04, 1.65), mats["green"])
    crowd_mats = [mats["blue"], mats["gold"], mats["red"], mats["cream"]]
    for index in range(9):
        x = -1.45 + (index % 5) * 0.72
        z = 0.46 + (index // 5) * 0.76
        mat = crowd_mats[index % len(crowd_mats)]
        uv_sphere(f"Crowd card | head {index+1}", (x, -0.02, z + 0.22), (0.11, 0.05, 0.12), mat)
        mesh_object(f"Crowd card | torso {index+1}",
                    [(x-0.16,-0.025,z-0.22),(x+0.16,-0.025,z-0.22),(x+0.12,-0.025,z+0.10),(x-0.12,-0.025,z+0.10)],
                    [(0,1,2,3)], [mat])
    finish_group(crowd, before, (0.0, 10.0, 0.0))

    return [wicket, bat, helmet, screen, board_a, board_b, stands, scoreboard, roller, umbrella, crowd]


def create_gallery_labels(roots: list[bpy.types.Object], mat: bpy.types.Material) -> None:
    for root in roots:
        world = root.location.copy()
        text_mesh("Gallery Label | " + root["sc_prop_group"], root["sc_prop_group"].upper(),
                  (world.x, world.y - 1.1, 0.04), 0.18, mat)


def setup_preview(scene: bpy.types.Scene, output: Path) -> None:
    floor_mat = material("Gallery | deep green", (0.055, 0.16, 0.115, 1.0))
    cube("Gallery ground", (0.0, 4.8, -0.10), (22.0, 14.0, 0.20), floor_mat)
    scene.world.color = (0.035, 0.045, 0.06)
    scene.render.engine = next(item.identifier for item in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items if "EEVEE" in item.identifier)
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output.resolve())
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "Medium High Contrast"
    camera_data = bpy.data.cameras.new("A5 Stadium Props Camera")
    camera = bpy.data.objects.new("A5 Stadium Props Camera", camera_data)
    scene.collection.objects.link(camera)
    target = Vector((0.0, 4.7, 0.82))
    camera.location = Vector((0.0, -15.0, 17.0))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 24.0
    scene.camera = camera
    for name, location, energy, size in (
        ("A5 Gallery Key", (1.0, -8.0, 14.0), 1800.0, 9.0),
        ("A5 Gallery Fill", (-8.0, 8.0, 9.0), 950.0, 8.0),
    ):
        data = bpy.data.lights.new(name, type="AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = location
        obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def export_glb(path: Path, selected: bool) -> None:
    result = bpy.ops.export_scene.gltf(
        filepath=str(path.resolve()),
        export_format="GLB",
        use_selection=selected,
        export_yup=True,
        export_apply=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"GLB export failed: {path}")


def main() -> None:
    args = parse_args()
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for collection in list(bpy.data.collections):
        if collection.name != "Collection" and collection.users == 0:
            bpy.data.collections.remove(collection)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.fps = 30
    mats = {
        "willow": material("Willow | pale bat and stumps", (0.78, 0.54, 0.28, 1.0)),
        "grip": material("Bat grip | charcoal", (0.12, 0.16, 0.20, 1.0)),
        "navy": material("Coastal navy", (0.035, 0.10, 0.20, 1.0)),
        "blue": material("Bright coastal blue", (0.06, 0.36, 0.70, 1.0)),
        "gold": material("Highland gold", (0.95, 0.57, 0.08, 1.0)),
        "red": material("Warm coral", (0.82, 0.15, 0.12, 1.0)),
        "white": material("Clean white", (0.94, 0.95, 0.91, 1.0)),
        "cream": material("Warm cream", (0.94, 0.86, 0.65, 1.0)),
        "steel": material("Powder-coated steel", (0.35, 0.46, 0.50, 1.0), metallic=0.62, roughness=0.38),
        "screen": material("Sight-screen white", (0.86, 0.90, 0.82, 1.0)),
        "green": material("Stadium green", (0.10, 0.38, 0.22, 1.0)),
        "display": material("Scoreboard black glass", (0.015, 0.04, 0.04, 1.0), metallic=0.2, roughness=0.24),
        "green_light": material("Scoreboard mint", (0.22, 0.95, 0.58, 1.0)),
    }
    roots = build_props(mats)
    create_gallery_labels(roots, mats["white"])

    args.blend_output.parent.mkdir(parents=True, exist_ok=True)
    args.gallery_output.parent.mkdir(parents=True, exist_ok=True)
    args.asset_dir.mkdir(parents=True, exist_ok=True)
    args.preview.parent.mkdir(parents=True, exist_ok=True)
    setup_preview(scene, args.preview)
    bpy.ops.wm.save_as_mainfile(filepath=str(args.blend_output.resolve()))
    bpy.ops.render.render(write_still=True)
    export_glb(args.gallery_output, selected=False)

    for root in roots:
        group = [root, *[obj for obj in bpy.context.scene.objects if obj.parent == root]]
        for obj in bpy.context.selected_objects:
            obj.select_set(False)
        for obj in group:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = root
        original_location = root.location.copy()
        root.location = (0.0, 0.0, 0.0)
        export_glb(args.asset_dir / (str(root["sc_prop_group"]).lower().replace(" ", "-").replace("/", "-") + ".glb"), selected=True)
        root.location = original_location

    print(f"Exported {len(roots)} modular prop GLBs to {args.asset_dir.resolve()}")
    print(f"Gallery GLB: {args.gallery_output.resolve()}")
    print(f"Blender source: {args.blend_output.resolve()}")
    print(f"Visual review render: {args.preview.resolve()}")


if __name__ == "__main__":
    main()
