"""Embed Super Cricket events and root-motion samples into glTF animation extras."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import struct
import tempfile

from player_animation_contract import load_animation_contract


GLB_MAGIC = b"glTF"
GLB_VERSION = 2
JSON_CHUNK_TYPE = 0x4E4F534A
PLAYER_METADATA_KEY = "superCricket"
METADATA_VERSION = 1


def read_glb(path: Path) -> tuple[dict, list[tuple[int, bytes]]]:
    data = path.read_bytes()
    if len(data) < 20:
        raise ValueError("GLB is too short to contain a header and JSON chunk.")
    magic, version, declared_length = struct.unpack_from("<4sII", data, 0)
    if magic != GLB_MAGIC or version != GLB_VERSION or declared_length != len(data):
        raise ValueError("File is not a complete glTF 2.0 binary (GLB).")

    chunks: list[tuple[int, bytes]] = []
    document = None
    offset = 12
    while offset + 8 <= len(data):
        chunk_length, chunk_type = struct.unpack_from("<II", data, offset)
        offset += 8
        end = offset + chunk_length
        if end > len(data):
            raise ValueError("GLB chunk extends beyond the declared file length.")
        chunk = data[offset:end]
        offset = end
        if chunk_type == JSON_CHUNK_TYPE:
            if document is not None:
                raise ValueError("GLB contains multiple JSON chunks.")
            document = json.loads(chunk.decode("utf-8").rstrip("\x00 \t\r\n"))
        chunks.append((chunk_type, chunk))
    if offset != len(data) or document is None:
        raise ValueError("GLB has an incomplete chunk or no JSON chunk.")
    return document, chunks


def _metadata_for_animation(animation_contract: dict, clip: dict | None, animation_name: str) -> dict:
    return {
        "version": METADATA_VERSION,
        "assetName": animation_contract["assetName"],
        "coordinateSystem": animation_contract["coordinateSystem"],
        "animationName": animation_name,
        "events": clip["events"] if clip is not None else [],
        "rootMotion": clip["rootMotion"] if clip is not None else {"mode": "zero", "samples": []},
    }


def _write_glb(document: dict, chunks: list[tuple[int, bytes]], output_path: Path) -> None:
    json_chunk = json.dumps(
        document,
        ensure_ascii=False,
        allow_nan=False,
        separators=(",", ":"),
    ).encode("utf-8")
    json_chunk += b" " * ((-len(json_chunk)) % 4)

    encoded_chunks = bytearray()
    for chunk_type, chunk in chunks:
        payload = json_chunk if chunk_type == JSON_CHUNK_TYPE else chunk
        encoded_chunks.extend(struct.pack("<II", len(payload), chunk_type))
        encoded_chunks.extend(payload)

    total_length = 12 + len(encoded_chunks)
    output = struct.pack("<4sII", GLB_MAGIC, GLB_VERSION, total_length) + encoded_chunks
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=output_path.parent, suffix=".glb.tmp", delete=False) as temporary:
        temporary_path = Path(temporary.name)
        temporary.write(output)
    temporary_path.replace(output_path)


def embed_player_metadata(glb_path: Path, animation_contract_path: Path, output_path: Path | None = None) -> int:
    glb_path = glb_path.resolve()
    animation_contract_path = animation_contract_path.resolve()
    output_path = (output_path or glb_path.with_name(glb_path.stem + ".metadata.glb")).resolve()
    document, chunks = read_glb(glb_path)
    animation_contract = load_animation_contract(animation_contract_path)
    source_clips = {clip["name"]: clip for clip in animation_contract["animations"]}
    animations = document.get("animations", [])
    animation_names = [animation.get("name", "") for animation in animations]
    if len(animation_names) != len(set(animation_names)):
        raise ValueError("GLB animation names must be unique before metadata embedding.")
    missing_clips = sorted(set(source_clips) - set(animation_names))
    if missing_clips:
        raise ValueError("GLB is missing player clips: " + ", ".join(missing_clips))

    for animation in animations:
        name = animation["name"]
        extras = animation.setdefault("extras", {})
        extras[PLAYER_METADATA_KEY] = _metadata_for_animation(animation_contract, source_clips.get(name), name)

    _write_glb(document, chunks, output_path)
    return len(animations)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("glb", type=Path, help="Input glTF 2.0 binary.")
    parser.add_argument("animation_contract", type=Path,
                        help="Animation contract JSON (or legacy .scplayer.json during migration).")
    parser.add_argument("--output", type=Path, help="Output GLB; defaults to a separate <name>.metadata.glb.")
    args = parser.parse_args()
    count = embed_player_metadata(args.glb, args.animation_contract, args.output)
    print(f"PASS: embedded Super Cricket events/root-motion metadata in {count} GLB animations: {args.output or args.glb.with_name(args.glb.stem + '.metadata.glb')}")


if __name__ == "__main__":
    main()
