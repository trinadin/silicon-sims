#!/usr/bin/env python3
"""Verify the native four-state cTSSystemButton law used by desktop CAS.

The verifier reads the owner's local PEF and UIGraphics corpus in place.  It
emits hashes, resource metadata, and a few decisive decoded instructions; it
does not copy the executable, art, or resource payloads into the repository.
"""

from __future__ import annotations

import hashlib
import re
import struct
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
ENGINE = REPO / "game-data" / "The Sims" / "The Sims Complete"
UI_FAR = REPO / "game-data" / "The Sims" / "UIGraphics" / "UIGraphics.far"
UI_TEXT = REPO / "game-data" / "The Sims" / "GameData" / "UIText.iff"
NBHD_RT = REPO / "game-data" / "The Sims" / "UIGraphics" / "Res_Nbhd.RT"
NBHD_H = REPO / "game-data" / "The Sims" / "UIGraphics" / "Res_Nbhd.h"
SYMBOLS = REPO / "tools" / "iff-dump" / "r141" / "symbol-index.txt"

ENGINE_SIZE = 6_486_820
ENGINE_SHA256 = "33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f"
UI_FAR_SHA256 = "f0afa06b24888bd4201a3e619cf2c8d3f5ea1c9b1de6e11572981dbc871e5c8b"
UI_TEXT_SHA256 = "b229da197eb9678370844c4da7c88640c3d1ce9a5e528654bfd71df6ef12a9ad"
SYMBOLS_SHA256 = "698765465ec861b1a6dc357e32e14359752a1f63269222dadb56ad6134fae2a6"
TILE_MEMBER = "Nbhd\\NbhdTileBtn.BMP"
TILE_MEMBER_SIZE = 9_842
TILE_MEMBER_SHA256 = "5c5470ae614f12bd9f8a1a10ecf6072a857c7b60db552ce41b40f94b9dd01d17"
FONT_MEMBER = "Fonts\\variablesans_12.ffn"
FONT_MEMBER_SIZE = 25_552
FONT_MEMBER_SHA256 = "f584b9de66c9e588f541d7d1643acf1f2e91586fdc8636fc408fbfe603117a1c"
PICK_STR_CHUNK_SHA256 = "dce5d06dda063180895de85efc2325021c774a6fe0285ee7a3204c49cdd781f0"
TILE_RESOURCE_ID = 5_301
STATE_COLUMNS = 4
STATE_ROWS = 1
CONTROL_WIDTH = 200
CAP_WIDTH = 45
CAPTION_MARGINS = 70


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def signed(value: int, bits: int) -> int:
    sign = 1 << (bits - 1)
    return value - (1 << bits) if value & sign else value


def immediate(value: int) -> str:
    if -9 <= value <= 9:
        return str(value)
    return f"-{abs(value):#x}" if value < 0 else f"{value:#x}"


def decode_ppc(address: int, value: int) -> str:
    """Decode only the PPC32 forms used by the decisive evidence below."""
    opcode = value >> 26
    rt = (value >> 21) & 31
    ra = (value >> 16) & 31
    rb = (value >> 11) & 31
    displacement = signed(value & 0xFFFF, 16)

    if opcode == 14:  # addi; the assembler aliases addi rD,r0,n to li.
        if ra == 0:
            return f"li r{rt}, {immediate(displacement)}"
        return f"addi r{rt}, r{ra}, {immediate(displacement)}"
    if opcode == 32:
        return f"lwz r{rt}, {immediate(displacement)}(r{ra})"
    if opcode == 11 and ((value >> 21) & 1) == 0:
        bf = (value >> 23) & 7
        prefix = "" if bf == 0 else f"cr{bf}, "
        return f"cmpwi {prefix}r{ra}, {immediate(displacement)}"
    if opcode == 16:
        bo = (value >> 21) & 31
        bi = (value >> 16) & 31
        branch_disp = signed(value & 0xFFFC, 16)
        target = branch_disp if value & 2 else address + branch_disp
        suffix = "l" if value & 1 else ""
        if bi == 2 and bo in (4, 12):
            return f"{'bne' if bo == 4 else 'beq'}{suffix} {target:#x}"
        return f"bc{suffix} {bo}, {bi}, {target:#x}"
    if opcode == 18:
        branch_disp = signed(value & 0x03FFFFFC, 26)
        target = branch_disp if value & 2 else address + branch_disp
        return f"b{'l' if value & 1 else ''} {target:#x}"
    if opcode == 31 and ((value >> 1) & 0x3FF) == 266:
        return f"add r{rt}, r{ra}, r{rb}{'.' if value & 1 else ''}"
    raise AssertionError(f"unsupported evidence word {value:08x} at {address:#x}")


