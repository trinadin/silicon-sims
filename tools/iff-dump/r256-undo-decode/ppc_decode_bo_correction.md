# ppc_decode.py BO-12/BO-4 polarity correction (R256)

`tools/iff-dump/ppc_decode.py` (line 140) maps conditional-branch BO fields as
`{0: lt, 8: gt, 12: ne, 4: eq}`. Per the PowerPC spec (and radare2 ground truth:
`rasm2 -a ppc -b 32 -e -d 41820014` → `beq 0x14`), **BO=12 branches when the CR bit
is SET (beq for BI=0) and BO=4 when CLEAR (bne)** — the decoder's names for 12/4
are swapped. Every `beq`/`bne` in any round's ppc_decode output since R91 is
polarity-flipped. Branch TARGETS are unaffected (R129's fix stands).

Validation in this round:
- r2 ground truth on raw words 0x41820014 (`beq`) and 0x40820010 (`bne`).
- Behavioral coherence with corrected polarity, each of which is nonsense with the
  printed mnemonics: `CPState::SetMode` flush guards (0x21087c-0x210890: null
  checks then FlushCommandQueues), `SubmitUndoable` commit path (commit success →
  push; refuse → flush @0x1946c0), `UndoLastCommand`/`RedoLastCommand` success
  paths (Undoable methods return 0 on success — floors/roof return literal 0),
  `cMoveTool::Cancel` flag guard, `HouseViewer::DoCommand` stack-size guards.
- `0x7c0300d0` = `neg r0, r3` (r2), confirming the ArchCanUndo/Redo size!=0 idiom
  (printed `.long` by the decoder).

Impact outside this round: any prior law that quotes a `beq`/`bne` from
ppc_decode output should be re-checked against r2 or the corrected table before
being load-bearing. (File left untouched — tools are read-only for UI-22.)
