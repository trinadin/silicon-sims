using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using FSO.SimAntics.Model.TS1Platform;
using FSO.SimAntics.NetPlay.Drivers;
using FSO.SimAntics.Primitives;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Panels.LiveSubpanels;
using Simitone.Client.UI.Screens;

namespace Simitone.Client
{
    // R245 battery: the tutorial lesson-highlight window (cTSWinTutorialHighlight
    // port) + the UI-event poll loop, on the AutotestTutorialUI241 pattern —
    // isolated unticked VM + real UILotControl sink, deterministic Clock,
    // RenderTarget2D captures, per-pixel asserts, full state restore, and no
    // owner-file mutation (PNG evidence only, under <UserDir>/ui-audit/r245).
    internal static class AutotestTutorialHighlight245
    {
        /// <summary>The 'ui events' BCON of the tutorial object: the script
        /// order of the event codes (labelled 'ui events' in the owner's
        /// private Tutorial.iff; read from the real owner resource). The
        /// chunk's raw id bytes are 10 01 — the game's IFF parser indexes ids
        /// big-endian, so the game-visible id is 0x1001 = 4097 (same reading
        /// as every other TS1 BCON lookup, e.g. the car-tuning 4097); a
        /// little-endian dump of the same bytes yields 272.</summary>
        private static readonly ushort[] UIEventsBCON =
            { 1, 15, 4, 5, 2, 6, 7, 8, 9, 10, 11, 12, 13, 14 };

