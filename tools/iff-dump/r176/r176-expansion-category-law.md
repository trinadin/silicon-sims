# R176 — Studio/Magic buy-category correction

## Scope and correction

The reported Food / Shops / Studio / Spa crop is the Studio Town BUY main row. The previous R146 decode
correctly recovered the slot masks and most of `LoadBooks`, but incorrectly
assumed that the Studio and Magic resource-ID arrays were sequential. They are
not. The owner's original `The Sims Complete` PowerPC executable, SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`, stores:

```text
TOC[-0x6ac8] -> data+0x55d88: 273,274,276,275,0,0,0,277
TOC[-0x6acc] -> data+0x55da8: 1705,1706,1708,1707,0,0,0,1709
```

`cWinCatalog::Init` loads those rows into the Studio `+0x268` and Magic
`+0x288` member arrays. Therefore the native semantic order is:

| slot | Studio Town | Magic Town | mask |
|---:|---|---|---:|
| 0 | Food (`BuyDDining`) | Food (`BuyDDining`) | `0x01` |
| 1 | Shops (`BuyDShops`) | Shops (`BuyDShops`) | `0x02` |
| 2 | Studio (`BuySTStudio`, res 276) | MagiCo (`BuyMTmagic`, res 1708) | `0x04` |
| 3 | Spa (`BuySTSpa`, res 275) | Outdoors (`BuyMOutdoor`, res 1707) | `0x08` |
| 7 | Misc (`BuyDMisc`) | Misc (`BuyDMisc`) | `0x80` |

The old desktop arrays crossed each slot-2 visual over slot-2's data and did
the inverse at slot 3. This made the outdoor bench open Magic objects and the
magic hat open outdoor objects. The click/filter implementation itself was
already correct: `TSOnCommand` passes the raw slot and every expansion mask
getter returns `1 << slot`.

## Native Magic tooltip overrides

Magic initially takes STR#150 `[40+i]`, but `LoadBooks` explicitly replaces:

- slot 2 with STR#150 `[44]`, `MagiCo`;
- slot 3 with STR#150 `[34]`, `Outdoors`.

The pointers are prepared at `0x269320..0x269338`; the replacements occur at
`0x269ed8..0x269f00`. Treating Magic as a plain Superstar `[40+i]` row caused
the visible `Studio` / `Spa` tooltip error.

## Main-state frames

The main category row has no selected/cyan plaque. UpdateView clears all eight
states at `0x2684b8..0x2684dc` and, while sort state is zero, skips the only
`SetState(1)` path at `0x268538`. Clicking a main swaps to the function-subs
row, where subsort 0 is selected and the chosen main is represented by its
large Back plaque. The four sheet columns are normal, selected/cyan, hover,
and disabled/ghost. The port's cyan Food plaque on entry was therefore also a
parity bug.

## Adjacent address-boundary correction

Studio Town addresses end at 89. Magic Town begins at 90 (private 90–92,
public 93–99). Two port branches used Studio `81..90` before testing Magic
`90..99`, making house 90 unreachable as Magic. Both catalog and neighborhood
classification now use disjoint `81..89` / `90..99` ranges. Residential Magic
lots 90–92 still use the normal BUY catalog unless classification is requested
for music/neighborhood mode; that distinction is deliberate.

## Port and gate

- `UIOriginalBuyChrome.ExpMainArt` now follows the two exact native tables.
- `ExpTipIndex` implements Magic's two non-contiguous native strings.
- Expansion main plaques remain frame 0 until the row changes to subsorts.
- `uiexpband` independently pins exact members, tooltip indices, normal state,
  raw Studio/Magic masks, named object anchors, all four live Studio click-to-filter
  transitions and their Back plaques, and the 89/90 boundary.
- `uinbhd` mounts the real Studio and Magic selectors and requires the exact
  hotspot sets `81..89` and `90..98`, preventing Studio lot 90 from routing to Magic.
- `r176-expansion-category-decode.py` re-verifies the original table pointers
  and decisive `LoadBooks` instructions without emitting proprietary bytes.
