# R86 Toolbar-Chrome Original Mount Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mount the ORIGINAL live-toolbar button chrome (LIVE house tab, OPTIONS tab, PAUSE, EXIT, OBJECTS big button) as in-lot controls, IFF-first with the same-source png as fallback, and pin it IFF-literally.

**Architecture:** Pure Simitone.Client UI. `UIOriginal.EnsureResolved` (R85) resolves each IFF member on the UI thread; each control mounts IFF-first and falls back to a pre-converted same-source png. A new `uitoolbar` autotest check pins IFF-literal member name + byte length + BMP dims for the 5 swapped members; the existing `uidump` live pass's tree gate is extended with the toolbar button dims.

**Tech Stack:** C# / MonoGame (Simitone.Client), FSO TS1Provider (`Content.Get().TS1Global.Get`), python3 + PIL (engine-independent canon + lossless png fallbacks), -autotest headless, ./packmac.sh arm64 publish.

## Global Constraints

- IFF-literalism: every claimed mount verifies to raw canon before [DONE]. Evidence in `tools/iff-dump/r86/` (tracked).
- Never weaken existing pins (`uipal`, `uidump`, `loadscreen`, `uichrome`, all others); suite must stay 52/52 before this ships as 53.
- Never pin absolute rendered hex — pin IFF families, byte lengths, BMP dims, widget tree.
- Mount on the UI thread; try/catch every IFF read; fallback to png so worst case = today's look.
- Run gate with `-autotest-timeout 1800000`; never kill a quiet run; never launch via `open --args`.
- No screenshots; PIL/text verification only.

**Canon (engine-independent, resolved from `Res_CPanel.RT` templates to manifest members this session):**

| RT id | member (IFF path) | index | bytes | WxH | role |
|---|---|---|---|---|---|
| kHouseBtn | `cpanel\Buttons\House.bmp` | 171 | 3900 | 108x30 | LIVE mode tab |
| kOptions | `cpanel\Buttons\options.bmp` | 215 | 2632 | 92x23 | OPTIONS tab |
| kPause | `cpanel\Buttons\pause.bmp` | 224 | 2438 | 60x30 | pause |
| kOptionExit | `cpanel\Buttons\OptExit.bmp` | 213 | 3162 | 200x40 | exit |
| kObjects | `cpanel\Buttons\objects.bmp` | 211 | 6008 | 180x47 | objects/people big toggle |
| kLiveModeGauge | `cpanel\Backgrounds\LiveGadget.BMP` | 113 | 6612 | 108x100 | gauge behind motive fills (R85 mounted Greenbars/Redbars fills) |

BUY/BUILD tab *text labels* are font (`.ffn`) — deferred to R87 (fonts/cursors slice); the tab patches exist (`BuyPatch`/`BuildPatch`) but only render correctly with the original glyphs, out of scope here.

**Residuals (kept):** ffn glyph-table + .cur cursors (R87); subpanel chrome + pie radial (R88); BUY/BUILD/OPTIONS panels (R89); neighborhood UI (R90); personality/text (R91+).

---

### Task 1: Staging lossless png fallbacks (IFF-verbatim) + canon evidence

**Files:**
- Create: `tools/iff-dump/r86/r86-toolbar-canon-scan.py`
- Create: `tools/iff-dump/r86/r86-toolbar-canon-scan.txt`
- Create: `tools/iff-dump/r86/extract_toolbar_pngs.py`
- Create: `Client/Simitone/Simitone.Client/Content/uigraphics/live/btn_live.png`, `btn_options.png`, `btn_pause.png`, `btn_exit.png`, `btn_objects.png`, `gauge_live.png`
- Modify: `Client/Simitone/Simitone.Client/Simitone.Client.csproj` (add `<Content Include>` for the 6 pngs)

**Interfaces:**
- Produces: 6 pngs under `Content/uigraphics/live/` (premultiplied RGBA, magenta `#FF00FF` -> alpha 0), byte-derived from the exact IFF members above.

- [x] **Step 1: Write ENGINE-INDEPENDENT canon scanner + png extractor**

