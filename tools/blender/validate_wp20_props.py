"""Validate the exported WP20 modular prop GLBs."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import struct


EXPECTED_PROPS = {
    "breakable-stumps-and-bails.glb": {"A5 | Breakable stumps and bails", "Stump 1 | removable", "Stump 2 | removable",
                                        "Stump 3 | removable", "Bail 1 | breakable", "Bail 2 | breakable"},
    "willow-bat.glb": {"A5 | Willow bat", "Bat | broad willow blade", "Bat | handle"},
    "batter-helmet-with-face-guard.glb": {"A5 | Batter helmet with face guard", "Helmet | shell",
                                          "Helmet | left guard rail", "Helmet | right guard rail"},
    "modular-sight-screen.glb": {"A5 | Modular sight screen", "Sight screen | panel"},
    "fictional-boundary-board-coastal.glb": {"A5 | Fictional boundary board Coastal", "Coastal board | fictional wordmark"},
    "fictional-boundary-board-highland.glb": {"A5 | Fictional boundary board Highland", "Highland board | fictional wordmark"},
    "modular-stand-seating-bay.glb": {"A5 | Modular stand seating bay"},
    "portable-electronic-scoreboard.glb": {"A5 | Portable electronic scoreboard", "Scoreboard | title"},
    "manual-pitch-roller.glb": {"A5 | Manual pitch roller", "Roller | drum"},
    "boundary-umbrella.glb": {"A5 | Boundary umbrella", "Umbrella | alternating canopy panels"},
    "crowd-impostor-card-set.glb": {"A5 | Crowd impostor card set", "Crowd card | backing"},
}


def read_document(path: Path) -> dict:
    data = path.read_bytes()
    if len(data) < 20:
        raise ValueError(f"{path} is too short to be a GLB.")
    magic, version, declared_length = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF" or version != 2 or declared_length != len(data):
        raise ValueError(f"{path} has an invalid glTF 2.0 binary header.")
    document = None
    offset = 12
    while offset + 8 <= len(data):
        length, kind = struct.unpack_from("<II", data, offset)
        offset += 8
        end = offset + length
        if end > len(data):
            raise ValueError(f"{path} has a truncated chunk.")
        payload = data[offset:end]
        offset = end
        if kind == 0x4E4F534A:
            if document is not None:
                raise ValueError(f"{path} has multiple JSON chunks.")
            document = json.loads(payload.decode("utf-8").rstrip("\x00 \t\r\n"))
    if offset != len(data) or document is None:
        raise ValueError(f"{path} is missing a complete JSON chunk.")
    if not document.get("meshes") or not document.get("nodes"):
        raise ValueError(f"{path} has no mesh and node data.")
    return document


def validate(asset_dir: Path, gallery: Path) -> None:
    missing = sorted(name for name in EXPECTED_PROPS if not (asset_dir / name).is_file())
    if missing:
        raise ValueError("Missing modular prop GLBs: " + ", ".join(missing))
    for filename, required_names in EXPECTED_PROPS.items():
        path = asset_dir / filename
        document = read_document(path)
        names = {node.get("name", "") for node in document["nodes"]}
        missing_names = sorted(required_names - names)
        if missing_names:
            raise ValueError(f"{filename} is missing named prop nodes: " + ", ".join(missing_names))
        print(f"PASS: {filename}: {len(document['meshes'])} meshes, {len(document['nodes'])} nodes, named parts intact.")

    gallery_document = read_document(gallery)
    if len(gallery_document["meshes"]) < len(EXPECTED_PROPS):
        raise ValueError("The gallery GLB has fewer meshes than prop groups.")
    print(f"PASS: {gallery.name}: {len(gallery_document['meshes'])} meshes, {len(gallery_document['nodes'])} nodes.")
    print(f"PASS: validated {len(EXPECTED_PROPS)} modular props and the gallery.")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset-dir", type=Path, default=Path("assets/stadium/props"))
    parser.add_argument("--gallery", type=Path, default=Path("assets/stadium/a5-props-gallery.glb"))
    args = parser.parse_args()
    validate(args.asset_dir.resolve(), args.gallery.resolve())


if __name__ == "__main__":
    main()
