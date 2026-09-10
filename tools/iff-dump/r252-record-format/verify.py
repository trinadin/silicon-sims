#!/usr/bin/env python3
"""R252: falsifiable re-pin of the ORIGINAL created-record save format.

Reads ONLY the staged (read-only) original game data under game-data/ and
asserts the field values that define the original NBRS/FAMI record format.
If any assertion fails the format claim is falsified.

Pass:
  * binary SHA256 that this whole round targets
  * NBRS chunk: data-version 0x3E, magic 'SRBN', 49 records
  * records: Unknown1==1 -> Version 4, PersonData 0xa0 bytes (80 shorts),
    PersonMode 5 (0 for bodiless NPCs), Name == character-file stem
  * person-data field map (skin/age/gender/family/job/variety offsets)
  * skin encoding via the character-file bodystring table (lgt/drk/med)
  * FAMI chunks: all Version 7, NO trailing-zero frame
  * FAMI chunk id == in-game family id == NBRS pd[61]

Usage: python3 verify.py
"""
import hashlib
import struct
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
GAME = os.path.join(ROOT, "game-data", "The Sims")
BIN = os.path.join(GAME, "The Sims Complete")
NBH = os.path.join(GAME, "UserData", "Neighborhood.iff")
CHARS = os.path.join(GAME, "UserData", "Characters")

BIN_SHA = "33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f"
IFF_HEADER = 0x40          # 60-byte IFF FILE 2.5 header + 4-byte rsmpOffset
CHUNK_HDR = 76             # type(4)+size(4)+id(2)+flags(2)+label(64)

# --- pin the binary SHA256 ------------------------------------------------
def pin_binary():
    h = hashlib.sha256(open(BIN, "rb").read()).hexdigest()
    assert h == BIN_SHA, f"binary SHA256 mismatch: {h}"
    print(f"[ok] binary SHA256 pinned  {h}")

# --- Maxis IFF chunk walker -------------------------------------------------
def walk_chunks(data, start=IFF_HEADER):
    pos = start
    while pos + 8 <= len(data):
        ctype = data[pos:pos+4]
        csize = struct.unpack(">I", data[pos+4:pos+8])[0]
        if csize < CHUNK_HDR or pos + csize > len(data):
            break
        yield pos, ctype, csize
        pos += csize

def find_chunks(data, want):
    return [(p, t, c) for p, t, c in walk_chunks(data) if t in want]

# --- NBRS record parser (mirrors the original layout) ----------------------
def parse_nbrs(data):
    d = data
    pad = struct.unpack_from("<I", d, 0)[0]
    ver = struct.unpack_from("<I", d, 4)[0]
    magic = d[8:12]
    count = struct.unpack_from("<I", d, 12)[0]
    pos = 16
    recs = []
    for _ in range(count):
        if pos + 4 > len(d):
            break
        unk1 = struct.unpack_from("<i", d, pos)[0]
        if unk1 != 1:
            pos += 4
            recs.append(None)
            continue
        rec_start = pos
        pos += 4
        rver = struct.unpack_from("<i", d, pos)[0]; pos += 4
        unknown3 = None
        if rver == 0xA:
            unknown3 = struct.unpack_from("<i", d, pos)[0]; pos += 4
        end = d.index(b"\0", pos)
        name = d[pos:end].decode("latin1"); pos = end + 1
        if len(name) % 2 == 0:
            pos += 1
        mystery = struct.unpack_from("<i", d, pos)[0]; pos += 4
        pm = struct.unpack_from("<i", d, pos)[0]; pos += 4
        pd = None
        if pm > 0:
            size = 0xa0 if rver == 0x4 else 0x200
            pd = struct.unpack_from(f"<{size//2}h", d, pos); pos += size
        nid = struct.unpack_from("<h", d, pos)[0]; pos += 2
        guid = struct.unpack_from("<I", d, pos)[0]; pos += 4
        negone = struct.unpack_from("<i", d, pos)[0]; pos += 4
        entries = struct.unpack_from("<i", d, pos)[0]; pos += 4
        rels = []
        for _ in range(entries):
            keycount = struct.unpack_from("<i", d, pos)[0]
            key = struct.unpack_from("<i", d, pos+4)[0]
            vc = struct.unpack_from("<i", d, pos+8)[0]
            vals = struct.unpack_from(f"<{vc}i", d, pos+12)
            pos += 12 + 4*vc
            rels.append((keycount, key, vals))
        recs.append(dict(start=rec_start, name=name, version=rver, unknown3=unknown3,
                         mystery=mystery, personmode=pm, pd=pd, nid=nid, guid=guid,
                         negone=negone, entries=entries, rels=rels))
    return ver, magic, count, recs, pos

