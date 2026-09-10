#!/usr/bin/env python3
# make_probes.py - Regenerate the IFF probe files the autotest gate reads.
#
# AutotestRunner.CheckGlobFix / CheckChunkRegion read two probe IFFs from /tmp:
#   /tmp/npc_glob_probe.iff  - must contain a GLOB chunk named VacationDirectorGlobals
#   /tmp/posi_probe.iff      - must contain preserved (non-skipped) POSI chunks
# These are not game-config; they are IFF-literalism probes: byte-verbatim real
# IFF files sliced straight out of the original .far archives, untouched.
# /tmp is volatile (cleared at boot), so run this before any gate execution.
#
#   python3 tools/iff-dump/make_probes.py
#
# Provenance (matches R86 evidence "glob-fix probes=2 named=2 vdg=2" and
# "chunk-reg posi=2 xxxx=0 nonEmpty=3"):
#   ExpansionShared.far/NPC_Vacation_Director.iff -> 2x GLOB,VacationDirectorGlobals
#   GameData/Objects/Objects.far/foodproc.iff     -> 2x POSI + 1 other unknown chunk
import struct, sys, os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                    "..", "..", "game-data", "The Sims")

def extract(far_path, name):
    """Byte-verbatim slice of one entry from a FAR1 archive."""
    with open(far_path, "rb") as f:
        data = f.read()
    man = struct.unpack("<I", data[12:16])[0]
    num = struct.unpack("<I", data[man:man + 4])[0]
    off = man + 4
    for i in range(num):
        dlen, _d2, doff, nlen = struct.unpack("<IIII", data[off:off + 16])
        nm = data[off + 16:off + 16 + nlen].decode("latin1", "replace")
        off += 16 + nlen
        if nm.lower() == name.lower() or nm.lower().endswith(name.lower()):
            return data[doff:doff + dlen]
    raise SystemExit("entry not found: %s in %s" % (name, far_path))

def main():
    os.makedirs("/tmp", exist_ok=True)
    base = os.path.join(ROOT.replace("/The Sims", ""))
    jobs = [
        (os.path.join(ROOT, "ExpansionShared", "ExpansionShared.far"),
         "NPC_Vacation_Director.iff", "/tmp/npc_glob_probe.iff"),
        (os.path.join(ROOT, "GameData", "Objects", "Objects.far"),
         "foodproc.iff", "/tmp/posi_probe.iff"),
    ]
    for far, name, out in jobs:
        blob = extract(far, name)
        if not blob.startswith(b"IFF FILE"):
            raise SystemExit("extracted %s is not an IFF" % name)
        with open(out, "wb") as f:
            f.write(blob)
        print("wrote %s (%d bytes, IFF-literal from %s/%s)" % (out, len(blob), far, name))

if __name__ == "__main__":
    main()
