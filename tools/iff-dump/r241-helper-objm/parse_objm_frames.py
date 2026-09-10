#!/usr/bin/env python3
"""R241 fixture: decode original OBJM TreeStack frames from saved lots.

Independent, read-only. Implements the serialization recovered from the
original PowerPC executable (TreeStack::ReconStream 0x153080, ReconBuffer
compressed readers 0x11e9c0/0x11e7e0/0x11ebe0 + ReconCmprInt 0x11f080 with
Precision schemes {5,8,13,16}/{6,11,21,32}/{2,4,6,8}) and validates each
object parse against the OBJM skip table's ReconMark position.

Per-frame layout (stream version > 25, on-disk order):
  s16 stack_object_id         (frame+4; absent only for version == 25)
  s16 tree_id                 (frame+0; bit15 = runtime flag, not serialized)
  s16 instruction_pointer     (frame+2; TreeSim::DoNodeAction range < 0xFB)
  u8  locals_count            (frame+6)
  u8  params_count            (frame+7)
  s16 params[params_count]    (frame+0x10...)
  s16 locals[locals_count]
  s32 primitive_state         (frame+8; opcode35 stores its phase here)
  s16 code_owner_objtype      (BehaviorFinder virtual; runtime frame+0xc)

Usage: python3 parse_objm_frames.py [max_files]
"""
import json
import struct
import sys
from pathlib import Path

ROOT = Path("/Users/nathannoom/Developer/Games/The Sims/simitone-fork")
DATA = ROOT / "game-data/The Sims"
HERE = Path(__file__).resolve().parent


class BitReader:
    """ReconBuffer read mode: ReconBits (MSB-first) + ReconCmprInt schemes."""

    def __init__(self, data, bitpos_bits):
        self.data = data
        self.bit = bitpos_bits

    def bits(self, n):
        v = 0
        for _ in range(n):
            v = (v << 1) | ((self.data[self.bit >> 3] >> (7 - (self.bit & 7))) & 1)
            self.bit += 1
        return v

    def cmpr(self, widths):
        if self.bits(1) == 0:
            return 0
        sel = self.bits(2)
        w = widths[sel]
        v = self.bits(w)
        if v & (1 << (w - 1)):
            v -= 1 << w
        return v

    def i8(self):
        return self.cmpr((2, 4, 6, 8))

    def i16(self):
        return self.cmpr((5, 8, 13, 16))

    def i32(self):
        return self.cmpr((6, 11, 21, 32))

    @property
    def bytepos(self):
        return (self.bit + 7) >> 3


class Objm:
    def __init__(self, data):
        self.data = data
        self.version = struct.unpack_from("<I", data, 4)[0]
        magic, = struct.unpack_from("<I", data, 8)
        assert magic == 0x4F626A4D, hex(magic)
        assert data[12] == 1, "compression code"
        f = BitReader(data, 13 * 8)
        self.ids = []
        while True:
            oid = f.i16()
            if oid == 0:
                break
            self.ids.append(oid)
            f.i16()
        # After the id pairs the stream is byte-aligned. Each object block is
        # preceded by a raw little-endian int32 whose value (relative to the
        # 12-byte header) locates the ReconMark that terminates that object's
        # compressed data. The mark itself is that int's own slot; the next
        # object starts right after it. (Validated by r239's chain walk.)
        pos = f.bytepos
        self.objects = []
        self.chain_ok = True
        marks = []
        for _ in self.ids:
            (skip,) = struct.unpack_from("<i", data, pos)
            mark = 12 + skip
            marks.append(mark)
            self.objects.append((pos + 4, mark))
            pos = mark
        # chain law: the int stored at mark_i points at mark_{i+1};
        # the last mark points at itself (r239 chain walk).
        self.final_mark_zero = True
        for i, mark in enumerate(marks):
            nxt = struct.unpack_from("<i", data, mark)[0]
            if i + 1 < len(marks):
                if 12 + nxt != marks[i + 1]:
                    self.chain_ok = False
            elif nxt != 0:
                self.final_mark_zero = False
        self.trailer = pos
        self.final_mark = struct.unpack_from("<i", data, pos)[0] if pos + 4 <= len(data) else None
        self.owner_bit = (pos + 4) * 8

    def mark_value(self, obj_index):
        start, mark = self.objects[obj_index]
        return struct.unpack_from("<i", self.data, mark)[0], mark


def parse_stack(f):
    """Parse frames; returns list of dicts with byte-accurate offsets."""
    count = f.i32()
    if count < 0 or count > 64:
        raise ValueError(f"stack count {count}")
    # stack byte-size long follows (TreeStack::ReconStream 0x153200)
    size = f.i32()
    frames = []
    for _ in range(count):
        fr = {}
        fr["stack_object"] = f.i16()
        fr["tree"] = f.i16()
        fr["node"] = f.i16()
        fr["locals_n"] = f.i8()
        fr["params_n"] = f.i8()
        if not (0 <= fr["locals_n"] <= 20 and 0 <= fr["params_n"] <= 12):
            raise ValueError(f"counts {fr}")
        if not (-1 <= fr["node"] < 0xFB):
            raise ValueError(f"node {fr['node']}")
        fr["params"] = [f.i16() for _ in range(fr["params_n"])]
        fr["locals"] = [f.i16() for _ in range(fr["locals_n"])]
        fr["primitive_state"] = f.i32()
        fr["code_owner_objtype"] = f.i16()
        frames.append(fr)
    return count, size, frames