def verify_group(
    engine: bytes, label: str, instructions: tuple[tuple[int, int, str], ...]
) -> list[str]:
    lines = [f"[{label}]"]
    for address, wanted, expected_decode in instructions:
        actual = struct.unpack_from(">I", engine, address)[0]
        if actual != wanted:
            raise AssertionError(
                f"{label} word drift at {address:#x}: {actual:08x} != {wanted:08x}"
            )
        decoded = decode_ppc(address, actual)
        if decoded != expected_decode:
            raise AssertionError(
                f"decoder drift at {address:#x}: {decoded!r} != {expected_decode!r}"
            )
        lines.append(f"  {address:#010x}: {actual:08x}  {decoded}")
    return lines


def verify_raw_group(
    engine: bytes, label: str, words: tuple[tuple[int, int, str], ...]
) -> list[str]:
    """Hash-gated words whose compact PPC dataflow is explained in the note."""
    lines = [f"[{label}]"]
    for address, wanted, meaning in words:
        actual = struct.unpack_from(">I", engine, address)[0]
        if actual != wanted:
            raise AssertionError(
                f"{label} word drift at {address:#x}: {actual:08x} != {wanted:08x}"
            )
        lines.append(f"  {address:#010x}: {actual:08x}  {meaning}")
    return lines


def far_member(data: bytes, wanted_name: str) -> tuple[int, bytes]:
    if data[:8] != b"FAR!byAZ":
        raise AssertionError(f"UIGraphics FAR magic drift: {data[:8]!r}")
    manifest = struct.unpack_from("<I", data, 12)[0]
    count = struct.unpack_from("<I", data, manifest)[0]
    offset = manifest + 4
    found: list[tuple[int, bytes]] = []
    for _ in range(count):
        length, duplicate_length, data_offset, name_length = struct.unpack_from(
            "<IIII", data, offset
        )
        offset += 16
        name = data[offset : offset + name_length].decode("latin1")
        offset += name_length
        if length != duplicate_length or data_offset + length > len(data):
            raise AssertionError(f"invalid FAR member metadata for {name!r}")
        if name.lower().replace("/", "\\") == wanted_name.lower():
            found.append((data_offset, data[data_offset : data_offset + length]))
    if len(found) != 1:
        raise AssertionError(f"expected one {wanted_name!r} member, found {len(found)}")
    return found[0]


def iff_str_english(data: bytes, chunk_id: int) -> tuple[str, bytes, list[str]]:
    if not data.startswith(b"IFF FILE 2.5:"):
        raise AssertionError("UIText.iff header drift")
    offset = 0x40
    while offset + 76 <= len(data):
        size = struct.unpack_from(">I", data, offset + 4)[0]
        cid = struct.unpack_from(">H", data, offset + 8)[0]
        if size < 76 or offset + size > len(data):
            break
        if data[offset : offset + 4] == b"STR#" and cid == chunk_id:
            raw = data[offset : offset + size]
            label = data[offset + 12 : offset + 76].split(b"\0")[0].decode("mac_roman")
            body = raw[76:]
            if struct.unpack_from("<h", body, 0)[0] != -3:
                raise AssertionError(f"STR# {chunk_id} format drift")
            count = struct.unpack_from("<H", body, 2)[0]
            pos = 4
            english: list[str] = []
            for _ in range(count):
                lang = body[pos]
                pos += 1
                end = body.index(0, pos)
                value = body[pos:end].decode("mac_roman")
                pos = end + 1
                end = body.index(0, pos)
                pos = end + 1
                if lang == 1:
                    english.append(value)
            return label, raw, english
        offset += size
    raise AssertionError(f"STR# {chunk_id} not found")


