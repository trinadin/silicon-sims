#!/usr/bin/env python3
"""R165 static canon scan for the original TS1 school-bus control law.

The scan reads only locally staged, user-owned game data.  It reuses the R154
FAR/IFF walker and mirrors the checked-in OBJM field decoder closely enough to
read one House05 person fixture.  Output is metadata only: hashes, resource
labels/counts, control-flow sites, GUIDs, and selected numeric fixture fields.

Run from any working directory:
    python3 /path/to/simitone-fork/tools/iff-dump/r165/r165-school-bus-canon-scan.py
"""

import hashlib
import importlib.util
import os
import struct
import sys

sys.dont_write_bytecode = True

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
OWNED_ROOT = os.path.join(REPO, "game-data", "The Sims")
HOUSE_PATH = os.path.join(OWNED_ROOT, "UserData", "Houses", "House05.iff")
OBJECTS_FAR = os.path.join(OWNED_ROOT, "GameData", "Objects", "Objects.far")
GLOBAL_FAR = os.path.join(OWNED_ROOT, "GameData", "Global", "Global.far")
R154_PATH = os.path.join(HERE, "..", "r154", "r154-callers-scan.py")
OUTPUT_PATH = os.path.join(HERE, "r165-school-bus-canon-scan.txt")

SCHOOL_BUS_GUID = 0x50AC15BF

EXPECTED_INPUTS = {
    "game-data/The Sims/UserData/Houses/House05.iff": (
        HOUSE_PATH,
        383122,
        "a6d18d81c5072c507b118b68598ee12ea57403634d6507c39b7a04ad4c6cdd99",
    ),
    "game-data/The Sims/GameData/Objects/Objects.far": (
        OBJECTS_FAR,
        75494806,
        "b029f449a91289b0a789810ff226da0e3a578b0931f1b5bcd104433e316efbf0",
    ),
    "game-data/The Sims/GameData/Global/Global.far": (
        GLOBAL_FAR,
        2078993,
        "c301952612dc2adfc4a94b9d9e6e78c3cef5635b989e1cb4342b29989d13b5fb",
    ),
}

EXPECTED_MEMBERS = {
    "CarPortal.iff": (
        846157,
        "267ffd7da0184bd14bde64e8005a287c1332fcae1f0df098fe14586e8aa81f6d",
    ),
    "SchoolBus.iff": (
        1313197,
        "19863972fab858424a0c50f757e18a550206de2765ee0396e007d7a47a57f621",
    ),
    "CarGlobals.iff": (
        8240,
        "a7f7fc870848885ae034d1f14084a506e5e8c37925e5264113f49827b193c917",
    ),
}

EXPECTED_CAR_PORTAL_BHAVS = {
    4100: ("process", 58),
    4115: ("check for school bus schedule", 22),
    4116: ("At School", 61),
    4117: ("lower all kids grades", 22),
    4119: ("Create School Bus", 10),
}

EXPECTED_SCHOOL_BUS_BHAVS = {
    4096: ("main", 22),
    4097: ("notify car portal", 3),
    4098: ("Go to Work", 10),
    4099: ("Go to Work TEST", 1),
    4100: ("Is this bus for me?", 2),
    4101: ("init common", 1),
    4102: ("init route part", 2),
    4103: ("auto to school", 7),
    4113: ("load tree", 2),
    4117: ("init Main", 2),
    4120: ("Leave?", 6),
    4121: ("send person to work", 26),
}

EXPECTED_CAR_GLOBALS_BHAVS = {
    8198: ("init", 9),
    8200: ("honk horn", 91),
    8205: ("drive away", 29),
    8208: ("Find Clone", 8),
}

EXPECTED_SEMIGLOBAL_CALLS = (
    (4096, 15, 8200, 18, 253),
    (4096, 16, 8200, 9, 253),
    (4096, 19, 8205, 21, 253),
    (4098, 7, 8208, 8, 5),
    (4101, 0, 8198, 254, 253),
)

