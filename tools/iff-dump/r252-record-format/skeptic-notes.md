# R252 — skeptic notes: how this format reading could be wrong

Adversarial self-review. For each way the NBRS/FAMI field reading could be
mistaken, I state the worry and how it was ruled out. Where it is *not* fully
ruled out, I say so explicitly (those items are also listed in decode.md §6).

---

## 1. Endianness (the big one)

The engine is PowerPC — **big-endian**. But the record data is little-endian.

Worry: did I read the record as little-endian when the engine actually writes
big-endian (so every field is byte-swapped)?

Ruled out:
- The chunk **header** (chunk size, chunk id) is big-endian (standard IFF); the
  chunk **data** (the NBRS/FAMI record) is little-endian. The port reads the
  chunk data with `IoBuffer.FromStream(stream, ByteOrder.LITTLE_ENDIAN)` in both
  `NBRS.Read` and `FAMI.Read`, and it parses this exact staged file in-game.
- Raw bytes: the NBRS chunk-data version is `3e 00 00 00`. As little-endian that
  is `0x3E` (matches R246's "0x3E"); as big-endian it is `0x3E000000`. The count
  is `31 00 00 00` → LE `49` (matches the port's known "49 entries"), BE would be
  `0x31000000`.
- The **record Version** is `04 00 00 00` → LE `4` (R246 "record Version 4");
  BE would be `0x04000000`.
- Person-data shorts: if I read them big-endian, `pd[60]` would be `03 00`→BE
  `0x0300=768` for the skin, which could never correlate to a `lgt/drk/med`
  bodystring token (skins are 1/2/3). Reading little-endian gives `3` and the
  correlation holds across all 20 files.
- Decisive: the person-data field indices (46..55, 56, 57, 58, 60, 61, 63, 65)
  only make sense as small integers in little-endian; the original binary's
  `GetPersistentDataFields` emits exactly those small field indices (see
  `getpersistentdatafields-disasm.txt`).

So: chunk header BE, record data LE. This is a Maxis quirk (the format is shared
with the Windows build). A reader that assumes the whole file is big-endian (an
easy mistake for a PPC target) would misread every record field.

## 2. short vs word vs long (PersonData entry size)

Worry: are the 80 "shorts" actually 16-bit, or 32-bit entries (0xa0/4 = 40
entries)?

Ruled out:
- The port reads `io.ReadInt16()` per entry and chooses `size = (Version==0x4) ?
  0xa0 : 0x200` **bytes**, then `for (i=0; i<size; i+=2)` — i.e. `0xa0` bytes =
  80 × `int16`.
- `0xa0` bytes / 2 = 80 shorts, matching R246's "80 shorts".
- If entries were 32-bit, `0xa0/4` = 40 entries and the field indices 56/58/60/65
  would still fit, but the `GetPersistentDataFields` field list only makes sense
  with 16-bit entries packed 2 bytes apart (the runtime `Neighbor` object zeroes
  `0x74..0xb2` in 2-byte `sth` strides; the person data lives at these offsets).
- The personality values (300..1000), skin (1/2/3), age (9/27) all fit in int16;
  if read as int32 the values would be corrupted.

## 3. "Chunk 0x3E/0x3F" — what does it mean exactly?

Worry: R246's "chunk 0x3E/0x3F" could be mis-read as the IFF **chunk ID**, or as
a record-level subtype.

Clarified/PROVEN:
- The NBRS IFF chunk **ID** is `1` (from the chunk header at 0x0b8: `00 01`), NOT
  0x3E.
- "0x3E/0x3F" is the **NBRS chunk-data Version field** (the first int32 of the
  chunk data, after the `pad`): `0x3E` in `UserData`, `0x3F` in `UserData2`–`8`.
- The record-level version is a **separate** field inside each record and is `4`,
  distinct from 0x3E/0x3F.

I verified this by reading the bytes directly and by checking the header `rsmp`
offset points at an actual `rsmp` chunk, confirming my chunk walk is aligned. The
"0x3E/0x3F" and the record "Version 4" are two different fields; conflating them
would be a real error, so it is called out explicitly in decode.md §2.1/§2.2.

## 4. Name representation and the pad byte

Worry: is the name a NUL-terminated string, a fixed-length field, or is my
"pad to even" rule wrong (which would shift every following field)?

Ruled out:
- Every name terminates in a NUL and is immediately followed, for even-length
  names, by one pad byte. The port does `Name = ReadNullTerminatedString(); if
  (Name.Length % 2 == 0) io.ReadByte();`.
- I tested this by parsing the whole 49-record stream **with** the pad rule: it
  consumes **exactly** 6734 bytes (== the NBRS chunk data size). Any off-by-one in
  the pad rule desyncs the parse and lands nowhere near 6734. That exact
  consumption is the strongest evidence the name/pad handling is right.
- Cross-check: the name string is the lowercase file stem; each of the 20
  `userNNNNN` names has a matching `Characters/UserNNNNN.iff`. The original file
  is capital-`U`, the NBRS name is lower-case `userNNNNN` (verified in the bytes:
  `75 73 65 72 30 30 30 32 33 00` = `user00023\0`).

Note (not a bug, but easy to trip on): the port's pad rule keys off the name
length *without* the NUL (`Name.Length % 2 == 0`). This makes the whole
name+NUL+pad field an even number of bytes.

## 5. PersonMode 5 vs 0 and the bodiless records

Worry: maybe every record has person data and PersonMode is not meaningfully 5/0.

Ruled out:
- Records named `bones` and `templateperson` have PersonMode 0 and the parse
  skips the 0xa0-byte person block. If they actually carried person data, the
  parse would desync (the next field would be read at the wrong offset). Since the
  total still lands on exactly 6734, PersonMode 0 genuinely means "no person
  data".
- All other records are PersonMode 5 and carry 0xa0 bytes.

## 6. Could the skin correlation be coincidence?

Worry: maybe pd[60] doesn't mean skin and the `lgt/drk/med` tokens are unrelated.

Ruled out:
- I correlated `pd[60]` to the bodystring skin token across **all 20** created
  character files. Every one matched: pd[60]=1→`lgt`, 2→`drk`, 3→`med` (0
  mismatches). A spurious correlation across 20 independent files is effectively
  impossible.
- The skin token also appears as a suffix on the BODY/HAND outfit names in the
  same table (`BODY=nffatlgt_01`, `HAND=huaodrk`), so it is definitely the applied
  skin, not a stray string.
- This therefore **confirms** (and is not a coincidence for) the non-obvious
  ordering light=1, dark=2, medium=3, matching R246's "lgt=1/med=3/drk=2".

## 7. Is the age field really 9/27 (child/adult)?

Worry: 9 and 27 are odd numbers; maybe they are encoded differently.

Ruled out:
- The same character-file bodystring table has an explicit age-category string
  (`child`/`adult`) and the age number as a string (`9`/`27`). pd[58]=9 always
  pairs with `child`/`9`; pd[58]=27 always pairs with `adult`/`27`; across all 20
  files. No mismatch.
- `10` appears on the `papercarrierf1` NPC (a teen), so the scale has at least
  child=9, teen=10, adult=27. The port's child/adult 9/27 are the two values that
  actually appear on the created family.

## 8. Chunk/data boundary and `chunkSize` semantics

Worry: maybe I mis-parsed the chunk header (size includes the 76-byte header) or
a pad byte exists between chunks.

Ruled out:
- I confirmed the header by reading the chunk `size` for `NGBH`=120 with data
  120-76=44 bytes, and that every following chunk lines up so the walk ends
  exactly at EOF (0x2d89).
- I confirmed the `rsmp` field in the 60-byte file header (`0x2296`) points at the
  actual `rsmp` chunk at offset 0x2296 — an independent cross-check that my chunk
  offsets and the header convention are correct.
- There is **no** even-align pad between odd-sized chunks (e.g. the `XXXX` id=3
  chunk is 91 bytes odd and the next `FAMI` begins immediately at `+91`). If a pad
  existed, the walk would misalign. So decode.md correctly states no pad.

## 9. Two save generations (0x3E vs 0x3F) — different record structure?

Worry: the 0x3F neighborhoods might use a different record format than 0x3E, so my
0x3E field map might not generalize.

Investigated:
- I parsed `UserData2` (0x3F). It uses the **same** record structure: Version 4,
  PersonMode 5 / 0, person data 0xa0 bytes, NUL+pad names, same 49-count header.
  Only the chunk-data version field differs (0x3F vs 0x3E).
- Caveat: I only spot-checked `UserData2` fully; `UserData3`–`8` are the same size
  (3483) as `UserData2` and are unmodified template neighborhoods, but I did not
  parse each. The version field being 0x3F in all of them is confirmed. This is a
  minor residual (marked INFERRED generalization), not a proven difference.

## 10. Trailing zeros / FAMI size

Worry: maybe the original FAMI *does* have trailing zeros and the port is right.

Ruled out:
- Each original FAMI data size exactly equals `40 + 4*guidcount` (no trailing
  bytes). E.g. id4 n=4 → 56 bytes, and 56 == 40+16. If there were 16 trailing
  zero bytes the size would be 72. All six FAMI chunks are byte-exact with zero
  trailing bytes.
- The port's `FAMI.Read` *tries* to read 4 trailing int32s inside a `try/catch`
  and tolerates their absence — i.e. the port author already knew a base-game
  FAMI has fewer trailing bytes ("FAMI Default only has 3 zeroes after it, but
  only if saved by base game"). My finding (0 trailing) goes further than that
  comment and is byte-verified.

## 11. Relationship-count encoding

Worry: the per-record relationship list might not be `{ keycount, key,
valuecount, int32[] }`, which would desync record parsing.

Ruled out:
- The parse consumes exactly 6734 bytes across 49 records. Any wrong count or
  wrong value-width in the relationship block would over/under-run.
- Records that do have relationships (e.g. `user00011`, `user00013`,
  `user00000`) parse cleanly; the others have 0 relationships.

## 12. Remaining genuine uncertainties (not fully pinned)

These are the honest residual items, also recorded in decode.md §6:

- The **personality trait-name → slot** mapping (pd[2]=Nice … pd[7]=Neat) is taken
  from the R246/port canon, not re-derived here. The **slot indices** 2..7 and the
  **always-zero pd[4]** are byte-proven; the trait *names* are canon.
- The exact **age scale** beyond child=9/teen=10/adult=27 (no elder encountered).
- The exact original **interest distribution** (creation-randomized; staged uses
  the raw 0..10 dialect, but the distribution is not pinned).
- The meaning of `pd[67]`/`pd[68]` (observed 5/1 on some records; port does not
  write them; the `VMPersonDataVariable` alias is a TSO-era name).