def ffn_metrics(data: bytes, text: str) -> tuple[int, int]:
    if data[:4] != b"FNTF":
        raise AssertionError("font FNTF header drift")
    count = struct.unpack_from("<H", data, 10)[0]
    directory = struct.unpack_from("<I", data, 20)[0]
    glyphs: dict[int, tuple[int, int, int, int]] = {}
    for index in range(count):
        pos = directory + index * 11
        char, width, height, _u, _v, tail0, _tail1, tail2 = struct.unpack_from(
            "<HBBHHbbb", data, pos
        )
        glyphs[char] = (width, height, tail0, tail2)
    measured = 0
    bottoms: list[int] = []
    tops: list[int] = []
    for char, (width, height, tail0, tail2) in glyphs.items():
        if char != 160 and width and height:
            bottoms.append(height + tail2)
            tops.append(tail2)
    for char in text:
        width, _height, tail0, _tail2 = glyphs[ord(char)]
        measured += width + tail0 if width > 1 else (5 if char == " " else 0)
    return measured, max(bottoms) - min(0, min(tops))


def verify_symbols(data: bytes) -> list[str]:
    if sha256(data) != SYMBOLS_SHA256:
        raise AssertionError(f"symbol index drift: {sha256(data)}")
    parsed: dict[str, tuple[int, int]] = {}
    for raw_line in data.decode("utf-8").splitlines():
        fields = raw_line.split(maxsplit=2)
        if len(fields) == 3:
            parsed[fields[2]] = (int(fields[0], 16), int(fields[1]))
    wanted = {
        ".Init__19cWinDesignCharacterFv": (0x2CE612, 5252),
        ".Init__16cWinDesignFamilyFv": (0x2D1352, 3116),
        ".CreateButton__14cWinPickFamilyFiPCc": (0x2D9482, 508),
        ".Init__14cWinPickFamilyFv": (0x2D99F2, 2080),
        ".SetArea__15cTSSystemButtonFllll": (0x4FFD72, 344),
        ".SetCaption__15cTSSystemButtonFRC9cTSString": (0x4FFF02, 552),
        ".ImageBlt__15cTSSystemButtonFv": (0x5004A2, 524),
        ".__ct__15cTSSystemButtonFP9cTSBuffer": (0x500882, 164),
        ".SetImage__9cTSWinBtnFP9cTSBufferii": (0x50C0F2, 548),
    }
    for name, expected in wanted.items():
        if parsed.get(name) != expected:
            raise AssertionError(f"symbol drift for {name}: {parsed.get(name)}")
    return [f"  {address:#010x} size={size:3d} {name}" for name, (address, size) in wanted.items()]


