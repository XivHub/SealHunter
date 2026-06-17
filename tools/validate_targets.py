#!/usr/bin/env python3
"""Structural validation for the bundled GC hunting-log dataset.

Checks that every GC key is present, every monster has an Id/Count and at least one
location with coordinates, and reports how many targets are duty-bound (Zone != 0),
which the bot cannot auto-farm. Run from the repo root.
"""
import json
import sys
from pathlib import Path

DATA = Path(__file__).resolve().parent.parent / "SealHunter" / "Data" / "hunt_targets.json"
GC_KEYS = {"10001": "Maelstrom", "10002": "Twin Adder", "10003": "Immortal Flames"}


def main() -> int:
    data = json.loads(DATA.read_text())
    ranks = data.get("JobRanks", {})
    errors = []
    monsters = open_world = duty = 0

    for key in GC_KEYS:
        if key not in ranks:
            errors.append(f"missing GC key {key} ({GC_KEYS[key]})")

    for key, rank_list in ranks.items():
        for ri, rank in enumerate(rank_list):
            for task in rank.get("Tasks", []):
                for m in task.get("Monsters", []):
                    monsters += 1
                    where = f"{GC_KEYS.get(key, key)} rank{ri+1} {m.get('Name')!r}"
                    if not m.get("Id"):
                        errors.append(f"{where}: missing Id")
                    if not m.get("Count"):
                        errors.append(f"{where}: missing Count")
                    locs = m.get("Locations", [])
                    if not locs:
                        errors.append(f"{where}: no locations")
                    for loc in locs:
                        if loc.get("Zone", 0) != 0:
                            duty += 1  # duty-bound target; no overworld coords expected
                        else:
                            open_world += 1
                            if loc.get("xCoord", 0) == 0 and loc.get("yCoord", 0) == 0:
                                errors.append(f"{where}: open-world location has zero coords")

    print(f"GC keys: {sorted(ranks.keys())}")
    print(f"monsters: {monsters}  open-world locations: {open_world}  duty locations: {duty}")

    if errors:
        print(f"\n{len(errors)} problem(s):")
        for e in errors:
            print("  -", e)
        return 1

    print("OK: dataset is structurally valid (no gaps).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
