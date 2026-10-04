using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// The original TransformMe form browser (TS1 dialog type 14,
    /// cWinTransformMeDlg), recovered from the PPC binary — ENG-11 decode
    /// (transformme-wizard-decode.md). Native law: 8 tabs (tab index wraps
    /// 0..7 on prev/next); tab → SAnimator::Outfit enum via LookupCostume
    /// keyed off the target's gender (pd[65]==1 female) and child iff
    /// 0 &lt; pd[58] &lt; 18 — adult male {48,58,44,45,39,46,47,40}, adult
    /// female {48,43,44,49,39,50,51,52}, child even tab 53 (boy) / 54 (girl)
    /// and odd tab 60. OK posts (form&lt;&lt;16)|0xFFFE, Cancel |0xFFFF; the
    /// tree's dialog consumes only the branch (TRUE/FALSE) with
    /// Temp0 = the outfit enum on BOTH edges — the pick never feeds any
    /// conversion natively (the wizard commits NOTHING; §C/§F of the decode),
    /// so this port surface is preview + answer only, byte-faithful.
    ///
    /// UI-36 — LIVE VITA PREVIEW. The native previews the target live on
    /// cWinVitaBtnSolo: SetPerson(target) once (0x57e6f4) — a separate
    /// MakeNewOutOfWorldObject twin from the person's own selector (§B) —
    /// then SetOutfit(LookupCostume(tab)) per tab change (0x57e6f8 /
    /// TSOnCommand 0x57d7b0/0x57d800); prev/next are SetOutfit-only and must
    /// NOT re-arm the idle cycle (the R212 law). The port now hosts the real
    /// UIOriginalVitaPreview (the CAS-02 framing-law component) fed the same
    /// way the CAS feeds it (TS1CASScreen: staging VMAvatar + WorldUI hidden
    /// + UIOriginalVitaIdlePlayer driving the pose each frame).
    ///
    /// ENUM→FORM MAPPING (UI-36's load-bearing decode; full evidence in
    /// coordination/evidence/UI-36/vita-preview-implementation.md). The 16
    /// wizard enums are the Makin' Magic CREATURE forms: the animator's
    /// outfit switch lives at runtime 0x3525c4 (outfit halfword lha +1436,
    /// cmplwi 60, jump table via TOC-0x4be0 = data image 0x60c30, 61 case
    /// blocks 0x3525f8..0x355e40); each case copies literal suit strings
    /// from the pool at TOC-0x4bb4 = data image 0x60d24. Case literals
    /// (r2 re-verified): 39 BlueGenie($g), 40 Minotaur, 43 Vampire(F),
    /// 44 Werewolf($g), 45 Headless, 46 Troll, 47 Hunchback, 48 Sorcerer($g),
    /// 49 Witch, 50 Ogress, 51 Crone, 52 Nymph, 58 Vampire(M), 60 child
    /// Skeleton; child day-tabs 53 Goblin(boy)/54 WizardGirl(girl). Each
    /// form = body mesh + body skin + head + handgroup ('$g' gender,
    /// '$h' gesture O/P/C). Every asset is confirmed present in the port's
    /// content reach (ExpansionPack7.far — scanned by TS1Provider's
    /// FAR1Provider — appearances in cmagic*.cmx.bcf, textures BMagic*/
    /// CMagic*/H*O|P|C_*, hand meshes hm/hf/hu in Animation.far).
    /// Disclosed native quirks kept byte-faithful: 49 Witch borrows the
    /// HUNCHBACK hand texture (the native case's own literal), and 44
    /// Werewolf uses MALE hands for both genders. Child forms reuse the
    /// adult hand meshes — the shipped data has no child hand meshes
    /// (Animation.far census: hm/hf/hu × l/r × o/p/c only).
    ///
    /// PREVIEW INSTANCE: an OUT-OF-WORLD twin VMAvatar from the TARGET's
    /// own character GUID (vm.Context.CreateObjectInstance(guid,
    /// OUT_OF_WORLD, NORTH, ghost) — the port's MakeNewOutOfWorldObject
    /// stand-in; TS1 person creation runs InheritNeighbor, so the twin
    /// inherits the target's strings without mutating the real target's
    /// shared STR resource — outfits are assigned as fresh in-memory
    /// VMOutfitReference objects, never via SetSuit). The twin is hidden
    /// from the lot (WorldUI.Visible=false, the CAS staging law) and is
    /// removed in Dismiss()/Removed() via Context.RemoveObjectInstance.
    /// Fallback (disclosed): if the twin cannot be built (no thread
    /// context, pet target, fixture objects) the REAL target renders
    /// render-only in its current outfit — the window never re-dresses
    /// the real target. The text fallback (PreviewLine) is gone; TabLine
    /// stays. The window grew 420x240 → 560x400 to host the 100x220 Vita
    /// rectangle at native scale (the native window is 600x800; the 420x240
    /// size was the text-only degradation's compaction). The Vita surface's
    /// static Backdrop is snapshotted from the CAS's own CreateACharBack
    /// Vita rect so both hosts share one consistent backdrop (the component
    /// caches its backdrop statically — first mount wins process-wide).
    /// </summary>
    public class UIOriginalTransformMeDialog : UIContainer
    {
        public const int WindowW = 560, WindowH = 400;
        public override Vector2 Size { get => new Vector2(WindowW, WindowH); set { } }
        public override Rectangle GetBounds() => new Rectangle(0, 0, WindowW, WindowH);
        public const int MsgX = 26, MsgY = 20, MsgW = 508, MsgLines = 3;
        // the 100x220 Vita rectangle at native scale (CAS-02 framing law),
        // flanked by the prev/next arrows (native skins 300/301)
        public const int VitaX = 230, VitaY = 92;
        public const int PrevX = 95, NextX = 365, ArrowY = 186, ArrowW = 100;
        public const int OkX = 155, CancelX = 305, ButtonY = 352, ButtonW = 100;
        private const float VitaFacing = MathHelper.PiOver2; // the CAS preview facing

        // probe surface (UI-35 gate)
        public static int DialogsMounted, Confirms, Cancels;
        // probe cleanup support (review-2 P2-1): every live mount, removed by Dismiss
        public static readonly List<UIOriginalTransformMeDialog> LiveMounted = new List<UIOriginalTransformMeDialog>();
        public static int LastEnum = -1;
        public static string LastClass = "";
        public static UIOriginalTransformMeDialog LastMounted;

        /// <summary>null = cancelled; otherwise the picked SAnimator outfit enum.</summary>
        public event Action<int> OnResult;

        private readonly UIBigButton PrevButton, NextButton, OkButton, CancelButton;
        private readonly UIOriginalText OkLabel, CancelLabel, TabLine;
        private readonly Vector2 OkLabelOrigin, CancelLabelOrigin;
        private readonly OriginalGlyphFont Font;
        private readonly bool Female, Child;
        private readonly string TargetName;
        private int Tab;

        // UI-36 live preview state (all paths idempotent-null after release)
        private CAS.UIOriginalVitaPreview VitaSurface;
        private FSO.SimAntics.VMAvatar PreviewAvatar;   // the OOW twin (outfit-switched)
        private FSO.SimAntics.VMContext PreviewContext; // its context (ghosts carry no Thread)
        private UIOriginalVitaIdlePlayer VitaIdle;

        /// <summary>
        /// One native wizard form: the four suit strings the animator's
        /// UpdateOutfitAndHands case copies (body mesh, body skin, head,
        /// head skin, handgroup pattern). '$g' = the target's gender M/F;
        /// '$h' in the hand pattern = the gesture O( idle)/P(oint)/C(losed).
        /// Literals verbatim from the case blocks (see class doc).
        /// </summary>
        public sealed class NativeForm
        {
            public readonly int Outfit;
            public readonly string Body, Skin, Head, HeadSkin, Hands;
            public NativeForm(int outfit, string body, string skin, string head, string headSkin, string hands)
            { Outfit = outfit; Body = body; Skin = skin; Head = head; HeadSkin = headSkin; Hands = hands; }
            /// <summary>The hand MESH gender: literal[1] M/F/U → the hm/hf/hu meshes.</summary>
            public char HandMeshCode
            {
                get
                {
                    switch (Hands[1])
                    {
                        case 'M': return 'm';
                        case 'F': return 'f';
                        default: return 'u';
                    }
                }
            }
        }

        /// <summary>The 16 wizard enums → their native form literals, in
        /// enum order. Evidence: the outfit switch's case blocks (class doc).</summary>
        private static readonly NativeForm[] Forms = new NativeForm[]
        {
            new NativeForm(39, "Magic$gAFit_BlueGenie", "BMagic$gAFit_BlueGenie", "CMagic$gAFit_BlueGenie", "CMagic$gA_BlueGenie", "H$gA$h_BlueGenie"),
            new NativeForm(40, "MagicMAFit_Minotaur", "BMagicMAFit_Minotaur", "CMagicMAFit_Minotaur", "CMagicMAFit_Minotaur", "HMA$h_Minotaur"),
            new NativeForm(43, "MagicFAFit_Vampire", "MagicFAFit_Vampire", "CMagicFAFit_Vampire", "CMagicFAFit_Vampire", "HFA$h_Vampire"),
            new NativeForm(44, "Magic$gAFit_Werewolf", "Magic$gAFit_Werewolf", "CMagic$gAFit_Werewolf", "CMagic$gAFit_Werewolf", "HMA$h_Werewolf"),
            new NativeForm(45, "MagicMAFit_Headless", "MagicMAFit_Headless", "CMagicMA_Headless", "CMagicMA_Headless", "HUA$h_Headless"),
            new NativeForm(46, "MagicMAFat_Troll", "MagicMAFat_Troll", "CMagicMAFat_Troll", "CMagicMAFat_Troll", "HUA$h_Troll"),
            new NativeForm(47, "MagicMAFat_Hunchback", "MagicMAFat_Hunchback", "CMagicMAFat_Hunchback", "CMagicMAFat_Hunchback", "HUA$h_Hunchback"),
            new NativeForm(48, "Magic$gAFit_Sorcerer", "Magic$gAFit_Sorcerer", "CMagic$gAFit_Sorcerer", "CMagic$gAFit_Sorcerer", "HMA$h_Sorcerer"),
            new NativeForm(49, "MagicFAFit_Witch", "MagicFAFit_Witch", "CMagicFAFit_Witch", "CMagicFAFit_Witch", "HUA$h_Hunchback"), // native literal: Witch borrows Hunchback hands
            new NativeForm(50, "MagicFAFat_Ogress", "BMagicFAFat_Ogress", "CMagicFAFat_Ogress", "CMagicFAFat_Ogress", "HFA$h_Ogress"),
            new NativeForm(51, "MagicFASkn_Crone", "BMagicFASkn_Crone", "CMagicFASkn_Crone", "CMagicFASkn_Crone", "HFA$h_Crone"),
            new NativeForm(52, "MagicFASkn_Nymph", "BMagicFASkn_Nymph", "CMagicFASkn_Nymph", "CMagicFASkn_Nymph", "HUA$h_Nymph"),
            new NativeForm(53, "MagicMCChd_Goblin", "MagicMCChd_Goblin", "CMagicMCChd_Goblin", "CMagicMCChd_Goblin", "HUC$h_Goblin"),
            new NativeForm(54, "MagicFCChd_WizardGirl", "MagicFCChd_WizardGirl", "CMagicFC_WizardGirl", "CMagicFC_WizardGirl", "HFC$h_WizardGirl"),
            new NativeForm(58, "MagicMASkn_Vampire", "MagicMASkn_Vampire", "CMagicMASkn_Vampire", "CMagicMASkn_Vampire", "HMA$h_Vampire"),
            new NativeForm(60, "MagicMCChd_Skeleton", "MagicMCChd_Skeleton", "CMagicMCChd_Skeleton", "CMagicMCChd_Skeleton", "GUA$h_SkeletonChild"),
        };

        /// <summary>The native form for a wizard enum, or null (every §B
        /// value is mapped; the null arm is an unreachable guard).</summary>
        public static NativeForm FormFor(int outfitEnum)
        {
            return Forms.FirstOrDefault(f => f.Outfit == outfitEnum);
        }

        /// <summary>
        /// LookupCostume (decode §B) as a pure function — tab 0..7 to the
        /// SAnimator::Outfit enum, keyed off the target class. Probe-pinned.
        /// </summary>
        public static int TabOutfit(int tab, bool female, bool child)
        {
            if (child)
            {
                // signed-parity idiom at 0x57d518-0x57d560 (r2-verified):
                // even tab -> boy 53 / girl 54; odd tab -> 60
                if ((tab & 1) != 0) return 60;
                return female ? 54 : 53;
            }
            var table = female
                ? new[] { 48, 43, 44, 49, 39, 50, 51, 52 }   // TOC[-17044], 0x57d5f0+8i
                : new[] { 48, 58, 44, 45, 39, 46, 47, 40 };  // TOC[-17040], 0x57d588+8i
            if (tab < 0 || tab > 7) return 0;                // cmplwi index,7; >7 -> 0
            return table[tab];
        }

        public UIOriginalTransformMeDialog(string title, string message, string okCaption,
            string cancelCaption, FSO.SimAntics.VMAvatar target)
        {
            Size = new Vector2(WindowW, WindowH);
            UpdatePosition();
            Font = OriginalGlyphFont.LoadByIndex(11, GameFacade.GraphicsDevice);

            // class from the REAL target (the native's charm-path StackElem quirk
            // is deliberately NOT replicated — decode §E, port wiring note 5)
            Female = target != null && (target.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.Gender) & 1) == 1;
            var age = (target != null) ? target.GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.PersonsAge) : (short)0;
            Child = age > 0 && age < 18;
            TargetName = target?.Name ?? "";
            LastClass = (Child ? "child " : "adult ") + (Female ? "female" : "male");

            if (!string.IsNullOrEmpty(title))
            {
                var titleLabel = new UIOriginalText(title, Font)
                { Position = new Vector2(MsgX, 6), Color = new Color(195, 205, 205) };
                Add(titleLabel);
            }
            int y = MsgY;
            foreach (var line in Wrap(message ?? "", MsgW, MsgLines))
            {
                var label = new UIOriginalText(line, Font)
                { Position = new Vector2(MsgX, y), Color = new Color(195, 205, 205) };
                Add(label);
                y += Font.LineHeight + 2;
            }

            PrevButton = new UIBigButton(false) { Caption = "", Width = ArrowW, Position = new Vector2(PrevX, ArrowY) };
            NextButton = new UIBigButton(false) { Caption = "", Width = ArrowW, Position = new Vector2(NextX, ArrowY) };
            OkButton = new UIBigButton(false) { Caption = "", Width = ButtonW, Position = new Vector2(OkX, ButtonY) };
            CancelButton = new UIBigButton(false) { Caption = "", Width = ButtonW, Position = new Vector2(CancelX, ButtonY) };
            Add(PrevButton); Add(NextButton); Add(OkButton); Add(CancelButton);

            ArrowLabel("<", PrevButton);
            ArrowLabel(">", NextButton);
            if (string.IsNullOrEmpty(okCaption)) okCaption = "OK";           // native: GetButtonLabel(3)
            if (string.IsNullOrEmpty(cancelCaption)) cancelCaption = "Cancel"; // UIText 273 row 3
            OkLabel = ButtonLabel(okCaption, OkButton);
            CancelLabel = ButtonLabel(cancelCaption, CancelButton);
            OkLabelOrigin = OkLabel.Position;
            CancelLabelOrigin = CancelLabel.Position;

            TabLine = new UIOriginalText("", Font)
            { Position = new Vector2(VitaX + 8, VitaY + 226), Color = new Color(195, 205, 205) }; // under the Vita window (the arrows now flank the preview, not the label)
            Add(TabLine);

            // UI-36: the live Vita preview — the native Init's SetPerson +
            // LookupCostume(0) → SetOutfit pair, ported onto the CAS-02
            // component. Every step is failure-tolerant: without a mountable
            // twin the window degrades to TabLine-only (never crashes).
            MountPreview(target);

            PrevButton.OnButtonClick += _ => { Tab = (Tab <= 0) ? 7 : Tab - 1; Refresh(); }; // 0x57d7b0
            NextButton.OnButtonClick += _ => { Tab = (Tab >= 7) ? 0 : Tab + 1; Refresh(); }; // 0x57d800
            OkButton.OnButtonClick += _ => Submit();
            CancelButton.OnButtonClick += _ => Cancel();

            Refresh();
            LastMounted = this;
            DialogsMounted++;
            LiveMounted.Add(this);
        }

        private void Dismiss()
        {
            ReleasePreview();
            UIScreen.RemoveDialog(this);
            LiveMounted.Remove(this);
        }

        /// <summary>UI-36 teardown — runs on Dismiss AND on any external
        /// removal path (Removed fires from UIScreen.RemoveDialog, e.g. the
        /// autotest's leak sweep); idempotent.</summary>
        public override void Removed()
        {
            ReleasePreview();
            base.Removed();
        }

        private void Refresh()
        {
            var outfit = TabOutfit(Tab, Female, Child);
            TabLine.Text = "Form " + (Tab + 1) + " of 8";
            ApplyForm(outfit); // the native per-tab SetOutfit (0x57e6f8)
        }

        // ================= UI-36 live Vita preview =================

        /// <summary>
        /// Builds the OOW twin from the target's own character GUID (the
        /// port's MakeNewOutOfWorldObject stand-in — TS1 person creation
        /// runs InheritNeighbor so the twin is the target's look-alike with
        /// its OWN in-memory outfit references), hosts the Vita surface and
        /// arms the idle player. Fallbacks, disclosed: no twin → the REAL
        /// target renders render-only (its current outfit — the window
        /// never re-dresses the real target); no surface → TabLine only.
        /// </summary>
        private void MountPreview(FSO.SimAntics.VMAvatar target)
        {
            try
            {
                var ctx = target?.Thread?.Context;
                if (ctx != null && !target.IsPet)
                {
                    var group = ctx.CreateObjectInstance(target.Object.OBJ.GUID,
                        FSO.LotView.Model.LotTilePos.OUT_OF_WORLD, FSO.LotView.Model.Direction.NORTH, true);
                    var twin = group?.BaseObject as FSO.SimAntics.VMAvatar;
                    if (twin != null)
                    {
                        PreviewAvatar = twin;
                        PreviewContext = ctx;
                        // the CAS staging law: the twin belongs only to the
                        // Vita surface — the lot view must not reveal it
                        twin.WorldUI.Visible = false;
                    }
                }
                if (PreviewAvatar == null && target != null)
                {
                    // disclosed fallback: render-only bind of the real target
                    // (its CURRENT outfit; ApplyForm is inert without
                    // PreviewContext, so the real sim is never re-dressed)
                    PreviewAvatar = target;
                    PreviewContext = null;
                }
                if (PreviewAvatar == null) return;

                var surface = BuildVitaSurface();
                if (surface != null)
                {
                    VitaSurface = surface;
                    Add(VitaSurface);
                    VitaSurface.Person = PreviewAvatar;
                    VitaSurface.Visible = true;
                }
                // SetPerson once (native 0x57e6f4): the idle cycle arms here
                // and only here — tab changes are SetOutfit-only (R212 law).
                // TWIN ONLY: the player writes RadianDirection every frame,
                // which the fallback's REAL target would mirror on the lot.
                if (PreviewContext != null)
                    VitaIdle = new UIOriginalVitaIdlePlayer(PreviewAvatar, Child, Female);
            }
            catch
            {
                // never let the preview break the picker (autotest fixture
                // targets are not always neighborhood characters) — full
                // teardown so a half-built twin cannot leak into the blueprint
                ReleasePreview();
            }
        }

        /// <summary>
        /// The CAS-02 component with the CAS's own Vita backdrop rect: the
        /// component snapshots its backdrop STATICALLY (first mount wins
        /// process-wide), so feeding it the CAS's CreateACharBack slice
        /// keeps both hosts on one consistent backdrop. Degenerate fallback
        /// (CAS art unavailable): a 100x220 tile of the dialog chrome's own
        /// PopupInfoTiles interior.
        /// </summary>
        private CAS.UIOriginalVitaPreview BuildVitaSurface()
        {
            var gd = GameFacade.GraphicsDevice;
            try
            {
                var casBack = UIOriginal.EnsureResolved("nbhd\\CreateACharBack.BMP")?.Get(gd);
                if (casBack != null && casBack.Width == CAS.UIOriginalDesignChar.ORIG_W
                    && casBack.Height == CAS.UIOriginalDesignChar.ORIG_H)
                {
                    var s = new CAS.UIOriginalVitaPreview(casBack, CAS.UIOriginalDesignChar.VITA_RECT);
                    s.Position = new Vector2(VitaX, VitaY);
                    return s;
                }
                var tiles = UIOriginalDialogChrome.GetPictureTiles();
                var backdrop = new Texture2D(gd, 100, 220);
                var cell = new Color[144];
                if (tiles != null)
                {
                    tiles.GetData(0, new Rectangle(12, 12, 12, 12), cell, 0, 144);
                    var px = new Color[100 * 220];
                    for (var yy = 0; yy < 220; yy++)
                        for (var xx = 0; xx < 100; xx++)
                            px[yy * 100 + xx] = cell[((yy % 12) * 12) + (xx % 12)];
                    backdrop.SetData(px);
                }
                else backdrop.SetData(Enumerable.Repeat(UIOriginalDialogChrome.FillColor, 100 * 220).ToArray());
                var surf = new CAS.UIOriginalVitaPreview(backdrop, new Rectangle(0, 0, 100, 220));
                surf.Position = new Vector2(VitaX, VitaY);
                return surf;
            }
            catch { return null; }
        }

        /// <summary>
        /// Dresses the preview twin in a form's four suits — the port of the
        /// native UpdateOutfitAndHands case: body (mesh+skin+handgroup) via
        /// BodyOutfit, head via HeadOutfit, hands carried on the body Outfit
        /// exactly like the CAS's SetBody idiom (TS1HandSet LiteralHandgroup,
        /// "h{m|f|u}{l|r}{o|p|c}.apr" meshes from Animation.far, the native
        /// hand texture pattern with $h → O/P/C). No STR writes, no
        /// Neighborhood.AvatarChanged — the target's character resource is
        /// never mutated.
        /// </summary>
        private void ApplyForm(int outfitEnum)
        {
            var avatar = PreviewAvatar;
            if (avatar == null || PreviewContext == null) return; // render-only fallback: the real target keeps its own outfit
            var form = FormFor(outfitEnum);
            if (form == null) return; // unreachable: every §B enum is mapped
            var g = Female ? "F" : "M";
            var mesh = form.HandMeshCode;
            string HandTex(string gesture) => form.Hands.Replace("$g", g).Replace("$h", gesture);

            var body = new FSO.Vitaboy.Outfit();
            body.TS1AppearanceID = form.Body.Replace("$g", g) + ".apr";
            body.TS1TextureID = form.Skin.Replace("$g", g);
            body.LiteralHandgroup = new FSO.Vitaboy.HandGroup
            {
                TS1HandSet = true,
                LightSkin = new FSO.Vitaboy.HandSet
                {
                    LeftHand = new FSO.Vitaboy.Hand
                    {
                        Idle = new FSO.Vitaboy.Gesture { Name = "h" + mesh + "lo.apr", TexName = HandTex("O") },
                        Pointing = new FSO.Vitaboy.Gesture { Name = "h" + mesh + "lp.apr", TexName = HandTex("P") },
                        Fist = new FSO.Vitaboy.Gesture { Name = "h" + mesh + "lc.apr", TexName = HandTex("C") }
                    },
                    RightHand = new FSO.Vitaboy.Hand
                    {
                        Idle = new FSO.Vitaboy.Gesture { Name = "h" + mesh + "ro.apr", TexName = HandTex("O") },
                        Pointing = new FSO.Vitaboy.Gesture { Name = "h" + mesh + "rp.apr", TexName = HandTex("P") },
                        Fist = new FSO.Vitaboy.Gesture { Name = "h" + mesh + "rc.apr", TexName = HandTex("C") }
                    }
                }
            };
            avatar.BodyOutfit = new FSO.SimAntics.Model.VMOutfitReference(body);

            var head = new FSO.Vitaboy.Outfit();
            head.TS1AppearanceID = form.Head.Replace("$g", g) + ".apr";
            head.TS1TextureID = form.HeadSkin.Replace("$g", g);
            avatar.HeadOutfit = new FSO.SimAntics.Model.VMOutfitReference(head);
        }

        /// <summary>Idempotent preview teardown: stops the idle player,
        /// unbinds + drops the surface (its own Removed() disposes the
        /// render targets) and removes the OOW twin from the lot VM
        /// (RemoveObjectInstance handles the ghost + blueprint paths).</summary>
        private void ReleasePreview()
        {
            VitaIdle = null;
            if (VitaSurface != null)
            {
                VitaSurface.Person = null;
                VitaSurface = null;
            }
            var twin = PreviewAvatar;
            var ctx = PreviewContext;
            PreviewAvatar = null;
            PreviewContext = null;
            if (twin != null && ctx != null)
            {
                try { twin.WorldUI.Visible = false; } catch { }
                try { ctx.RemoveObjectInstance(twin); } catch { }
            }
        }

        private UIOriginalText ArrowLabel(string caption, UIBigButton button)
        {
            var text = new UIOriginalText(caption, Font)
            { Position = button.Position + new Vector2((ArrowW - Font.Measure(caption)) / 2, Font.ButtonCaptionY(33)),
                Color = new Color(195, 205, 205) };
            Add(text);
            return text;
        }

        private UIOriginalText ButtonLabel(string caption, UIBigButton button)
        {
            var text = new UIOriginalText(caption, Font)
            { Position = button.Position + new Vector2((ButtonW - Font.Measure(caption)) / 2, Font.ButtonCaptionY(33)),
                Color = new Color(195, 205, 205) };
            Add(text);
            return text;
        }

        /// <summary>The current tab's outfit enum — the form value the native
        /// result carries on BOTH the confirm and the cancel edge (§D).</summary>
        public int CurrentEnum => TabOutfit(Tab, Female, Child);

        /// <summary>Probe/drive submit: the same path as the OK button
        /// (confirms the CURRENT tab's outfit enum).</summary>
        public void SubmitViaProbe()
        {
            Submit();
        }

        private void Submit()
        {
            Confirms++;
            LastEnum = TabOutfit(Tab, Female, Child);
            OnResult?.Invoke(LastEnum);
            Dismiss();
        }

        private void Cancel()
        {
            Cancels++;
            LastEnum = TabOutfit(Tab, Female, Child); // cancel also carries the form (decode §D)
            OnResult?.Invoke(-1);
            Dismiss();
        }

        private List<string> Wrap(string text, int width, int maxLines)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            var current = "";
            foreach (var word in text.Replace("\r\n", "\n").Split('\n'))
            {
                foreach (var part in word.Split(' '))
                {
                    var candidate = current == "" ? part : current + " " + part;
                    if (Font.Measure(candidate) <= width || current == "") current = candidate;
                    else { lines.Add(current); current = part; }
                }
                if (current != "") lines.Add(current);
                current = "";
                if (lines.Count >= maxLines) break;
            }
            return lines;
        }

        public void UpdatePosition()
        {
            var screen = GameFacade.Screens.CurrentUIScreen;
            Position = new Vector2((screen.ScreenWidth - WindowW) / 2f, (screen.ScreenHeight - WindowH) / 2f);
        }
        public override void GameResized() { UpdatePosition(); base.GameResized(); }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (state.NewKeys.Contains(Keys.Escape)) Cancel();
            OkLabel.Color = OkButton.IsDown ? Color.Cyan : OkButton.Hovered ? Color.White : new Color(195, 205, 205);
            CancelLabel.Color = CancelButton.IsDown ? Color.Cyan : CancelButton.Hovered ? Color.White : new Color(195, 205, 205);
            OkLabel.Position = OkLabelOrigin + (OkButton.IsDown ? new Vector2(2, 2) : Vector2.Zero);
            CancelLabel.Position = CancelLabelOrigin + (CancelButton.IsDown ? new Vector2(2, 2) : Vector2.Zero);
            // the CAS's per-frame pose driver (engine TSPaint -> AnimatePet);
            // SetOutfit-only tab changes never re-arm it (R212 law)
            if (VitaIdle != null)
            {
                try { VitaIdle.Update(state, VitaFacing); }
                catch { VitaIdle = null; }
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            UIOriginalDialogChrome.DrawPictureWindow(this, batch, 0, 0, WindowW, WindowH);
            base.Draw(batch);
        }
    }
}
