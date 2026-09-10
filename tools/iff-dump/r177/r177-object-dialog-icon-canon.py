"""R177: verify the original ObjectDialog icon-selector law.

The gate reads the owner's PowerPC PEF and shipped IFF/FAR corpus in place.
It emits only hashes, counters, addresses, and resource metadata; it never
copies original executable, string, image, or IFF payload bytes into output.
"""

import hashlib
import importlib.util
import io
import re
import struct
from collections import Counter
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
CORPUS = REPO / "game-data" / "The Sims"
ENGINE = CORPUS / "The Sims Complete"
UIGRAPHICS_FAR = CORPUS / "UIGraphics" / "UIGraphics.far"
RES_OTHER_H = CORPUS / "UIGraphics" / "Res_Other.h"
R154_SCANNER = REPO / "tools" / "iff-dump" / "r154" / "r154-callers-scan.py"

ENGINE_SHA256 = "33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f"
UNPACKED_SHA256 = "f740c1dfa1dea8187b12ebf222ca1a39223365d26ad5d2f2e353d5efba247be3"
DATA_CONTAINER_OFFSET = 0x5C22F0
DATA_CONTAINER_LENGTH = 0x6D834
DATA_UNPACKED_LENGTH = 0x7BF80
TOC_BASE = 0x8000

EXPECTED_SELECTOR_MODES = Counter({0: 1937, 1: 150, 2: 157, 3: 463, 4: 137})
EXPECTED_NAMED_COMMANDS = Counter(
    {"job": 81, "gzi": 40, "my": 3, "gz": 3, "guid": 1, "rel": 1}
)
EXPECTED_JOB_ARGUMENTS = Counter(
    {"$Local:9": 65, "$Local:3": 7, "$Local:0": 3,
     "$Local:4": 3, "$Local:6": 2, "$Local:2": 1}
)
EXPECTED_NONCOMMAND_SHA256 = {
    "23ba12ba53ad51f09c19ca34016599dbd5628c13f571396b48d8eb5155761da2",
    "846826c2153a99d0a40c20df06de5151475af462ce7d270a3b22e9b371d92761",
}
TRAGEDY_MASK_SHA256 = "560b7a7d880b598a37d961d3558043e2d90c20d708104452402dcf886688f89c"


def load_r154():
    spec = importlib.util.spec_from_file_location("r154_callers_scan", R154_SCANNER)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot load established scanner {R154_SCANNER}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


R154 = load_r154()


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


def signed_16(value: int) -> int:
    return value - 0x10000 if value & 0x8000 else value


def branch_target(data: bytes, address: int) -> int:
    instruction = word(data, address)
    opcode = instruction >> 26
    if opcode == 18:
        displacement = instruction & 0x03FFFFFC
        if displacement & 0x02000000:
            displacement -= 0x04000000
    elif opcode == 16:
        displacement = instruction & 0xFFFC
        if displacement & 0x8000:
            displacement -= 0x10000
    else:
        raise AssertionError(f"not a PPC branch at {address:#x}")
    if instruction & 2:
        return displacement
    return address + displacement


