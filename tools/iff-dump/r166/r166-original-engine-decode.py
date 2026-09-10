#!/usr/bin/env python3
"""R166: verify the original engine's Generic Sims Call mode-12 law.

The script reads the owner's original PPC PEF in place. It unpacks the PEF
data section in memory, resolves case 12 through the original jump table,
checks the decisive machine words in TryGenericSimCall and
CTilePt::BuildRotationLookup, and emits metadata/formulas only.
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
OUT = os.path.join(HERE, "r166-original-engine-decode.txt")

CODE_FILE_BASE = 0x8E90
DATA_CONTAINER_OFFSET = 0x5C22F0
DATA_CONTAINER_LENGTH = 0x6D834
DATA_UNPACKED_LENGTH = 0x7BF80
TOC_BASE = 0x8000


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


def camera_distance(tile_x: int, tile_y: int, rotation: int) -> int:
    if rotation == 1:
        rotated_x, rotated_y = 63 - tile_y, tile_x
    elif rotation == 2:
        rotated_x, rotated_y = 63 - tile_x, 63 - tile_y
    elif rotation == 3:
        rotated_x, rotated_y = tile_y, 63 - tile_x
    else:
        rotated_x, rotated_y = tile_x, tile_y
    return -(rotated_x + rotated_y)


def main() -> None:
    with open(ENGINE, "rb") as handle:
        engine = handle.read()
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
    case_12_virtual = word(section, switch_table + 12 * 4)
    case_12_file = CODE_FILE_BASE + case_12_virtual
    rotation_lookup_bss = word(section, TOC_BASE - 0x73E4)
    rotatable_world_bss = word(section, TOC_BASE - 0x7350)

    if switch_table != 0x485A8:
        raise AssertionError(f"switch table drift: {switch_table:#x}")
    if case_12_virtual != 0xE9778 or case_12_file != 0xF2608:
        raise AssertionError(
            f"case 12 target drift: virtual={case_12_virtual:#x} file={case_12_file:#x}"
        )
    if rotation_lookup_bss != 0x7F0B8:
        raise AssertionError(f"rotation lookup pointer drift: {rotation_lookup_bss:#x}")
    if rotatable_world_bss != 0x85C48:
        raise AssertionError(f"rotatable-world pointer drift: {rotatable_world_bss:#x}")

    with open(SYMBOLS, encoding="utf-8") as handle:
        symbols = {item["name"]: item["start"] for item in json.load(handle)}
    symbol_expect = {
        "BuildRotationLookup__7CTilePtFv": 0x60F20,
        "TryGenericSimCall__8cXObjectFP9StackElemP10XPrimParam": 0xF1F20,
    }
    for name, address in symbol_expect.items():
        if symbols.get(name) != address:
            raise AssertionError(
                f"symbol drift for {name}: {symbols.get(name)!r} != {address:#x}"
            )

    builder_words = verify_words(
        engine,
        {
            0x60F20: 0x39600000,
            0x60F24: 0x80C28C1C,
            0x60F3C: 0x206A003F,
            0x60F88: 0x38EA0000,
            0x60F8C: 0x2109003F,
            0x60F94: 0x39030000,
            0x60F98: 0x20E9003F,
            0x60FA0: 0x38E30000,
            0x60FA4: 0x39090000,
            0x60FA8: 0x99040000,
            0x60FAC: 0x98E40001,
            0x61060: 0x38C62000,
            0x61064: 0x2C0B0003,
        },
        "BuildRotationLookup",
    )
    case_words = verify_words(
        engine,
        {
            0xF2608: 0xA8180004,
            0xF2660: 0x88F300FC,
            0xF266C: 0x7CE60774,
            0xF2668: 0x88B300FD,
            0xF2670: 0x80630084,
            0xF2678: 0x7CA40774,
            0xF2690: 0x54606824,
            0xF26A8: 0x8864E000,
            0xF26AC: 0x8804E001,
            0xF26C0: 0x7C630774,
            0xF26C4: 0x7C000774,
            0xF26C8: 0x7C030214,
            0xF26CC: 0x7C0000D0,
            0xF26D0: 0xB01F003A,
        },
        "TryGenericSimCall mode 12",
    )

    builder = engine[0x60F20:0x61070]
    case = engine[0xF2608:0xF26D8]
    samples = []
    for x, y in ((11, 23), (0, 0), (63, 63), (0, 63), (63, 0)):
        samples.append(
            f"({x},{y}) -> "
            + ", ".join(f"r{rotation}={camera_distance(x, y, rotation)}" for rotation in range(4))
        )

    lines = [
        "# R166 original-engine decode: Generic Sims Call mode 12",
        f"engine sha256: {hashlib.sha256(engine).hexdigest()}",
        (
            f"PEF packed data: file+{DATA_CONTAINER_OFFSET:#x} length "
            f"{DATA_CONTAINER_LENGTH:#x} -> {len(section):#x}"
        ),
        f"PEF unpacked data sha256: {hashlib.sha256(section).hexdigest()}",
        (
            f"dispatch: TOC[-0x59a0] -> data+{switch_table:#x}; "
            f"entry[12] -> code-virtual {case_12_virtual:#x} -> file {case_12_file:#x}"
        ),
        (
            f"rotation lookup: TOC[-0x73e4] -> BSS+{rotation_lookup_bss:#x}; "
            f"world root: TOC[-0x7350] -> BSS+{rotatable_world_bss:#x}"
        ),
        (
            "code sha256: BuildRotationLookup[0x60f20,0x61070)="
            + hashlib.sha256(builder).hexdigest()
        ),
        (
            "code sha256: mode12[0xf2608,0xf26d8)="
            + hashlib.sha256(case).hexdigest()
        ),
        "",
        (
            "law recovered from BuildRotationLookup (x,y are byte-valued object "
            "tile coordinates loaded with lbz and sign-extended; validated domain "
            "0..63):"
        ),
        "  r0 = (x, y)",
        "  r1 = (63-y, x)",
        "  r2 = (63-x, 63-y)",
        "  r3 = (y, 63-x)",
        "  Temp0 = -(rotated_x + rotated_y); level byte is copied but unused",
        "  return value remains true",
        "",
        "sample vectors:",
        *[f"  {sample}" for sample in samples],
        "",
        "decisive BuildRotationLookup words:",
        *[f"  {line}" for line in builder_words],
        "",
        "decisive TryGenericSimCall mode-12 words:",
        *[f"  {line}" for line in case_words],
    ]
    report = "\n".join(lines) + "\n"
    with open(OUT, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(report)
    print(report, end="")


if __name__ == "__main__":
    main()
