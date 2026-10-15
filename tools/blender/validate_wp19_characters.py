#!/usr/bin/env python3
"""Validate WP19 character GLBs, skins, LOD size ordering and team-mask textures."""

from __future__ import annotations

import argparse
import json
import struct
from pathlib import Path

from PIL import Image


def read_glb(path: Path):
    data = path.read_bytes()
    if len(data) < 20:
        raise ValueError(f"{path} is too short to be a GLB")
    magic, version, total_length = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF" or version != 2 or total_length != len(data):
        raise ValueError(f"{path} has an invalid GLB header")
    json_length, json_kind = struct.unpack_from("<I4s", data, 12)
    if json_kind != b"JSON":
        raise ValueError(f"{path} is missing its JSON chunk")
    document = json.loads(data[20 : 20 + json_length].decode("utf-8").rstrip(" \0"))
    skins = document.get("skins", [])
    if len(skins) != 1 or len(skins[0].get("joints", [])) != 61:
        raise ValueError(f"{path} does not contain one 61-joint skin")
    triangle_count = 0
    skinned_mesh_nodes = 0
    for node in document.get("nodes", []):
        if "mesh" in node and "skin" in node:
            skinned_mesh_nodes += 1
    accessors = document.get("accessors", [])
    for mesh in document.get("meshes", []):
        for primitive in mesh.get("primitives", []):
            if primitive.get("mode", 4) != 4:
                continue
            if "indices" in primitive:
                count = accessors[primitive["indices"]]["count"]
            else:
                count = accessors[primitive["attributes"]["POSITION"]]["count"]
            triangle_count += count // 3
    if skinned_mesh_nodes == 0:
        raise ValueError(f"{path} contains no nodes with a skinned mesh")
    return {
        "bytes": len(data),
        "joints": len(skins[0]["joints"]),
        "mesh_nodes": skinned_mesh_nodes,
        "triangles": triangle_count,
        "materials": len(document.get("materials", [])),
        "textures": len(document.get("textures", [])),
        "images": len(document.get("images", [])),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset-dir", type=Path, default=Path("assets/characters/wp19"))
    args = parser.parse_args()
    asset_dir = args.asset_dir.resolve()
    manifest_path = asset_dir / "character-variants.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest.get("skeleton", {}).get("bone_count") != 61:
        raise ValueError("Manifest skeleton count is not 61")
    if len(manifest.get("variants", [])) != 3:
        raise ValueError("Expected three character variants")
    total_glbs = 0
    body_widths = []
    for variant in manifest["variants"]:
        lods = sorted(variant["lods"], key=lambda item: item["lod"])
        if [record["lod"] for record in lods] != [0, 1, 2]:
            raise ValueError(f"{variant['id']} is missing an LOD")
        stats = []
        for record in lods:
            path = asset_dir / record["glb"]
            info = read_glb(path)
            if info["triangles"] <= 0:
                raise ValueError(f"{path} has no triangles")
            stats.append(info)
            total_glbs += 1
            print(f"{path.name}: {info['joints']} joints, {info['mesh_nodes']} skinned meshes, {info['triangles']} triangles, {info['bytes']} bytes")
        if not (stats[0]["triangles"] > stats[1]["triangles"] > stats[2]["triangles"]):
            raise ValueError(f"{variant['id']} triangle counts do not decrease across LODs: {[s['triangles'] for s in stats]}")
        if variant.get("weighted_body_vertices") != variant.get("body_vertex_count"):
            raise ValueError(f"{variant['id']} has unweighted body vertices")
        bounds = variant.get("body_bounds_xyz")
        if not bounds or len(bounds) != 3:
            raise ValueError(f"{variant['id']} has no baked body bounds")
        body_widths.append(bounds[0][1] - bounds[0][0])
    for garment in ("jersey", "trouser"):
        record = manifest["team_color_masks"][garment]
        mask_path = asset_dir / record["mask"]
        base_path = asset_dir / record["base_color"]
        with Image.open(mask_path) as mask:
            mask_image = mask.convert("L")
            colors = set(mask_image.getdata())
            if mask_image.size != (128, 128) or len(colors) != 2:
                raise ValueError(f"{mask_path} is not a 128x128 two-tone team mask")
        with Image.open(base_path) as base:
            if base.size != (128, 128) or base.convert("RGB").getbbox() is None:
                raise ValueError(f"{base_path} is blank or has unexpected dimensions")
    if total_glbs != 9:
        raise ValueError(f"Expected 9 GLBs, found {total_glbs}")
    if max(body_widths) - min(body_widths) < 0.05:
        raise ValueError(f"Baked physique variants are not measurably distinct in width: {body_widths}")
    print("Body widths (m): " + ", ".join(f"{width:.3f}" for width in body_widths))
    print(f"WP19_VALIDATION_OK {total_glbs} GLBs, 3 variants, 9 skinned LOD assets, jersey/trouser masks")


if __name__ == "__main__":
    main()
