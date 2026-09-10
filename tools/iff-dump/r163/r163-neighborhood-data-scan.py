#!/usr/bin/env python3
"""R163: inventory every executable use of VM scope 34 (NeighborhoodData).

The scan covers every loose IFF/FAM and every IFF/FAM FAR member in the
owned original-game corpus.  It reports both the physical corpus and the
basename-last-wins view used by the port's object-resource index.

Scope fields are decoded from every primitive operand type which contains a
VMVariableScope in the current SimAntics engine: expression, random,
get-distance, get-direction, breakpoint, relationship, TS1 budget,
set-motive-change, set-to-next, test-object-type, and inventory operations.

Run from anywhere:
    python3 tools/iff-dump/r163/r163-neighborhood-data-scan.py

The script prints and rewrites ``r163-neighborhood-data-scan.txt`` beside
itself.  It imports the checked-in R154 IFF/FAR reader and creates no cache.
"""

from collections import Counter
import importlib.util
import os
import struct


HERE = os.path.dirname(os.path.abspath(__file__))
R154 = os.path.normpath(os.path.join(HERE, "..", "r154"))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "game-data", "The Sims"))
OUTPUT = os.path.join(HERE, "r163-neighborhood-data-scan.txt")
TARGET_SCOPE = 34


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


IFF = load_module("r154_iff_neighborhood_data", os.path.join(R154, "r154-callers-scan.py"))


OPERATOR_NAMES = {
    0: "GreaterThan",
    1: "LessThan",
    2: "Equals",
    3: "PlusEquals",
    4: "MinusEquals",
    5: "Assign",
    6: "MulEquals",
    7: "DivEquals",
    8: "IsFlagSet",
    9: "SetFlag",
    10: "ClearFlag",
    11: "IncAndLessThan",
    12: "ModEquals",
    13: "AndEquals",
    14: "GreaterThanOrEqualTo",
    15: "LessThanOrEqualTo",
    16: "NotEqualTo",
    17: "DecAndGreaterThan",
    18: "TS1OrEquals",
    19: "TS1XorEquals",
    20: "TS1AssignSqrtRHS",
}

# Expression LHS access follows VMExpression.Execute.  RHS is read for every
# recognized TS1 operator.  A read+write occurrence is one operand reference
# but contributes to both the read and write totals.
EXPRESSION_READ_ONLY = {0, 1, 2, 8, 14, 15, 16}
EXPRESSION_WRITE_ONLY = {5, 20}
EXPRESSION_READ_WRITE = {3, 4, 6, 7, 9, 10, 11, 12, 13, 17, 18, 19}


def iter_iffs():
    """Yield deterministic (physical display name, bytes) IFF records."""
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames.sort(key=str.lower)
        for filename in sorted(filenames, key=str.lower):
            lower = filename.lower()
            path = os.path.join(dirpath, filename)
            display = os.path.relpath(path, ROOT)
            if lower.endswith((".iff", ".fam")):
                try:
                    with open(path, "rb") as source:
                        yield display, source.read()
                except OSError:
                    continue
            elif lower.endswith(".far"):
                try:
                    with open(path, "rb") as source:
                        for member, data in IFF.far_members(path, source):
                            if member.lower().endswith((".iff", ".fam")):
                                yield display + "!" + member, data
                except OSError:
                    continue


def basename_key(display):
    member = display.rsplit("!", 1)[-1].replace("\\", "/")
    return member.rsplit("/", 1)[-1].lower()


def i16(data, offset):
    return struct.unpack_from("<h", data, offset)[0]


def u16(data, offset):
    return struct.unpack_from("<H", data, offset)[0]


def ref(role, access, data, detail):
    return {"role": role, "access": access, "data": data, "detail": detail}


