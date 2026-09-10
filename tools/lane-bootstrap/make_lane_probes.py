#!/usr/bin/env python3
# make_lane_probes.py - ENV-02: lane-namespaced IFF probe fixtures.
#
# AutotestRunner.CheckGlobFix / CheckChunkRegion read two probe IFFs from
# ENGINE-FIXED paths (/tmp/npc_glob_probe.iff, /tmp/posi_probe.iff); the path
# is compiled into the client, so a lane cannot point the engine elsewhere.
# This script regenerates the same byte-verbatim IFF slices that
# tools/iff-dump/make_probes.py produces, but per lane:
#   1. write probes into the lane's own probes/ dir (the lane namespace);
#   2. install to the fixed /tmp paths only via os.replace (atomic), and only
#      when the existing bytes differ, so a concurrent lane can never observe
#      a torn probe file and identical content is never churned.
# Full per-lane /tmp namespacing would need an engine change (AutotestRunner
# probe-path override); recorded as an ENV-02 limitation, not a defect.
#
#   python3 make_lane_probes.py --game-data <dir> --probe-dir <dir> [--install]
#
# Provenance (matches R86 evidence cited in make_probes.py):
#   ExpansionShared.far/NPC_Vacation_Director.iff -> GLOB VacationDirectorGlobals
#   GameData/Objects/Objects.far/foodproc.iff     -> 2x POSI + 1 unknown chunk
import argparse
import hashlib
import os
import struct
import sys

PROBES = [
    {
        "name": "npc_glob_probe.iff",
        "far": os.path.join("ExpansionShared", "ExpansionShared.far"),
        "entry": "NPC_Vacation_Director.iff",
        "check": b"IFF FILE",
    },
    {
        "name": "posi_probe.iff",
        "far": os.path.join("GameData", "Objects", "Objects.far"),
        "entry": "foodproc.iff",
        "check": b"IFF FILE",
    },
]


def extract(far_path, name):
    """Byte-verbatim slice of one entry from a FAR1 archive."""
    with open(far_path, "rb") as f:
        data = f.read()
    man = struct.unpack("<I", data[12:16])[0]
    num = struct.unpack("<I", data[man:man + 4])[0]
    off = man + 4
    for _ in range(num):
        dlen, _d2, doff, nlen = struct.unpack("<IIII", data[off:off + 16])
        nm = data[off + 16:off + 16 + nlen].decode("latin1", "replace")
        off += 16 + nlen
        if nm.lower() == name.lower() or nm.lower().endswith(name.lower()):
            return data[doff:doff + dlen]
    raise SystemExit("entry not found: %s in %s" % (name, far_path))


def sha256(blob):
    return hashlib.sha256(blob).hexdigest()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--game-data", required=True, help="game data root ('The Sims' tree)")
    ap.add_argument("--probe-dir", required=True, help="lane probe output dir")
    ap.add_argument("--install", action="store_true",
                    help="atomically install to the engine-fixed /tmp paths")
    args = ap.parse_args()

    os.makedirs(args.probe_dir, exist_ok=True)
    for spec in PROBES:
        blob = extract(os.path.join(args.game_data, spec["far"]), spec["entry"])
        if not blob.startswith(spec["check"]):
            raise SystemExit("extracted %s is not an IFF" % spec["entry"])
        out = os.path.join(args.probe_dir, spec["name"])
        tmp = out + ".tmp.%d" % os.getpid()
        with open(tmp, "wb") as f:
            f.write(blob)
        os.replace(tmp, out)
        print("probe %s sha256=%s bytes=%d (lane file)"
              % (out, sha256(blob), len(blob)))
        if args.install:
            fixed = os.path.join("/tmp", spec["name"])
            same = False
            try:
                with open(fixed, "rb") as f:
                    same = f.read() == blob
            except OSError:
                same = False
            if same:
                print("install %s unchanged (content identical, not churned)" % fixed)
            else:
                tmp = fixed + ".tmp.%d" % os.getpid()
                with open(tmp, "wb") as f:
                    f.write(blob)
                os.replace(tmp, fixed)  # atomic: readers see old or new, never torn
                print("install %s sha256=%s (atomic replace)" % (fixed, sha256(blob)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