def verify_engine() -> tuple[str, str, dict[str, str]]:
    engine = ENGINE.read_bytes()
    engine_sha = hashlib.sha256(engine).hexdigest()
    if engine_sha != ENGINE_SHA256:
        raise AssertionError(f"original engine drift: {engine_sha}")

    packed = engine[
        DATA_CONTAINER_OFFSET : DATA_CONTAINER_OFFSET + DATA_CONTAINER_LENGTH
    ]
    section = unpack_pef_data(packed)
    section_sha = hashlib.sha256(section).hexdigest()
    if len(section) != DATA_UNPACKED_LENGTH or section_sha != UNPACKED_SHA256:
        raise AssertionError(
            f"unpacked PEF drift: length={len(section):#x} sha256={section_sha}"
        )

    regions = {
        "automatic-parse[0xcd058,0xcd0e4)": (0xCD058, 0xCD0E4),
        "mode-dispatch[0xcd0f4,0xcd1fc)": (0xCD0F4, 0xCD1FC),
        "named-dispatch[0xcd47c,0xcd914)": (0xCD47C, 0xCD914),
    }
    expected_region_sha = {
        "automatic-parse[0xcd058,0xcd0e4)":
            "6a9a6ad1e51df73cb56960b66d936ae46e3569b0360ee43854ecd382284567a4",
        "mode-dispatch[0xcd0f4,0xcd1fc)":
            "6f7aaadd073cc4b413ffc2ae75bc5af8acbfec54481e485eb4c3012ced066609",
        "named-dispatch[0xcd47c,0xcd914)":
            "d58b6cfba89dcaa1da60b60568cd0876654576ed9a111262c41b1f80988839a9",
    }
    region_sha = {
        name: hashlib.sha256(engine[start:end]).hexdigest()
        for name, (start, end) in regions.items()
    }
    if region_sha != expected_region_sha:
        raise AssertionError(f"ObjectDialog code-region drift: {region_sha}")

    # The same ParseUIString selection output is passed through fields
    # Yes, No, Cancel, Title, Message in that exact order.
    parse_fields = [0xC, 0x10, 0x14, 0x8, 0x18]
    field_instructions = [0xCD068, 0xCD08C, 0xCD0A4, 0xCD0BC, 0xCD0D4]
    call_instructions = [0xCD080, 0xCD098, 0xCD0B0, 0xCD0C8, 0xCD0E0]
    actual_fields = [signed_16(word(engine, at) & 0xFFFF) for at in field_instructions]
    actual_targets = [branch_target(engine, at) for at in call_instructions]
    if actual_fields != parse_fields or actual_targets != [0xF8610] * 5:
        raise AssertionError(
            f"automatic parse-order drift: fields={actual_fields}, "
            f"targets={actual_targets}"
        )

    decisive_words = {
        # flags[7] >> 1 & 7, then 0/1/2/3/4/reserved dispatch
        0xCD0F4: 0x881E0007,
        0xCD0F8: 0x5400FF7E,
        0xCD0FC: 0x2C000002,
        0xCD108: 0x2C000000,
        0xCD114: 0x2C000004,
        # automatic fallback and neighbor both use signed Stack Object ID
        0xCD140: 0xA81F003A,
        0xCD190: 0xA89F003A,
        # indexed: behavior owner selector, then raw byte + 5000
        0xCD1AC: 0x809D000C,
        0xCD1BC: 0x889E0001,
        0xCD1C4: 0x3B041388,
        0xCD230: 0x38C00004,
        # named: operand byte 1 is parsed before token dispatch
        0xCD1CC: 0x88BE0001,
        # shared empty selector takes the blank-image path
        0xCD200: 0x28190000,
        0xCD214: 0x41820258,
        # keyword-table relative offsets: gz, gzi, my, guid, rel, job
        0xCD520: 0x389C000D,
        0xCD55C: 0x389C0010,
        0xCD650: 0x389C0014,
        0xCD6D8: 0x389C0017,
        0xCD770: 0x389C001C,
        0xCD890: 0x389C0020,
        # gz length == 2 divides the source width by four; gzi does not
        0xCD5CC: 0x8003001C,
        0xCD5E8: 0x2C030002,
        0xCD5F4: 0x7C001670,
        0xCD5F8: 0x7C000194,
        0xCD5FC: 0x90010080,
        # rel chooses the center frame of a five-wide strip
        0xCD808: 0x8003001C,
        0xCD818: 0x7C040096,
        0xCD81C: 0x7C000E70,
        0xCD824: 0x7EA02214,
    }
    for address, expected in decisive_words.items():
        actual = word(engine, address)
        if actual != expected:
            raise AssertionError(
                f"ObjectDialog word drift at {address:#x}: "
                f"{actual:08x} != {expected:08x}"
            )

    expected_branches = {
        0xCD100: 0xCD18C,  # mode 2 neighbor
        0xCD10C: 0xCD124,  # mode 0 automatic
        0xCD110: 0xCD1FC,  # mode 1 none
        0xCD118: 0xCD1CC,  # mode 4 named
        0xCD11C: 0xCD1FC,  # modes 5..7 blank
        0xCD120: 0xCD1A8,  # mode 3 indexed
        0xCD214: 0xCD46C,  # missing selector stays blank
    }
    for address, expected in expected_branches.items():
        actual = branch_target(engine, address)
        if actual != expected:
            raise AssertionError(
                f"ObjectDialog branch drift at {address:#x}: "
                f"{actual:#x} != {expected:#x}"
            )

    keyword_base = word(section, TOC_BASE - 0x5AA8)
    keyword_blob = b"gz\0gzi\0my\0guid\0rel\0job\0"
    if keyword_base != 0x46340:
        raise AssertionError(f"named keyword table pointer drift: {keyword_base:#x}")
    if section[keyword_base + 0xD : keyword_base + 0xD + len(keyword_blob)] != keyword_blob:
        raise AssertionError("named keyword vocabulary drift")

    return engine_sha, section_sha, region_sha