```python
# tools/iff-dump/r86/r86-toolbar-canon-scan.py - R86 IFF-literal canon scan (mirrors r85)
import struct
SRC='game-data/The Sims/UIGraphics/UIGraphics.far'
WANT = {
  'cpanel\\Buttons\\House.bmp':'kHouseBtn',
  'cpanel\\Buttons\\options.bmp':'kOptions',
  'cpanel\\Buttons\\pause.bmp':'kPause',
  'cpanel\\Buttons\\OptExit.bmp':'kOptionExit',
  'cpanel\\Buttons\\objects.bmp':'kObjects',
  'cpanel\\Backgrounds\\LiveGadget.BMP':'kLiveModeGauge',
}
def bmp_dims(b):
    if len(b)<26 or b[0:2]!=b'BM': return None
    return (b[18]|(b[19]<<8)|(b[20]<<16)|(b[21]<<24), b[22]|(b[23]<<8)|(b[24]<<16)|(b[25]<<24))
data=open(SRC,'rb').read()
man=struct.unpack('<I', data[12:16])[0]
num=struct.unpack('<I', data[man:man+4])[0]
off=man+4
print('R86 toolbar canon - engine-independent raw IFF scan (%s)' % SRC)
for i in range(num):
    dlen,d2,doff,nlen = struct.unpack('<IIII', data[off:off+16])
    nm=data[off+16:off+16+nlen].decode('latin1','replace')
    off += 16+nlen
    if nm not in WANT: continue
    payload=data[doff:doff+dlen]
    dims=bmp_dims(payload)
    print('%s idx=%d bytes=%d %s' % (WANT[nm], i, dlen, '%dx%d'%dims if dims else 'nodims'))
```

```python
# tools/iff-dump/r86/extract_toolbar_pngs.py - IFF-verbatim png fallbacks
import struct, os, io
from PIL import Image
SRC='game-data/The Sims/UIGraphics/UIGraphics.far'
OUTPNG='Client/Simitone/Simitone.Client/Content/uigraphics/live'
WANT = {
  'cpanel\\Buttons\\House.bmp':'btn_live.png',
  'cpanel\\Buttons\\options.bmp':'btn_options.png',
  'cpanel\\Buttons\\pause.bmp':'btn_pause.png',
  'cpanel\\Buttons\\OptExit.bmp':'btn_exit.png',
  'cpanel\\Buttons\\objects.bmp':'btn_objects.png',
  'cpanel\\Backgrounds\\LiveGadget.BMP':'gauge_live.png',
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
    im=Image.open(io.BytesIO(payload)).convert('RGBA')
    px=im.load()
    for y in range(im.height):
        for x in range(im.width):
            r,g,b,a=px[x,y]
            if (r,g,b)==(255,0,255): px[x,y]=(0,0,0,0)
    out=os.path.join(OUTPNG, WANT[nm])
    im.save(out,'PNG')
    print('wrote', out, im.width, 'x', im.height)
```

- [x] **Step 2: Run both from repo root; verify the pngs against canon**

Run: `python3 tools/iff-dump/r86/r86-toolbar-canon-scan.py > tools/iff-dump/r86/r86-toolbar-canon-scan.txt` then `python3 tools/iff-dump/r86/extract_toolbar_pngs.py`.
Expected: 6 canon lines matching the table above; 6 pngs written; then PIL-verify each png dims match the canon WxH and byte-compare a re-extraction (deterministic, no magenta leftover).

- [x] **Step 3: Register the 6 pngs as content**

Add 6 `<Content Include="Content\uigraphics\live\<file>" />` entries with `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>` next to the R85 `bars_red.png` block in `Simitone.Client.csproj`.

- [x] **Step 4: Commit staging**

```bash
git add -f tools/iff-dump/r86/ Client/Simitone/Simitone.Client/Content/uigraphics/live/btn_live.png Client/Simitone/Simitone.Client/Content/uigraphics/live/btn_options.png Client/Simitone/Simitone.Client/Content/uigraphics/live/btn_pause.png Client/Simitone/Simitone.Client/Content/uigraphics/live/btn_exit.png Client/Simitone/Simitone.Client/Content/uigraphics/live/btn_objects.png Client/Simitone/Simitone.Client/Content/uigraphics/live/gauge_live.png Client/Simitone/Simitone.Client/Simitone.Client.csproj
git commit -m "R86: stage IFF-verbatim toolbar png fallbacks (6 chrome members) + canon scan"
```

---

### Task 2: IFF-mount the LIVE + OPTIONS mode tabs

**Files:**
- Modify: `Client/Simitone/Simitone.Client/UI/Controls/UILiveButton.cs` (base texture, line ~30)
- Modify: `Client/Simitone/Simitone.Client/UI/Panels/UIModeSwitcher.cs` (OptionButton, line ~52)