# --- character-file bodystring skin token ----------------------------------
def _walk(data, start=IFF_HEADER):
    pos = start
    while pos + 8 <= len(data):
        ct = data[pos:pos+4]; cs = struct.unpack(">I", data[pos+4:pos+8])[0]
        if cs < CHUNK_HDR or pos + cs > len(data):
            break
        yield pos, ct, cs
        pos += cs

def char_skin_and_age(fname):
    """Return (skin_token, age_category, age_num, gender) from the STR# 200
    bodystring table, or (None,...) if the file has no bodystring table."""
    data = open(fname, "rb").read()
    for p, ct, cs in _walk(data):
        if ct != b"STR#":
            continue
        d = data[p+CHUNK_HDR:p+cs]
        joined = "".join(chr(x) if 32 <= x < 127 else "\n" for x in d)
        lines = [l.strip() for l in joined.split("\n") if l.strip()]
        if any(x in lines for x in ("lgt", "drk", "med")):
            skin = next((x for x in ("lgt", "drk", "med") if x in lines), None)
            agecat = next((x for x in ("child", "teen", "adult", "elder") if x in lines), None)
            age = next((x for x in lines if x.isdigit()), None)
            gender = next((x for x in ("male", "female") if x in lines), None)
            return skin, agecat, age, gender
    return None, None, None, None