        internal static bool Check(TS1GameScreen game, out string diagnostics)
        {
            var failures = new List<string>();
            Action<bool, string> require = (ok, label) => { if (!ok) failures.Add(label); };
            bool useWorld = VM.UseWorld, globalTS1 = VM.GlobTS1;
            var gd = GameFacade.GraphicsDevice;
            var targets = gd.GetRenderTargets(); var viewport = gd.Viewport;
            var blend = gd.BlendState; var depth = gd.DepthStencilState;
            var rasterizer = gd.RasterizerState; var sampler = gd.SamplerStates[0];
            var oldTexture = gd.Textures[0];
            UILotControl control = null;
            UIOriginalPeopleChrome chrome = null;
            var savedChrome = game.Frontend?.MainPanel?.PeopleChrome;
            UIRelationshipSubpanel relPanel = null;
            UISubpanel savedSubPanel = null;
            var root = new UIContainer { ScaleX = UIScreen.Current.ScaleX, ScaleY = UIScreen.Current.ScaleY };
            var output = Path.Combine(FSO.Common.FSOEnvironment.UserDir, "ui-audit", "r245");
            try
            {
                Directory.CreateDirectory(output);

                // ===== isolated unticked VM + the real tutorial owner =====
                VM.UseWorld = false;
                var context = new VMContext(null);
                var vm = new VM(context, new VMServerDriver(null), null)
                    { PlatformState = new VMTS1LotState(), TS1 = true };
                vm.Init();   // allocates simulator GlobalState (38 words) —
                             // the poll mirror writes global 12 through it
                context.RoomInfo = new[] { new VMRoomInfo { Entities = new List<VMEntity>() } };
                var definition = Content.Get().WorldObjects.Get(0xc3249a1d);
                if (definition == null) throw new Exception("Original tutorial object missing");
                var owner = new VMGameObject(definition, null);
                owner.Thread = new VMThread(context, owner, 4);
                vm.AddEntity(owner);
                context.SetTutorialObject(owner);

                // ===== (a) byte/IFF contract, from the real owner resource =====
                var bcon = definition.Resource.Get<BCON>(4097);
                require(bcon != null && bcon.ChunkLabel == "ui events", "ui-events-chunk-label");
                require(bcon != null && bcon.Constants.SequenceEqual(UIEventsBCON), "ui-events-codes");
                if (owner.TreeByName == null) owner.FetchTreeByName(context);
                require(owner.TreeByName != null && owner.TreeByName.ContainsKey("query wait event")
                    && owner.TreeByName.ContainsKey("got wait event"), "owner-named-trees");

                // ===== bind the real sink (vm_OnDialog route, 241 pattern) =====
                control = new UILotControl(vm, game.LotControl.World);
                root.Add(control);
                var operand = new VMDialogOperand { Type = VMDialogType.Sims1Tutorial,
                    Flags = VMDialogFlags.Continue | VMDialogFlags.NewEngageContinue };
                var info = new VMDialogInfo { Caller = owner, Operand = operand, DialogID = 24500,
                    Title = "highlight probe", Message = "highlight probe", Block = false };
                vm.SignalDialog(info);   // vm_OnDialog -> BindTutorialContext mounts presenter + highlight
                var highlight = control.TutorialHighlight;
                require(control.TutorialPresenter != null && highlight != null, "highlight-mounted");
                var boundDialog = control.TutorialPresenter?.CurrentDialog;
                if (boundDialog != null)
                {
                    control.TutorialPresenter.Forget(boundDialog);
                    UIScreen.RemoveDialog(boundDialog);
                }

                // ===== synthetic registry probe (deterministic geometry) =====
                var probe = new ProbeButton { Position = new Vector2(100, 200) };
                root.Add(probe);
                TutorialControlMap.Register(probe, 7450, false, "battery probe button 120x40");
                // a second RESOLVABLE target for the native off-law (P2 #2).
                var probe2 = new ProbeButton { Position = new Vector2(600, 100) };
                root.Add(probe2);
                TutorialControlMap.Register(probe2, 7451, false, "battery probe button 2 120x40");

                // ===== (b) flash law: anchor math, 50x50, lot-button override =====
                Rectangle probeRect;
                require(TutorialControlMap.TryResolveRect(game, 7450, out probeRect)
                    && probeRect == new Rectangle(100, 200, 120, 40), "probe-registered-rect");
                require(highlight.FlashControl(game, 7450), "flash-control-shows");
                require(highlight.AnchorRect == new Rectangle(135, 147, 50, 50),
                    "anchor-math-trunc-x-plus-y-minus-53");   // x=100+(120-50)/2, y=200-50-3
                require(highlight.Visible, "flash-visible");
                // cWinLotBtn override: +50px down after the base anchor.
                require(highlight.Flash(new Rectangle(100, 200, 120, 40), probe, true)
                    && highlight.AnchorRect == new Rectangle(135, 197, 50, 50), "lotbtn-plus-50-shift");
                highlight.Clear();
                require(!highlight.Visible, "clear-hides");
                // an unregistered id is a native failure: no highlight change.
                require(highlight.FlashControl(game, 7450), "rearm-for-failure-case");
                require(!highlight.FlashControl(game, 424242)
                    && highlight.Visible && highlight.AnchorRect == new Rectangle(135, 147, 50, 50),
                    "unregistered-id-no-change");
                // an invisible target hides the highlight (HiliteForTutorial law).
                // The flash helpers report false for the invisible-hide branch
                // (it is the documented "HideWindow(); return" path), so the
                // law asserts BOTH the false return and the hidden window.
                probe.Visible = false;
                require(!highlight.FlashControl(game, 7450) && !highlight.Visible,
                    "invisible-target-hides");
                probe.Visible = true;

                // ===== (c) flash cadence: 167ms ping-pong, never auto-hides =====
                long time = 5000;
                highlight.Clock = () => time;
                highlight.FlashControl(game, 7450);
                require(highlight.Phase == 0 && highlight.Direction == 1, "show-resets-phase");
                var phaseCaptures = new List<Color[]>();
                int pw = (int)Math.Ceiling(UIScreen.Current.ScreenWidth * root.ScaleX);
                int ph = (int)Math.Ceiling(UIScreen.Current.ScreenHeight * root.ScaleY);
                using (var target = new RenderTarget2D(gd, pw, ph, false, SurfaceFormat.Color, DepthFormat.None))
                using (var batch = new UISpriteBatch(gd, 0))
                {
                    Action<string> capture = name =>
                    {
                        Prime(root);
                        gd.SetRenderTarget(target); gd.Clear(new Color(45, 65, 45));
                        batch.UIBegin(BlendState.AlphaBlend, SpriteSortMode.Immediate);
                        highlight.Draw(batch);
                        batch.End(); gd.SetRenderTargets(targets);
                        var pixels = new Color[pw * ph]; target.GetData(pixels);
                        phaseCaptures.Add(pixels);
                        using (var stream = File.Create(Path.Combine(output, name + ".png")))
                            target.SaveAsPng(stream, pw, ph);
                    };

                    capture("flash-phase0");
                    time += 166; highlight.Advance();
                    require(highlight.Phase == 0, "phase-holds-under-167ms");
                    time += 1; highlight.Advance();
                    require(highlight.Phase == 1, "phase-steps-at-167ms");
                    capture("flash-phase1");
                    time += 167; highlight.Advance();
                    require(highlight.Phase == 2 && highlight.Direction == 1, "phase-reaches-2");
                    capture("flash-phase2");
                    time += 167; highlight.Advance();
                    require(highlight.Phase == 1 && highlight.Direction == -1, "pingpong-down");
                    time += 167; highlight.Advance();
                    require(highlight.Phase == 0 && highlight.Direction == -1, "pingpong-floor");
                    time += 167; highlight.Advance();
                    require(highlight.Phase == 1 && highlight.Direction == 1, "pingpong-up-again");
                    require(highlight.Visible, "animation-never-auto-hides");

                    // The three art frames are distinct: pairwise pixel diffs.
                    require(DiffCount(phaseCaptures[0], phaseCaptures[1]) > 100
                        && DiffCount(phaseCaptures[1], phaseCaptures[2]) > 100
                        && DiffCount(phaseCaptures[0], phaseCaptures[2]) > 100, "tuthigh-frames-distinct");

                    // Per-pixel: keyed art only inside the anchor rect (2px bleed margin
                    // absorbs fractional-DPI sampling), something visible inside.
                    var anchor = highlight.AnchorRect;
                    int left = (int)(anchor.Left * root.ScaleX) - 2, top = (int)(anchor.Top * root.ScaleY) - 2;
                    int right = (int)Math.Ceiling(anchor.Right * root.ScaleX) + 2,
                        bottom = (int)Math.Ceiling(anchor.Bottom * root.ScaleY) + 2;
                    var pixelsNow = phaseCaptures[0];
                    int inside = 0, outside = 0;
                    for (int y = 0; y < ph; y++)
                        for (int x = 0; x < pw; x++)
                        {
                            if (pixelsNow[y * pw + x] == new Color(45, 65, 45)) continue;
                            if (x >= left && x < right && y >= top && y < bottom) inside++;
                            else outside++;
                        }
                    require(inside > 500 && outside == 0,
                        "tuthigh-pixels-contained(" + inside + "," + outside + ")");
                    // Magenta-key fingerprint: the TS1 UI art pipeline
                    // (TextureCodec's mask family FF00FF/FE02FE/FF01FF) keys
                    // the strip at LOAD, so the highlight's own
                    // ApplyMagentaKey pass is a no-op (KeyedPixels == 0). The
                    // fingerprint is the key's FOOTPRINT: the loaded source
                    // must have exactly the 5892 mask-family pixels of
                    // cpanel\TutHigh.bmp transparent (raw BMP census: 5892 of
                    // 7500 — verified against the extracted art).
                    var keyedSource =
                        UIOriginal.EnsureResolvedByID(UIOriginalTutorialHighlight.ArtID)?
                            .Get(GameFacade.GraphicsDevice);
                    int transparent = -1, srcPixels = -1;
                    if (keyedSource != null)
                    {
                        var srcPx = new Color[keyedSource.Width * keyedSource.Height];
                        keyedSource.GetData(srcPx);
                        srcPixels = srcPx.Length;
                        transparent = 0;
                        for (int i = 0; i < srcPx.Length; i++)
                            if (srcPx[i].A == 0) transparent++;
                    }
                    require(highlight.KeyedPixels == 0 && transparent == 5892 && srcPixels == 7500,
                        "magenta-key-5892-of-7500(keyed=" + highlight.KeyedPixels +
                        ",transparent=" + transparent + ",px=" + srcPixels + ")");
                }

                // hover resolution over the registry (event 8's UI source).
                var hoverState = new UpdateState
                {
                    MouseState = new MouseState((int)(105 * root.ScaleX), (int)(205 * root.ScaleY),
                        0, ButtonState.Released, ButtonState.Released, ButtonState.Released,
                        ButtonState.Released, ButtonState.Released)
                };
                require(TutorialControlMap.ResolveHoverImageId(game, hoverState) == 7450,
                    "hover-resolves-registered");

                // ===== (d) primitive 34 sink paths through the real VM signal =====
                vm.SignalTutorialUIEffect(0, 7450, true);
                require(highlight.Visible && highlight.AnchorRect == new Rectangle(135, 147, 50, 50),
                    "ui-effect-flash-control");
                vm.SignalTutorialUIEffect(0, 999, true);
                require(highlight.Visible && highlight.AnchorRect == new Rectangle(135, 147, 50, 50),
                    "ui-effect-failure-no-change");
                vm.SignalTutorialUIEffect(0, 999, false);
                require(highlight.Visible, "ui-effect-off-unresolvable-keeps");
                // Native off-law (P2 #2): off for ANOTHER RESOLVABLE target
                // clears the one highlighter regardless of what is flashed.
                vm.SignalTutorialUIEffect(0, 7451, false);
                require(!highlight.Visible, "ui-effect-off-other-resolvable-clears");
                vm.SignalTutorialUIEffect(0, 7450, true);
                require(highlight.Visible && highlight.AnchorRect == new Rectangle(135, 147, 50, 50),
                    "ui-effect-off-reflash-anchors");
                vm.SignalTutorialUIEffect(0, 7450, false);
                require(!highlight.Visible, "ui-effect-off-clears");

                // sub-op 1: portrait identity via the people chrome webcam.
                chrome = new UIOriginalPeopleChrome(game) { Position = new Vector2(300, 400) };
                root.Add(chrome);
                TutorialControlMap.RegisterPeopleChrome(chrome);
                VMAvatar avatar = null;
                try { avatar = new VMAvatar(definition); }
                catch (Exception ex) { failures.Add("probe-avatar-ctor " + ex.GetType().Name); }
                if (avatar != null)
                {
                    avatar.PersistID = 4242;
                    chrome.Webcams[0].Avatar = avatar;
                    vm.SignalTutorialUIEffect(1, 4242, true);
                    require(highlight.Visible
                        && highlight.AnchorRect == new Rectangle(307, 352, 50, 50),
                        "ui-effect-flash-person-panel");
                    vm.SignalTutorialUIEffect(1, 999999, true);
                    require(highlight.AnchorRect == new Rectangle(307, 352, 50, 50),
                        "person-panel-miss-no-change");
                    vm.SignalTutorialUIEffect(1, 4242, false);
                    require(!highlight.Visible, "person-panel-off-clears");
                }

                // sub-op 2: neighbor id gates EXISTENCE only; the first
                // relationship panel window itself is flashed (skeptic C4).
                var neighbours = Content.Get()?.Neighborhood?.Neighbors;
                short relId = 0;
                if (neighbours != null)
                    foreach (var e in neighbours.Entries)
                        if (e != null && neighbours.NeighbourByID.ContainsKey(e.NeighbourID))
                        { relId = e.NeighbourID; break; }
                require(relId > 0, "neighborhood-has-neighbors");
                var mainPanel = game.Frontend?.MainPanel;
                require(mainPanel != null, "live-main-panel");
                relPanel = new UIRelationshipSubpanel(game) { Position = new Vector2(250, 300) };
                root.Add(relPanel);
                savedSubPanel = mainPanel.SubPanel;
                mainPanel.SubPanel = relPanel;
                try
                {
                    vm.SignalTutorialUIEffect(2, relId, true);
                    // Independently derived expectation (P2 #8): this fixture
                    // placed the panel at (250,300) on the battery root, so
                    // its logical top-left is known without the resolver; the
                    // only element data read is its own Size. The ANCHOR LAW
                    // is written out literally: x = 250 + (W-50)/2,
                    // y = 300 - 50 - 3, and the window is 50x50. No
                    // TutorialControlMap.LogicalRect call.
                    require((int)relPanel.Position.X == 250 && (int)relPanel.Position.Y == 300,
                        "rel-panel-fixture-placement");
                    var relExpected = new Rectangle(250 + ((int)relPanel.Size.X - 50) / 2,
                        300 - 50 - 3, 50, 50);
                    require(highlight.Visible && highlight.AnchorRect == relExpected,
                        "ui-effect-flash-rel-panel");
                    vm.SignalTutorialUIEffect(2, 424242, true);
                    require(highlight.AnchorRect == relExpected, "rel-missing-person-no-change");
                    vm.SignalTutorialUIEffect(2, relId, false);
                    require(!highlight.Visible, "rel-off-clears");
                }
                finally { mainPanel.SubPanel = savedSubPanel; }

                // ===== (f) modal auto-hide + restore; target-destroy clear =====
                highlight.FlashControl(game, 7450);
                highlight.SetModalObscured(true);
                require(!highlight.Visible && highlight.ObscuredByModal, "modal-hides");
                highlight.SetModalObscured(false);
                require(highlight.Visible && highlight.Phase == 0 && highlight.Direction == 1,
                    "modal-restore-fresh-phase");
                root.Remove(probe);   // the flashed target dies
                highlight.Update(new UpdateState());
                require(!highlight.Visible, "target-destroy-clears");

                // ===== (e) poll path: real trees on the real owner mailbox =====
                // (the real 'query wait event' BHAV heartbeats myAttr[7] on every
                // waiting-or-idle poll; the real 'got wait event' BHAV clears
                // myAttr[2] — the flush IS the match observable, exactly once.)
                var poller = vm.TutorialEvents;
                var ui = poller.State;
                poller.MirrorLastButton(2011);
                require(vm.GetGlobalValue(12) == 2011 && ui.LastButtonClickImageId == 2011,
                    "mirror-global-12-and-state");
                poller.MirrorLastButton(0);
                require(vm.GetGlobalValue(12) == 0, "mirror-global-12-clears");

                int hb = owner.GetAttribute(7);
                owner.SetAttribute(2, 4); owner.SetAttribute(3, 2011);
                poller.MirrorLastButton(2011);
                poller.Poll();
                require(owner.GetAttribute(2) == 0 && owner.GetAttribute(7) == hb + 1,
                    "poll-match-flushes-mailbox-heartbeat");
                int hb2 = owner.GetAttribute(7);
                poller.Poll();   // mailbox empty: query still runs, nothing fires
                require(owner.GetAttribute(7) == hb2 + 1 && owner.GetAttribute(2) == 0,
                    "poll-idle-heartbeat-only");

                // non-match while waiting: mailbox persists (re-polled each tick).
                owner.SetAttribute(2, 4); owner.SetAttribute(3, 2011);
                poller.MirrorLastButton(2008);
                poller.Poll();
                require(owner.GetAttribute(2) == 4 && owner.GetAttribute(7) == hb2 + 2,
                    "poll-waiting-persists-on-mismatch");
                poller.MirrorLastButton(2011);
                poller.Poll();
                require(owner.GetAttribute(2) == 0, "poll-delayed-match-flushes");

                // ev 9 vs ev 11 page captions (skeptic C1 regression) + ev 2/13/15/14.
                ui.PersonPanelPage = "rel";
                owner.SetAttribute(2, 9); owner.SetAttribute(3, 0);
                poller.Poll();
                require(owner.GetAttribute(2) == 0, "ev9-is-rel");
                owner.SetAttribute(2, 11);
                poller.Poll();
                require(owner.GetAttribute(2) == 11, "ev11-is-not-rel");
                ui.PersonPanelPage = "job";
                poller.Poll();
                require(owner.GetAttribute(2) == 0, "ev11-is-job");
                ui.PersonPanelPage = "motives";
                owner.SetAttribute(2, 2);
                poller.Poll();
                require(owner.GetAttribute(2) == 0, "ev2-is-motives");
                owner.SetAttribute(2, 13);
                poller.Poll();
                require(owner.GetAttribute(2) == 13, "ev13-is-not-motives");
                ui.PersonPanelPage = "skill";
                poller.Poll();
                require(owner.GetAttribute(2) == 0, "ev13-is-skill");
                ui.PersonPanelPage = "per.ity";
                owner.SetAttribute(2, 15);
                poller.Poll();
                require(owner.GetAttribute(2) == 0, "ev15-is-perity");
                ui.PersonPanelPage = null;
                owner.SetAttribute(2, 14);
                poller.Poll();
                require(owner.GetAttribute(2) == 0, "ev14-page-absent");
                ui.PersonPanelPage = "rel";
                owner.SetAttribute(2, 14);
                poller.Poll();
                require(owner.GetAttribute(2) == 14, "ev14-page-present-blocks");

                // ev 1 (CP mode zero), ev 5 (shown portrait), ev 8 (hover).
                ui.CpModeIsZero = false;
                owner.SetAttribute(2, 1);
                poller.Poll();
                require(owner.GetAttribute(2) == 1, "ev1-mode-blocks");
                ui.CpModeIsZero = true;
                poller.Poll();
                require(owner.GetAttribute(2) == 0, "ev1-mode-zero-fires");
                owner.SetAttribute(2, 5); owner.SetAttribute(3, 42);
                ui.ShownPortraitNeighborId = 42;
                poller.Poll();
                require(owner.GetAttribute(2) == 0, "ev5-portrait-fires");
                owner.SetAttribute(2, 8); owner.SetAttribute(3, 2013);
                ui.HoverButtonImageId = -1;
                poller.Poll();
                require(owner.GetAttribute(2) == 8, "ev8-hover-blocks");
                ui.HoverButtonImageId = 2013;
                poller.Poll();
                require(owner.GetAttribute(2) == 0, "ev8-hover-fires");

                // ev 6/7: the shared -1 edge latch (skeptic C7) — first sight
                // caches, a change fires once, and a found poll re-arms it.
                ui.PersonPanelPage = null;
                ui.RotationEdge = 500;
                owner.SetAttribute(2, 6);
                poller.Poll();
                require(owner.GetAttribute(2) == 6, "ev6-first-sight-caches");
                ui.RotationEdge = 640;
                poller.Poll();
                require(owner.GetAttribute(2) == 0, "ev6-change-fires");
                ui.ScrollEdge = 777;
                owner.SetAttribute(2, 7);
                poller.Poll();
                require(owner.GetAttribute(2) == 7, "ev7-first-sight-caches");
                ui.RotationEdge = 777;   // poisoned: ev6 now sees no change either
                owner.SetAttribute(2, 6);
                poller.Poll();
                require(owner.GetAttribute(2) == 6, "shared-latch-poisons-ev6");
                ui.RotationEdge = 778;
                poller.Poll();
                require(owner.GetAttribute(2) == 0, "shared-latch-change-fires");

                // re-entrancy guard (skeptic C3): no query runs while blocked.
                poller.ModalDialogTracked = true;
                require(poller.IsReentrancyBlocked, "guard-reports-blocked");
                int hb3 = owner.GetAttribute(7);
                owner.SetAttribute(2, 4); owner.SetAttribute(3, 2011);
                poller.MirrorLastButton(2011);
                poller.Poll();
                require(owner.GetAttribute(2) == 4 && owner.GetAttribute(7) == hb3,
                    "guard-blocks-poll");
                poller.ModalDialogTracked = false;
                poller.Poll();
                require(owner.GetAttribute(2) == 0 && owner.GetAttribute(7) == hb3 + 1,
                    "guard-release-runs-once");

                // owner gone: the poll returns after the mirror (native guard).
                context.SetTutorialObject(null);
                int hb4 = owner.GetAttribute(7);
                poller.Poll();
                require(owner.GetAttribute(7) == hb4, "no-owner-no-query");
            }
            catch (Exception ex) { failures.Add(ex.ToString()); }
            finally
            {
                // P2 #7: the battery's shared registry/mirror signals can
                // bleed into the REAL lot control's tutorial machinery.
                // Self-heal deterministically: clear the real control's
                // tutorial highlight (its Clear API) and Forget whatever the
                // real presenter is showing, so test-scoped pollution cannot
                // outlive this battery.
                var realControl = game.LotControl;
                if (realControl != null)
                {
                    realControl.TutorialHighlight?.Clear();
                    var liveDialog = realControl.TutorialPresenter?.CurrentDialog;
                    if (liveDialog != null) realControl.TutorialPresenter.Forget(liveDialog);
                }
                if (control != null) root.Remove(control);
                if (chrome != null) root.Remove(chrome);
                if (relPanel != null) root.Remove(relPanel);
                TutorialControlMap.RegisterPeopleChrome(savedChrome);
                VM.UseWorld = useWorld; VM.GlobTS1 = globalTS1;
                gd.SetRenderTargets(targets); gd.Viewport = viewport;
                gd.BlendState = blend; gd.DepthStencilState = depth;
                gd.RasterizerState = rasterizer; gd.SamplerStates[0] = sampler;
                gd.Textures[0] = oldTexture;
            }
            diagnostics = failures.Count == 0
                ? "ui-events BCON+named trees pinned; anchor/lotbtn/clear/invisible laws; 167ms ping-pong GPU captures (3 distinct frames, contained); magenta-key footprint 5892 (loader-keyed); modal hide/restore; target-destroy clear; prim-34 sub-ops 0/1/2 + failure no-ops + native off-law (unresolvable keeps / resolvable clears); poll mailbox flush/heartbeat/exactly-once; ev 9/11 skeptic regression; shared edge latch; re-entrancy guard; hover resolution"
                : string.Join("; ", failures);
            return failures.Count == 0;
        }

        private static int DiffCount(Color[] a, Color[] b)
        {
            int diff = 0;
            for (int i = 0; i < a.Length && i < b.Length; i++)
                if (a[i] != b[i]) diff++;
            return diff;
        }

        private static void Prime(UIElement element)
        {
            element.CalculateMatrix();
            _ = element.BlendColor;
            if (element is UIContainer container)
                foreach (var child in container.GetChildren()) Prime(child);
        }

        /// <summary>A registered click source with deterministic geometry
        /// (the battery's flash target).</summary>
        private sealed class ProbeButton : UIButton
        {
            public ProbeButton() : base(FSO.Common.Utils.TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice)) { }

            public override Vector2 Size => new Vector2(120, 40);
        }
    }
}
