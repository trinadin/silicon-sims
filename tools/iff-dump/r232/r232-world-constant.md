# R232 — the [world+12] candidate + the outside tail (evidence round)

## [world+12] (the phase-1 stat source)

The world struct (TOC−29216 → BSS 0x90850) is runtime-initialized. The only
`stfs +12` candidate near any TOC−29216 reference is the LightLayer init
block at 0x1dc1f4: `stfs f23, 0xc(r31)` with f23 = TOC−0x1a4c = **1.0**
(part of the 1.0-heavy constant block copied into a BSS struct at
TOC−0x54f8). If r31 aliases the world struct (both BSS), **[world+12] =
1.0** → room108 (phase-1) = 1.0² × type-factor.

## Consequence for the calibration

With room108 = 1.0 (type factor 1): the object term = +20 → the hypC
shadows land at **23.53 (House07) / 63.33 (House05's room 4)** vs the
fixtures 50.222 / 86.333 — both ~26.7/~23 short, a CONSISTENT gap
suggesting exactly one missing additive term (~+26.7/+23) rather than a
wrong constant: the outside tail (the object/PositionLight iteration at
0x126ba0+, whose loop math — 255×room108 scaling seen at 0x126ba0-0x126bcc —
remains to be decoded) or a second light-stat term.

## Status

The pin is still honestly blocked: the fitted 1.6 from R231 is now
EXPLAINED (1.0 + the missing ~0.6 term), and the remaining decode is the
0x126ba0-0x126dec outside/object iteration. No engine change.
