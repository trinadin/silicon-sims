#!/usr/bin/env python3
"""R167: verify the original engine's Generic Sims Call mode-13 law.

The script reads the owner's original PPC PEF in place. It unpacks the PEF
data section in memory, resolves case 13 through the original jump table,
checks decisive words in TryGenericSimCall, ObjectModule::CleanupPeople,
cXObject::Cleanup, and cXPerson::Cleanup, and emits metadata only.
"""

from __future__ import annotations

import hashlib
import json
import os
import struct

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
ENGINE = os.path.join(REPO, "game-data", "The Sims", "The Sims Complete")
SYMBOLS = os.path.join(
    REPO, "tools", "iff-dump", "r153", "r153-symbols.json"
)
OUT = os.path.join(HERE, "r167-original-engine-decode.txt")

CODE_FILE_BASE = 0x8E90
DATA_CONTAINER_OFFSET = 0x5C22F0
DATA_CONTAINER_LENGTH = 0x6D834
DATA_UNPACKED_LENGTH = 0x7BF80
TOC_BASE = 0x8000
EXPECTED_ENGINE_SHA256 = (
    "33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f"
)


def read_varint(data: bytes, offset: int) -> tuple[int, int]:
    value = 0
    while True:
        byte = data[offset]
        offset += 1
        value = ((value << 7) | (byte & 0x7F)) & 0xFFFFFFFF
        if not byte & 0x80:
            return value, offset


def unpack_pef_data(data: bytes) -> bytes:
    """Decode PEF packed data per Apple's opcode/count format."""
    result = bytearray()
    offset = 0
    while offset < len(data):
        opcode_byte = data[offset]
        offset += 1
        opcode = opcode_byte >> 5
        count = opcode_byte & 0x1F
        if count == 0:
            count, offset = read_varint(data, offset)

        if opcode == 0:
            result.extend(b"\0" * count)
        elif opcode == 1:
            result.extend(data[offset : offset + count])
            offset += count
        elif opcode == 2:
            repeat_count, offset = read_varint(data, offset)
            block = data[offset : offset + count]
            offset += count
            result.extend(block * (repeat_count + 1))
        elif opcode in (3, 4):
            custom_size, offset = read_varint(data, offset)
            repeat_count, offset = read_varint(data, offset)
            if opcode == 3:
                common = data[offset : offset + count]
                offset += count
            else:
                common = b"\0" * count
            result.extend(common)
            for _ in range(repeat_count):
                result.extend(data[offset : offset + custom_size])
                offset += custom_size
                result.extend(common)
        else:
            raise ValueError(
                f"reserved PEF opcode {opcode} at packed offset {offset - 1:#x}"
            )
    return bytes(result)


def word(data: bytes, offset: int) -> int:
    return struct.unpack_from(">I", data, offset)[0]


def verify_words(
    engine: bytes, expected: dict[int, int], label: str
) -> list[str]:
    lines = []
    for address, wanted in sorted(expected.items()):
        actual = word(engine, address)
        if actual != wanted:
            raise AssertionError(
                f"{label} word drift at {address:#x}: {actual:08x} != {wanted:08x}"
            )
        lines.append(f"{address:#08x} {actual:08x}")
    return lines


