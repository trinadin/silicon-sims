#!/usr/bin/env python3
"""Prepare a reviewable patch only; never mutate maintained product sources."""
from pathlib import Path
import difflib

ROOT = Path(__file__).resolve().parents[3]
CLIENT = "Client/Simitone/Simitone.Client/"
changes = {}

def replace(path, old, new):
    if path not in changes:
        source = (ROOT / path).read_text()
        changes[path] = [source, source]
    assert old in changes[path][1], (path, old)
    changes[path][1] = changes[path][1].replace(old, new)

font = CLIENT + "UI/Controls/UIOriginalText.cs"
replace(font, "        public static OriginalGlyphFont FromFFN", """        // InitBitmapped 0x4b4640–47dc normalizes every runtime glyph offset
        // by the minimum nonempty, non-NBSP top. Preserve raw FFN T2 for
        // byte-level evidence; only destinations use the normalized offset.
        public int RuntimeTopOffset { get; private set; }
        public int GlyphY(Glyph glyph) => glyph.T2 + RuntimeTopOffset;

        public static OriginalGlyphFont FromFFN""")
replace(font, "                f.Glyphs[k] = g;", """                f.Glyphs[k] = g;
                if (g.Char != 160 && g.W != 0 && g.H != 0)
                    f.RuntimeTopOffset = System.Math.Max(f.RuntimeTopOffset, -g.T2);""")
replace(font, "same raw-T2 draw space used by UIOriginalText", "same normalized draw space used by UIOriginalText")
replace(font, "(height - capitalA.H) / 2 - capitalA.T2", "(height - capitalA.H) / 2 - GlyphY(capitalA)")
replace(font, "pos.Y + g.T2", "pos.Y + GlyphY(g)")
replace(font, "new Vector2(x, g.T2)", "new Vector2(x, Font.GlyphY(g))")
for path, old, new in [
    ("UI/Controls/OriginalVectorFont.cs", "pos.Y + g.T2 * s", "pos.Y + f.GlyphY(g) * s"),
    ("UI/Controls/UIBigButton.cs", "y + g.T2", "y + font.GlyphY(g)"),
    ("UI/Controls/UIOriginalTextEdit.cs", "y + glyph.T2", "y + Font.GlyphY(glyph)"),
    ("UI/Controls/UIOriginalTextList.cs", "new Rectangle(x, g.T2,", "new Rectangle(x, Font.GlyphY(g),"),
    ("UI/Panels/UIOriginalHelpDialog.cs", "(int)line.Y + glyph.T2", "(int)line.Y + Font.GlyphY(glyph)"),
    ("UI/Panels/LiveSubpanels/UIJobSubpanel.cs", "y + glyph.T2", "y + Font.GlyphY(glyph)"),
    ("UI/Panels/LiveSubpanels/UIJobSubpanel.cs", "new Vector2(x, glyph.T2)", "new Vector2(x, Font.GlyphY(glyph))"),
]:
    replace(CLIENT + path, old, new)
nav = CLIENT + "UI/Panels/UINeighbourhoodSwitcher.cs"
replace(nav, """        // variablesans_12 minT2=-4; original InitBitmapped subtracts minT2
        // from every runtime glyph record before the current-number draw.
        internal const int CurrentNumberGlyphT2Normalization = 4;
""", "")
replace(nav, """                        // InitBitmapped normalizes every runtime glyph T2 by
                        // -minT2. variablesans_12 has minT2=-4; UIOriginalText
                        // intentionally retains raw FFN T2, so apply that exact
                        // font-local normalization only to this engine draw.
                        3 + indTex.Height / 2 - 1 - textHeight / 2
                            + CurrentNumberGlyphT2Normalization""", """                        // The shared font now normalizes glyph destinations.
                        3 + indTex.Height / 2 - 1 - textHeight / 2""")
gate = CLIENT + "AutotestRunner.cs"
replace(gate, "Simitone.Client.UI.Panels.UINeighbourhoodSwitcher.CurrentNumberGlyphT2Normalization == 4", "numberFont.RuntimeTopOffset == 4")
replace(gate, "                            + Simitone.Client.UI.Panels.UINeighbourhoodSwitcher.CurrentNumberGlyphT2Normalization\n", "")
layer = "FreeSO/TSOClient/FSO.UI/UILayer.cs"
replace(layer, """            // cached glyph sheet normalizes its -2 top bearing, while our raw
            // FFN renderer leaves that bearing on every glyph. Add those two
            // pixels here so the visible ink lands at the original baseline.""", """            // cached glyph sheet normalizes its -2 top bearing; the shared
            // FFN renderer now applies the same normalization to destinations.""")
replace(layer, "            const int tooltipGlyphTopNormalization = 2;\n", "")
replace(layer, "(tooltipYInset + tooltipGlyphTopNormalization) * toolScale", "tooltipYInset * toolScale")
patch = "".join("".join(difflib.unified_diff(old.splitlines(True), new.splitlines(True),
    fromfile="a/" + path, tofile="b/" + path)) for path, (old, new) in changes.items())
target = Path(__file__).with_name("font-normalization-review.patch")
target.write_text(patch)
print(f"Prepared {target}: {len(changes)} source paths; no source edits applied")