def walk_chunks(data: bytes):
    """Yield strict production IFF headers; IDs and flags are big-endian."""
    if not data.startswith(b"IFF FILE"):
        return
    offset = 64
    while offset < len(data):
        if offset + 76 > len(data):
            raise ValueError(f"truncated IFF header at {offset:#x}")
        tag = data[offset : offset + 4].decode("latin1")
        size = struct.unpack_from(">I", data, offset + 4)[0]
        if size < 76 or offset + size > len(data):
            raise ValueError(f"invalid {tag} chunk at {offset:#x}: size={size:#x}")
        chunk_id, flags = struct.unpack_from(">HH", data, offset + 8)
        label = (
            data[offset + 12 : offset + 76]
            .split(b"\0", 1)[0]
            .decode("latin1", "replace")
        )
        yield tag, offset, size, chunk_id, flags, label, offset + 76
        offset += size


def read_c_string(data: bytes, offset: int) -> tuple[str, int]:
    end = data.find(b"\0", offset)
    if end < 0:
        raise ValueError("unterminated STR# string")
    return data[offset:end].decode("latin1", "replace"), end + 1


def read_7bit_length(data: bytes, offset: int) -> tuple[int, int]:
    value = 0
    shift = 0
    while True:
        byte = data[offset]
        offset += 1
        value |= (byte & 0x7F) << shift
        if not byte & 0x80:
            return value, offset
        shift += 7
        if shift > 28:
            raise ValueError("invalid variable Pascal string length")


def read_variable_pascal(data: bytes, offset: int) -> tuple[str, int]:
    length, offset = read_7bit_length(data, offset)
    end = offset + length
    if end > len(data):
        raise ValueError("truncated variable Pascal string")
    return data[offset:end].decode("utf-8", "replace"), end


def parse_english_strings(body: bytes) -> list[str]:
    if len(body) < 4:
        return []
    format_code, count = struct.unpack_from("<hH", body, 0)
    offset = 4
    strings: list[str] = []
    if format_code == 0:
        for _ in range(count):
            length = body[offset]
            offset += 1
            strings.append(body[offset : offset + length].decode("latin1", "replace"))
            offset += length
    elif format_code == -1:
        for _ in range(count):
            value, offset = read_c_string(body, offset)
            strings.append(value)
    elif format_code == -2:
        for _ in range(count):
            value, offset = read_c_string(body, offset)
            _comment, offset = read_c_string(body, offset)
            strings.append(value)
    elif format_code == -3:
        language_sets: dict[int, list[str]] = {}
        for _ in range(count):
            language = body[offset]
            offset += 1
            value, offset = read_c_string(body, offset)
            _comment, offset = read_c_string(body, offset)
            language_sets.setdefault(1 if language == 0 else language, []).append(value)
        strings = language_sets.get(1, [])
    elif format_code == -4:
        language_count = body[offset]
        offset += 1
        language_sets = []
        for _ in range(language_count):
            pair_count = struct.unpack_from("<H", body, offset)[0]
            offset += 2
            values = []
            for _ in range(pair_count):
                offset += 1  # stored language index
                value, offset = read_variable_pascal(body, offset)
                _comment, offset = read_variable_pascal(body, offset)
                values.append(value)
            language_sets.append(values)
        strings = language_sets[0] if language_sets else []
    return strings


def iter_owned_iffs():
    for path in sorted(CORPUS.rglob("*"), key=lambda item: str(item).lower()):
        if not path.is_file():
            continue
        relative = path.relative_to(CORPUS).as_posix()
        lower = path.name.lower()
        if lower.endswith(".far"):
            with path.open("rb") as handle:
                for member, data in R154.far_members(str(path), handle):
                    member_lower = member.lower()
                    if member_lower.endswith(".far"):
                        nested = io.BytesIO(data)
                        for nested_member, nested_data in R154.far_members(
                            member, nested
                        ):
                            if nested_member.lower().endswith((".iff", ".fam")):
                                yield (
                                    f"{relative}!{member}!{nested_member}", nested_data
                                )
                    elif member_lower.endswith((".iff", ".fam")):
                        yield f"{relative}!{member}", data
        elif lower.endswith((".iff", ".fam")):
            yield relative, path.read_bytes()