WIDTHS_16 = (5, 8, 13, 16)
WIDTHS_32 = (6, 11, 21, 32)
WIDTHS_BYTE = (2, 4, 6, 8)


def require_equal(actual, expected, description):
    if actual != expected:
        raise AssertionError(
            f"R165 canon drift: {description}: expected {expected!r}, got {actual!r}"
        )


def require(condition, description):
    if not condition:
        raise AssertionError(f"R165 canon drift: {description}")


def load_r154_parser():
    spec = importlib.util.spec_from_file_location("r165_r154_iff", R154_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot load R154 IFF parser: {R154_PATH}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


IFF = load_r154_parser()


def sha256_bytes(data):
    return hashlib.sha256(data).hexdigest()


def sha256_file(path):
    digest = hashlib.sha256()
    with open(path, "rb") as source:
        while block := source.read(1024 * 1024):
            digest.update(block)
    return digest.hexdigest()


def chunks(data):
    for tag, offset, size, _chunk_id, _flags, label, data_start in IFF.walk_chunks(data):
        # IFF chunk ids are big-endian.  R154 is retained for its resynchronizing
        # offsets, but its historical diagnostic id field is little-endian.
        chunk_id = struct.unpack_from(">H", data, offset + 8)[0]
        yield tag, chunk_id, label, data_start, data[data_start:offset + size]


def member_basename(name):
    return name.replace("\\", "/").rsplit("/", 1)[-1]


def read_far_targets(path, target_names):
    wanted = {name.lower(): name for name in target_names}
    found = {}
    with open(path, "rb") as source:
        for member, data in IFF.far_members(path, source):
            key = member_basename(member).lower()
            if key not in wanted:
                continue
            display = wanted[key]
            require(display not in found, f"duplicate FAR member {display}")
            found[display] = data
    require_equal(set(found), set(target_names), f"target members in {os.path.basename(path)}")
    return found


def parse_objt(body):
    require(len(body) >= 12, "House05 OBJT header is truncated")
    _padding, version, magic = struct.unpack_from("<III", body, 0)
    require_equal(magic, 0x6F626A74, "House05 OBJT magic")

    entries = []
    position = 12
    while position < len(body):
        require(position + 4 <= len(body), "House05 OBJT GUID is truncated")
        guid = struct.unpack_from("<I", body, position)[0]
        position += 4
        if guid == 0:
            entries.append({"guid": 0, "name": "", "objd_type": 0})
            continue

        require(position + 12 <= len(body), "House05 OBJT entry is truncated")
        fields = struct.unpack_from("<HHHHHH", body, position)
        position += 12
        nul = body.find(b"\0", position)
        require(nul >= 0, "House05 OBJT name is unterminated")
        name = body[position:nul].decode("latin1", "replace")
        position = nul + 1
        if len(name) % 2 == 0:
            position += 1
        if version > 2:
            position += 4
        require(position <= len(body), "House05 OBJT entry exceeds chunk")
        entries.append({"guid": guid, "name": name, "objd_type": fields[5]})
    return version, entries


class FieldReader:
    """Strict numeric/string subset of FreeSO's IffFieldEncode reader."""

    def __init__(self, data, start=0, odd_offset=False):
        require(start < len(data), "field-encoded stream has no first byte")
        self.data = data
        self.stream_position = start + 1
        self.current_byte = data[start]
        self.bit_position = 0
        self.odd = not odd_offset

    def read_bit(self):
        require(self.bit_position < 8, "numeric field followed a non-final OBJM string")
        require(self.current_byte is not None, "field-encoded stream ended")
        value = (self.current_byte >> (7 - self.bit_position)) & 1
        self.bit_position += 1
        if self.bit_position == 8:
            self.bit_position = 0
            if self.stream_position < len(self.data):
                self.current_byte = self.data[self.stream_position]
                self.stream_position += 1
                self.odd = not self.odd
            else:
                self.current_byte = None
        return value

    def read_bits(self, count):
        value = 0
        for _ in range(count):
            value = (value << 1) | self.read_bit()
        return value

    def read_field(self, widths):
        if self.read_bit() == 0:
            return 0
        width = widths[self.read_bits(2)]
        value = self.read_bits(width)
        if value & (1 << (width - 1)):
            value -= 1 << width
        return value

    def byte(self):
        return self.read_field(WIDTHS_BYTE) & 0xFF

    def i16(self):
        return self.read_field(WIDTHS_16)

    def u16(self):
        return self.read_field(WIDTHS_16) & 0xFFFF

    def i32(self):
        return self.read_field(WIDTHS_32)

    def interrupt_position(self):
        # IffFieldEncode.Interrupt backs up over a prefetched byte only when
        # the cursor is byte-aligned.
        if self.bit_position == 0:
            return self.stream_position - 1
        return self.stream_position

    def string(self, next_field):
        if self.bit_position == 0:
            self.stream_position -= 1
            self.odd = not self.odd
        start = self.stream_position
        nul = self.data.find(b"\0", start)
        require(nul >= 0, "field-encoded OBJM string is unterminated")
        raw = self.data[start:nul]
        self.stream_position = nul + 1
        if (len(raw) % 2 == 0) == (not self.odd):
            self.stream_position += 1
        require(self.stream_position <= len(self.data), "OBJM string padding exceeds instance")

        self.bit_position = 8
        if next_field:
            require(self.stream_position < len(self.data), "OBJM final string has no next field")
            self.current_byte = self.data[self.stream_position]
            self.stream_position += 1
            self.odd = True
            self.bit_position = 0
        else:
            self.odd = False
        return raw.decode("latin1", "replace")


def skip_stack_frame(reader):
    reader.i16()  # StackObjectID
    reader.i16()  # TreeID
    reader.i16()  # NodeID
    local_count = reader.byte()
    parameter_count = reader.byte()
    require(local_count <= 255 and parameter_count <= 255, "OBJM stack register count")
    for _ in range(parameter_count + local_count):
        reader.i16()
    reader.i32()  # primitive state
    reader.i16()  # code-owner OBJT type


def decode_target_person(body, objt_entries, target_object_id):
    require(len(body) >= 14, "House05 ObjM header is truncated")
    _padding, version, magic = struct.unpack_from("<III", body, 0)
    require_equal(magic, 0x4F626A4D, "House05 ObjM magic")
    require_equal(body[12], 1, "House05 ObjM compression code")

    table_reader = FieldReader(body, 13)
    id_to_type = {}
    while True:
        object_id = table_reader.u16()
        if object_id == 0:
            break
        id_to_type[object_id] = table_reader.u16()

    raw_position = table_reader.interrupt_position()
    result = None
    for _ in range(len(id_to_type)):
        require(raw_position + 4 <= len(body), "House05 ObjM instance offset is truncated")
        skip_offset = struct.unpack_from("<i", body, raw_position)[0]
        data_start = raw_position + 4
        skip_position = 12 + skip_offset
        require(
            data_start <= skip_position <= len(body),
            f"House05 ObjM has invalid instance boundary {skip_position}",
        )
        encoded = body[data_start:skip_position]
        reader = FieldReader(encoded, odd_offset=((data_start - 12) & 1) == 1)

        for _ in range(4):
            reader.i32()  # footprint
        for _ in range(3):
            reader.i32()  # x, y, level
        reader.i16()  # unknown data
        attribute_count = reader.i16()
        require(0 <= attribute_count <= 4096, "House05 ObjM attribute count")
        for _ in range(attribute_count + 8):
            reader.i16()  # attributes and temp registers
        object_data = [reader.i16() for _ in range(68)]
        for _ in range(5):
            reader.i16()  # extra object data

        frame_count = reader.i32()
        reader.i32()  # stack flags
        require(0 <= frame_count <= 4096, "House05 ObjM frame count")
        for _ in range(frame_count):
            skip_stack_frame(reader)

        object_id = object_data[11]
        if object_id == target_object_id:
            require(result is None, f"duplicate House05 object {target_object_id}")
            type_id = id_to_type.get(object_id)
            require(type_id is not None, f"House05 object {object_id} lacks an OBJT mapping")
            require(1 <= type_id <= len(objt_entries), f"House05 object {object_id} OBJT index")
            objt = objt_entries[type_id - 1]
            require_equal(objt["objd_type"], 2, f"House05 object {object_id} type")

            relationship_flag = reader.i32()
            require(relationship_flag < 0, "House05 person relationship flag")
            relationship_count = reader.i32()
            require(0 <= relationship_count <= 4096, "House05 person relationship count")
            for _ in range(relationship_count):
                require(reader.i32() != 0, "House05 person absent relationship entry")
                reader.i32()  # target id
                value_count = reader.i32()
                require(0 <= value_count <= 4096, "House05 relationship value count")
                for _ in range(value_count):
                    reader.i32()

            slot_count = reader.i16()
            require(slot_count >= 0, "House05 person slot count")
            for _ in range(slot_count * 2):
                reader.i16()
            dynamic_sprite_count = reader.i16()
            require(dynamic_sprite_count >= 0, "House05 dynamic-sprite count")
            for _ in range(dynamic_sprite_count):
                reader.i16()

            reader.i32()  # animation event count
            reader.i32()  # engaged flag
            for string_index in range(10):
                reader.string(next_field=(string_index == 9))
            accessory_count = reader.i32()
            require(0 <= accessory_count <= 4096, "House05 person accessory count")
            for _ in range(accessory_count * 2):
                reader.string(next_field=False)
            reader.string(next_field=False)  # animation
            reader.string(next_field=False)  # carry animation
            reader.string(next_field=True)  # base animation
            reader.i32()  # routing state
            for _ in range(9 + 16 + 16):
                reader.i32()  # float bit patterns

            person_data_count = 0x100 if version > 0x45 else 0x50
            person_data = [reader.i16() for _ in range(person_data_count)]
            result = {
                "version": version,
                "type_id": type_id,
                "objt": objt,
                "person_data": person_data,
            }
        raw_position = skip_position

    require(result is not None, f"House05 object {target_object_id} was not found")
    return result


def extract_house_fixture():
    with open(HOUSE_PATH, "rb") as source:
        data = source.read()
    objt_bodies = []
    objm_bodies = []
    for tag, _chunk_id, _label, _data_start, body in chunks(data):
        if tag in ("OBJT", "objt"):
            objt_bodies.append(body)
        elif tag == "ObjM":
            objm_bodies.append(body)
    require_equal(len(objt_bodies), 1, "House05 OBJT chunk count")
    require_equal(len(objm_bodies), 1, "House05 ObjM chunk count")
    _objt_version, objt_entries = parse_objt(objt_bodies[0])
    return decode_target_person(objm_bodies[0], objt_entries, 16)


def parse_bhavs(data):
    result = {}
    for tag, chunk_id, label, data_start, _body in chunks(data):
        if tag != "BHAV":
            continue
        parsed = IFF.parse_bhav(data, data_start)
        require(parsed is not None, f"BHAV {chunk_id} could not be decoded")
        require(chunk_id not in result, f"duplicate BHAV {chunk_id}")
        result[chunk_id] = {
            "label": label,
            "instructions": parsed["instrs"],
        }
    return result


def bhav_metadata(bhavs, ids=None):
    selected = sorted(bhavs) if ids is None else sorted(ids)
    return {
        routine_id: (bhavs[routine_id]["label"], len(bhavs[routine_id]["instructions"]))
        for routine_id in selected
    }


def operand_hex(instruction):
    return struct.pack("<hhhh", *instruction[3:7]).hex()


def parse_bcon(body):
    require(len(body) >= 2, "BCON is truncated")
    count, flags = body[:2]
    require_equal(len(body), 2 + count * 2, "BCON byte count")
    constants = struct.unpack_from(f"<{count}H", body, 2)
    return flags, constants


def parse_glob(body):
    require(body, "GLOB is empty")
    if body[0] < 48:
        name = body[1:1 + body[0]].decode("latin1", "replace")
    else:
        name = body.split(b"\0", 1)[0].decode("latin1", "replace")
    name = name.strip()
    require(name, "GLOB name is empty")
    if not name.lower().endswith(".iff"):
        name += ".iff"
    return name.lower()


def parse_objd(chunk_id, label, body):
    require(len(body) >= 4 + 46 * 2, f"OBJD {chunk_id} is truncated")
    version = struct.unpack_from("<I", body, 0)[0]
    fields = struct.unpack_from(f"<{(len(body) - 4) // 2}H", body, 4)
    sub_index = fields[9] if fields[9] < 0x8000 else fields[9] - 0x10000
    return {
        "chunk_id": chunk_id,
        "label": label,
        "version": version,
        "stack_size": fields[0],
        "main_id": fields[3],
        "object_type": fields[7],
        "master_id": fields[8],
        "sub_index": sub_index,
        "guid": fields[12] | (fields[13] << 16),
        "uses_function_table": fields[20],
        "init_id": fields[41],
        "load_id": fields[45],
    }


def member_metadata(name, data):
    size, digest = EXPECTED_MEMBERS[name]
    require_equal(len(data), size, f"{name} member size")
    actual_digest = sha256_bytes(data)
    require_equal(actual_digest, digest, f"{name} member SHA-256")
    return size, actual_digest


def instruction(bhavs, routine_id, instruction_index):
    instructions = bhavs[routine_id]["instructions"]
    require(instruction_index < len(instructions), f"BHAV {routine_id}:{instruction_index} exists")
    return instructions[instruction_index]


def build_report():
    input_rows = []
    for display, (path, expected_size, expected_digest) in EXPECTED_INPUTS.items():
        require(os.path.isfile(path), f"owned input is missing: {display}")
        actual_size = os.path.getsize(path)
        actual_digest = sha256_file(path)
        require_equal(actual_size, expected_size, f"{display} size")
        require_equal(actual_digest, expected_digest, f"{display} SHA-256")
        input_rows.append((display, actual_size, actual_digest))

    object_members = read_far_targets(OBJECTS_FAR, ("CarPortal.iff", "SchoolBus.iff"))
    global_members = read_far_targets(GLOBAL_FAR, ("CarGlobals.iff",))
    car_portal = object_members["CarPortal.iff"]
    school_bus = object_members["SchoolBus.iff"]
    car_globals = global_members["CarGlobals.iff"]
    member_rows = [
        (name, *member_metadata(name, data))
        for name, data in (
            ("CarPortal.iff", car_portal),
            ("SchoolBus.iff", school_bus),
            ("CarGlobals.iff", car_globals),
        )
    ]

    fixture = extract_house_fixture()
    person_data = fixture["person_data"]
    fixture_fingerprint = (
        fixture["version"],
        fixture["type_id"],
        fixture["objt"]["guid"],
        fixture["objt"]["name"],
        fixture["objt"]["objd_type"],
        len(person_data),
        person_data[31],
        person_data[32],
        person_data[56],
        person_data[57],
        person_data[58],
        person_data[63],
    )
    require_equal(
        fixture_fingerprint,
        (62, 113, 0xC1207913, "Cassandra", 2, 80, 32, 0, -1, 4, 9, 0),
        "House05 Cassandra object-16 fixture",
    )
    require(person_data[58] < 18, "House05 Cassandra is not a child by the original age test")

    portal_bhavs = parse_bhavs(car_portal)
    require_equal(
        bhav_metadata(portal_bhavs, EXPECTED_CAR_PORTAL_BHAVS),
        EXPECTED_CAR_PORTAL_BHAVS,
        "CarPortal school-bus BHAV metadata",
    )

    tuning_chunks = []
    for tag, chunk_id, label, _data_start, body in chunks(car_portal):
        if tag == "BCON" and chunk_id == 4097:
            tuning_chunks.append((label, *parse_bcon(body)))
    require_equal(len(tuning_chunks), 1, "CarPortal BCON 4097 chunk count")
    tuning_label, tuning_flags, tuning_constants = tuning_chunks[0]
    require_equal(
        (tuning_label, tuning_flags, tuning_constants),
        ("Tuning", 0x80, (100, 5, 9, 15, 2, 40, 0, 12)),
        "CarPortal BCON 4097",
    )

    schedule_start_read = instruction(portal_bhavs, 4115, 8)
    schedule_end_read = instruction(portal_bhavs, 4116, 5)
    require_equal(
        (schedule_start_read[:3], operand_hex(schedule_start_read)),
        ((2, 9, 253), "000082000005191a"),
        "CarPortal 4115:8 school-start tuning read",
    )
    require_equal(
        (schedule_end_read[:3], operand_hex(schedule_end_read)),
        ((2, 15, 6), "8300000000021a06"),
        "CarPortal 4116:5 school-end tuning read",
    )

    portal_calls = (
        (4100, 28, instruction(portal_bhavs, 4100, 28)),
        (4115, 10, instruction(portal_bhavs, 4115, 10)),
        (4116, 27, instruction(portal_bhavs, 4116, 27)),
    )
    require_equal(
        tuple((routine, index, item[0], item[1], item[2], operand_hex(item))
              for routine, index, item in portal_calls),
        (
            (4100, 28, 4115, 5, 253, "ffffffffffffffff"),
            (4115, 10, 4119, 11, 11, "ffffffffffffffff"),
            (4116, 27, 4119, 41, 6, "ffffffffffffffff"),
        ),
        "CarPortal school-bus call sites",
    )

    select_site = instruction(portal_bhavs, 4119, 3)
    create_site = instruction(portal_bhavs, 4119, 4)
    require_equal(
        (select_site[:3], operand_hex(select_site)),
        ((31, 254, 6), "bf15ac50840a0000"),
        "CarPortal 4119:3 SchoolBus selection site",
    )
    require_equal(
        (create_site[:3], operand_hex(create_site)),
        ((42, 5, 255), "bf15ac5006200000"),
        "CarPortal 4119:4 SchoolBus creation site",
    )
    select_operand = bytes.fromhex(operand_hex(select_site))
    create_operand = bytes.fromhex(operand_hex(create_site))
    require_equal(struct.unpack_from("<I", select_operand)[0], SCHOOL_BUS_GUID, "selection GUID")
    require_equal(struct.unpack_from("<I", create_operand)[0], SCHOOL_BUS_GUID, "creation GUID")
    require_equal(select_operand[4:], b"\x84\x0a\x00\x00", "selection mode/target")
    require_equal(create_operand[4:], b"\x06\x20\x00\x00", "creation position/flags")

    attendance_sites = (
        (4116, 31, instruction(portal_bhavs, 4116, 31)),
        (4116, 59, instruction(portal_bhavs, 4116, 59)),
        (4116, 60, instruction(portal_bhavs, 4116, 60)),
        (4117, 4, instruction(portal_bhavs, 4117, 4)),
    )
    require_equal(
        tuple((routine, index, item[0], item[1], item[2], operand_hex(item))
              for routine, index, item in attendance_sites),
        (
            (4116, 31, 2, 5, 32, "2200010000020307"),
            (4116, 59, 2, 4, 253, "5700010000051207"),
            (4116, 60, 2, 7, 253, "5700000000051207"),
            (4117, 4, 2, 20, 253, "3900030000031307"),
        ),
        "CarPortal school attendance state sites",
    )

    school_bhavs = parse_bhavs(school_bus)
    require_equal(
        bhav_metadata(school_bhavs),
        EXPECTED_SCHOOL_BUS_BHAVS,
        "SchoolBus private BHAV inventory",
    )
    school_objds = []
    school_globs = []
    for tag, chunk_id, label, _data_start, body in chunks(school_bus):
        if tag == "OBJD":
            school_objds.append(parse_objd(chunk_id, label, body))
        elif tag == "GLOB":
            school_globs.append(parse_glob(body))
    require_equal(len(school_objds), 31, "SchoolBus OBJD count")
    masters = [objd for objd in school_objds if objd["sub_index"] == -1]
    require_equal(len(masters), 1, "SchoolBus master OBJD count")
    master = masters[0]
    require_equal(
        master,
        {
            "chunk_id": 16807,
            "label": "Car - School Bus",
            "version": 138,
            "stack_size": 4,
            "main_id": 4096,
            "object_type": 4,
            "master_id": 2,
            "sub_index": -1,
            "guid": SCHOOL_BUS_GUID,
            "uses_function_table": 1,
            "init_id": 4101,
            "load_id": 4113,
        },
        "SchoolBus master OBJD",
    )
    require_equal(school_globs, ["carglobals.iff"], "SchoolBus GLOB relationship")

    send_person_sites = (
        (5, instruction(school_bhavs, 4121, 5)),
        (6, instruction(school_bhavs, 4121, 6)),
        (20, instruction(school_bhavs, 4121, 20)),
        (23, instruction(school_bhavs, 4121, 23)),
    )
    require_equal(
        tuple((index, item[0], item[1], item[2], operand_hex(item))
              for index, item in send_person_sites),
        (
            (5, 45, 7, 20, "0400020001000000"),
            (6, 2, 254, 253, "2200010000050307"),
            (20, 27, 7, 23, "0000fffe00000400"),
            (23, 2, 255, 253, "1100000000050307"),
        ),
        "SchoolBus 4121 primary/fallback boarding law",
    )

    semiglobal_calls = []
    for routine_id in sorted(school_bhavs):
        for instruction_index, item in enumerate(school_bhavs[routine_id]["instructions"]):
            if item[0] >= 8192:
                semiglobal_calls.append(
                    (routine_id, instruction_index, item[0], item[1], item[2])
                )
    require_equal(
        tuple(semiglobal_calls),
        EXPECTED_SEMIGLOBAL_CALLS,
        "SchoolBus calls through CarGlobals",
    )

    global_bhavs = parse_bhavs(car_globals)
    require_equal(
        bhav_metadata(global_bhavs, EXPECTED_CAR_GLOBALS_BHAVS),
        EXPECTED_CAR_GLOBALS_BHAVS,
        "referenced CarGlobals BHAV metadata",
    )

    lines = [
        "R165 ORIGINAL SCHOOL-BUS CANON SCAN",
        "status: PASS",
        "scope: static metadata from locally staged, user-owned original-game inputs",
        "payload policy: no resource payloads emitted",
        "",
        "PINNED INPUTS",
    ]
    for display, size, digest in input_rows:
        lines.append(f"  {display}: bytes={size} sha256={digest}")
    lines.extend(("", "PINNED FAR MEMBERS"))
    for name, size, digest in member_rows:
        lines.append(f"  {name}: bytes={size} sha256={digest}")

    lines.extend((
        "",
        "HOUSE05 CHILD FIXTURE (field-encoded ObjM)",
        "  object_id=16 objt_name='Cassandra' objt_type=Person objt_guid=0xc1207913",
        "  neighbor_id=PersonData[31]=32 person_type=PersonData[32]=0",
        "  age=PersonData[58]=9; original child predicate age < 18 => true",
        "  job_type=PersonData[56]=-1",
        "  grade_slot=PersonData[57]=4 (JobPromotionLevel / school grade)",
        "  performance=PersonData[63]=0",
        f"  ObjM_version={fixture['version']} PersonData_words={len(person_data)}",
        "",
        "CARPORTAL SCHOOL SCHEDULE",
        (
            f"  BCON 4097 {tuning_label!r}: flags=0x{tuning_flags:02x} "
            f"constants={list(tuning_constants)}"
        ),
        "  school_start: BCON[2] / Tuning[130] = 9; read at BHAV 4115:8",
        "  school_end:   BCON[3] / Tuning[131] = 15; read at BHAV 4116:5",
        "",
        "CARPORTAL RELEVANT BHAV METADATA",
    ))
    for routine_id, (label, count) in sorted(EXPECTED_CAR_PORTAL_BHAVS.items()):
        lines.append(f"  {routine_id} {label!r}: instructions={count}")
    lines.extend((
        "",
        "CARPORTAL CONTROL-FLOW LAW",
        "  BHAV 4100:28 -> 4115 (schedule check), true=5 false=253",
        "  BHAV 4115:10 -> 4119 (outbound create), true=11 false=11",
        "  BHAV 4116:27 -> 4119 (return create), true=41 false=6",
        (
            "  BHAV 4119:3 set-to-next ObjectOfType GUID=0x50ac15bf; "
            "target=StackObjectID[0]; true=254 false=6; operand=bf15ac50840a0000"
        ),
        (
            "  BHAV 4119:4 createObjectInstance GUID=0x50ac15bf; "
            "position=OutOfWorld; FaceStackObjDir=true; true=5 false=255; "
            "operand=bf15ac5006200000"
        ),
        "",
        "ATTENDANCE STATE LAW",
        "  SchoolBus 4121:5 goto private routing SLOT[4]; true=7 false=20",
        "  SchoolBus 4121:20 goto-relative fallback; true=7 false=23",
        "  SchoolBus 4121:6 MyObject.Hidden = 1 (successful boarding latch)",
        "  CarPortal 4116:31 tests MyObject.Hidden == 1 while the bus waits",
        "  CarPortal 4116:59 MyPersonData[87] = 1 (away at school)",
        "  CarPortal 4116:60 MyPersonData[87] = 0 (returned home)",
        "  CarPortal 4117:4 StackObjectPersonData[57] += 3 (missed-school penalty)",
        "",
        "SCHOOLBUS.IFF RESOURCE METADATA",
        "  master OBJD 16807 'Car - School Bus': version=138 object_type=4",
        "  master_id=2 sub_index=-1 stack_size=4 uses_function_table=1",
        "  main_id=4096 init_id=4101 load_id=4113",
        "  master GUID=0x50ac15bf; OBJD_count=31",
        "  GLOB='CarGlobals.iff'",
        "  private BHAV inventory:",
    ))
    for routine_id, (label, count) in sorted(EXPECTED_SCHOOL_BUS_BHAVS.items()):
        lines.append(f"    {routine_id} {label!r}: instructions={count}")

    lines.extend(("", "SCHOOLBUS -> CARGLOBALS SEMIGLOBAL RELATIONSHIP"))
    for routine_id, instruction_index, callee, true_target, false_target in semiglobal_calls:
        label, count = EXPECTED_CAR_GLOBALS_BHAVS[callee]
        lines.append(
            f"  SchoolBus {routine_id}:{instruction_index} -> CarGlobals {callee} "
            f"{label!r} (instructions={count}); true={true_target} false={false_target}"
        )

    lines.extend((
        "",
        "DISCLOSURE",
        "  Cassandra's fixture was decoded directly and is included; no requested fixture fact",
        "  was omitted. 'grade_slot' names the original PersonData[57] field used and mutated",
        "  by CarPortal BHAVs 4116/4117; no additional fixture semantics were guessed.",
        "  Every reported fact and every input/member hash is a hard drift assertion.",
        "",
    ))
    return "\n".join(lines)


def main():
    report = build_report()
    with open(OUTPUT_PATH, "w", encoding="utf-8", newline="\n") as output:
        output.write(report)
    print(report, end="")


if __name__ == "__main__":
    main()
