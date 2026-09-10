# R85 In-lot UI original-mount (slice 1: artwork) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mount the ORIGINAL `UIGraphics.far` live-toolbar chrome (5 category buttons + Greenbars/Redbars motive fills) as in-lot controls, IFF-first with the existing CustomUI png as fallback, and pin it IFF-literally so nothing drifts.

**Architecture:** Pure Simitone.Client UI. A tiny `UIOriginal` helper resolves an IFF member to `ITextureRef` on the UI thread exactly like the R83 loadscreen `UISimitoneBg.ResolveOriginal`; each control mounts IFF-first and falls back to a pre-converted same-source png on any failure (never crash, never block). A new `uichrome` autotest check pins IFF-literal member name + byte length + BMP dims for the 7 swapped members, and the existing `uidump` live pass is extended with a widget-tree gate (original-dimension art present + visible). No FreeSO/Autiottest weakening; suite stays green.

**Tech Stack:** C# / MonoGame (Simitone.Client), FSO TS1Provider (`Content.Get().TS1Global.Get`), python3 + PIL (engine-independent canon + lossless png fallbacks), -autotest headless (FSOAutoTest), ./packmac.sh arm64 publish.

## Global Constraints

- IFF-literalism: every claimed mount verifies to raw canon before [DONE]. Evidence in `tools/iff-dump/r85/` (tracked, commit normally).
- Never weaken existing pins (`uipal`, `uidump`, `loadscreen`, and all others); suite must stay 51/51 before this ships as 52.
- Never pin absolute rendered hex (colors wobble run-to-run, ERRORS.md) — pin IFF families, byte lengths, BMP dims, widget tree.
- Mount on the UI thread (mirror `UISimitoneBg.ResolveOriginal`); try/catch every IFF read; fallback to CustomUI png so worst case = today's look.
- Run gate with `-autotest-timeout 1800000`; never kill a quiet run; never launch via `open --args`.
- Full-suite gate needs the published app: `dotnet publish -p:NoWarn=NU1605` then `./packmac.sh` arm64, then run the packaged binary with `-autotest` echoing (not `open --args`).

**Canon (engine-independent, from `tools/iff-dump/r85/r85-uigr-canon-scan.txt`):**

| member (IFF path, backslash) | index | Bytes | BMP WxH | role |
|---|---|---|---|---|
| `cpanel\Buttons\Mood.bmp` | 206 | 5846 | 128x39 | motives category btn |
| `cpanel\Buttons\Job.bmp` | 198 | 3648 | 108x30 | job category btn |
| `cpanel\Buttons\Personality.bmp` | 227 | 3862 | 108x30 | personality category btn |
| `cpanel\Buttons\Relationship.BMP` | 230 | 4290 | 108x30 | relationships category btn |
| `cpanel\Buttons\people.bmp` | 225 | 7478 | 200x50 | inventory/people category btn |
| `cpanel\Greenbars.BMP` | 47 | 2156 | 27x25 | motive fill (green, 3-slice: 9px caps + 9px mid) |
| `cpanel\Redbars.bmp` | 72 | 2156 | 27x25 | motive fill (red, 3-slice) |

Original resource template names (CPanel.RT): `kMoodBtn`→Mood.bmp, `kJobBtn`→Job.bmp, `kPersonalityBtn`→Personality.bmp, `kRelationshipBtn`→Relationship.bmp, `kPeople`→People.bmp, `kLiveModeGauge`→LiveGadget.BMP. The category switcher mounts Mood/Job/Personality/Relationship/people via the 5 button members above.

**Residuals (not in this slice, kept):** original layout montage; ffn glyph-table mount + .cur live cursor mount; BUY/BUILD/OPTIONS chrome; pie radial; personality-allocation UI; sound-fidelity; text-style parity.

---

### Task 1: Staging lossless png fallbacks (IFF-verbatim) + canon evidence