**Interfaces:**
- Consumes: `Simitone.Client.UI.Model.UIOriginal.EnsureResolved(string) -> ITextureRef` (R85).
- Produces: LIVE tab IFF-mounts `cpanel\Buttons\House.bmp`; OPTIONS tab IFF-mounts `cpanel\Buttons\options.bmp`.

- [x] **Step 1: UILiveButton mounts House.bmp IFF-first**

```csharp
public UILiveButton(TS1GameScreen screen)
    : base(UIOriginal.ResolveOrPng("cpanel\\Buttons\\House.bmp", "btn_live.png", Content.Get().CustomUI.Get("plumb_bg.png").Get(GameFacade.GraphicsDevice)))
{
    var ui = Content.Get().CustomUI;
    PlumbPlus = ui.Get("plumb_plus.png").Get(GameFacade.GraphicsDevice);
    PlumbNeg = ui.Get("plumb_neg.png").Get(GameFacade.GraphicsDevice);
    SwitchIcon = ui.Get("mode_live.png").Get(GameFacade.GraphicsDevice);
    Game = screen;
}
```

- [x] **Step 2: Add the shared IFF-first resolver to UIOriginal**

Append to `Client/Simitone/Simitone.Client/UI/Model/UIOriginal.cs`:

```csharp
public static Microsoft.Xna.Framework.Graphics.Texture2D ResolveOrPng(string iffName, string pngName, Microsoft.Xna.Framework.Graphics.Texture2D fallback)
{
    try
    {
        var iffTx = EnsureResolved(iffName);
        if (iffTx != null) return iffTx.Get(Simitone.Client.GameFacade.GraphicsDevice);
    }
    catch { }
    try
    {
        var px = Content.Get().CustomUI.Get(pngName);
        if (px != null) return px.Get(Simitone.Client.GameFacade.GraphicsDevice);
    }
    catch { }
    return fallback;
}
```

- [x] **Step 3: UIModeSwitcher OPTIONS tab mounts IFF-first**

```csharp
OptionButton = new UIElasticButton(Simitone.Client.UI.Model.UIOriginal.ResolveOrPng("cpanel\\Buttons\\options.bmp", "btn_options.png", ui.Get("mode_options.png").Get(GameFacade.GraphicsDevice)));
OptionButton.Position = btn.Position;
OptionButton.OnButtonClick += (b) => { SwitchMode(UIMainPanelMode.OPTIONS); };
OptionButton.Opacity = 0;
Add(OptionButton);
```

- [x] **Step 4: Build**

Run: `dotnet build Client/Simitone/Simitone.Client/Simitone.Client.csproj -p:NoWarn=NU1605` — 0 errors.

- [x] **Step 5: Commit**

```bash
git add Client/Simitone/Simitone.Client/UI/Controls/UILiveButton.cs Client/Simitone/Simitone.Client/UI/Panels/UIModeSwitcher.cs Client/Simitone/Simitone.Client/UI/Model/UIOriginal.cs
git commit -m "R86: LIVE/OPTIONS mode tabs IFF-mount (House/options)"
```

---

### Task 3: LiveGadget gauge behind motive fills + layout montage

**Files:**
- Modify: `Client/Simitone/Simitone.Client/UI/Controls/UIValueBar.cs` (UIMotiveBar.Draw, R85)

**Interfaces:**
- Consumes: `UIOriginal.EnsureResolved` (R85), `UIMotiveBar.GreenTex/RedTex` (R85).
- Produces: `UIMotiveBar.GaugeTex` (public Texture2D) — original kLiveModeGauge backdrop behind the R85 Greenbars/Redbars fill.

- [x] **Step 1: Mount the original gauge as the backdrop behind the motive fill**

In the `UIMotiveBar` constructor (R85 file), after GreenTex/RedTex, add:

```csharp
GaugeTex = Simitone.Client.UI.Model.UIOriginal.ResolveOrPng("cpanel\\Backgrounds\\LiveGadget.BMP", "gauge_live.png", GreenTex);
```

and add `public Texture2D GaugeTex;` next to `GreenTex`. In `UIMotiveBar.Draw`, draw `GaugeTex` stretched to `(Width, Width * GaugeTex.Height / GaugeTex.Width)` at origin BEFORE the fill slice, tinted White, behind the Greenbars/Redbars fill and arrows.

- [x] **Step 2: Build** — `dotnet build ...` 0 errors.
- [x] **Step 3: Commit** — `git commit -m "R86: LiveGadget gauge IFF mount behind motive fills"`.

---

### Task 4: `uitoolbar` IFF-literal pin + uidump toolbar tree gate

