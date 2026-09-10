#!/usr/bin/env python3
"""Pin the House05 TS1 saved-frame compatibility failures found in R163.

This diagnostic reads only the locally staged, user-owned House05/Objects/Global
data.  It imports the side-effect-free FAR and IFF chunk readers from R154, then
decodes the field-encoded OBJM continuation metadata using the same widths and
bit order as FreeSO's IffFieldEncode.  It writes only the report beside this
script; in particular, it never invokes iff_inspect.py or rewrites a shared
summary.
"""

import hashlib
import importlib.util
import os
import struct

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
OWNED_ROOT = os.path.join(REPO, "game-data", "The Sims")
HOUSE_PATH = os.path.join(OWNED_ROOT, "UserData", "Houses", "House05.iff")
OBJECTS_FAR = os.path.join(OWNED_ROOT, "GameData", "Objects", "Objects.far")
GLOBAL_FAR = os.path.join(OWNED_ROOT, "GameData", "Global", "Global.far")
R154_PATH = os.path.join(HERE, "..", "r154", "r154-callers-scan.py")
OUTPUT_PATH = os.path.join(HERE, "r164-saved-frame-compat-scan.txt")

WIDTHS_16 = (5, 8, 13, 16)
WIDTHS_32 = (6, 11, 21, 32)
WIDTHS_BYTE = (2, 4, 6, 8)


