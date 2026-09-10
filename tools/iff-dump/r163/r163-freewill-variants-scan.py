#!/usr/bin/env python3
"""R163: prove that dynamic pie strings reach autonomous TTAB entries.

Scans every loose IFF/FAM and every IFF/FAM member of every FAR under the
staged original game-data tree.  Opcode 50 is TS1 ``Change Action String``.
The focused TTAB result is deliberately conservative: an interaction must
have a non-zero motive advertisement, and its *local* test BHAV must itself
contain opcode 50.  No call-graph inference is counted in that total.

Run from anywhere:
    python3 tools/iff-dump/r163/r163-freewill-variants-scan.py

The script prints and rewrites ``r163-freewill-variants-scan.txt`` beside
itself.  It imports the already checked-in R154 IFF/FAR and field-code readers;
it creates no corpus cache.
"""

import importlib.util
import os
import struct


HERE = os.path.dirname(os.path.abspath(__file__))
R154 = os.path.normpath(os.path.join(HERE, "..", "r154"))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "game-data", "The Sims"))
OUTPUT = os.path.join(HERE, "r163-freewill-variants-scan.txt")


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


IFF = load_module("r154_iff", os.path.join(R154, "r154-callers-scan.py"))
FIELD = load_module("r154_field", os.path.join(R154, "r154-ttab-encoded-scan.py"))


def iter_iffs():
    """Yield (physical-container-name, bytes) without materializing a cache."""
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames.sort(key=str.lower)
        for filename in sorted(filenames, key=str.lower):
            lower = filename.lower()
            path = os.path.join(dirpath, filename)
            display = os.path.relpath(path, ROOT)
            if lower.endswith((".iff", ".fam")):
                try:
                    with open(path, "rb") as source:
                        yield display, source.read()
                except OSError:
                    continue
            elif lower.endswith(".far"):
                try:
                    with open(path, "rb") as source:
                        for member, data in IFF.far_members(path, source):
                            if member.lower().endswith((".iff", ".fam")):
                                yield display + "!" + member, data
                except OSError:
                    continue


def parse_ttab(body):
    """Return (action, test, TTA index, flags, motive deltas) entries."""
    if len(body) < 4:
        return []
    count, version = struct.unpack_from("<HH", body, 0)
    if count == 0 or count > 500 or version <= 3:
        return []

    offset = 4
    encoded = False
    if 9 <= version <= 10:
        if offset >= len(body):
            return []
        encoded = body[offset] == 1
        offset += 1

    result = []
    try:
        if encoded:
            reader = FIELD.BitReader(body, 5)
            for _ in range(count):
                action = reader.u16()
                test = reader.u16()
                motive_count = reader.u32()
                flags = reader.u32()
                tta_index = reader.u32()
                if motive_count > 64:
                    return []
                if version > 6:
                    reader.u32()  # attenuation code
                reader.u32()  # attenuation float bit pattern
                reader.u32()  # autonomy threshold
                reader.i32()  # joining index
                deltas = []
                for _ in range(motive_count):
                    if version > 6:
                        reader.i16()  # effect-range minimum
                    deltas.append(reader.i16())
                    if version > 6:
                        reader.u16()  # personality modifier
                if version > 9:
                    reader.u32()  # TSO flags (present in the original v10 form)
                result.append((action, test, tta_index, flags, deltas))
        else:
            for _ in range(count):
                action, test = struct.unpack_from("<HH", body, offset)
                offset += 4
                motive_count, flags, tta_index = struct.unpack_from("<III", body, offset)
                offset += 12
                if motive_count > 64:
                    return []
                if version > 6:
                    offset += 4  # attenuation code
                offset += 12  # attenuation float, autonomy threshold, joining index
                deltas = []
                for _ in range(motive_count):
                    if version > 6:
                        offset += 2  # effect-range minimum
                    deltas.append(struct.unpack_from("<h", body, offset)[0])
                    offset += 2
                    if version > 6:
                        offset += 2  # personality modifier
                if version > 9:
                    offset += 4
                result.append((action, test, tta_index, flags, deltas))
    except (EOFError, IndexError, struct.error):
        return []
    return result


def scan():
    records = []
    dynamic_bhavs = 0
    dynamic_containers = set()

    for display, data in iter_iffs():
        if not data.startswith(b"IFF FILE"):
            continue
        bhavs = {}
        ttabs = []
        for tag, offset, size, chunk_id, flags, label, data_start in IFF.walk_chunks(data):
            body = data[data_start:offset + size]
            if tag == "BHAV":
                parsed = IFF.parse_bhav(data, data_start)
                if parsed is not None:
                    bhavs[chunk_id] = (label, parsed["instrs"])
            elif tag == "TTAB":
                for entry in parse_ttab(body):
                    ttabs.append((chunk_id, label, entry))
        records.append((display, bhavs, ttabs))
        for _, instructions in bhavs.values():
            if any(instruction[0] == 50 for instruction in instructions):
                dynamic_bhavs += 1
                dynamic_containers.add(display)

    hits = []
    for display, bhavs, ttabs in records:
        for table_id, table_label, entry in ttabs:
            action, test, tta_index, flags, deltas = entry
            if not any(delta != 0 for delta in deltas):
                continue
            if not (4096 <= test < 8192):
                continue  # conservative direct/local-only reach proof
            test_bhav = bhavs.get(test)
            if test_bhav is None or not any(ins[0] == 50 for ins in test_bhav[1]):
                continue
            motives = ",".join("%d:%d" % (index, delta)
                               for index, delta in enumerate(deltas) if delta != 0)
            hits.append((display, table_id, table_label, tta_index, action, test,
                         test_bhav[0], motives))

    hits.sort(key=lambda item: tuple(str(value).lower() for value in item))
    lines = [
        "R163 FREE-WILL DYNAMIC-VARIANT CORPUS SCAN",
        "scope: every physical loose/FAR-member IFF record; shadowed duplicate records retained",
        "parser: R154 resynchronizing IFF/FAR reader + TTAB v4-v10 normal/field-code mirror",
        "corpus IFF records: %d" % len(records),
        "BHAVs containing opcode 50 (Change Action String): %d" % dynamic_bhavs,
        "physical containers containing opcode 50: %d" % len(dynamic_containers),
        "direct local auto-TTAB tests containing opcode 50: %d" % len(hits),
        "",
    ]
    for hit in hits:
        display, table_id, table_label, tta_index, action, test, test_label, motives = hit
        lines.append("%s | TTAB %d %r | tta=%d action=%d test=%d %r | motive-delta=%s" %
                     (display, table_id, table_label, tta_index, action, test,
                      test_label, motives))
    lines.extend([
        "",
        "Interpretation: this is a lower bound. Indirect/called Change Action String tests and",
        "semiglobal/global test roots are intentionally not credited to the 16-entry reach total.",
    ])
    return "\n".join(lines) + "\n"


if __name__ == "__main__":
    report = scan()
    print(report, end="")
    with open(OUTPUT, "w", encoding="utf-8", newline="\n") as output:
        output.write(report)