def parse_to_stack(f):
    """Sequential fields before the stack (OBJM.cs OBJMInstance prefix)."""
    for _ in range(4):  # footprint
        f.i32()
    f.i32()  # X
    f.i32()  # Y
    level = f.i32()
    if level not in (1, 2):
        raise ValueError(f"level {level}")
    f.i16()  # unknown (== 1)
    attr_n = f.i16()
    if not (0 <= attr_n <= 0x100):
        raise ValueError(f"attr_n {attr_n}")
    for _ in range(attr_n):
        f.i16()
    for _ in range(8):  # temps
        f.i16()
    for _ in range(68):  # object data
        f.i16()
    for _ in range(5):  # extra data
        f.i16()


def main():
    import runpy
    helpers = runpy.run_path(str(ROOT / "tools/iff-dump/r154/r154-callers-scan.py"))
    walk = helpers["walk_chunks"]
    max_files = int(sys.argv[1]) if len(sys.argv) > 1 else 10 ** 6
    stats = dict(files=0, objm=0, objects=0, stacks=0, frames=0,
                 phase={}, state_values={}, errors=[], versions={},
                 mark_self_ok=0, mark_bad=0)
    phase_frames = []
    examples = []
    files = sorted(DATA.glob("UserData*/Houses/House*.iff"))
    for path in files[:max_files]:
        data = path.read_bytes()
        stats["files"] += 1
        for tag, off, size, cid, flags, label, body_start in walk(data):
            if tag != "ObjM":
                continue
            body = data[body_start:off + size]
            try:
                objm = Objm(body)
                stats["objm"] += 1
                stats["versions"][objm.version] = stats["versions"].get(objm.version, 0) + 1
                for i in range(len(objm.ids)):
                    stats["objects"] += 1
                    try:
                        start, mark = objm.objects[i]
                        f = BitReader(body, start * 8)
                        # start is a byte offset; BitReader takes bits
                        parse_to_stack(f)
                        count, size, frames = parse_stack(f)
                        stats["stacks"] += 1
                        stats["frames"] += len(frames)
                        if f.bytepos <= mark:
                            stats["mark_self_ok"] += 1
                        else:
                            stats["mark_bad"] += 1
                            stats["errors"].append(
                                f"{path.name} obj#{i}: OVERSHOT mark {mark} -> {f.bytepos}")
                            break
                        if not objm.chain_ok:
                            stats["chain_broken"] = stats.get("chain_broken", 0) + 1
                            if stats["chain_broken"] < 4:
                                stats["errors"].append(f"{path.name}: mark chain broken")
                        for fr in frames:
                            if not (-0x8000 <= fr["tree"] < 0x8000 and
                                    0 <= fr["code_owner_objtype"] < 0x1000):
                                raise ValueError(f"frame fields {fr}")
                        if len(examples) < 8 and count:
                            examples.append(dict(
                                file=path.name, obj=objm.ids[i],
                                stack_bytes=size,
                                bitpos_after_stack=f.bit,
                                mark_pos=mark,
                                frames=frames))
                        stats.setdefault("sizes", {})
                        key = str(size)
                        stats["sizes"][key] = stats["sizes"].get(key, 0) + 1
                        for fr in frames:
                            st = fr["primitive_state"]
                            stats["state_values"][str(st)] = stats["state_values"].get(str(st), 0) + 1
                            if st in (1, 2):
                                stats["phase"][str(st)] = stats["phase"].get(str(st), 0) + 1
                                phase_frames.append(dict(file=path.name, obj=objm.ids[i], **fr))
                    except ValueError as e:
                        stats["errors"].append(f"{path.name} obj#{i}: {e}")
                        break
            except (AssertionError, struct.error, IndexError) as e:
                stats["errors"].append(f"{path.name}: {e!r}")
            else:
                ow = BitReader(body, objm.owner_bit).i16() if objm.owner_bit + 8 <= len(body) * 8 else None
                stats.setdefault("owners_nonzero", 0)
                if ow:
                    stats["owners_nonzero"] += 1
                fm = objm.final_mark
                stats.setdefault("final_marks", {})
                stats["final_marks"][str(fm)] = stats["final_marks"].get(str(fm), 0) + 1
    print(json.dumps(stats, indent=2))
    print("\nSAMPLE FRAMES:")
    for ex in examples:
        print(json.dumps(ex, indent=1))
    print(f"\nIN-PROGRESS OPCODE35 CANDIDATES (primitive_state 1/2): {len(phase_frames)}")
    for fr in phase_frames[:40]:
        print(json.dumps(fr))
    out = dict(stats=stats, phase_frames=phase_frames[:200], examples=examples)
    (HERE / "objm-frame-corpus.json").write_text(json.dumps(out, indent=2) + "\n")


if __name__ == "__main__":
    main()