def scan_corpus() -> dict[str, object]:
    selector_modes: Counter[int] = Counter()
    named_commands: Counter[str] = Counter()
    named_arguments: dict[str, Counter[str]] = {}
    noncommand_hashes: set[str] = set()
    raw_dialog_sites = 0
    clothes_selectors = 0
    missing_named = 0
    iffs_scanned = 0
    bhavs_scanned = 0
    hd_help_bmps: dict[str, set[int]] = {}
    tutorial_bmp300: dict[str, int] = {}

    for display, data in iter_owned_iffs():
        if not data.startswith(b"IFF FILE"):
            continue
        iffs_scanned += 1
        chunks = list(walk_chunks(data))
        strings_301: list[str] | None = None
        for tag, offset, size, chunk_id, _flags, _label, data_start in chunks:
            if tag == "STR#" and chunk_id == 301:
                strings_301 = parse_english_strings(data[data_start : offset + size])

        lower_display = display.lower()
        if lower_display.endswith("!hdhelpsystem.iff"):
            hd_help_bmps[display] = {
                chunk_id for tag, _o, _s, chunk_id, _f, _l, _d in chunks
                if tag == "BMP_" and 5000 <= chunk_id <= 5021
            }
        if lower_display.endswith("!tutorial.iff"):
            tutorial_bmp300[display] = sum(
                1 for tag, _o, _s, chunk_id, _f, _l, _d in chunks
                if tag == "BMP_" and chunk_id == 300
            )

        for tag, _offset, _size, _chunk_id, _flags, _label, data_start in chunks:
            if tag != "BHAV":
                continue
            bhav = R154.parse_bhav(data, data_start)
            if bhav is None:
                continue
            bhavs_scanned += 1
            for instruction in bhav["instrs"]:
                if instruction[0] != 36:
                    continue
                raw_dialog_sites += 1
                operand = struct.pack("<hhhh", *instruction[3:])
                mode = (operand[7] >> 1) & 7
                dialog_type = operand[5]

                # TS1 dialog type 6 is the separate clothing selector. Its 13
                # sites all encode automatic but do not enter picture-dialog
                # icon selection, so keep them adjacent to, not inside, census.
                if dialog_type == 6:
                    if mode != 0:
                        raise AssertionError("clothing selector gained an icon mode")
                    clothes_selectors += 1
                    continue

                selector_modes[mode] += 1
                if mode != 4:
                    continue

                string_id = operand[1]
                if (
                    string_id == 0
                    or strings_301 is None
                    or string_id > len(strings_301)
                ):
                    missing_named += 1
                    continue
                value = strings_301[string_id - 1]
                command, separator, argument = value.partition(" ")
                command = command.lower()
                if command not in {"gz", "gzi", "my", "guid", "rel", "job"}:
                    noncommand_hashes.add(hashlib.sha256(value.encode("latin1")).hexdigest())
                    continue
                named_commands[command] += 1
                named_arguments.setdefault(command, Counter())[argument if separator else ""] += 1

    expected_all_modes = Counter({0: 1937, 1: 150, 2: 157, 3: 463, 4: 137,
                                  5: 0, 6: 0, 7: 0})
    actual_all_modes = Counter({mode: selector_modes[mode] for mode in range(8)})
    if actual_all_modes != expected_all_modes:
        raise AssertionError(
            f"ObjectDialog icon-mode census drift: {actual_all_modes}"
        )
    if raw_dialog_sites != 2857 or clothes_selectors != 13:
        raise AssertionError(
            f"raw dialog census drift: sites={raw_dialog_sites}, "
            f"clothes={clothes_selectors}"
        )
    if named_commands != EXPECTED_NAMED_COMMANDS or missing_named != 6:
        raise AssertionError(
            f"named selector census drift: commands={named_commands}, "
            f"missing={missing_named}"
        )
    if named_arguments.get("job") != EXPECTED_JOB_ARGUMENTS:
        raise AssertionError(f"job argument census drift: {named_arguments.get('job')}")
    expected_arguments = {
        "gzi": Counter({"9005": 40}),
        "my": Counter({"300": 3}),
        "gz": Counter({"$Local:0": 3}),
        "guid": Counter({"-2032092235": 1}),
        "rel": Counter({"": 1}),
    }
    for command, expected in expected_arguments.items():
        if named_arguments.get(command) != expected:
            raise AssertionError(
                f"{command} argument census drift: {named_arguments.get(command)}"
            )
    if noncommand_hashes != EXPECTED_NONCOMMAND_SHA256:
        raise AssertionError(f"named non-command set drift: {noncommand_hashes}")

    expected_help = set(range(5000, 5022))
    if len(hd_help_bmps) != 1 or next(iter(hd_help_bmps.values())) != expected_help:
        raise AssertionError(f"HDHelpSystem BMP_ fixture drift: {hd_help_bmps}")
    tutorial_matches = {
        name: count for name, count in tutorial_bmp300.items() if count == 1
    }
    if tutorial_matches != {"GameData/Objects/Objects.far!Tutorial.iff": 1}:
        raise AssertionError(f"Tutorial BMP_ 300 fixture drift: {tutorial_bmp300}")

    return {
        "iffs": iffs_scanned,
        "bhavs": bhavs_scanned,
        "raw_dialogs": raw_dialog_sites,
        "clothes": clothes_selectors,
        "modes": selector_modes,
        "named": named_commands,
        "named_missing": missing_named,
        "named_noncommands": len(noncommand_hashes),
        "help_container": next(iter(hd_help_bmps)),
    }