def main() -> None:
    with open(ENGINE, "rb") as handle:
        engine = handle.read()
    engine_sha = hashlib.sha256(engine).hexdigest()
    if engine_sha != EXPECTED_ENGINE_SHA256:
        raise AssertionError(
            f"original engine drift: {engine_sha} != {EXPECTED_ENGINE_SHA256}"
        )

    packed = engine[
        DATA_CONTAINER_OFFSET : DATA_CONTAINER_OFFSET + DATA_CONTAINER_LENGTH
    ]
    if len(packed) != DATA_CONTAINER_LENGTH:
        raise AssertionError("original engine data container is truncated")
    section = unpack_pef_data(packed)
    if len(section) != DATA_UNPACKED_LENGTH:
        raise AssertionError(
            f"PEF data length {len(section):#x} != {DATA_UNPACKED_LENGTH:#x}"
        )

    switch_table = word(section, TOC_BASE - 0x59A0)
    case_13_virtual = word(section, switch_table + 13 * 4)
    case_14_virtual = word(section, switch_table + 14 * 4)
    case_13_file = CODE_FILE_BASE + case_13_virtual
    case_14_file = CODE_FILE_BASE + case_14_virtual
    if switch_table != 0x485A8:
        raise AssertionError(f"switch table drift: {switch_table:#x}")
    if case_13_virtual != 0xE9848 or case_13_file != 0xF26D8:
        raise AssertionError(
            f"case 13 target drift: virtual={case_13_virtual:#x} "
            f"file={case_13_file:#x}"
        )
    if case_14_virtual != 0xE98AC or case_14_file != 0xF273C:
        raise AssertionError(
            f"case 14 target drift: virtual={case_14_virtual:#x} "
            f"file={case_14_file:#x}"
        )

    with open(SYMBOLS, encoding="utf-8") as handle:
        symbols = {item["name"]: item["start"] for item in json.load(handle)}
    symbol_expect = {
        "TryGenericSimCall__8cXObjectFP9StackElemP10XPrimParam": 0xF1F20,
        "CleanupPeople__12ObjectModuleFP8cXObject": 0xE3BC0,
        "Cleanup__8cXObjectFP8cXObject": 0xCA0D0,
        "Cleanup__8cXPersonFP8cXObject": 0x1081D0,
    }
    for name, address in symbol_expect.items():
        if symbols.get(name) != address:
            raise AssertionError(
                f"symbol drift for {name}: {symbols.get(name)!r} != {address:#x}"
            )

    case_words = verify_words(
        engine,
        {
            0xF26D8: 0xA8180004,
            0xF26E0: 0x809F00DC,
            0xF2708: 0x7E63002E,
            0xF270C: 0x28130000,
            0xF2710: 0x4082001C,
            0xF272C: 0x807F00DC,
            0xF2730: 0x7E649B78,
            0xF2734: 0x4BFF148D,
            0xF2738: 0x480019E4,
        },
        "TryGenericSimCall mode 13",
    )
    module_words = verify_words(
        engine,
        {
            0xE3BE0: 0x83E30090,
            0xE3BEC: 0x7C03F040,
            0xE3BF0: 0x41820018,
            0xE3BF4: 0x81830028,
            0xE3BFC: 0x818C0040,
            0xE3C0C: 0x801D008C,
            0xE3C1C: 0x7C1F0040,
        },
        "ObjectModule::CleanupPeople",
    )
    object_words = verify_words(
        engine,
        {
            0xCA0D8: 0x28040000,
            0xCA0E8: 0x408201A4,
            0xCA28C: 0x80010088,
        },
        "cXObject::Cleanup",
    )
    person_words = verify_words(
        engine,
        {
            0x1081FC: 0x281F0000,
            0x108240: 0x801E0AB4,
            0x108258: 0x80050000,
            0x108280: 0x801E0A14,
            0x10865C: 0x281F0000,
            0x108660: 0x41820718,
            0x108664: 0x807E0A00,
            0x108698: 0x80040828,
            0x1086A4: 0x8004082C,
            0x108704: 0x80190828,
            0x108710: 0x8019082C,
            0x108D78: 0x3B610228,
            0x108D90: 0x4E800020,
        },
        "cXPerson::Cleanup",
    )

    regions = {
        "mode13[0xf26d8,0xf273c)": engine[0xF26D8:0xF273C],
        "CleanupPeople[0xe3bc0,0xe3c40)": engine[0xE3BC0:0xE3C40],
        "cXObject::Cleanup[0xca0d0,0xca2a0)": engine[0xCA0D0:0xCA2A0],
        "cXPerson::Cleanup[0x1081d0,0x108d94)": engine[0x1081D0:0x108D94],
    }

    lines = [
        "# R167 original-engine decode: Generic Sims Call mode 13",
        f"engine sha256: {engine_sha}",
        (
            f"PEF packed data: file+{DATA_CONTAINER_OFFSET:#x} length "
            f"{DATA_CONTAINER_LENGTH:#x} -> {len(section):#x}"
        ),
        f"PEF unpacked data sha256: {hashlib.sha256(section).hexdigest()}",
        (
            f"dispatch: TOC[-0x59a0] -> data+{switch_table:#x}; "
            f"entry[13] -> code-virtual {case_13_virtual:#x} -> "
            f"file {case_13_file:#x}; entry[14] -> file {case_14_file:#x}"
        ),
        *(
            f"code sha256: {name}={hashlib.sha256(region).hexdigest()}"
            for name, region in regions.items()
        ),
        "",
        "recovered law:",
        "  target = object resolved from Stack Object ID",
        "  ObjectModule::CleanupPeople visits every object except target",
        "  each visited object receives virtual Cleanup(target) at vtable +0x40",
        "  base cXObject::Cleanup is a no-op for non-null target",
        "  a failed target lookup reports engine error 10, then still calls CleanupPeople(null)",
        "  null Cleanup visits every object and enters broad object/person cleanup paths",
        "  for valid targets cXPerson::Cleanup detects target in current object-use/stack state",
        "  and in either 60-byte queue-entry field at +0x08 (TargetID) or +0x0c (Icon)",
        "  matching queued entries are removed and their Queue Skipped path is invoked",
        "  current object-use/stack matches enter the original current-interaction cleanup path",
        "  TryGenericSimCall takes its common true return",
        "",
        "decisive TryGenericSimCall mode-13 words:",
        *[f"  {line}" for line in case_words],
        "",
        "decisive ObjectModule::CleanupPeople words:",
        *[f"  {line}" for line in module_words],
        "",
        "decisive cXObject::Cleanup words:",
        *[f"  {line}" for line in object_words],
        "",
        "decisive cXPerson::Cleanup words:",
        *[f"  {line}" for line in person_words],
    ]
    report = "\n".join(lines) + "\n"
    with open(OUT, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(report)
    print(report, end="")


if __name__ == "__main__":
    main()