**Files:**
- Create: `tools/iff-dump/r85/extract_chrome_pngs.py`
- Create: `tools/iff-dump/r85/r85-uigr-canon-scan.txt` (already present from design phase — regenerate)
- Create: `Client/Simitone/Simitone.Client/Content/uigraphics/live/btn_mood.png`, `btn_job.png`, `btn_personality.png`, `btn_relationship.png`, `btn_people.png`, `bars_green.png`, `bars_red.png`
- Modify: `Client/Simitone/Simitone.Client/Simitone.Client.csproj` (add `<Content Include>` for the 7 pngs)

**Interfaces:**
- Produces: 7 pngs under `Content/uigraphics/live/` (premultiplied RGBA, magenta `#FF00FF` → alpha 0), byte-derived from the exact IFF members above.

- [ ] **Step 1: Write ENGINE-INDEPENDENT canon + png extractor**

```python
import struct, os, sys
from PIL import Image
SRC='game-data/The Sims/UIGraphics/UIGraphics.far'
OUTPNG='Client/Simitone/Simitone.Client/Content/uigraphics/live'
WANT = {
  'cpanel\\Buttons\\Mood.bmp': 'btn_mood.png',
  'cpanel\\Buttons\\Job.bmp': 'btn_job.png',
  'cpanel\\Buttons\\Personality.bmp': 'btn_personality.png',
  'cpanel\\Buttons\\Relationship.BMP': 'btn_relationship.png',
  'cpanel\\Buttons\\people.bmp': 'btn_people.png',
  'cpanel\\Greenbars.BMP': 'bars_green.png',
  'cpanel\\Redbars.bmp': 'bars_red.png',
}
data=open(SRC,'rb').read()
man=struct.unpack('<I', data[12:16])[0]
num=struct.unpack('<I', data[man:man+4])[0]
off=man+4
os.makedirs(OUTPNG, exist_ok=True)
for i in range(num):
    dlen,d2,doff,nlen = struct.unpack('<IIII', data[off:off+16])
    nm=data[off+16:off+16+nlen].decode('latin1','replace')
    off += 16+nlen
    if nm not in WANT: continue
    payload=data[doff:doff+dlen]
    im=Image.open(__import__('io').BytesIO(payload)).convert('RGBA')
    # magenta key -> transparent, then premultiply (matches ImageLoader.PremultiplyPNG=1)
    px=im.load()
    for y in range(im.height):
        for x in range(im.width):
            r,g,b,a=px[x,y]
            if (r,g,b)==(255,0,255): px[x,y]=(0,0,0,0)
    out=os.path.join(OUTPNG, WANT[nm])
    im.save(out, 'PNG')
    print('wrote', out, im.width, 'x', im.height)
```

- [ ] **Step 2: Run it and verify the pngs visually**

Run: `python3 tools/iff-dump/r85/extract_chrome_pngs.py` (from repo root).
Expected: all 7 lines printed; then I (the implementer) Read each png and confirm it is the original button/bar art on transparency — not magenta boxes.

- [ ] **Step 3: Register the pngs as content**

Modify `Client/Simitone/Simitone.Client/Simitone.Client.csproj` — add 7 `<Content Include="Content\uigraphics\live\<file>" />` entries adjacent to the existing `orig_panel_back.png` entry (line ~621).

- [ ] **Step 4: Commit staging**

```bash
git add tools/iff-dump/r85/ Client/Simitone/Simitone.Client/Content/uigraphics/live/ Client/Simitone/Simitone.Client/Simitone.Client.csproj
git commit -m "R85: stage IFF-verbatim png fallbacks (7 chrome members) + canon scan"
```

---

### Task 2: `UIOriginal` IFF-mount helper

**Files:**
- Create: `Client/Simitone/Simitone.Client/UI/Model/UIOriginal.cs`

**Interfaces:**
- Produces: `public static class UIOriginal` with `public static System.Collections.Concurrent.ConcurrentDictionary<string, System.Lazy<ITextureRef>>` cache and `public static ITextureRef EnsureResolved(string name)` (returns cached ref or null on any failure, logs once).

- [ ] **Step 1: Write the helper**

