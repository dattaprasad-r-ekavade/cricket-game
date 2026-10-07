from __future__ import annotations

import contextlib
import copy
import io
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
BLENDER_TOOLS = ROOT / "tools" / "blender"
sys.path.insert(0, str(BLENDER_TOOLS))

from embed_player_metadata import embed_player_metadata  # noqa: E402
from player_animation_contract import (  # noqa: E402
    contract_from_legacy_player_asset,
    load_animation_contract,
    validate_animation_contract,
)
from validate_humanoid_glb import load_glb_json, validate  # noqa: E402


ASSETS = (
    ("batter", "practice-batter"),
    ("bowler", "practice-bowler"),
)


class PlayerAnimationContractTests(unittest.TestCase):
    def test_checked_in_contracts_match_legacy_parity_sources(self) -> None:
        for _, asset_name in ASSETS:
            with self.subTest(asset=asset_name):
                legacy_path = ROOT / "assets" / "characters" / f"{asset_name}.scplayer.json"
                contract_path = ROOT / "assets" / "characters" / f"{asset_name}.animation-contract.json"
                legacy = json.loads(legacy_path.read_text(encoding="utf-8"))
                expected = contract_from_legacy_player_asset(legacy)

                self.assertEqual(expected, load_animation_contract(contract_path))
                self.assertEqual(expected, load_animation_contract(legacy_path))
                self.assertNotIn("bones", expected)
                self.assertNotIn("meshes", expected)
                self.assertTrue(all("samples" not in clip for clip in expected["animations"]))
                self.assertLess(contract_path.stat().st_size * 20, legacy_path.stat().st_size)

                with tempfile.TemporaryDirectory(prefix="sc-extract-") as directory:
                    extracted_path = Path(directory) / contract_path.name
                    subprocess.run(
                        [
                            sys.executable,
                            str(BLENDER_TOOLS / "extract_player_animation_contract.py"),
                            str(legacy_path),
                            str(extracted_path),
                        ],
                        check=True,
                        capture_output=True,
                        text=True,
                    )
                    self.assertEqual(expected, json.loads(extracted_path.read_text(encoding="utf-8")))

    def test_contract_round_trip_preserves_glb_metadata_and_validation(self) -> None:
        for role, asset_name in ASSETS:
            with self.subTest(asset=asset_name), tempfile.TemporaryDirectory(prefix="sc-contract-") as directory:
                glb_path = ROOT / "assets" / "characters" / f"{asset_name}-humanoid.glb"
                contract_path = ROOT / "assets" / "characters" / f"{asset_name}.animation-contract.json"
                copied_glb = Path(directory) / glb_path.name
                shutil.copyfile(glb_path, copied_glb)

                original = load_glb_json(glb_path)
                embedded_count = embed_player_metadata(copied_glb, contract_path, copied_glb)
                rewritten = load_glb_json(copied_glb)
                original_metadata = {
                    animation["name"]: animation["extras"]["superCricket"]
                    for animation in original["animations"]
                }
                rewritten_metadata = {
                    animation["name"]: animation["extras"]["superCricket"]
                    for animation in rewritten["animations"]
                }

                self.assertEqual(len(original_metadata), embedded_count)
                self.assertEqual(original_metadata, rewritten_metadata)
                with contextlib.redirect_stdout(io.StringIO()):
                    validate(copied_glb, contract_path, role)

    def test_contract_rejects_non_increasing_root_motion_samples(self) -> None:
        source_path = ROOT / "assets" / "characters" / "practice-bowler.animation-contract.json"
        contract = copy.deepcopy(load_animation_contract(source_path))
        samples = contract["animations"][0]["rootMotion"]["samples"]
        samples[1]["timeSeconds"] = samples[0]["timeSeconds"]

        with self.assertRaisesRegex(ValueError, "sample times must increase"):
            validate_animation_contract(contract)


if __name__ == "__main__":
    unittest.main()
