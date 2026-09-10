#!/usr/bin/env python3
"""Verify the owner's original expansion-category order and Magic tooltips.

Reads the local PowerPC PEF in place and emits metadata only. No original game
bytes are copied into the repository or output.
"""

from __future__ import annotations

import hashlib
import struct
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
ENGINE = REPO / "game-data" / "The Sims" / "The Sims Complete"
ENGINE_SHA256 = "33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f"
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
            raise ValueError(f"reserved PEF opcode {opcode} at {offset - 1:#x}")
    return bytes(result)


def word(data: bytes, offset: int) -> int:
    return struct.unpack_from(">I", data, offset)[0]


def toc_row(section: bytes, magnitude: int) -> tuple[int, list[int]]:
    pointer = word(section, TOC_BASE - magnitude)
    return pointer, [word(section, pointer + index * 4) for index in range(8)]


def main() -> None:
    engine = ENGINE.read_bytes()
    digest = hashlib.sha256(engine).hexdigest()
    if digest != ENGINE_SHA256:
        raise AssertionError(f"original engine drift: {digest}")
    packed = engine[
        DATA_CONTAINER_OFFSET : DATA_CONTAINER_OFFSET + DATA_CONTAINER_LENGTH
    ]
    section = unpack_pef_data(packed)
    if len(section) != DATA_UNPACKED_LENGTH:
        raise AssertionError(f"unpacked section length drift: {len(section):#x}")

    studio_pointer, studio = toc_row(section, 0x6AC8)
    magic_pointer, magic = toc_row(section, 0x6ACC)
    expected_studio = [273, 274, 276, 275, 0, 0, 0, 277]
    expected_magic = [1705, 1706, 1708, 1707, 0, 0, 0, 1709]
    if studio != expected_studio:
        raise AssertionError(f"Studio row drift: {studio}")
    if magic != expected_magic:
        raise AssertionError(f"Magic row drift: {magic}")

    # LoadBooks first assigns STR#150[40+i], then replaces Magic slots 3 and 2
    # from pointers prepared as r31+0x88 ([34]) and r31+0xb0 ([44]).
    code_words = {
        0x269320: 0x381F0088,
        0x269328: 0x90010050,
        0x26932C: 0x381F00B0,
        0x269338: 0x90010054,
        0x269EC4: 0x38100028,
        0x269ED8: 0x2C100003,
        0x269EE4: 0x80810050,
        0x269EEC: 0x2C100002,
        0x269EF8: 0x80810054,
    }
    for address, expected in code_words.items():
        actual = word(engine, address)
        if actual != expected:
            raise AssertionError(
                f"LoadBooks drift at {address:#x}: {actual:08x} != {expected:08x}"
            )

    print(f"engine sha256 {digest}")
    print(f"Studio TOC[-0x6ac8] -> data+{studio_pointer:#x}: {studio}")
    print(f"Magic  TOC[-0x6acc] -> data+{magic_pointer:#x}: {magic}")
    print("Magic tooltip slots: 2 -> STR#150[44] MagiCo; 3 -> STR#150[34] Outdoors")
    print("verified LoadBooks words: " + ", ".join(f"{x:#x}" for x in code_words))


if __name__ == "__main__":
    main()
