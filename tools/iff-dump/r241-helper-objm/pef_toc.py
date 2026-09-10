#!/usr/bin/env python3
"""R241 helper: resolve PEF TOC-relative globals and dump ReconCmprInt schemes.

Section table starts at file offset 44 (header is 44 bytes for this image),
28 bytes per entry: nameOffset, totalSize, packedSize, unpackedSize,
containerOffset, then kind/share/align/reserved bytes. The TOC base used by
`lwz rX, -N(r2)` was anchored by round r232 ("TOC-29216 -> BSS 0x90850"):
TOC vaddr = 0x97a70. This is independently re-verified below against the
r240-pip anchor (TOC[-0x4f58] holds code vaddr 0x59b7a0 = file 0x5a4630).

Usage: python3 pef_toc.py
"""
import struct

EXE = "/Users/nathannoom/Developer/Games/The Sims/simitone-fork/game-data/The Sims/The Sims Complete"
TOC = 0x97A70  # verified below against two independent prior-round anchors

GLOBALS = {
    "Recon16 scheme (TOC-0x5868)": -0x5868,
    "Recon32 scheme (TOC-0x5864)": -0x5864,
}


def unpack(d):
    out = bytearray()
    i = 0
    n = len(d)
    while i < n:
        opb = d[i]; i += 1
        op = opb >> 5
        cnt = opb & 0x1F
        if cnt == 0:
            v = 0
            while True:
                b = d[i]; i += 1
                v = ((v << 7) | (b & 0x7F)) & 0xFFFFFFFF
                if not (b & 0x80):
                    break
            cnt = v
        if op == 0:
            out += b"\x00" * cnt
        elif op == 1:
            out += d[i:i+cnt]; i += cnt
        elif op == 2:
            rc = 0
            while True:
                b = d[i]; i += 1
                rc = ((rc << 7) | (b & 0x7F)) & 0xFFFFFFFF
                if not (b & 0x80):
                    break
            blk = d[i:i+cnt]; i += cnt
            out += blk * (rc + 1)
        elif op in (3, 4):
            custom = 0
            while True:
                b = d[i]; i += 1
                custom = ((custom << 7) | (b & 0x7F)) & 0xFFFFFFFF
                if not (b & 0x80):
                    break
            rc = 0
            while True:
                b = d[i]; i += 1
                rc = ((rc << 7) | (b & 0x7F)) & 0xFFFFFFFF
                if not (b & 0x80):
                    break
            common = b"\x00" * cnt if op == 4 else d[i:i+cnt]
            if op == 3:
                i += cnt
            out += common
            for _ in range(rc):
                out += d[i:i+custom]; i += custom
                out += common
        else:
            raise ValueError(f"reserved op {op}")
    return bytes(out)


class PEF:
    def __init__(self, data):
        self.data = data
        count = 0
        self.sections = []
        off = 44
        while True:
            name_off, total, packed, unpacked, foff = struct.unpack_from(">iIIII", data, off)
            kind = data[off+20]
            if foff == 0 and total == 0 and packed == 0 and unpacked == 0:
                break
            self.sections.append(dict(idx=count, total=total, packed=packed,
                                      unpacked=unpacked, file_off=foff, kind=kind))
            count += 1
            off += 28
            if count > 20:
                break
        self.cache = {}

    def section_data(self, sec):
        if sec["idx"] in self.cache:
            return self.cache[sec["idx"]]
        raw = self.data[sec["file_off"]:sec["file_off"]+sec["packed"]]
        out = unpack(raw) if sec["kind"] in (2, 3) else raw
        self.cache[sec["idx"]] = out
        return out

    def read_ptr(self, vaddr):
        """vaddr is section-relative from 0; search sections that cover it."""
        results = []
        for sec in self.sections:
            span = max(sec["total"], sec["unpacked"])
            if sec["total"] == 0 and sec["unpacked"] == 0:
                continue
            if 0 <= vaddr < span:
                d = self.section_data(sec)
                if vaddr + 4 <= len(d):
                    results.append((sec, struct.unpack_from(">I", d, vaddr)[0]))
        return results


def main():
    data = open(EXE, "rb").read()
    pef = PEF(data)
    for s in pef.sections:
        print(f"  [{s['idx']}] kind={s['kind']} total={s['total']:#x} packed={s['packed']:#x} "
              f"unpacked={s['unpacked']:#x} container={s['file_off']:#x}")

    # Anchor 1 (r240-pip): TOC-0x4f58 holds code vaddr 0x59b7a0 (file 0x5a4630)
    hits = pef.read_ptr(TOC - 0x4F58)
    for sec, val in hits:
        print(f"anchor1: TOC-0x4f58 in section {sec['idx']} -> {val:#x} "
              f"({'OK' if val == 0x59b7a0 else 'MISMATCH'})")
    # Anchor 2 (r232): TOC-0x7220 = 0x90850 (BSS)
    hits = pef.read_ptr(TOC - 0x7220)
    for sec, val in hits:
        print(f"anchor2: TOC-0x7220 in section {sec['idx']} -> {val:#x} "
              f"({'OK' if val == 0x90850 else 'MISMATCH'})")

    for label, rel in GLOBALS.items():
        hits = pef.read_ptr(TOC + rel)
        for sec, ptr in hits:
            print(f"\n{label}: TOC entry (section {sec['idx']}) -> {ptr:#x}")
            # scheme: find ptr in data/const sections
            for sec2, in [ ]:
                pass
            for cand in pef.sections:
                span = max(cand["total"], cand["unpacked"])
                if cand["total"] == 0 and cand["unpacked"] == 0:
                    continue
                if 0 <= ptr < span:
                    d = pef.section_data(cand)
                    if ptr + 40 <= len(d):
                        b = d[ptr:ptr+40]
                        print(f"  scheme bytes @ {ptr:#x} (section {cand['idx']}): {b.hex()}")
                        for k in range(5):
                            w, mask = struct.unpack_from(">iI", b, k*8)
                            print(f"    bucket {k}: width={w} signext_mask={mask:#010x}")


if __name__ == "__main__":
    main()