def scope_refs(opcode, operand):
    """Decode all effective VMVariableScope operands for one instruction."""
    refs = []

    if opcode == 2:  # VMExpressionOperand
        operator = operand[5]
        lhs_access = None
        if operator in EXPRESSION_READ_ONLY:
            lhs_access = "read"
        elif operator in EXPRESSION_WRITE_ONLY:
            lhs_access = "write"
        elif operator in EXPRESSION_READ_WRITE:
            lhs_access = "read+write"
        else:
            # Preserve unknown operators as visible regression failures rather
            # than silently dropping otherwise valid scope bytes.
            lhs_access = "unknown"
        detail = "operator=%d:%s" % (operator, OPERATOR_NAMES.get(operator, "unknown"))
        refs.append((operand[6], ref("lhs", lhs_access, i16(operand, 0), detail)))
        rhs_access = "read" if operator in OPERATOR_NAMES else "unknown"
        refs.append((operand[7], ref("rhs", rhs_access, i16(operand, 2), detail)))

    elif opcode == 8:  # VMRandomNumberOperand
        refs.append((u16(operand, 2), ref("destination", "write", i16(operand, 0), "random")))
        refs.append((u16(operand, 6), ref("range", "read", i16(operand, 4), "random")))

    elif opcode == 11:  # VMGetDistanceToOperand
        if operand[2] & 1:  # otherwise the loader replaces it with MyObject[11]
            refs.append((operand[3], ref("object", "read", i16(operand, 4), "get-distance")))

    elif opcode == 12:  # VMGetDirectionToOperand
        refs.append((u16(operand, 2), ref("result", "write", i16(operand, 0), "get-direction")))
        if operand[4] & 1:  # otherwise the loader replaces it with MyObject[11]
            refs.append((operand[5], ref("object", "read", i16(operand, 6), "get-direction")))

    elif opcode == 15:  # VMBreakPointOperand condition
        refs.append((u16(operand, 2), ref("condition", "read", i16(operand, 0), "breakpoint")))

    elif opcode == 26:  # VMRelationshipOperand; opcode 24 has a fixed Parameter scope
        flags = operand[2]
        set_mode = ((flags >> 2) & 1) | ((flags >> 4) & 2)
        if set_mode == 0:
            access = "write"
        elif set_mode in (1, 2):
            access = "read"
        else:
            access = None  # mode 3 does not access VarScope in VMRelationship.Execute
        if access is not None:
            refs.append((u16(operand, 4), ref("relationship-value", access,
                                             i16(operand, 6), "set-mode=%d" % set_mode)))

    elif opcode == 25:  # TS1 VMTransferFundsOperand / budget
        old_owner = operand[0]
        if old_owner not in (0, 1, 2):  # Normal/default: byte 1 is the effective scope
            refs.append((operand[1], ref("amount", "read", u16(operand, 2), "TS1-budget")))

    elif opcode == 29:  # VMSetMotiveChangeOperand
        if not (operand[3] & 1):  # ClearAll bypasses both data operands
            refs.append((operand[0], ref("delta", "read", i16(operand, 4), "motive-change")))
            refs.append((operand[1], ref("maximum", "read", i16(operand, 6), "motive-change")))

    elif opcode == 31:  # VMSetToNextOperand
        if operand[4] & 0x80:  # otherwise loader forces StackObjectID[0]
            refs.append((operand[5], ref("target", "read+write", operand[7], "set-to-next")))

    elif opcode == 32:  # VMTestObjectTypeOperand
        refs.append((operand[6], ref("object-id", "read", i16(operand, 4), "test-object-type")))

    elif opcode == 67:  # VMInventoryOperationsOperand
        mode = operand[4] & 0x7F
        if mode in (4, 5):
            access = "read"
        elif mode in (34, 38, 127):
            access = "write"
        elif mode in (35, 36, 37):
            access = "read+write"
        else:
            access = None
        if access is not None:
            refs.append((operand[5], ref("inventory-value", access,
                                         i16(operand, 6), "mode=%d" % mode)))

    return [item for scope, item in refs if scope == TARGET_SCOPE]


def scan(records):
    hits = []
    bhav_count = 0
    instruction_count = 0

    for display, data in records:
        if not data.startswith(b"IFF FILE"):
            continue
        for tag, offset, size, chunk_id, _flags, label, data_start in IFF.walk_chunks(data):
            if tag != "BHAV":
                continue
            parsed = IFF.parse_bhav(data, data_start)
            if parsed is None:
                continue
            bhav_count += 1
            instruction_count += len(parsed["instrs"])
            for instruction_index, instruction in enumerate(parsed["instrs"]):
                opcode = instruction[0]
                operand = struct.pack("<hhhh", *instruction[3:7])
                for found in scope_refs(opcode, operand):
                    hit = dict(found)
                    hit.update({
                        "display": display,
                        "chunk_id": chunk_id,
                        "label": label,
                        "instruction": instruction_index,
                        "opcode": opcode,
                    })
                    hits.append(hit)

    hits.sort(key=lambda hit: (
        hit["display"].lower(), hit["chunk_id"], hit["instruction"], hit["role"]
    ))
    return {
        "records": sum(1 for _, data in records if data.startswith(b"IFF FILE")),
        "bhavs": bhav_count,
        "instructions": instruction_count,
        "hits": hits,
    }


def count_access(hits, access):
    return sum(access in hit["access"].split("+") for hit in hits)


