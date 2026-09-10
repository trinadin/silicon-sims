# R182 — Studio Town category-strip recheck

## Reported surface

The reported crop is Studio Town's unopened BUY main row: Food, Shops,
Studio, Spa, plus Misc in slot 7.  The bad surface had two independent faults:

- the unopened row retained the cyan selected frame on Food;
- Studio and Spa were inferred from a sequential resource range even though
  the original table is non-sequential.

## Owner-executable evidence

`r176-expansion-category-decode.py` re-read the owner's local Complete
executable (SHA-256
`33c76da298841dcaf8402eff8814700bb20e36bb22878d00ea16fcc874c06a5f`)
and re-verified:

```text
Studio: [273, 274, 276, 275, 0, 0, 0, 277]
Magic:  [1705, 1706, 1708, 1707, 0, 0, 0, 1709]
```

Thus Studio slots 0..3 are exactly `BuyDDining`, `BuyDShops`,
`BuySTStudio`, `BuySTSpa`.  The main row is frame 0 throughout; frame 1 is
reserved for a selected subsort after a main category opens.

## End-to-end click law

The packaged `uiexpband` gate enters a forced Studio Town address through the
real `UIMainPanel` BUY path and dispatches actual `UIButton` mouse events to
all four plaques.  For every slot it requires all of the following to agree:

| slot | visual / tooltip | mask | category-exclusive anchor |
|---:|---|---:|---|
| 0 | Food / `BuyDDining` | `0x01` | Buffet Table - Studio (`3672a8b2`) |
| 1 | Shops / `BuyDShops` | `0x02` | Cash Register - Fame (`8ad987b8`) |
| 2 | Studio / `BuySTStudio` | `0x04` | Music Recording Studio (`1d772052`) |
| 3 | Spa / `BuySTSpa` | `0x08` | Spa - Tub - Single (`52ae7a4e`) |

It additionally pins the corresponding Back plaque, selected main slot,
subsort transition, and function-filtered result set.  This prevents a static
icon table and a separately correct data filter from accidentally blessing a
disconnected click route.

## Packaged verification

Focused packaged run:

```text
checks: lot, corpus, uiexpband, uinbhd, uibandlaw, uivis, uisurvey
uiexpband: art=True (23 pins), chrome=True, live=True, boundary=True
Studio masks: slot 2=270 items, slot 3=110 items
summary: 11 passed, 0 failed, 0 skipped
```

- log: `/tmp/r182-expansion-focus.log`
- log SHA-256: `00a5f67f0a4d024333394b741eb9e55fd37994970f2969250928ddab3c354488`
- packaged `Simitone.Client.dll` SHA-256:
  `b73d3f62e74d440dec396711fa6324f89522860b5730b315ae7e4af6ee5cc5c5`
- refreshed Studio main-row survey:
  `/Users/nathannoom/Documents/Simitone/uisurvey-studio-main.png`
- survey SHA-256:
  `26c2c9bd1902b118b7b88151ef0a192a4ba3be083755b1a6eec4d6b26ad1a4a0`

The focused recheck required no further production edit beyond the already
applied non-sequential resource correction, frame-state correction, and live
click-path wiring.  `git diff --check` is clean.
