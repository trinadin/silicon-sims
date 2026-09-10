#!/usr/bin/env python3
"""R168: find every TS1 Generic Sims Call mode 14 in the owned corpus.

The scan reuses R167's production-format IFF/FAR readers, walks loose IFF/FAM
files plus one nested FAR level, and pins each caller and every incoming Temp0
setup edge. It emits metadata only.
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
R167_SCANNER = os.path.join(
    REPO, "tools", "iff-dump", "r167", "r167-generic-call13-scan.py"
)
OUT = os.path.join(HERE, "r168-generic-call14.txt")

spec = importlib.util.spec_from_file_location("r167_call13_scan", R167_SCANNER)
if spec is None or spec.loader is None:
    raise RuntimeError(f"cannot load {R167_SCANNER}")
r167 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(r167)

MODE_OPERAND = bytes.fromhex("0e 00 00 00 00 00 00 00")
PARAM0_OPERAND = bytes.fromhex("00 00 00 00 00 05 08 09")
LITERAL2_OPERAND = bytes.fromhex("00 00 02 00 00 05 08 07")


def walk_chunks_strict(data: bytes):
    """Yield every declared IFF chunk and reject truncation or trailing bytes."""
    if not data.startswith(b"IFF FILE"):
        raise ValueError("candidate does not start with the IFF FILE signature")
    offset = 64
    while offset < len(data):
        if offset + 76 > len(data):
            raise ValueError(
                f"truncated IFF chunk header at {offset:#x}/{len(data):#x}"
            )
        tag = data[offset : offset + 4].decode("latin1")
        size = struct.unpack_from(">I", data, offset + 4)[0]
        if size < 76:
            raise ValueError(f"invalid {tag} chunk size {size} at {offset:#x}")
        if offset + size > len(data):
            raise ValueError(
                f"{tag} chunk spills past EOF: {offset:#x}+{size:#x}>{len(data):#x}"
            )
        chunk_id, flags = struct.unpack_from(">HH", data, offset + 8)
        label = (
            data[offset + 12 : offset + 76]
            .split(b"\0", 1)[0]
            .decode("latin1", "replace")
        )
        yield tag, offset, size, chunk_id, flags, label, offset + 76
        offset += size
    if offset != len(data):
        raise ValueError(f"IFF ended at {offset:#x}, expected {len(data):#x}")


def tuning_operand(index: int) -> bytes:
    return struct.pack("<hhBBBB", 0, index, 0, 5, 8, 26)


def setup_text(operand: bytes) -> str:
    lhs_data, rhs_data, signed, operator, lhs_owner, rhs_owner = struct.unpack(
        "<hhBBBB", operand
    )
    if (lhs_data, signed, operator, lhs_owner) != (0, 0, 5, 8):
        raise AssertionError(f"not a Temp0 assignment: {operand.hex(' ')}")
    if (rhs_owner, rhs_data) == (9, 0):
        return "Temp0 := Param0"
    if (rhs_owner, rhs_data) == (7, 2):
        return "Temp0 := literal 2"
    if rhs_owner == 26:
        return f"Temp0 := local tuning {rhs_data:#x}"
    raise AssertionError(f"unexpected mode-14 setup: {operand.hex(' ')}")


def scan_iff(display: str, data: bytes, hits: list[dict[str, object]]) -> int:
    count = 0
    for tag, _off, _size, chunk_id, _flags, label, data_start in walk_chunks_strict(data):
        if tag != "BHAV":
            continue
        bhav = r167.r154.parse_bhav(data, data_start)
        if bhav is None:
            continue
        count += 1
        instructions = bhav["instrs"]
        for index, instruction in enumerate(instructions):
            raw_operand = r167.operand_bytes(instruction)
            if instruction[0] != 1 or raw_operand[0] != 14:
                continue
            incoming = [
                (source, item)
                for source, item in enumerate(instructions)
                if item[1] == index or item[2] == index
            ]
            hits.append(
                {
                    "container": display,
                    "bhav": chunk_id,
                    "label": label,
                    "index": index,
                    "true": instruction[1],
                    "false": instruction[2],
                    "operand": raw_operand,
                    "incoming": incoming,
                    "routine_sha": hashlib.sha256(
                        b"".join(
                            struct.pack(
                                "<HBBhhhh",
                                item[0],
                                item[1],
                                item[2],
                                *item[3:],
                            )
                            for item in instructions
                        )
                    ).hexdigest(),
                    "instructions": len(instructions),
                }
            )
    return count


def expected_hits() -> list[tuple[object, ...]]:
    return [
        (
            "Downloads/Jukebox/Jukebox.far!JukeBox.iff",
            4118,
            "SubSwitch",
            9,
            8,
            253,
            12,
            "ba85fea91d4aa9eedfe4f3d903091013f2423e53d9736d21850875089a39f348",
            ((2, 2, 9, 253, PARAM0_OPERAND),),
        ),
        (
            "Downloads/Jukebox/Jukebox.far!JukeBox.iff",
            4152,
            "Turn On Specific",
            12,
            8,
            253,
            15,
            "150123c183e8881572bf7e339ae72402abb13859b6b3353b8a2585929e6baa87",
            (
                (11, 2, 12, 253, tuning_operand(0x281)),
                (14, 2, 12, 253, tuning_operand(0x287)),
            ),
        ),
        (
            "ExpansionPack/ExpansionPack.far!piano2.iff",
            4118,
            "play piano npc",
            25,
            28,
            253,
            29,
            "48a738287c1df63cd376f287a32a5609672e9134619e5829ac9a5cf2b9eeeea8",
            ((24, 2, 25, 253, LITERAL2_OPERAND),),
        ),
        (
            "ExpansionPack2/ExpansionPack2.far!DJBooth.iff",
            4136,
            "Station Change",
            3,
            2,
            253,
            5,
            "4cbac74b250728b5beef7ad5a19c6cd7515efe855d4b961574d4ad91a4f588a8",
            ((0, 2, 3, 253, PARAM0_OPERAND),),
        ),
        (
            "ExpansionPack2/ExpansionPack2.far!StereoSpeakers.iff",
            4136,
            "Station Change",
            3,
            2,
            253,
            5,
            "4cbac74b250728b5beef7ad5a19c6cd7515efe855d4b961574d4ad91a4f588a8",
            ((0, 2, 3, 253, PARAM0_OPERAND),),
        ),
        (
            "ExpansionPack3/ExpansionPack3.far!Piano3.iff",
            4118,
            "play piano npc",
            25,
            28,
            253,
            29,
            "48a738287c1df63cd376f287a32a5609672e9134619e5829ac9a5cf2b9eeeea8",
            ((24, 2, 25, 253, LITERAL2_OPERAND),),
        ),
        (
            "ExpansionPack3/ExpansionPack3.far!Stereos2.iff",
            4096,
            "main",
            9,
            10,
            253,
            29,
            "374194cae7db23951cfb0cbe99445f29b26b0b9e392805aeec13886c5f810db8",
            (
                (8, 2, 9, 253, tuning_operand(0x289)),
                (16, 2, 9, 253, tuning_operand(0x28A)),
            ),
        ),
        (
            "ExpansionPack3/ExpansionPack3.far!Stereos2.iff",
            4118,
            "SubSwitch",
            8,
            7,
            253,
            11,
            "19b1469784c1ca5c1569a0d9cad1ec52428e55e6adf8d0a7e20f34e278179179",
            ((1, 2, 8, 253, PARAM0_OPERAND),),
        ),
        (
            "ExpansionPack5/ExpansionPack5.FAR!stereowallunleashed.iff",
            4096,
            "main",
            8,
            9,
            253,
            34,
            "759586e5aacc00fb45fabbd2b170250ebe5bb8c6d276b11d914e3bdf69cdb3e9",
            (
                (14, 2, 8, 253, tuning_operand(0x28B)),
                (33, 2, 8, 253, tuning_operand(0x28C)),
            ),
        ),
        (
            "ExpansionPack5/ExpansionPack5.FAR!stereowallunleashed.iff",
            4118,
            "SubSwitch",
            8,
            7,
            253,
            11,
            "19b1469784c1ca5c1569a0d9cad1ec52428e55e6adf8d0a7e20f34e278179179",
            ((1, 2, 8, 253, PARAM0_OPERAND),),
        ),
        (
            "ExpansionPack6/ExpansionPack6.far!PianoSuperstar.iff",
            4118,
            "play piano npc",
            25,
            28,
            253,
            29,
            "48a738287c1df63cd376f287a32a5609672e9134619e5829ac9a5cf2b9eeeea8",
            ((24, 2, 25, 253, LITERAL2_OPERAND),),
        ),
        (
            "ExpansionPack6/ExpansionPack6.far!StereoSpeakersSuperstar.iff",
            4096,
            "main",
            17,
            2,
            253,
            18,
            "7dae1948c2265cdb018fe15a5b1049ebcebf1f472111956da3da30722cedfa2d",
            ((16, 2, 17, 253, tuning_operand(0x309)),),
        ),
        (
            "ExpansionPack6/ExpansionPack6.far!StereoSpeakersSuperstar.iff",
            4136,
            "Station Change",
            3,
            2,
            253,
            5,
            "4cbac74b250728b5beef7ad5a19c6cd7515efe855d4b961574d4ad91a4f588a8",
            ((0, 2, 3, 253, PARAM0_OPERAND),),
        ),
        (
            "ExpansionPack6/ExpansionPack6.far!StereoSuperstar.iff",
            4118,
            "SubSwitch",
            8,
            7,
            253,
            11,
            "d695822167dc9e2688458d5c21f8ec54c3d324385a5fa76899e8aff4f2e7c004",
            ((1, 2, 8, 253, PARAM0_OPERAND),),
        ),
        (
            "ExpansionPack6/ExpansionPack6.far!StereoWallSuperStar.iff",
            4096,
            "main",
            8,
            9,
            253,
            34,
            "1f6f77a10dca1b657eadfba898a74f85e51ee622564b12b0621fa71eaad10c61",
            (
                (14, 2, 8, 253, tuning_operand(0x28B)),
                (33, 2, 8, 253, tuning_operand(0x28D)),
            ),
        ),
        (
            "ExpansionPack6/ExpansionPack6.far!StereoWallSuperStar.iff",
            4118,
            "SubSwitch",
            8,
            7,
            253,
            11,
            "19b1469784c1ca5c1569a0d9cad1ec52428e55e6adf8d0a7e20f34e278179179",
            ((1, 2, 8, 253, PARAM0_OPERAND),),
        ),
        (
            "GameData/Objects/Objects.far!piano.iff",
            4117,
            "play piano npc",
            25,
            27,
            253,
            29,
            "f98ed5256be62aa657cbfd4f10c01b55295809209154bc6c631b21969bb27aa8",
            ((24, 2, 25, 253, LITERAL2_OPERAND),),
        ),
        (
            "GameData/Objects/Objects.far!Stereos.iff",
            4118,
            "SubSwitch",
            8,
            7,
            253,
            11,
            "d695822167dc9e2688458d5c21f8ec54c3d324385a5fa76899e8aff4f2e7c004",
            ((1, 2, 8, 253, PARAM0_OPERAND),),
        ),
    ]


def main() -> None:
    hits: list[dict[str, object]] = []
    files_walked = 0
    iffs_scanned = 0
    bhavs_scanned = 0
    deeper_nested_fars = 0

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
                    for member, data in r167.r154.far_members(path, handle):
                        if member.lower().endswith(".far"):
                            nested = io.BytesIO(data)
                            for nested_member, nested_data in r167.r154.far_members(
                                member, nested
                            ):
                                nested_base = nested_member.lower()
                                if nested_base.endswith(".far"):
                                    deeper_nested_fars += 1
                                elif nested_base.endswith((".iff", ".fam")):
                                    iffs_scanned += 1
                                    bhavs_scanned += scan_iff(
                                        f"{rel}!{member}!{nested_member}",
                                        nested_data,
                                        hits,
                                    )
                        elif member.lower().endswith((".iff", ".fam")):
                            iffs_scanned += 1
                            bhavs_scanned += scan_iff(f"{rel}!{member}", data, hits)
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
    actual_hits = [
        (
            hit["container"],
            hit["bhav"],
            hit["label"],
            hit["index"],
            hit["true"],
            hit["false"],
            hit["instructions"],
            hit["routine_sha"],
            tuple(
                (
                    source,
                    item[0],
                    item[1],
                    item[2],
                    r167.operand_bytes(item),
                )
                for source, item in hit["incoming"]
            ),
        )
        for hit in hits
    ]
    wanted_hits = expected_hits()
    if actual_hits != wanted_hits:
        raise AssertionError(
            "mode-14 caller census changed:\n"
            f"expected={wanted_hits!r}\nactual={actual_hits!r}"
        )
    if (files_walked, iffs_scanned, bhavs_scanned) != (8871, 2238, 28246):
        raise AssertionError(
            "corpus census changed: "
            f"{files_walked}/{iffs_scanned}/{bhavs_scanned}"
        )
    if deeper_nested_fars != 0:
        raise AssertionError(
            f"scanner depth insufficient: found {deeper_nested_fars} deeper nested FARs"
        )

    setup_counts: Counter[str] = Counter()
    setup_paths = 0
    for hit in hits:
        if hit["operand"] != MODE_OPERAND or hit["false"] != 253:
            raise AssertionError(
                f"unexpected mode-14 call shape in {hit['container']} "
                f"BHAV {hit['bhav']}"
            )
        types = set()
        for _source, instruction in hit["incoming"]:
            operand = r167.operand_bytes(instruction)
            description = setup_text(operand)
            types.add(description.split(" := ", 1)[1].split(" ", 1)[0])
            setup_paths += 1
        if types == {"Param0"}:
            setup_counts["parameter caller sites"] += 1
        elif types == {"literal"}:
            setup_counts["literal caller sites"] += 1
        elif types == {"local"}:
            setup_counts["tuning caller sites"] += 1
        else:
            raise AssertionError(f"mixed/unexpected setup types: {types}")
    if setup_counts != {
        "parameter caller sites": 9,
        "literal caller sites": 4,
        "tuning caller sites": 5,
    } or setup_paths != 22:
        raise AssertionError(
            f"mode-14 setup census changed: {setup_counts}, paths={setup_paths}"
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
    unique_hashes = {str(hit["routine_sha"]) for hit in hits}
    containers = {str(hit["container"]) for hit in hits}
    if (len(signatures), len(unique_hashes), len(containers)) != (11, 11, 13):
        raise AssertionError(
            "mode-14 uniqueness changed: "
            f"signatures={len(signatures)} hashes={len(unique_hashes)} "
            f"containers={len(containers)}"
        )

    lines = [
        "# R168 TS1 Generic Sims Call mode 14 corpus scan",
        f"# corpus: {CORPUS}",
        "# match law: BHAV opcode == 1 and first operand byte == 14",
        "# call operand: 0e 00 00 00 00 00 00 00",
        (
            f"# files walked: {files_walked}; IFFs scanned: {iffs_scanned}; "
            f"BHAV chunks: {bhavs_scanned}"
        ),
        "# strict structure: every IFF consumed exactly; deeper nested FARs: 0",
        (
            f"# call sites: {len(hits)} instances in {len(containers)} IFFs; "
            f"{len(signatures)} unique call-site signatures; "
            f"{len(unique_hashes)} unique routine instruction payload hashes"
        ),
        (
            "# setup: 9 parameter sites, 4 literal-2 sites, 5 tuning sites; "
            "22 exact incoming Temp0 assignment paths"
        ),
        "# all calls use only the true successor; every false pointer is 253",
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
            for source, instruction in hit["incoming"]:
                operand = r167.operand_bytes(instruction)
                lines.append(
                    f"        incoming instruction[{source}] T={instruction[1]} "
                    f"F={instruction[2]} operand={operand.hex(' ')} "
                    f"({setup_text(operand)})"
                )

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