def format_counter(counter, value_format=str):
    return ", ".join("%s:%d" % (value_format(key), counter[key])
                     for key in sorted(counter, key=lambda value: str(value).lower())) or "none"


def summary_lines(name, result):
    hits = result["hits"]
    opcodes = Counter(hit["opcode"] for hit in hits)
    roles = Counter(hit["role"] for hit in hits)
    indices = Counter(hit["data"] for hit in hits)
    operators = Counter(
        hit["detail"].split(":", 1)[1]
        for hit in hits if hit["detail"].startswith("operator=")
    )
    return [
        "%s:" % name,
        "  IFF records: %d" % result["records"],
        "  BHAV chunks: %d" % result["bhavs"],
        "  BHAV instructions: %d" % result["instructions"],
        "  scope-34 operand references: %d" % len(hits),
        "  references which read: %d" % count_access(hits, "read"),
        "  references which write: %d" % count_access(hits, "write"),
        "  opcodes: %s" % format_counter(opcodes),
        "  expression operators: %s" % format_counter(operators),
        "  operand roles: %s" % format_counter(roles),
        "  data indices: %s" % format_counter(indices),
    ]


def hit_fingerprint(hit):
    return (
        hit["display"], hit["chunk_id"], hit["label"], hit["instruction"],
        hit["opcode"], hit["role"], hit["access"], hit["data"], hit["detail"],
    )


def build_report():
    physical_records = list(iter_iffs())
    mounted_by_basename = {}
    for display, data in physical_records:
        mounted_by_basename[basename_key(display)] = (display, data)
    mounted_records = sorted(mounted_by_basename.values(), key=lambda item: item[0].lower())

    physical = scan(physical_records)
    mounted = scan(mounted_records)
    physical_fingerprints = [hit_fingerprint(hit) for hit in physical["hits"]]
    mounted_fingerprints = [hit_fingerprint(hit) for hit in mounted["hits"]]

    expected = (
        physical["records"] == 2238
        and mounted["records"] == 1375
        and len(physical["hits"]) == 30
        and len(mounted["hits"]) == 30
        and count_access(physical["hits"], "read") == 30
        and count_access(mounted["hits"], "read") == 30
        and count_access(physical["hits"], "write") == 0
        and count_access(mounted["hits"], "write") == 0
        and Counter(hit["data"] for hit in physical["hits"]) == Counter({1: 3, 2: 27})
        and Counter(hit["role"] for hit in physical["hits"]) == Counter({"lhs": 11, "rhs": 19})
        and all(hit["opcode"] == 2 and hit["detail"] == "operator=2:Equals"
                for hit in physical["hits"])
        and physical_fingerprints == mounted_fingerprints
    )

    lines = [
        "R163 NEIGHBORHOOD-DATA SCOPE CORPUS SCAN",
        "corpus: game-data/The Sims (owned original assets; no bytes copied)",
        "scope: VMVariableScope.NeighborhoodData = 34",
        "enumeration: every loose/FAR-member IFF/FAM; deterministic case-folded path order",
        "mounted view: final physical record for each case-insensitive member basename",
        "operand schema coverage: opcodes 2,8,11,12,15,25,26,29,31,32,67",
        "parser: checked-in R154 resynchronizing IFF/FAR reader",
        "",
    ]
    lines.extend(summary_lines("physical corpus", physical))
    lines.append("")
    lines.extend(summary_lines("basename-last-wins mounted view", mounted))
    lines.extend([
        "",
        "physical and mounted hit fingerprints identical: %s" %
        ("YES" if physical_fingerprints == mounted_fingerprints else "NO"),
        "regression verdict: %s" % ("PASS" if expected else "FAIL"),
        "",
        "PHYSICAL HIT LIST",
    ])
    for hit in physical["hits"]:
        lines.append(
            "%s | BHAV %d %r | ins=%d opcode=%d | %s %s NeighborhoodData[%d] | %s" %
            (hit["display"], hit["chunk_id"], hit["label"], hit["instruction"],
             hit["opcode"], hit["role"], hit["access"], hit["data"], hit["detail"])
        )
    lines.extend([
        "",
        "Interpretation: all effective scope-34 references are equality operands, so both",
        "LHS and RHS occurrences are reads. No decoded primitive operand writes scope 34.",
    ])

    if not expected:
        raise RuntimeError("NeighborhoodData corpus regression failed\n" + "\n".join(lines))
    return "\n".join(lines) + "\n"


if __name__ == "__main__":
    report = build_report()
    print(report, end="")
    with open(OUTPUT, "w", encoding="utf-8", newline="\n") as output:
        output.write(report)