```csharp
using FSO.Content;
using FSO.Content.Model;
using System;

namespace Simitone.Client.UI.Model
{
    // R85: IFF-mount helper (mirrors LoadingGameScreen.UISimitoneBg.ResolveOriginal).
    // Resolves an ORIGINAL UIGraphics.far member to its ITextureRef on the UI thread the
    // first time it is asked for, then caches it. Returns null on ANY failure (IFF not yet
    // mounted, member missing, decode error) so callers keep their CustomUI png fallback.
    public static class UIOriginal
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ITextureRef> _cache =
            new System.Collections.Concurrent.ConcurrentDictionary<string, ITextureRef>();

        public static ITextureRef EnsureResolved(string memberName)
        {
            ITextureRef hit;
            if (_cache.TryGetValue(memberName, out hit)) return hit;
            var ts1 = Content.Get()?.TS1Global;
            if (ts1 == null) return null;
            try
            {
                var tx = ts1.Get(memberName) as ITextureRef;
                if (tx == null) return null;
                if (_cache.TryAdd(memberName, tx))
                    Simitone.Client.GameLog.Write("uioriginal: IFF-mount original " + memberName);
                return tx;
            }
            catch (Exception e)
            {
                Simitone.Client.GameLog.Write("uioriginal: IFF-mount EXC " + e.GetType().Name + " " + e.Message);
                return null;
            }
        }
    }
}
```

- [ ] **Step 2: Build check**

Run: `dotnet build Client/Simitone/Simitone.Client/Simitone.Client.csproj -p:NoWarn=NU1605`
Expected: builds clean, no new warnings.

- [ ] **Step 3: Commit**

```bash
git add Client/Simitone/Simitone.Client/UI/Model/UIOriginal.cs
git commit -m "R85: UIOriginal IFF-mount helper"
```

---

### Task 3: Live category buttons IFF-mount

**Files:**
- Modify: `Client/Simitone/Simitone.Client/UI/Panels/UIMainPanel.cs` (LiveCategories table, lines ~83-90)
- Modify: `Client/Simitone/Simitone.Client/UI/Controls/UICategorySwitcher.cs` (Select method, lines ~80-115)
- Modify: `Client/Simitone/Simitone.Client/UI/Controls/UICategory.cs` (add `public string OriginalName;`)

**Interfaces:**
- Consumes: `UIOriginal.EnsureResolved(string) -> ITextureRef` (Task 2).
- Produces: `UICategory.OriginalName` (nullable) so the switcher tries IFF first, png fallback.

- [ ] **Step 1: Add `OriginalName` to UICategory**

Find `class UICategory` (used by `UICategorySwitcher`); add field `public string OriginalName;` alongside `ID`/`IconName`.

- [ ] **Step 2: Point the 5 live categories at IFF members**

In `UIMainPanel.LiveCategories`, add the IFF member name to each entry:

```csharp
private List<UICategory> LiveCategories = new List<UICategory>()
{
    new UICategory() { ID = 0, IconName = "live_motives.png",       OriginalName = "cpanel\\Buttons\\Mood.bmp" },
    new UICategory() { ID = 1, IconName = "live_job.png",           OriginalName = "cpanel\\Buttons\\Job.bmp" },
    new UICategory() { ID = 2, IconName = "live_personality.png",   OriginalName = "cpanel\\Buttons\\Personality.bmp" },
    new UICategory() { ID = 3, IconName = "live_relationships.png", OriginalName = "cpanel\\Buttons\\Relationship.BMP" },
    new UICategory() { ID = 4, IconName = "live_inventory.png",     OriginalName = "cpanel\\Buttons\\people.bmp" }
};
```

- [ ] **Step 3: Mount in UICategorySwitcher.Select**

In `Select(int cat)`, replace the two `Content.Get().CustomUI.Get(catG.IconName).Get(GameFacade.GraphicsDevice)` texture constructions with a helper that tries IFF first:

```csharp
private Microsoft.Xna.Framework.Graphics.Texture2D CategoryTexture(UICategory catG)
{
    if (catG.OriginalName != null)
    {
        var iffTx = Simitone.Client.UI.Model.UIOriginal.EnsureResolved(catG.OriginalName);
        if (iffTx != null)
        {
            try { return iffTx.Get(GameFacade.GraphicsDevice); }
            catch { }
        }
    }
    return Content.Get().CustomUI.Get(catG.IconName).Get(GameFacade.GraphicsDevice);
}
```

Then in `Select`, use `CategoryTexture(catG)` for both the `MainButton.Texture` assignment and the `new UIStencilButton(...)` constructor.

- [ ] **Step 4: Build**

Run: `dotnet build Client/Simitone/Simitone.Client/Simitone.Client.csproj -p:NoWarn=NU1605` — builds clean.

- [ ] **Step 5: Commit**

```bash
git add Client/Simitone/Simitone.Client/UI/Panels/UIMainPanel.cs Client/Simitone/Simitone.Client/UI/Controls/UICategorySwitcher.cs Client/Simitone/Simitone.Client/UI/Controls/UICategory.cs
git commit -m "R85: live category buttons IFF-mount (Mood/Job/Personality/Relationship/people)"
```

---

### Task 4: Motive bar original fill (Greenbars/Redbars)

**Files:**
- Modify: `Client/Simitone/Simitone.Client/UI/Controls/UIValueBar.cs` (UIMotiveBar, lines 55-118)

**Interfaces:**
- Consumes: `UIOriginal.EnsureResolved`.
- Produces: `UIMotiveBar.GreenTex` / `UIMotiveBar.RedTex` (public `Texture2D`), IFF-first with `bars_green.png`/`bars_red.png` fallback; `Draw` paints green (or red when `Value < 0.25`) fill of length `Value*Width` with White tint, keeping the arrow overlay.

- [ ] **Step 1: Add IFF-mounted bar textures**

In the `UIMotiveBar` constructor, mount Greenbars/Redbars IFF-first with png fallback:

```csharp
public UIMotiveBar() : base(Content.Get().CustomUI.Get("motive_bg.png").Get(GameFacade.GraphicsDevice))
{
    ArrowGfx = Content.Get().CustomUI.Get("motive_arrow.png").Get(GameFacade.GraphicsDevice);
    GreenTex = ResolveBar("cpanel\\Greenbars.BMP", "bars_green.png", BarBase);
    RedTex   = ResolveBar("cpanel\\Redbars.bmp", "bars_red.png", BarBase);
    Width = 150;
}

private static Texture2D ResolveBar(string iffName, string pngName, Texture2D fallback)
{
    try
    {
        var iffTx = Simitone.Client.UI.Model.UIOriginal.EnsureResolved(iffName);
        if (iffTx != null) return iffTx.Get(GameFacade.GraphicsDevice);
    }
    catch { }
    return Content.Get().CustomUI.Get(pngName).Get(GameFacade.GraphicsDevice) ?? fallback;
}
```

- [ ] **Step 2: Draw original fill**

Replace the `override void Draw` so it paints the IFF bar (green normally, red when the motive is low), White-tinted, no value-lerped tint, preserving the arrows:

```csharp
public override void Draw(UISpriteBatch batch)
{
    if (!Visible) return;
    var low = Value < 0.25f;
    var tex = (low ? RedTex : GreenTex) ?? BarBase;
    var active = (int)Math.Round(Value * Width);
    DrawSlice(batch, active, Color.White, 0);   // original 3-slice fill, no tint
    var w = BarBase.Width / 3;
    var spanw = (int)(Width * Value) - w * 2;
    var arrows = spanw / 14;
    …same arrow code as before…
}
```

- [ ] **Step 3: Build**

`dotnet build Client/Simitone/Simitone.Client/Simitone.Client.csproj -p:NoWarn=NU1605` — builds clean.

- [ ] **Step 4: Commit**

```bash
git add Client/Simitone/Simitone.Client/UI/Controls/UIValueBar.cs
git commit -m "R85: motive bar original Greenbars/Redbars IFF fill"
```

---