def far_member(path: Path, wanted: str) -> bytes:
    data = path.read_bytes()
    if data[:8] != b"FAR!byAZ":
        raise ValueError(f"not a FAR archive: {path}")
    manifest = struct.unpack_from("<I", data, 12)[0]
    count = struct.unpack_from("<I", data, manifest)[0]
    offset = manifest + 4
    matches = []
    for _ in range(count):
        length, _stored_length, data_offset, name_length = struct.unpack_from(
            "<IIII", data, offset
        )
        name = data[offset + 16 : offset + 16 + name_length].decode(
            "latin1", "replace"
        )
        offset += 16 + name_length
        if name.lower() == wanted.lower():
            matches.append(data[data_offset : data_offset + length])
    if len(matches) != 1:
        raise AssertionError(f"expected one {wanted!r} member, got {len(matches)}")
    return matches[0]


def verify_named_resource_fixture() -> tuple[int, int, int, str]:
    header = RES_OTHER_H.read_text(encoding="latin1")
    match = re.search(r"\bkTragedyUnhappyMask\s*=\s*(\d+)\s*;", header)
    if match is None or int(match.group(1)) != 9005:
        raise AssertionError("Res_Other resource 9005 mapping drift")
    bitmap = far_member(UIGRAPHICS_FAR, r"Other\TragedyMask.BMP")
    if bitmap[:2] != b"BM" or len(bitmap) < 26:
        raise AssertionError("TragedyMask bitmap header drift")
    width, height = struct.unpack_from("<ii", bitmap, 18)
    digest = hashlib.sha256(bitmap).hexdigest()
    if (len(bitmap), width, height, digest) != (
        41256, 133, 103, TRAGEDY_MASK_SHA256
    ):
        raise AssertionError(
            f"TragedyMask fixture drift: bytes={len(bitmap)}, "
            f"size={width}x{height}, sha256={digest}"
        )
    return len(bitmap), width, height, digest


def main() -> None:
    engine_sha, section_sha, region_sha = verify_engine()
    corpus = scan_corpus()
    bitmap_length, bitmap_width, bitmap_height, bitmap_sha = (
        verify_named_resource_fixture()
    )

    print(f"engine sha256 {engine_sha}")
    print(f"PEF unpacked sha256 {section_sha}")
    for name, digest in region_sha.items():
        print(f"code region sha256 {name} {digest}")
    print(
        "dispatch verified: mode0=automatic mode1=none mode2=neighbor "
        "mode3=indexed mode4=named modes5..7=blank"
    )
    print(
        "automatic parse order verified: Yes, No, Cancel, Title, Message; "
        "shared last-selector output"
    )
    print(
        "named keywords verified: count=6; gz=first-width-quarter; gzi=full"
    )
    print(
        f"corpus metadata: IFFs={corpus['iffs']} BHAVs={corpus['bhavs']} "
        f"raw-opcode36={corpus['raw_dialogs']} clothes-type6={corpus['clothes']} "
        f"object-icon-sites={sum(corpus['modes'].values())}"
    )
    modes = corpus["modes"]
    print(
        "mode census: "
        + " ".join(
            f"{name}={modes[index]}"
            for index, name in enumerate(
                ["automatic", "none", "neighbor", "indexed", "named"]
            )
        )
        + " reserved5=0 reserved6=0 reserved7=0"
    )
    named = corpus["named"]
    print(
        "named census: "
        + " ".join(
            f"{name}={named[name]}" for name in ["gz", "gzi", "my", "guid", "rel", "job"]
        )
        + f" missing={corpus['named_missing']} "
        f"noncommands={corpus['named_noncommands']}"
    )
    print(
        f"mode3 fixture: {corpus['help_container']} BMP_ ids=5000..5021 count=22"
    )
    print("mode4 private fixture: Tutorial.iff BMP_ id=300 count=1")
    print(
        f"mode4 global fixture: RT id=9005 bitmap={bitmap_width}x{bitmap_height} "
        f"bytes={bitmap_length} sha256={bitmap_sha}"
    )
    print("R177 ObjectDialog icon-selector canon PASS")


if __name__ == "__main__":
    main()
