#!/usr/bin/env python3
"""env258.py — UI-24 PEF-revisit shared environment.

Combines pef_load.py (UI-15 tooling: expanded + relocation-event-tracked data
image) with ppc_decode.py's instruction decoder into one importable env.

Address conventions (validated against the law docs):
  * Law-doc "file offsets" for code = section-offset + 0x8E90 (code
    containerOffset). ppc_decode.py takes file offsets.
  * Data-section pointers (TOC entries etc.) are section-relative offsets into
    the expanded image; >= unpackedLength (0x7BF80) => BSS (zero-init).
  * pef_load patch events give (kind, dataOffset, fileWord, sectionValue):
    loaded word = fileWord + sectionValue for C/D/bySection patches.
"""
import struct
import sys
import os

_HERE = os.path.dirname(os.path.abspath(__file__))
_TOOLS = os.path.dirname(_HERE)
_ROOT = os.path.dirname(os.path.dirname(_TOOLS))  # simitone-fork/
sys.path.insert(0, _TOOLS)

import pef_load  # noqa: E402
import ppc_decode  # noqa: E402

BIN_PATH = os.path.join(_ROOT, "game-data", "The Sims", "The Sims Complete")
CODE_BASE = 0x8E90  # section 0 containerOffset
DATA_UNPACKED = 0x7BF80
DATA_TOTAL = 0x989A4
TOC_BASE = 0x8000  # data offset of r2

_PEFCACHE = {}


def pef():
    if "p" not in _PEFCACHE:
        _PEFCACHE["p"] = pef_load.load_pef(BIN_PATH, verbose=False)
    return _PEFCACHE["p"]


def code():
    """Raw binary bytes (code section = file offsets)."""
    return pef()["data"]


def image():
    """Expanded data-section image (unrelocated words; use loaded_word())."""
    return pef()["image"]


def relmap():
    """dataOffset -> (kind, fileWord, sectionValue/importIndex) from events."""
    if "rel" not in _PEFCACHE:
        m = {}
        for kind, doff, fileWord, val in pef()["events"]:
            m.setdefault(doff, []).append((kind, fileWord, val))
        _PEFCACHE["rel"] = m
    return _PEFCACHE["rel"]


def loaded_word(doff):
    """The as-loaded (post-relocation) word at data-section offset doff."""
    img = image()
    if doff < 0 or doff + 4 > len(img):
        return None
    w = struct.unpack_from(">I", img, doff)[0]
    evs = relmap().get(doff)
    if evs:
        kind, fw, val = evs[-1]
        if kind in ("C", "D", "bySection"):
            return (fw + val) & 0xFFFFFFFF
        return None  # import-based: unresolved statically
    return w


def toc_entry(toc_off):
    """Raw word at r2+toc_off (data offset 0x8000+toc_off), unrelocated."""
    doff = (TOC_BASE + toc_off) & 0xFFFFFFFF
    img = image()
    if doff + 4 > len(img):
        return None
    return struct.unpack_from(">I", img, doff)[0]


def toc_loaded(toc_off):
    """Post-relocation pointer stored in the TOC entry at r2+toc_off."""
    return loaded_word((TOC_BASE + toc_off) & 0xFFFFFFFF)


def classify(ptr):
    """Interpret a loaded pointer: ('code', fileOffset) | ('data', doff) |
    ('bss', doff) | ('zero', 0)."""
    if ptr is None:
        return ("unresolved", None)
    if ptr < DATA_UNPACKED:
        return ("data", ptr)
    if ptr < DATA_TOTAL:
        return ("bss", ptr)
    return ("code?", ptr)  # data section max < code length; ambiguous


def ptr_target(ptr):
    """Full classification incl. code resolution (code ptrs = file offsets
    are section-offset + CODE_BASE, so data-total <= ptr < code_len means
    code at file ptr + CODE_BASE... but data ptrs are section-relative, so
    any ptr < 0x7BF80 is data; 0x7BF80..0x989A4 is bss; values in
    [0x989A4, 0x5B9458) would be code section offsets)."""
    if ptr is None:
        return ("unresolved", None)
    if ptr < DATA_UNPACKED:
        return ("data", ptr)
    if ptr < DATA_TOTAL:
        return ("bss", ptr - DATA_UNPACKED)
    if ptr < pef()["codeLen"]:
        return ("code", ptr + CODE_BASE)
    return ("out-of-range", ptr)


def code_word(faddr):
    d = code()
    return struct.unpack_from(">I", d, faddr)[0]


def disasm(faddr, count):
    out = []
    for i in range(count):
        a = faddr + i * 4
        w = code_word(a)
        txt = bytes(code()[a:a + 4])
        asc = "".join(chr(c) if 32 <= c < 127 else "." for c in txt)
        d = ppc_decode.decode(w, a)
        out.append(f"{a:#08x}: {w:08x}  {d if d else '.long':42s} |{asc}|")
    return out


def disasm_str(faddr, count):
    return "\n".join(disasm(faddr, count))


def scan_imm(imm, lo=CODE_BASE, hi=None, ops=None):
    """Find D-form words with the given signed immediate (and optional op
    filter) across the code section; returns (fileOffset, word)."""
    d = code()
    hi = hi if hi is not None else len(d) - 3
    u = imm & 0xFFFF
    hits = []
    for a in range(lo, hi, 4):
        w = struct.unpack_from(">I", d, a)[0]
        if (w & 0xFFFF) == u:
            if ops is None or (w >> 26) in ops:
                hits.append((a, w))
    return hits