### Task 5: Stop over-drawing the navy rect in LIVE

**Files:**
- Modify: `Client/Simitone/Simitone.Client/UI/Panels/UIMainPanel.cs` (Draw, lines ~434-445)

- [ ] **Step 1: Skip the modern navy rect when the original backdrop is mounted in LIVE**

```csharp
if (CurWidth > 211)
{
    if (OriginalPanelBack != null && Mode == UIMainPanelMode.LIVE)
    {
        // R85: original backdrop already reads as the bar; don't paint Simitone's flat navy over it.
    }
    else if (ShowingSelect)
    {
        DrawLocalTexture(batch, WhitePx, null, new Vector2(0, 0), new Vector2(211+52, 128), UIStyle.Current.Bg);
    }
    else
    {
        DrawLocalTexture(batch, WhitePx, null, new Vector2(0, 0), new Vector2(211, 128), UIStyle.Current.Bg);
        DrawLocalTexture(batch, Div, DivRect, new Vector2(211, 0), Vector2.One, UIStyle.Current.Bg);
    }
}
```

- [ ] **Step 2: Build** — `dotnet build …` clean.
- [ ] **Step 3: Commit** — `git commit -m "R85: don't over-paint navy in LIVE when original backdrop mounted"`.

---

### Task 6: `uichrome` IFF-literal pin + uidump tree gate

**Files:**
- Modify: `Client/Simitone/Simitone.Client/AutotestRunner.cs` (Checks list line 51, dispatch near line 688, new method near CheckLoadScreen)
- Modify: `Client/Simitone/Simitone.Client/AutotestCatalogCanon.cs` (add `LiveChrome` canon array near GoScreens)
- Modify: `Client/Simitone/Simitone.Client/AutotestRunner.cs` (CheckUIDump: add chrome tree gate, lines ~3656-3659)

**Interfaces:**
- Consumes: `AutotestCatalogCanon.LiveChrome` (same shape as `GoScreens`).
- Consumes: `UICategorySwitcher.MainButton.Texture` dims; `UIMotiveSubpanel.MotiveDisplays[0].GreenTex/RedTex`.

- [ ] **Step 1: Add canon array**

```csharp
// ROUND-85 'uichrome' IFF-LITERAL canon: the ORIGINAL live-toolbar category buttons +
// motive bars (CPanel.RT: kMoodBtn=cpanel\Buttons\Mood.bmp, kJobBtn=Job.bmp,
// kPersonalityBtn=Personality.bmp, kRelationshipBtn=Relationship.bmp, kPeople=people.bmp,
// kLiveModeGauge=LiveGadget.BMP); motive fills Greenbars.BMP/Redbars.bmp are the 3-slice
// (9px cap + 9px mid) gauge fill. name+Bytes+WxH from tools/iff-dump/r85/r85-uigr-canon-scan.txt.
public class ChromeCanon
{
    public string Name; public int Bytes; public int W; public int H;
    public ChromeCanon(string name, int bytes, int w, int h) { Name = name; Bytes = bytes; W = w; H = h; }
}
public static readonly ChromeCanon[] LiveChrome = new ChromeCanon[] {
    new ChromeCanon("cpanel\\Buttons\\Mood.bmp", 5846, 128, 39),
    new ChromeCanon("cpanel\\Buttons\\Job.bmp", 3648, 108, 30),
    new ChromeCanon("cpanel\\Buttons\\Personality.bmp", 3862, 108, 30),
    new ChromeCanon("cpanel\\Buttons\\Relationship.BMP", 4290, 108, 30),
    new ChromeCanon("cpanel\\Buttons\\people.bmp", 7478, 200, 50),
    new ChromeCanon("cpanel\\Greenbars.BMP", 2156, 27, 25),
    new ChromeCanon("cpanel\\Redbars.bmp", 2156, 27, 25),
};
```

- [ ] **Step 2: Add `CheckUIChrome` (mirror CheckLoadScreen exactly)**