def load_r154_parser():
    spec = importlib.util.spec_from_file_location("r164_r154_iff", R154_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot load R154 IFF parser: {R154_PATH}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


IFF = load_r154_parser()


class FieldReader:
    """Minimal IffFieldEncode reader for numeric OBJM fields."""

    def __init__(self, data, start=0):
        self.data = data
        self.pos = start
        self.bit = 0

    def read_bit(self):
        if self.pos >= len(self.data):
            raise EOFError("field-encoded stream ended")
        value = (self.data[self.pos] >> (7 - self.bit)) & 1
        self.bit += 1
        if self.bit == 8:
            self.bit = 0
            self.pos += 1
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
        # Mirrors IffFieldEncode.Interrupt(): the underlying stream is one byte
        # past the current byte unless the bit cursor is byte-aligned.
        return self.pos if self.bit == 0 else self.pos + 1


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as source:
        while True:
            block = source.read(1024 * 1024)
            if not block:
                break
            digest.update(block)
    return digest.hexdigest()


def chunks(data):
    for tag, offset, size, _, _, label, data_start in IFF.walk_chunks(data):
        # IFF file headers are big-endian.  R154 is reused for its resynchronizing
        # offsets, but its diagnostic-oriented chunk-id field is little-endian.
        chunk_id = struct.unpack_from(">H", data, offset + 8)[0]
        yield tag, chunk_id, label, data_start, data[offset + 76:offset + size]


def parse_objt(body):
    if len(body) < 12:
        raise ValueError("OBJT header is truncated")
    _, version, magic = struct.unpack_from("<III", body, 0)
    if magic != 0x6F626A74:  # bytes "tjbo"
        raise ValueError(f"unexpected OBJT magic 0x{magic:08x}")

    entries = []
    pos = 12
    while pos < len(body):
        if pos + 4 > len(body):
            raise ValueError("truncated OBJT GUID")
        guid = struct.unpack_from("<I", body, pos)[0]
        pos += 4
        if guid == 0:
            entries.append({"guid": 0, "name": "", "main_version": 0, "type_id": 0})
            continue
        if pos + 12 > len(body):
            raise ValueError("truncated OBJT entry")
        unknown_1a, init_version, unknown_2a, main_version, type_id, objd_type = (
            struct.unpack_from("<HHHHHH", body, pos)
        )
        pos += 12
        nul = body.find(b"\0", pos)
        if nul < 0:
            raise ValueError("unterminated OBJT name")
        name = body[pos:nul].decode("latin1", "replace")
        pos = nul + 1
        if len(name) % 2 == 0:
            pos += 1
        if version > 2:
            pos += 4
        if pos > len(body):
            raise ValueError("OBJT entry extends beyond chunk")
        entries.append({
            "guid": guid,
            "name": name,
            "main_version": main_version,
            "type_id": type_id,
            "objd_type": objd_type,
            "init_version": init_version,
            "unknown_1a": unknown_1a,
            "unknown_2a": unknown_2a,
        })
    return entries


def parse_objm(body, objt_entries):
    if len(body) < 14:
        raise ValueError("ObjM header is truncated")
    _, version, magic = struct.unpack_from("<III", body, 0)
    if magic != 0x4F626A4D:
        raise ValueError(f"unexpected ObjM magic 0x{magic:08x}")
    if body[12] != 1:
        raise ValueError(f"expected compressed ObjM, got code {body[12]}")

    reader = FieldReader(body, 13)
    id_to_type = {}
    while True:
        object_id = reader.u16()
        if object_id == 0:
            break
        id_to_type[object_id] = reader.u16()

    raw_pos = reader.interrupt_position()
    instances = []
    for _ in range(len(id_to_type)):
        if raw_pos + 4 > len(body):
            raise ValueError("truncated ObjM instance offset")
        skip_offset = struct.unpack_from("<i", body, raw_pos)[0]
        data_start = raw_pos + 4
        skip_position = 12 + skip_offset
        if not (data_start <= skip_position <= len(body)):
            raise ValueError(f"invalid ObjM instance boundary {skip_position}")
        instance_reader = FieldReader(body[data_start:skip_position])

        for _ in range(4):
            instance_reader.i32()  # footprint
        x = instance_reader.i32()
        y = instance_reader.i32()
        level = instance_reader.i32()
        instance_reader.i16()  # unknown data
        attr_count = instance_reader.i16()
        if not (0 <= attr_count <= 4096):
            raise ValueError(f"implausible ObjM attribute count {attr_count}")
        for _ in range(attr_count):
            instance_reader.i16()
        for _ in range(8):
            instance_reader.i16()  # temp registers
        object_data = [instance_reader.i16() for _ in range(68)]
        for _ in range(5):
            instance_reader.i16()  # extra object data

        frame_count = instance_reader.i32()
        stack_flags = instance_reader.i32()
        if not (0 <= frame_count <= 4096):
            raise ValueError(f"implausible ObjM frame count {frame_count}")
        frames = []
        for depth in range(frame_count):
            stack_object = instance_reader.i16()
            tree_id = instance_reader.i16()
            node_id = instance_reader.i16()
            local_count = instance_reader.byte()
            parameter_count = instance_reader.byte()
            parameters = [instance_reader.i16() for _ in range(parameter_count)]
            locals_ = [instance_reader.i16() for _ in range(local_count)]
            primitive_state = instance_reader.i32()
            owner_type = instance_reader.i16()
            if not (1 <= owner_type <= len(objt_entries)):
                raise ValueError(f"invalid code-owner OBJT type {owner_type}")
            owner = objt_entries[owner_type - 1]
            frames.append({
                "depth": depth,
                "stack_object": stack_object,
                "routine_id": tree_id & 0xFFFF,
                "instruction_pointer": node_id & 0xFFFF,
                "parameters": parameters,
                "locals": locals_,
                "primitive_state": primitive_state,
                "owner_type": owner_type,
                "owner": owner,
            })

        object_id = object_data[11]
        type_id = id_to_type.get(object_id)
        if type_id is None or not (1 <= type_id <= len(objt_entries)):
            raise ValueError(f"ObjM object {object_id} has invalid OBJT type {type_id!r}")
        instances.append({
            "object_id": object_id,
            "type_id": type_id,
            "objt": objt_entries[type_id - 1],
            "frames": frames,
            "stack_flags": stack_flags,
            "x": x,
            "y": y,
            "level": level,
        })
        raw_pos = skip_position
    return version, id_to_type, instances


def parse_glob(body):
    if not body:
        return None
    if body[0] < 48:
        name = body[1:1 + body[0]].decode("latin1", "replace")
    else:
        name = body.split(b"\0", 1)[0].decode("latin1", "replace")
    name = name.strip()
    if not name:
        return None
    if not name.lower().endswith(".iff"):
        name += ".iff"
    return name.lower()


def parse_bhav(data, data_start, chunk_id, label, container):
    parsed = IFF.parse_bhav(data, data_start)
    if parsed is None:
        raise ValueError(f"cannot parse BHAV {chunk_id} in {container}")
    signature = parsed["sig"]
    if signature == 0x8003:
        version = struct.unpack_from("<H", data, data_start + 7)[0]
    elif signature == 0x8002:
        version = struct.unpack_from("<H", data, data_start + 8)[0]
    else:
        version = None
    return {
        "id": chunk_id,
        "label": label,
        "version": version,
        "instructions": len(parsed["instrs"]),
        "container": container,
    }


def parse_objd(body):
    if len(body) < 4 + 21 * 2:
        raise ValueError("OBJD is truncated")
    version = struct.unpack_from("<I", body, 0)[0]
    fields = struct.unpack_from(f"<{(len(body) - 4) // 2}H", body, 4)
    return {
        "version": version,
        "main_id": fields[3],
        "guid": fields[12] | (fields[13] << 16),
        "uses_function_table": fields[20],
    }


def parse_objf_main(body):
    if len(body) < 20:
        raise ValueError("OBJf is truncated")
    count = struct.unpack_from("<I", body, 12)[0]
    if count <= 1 or 16 + count * 4 > len(body):
        raise ValueError("OBJf has no complete main entry")
    return struct.unpack_from("<H", body, 16 + 4 + 2)[0]


def read_far(path):
    members = []
    with open(path, "rb") as source:
        for name, data in IFF.far_members(path, source):
            if data[:8] == b"IFF FILE":
                members.append((name, data))
    return members


def scan_globals():
    result = {}
    for member, data in read_far(GLOBAL_FAR):
        bhavs = {}
        for tag, chunk_id, label, data_start, _ in chunks(data):
            if tag == "BHAV":
                bhavs[chunk_id] = parse_bhav(data, data_start, chunk_id, label, member)
        result[os.path.basename(member).lower()] = {"file": member, "bhavs": bhavs}
    return result


def scan_objects():
    by_guid = {}
    for member, data in read_far(OBJECTS_FAR):
        bhavs = {}
        glob = None
        objds = {}
        objf_mains = {}
        for tag, chunk_id, label, data_start, body in chunks(data):
            if tag == "BHAV":
                bhavs[chunk_id] = parse_bhav(data, data_start, chunk_id, label, member)
            elif tag == "GLOB":
                glob = parse_glob(body)
            elif tag == "OBJD":
                objds[chunk_id] = parse_objd(body)
            elif tag == "OBJf":
                objf_mains[chunk_id] = parse_objf_main(body)
        for chunk_id, objd in objds.items():
            if objd["uses_function_table"]:
                main_id = objf_mains.get(chunk_id)
            else:
                main_id = objd["main_id"]
            record = {
                "file": member,
                "guid": objd["guid"],
                "glob": glob,
                "private_bhavs": bhavs,
                "main_id": main_id,
            }
            previous = by_guid.get(objd["guid"])
            if previous is not None:
                raise ValueError(
                    "duplicate current GUID {:08x} in {} and {}".format(objd["guid"], previous["file"], member)
                )
            by_guid[objd["guid"]] = record
    return by_guid


def resolve_routine(resource, routine_id, globals_):
    if routine_id < 4096:
        scope_name = "Global.iff"
        scope = globals_.get("global.iff")
        kind = "global"
    elif routine_id < 8192:
        return "private", resource["file"], resource["private_bhavs"].get(routine_id)
    else:
        scope_name = resource["glob"]
        scope = globals_.get(scope_name) if scope_name else None
        kind = "semiglobal"
    if scope is None:
        return kind, scope_name or "(none)", None
    return kind, scope["file"], scope["bhavs"].get(routine_id)


def extract_house():
    with open(HOUSE_PATH, "rb") as source:
        data = source.read()
    objt_body = None
    objm_body = None
    for tag, _, _, _, body in chunks(data):
        if tag in ("OBJT", "objt"):
            objt_body = body
        elif tag == "ObjM":
            objm_body = body
    if objt_body is None or objm_body is None:
        raise ValueError("House05 lacks OBJT or ObjM")
    objt = parse_objt(objt_body)
    version, id_to_type, instances = parse_objm(objm_body, objt)
    return version, objt, id_to_type, instances


def quoted(value):
    return '"{}"'.format(value.replace("\\", "\\\\").replace('"', '\\"'))


def build_report():
    globals_ = scan_globals()
    objects = scan_objects()
    objm_version, objt, id_to_type, instances = extract_house()
    instances_by_id = {item["object_id"]: item for item in instances}

    missing = []
    out_of_bounds = []
    unresolved_resources = []
    unresolved_scopes = []
    frame_total = 0
    for instance in instances:
        for frame in instance["frames"]:
            frame_total += 1
            owner = frame["owner"]
            resource = objects.get(owner["guid"])
            item = {
                "object_id": instance["object_id"],
                "depth": frame["depth"],
                "routine_id": frame["routine_id"],
                "ip": frame["instruction_pointer"],
                "owner_type": frame["owner_type"],
                "owner_guid": owner["guid"],
                "owner_name": owner["name"],
                "saved_main_version": owner["main_version"],
                "resource": resource,
            }
            if resource is None:
                unresolved_resources.append(item)
                continue
            kind, scope_file, routine = resolve_routine(resource, frame["routine_id"], globals_)
            item["scope_kind"] = kind
            item["scope_file"] = scope_file
            item["routine"] = routine
            if routine is None:
                if kind == "semiglobal" and resource["glob"] not in globals_:
                    unresolved_scopes.append(item)
                else:
                    missing.append(item)
            elif frame["instruction_pointer"] >= routine["instructions"]:
                out_of_bounds.append(item)

    missing.sort(key=lambda item: (item["object_id"], item["depth"]))
    out_of_bounds.sort(key=lambda item: (item["object_id"], item["depth"]))
    unresolved_resources.sort(key=lambda item: (item["object_id"], item["depth"]))

    expected_missing = [
        (10, 0, 4096, 1, 0x6B264238),
        (10, 1, 4113, 0, 0x6B264238),
        (123, 1, 4109, 0, 0x28975923),
        (131, 1, 4109, 0, 0xDF77EE12),
        (133, 0, 4096, 0, 0xD1CF9596),
        (134, 0, 4096, 2, 0xFBE586B7),
        (173, 0, 4096, 1, 0xD5CA2D74),
        (174, 0, 4096, 1, 0x24E26386),
        (191, 0, 4096, 1, 0xC59350C6),
        (192, 0, 4096, 1, 0x23A15547),
        (194, 1, 4109, 0, 0xFD746AD2),
        (226, 0, 4096, 0, 0x7D2089B6),
    ]
    actual_missing = [
        (item["object_id"], item["depth"], item["routine_id"], item["ip"], item["owner_guid"])
        for item in missing
    ]
    expected_oob = [(object_id, 0, 4096, 3, 2) for object_id in (114, 115, 116, 117)]
    actual_oob = [
        (
            item["object_id"],
            item["depth"],
            item["routine_id"],
            item["ip"],
            item["routine"]["instructions"],
        )
        for item in out_of_bounds
    ]

    assert len(instances) == len(id_to_type) == 245, (len(instances), len(id_to_type))
    assert frame_total == 522, frame_total
    assert actual_missing == expected_missing, actual_missing
    assert len({item["object_id"] for item in missing}) == 11
    assert actual_oob == expected_oob, actual_oob
    assert not unresolved_scopes, unresolved_scopes
    assert len(unresolved_resources) == 9, len(unresolved_resources)
    assert {item["object_id"] for item in unresolved_resources} == {16, 18, 21}

    invalid_ids = sorted({item["object_id"] for item in missing + out_of_bounds})
    expected_invalid_ids = [10, 114, 115, 116, 117, 123, 131, 133, 134, 173, 174, 191, 192, 194, 226]
    assert invalid_ids == expected_invalid_ids, invalid_ids

    replacement_order = ("Counters.iff", "Lamps.iff", "Paintings.iff", "phones.iff", "Sofas.iff")
    replacement_expected = {
        "counters.iff": (4096, 2, 2),
        "lamps.iff": (8197, 2, 5),
        "paintings.iff": (8194, 1, 8),
        "phones.iff": (8284, 4, 13),
        "sofas.iff": (8208, 6, 7),
    }
    replacement_rows = []
    for family in replacement_order:
        affected = []
        saved_versions = set()
        resource = None
        for object_id in invalid_ids:
            instance = instances_by_id[object_id]
            current = objects.get(instance["objt"]["guid"])
            if current is not None and current["file"].lower() == family.lower():
                affected.append(object_id)
                saved_versions.add(instance["objt"]["main_version"])
                resource = current
        assert resource is not None and affected, family
        scope_kind, scope_file, main = resolve_routine(resource, resource["main_id"], globals_)
        assert main is not None, (family, resource["main_id"])
        actual = (resource["main_id"], main["version"], main["instructions"])
        assert actual == replacement_expected[family.lower()], (family, actual)
        replacement_rows.append({
            "family": family,
            "object_ids": affected,
            "saved_main_versions": sorted(saved_versions),
            "main_id": resource["main_id"],
            "main": main,
            "scope_kind": scope_kind,
            "scope_file": scope_file,
        })

    lines = [
        "R164 House05 saved-frame compatibility diagnostic",
        "verdict: PASS",
        "",
        "inputs (local owned-data metadata only)",
        f"  House05.iff sha256={sha256(HOUSE_PATH)}",
        f"  Objects.far sha256={sha256(OBJECTS_FAR)}",
        f"  Global.far sha256={sha256(GLOBAL_FAR)}",
        f"  parser={os.path.relpath(R154_PATH, REPO)}",
        f"  objm_version={objm_version} objt_entries={len(objt)}",
        "",
        "house census",
        "  instances=245",
        "  saved_frames=522",
        "  unresolved_character_resource_frames=9 object_ids=16,18,21 (excluded; character IFFs are outside Objects.far)",
        "",
        "missing routine frames",
        "  frames=12 objects=11 object_ids={}".format(",".join(str(value) for value in sorted({item["object_id"] for item in missing}))),
    ]
    for item in missing:
        lines.append(
            f"  object_id={item['object_id']} depth={item['depth']} "
            f"routine={item['routine_id']} ip={item['ip']} owner_type={item['owner_type']} "
            f"owner_guid={item['owner_guid']:08x} owner_name={quoted(item['owner_name'])} "
            f"resource={item['resource']['file']} scope={item['scope_kind']}:{item['scope_file']}"
        )

    lines.extend([
        "",
        "out-of-bounds instruction frames",
        "  frames=4 objects=4 object_ids=114,115,116,117",
    ])
    for item in out_of_bounds:
        routine = item["routine"]
        lines.append(
            f"  object_id={item['object_id']} depth={item['depth']} "
            f"routine={item['routine_id']} ip={item['ip']} instructions={routine['instructions']} "
            f"owner_type={item['owner_type']} owner_guid={item['owner_guid']:08x} "
            f"owner_name={quoted(item['owner_name'])} resource={item['resource']['file']} "
            f"scope={item['scope_kind']}:{item['scope_file']}"
        )

    lines.extend(["", "current replacement mains"])
    for row in replacement_rows:
        main = row["main"]
        lines.append(
            f"  resource={row['family']} "
            f"affected_object_ids={','.join(str(value) for value in row['object_ids'])} "
            f"saved_main_versions={','.join(str(value) for value in row['saved_main_versions'])} "
            f"main_id={row['main_id']} main_version={main['version']} "
            f"instructions={main['instructions']} label={quoted(main['label'])} "
            f"scope={row['scope_kind']}:{row['scope_file']}"
        )

    lines.extend([
        "",
        "pinned invalid continuation object_ids={}".format(",".join(str(value) for value in invalid_ids)),
        "classification: 12 missing-routine frames plus 4 stale-IP frames; these are saved continuation incompatibilities against current replacement resources.",
    ])
    return "\n".join(lines) + "\n"


def main():
    report = build_report()
    with open(OUTPUT_PATH, "w", encoding="utf-8", newline="\n") as output:
        output.write(report)
    print(report, end="")
    print(f"wrote {OUTPUT_PATH}")


if __name__ == "__main__":
    main()