def main():
    pin_binary()
    nbh = open(NBH, "rb").read()

    # --- NBRS chunk ---------------------------------------------------------
    nbrs = find_chunks(nbh, {b"NBRS"})
    assert len(nbrs) == 1, f"expected 1 NBRS chunk, got {len(nbrs)}"
    p, ct, cs = nbrs[0]
    nd = nbh[p+CHUNK_HDR:p+cs]
    ver, magic, count, recs, consumed = parse_nbrs(nd)
    print(f"[ok] NBRS chunk @0x{p:x} data={len(nd)} bytes")
    assert magic == b"SRBN", f"NBRS magic {magic!r} != 'SRBN'"
    assert ver == 0x3E, f"NBRS chunk data version {ver:#x} != 0x3E"
    assert count == 49, f"NBRS count {count} != 49"
    assert consumed == len(nd), f"NBRS parse consumed {consumed} != data {len(nd)}"
    print(f"[ok] NBRS chunk-data version 0x{ver:x}, magic {magic!r}, count={count}, "
          f"parse consumed exactly {consumed}/{len(nd)} bytes")

    valid = [r for r in recs if r]
    assert all(r["version"] == 0x4 for r in valid), "not all records are Version 4"
    assert all(r["mystery"] == 0 for r in valid), "MysteryZero should be 0"
    print(f"[ok] {len(valid)} of {count} records are Unknown1==1, ALL record Version == 0x4")

    withpd = [r for r in valid if r["pd"] is not None]
    nopd = [r for r in valid if r["pd"] is None]
    assert all(r["personmode"] == 5 for r in withpd), "PersonMode != 5 on a person-data record"
    assert all(r["personmode"] == 0 for r in nopd), "PersonMode != 0 on a bodiless record"
    assert all(len(r["pd"]) == 80 for r in withpd), "person data is not 80 shorts"
    print(f"[ok] PersonMode == 5 on {len(withpd)} records, 0 on {len(nopd)}; "
          f"person data is 80 shorts (0xa0 bytes) on all")

    # Name == character-file stem (lowercase userNNNNN / known NPC names)
    created = [r for r in withpd if r["name"].startswith("user")]
    assert created, "no created user records found"
    for r in created:
        stem = r["name"]
        cf = os.path.join(CHARS, stem.capitalize() + ".iff")
        assert os.path.exists(cf), f"NBRS name {stem!r} has no matching character file {cf}"
    print(f"[ok] {len(created)} created records: NBRS Name == character-file stem "
          f"(e.g. {created[0]['name']!r} -> {created[0]['name'].capitalize()}.iff); "
          f"NOT the literal 'iffname'")

    # --- FAMI chunks --------------------------------------------------------
    famis = find_chunks(nbh, {b"FAMI"})
    assert len(famis) == 6, f"expected 6 FAMI chunks, got {len(famis)}"
    fami_ids = []
    for p, ct, cs in famis:
        d = nbh[p+CHUNK_HDR:p+cs]
        v = struct.unpack_from("<I", d, 4)[0]
        assert v == 7, f"FAMI chunk @0x{p:x} version {v} != 7"
        gcount = struct.unpack_from("<i", d, 36)[0]
        expected_len = 4 + 4 + 4 + 6*4 + 4 + 4*gcount   # pad+ver+magic+6 ints+guidcount+guids
        assert len(d) == expected_len, (
            f"FAMI @0x{p:x} data len {len(d)} != expected {expected_len}: "
            f"*** has {len(d)-expected_len} trailing bytes — the port appends 16 trailing zeros ***")
        guidstart = 40
        guids = struct.unpack_from(f"<{gcount}I", d, guidstart) if gcount else ()
        fami_ids.append((struct.unpack_from(">H", nbh, p+8)[0], guids))
        print(f"[ok] FAMI @0x{p:x} version 7, guidcount={gcount}, guids={[hex(g) for g in guids]}, "
              f"data len {len(d)} == {expected_len} (no trailing frame)")
    print(f"[ok] all FAMI chunks Version 7 and byte-exact (no 16 trailing zeros)")

    # --- family id cross-check (FAMI chunk id == pd[61]) --------------------
    fami_by_id = {cid: set(guids) for cid, guids in fami_ids}
    for r in created:
        fid = r["pd"][61]
        if fid == 0:
            continue
        assert fid in fami_by_id, f"{r['name']}: pd[61]={fid} has no FAMI chunk"
        assert r["guid"] in fami_by_id[fid], (
            f"{r['name']}: guid {r['guid']:#x} not in FAMI chunk {fid}")
    print(f"[ok] FAMI chunk id == in-game family id == NBRS pd[61] for all moved-in created sims")

    # --- person data field map ----------------------------------------------
    from collections import Counter
    def report(offset, label):
        vals = Counter(r["pd"][offset] for r in created)
        return f"pd[{offset:2d}] {label:24s} {dict(sorted(vals.items()))}"
    print("\n-- original person-data field map (created records) --")
    for off, lab in [(2,"NicePersonality"),(3,"ActivePersonality"),(4,"GenerousPersonality"),
                     (5,"PlayfulPersonality"),(6,"OutgoingPersonality"),(7,"NeatPersonality"),
                     (46,"Interest base [46..55]"),(56,"JobType"),(57,"JobPromotionLevel"),
                     (58,"PersonsAge"),(60,"SkinColor"),(61,"TS1FamilyNumber"),
                     (63,"JobPerformance"),(65,"Gender"),(70,"TS1Zodiac")]:
        print("  " + report(off, lab))
    assert all(r["pd"][4] == 0 for r in created), "Generous (pd[4]) is not always 0"
    assert all(r["pd"][70] == 0 for r in created), "Zodiac (pd[70]) is not always 0"
    assert set(r["pd"][60] for r in created) <= {1,2,3}, "skin not in {1,2,3}"
    assert set(r["pd"][58] for r in created) <= {9,27}, "age not in {9,27}"
    assert set(r["pd"][65] for r in created) <= {0,1}, "gender not in {0,1}"
    print("[ok] pd[4]=0 (Generous never persisted), pd[70]=0 (zodiac never persisted); "
          "skin={1,2,3}, age={9,27}, gender={0,1}")

    # --- skin encoding via bodystrings --------------------------------------
    skin_map = {1: "lgt", 2: "drk", 3: "med"}
    checked = 0
    for r in created:
        cf = os.path.join(CHARS, r["name"].capitalize() + ".iff")
        skin, agecat, age, gender = char_skin_and_age(cf)
        if skin is None:
            continue
        assert skin == skin_map[r["pd"][60]], (
            f"{r['name']}: pd[60]={r['pd'][60]} but bodystring skin='{skin}' "
            f"(expected {skin_map[r['pd'][60]]})")
        exp_agecat = "child" if r["pd"][58] == 9 else "adult"
        assert agecat == exp_agecat, (
            f"{r['name']}: pd[58]={r['pd'][58]} but bodystring agecat='{agecat}'")
        exp_gender = "female" if r["pd"][65] == 1 else "male"
        assert gender == exp_gender, (
            f"{r['name']}: pd[65]={r['pd'][65]} but bodystring gender='{gender}'")
        checked += 1
    print(f"[ok] skin encoding PROVEN: pd[60] 1->lgt(light) 2->drk(dark) 3->med(medium), "
          f"age 9->child 27->adult, gender 1->female 0->male (checked {checked} bodystring tables)")

    # --- port divergence summary --------------------------------------------
    print("\n-- port divergence summary (from the port source, not the original) --")
    print("  port NBRS.Write: chunk-data version 0x49; record Version 0xA + Unknown3=9;")
    print("  Neighbour.Save: PersonMode default, Name='iffname', skin 0/1/2 (AppearanceType 0/1/2).")
    print("  port FAMI.Write: version 9 + 4 trailing int32 (16 zero bytes).")
    print("\nALL ASSERTIONS PASSED.")

if __name__ == "__main__":
    main()