GROUPS = (
    (
        "cTSSystemButton ctor: inherited SetImage(buffer, 4, 1)",
        (
            (0x5008B0, 0x389F0000, "addi r4, r31, 0"),
            (0x5008B4, 0x38A00004, "li r5, 4"),
            (0x5008BC, 0x38C00001, "li r6, 1"),
            (0x5008C0, 0x819E0000, "lwz r12, 0(r30)"),
            (0x5008C4, 0x818C01A4, "lwz r12, 0x1a4(r12)"),
            (0x5008C8, 0x480A2119, "bl 0x5a29e0"),
        ),
    ),
    (
        "Create-a-Character: load RT 5301 and pass it to cTSSystemButton",
        (
            (0x2CE7E4, 0x38900190, "addi r4, r16, 0x190"),
            (0x2CE7E8, 0x386014B5, "li r3, 0x14b5"),
            (0x2CE7EC, 0x38A00004, "li r5, 4"),
            (0x2CE7F0, 0x480E79A1, "bl 0x3b6190"),
            (0x2CE9EC, 0x80900190, "lwz r4, 0x190(r16)"),
            (0x2CE9F0, 0x48231E91, "bl 0x500880"),
        ),
    ),
    (
        "Create-a-Character: logical area is 200 by buffer height",
        (
            (0x2CEA14, 0x80D00190, "lwz r6, 0x190(r16)"),
            (0x2CEA20, 0x808F0074, "lwz r4, 0x74(r15)"),
            (0x2CEA24, 0x80060020, "lwz r0, 0x20(r6)"),
            (0x2CEA28, 0x80AF0078, "lwz r5, 0x78(r15)"),
            (0x2CEA2C, 0x38C400C8, "addi r6, r4, 0xc8"),
            (0x2CEA30, 0x818C0068, "lwz r12, 0x68(r12)"),
            (0x2CEA34, 0x7CE02A14, "add r7, r0, r5"),
            (0x2CEA38, 0x482D3FA9, "bl 0x5a29e0"),
        ),
    ),
    (
        "Create-a-Family: load RT 5301 and pass it to cTSSystemButton",
        (
            (0x2D13B8, 0x389F0178, "addi r4, r31, 0x178"),
            (0x2D13BC, 0x386014B5, "li r3, 0x14b5"),
            (0x2D13C4, 0x38A00004, "li r5, 4"),
            (0x2D13CC, 0x480E4DC5, "bl 0x3b6190"),
            (0x2D1BB0, 0x809F0178, "lwz r4, 0x178(r31)"),
            (0x2D1BB4, 0x4822ECCD, "bl 0x500880"),
        ),
    ),
    (
        "Create-a-Family: logical area is 200 by buffer height",
        (
            (0x2D1BC0, 0x80DF0178, "lwz r6, 0x178(r31)"),
            (0x2D1BCC, 0x809A0074, "lwz r4, 0x74(r26)"),
            (0x2D1BD0, 0x80060020, "lwz r0, 0x20(r6)"),
            (0x2D1BD4, 0x80BA0078, "lwz r5, 0x78(r26)"),
            (0x2D1BD8, 0x38C400C8, "addi r6, r4, 0xc8"),
            (0x2D1BDC, 0x818C0068, "lwz r12, 0x68(r12)"),
            (0x2D1BE0, 0x7CE02A14, "add r7, r0, r5"),
            (0x2D1BE4, 0x482D0DFD, "bl 0x5a29e0"),
        ),
    ),
    (
        "Pick-a-Family: load RT 5301 into the shared product-tile slot",
        (
            (0x2D9A40, 0x389F0128, "addi r4, r31, 0x128"),
            (0x2D9A44, 0x386014B5, "li r3, 0x14b5"),
            (0x2D9A4C, 0x38A00004, "li r5, 4"),
            (0x2D9A54, 0x480DC73D, "bl 0x3b6190"),
        ),
    ),
    (
        "Pick-a-Family CreateButton(3): cTSSystemButton and 200px area",
        (
            (0x2D9490, 0x2C1B0003, "cmpwi r27, 3"),
            (0x2D94A4, 0x40820108, "bne 0x2d95ac"),
            (0x2D94B8, 0x809D0128, "lwz r4, 0x128(r29)"),
            (0x2D94BC, 0x482273C5, "bl 0x500880"),
            (0x2D94C8, 0x80DD0128, "lwz r6, 0x128(r29)"),
            (0x2D94D4, 0x809C0074, "lwz r4, 0x74(r28)"),
            (0x2D94D8, 0x80060020, "lwz r0, 0x20(r6)"),
            (0x2D94DC, 0x80BC0078, "lwz r5, 0x78(r28)"),
            (0x2D94E0, 0x38C400C8, "addi r6, r4, 0xc8"),
            (0x2D94E4, 0x818C0068, "lwz r12, 0x68(r12)"),
            (0x2D94E8, 0x7CE02A14, "add r7, r0, r5"),
            (0x2D94EC, 0x482C94F5, "bl 0x5a29e0"),
        ),
    ),
)


