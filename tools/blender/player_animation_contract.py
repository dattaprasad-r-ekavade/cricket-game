"""Read the compact animation metadata contract used by player GLB tools."""

from __future__ import annotations

import json
import math
from pathlib import Path
from typing import Any


CONTRACT_VERSION = 1
COORDINATE_SYSTEM = "right-handed-y-up-metres"


def contract_from_legacy_player_asset(player_asset: dict[str, Any]) -> dict[str, Any]:
    """Extract clip events and root motion from a v1 player asset."""
    contract = {
        "contractVersion": CONTRACT_VERSION,
        "assetName": player_asset.get("name"),
        "coordinateSystem": player_asset.get("coordinateSystem"),
        "animations": [],
    }
    for clip in player_asset.get("animations", []):
        root_samples = []
        for sample in clip.get("samples", []):
            root = sample.get("rootMotion")
            if not isinstance(root, dict):
                raise ValueError(f"Clip '{clip.get('name', '')}' has a sample without root motion.")
            root_samples.append({
                "timeSeconds": sample.get("timeSeconds"),
                "positionMeters": [root.get(axis) for axis in ("x", "y", "z")],
            })
        contract["animations"].append({
            "name": clip.get("name"),
            "events": clip.get("events", []),
            "rootMotion": {
                "mode": "samples" if root_samples else "zero",
                "samples": root_samples,
            },
        })
    return validate_animation_contract(contract)


def validate_animation_contract(contract: dict[str, Any]) -> dict[str, Any]:
    if not isinstance(contract, dict) or contract.get("contractVersion") != CONTRACT_VERSION:
        raise ValueError(f"Animation contract version must be {CONTRACT_VERSION}.")
    asset_name = contract.get("assetName")
    if not isinstance(asset_name, str) or not asset_name.strip():
        raise ValueError("Animation contract must have a non-empty assetName.")
    if contract.get("coordinateSystem") != COORDINATE_SYSTEM:
        raise ValueError(f"Animation contract coordinates must be {COORDINATE_SYSTEM}.")
    animations = contract.get("animations")
    if not isinstance(animations, list) or not animations:
        raise ValueError("Animation contract must contain at least one animation.")

    names: set[str] = set()
    for animation in animations:
        if not isinstance(animation, dict):
            raise ValueError("Animation contract contains an invalid animation.")
        name = animation.get("name")
        if not isinstance(name, str) or not name.strip() or name in names:
            raise ValueError("Animation contract animation names must be unique and non-empty.")
        names.add(name)

        events = animation.get("events")
        if not isinstance(events, list):
            raise ValueError(f"Animation '{name}' events must be a list.")
        event_names: set[str] = set()
        for event in events:
            if not isinstance(event, dict):
                raise ValueError(f"Animation '{name}' contains an invalid event.")
            event_name = event.get("name")
            time = event.get("timeSeconds")
            if not isinstance(event_name, str) or not event_name.strip() or event_name.casefold() in event_names:
                raise ValueError(f"Animation '{name}' event names must be unique and non-empty.")
            if not _is_finite_number(time) or time < 0:
                raise ValueError(f"Animation '{name}' event times must be finite and non-negative.")
            event_names.add(event_name.casefold())

        root_motion = animation.get("rootMotion")
        if not isinstance(root_motion, dict):
            raise ValueError(f"Animation '{name}' must define rootMotion.")
        mode = root_motion.get("mode")
        samples = root_motion.get("samples")
        if not isinstance(samples, list):
            raise ValueError(f"Animation '{name}' root-motion samples must be a list.")
        if mode == "zero":
            if samples:
                raise ValueError(f"Animation '{name}' zero root motion must not have samples.")
            continue
        if mode != "samples" or not samples:
            raise ValueError(f"Animation '{name}' root motion must use zero or sampled mode.")

        previous_time = -math.inf
        for sample in samples:
            if not isinstance(sample, dict):
                raise ValueError(f"Animation '{name}' has an invalid root-motion sample.")
            time = sample.get("timeSeconds")
            position = sample.get("positionMeters")
            if not _is_finite_number(time) or time < 0 or time <= previous_time:
                raise ValueError(f"Animation '{name}' root-motion sample times must increase from zero or later.")
            if not isinstance(position, list) or len(position) != 3 or any(not _is_finite_number(value) for value in position):
                raise ValueError(f"Animation '{name}' root-motion positions must contain finite XYZ values.")
            previous_time = time
    return contract


def load_animation_contract(path: Path) -> dict[str, Any]:
    document = json.loads(path.read_text(encoding="utf-8"))
    if isinstance(document, dict) and "contractVersion" in document:
        return validate_animation_contract(document)
    if isinstance(document, dict) and "name" in document and "animations" in document:
        return contract_from_legacy_player_asset(document)
    raise ValueError(f"'{path}' is neither an animation contract nor a legacy player asset.")


def _is_finite_number(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)