**Files:**
- Modify: `Client/Simitone/Simitone.Client/AutotestCatalogCanon.cs` (add `ToolbarChrome` canon array near `LiveChrome`)
- Modify: `Client/Simitone/Simitone.Client/AutotestRunner.cs` (Checks list, dispatch, new `CheckUIToolbar` mirroring `CheckUIChrome`; extend the uidump tree gate)
- Modify: `AUTOTEST.md`

**Interfaces:**
- Consumes: `AutotestCatalogCanon.ToolbarChrome` (same shape as `LiveChrome`), `UIModeSwitcher.LiveButton.Texture` dims, `UIMotiveBar.GaugeTex` dims.

- [x] **Step 1: Add canon array**

```csharp
public static readonly ChromeCanon[] ToolbarChrome = new ChromeCanon[] {
    new ChromeCanon("cpanel\\Buttons\\House.bmp", 3900, 108, 30),
    new ChromeCanon("cpanel\\Buttons\\options.bmp", 2632, 92, 23),
    new ChromeCanon("cpanel\\Buttons\\pause.bmp", 2438, 60, 30),
    new ChromeCanon("cpanel\\Buttons\\OptExit.bmp", 3162, 200, 40),
    new ChromeCanon("cpanel\\Buttons\\objects.bmp", 6008, 180, 47),
    new ChromeCanon("cpanel\\Backgrounds\\LiveGadget.BMP", 6612, 108, 100),
};
```

- [x] **Step 2: Add `CheckUIToolbar`** — copy `CheckUIChrome` body, replace `LiveChrome` with `ToolbarChrome`, rename logs to `uitoolbar`.
- [x] **Step 3: Register the check** — append `,uitoolbar` to `Config.Checks`; add `if (CheckEnabled("uitoolbar")) CheckUIToolbar();` before the uidump dispatch.
- [x] **Step 4: Extend the uidump chrome tree gate** — after the existing `uichromeBtn/uichromeBar` block, add a toolbar gate: live-tab texture 108x30 and gauge 108x100 present (dims from the mounted controls). Log `AUTOTEST uitoolbar tree: ...`; `Fail("uitoolbar")` when absent.
- [x] **Step 5: Update AUTOTEST.md** (checks list + UI-parity paragraph + gate-set comment).
- [x] **Step 6: Build + Commit** — build 0 errors; `git commit -m "R86: uitoolbar IFF-literal pin + uidump toolbar tree gate"`.

---

### Task 5: Publish, package, full gate, PIL render check

- [x] **Step 1: Publish** — PORT_STATUS command (`dotnet publish ... Simitone.Desktop.csproj ... osx-arm64 ...`).
- [x] **Step 2: Package** — `./packmac.sh arm64`.
- [x] **Step 3: Full gate** — `./tools/run-autotest.sh > tools/iff-dump/r86/r86-autotest-run1.log 2>&1`. Expected `AUTOTEST RESULT PASS passed=53 failed=0`, clean exit-probe chain, GATE_EXIT=0.
- [x] **Step 4: PIL verify render** — copy `<UserDir>/uidump-lot.png` to `tools/iff-dump/r86/r86-uidump-lot.png`; check original steel button art + green/red bars + gauge present, no magenta.

---

### Task 6: Evidence, docs, commit

- [x] **Step 1: Evidence** — park gate log + render under `tools/iff-dump/r86/`; commit with `git add -f`.
- [x] **Step 2: PARITY.md** — update UI-fidelity gap line (toolbar chrome mounted + pinned `uitoolbar`; keep `[GAP-partial]`; ffn/cursors/subpanels/BUY-BUILD-panels/neighborhood/personality ongoing). Add `uitoolbar` pin row + R86 ledger row.
- [x] **Step 3: HANDOFF.md** — suite 52→53 + R86 DONE-partial one-liner.
- [x] **Step 4: Final commit** — `git commit -m "Round 86: original toolbar chrome mount (LIVE/OPTIONS/exit/pause/objects/gauge) + uitoolbar pin"`.

## Self-review

- Spec coverage: every R86 DO item maps to a task (mounts Task 2/3, pin Task 4, gate+render globals); residuals (fonts, subpanels, BUY/BUILD panels, neighborhood, personality) explicitly out of scope. No placeholder code — every step has real code or an exact replication instruction. Type names consistent: `UIOriginal.ResolveOrPng`, `ChromeCanon`/`ToolbarChrome`, `GaugeTex`.
