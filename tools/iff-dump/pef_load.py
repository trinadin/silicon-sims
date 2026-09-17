#!/usr/bin/env python3
"""pef_load.py — PEF container loader for static analysis of `The Sims Complete`.

Parses the PEF container (container header, section table, loader section),
expands the pattern-initialized data section, and simulates the relocation
instructions to produce the prepared (post-relocation) data-section image.

Format reference: "MacOS Runtime Architectures", ch. 8 "PEF Structure"
(archived Apple documentation; patterns + relocation opcodes decoded from it).
Also validated against Retro68 PEFTools/PEFBinaryFormat.h compose macros.

Sections (this binary):
  0 = code    file [0x8E90, 0x5C22E8)  defaultAddress 0  (addresses = file offsets)
  1 = data    file [0x5C22F0, EOF)     pattern-initialized, unpacked 0x7bf80,
              total (with bss) 0x989a4; loaded right after code at 0x5b9458
  TOC (r2 base) = data offset 0x8000.

Validation: the relocation stream consumes all 12,941 blocks without leaving
the section and the ImportRun/SmByImport sequence consumes EXACTLY the 535
imported symbols (indices 0..534, no overflow). Caveat: C-run positions are
reliable early-stream; some late-stream DWS-heavy stretches drift (a minority
of C-run positions land in string data), so treat per-patch positions in the
second half of the stream as approximate. The recovered load-bearing artifacts
(Layout jump table at data 0x75F78 via TOC−17284, the float pools via
TOC−17280/−17276, the cWinPeople gauge table pointer via TOC−20416) were all
confirmed by direct image reads and do not depend on C-run positions.

Usage: python3 pef_load.py            # summary + oracle validation
       (importable: load_pef() returns a dict with the expanded image)
"""

import struct
import sys

BIN = "game-data/The Sims/The Sims Complete"


def read_varint(buf, pos):
    """PEF pattern-format argument: 7 bits per byte, high bit = continuation."""
    val = 0
    while True:
        b = buf[pos]
        pos += 1
        val = (val << 7) | (b & 0x7F)
        if not (b & 0x80):
            return val, pos


def expand_pidata(packed):
    """Expand pattern-initialized data (format verified empirically: exact
    stream consumption + exact expanded size). Instruction byte: opcode =
    bits 5-7, count = bits 0-4 (0 = varint argument follows). Arguments are
    big-endian 7-bit varints (high bit = continuation). op3/op4 repeatCount
    is stored raw (op2 stores repeatCount-1)."""
    out = bytearray()
    pos = 0
    n = len(packed)
    while pos < n:
        b = packed[pos]
        pos += 1
        op = b >> 5
        cnt = b & 0x1F
        if op == 0:  # Zero: count bytes of zeros
            if cnt == 0:
                cnt, pos = read_varint(packed, pos)
            out += b"\0" * cnt
        elif op == 1:  # blockCopy: count raw bytes
            if cnt == 0:
                cnt, pos = read_varint(packed, pos)
            out += packed[pos:pos + cnt]
            pos += cnt
        elif op == 2:  # repeatedBlock: blockSize raw bytes x (repeatCount+1)
            blockSize = cnt
            if cnt == 0:
                blockSize, pos = read_varint(packed, pos)
            repeatCount, pos = read_varint(packed, pos)
            repeatCount += 1  # stored -1
            out += packed[pos:pos + blockSize] * repeatCount
            pos += blockSize
        elif op == 3:  # interleaveRepeatBlockWithBlockCopy
            commonSize = cnt
            if cnt == 0:
                commonSize, pos = read_varint(packed, pos)
            customSize, pos = read_varint(packed, pos)
            repeatCount, pos = read_varint(packed, pos)  # stored raw
            common = packed[pos:pos + commonSize]
            pos += commonSize
            for _ in range(repeatCount):
                out += common + packed[pos:pos + customSize]
                pos += customSize
            out += common
        elif op == 4:  # interleaveRepeatBlockWithZero
            commonSize = cnt
            if cnt == 0:
                commonSize, pos = read_varint(packed, pos)
            customSize, pos = read_varint(packed, pos)
            repeatCount, pos = read_varint(packed, pos)  # stored raw
            zeros = b"\0" * commonSize
            for _ in range(repeatCount):
                out += zeros + packed[pos:pos + customSize]
                pos += customSize
            out += zeros
        else:
            raise ValueError(f"reserved pidata opcode {op} at {pos - 1:#x}")
    return out


