#!/usr/bin/env python3
"""R166: find every TS1 Generic Sims Call mode 12 in the owned corpus.

TS1 registers primitive opcode 1 as generic_sims_call. Its first operand
byte is the call mode, so mode 12 is an instruction with opcode 1 and
operand[0] == 12. This scanner walks loose IFF/FAM files, FAR members, and
one level of nested FARs under game-data/The Sims.

The FAR and BHAV readers are reused from R154's full-corpus scanner. Chunk
headers deliberately follow the production IffFile reader instead: fields
are big-endian and the next chunk begins at the exact declared size.
"""

from __future__ import annotations

import hashlib
import importlib.util
import io
import os
import struct
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
CORPUS = os.path.join(REPO, "game-data", "The Sims")
R154_SCANNER = os.path.join(
    REPO, "tools", "iff-dump", "r154", "r154-callers-scan.py"
)
OUT = os.path.join(HERE, "r166-generic-call12.txt")

spec = importlib.util.spec_from_file_location("r154_callers_scan", R154_SCANNER)
if spec is None or spec.loader is None:
    raise RuntimeError(f"cannot load {R154_SCANNER}")
r154 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(r154)


def operand_bytes(instruction: tuple[int, ...]) -> bytes:
    return struct.pack("<hhhh", *instruction[3:])


def walk_chunks(data: bytes):
    """Yield production-format IFF chunk headers without heuristic resync."""
    offset = 64
    while offset + 76 <= len(data):
        tag = data[offset : offset + 4].decode("latin1")
        size = struct.unpack_from(">I", data, offset + 4)[0]
        if size < 76 or offset + size > len(data):
            return
        chunk_id, flags = struct.unpack_from(">HH", data, offset + 8)
        label = (
            data[offset + 12 : offset + 76]
            .split(b"\0", 1)[0]
            .decode("latin1", "replace")
        )
        yield tag, offset, size, chunk_id, flags, label, offset + 76
        offset += size


def scan_iff(display: str, data: bytes, hits: list[dict[str, object]]) -> int:
    if not data.startswith(b"IFF FILE"):
        return 0
    count = 0
    for tag, _off, _size, chunk_id, _flags, label, data_start in walk_chunks(data):
        if tag != "BHAV":
            continue
        bhav = r154.parse_bhav(data, data_start)
        if bhav is None:
            continue
        count += 1
        for index, instruction in enumerate(bhav["instrs"]):
            raw_operand = operand_bytes(instruction)
            if instruction[0] == 1 and raw_operand[0] == 12:
                hits.append(
                    {
                        "container": display,
                        "bhav": chunk_id,
                        "label": label,
                        "index": index,
                        "true": instruction[1],
                        "false": instruction[2],
                        "operand": raw_operand,
                        "routine_sha": hashlib.sha256(
                            b"".join(
                                struct.pack(
                                    "<HBBhhhh",
                                    item[0],
                                    item[1],
                                    item[2],
                                    *item[3:],
                                )
                                for item in bhav["instrs"]
                            )
                        ).hexdigest(),
                        "instructions": len(bhav["instrs"]),
                    }
                )
    return count


def main() -> None:
    hits: list[dict[str, object]] = []
    files_walked = 0
    iffs_scanned = 0
    bhavs_scanned = 0

    paths: list[str] = []
    for dirpath, _dirnames, filenames in os.walk(CORPUS):
        for filename in sorted(filenames):
            paths.append(os.path.join(dirpath, filename))

    for path in paths:
        files_walked += 1
        rel = os.path.relpath(path, CORPUS)
        base = os.path.basename(path).lower()
        try:
            if base.endswith(".far"):
                with open(path, "rb") as handle:
                    for member, data in r154.far_members(path, handle):
                        if member.lower().endswith(".far"):
                            nested = io.BytesIO(data)
                            for nested_member, nested_data in r154.far_members(
                                member, nested
                            ):
                                if nested_member.lower().endswith(".iff"):
                                    iffs_scanned += 1
                                    bhavs_scanned += scan_iff(
                                        f"{rel}!{member}!{nested_member}",
                                        nested_data,
                                        hits,
                                    )
                        elif member.lower().endswith(".iff"):
                            iffs_scanned += 1
                            bhavs_scanned += scan_iff(
                                f"{rel}!{member}", data, hits
                            )
            elif base.endswith((".iff", ".fam")):
                with open(path, "rb") as handle:
                    data = handle.read()
                iffs_scanned += 1
                bhavs_scanned += scan_iff(rel, data, hits)
        except (OSError, EOFError, struct.error, ValueError) as error:
            raise RuntimeError(f"failed while scanning {rel}: {error}") from error

    hits.sort(
        key=lambda item: (
            str(item["container"]).lower(),
            int(item["bhav"]),
            int(item["index"]),
        )
    )
    signatures: dict[tuple[object, ...], list[dict[str, object]]] = defaultdict(list)
    for hit in hits:
        key = (
            hit["bhav"],
            hit["label"],
            hit["index"],
            hit["true"],
            hit["false"],
            hit["operand"],
            hit["routine_sha"],
        )
        signatures[key].append(hit)
    routine_hashes = {str(hit["routine_sha"]) for hit in hits}

    lines = [
        "# R166 TS1 Generic Sims Call mode 12 corpus scan",
        f"# corpus: {CORPUS}",
        "# match law: BHAV opcode == 1 and first operand byte == 12",
        (
            f"# files walked: {files_walked}; IFFs scanned: {iffs_scanned}; "
            f"BHAV chunks: {bhavs_scanned}"
        ),
        (
            f"# call sites: {len(hits)} instances; "
            f"{len(signatures)} unique call-site signatures; "
            f"{len(routine_hashes)} unique routine instruction payload hashes"
        ),
        "",
        "## unique call-site signatures",
    ]
    for number, (key, instances) in enumerate(
        sorted(signatures.items(), key=lambda item: repr(item[0])), 1
    ):
        bhav, label, index, true_branch, false_branch, raw_operand, sha = key
        lines.extend(
            [
                (
                    f"[{number}] BHAV {bhav} {label!r} instruction[{index}] "
                    f"T={true_branch} F={false_branch} operand="
                    f"{bytes(raw_operand).hex(' ')}"
                ),
                (
                    f"    routine instruction payload: "
                    f"instructions={instances[0]['instructions']} sha256={sha}"
                ),
                f"    instances={len(instances)}",
            ]
        )
        for hit in instances:
            lines.append(f"      {hit['container']}")

    lines.extend(["", "## container-family counts"])
    families = Counter(str(hit["container"]).split("!", 1)[0] for hit in hits)
    for family, count in sorted(families.items()):
        lines.append(f"{count:4d}  {family}")

    report = "\n".join(lines) + "\n"
    with open(OUT, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(report)
    print(report, end="")


if __name__ == "__main__":
    main()