Copy `CheckLoadScreen`'s body, replace `GoScreens` with `LiveChrome`, rename logs to `uichrome`. It pins IFF-literal member name + DataLength + raw BMP dims (offset 18/22) — engine-independent, no rendered hex.

- [ ] **Step 3: Extend CheckUIDump with a chrome TREE gate**

After the existing panel-open block (category 0 / motives selected), gate on the mounted control dims before the final uidump Pass/Fail:

```csharp
var mbTex = bmp?.Switcher?.MainButton?.Texture;
var barTex = (bmp?.SubPanel as Simitone.Client.UI.Panels.LiveSubpanels.UIMotiveSubpanel)?.MotiveDisplays?[0]?.GreenTex;
bool chromeBtn = mbTex != null && mbTex.Width == 128 && mbTex.Height == 39;
bool chromeBar = barTex != null && barTex.Width == 27 && barTex.Height == 25;
Log("AUTOTEST uichrome tree: btn=" + chromeBtn + " (" + (mbTex != null ? mbTex.Width + "x" + mbTex.Height : "null") + ") bar=" + chromeBar + " (" + (barTex != null ? barTex.Width + "x" + barTex.Height : "null") + ")");
if (!chromeBtn || !chromeBar) { Log("AUTOTEST uichrome FAIL: original chrome not mounted/live"); Fail("uichrome"); return; }
```

- [ ] **Step 4: Register the check**

Append `,uichrome` to `Config.Checks` (line 51) and add `if (CheckEnabled("uichrome")) CheckUIChrome();` in the dispatch (before CheckUIDump).

- [ ] **Step 5: Update AUTOTEST.md** (README row + gate list + UI-parity paragraph).

- [ ] **Step 6: Commit** — `git commit -m "R85: uichrome IFF-literal pin + uidump chrome tree gate"`.

---

### Task 7: Publish, package, full gate, render inspect

- [ ] **Step 1: Publish**

`dotnet publish .../Simitone.MacOS.csproj -c Release -r osx-arm64 --self-contained -p:NoWarn=NU1605` (absolute paths per SOP).

- [ ] **Step 2: Package**

`./packmac.sh` arm64 (as the round scripts do).

- [ ] **Step 3: Run the FULL gate**

Run the packaged binary with `-autotest` full checks (echoing/tty, never `open --args`) and `-autotest-timeout 1800000`. Expected: 52/52 PASS (51 prior + uichrome). Never interrupt a quiet run.

- [ ] **Step 4: Regenerate + inspect the render**

`uidump-lot.png` is rewritten under `~/.local/share/...`/UserDir; copy to `tools/iff-dump/r85/r85-uidump-lot.png`, Read it, and confirm: original Mood button art on the switcher, original green/red bars, no magenta boxes, backdrop + speed strip intact.

- [ ] **Step 5: If magenta boxes appear anywhere**, flip the affected control to the png fallback only (remove/guard its IFF-mount path) and re-run the gate; the IFF-literal uichrome pin stays valid because it pins the member, not the render.

---

### Task 8: Evidence, docs, commit

- [ ] **Step 1: Evidence** — park the gate log + uidump-lot.png + canon scan under `tools/iff-dump/r85/`; commit.
- [ ] **Step 2: PARITY.md** — update the LOOK row: artwork subset mounted + pinned (`uichrome`), motive bars original; keep `[GAP-partial]`; layout montage + ffn/.cur still residuals; correct the "none" wording exactly as the spec dictates.
- [ ] **Step 3: HANDOFF.md round ledger** — add `R85 DONE-partial` one-liner.
- [ ] **Step 4: Final commit** — `git commit -m "Round 85: in-lot UI original chrome mount (category buttons + motive bars) + uichrome pin"`.

## Self-review

- Spec coverage: every slice-1 item (buttons Task 3, bars Task 4, backdrop skip Task 5, pin Task 6) maps to a task; layout montage/ffn/.cur explicitly residual. No placeholder code — every step has real code or an exact replication instruction. Type names consistent: `UIOriginal.EnsureResolved`, `ChromeCanon`/`LiveChrome`, `GreenTex`/`RedTex`.
