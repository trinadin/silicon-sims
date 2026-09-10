# R177 — ObjectDialog icon-selector law

## Evidence boundary

This law is recovered from the owner's original PowerPC executable and shipped
IFF/FAR corpus. The executable is `game-data/The Sims/The Sims Complete`,
SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`.
Addresses below are executable file offsets. The R177 verifier emits only
hashes, counters, addresses, and resource metadata; it does not emit original
code, strings, images, or IFF payloads.

## Selector dispatch

`ObjectDialog::SetupDialog` extracts `(operand[7] >> 1) & 7` at
`0x0cd0f4..0x0cd120`. Its exact branches are:

| mode | branch | native result |
|---:|---:|---|
| 0 | `0x0cd124` | automatic |
| 1 | `0x0cd110 -> 0x0cd1fc` | no image |
| 2 | `0x0cd18c` | neighbor selector |
| 3 | `0x0cd1a8` | indexed private bitmap |
| 4 | `0x0cd1cc` | named selector |
| 5–7 | `0x0cd11c -> 0x0cd1fc` | no image |

Failure is deliberately blank. The common null-selector test at
`0x0cd200..0x0cd214` branches past image creation; no explicit mode falls back
to automatic selection.

### Mode 0 — automatic

The original expands the five dialog strings while passing the same selector
output through every call to `ParseUIString`:

1. Yes at `0x0cd068..0x0cd080`;
2. No at `0x0cd08c..0x0cd098`;
3. Cancel at `0x0cd0a4..0x0cd0b0`;
4. Title at `0x0cd0bc..0x0cd0c8`; and
5. Message at `0x0cd0d4..0x0cd0e0`.

Only `$Object` and `$Neighbor` update that selector. The last successful token
in the above parse order wins. `$Me` expands text but does not select an icon.
If no token selected anything, `0x0cd134..0x0cd184` resolves the signed Stack
Object ID stored at `ObjectDialog+0x3a`; lookup or master-selector failure leaves
the dialog image blank.

### Mode 1 — none

Mode 1 unconditionally joins the blank common path. It does not inspect the
stack entity.

### Mode 2 — neighbor

`0x0cd18c..0x0cd1a4` passes the signed Stack Object ID to the neighborhood's
neighbor-selector lookup. A missing neighbor selector produces no image and no
fallback.

### Mode 3 — indexed

`0x0cd1a8..0x0cd1c8` resolves the resource selector owned by the currently
executing behavior, then uses resource ID `5000 + operand[1]`. The common
resource lookup at `0x0cd218..0x0cd24c` requests original bitmap type 4. This is
a raw private-IFF `BMP_` lookup, not a UI-resource alias and not an object GUID.
Missing resources remain blank.

The shipped fixture `ExpansionShared/ExpansionShared.far!HDHelpSystem.iff`
contains the complete big-endian `BMP_` ID range 5000–5021. The correct IFF
chunk-header ID decode is big-endian; the older little-endian diagnostic walker
would invent unrelated IDs.

### Mode 4 — named

`0x0cd1cc..0x0cd1f8` expands the 1-based STR# 301 entry selected by
`operand[1]`. `0x0cd47c..0x0cd500` splits at the first ASCII space. Matching is
case-insensitive against the six-token table recovered at unpacked PEF data
`+0x4634d`:

| token | native action |
|---|---|
| `gz` | global UI resource, first horizontal quarter |
| `gzi` | global UI resource, full image |
| `my` | current private resource, full image |
| `guid` | signed object GUID, composed 45x45 product image |
| `rel` | selected relation picture, center frame of a five-frame strip |
| `job` | `cWinSubpanelJob` job-popup composite |

The `gz`/`gzi` direction is binary-proved, not inferred from their names. Both
tokens converge at `0x0cd57c`; `0x0cd5e8` tests whether the token length is two,
and only that branch divides the source width by four at
`0x0cd5f0..0x0cd5fc`. Therefore **`gz` is quarter-width and `gzi` is full**.
The `rel` branch computes one fifth at `0x0cd808..0x0cd828` and advances by two
frames, selecting the center frame. Unknown, malformed, or missing names leave
the dialog blank.

Two shipped resource fixtures pin the named lookup domains:

- `Tutorial.iff` owns private `BMP_` 300 used by all three `my 300` sites.
- global resource 9005 maps to the 133x103 `Other/TragedyMask.BMP`; all 40
  shipped `gzi 9005` sites therefore request its complete image.

## Common image composition

A person selector uses the original PersonFinder portrait at 45x45. A
non-person selector uses `Product::DrawIcon` in a 120x120 buffer
(`0x0cd354..0x0cd468`). The 45x45 picture-dialog path is subsequently centered
inside the native 133x103 SimStub; neither selector class authorizes scaling the
source itself.

## Shipped corpus census

The strict scanner walks loose IFF/FAM files, FAR members, and one nested FAR
level. It finds 2,857 raw private Dialog (opcode 36) instructions. Thirteen are
type-6 clothing selectors, an adjacent custom selector path whose encoded icon
mode is always zero. The native ObjectDialog icon-selector census is therefore
2,844:

| mode | sites |
|---|---:|
| automatic | 1,937 |
| none | 150 |
| neighbor | 157 |
| indexed | 463 |
| named | 137 |
| reserved 5–7 | 0 |

Of the 137 named sites, 131 resolve an English STR# 301 entry. There are 129
recognized commands (`job` 81, `gzi` 40, `my` 3, `gz` 3, `guid` 1, `rel` 1),
two authored non-command strings, and six missing entries. The job sites divide
into 65 `$Local:9` references and 16 references to other local indices. The
malformed strings are fingerprinted by hash only in the gate so their original
text is not reproduced.

Run the metadata-only gate with:

```sh
python3 tools/iff-dump/r177/r177-object-dialog-icon-canon.py
```