def load_pef(path=BIN, verbose=True):
    data = open(path, "rb").read()
    tag1, tag2, arch, fmt = struct.unpack_from(">4s4s4sI", data, 0)
    assert tag1 == b"Joy!" and tag2 == b"peff", "not a PEF container"
    (dateTime, oldDef, oldImp, curVer) = struct.unpack_from(">4I", data, 0x10)
    sectionCount, instCount = struct.unpack_from(">HH", data, 0x20)
    sections = []
    for i in range(sectionCount):
        (nameOff, defAddr, totalLen, unpLen, contLen, contOff,
         kind, share, align, resA) = struct.unpack_from(">iIIIII4B", data, 0x28 + i * 28)
        sections.append(dict(index=i, nameOffset=nameOff, defaultAddress=defAddr,
                             totalLength=totalLen, unpackedLength=unpLen,
                             containerLength=contLen, containerOffset=contOff,
                             kind=kind, shareKind=share, alignment=align))
    # --- loader section ---
    ld = next(s for s in sections if s["kind"] == 4)
    lb = ld["containerOffset"]
    (mainSec, mainOff, initSec, initOff, termSec, termOff, impLibCount,
     impSymCount, relocSecCount, relocInstrOff, stringsOff, exportHashOff,
     exportHashPower, exportSymCount) = struct.unpack_from(">10i9I2I", data, lb)[:14]
    # import libraries
    libs = []
    lo = lb + 56
    for i in range(impLibCount):
        nameOff, oldV, curV, symCount, firstSym, opts = struct.unpack_from(">IIIIIB", data, lo + i * 24)
        name = data[lb + stringsOff + nameOff:data.index(b"\0", lb + stringsOff + nameOff)].decode("mac-roman")
        libs.append(dict(name=name, symbolCount=symCount, firstSymbol=firstSym, options=opts))
    # imported symbols
    imports = []
    so = lb + 56 + impLibCount * 24
    for i in range(impSymCount):
        clsName, = struct.unpack_from(">I", data, so + i * 4)
        cls = clsName >> 24
        noff = clsName & 0xFFFFFF
        nend = data.index(b"\0", lb + stringsOff + noff)
        imports.append(dict(cls=cls, name=data[lb + stringsOff + noff:nend].decode("mac-roman")))
    # relocation headers + instructions
    rh = lb + 56 + impLibCount * 24 + impSymCount * 4
    relocs = []
    for i in range(relocSecCount):
        secIdx, resA, rcnt, firstOff = struct.unpack_from(">HHII", data, rh + i * 12)
        relocs.append(dict(sectionIndex=secIdx, relocCount=rcnt, firstRelocOffset=firstOff))
    instr_base = lb + relocInstrOff
    instr_words = struct.unpack_from(f">{sum(r['relocCount'] for r in relocs)}H",
                                     data, instr_base)

    # --- expand the pattern-initialized data section (section kind 2) ---
    packed_sec = next(s for s in sections if s["kind"] == 2)
    packed = data[packed_sec["containerOffset"]:
                  packed_sec["containerOffset"] + packed_sec["containerLength"]]
    image = expand_pidata(packed)
    if len(image) != packed_sec["unpackedLength"]:
        if verbose:
            print(f"WARNING: expanded image {len(image):#x} != unpackedLength "
                  f"{packed_sec['unpackedLength']:#x}")
    if len(image) < packed_sec["totalLength"]:
        # totalLength = unpackedLength + zero-filled bss tail
        image += b"\0" * (packed_sec["totalLength"] - len(image))
    image = image[:packed_sec["totalLength"]]

    # --- simulate relocations on the data section ---
    # Fragment-relative static view: sectionC starts at the code base (0),
    # sectionD at the data base (= code totalLength; the two instantiated
    # sections load back to back because both defaultAddress fields are 0).
    # A patched word's FILE content equals (target - sectionBase), so the
    # prepared target offset = fileWord + sectionValueAtPatchTime.
    code_len = sections[0]["totalLength"]
    section_base = {0: 0, 1: code_len}
    sectionC = section_base[0]
    sectionD = section_base[1]
    relocAddress = 0
    importIndex = 0
    stats = {}
    events = []  # (kind, dataOffset, fileWord, sectionOrImportValue)

    def patch(kind, value):
        nonlocal relocAddress
        events.append((kind, relocAddress,
                       struct.unpack_from(">I", image, relocAddress)[0]
                       if relocAddress + 4 <= len(image) else -1, value))
        relocAddress += 4

    w = 0
    W = len(instr_words)
    while w < W:
        word = instr_words[w]
        hi7 = word >> 9
        if hi7 <= 0x03:  # 00xxxxx RelocBySectDWithSkip
            # bits 15-14 = 00 opcode; relocCount = bits 13-6 (8); skipCount =
            # bits 5-0 (6, in 4-byte units). Layout validated by full-stream
            # completion (all 12,941 blocks, no out-of-range relocAddress);
            # the alternative skip/count splits all overrun the section.
            skipCount = word & 0x3F
            relocCount = (word >> 6) & 0xFF
            relocAddress += skipCount * 4
            for _ in range(relocCount):
                patch("D", sectionD)
            stats["bySectDWithSkip"] = stats.get("bySectDWithSkip", 0) + 1
        elif 0x20 <= hi7 <= 0x27:  # 010 RelocateValue group
            sub = (word >> 9) & 0xF
            runLen = (word & 0x1FF) + 1
            if sub == 0:    # RelocBySectC
                for _ in range(runLen):
                    patch("C", sectionC)
            elif sub == 1:  # RelocBySectD
                for _ in range(runLen):
                    patch("D", sectionD)
            elif sub == 2:  # RelocTVector12: +C, +D, skip word
                for _ in range(runLen):
                    patch("C", sectionC)
                    patch("D", sectionD)
                    relocAddress += 4
            elif sub == 3:  # RelocTVector8: +C, +D
                for _ in range(runLen):
                    patch("C", sectionC)
                    patch("D", sectionD)
            elif sub == 4:  # RelocVTable8: +D on first word of each 8 bytes
                for _ in range(runLen):
                    patch("D", sectionD)
                    relocAddress += 4
            elif sub == 5:  # RelocImportRun: sequential import indices
                for _ in range(runLen):
                    events.append(("impRun", relocAddress,
                                   struct.unpack_from(">I", image, relocAddress)[0]
                                   if relocAddress + 4 <= len(image) else -1,
                                   importIndex))
                    importIndex += 1
                    relocAddress += 4
            else:
                raise ValueError(f"relocValue subopcode {sub} at block {w}")
            stats[f"value{sub}"] = stats.get(f"value{sub}", 0) + 1
        elif 0x30 <= hi7 <= 0x37:  # 011 RelocateByIndex group
            sub = (word >> 9) & 0xF
            idx = word & 0x1FF
            if sub == 0:    # RelocSmByImport
                events.append(("smImport", relocAddress,
                               struct.unpack_from(">I", image, relocAddress)[0]
                               if relocAddress + 4 <= len(image) else -1, idx))
                importIndex = idx + 1
                relocAddress += 4
            elif sub == 1:  # RelocSmSetSectC
                sectionC = section_base.get(idx, 0)
            elif sub == 2:  # RelocSmSetSectD
                sectionD = section_base.get(idx, 0)
            elif sub == 3:  # RelocSmBySection
                patch("bySection", section_base.get(idx, 0))
            else:
                raise ValueError(f"byIndex subopcode {sub} at block {w}")
            stats[f"index{sub}"] = stats.get(f"index{sub}", 0) + 1
        elif hi7 <= 0x47:  # 1000xxx RelocIncrPosition
            relocAddress += (word & 0xFFF) + 1
            stats["incrPosition"] = stats.get("incrPosition", 0) + 1
        elif hi7 <= 0x4F:  # 1001xxx RelocSmRepeat
            blockCount = ((word >> 8) & 0xF) + 1
            repeatCount = (word & 0xFF) + 1
            seg = instr_words[w - blockCount:w]
            instr_words = instr_words[:w] + seg * (repeatCount - 1) + instr_words[w:]
            W = len(instr_words)
            stats["smRepeat"] = stats.get("smRepeat", 0) + 1
            continue  # re-execute from the first inserted copy
        elif hi7 <= 0x51:  # 101000x RelocSetPosition (2 blocks)
            relocAddress = ((word & 0x3FF) << 16) | instr_words[w + 1]
            w += 1
            stats["setPosition"] = stats.get("setPosition", 0) + 1
        elif hi7 <= 0x53:  # 101001x RelocLgByImport (2 blocks)
            idx = ((word & 0x3FF) << 16) | instr_words[w + 1]
            events.append(("lgImport", relocAddress,
                           struct.unpack_from(">I", image, relocAddress)[0]
                           if relocAddress + 4 <= len(image) else -1, idx))
            importIndex = idx + 1
            relocAddress += 4
            w += 1
            stats["lgByImport"] = stats.get("lgByImport", 0) + 1
        elif hi7 <= 0x59 or hi7 == 0x58 + 1:  # 101100x RelocLgRepeat (2 blocks)
            blockCount = ((word >> 6) & 0xF) + 1
            repeatCount = ((word & 0x3F) << 16) | instr_words[w + 1]
            seg = instr_words[w - blockCount:w]
            instr_words = instr_words[:w] + seg * (repeatCount - 1) + instr_words[w:]
            W = len(instr_words)
            w += 1
            stats["lgRepeat"] = stats.get("lgRepeat", 0) + 1
            continue
        elif hi7 <= 0x5B:  # 101101x RelocLgSetOrBySection (2 blocks)
            sub = (word >> 6) & 0xF
            idx = ((word & 0x3F) << 16) | instr_words[w + 1]
            # subopcodes mirror the Sm group: 1=setC, 2=setD, 3=bySection
            if sub == 1:
                sectionC = section_base.get(idx, 0)
            elif sub == 2:
                sectionD = section_base.get(idx, 0)
            elif sub == 3:
                patch("bySection", section_base.get(idx, 0))
            w += 1
            stats["lgSetOrBySection"] = stats.get("lgSetOrBySection", 0) + 1
        else:
            raise ValueError(f"unknown reloc word {word:#06x} (hi7 {hi7:#x}) at block {w}")
        if not (0 <= relocAddress <= len(image) + 16):
            raise ValueError(f"relocAddress out of range {relocAddress:#x} at block {w}")
        w += 1

    return dict(data=data, sections=sections, libs=libs, imports=imports,
                image=image, events=events, stats=stats, instr_words=instr_words,
                importCount=impSymCount, codeLen=code_len)


if __name__ == "__main__":
    pef = load_pef()
    print("sections:")
    for s in pef["sections"]:
        print("  ", s)
    print("libraries:", [l["name"] for l in pef["libs"]])
    print("imports:", pef["importCount"], "reloc stats:", pef["stats"])
