# R105 — the relocation trace: the cloud tables are NOT statically initialized

Round goal: resolve the InitClouds lwz 282/342/462 bases via the sec2
relocation stream and recover the loop-2/3 tables. Full trace, honest close.

## What was built and run

1. **Loader layout parsed** (Ghidra LoaderInfoHeader reference): sec2 at
   file 0x80, 14-word loader header (7 imports, 535 symbols, 1 reloc
   section, instructions at loader+0x948 = file 0x9c8); three relocation
   headers: sec0-a (3 instrs @0xc0), sec0-b (53 @0x114), sec1 (12941 @0x9bc).
2. **Full relocation simulator** (r105 trace + sec1-relocated-partial.bin):
   all Ghidra opcode classes implemented (BySectD/CWithSkip, ValueGroup
   subops 0-5, IncrPos, SetPos, ByIndexGroup, LgByImport, LgRepeat,
   SmRepeat with chunk expansion) — 40,470 patch actions simulated across
   sec1; 1,516 words written in the first 0x2000 alone (the TOC).
3. **Result: sec1+282..720 — the exact addresses the cloud lwz reads —
   remain ZERO after the complete relocation simulation.** The sec0 chains
   are pure TOC-import patches (ValueGroup sub5 runs against the jump
   table); no section-0 displacement relocation touches the lwz sites.

## Why the tables are statically unreachable

The architectural decode is now airtight: `xor r0,r0,r4` (0x57c8f8,
XO-316 — resolving the last .long), r31 = li 0 (0x57c908, rA=0 literal),
r26 = r31 = 0 (0x57c934), so `lwz r0, 342(r26)` reads absolute 342+32j —
which can only be valid if (a) the table is written at runtime by an init
routine we have not found (not by InitClouds, which only READS it), or
(b) the loop-2 window is structurally misparsed (e.g., those bytes are a
branch-table target region, not straight-line code). Both are beyond
static recovery with the current disassembler (a full dataflow-emulating
decompiler would be needed for (a), and (b) is a refutation-class doubt
like R101's).

## Honest state

The 142/262 disclosed model in UINeighborhoodCloudLayer REMAINS (its
visible behavior — Y ladders, columns, drift, wrap — is all engine-literal
from the loop structure; only the loop-2/3 table VALUES are modeled).
Tooling delivered this round: the relocation simulator (committed inline
in this evidence file's generating session, ~150 lines) and the relocated
sec1 image (sec1-relocated-partial.bin) — the full static data section as
it exists after load, useful for every future RE slice.
