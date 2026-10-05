"""Resize an artist or AI texture and feather its opposite edges for repeat tiling."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


def smooth_weight(distance: int, width: int) -> float:
    t = max(0.0, 1.0 - distance / width)
    return t * t * (3.0 - 2.0 * t)


def feather_opposite_edges(image: Image.Image, blend_width: int) -> Image.Image:
    if image.width != image.height:
        raise ValueError("Input texture must be square.")
    if blend_width < 1 or blend_width * 2 >= image.width:
        raise ValueError("Blend width must be positive and less than half the texture size.")

    pixels = image.load()
    size = image.width
    for y in range(size):
        left = pixels[0, y]
        right = pixels[size - 1, y]
        for x in range(blend_width + 1):
            left_weight = smooth_weight(x, blend_width)
            right_x = size - 1 - x
            right_weight = smooth_weight(x, blend_width)
            pixels[x, y] = tuple(
                round(pixels[x, y][channel] + (right[channel] - left[channel]) * 0.5 * left_weight)
                for channel in range(3)
            )
            pixels[right_x, y] = tuple(
                round(pixels[right_x, y][channel] + (left[channel] - right[channel]) * 0.5 * right_weight)
                for channel in range(3)
            )

    for x in range(size):
        top = pixels[x, 0]
        bottom = pixels[x, size - 1]
        for y in range(blend_width + 1):
            weight = smooth_weight(y, blend_width)
            bottom_y = size - 1 - y
            pixels[x, y] = tuple(
                round(pixels[x, y][channel] + (bottom[channel] - top[channel]) * 0.5 * weight)
                for channel in range(3)
            )
            pixels[x, bottom_y] = tuple(
                round(pixels[x, bottom_y][channel] + (top[channel] - bottom[channel]) * 0.5 * weight)
                for channel in range(3)
            )

    return image


def edge_delta(image: Image.Image) -> tuple[int, int]:
    pixels = image.load()
    size = image.width
    horizontal = max(
        abs(pixels[0, y][channel] - pixels[size - 1, y][channel])
        for y in range(size)
        for channel in range(3)
    )
    vertical = max(
        abs(pixels[x, 0][channel] - pixels[x, size - 1][channel])
        for x in range(size)
        for channel in range(3)
    )
    return horizontal, vertical


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--size", type=int, default=256, help="square output dimension (default: 256)")
    parser.add_argument("--blend-width", type=int, default=12, help="edge feather width in pixels (default: 12)")
    args = parser.parse_args()

    if args.size < 64:
        parser.error("--size must be at least 64 pixels")
    with Image.open(args.source) as source:
        texture = source.convert("RGB").resize(
            (args.size, args.size),
            Image.Resampling.LANCZOS,
        )

    texture = feather_opposite_edges(texture, args.blend_width)
    args.destination.parent.mkdir(parents=True, exist_ok=True)
    texture.save(args.destination, format="PNG", optimize=True)
    horizontal, vertical = edge_delta(texture)
    print(f"Wrote {args.destination} ({args.size}x{args.size}); max wrapped edge delta: X={horizontal}, Y={vertical}.")
    return 0 if horizontal <= 1 and vertical <= 1 else 1


if __name__ == "__main__":
    raise SystemExit(main())
