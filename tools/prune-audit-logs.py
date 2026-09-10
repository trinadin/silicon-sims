#!/usr/bin/env python3
"""Preview or delete raw logs outside the latest four numbered evidence rounds."""

import argparse
from pathlib import Path
import re


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true", help="delete without archives")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent / "iff-dump"
    rounds = []
    for directory in root.iterdir():
        match = re.match(r"r(\d+)(?:\D|$)", directory.name)
        if directory.is_dir() and not directory.is_symlink() and match:
            rounds.append((int(match[1]), directory))
    if not rounds:
        parser.error("no numbered evidence rounds found")
    cutoff = max(number for number, _ in rounds) - 3
    candidates = []
    for number, directory in rounds:
        if number >= cutoff:
            continue
        # Never follow directory/file symlinks outside the evidence tree.
        for path in directory.rglob("*.log"):
            if path.is_file() and not any(
                item.is_symlink() for item in (path, *path.parents)
            ):
                candidates.append(path)
    size = sum(path.stat().st_size for path in candidates)
    print(f"Retain r{cutoff}+; {len(candidates)} old logs, {size:,} bytes")
    if args.apply:
        for path in candidates:
            path.unlink()
        print("Deleted directly; no archives created.")
    else:
        print("Preview only. Pass --apply to delete.")


if __name__ == "__main__":
    main()