RAW_GROUPS = (
    (
        "SetCaption: font[12], 35px inset, final width textWidth+70",
        (
            (0x4FFF44, 0x3800000C, "li r0,12"),
            (0x4FFF48, 0x901D019C, "stw r0,this+0x19c (font slot)"),
            (0x4FFFF0, 0x3804FFF6, "caption left = cap-10 = 35"),
            (0x500010, 0x3803FFF6, "caption right = textWidth+cap-10"),
            (0x500080, 0x57C4083C, "r4 = 2*cap"),
            (0x50009C, 0x3B83FFEC, "caption span - 20"),
            (0x5000A0, 0x7F84E214, "final width = textWidth-20+2*cap"),
        ),
    ),
    (
        "ImageBlt: derive 45px caps and issue three blits",
        (
            (0x5004C0, 0x8005001C, "load full 364px source width"),
            (0x5004C8, 0x7C001670, "divide by four states"),
            (0x5004D4, 0x38A5FFFF, "stateWidth-1"),
            (0x5004E0, 0x7C1C0E70, "cap=(stateWidth-1)/2 = 45"),
            (0x5005A4, 0x480A243D, "left-cap blit"),
            (0x500614, 0x480A23CD, "one-pixel center stretch"),
            (0x500674, 0x480A236D, "right-cap blit"),
        ),
    ),
    (
        "constructor caption offsets: pressed is +2,+2 only",
        (
            (0x5008E4, 0x90A10040, "normal x=0"),
            (0x5008E8, 0x90A10044, "normal y=0"),
            (0x5008EC, 0x90010048, "pressed x=2"),
            (0x5008F0, 0x9001004C, "pressed y=2"),
            (0x500904, 0x4800BAED, "install four caption-offset pairs"),
        ),
    ),
    (
        "mouse-up dispatch requires pointer-inside byte",
        (
            (0x50BCB0, 0x881F0161, "load pointer-entered byte +0x161"),
            (0x50BCB8, 0x41820024, "skip command when outside"),
            (0x50BCC4, 0x38800001, "command id 1 on valid release"),
            (0x50BCCC, 0x4BFFEBB5, "send command"),
        ),
    ),
    (
        "PickFamily button 3 tooltip and one-shot setup",
        (
            (0x2DA0F0, 0x38800010, "GetString API id 16 = disk English[15]"),
            (0x2DA108, 0x807F010C, "load button[3]"),
            (0x2DA110, 0x4822A9C1, "SetToolTipsText"),
            (0x2DA120, 0x807F010C, "load button[3]"),
            (0x2DA124, 0x38800001, "SetOneShot(true)"),
        ),
    ),
)


