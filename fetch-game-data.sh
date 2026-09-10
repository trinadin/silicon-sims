#!/bin/bash
# fetch-game-data.sh - Legally stage The Sims: Complete Collection data for Simitone macOS.
# You must own The Sims 1. Downloads the official DVD ISO from archive.org and extracts it.
set -e
cd "$(dirname "$(readlink -f "$0" 2>/dev/null || echo "$0")")"
ISO=SimsComplete.iso
ITEM=the-sims-complete-collection-usa-canada-enfr
URL="https://archive.org/download/${ITEM}/Sims%2C%20The%20-%20Complete%20Collection%20%28USA%2C%20Canada%29%20%28En%2CFr%29.iso"
if [ ! -f "$ISO" ]; then
  echo "Downloading Complete Collection ISO (~3.4 GB) from archive.org..."
  curl -L -C - -o "$ISO" "$URL"
fi
echo "Mounting ISO..."
MOUNT=$(hdiutil attach "$ISO" -readonly -nobrowse | tail -1 | awk '{print $NF}')
echo "Mounted at $MOUNT"
SRC="$MOUNT/The Sims"
mkdir -p game-data
echo "Copying The Sims data to game-data/ (3.2 GB)..."
rm -rf "game-data/The Sims"
ditto "$SRC" "game-data/The Sims"
hdiutil detach "$MOUNT" >/dev/null 2>&1 || true
if [ -f "game-data/The Sims/GameData/Behavior.iff" ]; then
  echo "OK: game-data present with GameData/Behavior.iff"
  exit 0
fi
echo "ERROR: Behavior.iff not found"
exit 1
