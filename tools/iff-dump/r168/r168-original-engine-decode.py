#!/usr/bin/env python3
"""R168: verify the original engine's Generic Sims Call mode-14 law.

The script reads the owner's original PPC PEF in place. It unpacks the PEF
data section, resolves jump-table entries 14/15, pins the pointer chain and
SetGlobal helper, and emits metadata only.
"""

from __future__ import annotations

import hashlib
import json
import os
import struct

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
ENGINE = os.path.join(REPO, "game-data", "The Sims", "The Sims Complete")
SYMBOLS = os.path.join(REPO, "tools", "iff-dump", "r153", "r153-symbols.json")
OUT = os.path.join(HERE, "r168-original-engine-decode.txt")

CODE_FILE_BASE = 0x8E90
DATA_CONTAINER_OFFSET = 0x5C22F0
DATA_CONTAINER_LENGTH = 0x6D834
DATA_UNPACKED_LENGTH = 0x7BF80
TOC_BASE = 0x8000
EXPECTED_ENGINE_SHA256 = (
    "33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f"
)
EXPECTED_UNPACKED_SHA256 = (
    "f740c1dfa1dea8187b12ebf222ca1a39223365d26ad5d2f2e353d5efba247be3"
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
    section = unpack_pef_data(packed)
    if len(section) != DATA_UNPACKED_LENGTH:
        raise AssertionError(
            f"PEF data length {len(section):#x} != {DATA_UNPACKED_LENGTH:#x}"
        )
    unpacked_sha = hashlib.sha256(section).hexdigest()
    if unpacked_sha != EXPECTED_UNPACKED_SHA256:
        raise AssertionError(
            f"PEF data drift: {unpacked_sha} != {EXPECTED_UNPACKED_SHA256}"
        )

    switch_table = word(section, TOC_BASE - 0x59A0)
    case_14_virtual = word(section, switch_table + 14 * 4)
    case_15_virtual = word(section, switch_table + 15 * 4)
    case_14_file = CODE_FILE_BASE + case_14_virtual
    case_15_file = CODE_FILE_BASE + case_15_virtual
    expected_dispatch = (0x485A8, 0xE98AC, 0xF273C, 0xE98C4, 0xF2754)
    actual_dispatch = (
        switch_table,
        case_14_virtual,
        case_14_file,
        case_15_virtual,
        case_15_file,
    )
    if actual_dispatch != expected_dispatch:
        raise AssertionError(
            f"mode-14 dispatch drift: {actual_dispatch!r} != {expected_dispatch!r}"
        )

    with open(SYMBOLS, encoding="utf-8") as handle:
        symbols = {item["name"]: item["start"] for item in json.load(handle)}
    symbol_expect = {
        "TryGenericSimCall__8cXObjectFP9StackElemP10XPrimParam": 0xF1F20,
        "SetGlobal__10cSimulatorFss": 0x138C20,
        "__ct__8cXObjectFP11ObjSelectorP12ObjectModule": 0xCAB50,
        "__ct__12ObjectModuleFP10cSimulator": 0xE8B10,
    }
    for name, address in symbol_expect.items():
        if symbols.get(name) != address:
            raise AssertionError(
                f"symbol drift for {name}: {symbols.get(name)!r} != {address:#x}"
            )

    case_words = verify_words(
        engine,
        {
            0xF273C: 0x807F00DC,
            0xF2740: 0x3880001F,
            0xF2744: 0xA8BF003A,
            0xF2748: 0x80630080,
            0xF274C: 0x480464D5,
            0xF2750: 0x480019CC,
        },
        "TryGenericSimCall mode 14",
    )
    helper_words = verify_words(
        engine,
        {
            0x138C20: 0x7C800734,
            0x138C24: 0x5400083C,
            0x138C28: 0x7C630214,
            0x138C2C: 0xB0A30010,
            0x138C30: 0x4E800020,
        },
        "cSimulator::SetGlobal",
    )
    return_words = verify_words(
        engine,
        {
            0xF1F30: 0x7C7F1B78,
            0xF1F44: 0x3B800001,
            0xF411C: 0x7F83E378,
            0xF4138: 0x4E800020,
        },
        "TryGenericSimCall true return",
    )
    dispatch_words = verify_words(
        engine,
        {
            0xF1F68: 0xA8050000,
            0xF1F6C: 0x2800002B,
            0xF1F70: 0x41812194,
            0xF1F74: 0x8082A660,
            0xF1F78: 0x5400103A,
            0xF1F7C: 0x7C84002E,
            0xF1F80: 0x7C8903A6,
            0xF1F84: 0x4E800420,
        },
        "TryGenericSimCall dispatch",
    )
    constructor_words = verify_words(
        engine,
        {
            # cXObject(this=r3, selector=r4, ObjectModule=r5):
            # retain this in r31 and ObjectModule in r30, then store r30 at +0xdc.
            0xCAB58: 0x3BE30000,
            0xCAB60: 0x3BC50000,
            0xCABEC: 0x93DF00DC,
            # ObjectModule(this=r3, cSimulator=r4): retain the simulator in
            # r31 and this in r30, then store r31 at +0x80.
            0xE8B18: 0x3BE40000,
            0xE8B20: 0x3BC30000,
            0xE8C34: 0x93FE0080,
        },
        "mode-14 constructor pointer provenance",
    )

    mode_region = engine[case_14_file:case_15_file]
    helper_region = engine[0x138C20:0x138C34]
    mode_sha = hashlib.sha256(mode_region).hexdigest()
    helper_sha = hashlib.sha256(helper_region).hexdigest()
    if mode_sha != "c6a1b6222721ef0a9f8d090fb71c5e4ae24471f6214d3804d35b320577424a44":
        raise AssertionError(f"mode-14 code drift: {mode_sha}")
    if helper_sha != "0f5525dcf5c237f281b90a7cb88ea76e1e468711f4b0c8159ea501a30faf6e6e":
        raise AssertionError(f"SetGlobal code drift: {helper_sha}")

    lines = [
        "# R168 original-engine decode: Generic Sims Call mode 14",
        f"engine sha256: {engine_sha}",
        (
            f"PEF packed data: file+{DATA_CONTAINER_OFFSET:#x} length "
            f"{DATA_CONTAINER_LENGTH:#x} -> {len(section):#x}"
        ),
        f"PEF unpacked data sha256: {unpacked_sha}",
        (
            f"dispatch: TOC[-0x59a0] -> data+{switch_table:#x}; "
            f"entry[14] -> code-virtual {case_14_virtual:#x} -> "
            f"file {case_14_file:#x}; entry[15] -> file {case_15_file:#x}"
        ),
        (
            f"code sha256: mode14[{case_14_file:#x},{case_15_file:#x})="
            f"{mode_sha}"
        ),
        "code sha256: cSimulator::SetGlobal[0x138c20,0x138c34)="
        + helper_sha,
        "",
        "recovered law:",
        "  r31 = the cXObject receiver; the StackElem argument is not used",
        "  ObjectModule = *(cXObject + 0xdc)",
        "  cSimulator = *(ObjectModule + 0x80)",
        "  signed Temp0 = *(int16_t *)(cXObject + 0x3a)",
        "  cSimulator::SetGlobal(index=31, value=Temp0)",
        "  SetGlobal stores the 16-bit value at cSimulator + 0x10 + 2*index",
        "  therefore global[31] (cSimulator + 0x4e) is overwritten by Temp0",
        "  the case performs no comparison and does not inspect Stack Object",
        "  TryGenericSimCall takes its common true return",
        "",
        "decisive TryGenericSimCall dispatch words:",
        *[f"  {line}" for line in dispatch_words],
        "",
        "decisive TryGenericSimCall mode-14 words:",
        *[f"  {line}" for line in case_words],
        "",
        "decisive cSimulator::SetGlobal words:",
        *[f"  {line}" for line in helper_words],
        "",
        "constructor argument/store words proving the pointer chain:",
        *[f"  {line}" for line in constructor_words],
        "",
        "common-true-return words:",
        *[f"  {line}" for line in return_words],
    ]
    report = "\n".join(lines) + "\n"
    with open(OUT, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(report)
    print(report, end="")


if __name__ == "__main__":
    main()