def main() -> None:
    engine = ENGINE.read_bytes()
    if len(engine) != ENGINE_SIZE or sha256(engine) != ENGINE_SHA256:
        raise AssertionError(
            f"owner executable drift: size={len(engine)} sha256={sha256(engine)}"
        )

    far = UI_FAR.read_bytes()
    if sha256(far) != UI_FAR_SHA256:
        raise AssertionError(f"UIGraphics.far drift: {sha256(far)}")

    ui_text = UI_TEXT.read_bytes()
    if sha256(ui_text) != UI_TEXT_SHA256:
        raise AssertionError(f"UIText.iff drift: {sha256(ui_text)}")

    header = NBHD_H.read_text(encoding="latin1")
    rt = NBHD_RT.read_text(encoding="latin1")
    id_match = re.search(r"const\s+Sint32\s+kNghDefaultBtn\s*=\s*(\d+)\s*;", header)
    path_match = re.search(
        r'\{RLEBMP\s*,\s*kNghDefaultBtn\s*,\s*"([^"]+)"\s*\}', rt, re.IGNORECASE
    )
    if not id_match or int(id_match.group(1)) != TILE_RESOURCE_ID:
        raise AssertionError("Res_Nbhd.h kNghDefaultBtn mapping drift")
    if not path_match or path_match.group(1).replace("/", "\\").lower() != TILE_MEMBER.lower():
        raise AssertionError("Res_Nbhd.RT kNghDefaultBtn path drift")

    member_offset, member = far_member(far, TILE_MEMBER)
    if len(member) != TILE_MEMBER_SIZE or sha256(member) != TILE_MEMBER_SHA256:
        raise AssertionError(
            f"{TILE_MEMBER} drift: size={len(member)} sha256={sha256(member)}"
        )
    if member[:2] != b"BM" or struct.unpack_from("<I", member, 14)[0] != 40:
        raise AssertionError("NbhdTileBtn is no longer the expected BMP/DIB")
    width, height = struct.unpack_from("<ii", member, 18)
    bpp = struct.unpack_from("<H", member, 28)[0]
    compression = struct.unpack_from("<I", member, 30)[0]
    if (width, height, bpp, compression) != (364, 62, 8, 1):
        raise AssertionError(
            f"NbhdTileBtn header drift: {(width, height, bpp, compression)}"
        )
    if width % STATE_COLUMNS or width // STATE_COLUMNS != 91:
        raise AssertionError(f"state-width drift: {width}/{STATE_COLUMNS}")

    _font_offset, font = far_member(far, FONT_MEMBER)
    if len(font) != FONT_MEMBER_SIZE or sha256(font) != FONT_MEMBER_SHA256:
        raise AssertionError(
            f"{FONT_MEMBER} drift: size={len(font)} sha256={sha256(font)}"
        )
    done_width, line_height = ffn_metrics(font, "Done")
    cancel_width, cancel_line_height = ffn_metrics(font, "Cancel")
    if (done_width, cancel_width, line_height, cancel_line_height) != (39, 51, 23, 23):
        raise AssertionError("font[12] Done/Cancel metrics drift")

    pick_label, pick_raw, pick_english = iff_str_english(ui_text, 128)
    if pick_label != "PickFamilyStrs" or sha256(pick_raw) != PICK_STR_CHUNK_SHA256:
        raise AssertionError("PickFamily STR#128 identity drift")
    if len(pick_english) <= 15 or pick_english[5] != "Cancel" or pick_english[15] != "Back to Neighborhood":
        raise AssertionError("PickFamily Cancel label/tooltip drift")

    symbol_lines = verify_symbols(SYMBOLS.read_bytes())
    instruction_lines: list[str] = []
    for label, instructions in GROUPS:
        instruction_lines.extend(verify_group(engine, label, instructions))
    for label, words in RAW_GROUPS:
        instruction_lines.extend(verify_raw_group(engine, label, words))

    print(f"owner executable size {len(engine)} sha256 {sha256(engine)}")
    print(f"symbol index sha256 {sha256(SYMBOLS.read_bytes())}")
    print(f"UIGraphics.far sha256 {sha256(far)}")
    print(f"UIText.iff sha256 {sha256(ui_text)}")
    print(
        f"RT {TILE_RESOURCE_ID} kNghDefaultBtn -> {TILE_MEMBER}; "
        f"FAR+{member_offset:#x} size={len(member)} sha256={sha256(member)}"
    )
    print(
        f"BMP {width}x{height} bpp={bpp} compression={compression}; "
        f"SetImage({STATE_COLUMNS},{STATE_ROWS}) => state {width // STATE_COLUMNS}x{height}"
    )
    print(f"font[12] Done={done_width}px Cancel={cancel_width}px lineHeight={line_height}px")
    print(
        f"verified provisional area {CONTROL_WIDTH}x{height}; ImageBlt caps "
        f"{CAP_WIDTH}/1/{CAP_WIDTH}; post-caption widths "
        f"Done={done_width + CAPTION_MARGINS}, Cancel={cancel_width + CAPTION_MARGINS}"
    )
    print(
        f"PickFamily STR#128[5]={pick_english[5]!r}; "
        f"[15]={pick_english[15]!r}"
    )
    print("[symbols; extracted PEF symbol addresses retain their +2 tag]")
    print("\n".join(symbol_lines))
    print("\n".join(instruction_lines))


if __name__ == "__main__":
    main()
