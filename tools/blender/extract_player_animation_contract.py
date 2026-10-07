"""Extract GLB event/root-motion metadata from a legacy player asset."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from player_animation_contract import contract_from_legacy_player_asset


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("player_asset", type=Path, help="Legacy v1 .scplayer.json source asset.")
    parser.add_argument("output", type=Path, help="Output animation-contract JSON path.")
    args = parser.parse_args()

    player_asset = json.loads(args.player_asset.read_text(encoding="utf-8"))
    contract = contract_from_legacy_player_asset(player_asset)
    encoded = json.dumps(contract, ensure_ascii=False, allow_nan=False, indent=2) + "\n"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(encoded, encoding="utf-8", newline="\n")
    print(f"PASS: extracted {len(contract['animations'])} animation contracts to {args.output}.")


if __name__ == "__main__":
    main()
