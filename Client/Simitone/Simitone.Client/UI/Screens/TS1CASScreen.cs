using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Utils;
using FSO.SimAntics;
using FSO.SimAntics.Engine.TSOTransaction;
using FSO.SimAntics.NetPlay;
using FSO.SimAntics.NetPlay.Drivers;
using FSO.SimAntics.NetPlay.Model;
using Simitone.Client.UI.Panels.WorldUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Rendering.Framework.Camera;
using Microsoft.Xna.Framework;
using FSO.LotView.RC;
using FSO.LotView.Utils;
using FSO.LotView.Model;
using FSO.Content;
using FSO.Vitaboy;
using FSO.Content.TS1;
using Simitone.Client.UI.Panels.CAS;
using Simitone.Client.UI.Controls;
using FSO.SimAntics.Model;
using FSO.Files.Formats.IFF.Chunks;
using Simitone.Client.UI.Panels;
using FSO.Client.UI.Controls;
using Simitone.Client.Utils;
using FSO.SimAntics.Utils;
using FSO.SimAntics.Model.TS1Platform;
using FSO.LotView;

namespace Simitone.Client.UI.Screens
{
    public class TS1CASScreen : UIScreen
    {
        private FSO.LotView.World World;
        public FSO.SimAntics.VM vm { get; set; }
        public VMNetDriver Driver;
        public BasicCamera Cam;
        public bool Initialized;
        public VMAvatar[] HeadAvatars;
        public VMAvatar[] BodyAvatars;
        public List<string> ActiveHeads;
        public List<string> ActiveBodies;
        public List<string> ActiveHeadTex;
        public List<string> ActiveHandgroupTex;
        public List<string> ActiveBodyTex;

        public static int NeighTypeFrom = 4;
        private bool Dead;

        private float _FamilySimInterp = -2;
        public float FamilySimInterp
        {
            set
            {
                CameraInterp(value);
                if (Original)
                {
                    // engine law: each original screen is an opaque 800x600
                    // replacement of the previous, not a layered slide.
                    DesktopCAS.Visible = value > 0.5f;
                    if (!DesktopCAS.Visible) DesktopCAS.BioEdit.Blur();
                    DesktopFamily.Visible = value <= 0.5f && value > -0.5f;
                    DesktopFamilies.Visible = value <= -0.5f;
                    _FamilySimInterp = value;
                    return;
                }
                CASPanel.Position = new Vector2((Cam == null)?10:((ScreenWidth - 500) / 2), 10 - (282 * (1-value)));
                FamilyPanel.ShowI = 1-Math.Abs(value);
                FamiliesPanel.TitleI = 1 - Math.Abs(value+1);

                _FamilySimInterp = value;
            }
            get
            {
                return _FamilySimInterp;
            }
        }

        public float HeadPosition = -9f;
        public float BodyPosition = -9f;

        public float HeadSpeed = 0f;
        public float BodySpeed = 0f;
        public float XLast = -1f;

        public int HeadPositionLast = 0;
        public int BodyPositionLast = 0;

        // AUD-17 F-3: last outfit applied to the vita preview (skip no-op
        // per-frame SetBody/SetHead rebuilds).
        private int _lastVitaBody = int.MinValue, _lastVitaHead = int.MinValue;

        public string CurrentCode = "ma";
        public string CurrentSkin = "lgt";
        private bool CurrentChild;

        // Native cWinDesignCharacter owns four zero-initialized {body,head}
        // rows. They survive type/skin changes only within one editing session.
        private readonly int[,] DesktopSuitMemory = new int[4, 2];
        internal Action<string> DesktopTypeLoaderForTest;

        private static int DesktopTypeIndex(string type) => (type[0] == 'f' ? 2 : 0) + (type[1] == 'c' ? 1 : 0);
        internal static int SanifyDesktopSuit(int index, int count) => count == 0 ? -1 : Math.Max(0, Math.Min(count - 1, index));
        private static int CarouselIndex(float position, int count) => count == 0 ? -1
            : (int)DirectionUtils.PosMod(Math.Round(position + 8), count);

        private void LoadDesktopType(string type)
        {
            if (DesktopTypeLoaderForTest == null) PopulateSimType(type);
            else { CurrentCode = type; DesktopTypeLoaderForTest(type); }
        }

        private void ChangeDesktopCollection(string type, string skin)
        {
            if (vm == null && DesktopTypeLoaderForTest == null) return;
            if (CurrentSkin == skin && CurrentCode == type) return;
            int previous = DesktopTypeIndex(CurrentCode);
            DesktopSuitMemory[previous, 0] = CarouselIndex(BodyPosition, ActiveBodies.Count);
            DesktopSuitMemory[previous, 1] = CarouselIndex(HeadPosition, ActiveHeads.Count);
            CurrentSkin = skin;
            LoadDesktopType(type);
            int next = DesktopTypeIndex(type);
            BodyPosition = SanifyDesktopSuit(DesktopSuitMemory[next, 0], ActiveBodies.Count) - 8;
            HeadPosition = SanifyDesktopSuit(DesktopSuitMemory[next, 1], ActiveHeads.Count) - 8;
        }

        private int? MoveInFamily;

        private UICASMode Mode = UICASMode.FamilyEdit;
        internal UICASMode CurrentMode => Mode;
        internal Action<int?> NeighborhoodTransition;

        public List<CASFamilyMember> WIPFamily = new List<CASFamilyMember>();
        public List<VMAvatar> RepresentFamily = new List<VMAvatar>();

        public UISimCASPanel CASPanel;
        public UIFamilyCASPanel FamilyPanel;
        public UIFamiliesCASPanel FamiliesPanel;
        public UITwoStateButton BackButton;
        public UITwoStateButton AcceptButton;

        // R143: desktop runs the ORIGINAL 800x600 screens on the decoded
        // engine law (r143/cas-layout-law.md + nbhd-layout-law.md); the FreeSO
        // mobile panels below are the !Original path, unchanged.
        public readonly bool Original = !FSOEnvironment.SoftwareKeyboard;
        public Simitone.Client.UI.Panels.CAS.UIOriginalDesignChar DesktopCAS;
        public Simitone.Client.UI.Panels.CAS.UIOriginalDesignFamily DesktopFamily;
        public Simitone.Client.UI.Panels.CAS.UIOriginalPickFamily DesktopFamilies;
        public VMAvatar VitaPreview;

        // R212: the engine vita window plays its idle cycle per paint
        // (cWinVitaBtnSolo::AnimatePet law) — the preview's idle driver.
        public Simitone.Client.UI.Panels.UIOriginalVitaIdlePlayer VitaIdle;

        // R143 Vita window calibration: the engine viewport rect is canon
        // ((618,145) 100x220); these camera constants are tuned ONCE against
        // the uisurvey dump so the preview sim lands in the hole (the engine's
        // own camera keyframes are a decoding residual).
        public Vector2 VitaCenterTile = new Vector2(31.1f, 23.6f);
        public float VitaZoom = 1.5f;
        public Vector3 VitaWorldPos = new Vector3(90f, 0, 58f);
        public float VitaFacing = MathHelper.PiOver2;

        public Vector3[] ModePositions = new Vector3[]
        {
            new Vector3(177.3843f, 150.92333f, 3.25105f),
            new Vector3(157.3843f, 28.92333f, 23.25105f),
            new Vector3(119.5611f, 6.122346f, 104.0364f),
            new Vector3(114.0793f, 10f, 64.67827f)
        };

        public Vector3[] ModeTargets = new Vector3[]
        {
            new Vector3(177.3843f-7f, 130.92333f, 3.25105f+5f),
            new Vector3(150.3057f, 26.01005f, 29.6858f),
            new Vector3(111.7678f, 3.936443f, 98.164f),
            new Vector3(104.5736f, 6.896059f, 64.59684f)
        };

        public Vector3[] Mode2D = new Vector3[]
        {
            new Vector3(104, 0, 57),
            new Vector3(104, 0, 57),
            new Vector3(103.5611f, 0, 92.0364f),
            new Vector3(84.0793f+6, 0, 64f-6)
        };

        public Vector3[] SinTransitions = new Vector3[]
        {
            new Vector3(0, 0, 0),
            new Vector3(12f, 0, 0),
            new Vector3(-6f, 0, 0),
            new Vector3()
        };

        public void CameraInterp(float value)
        {
            if (value < -2) value = -2;
            var prev = (int)(value + 2);
            var next = (int)(Math.Ceiling(value) + 2);
            if (next > 3) next = 3;

            if (Cam == null)
            {
                //2d
                var pos1 = Mode2D[prev];
                var pos2 = Mode2D[next];

                if (World != null) World.State.PreciseZoom = 1 + Math.Min(0, value * 0.5f);
                value = (float)DirectionUtils.PosMod(value, 1.0);
                var campos = Vector3.Lerp(pos1, pos2, value);
                campos += (float)Math.Sin(value * Math.PI) * SinTransitions[prev];



                if (World != null)
                    World.State.CenterTile = new Vector2(campos.X, campos.Z) / 3f;
            }
            else
            {

                var pos1 = ModePositions[prev];
                var pos2 = ModePositions[next];
                var targ1 = ModeTargets[prev] - pos1;
                var targ2 = ModeTargets[next] - pos2;

                value = (float)DirectionUtils.PosMod(value, 1.0);
                var camvec = Vector3.Lerp(targ1, targ2, value);
                var campos = Vector3.Lerp(pos1, pos2, value);

                campos += (float)Math.Sin(value * Math.PI) * SinTransitions[prev];

                Cam.Position = campos;
                Cam.Target = campos + camvec;
            }
        }

        private string MissingFallback(string x, IEnumerable<string> texnames)
        {
            return "x";
        }

        private void PopulateSimType(string simtype)
        {
            CurrentCode = simtype;
            // Filtering one skin must not remove entries from the shared
            // content collection and permanently hide them for other skins.
            var heads = Content.Get().BCFGlobal.CollectionsByName["c"].ClothesByAvatarType[simtype].ToList();
            if (simtype[1] == 'c') simtype += "chd";
            var bodies = Content.Get().BCFGlobal.CollectionsByName["b"].GeneralAvatarType(simtype).ToList();

            var tex = (TS1AvatarTextureProvider)Content.Get().AvatarTextures;
            var texnames = tex.GetAllNames();
            ActiveHeads = heads;
            ActiveBodies = bodies;
            
            ActiveHeadTex = heads.Select(x => RemoveExt(texnames.FirstOrDefault(y => y.StartsWith(ExtractID(x, CurrentSkin))))).ToList();
            ActiveBodyTex = bodies.Select(x => RemoveExt(
                texnames.FirstOrDefault(y => y.StartsWith(ExtractID(x, CurrentSkin)))
                ?? texnames.FirstOrDefault(y => y.StartsWith(ExtractID(x, ""))) ?? MissingFallback(x, texnames)
                )).ToList();
            ActiveHandgroupTex = ActiveBodyTex.Select(x => (RemoveExt(texnames.FirstOrDefault(y => RemoveExt(y) == "huao"+FindHG(x))) ?? "huao"+ CurrentSkin).Substring(4)).ToList();

            for (int i=0; i<ActiveHeads.Count; i++)
            {
                if (ActiveHeadTex[i] == null)
                {
                    ActiveHeadTex.RemoveAt(i);
                    ActiveHeads.RemoveAt(i--);
                }
            }

            for (int i = 0; i < ActiveBodies.Count; i++)
            {
                if (ActiveBodyTex[i] == null)
                {
                    ActiveBodyTex.RemoveAt(i);
                    ActiveHandgroupTex.RemoveAt(i);
                    ActiveBodies.RemoveAt(i--);
                }
            }

            HeadPositionLast = 0;
            BodyPositionLast = 0;
            PopulateReal();
        }

        private string FindHG(string item)
        {
            var ind = item.IndexOf('_');
            if (ind != -1) item = item.Substring(ind);
            return item;
        }

        private string RemoveExt(string item)
        {
            if (item == null) return null;
            var ind = item.LastIndexOf('.');
            if (ind != -1) return item.Substring(0, ind);
            return item;
        }

        private string ExtractID(string item, string skncol)
        {
            var ind = item.IndexOf('_');
            if (ind != -1) item = item.Substring(0, ind);
            return item + skncol;
        }

        private string InsertSkinColor(string name, string skncol)
        {
            var ind = name.IndexOf('_');
            if (ind != -1) name = name.Insert(ind, skncol);
            return name;
        }

        private void UpdateCarousel(UpdateState state)
        {
            var frac = 60f / FSOEnvironment.RefreshRate;
            var minSpeed = (Math.PI / 240f) * frac;
            var mult = (float)Math.Pow(0.95, frac);

            var moving = 0;

            if (state.MouseStates.Count(x => x.MouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed) > 0)
            {
                if (XLast == -1)
                {
                    if (state.MouseState.Y > 282)
                        XLast = state.MouseState.X;
                }
                else
                {
                    if (state.MouseState.Y / (float)UIScreen.Current.ScreenHeight > 0.625f)
                    {
                        BodySpeed = ((XLast - state.MouseState.X) / 200f) * frac;
                        moving = 1;
                    }
                    else
                    {
                        HeadSpeed = ((XLast - state.MouseState.X) / 200f) * frac;
                        moving = 2;
                    }
                    XLast = state.MouseState.X;
                }
            }
            else
            {
                XLast = -1;
            }

            BodySpeed = BodySpeed * mult;
            if (Math.Abs(BodySpeed) < minSpeed && moving != 1)
            {
                var targInt = (int)Math.Round(BodyPosition);
                BodySpeed = (float)Math.Max(-minSpeed, Math.Min(minSpeed, targInt - BodyPosition));
            }

            HeadSpeed = HeadSpeed * mult;
            if (Math.Abs(HeadSpeed) < minSpeed && moving != 2)
            {
                var targInt = (int)Math.Round(HeadPosition);
                HeadSpeed = (float)Math.Max(-minSpeed, Math.Min(minSpeed, targInt - HeadPosition));
            }

            HeadPosition += HeadSpeed;
            BodyPosition += BodySpeed;

            var room = vm.Context.GetRoomAt(LotTilePos.FromBigTile(28, 21, 1));
            foreach (var body in BodyAvatars) body.SetRoom(room);
            foreach (var head in HeadAvatars) head.SetRoom(65534);

            var curbody = (int)BodyPosition;
            if (curbody != BodyPositionLast)
            {
                FSO.HIT.HITVM.Get().PlaySoundEvent(FSO.Client.UI.Model.UISounds.Click);
                int replacePos = (int)DirectionUtils.PosMod((-BodyPositionLast), 18);
                int increment = 1;
                if (curbody < BodyPositionLast) //start adding after last position's compliment position    
                {
                    increment = -1;
                    replacePos = (int)DirectionUtils.PosMod((-BodyPositionLast), 18);
                }
                var total = Math.Abs(curbody - BodyPositionLast);
                for (int i=0; i<total; i++)
                {
                    if (increment == -1)
                    {
                        SetBody(BodyAvatars[replacePos], BodyPositionLast-1);
                    } else
                    {
                        SetBody(BodyAvatars[replacePos], BodyPositionLast + 17);
                    }
                    BodyPositionLast += increment;
                    replacePos = ((replacePos - increment) + 18) % 18;

                }
                BodyPositionLast = curbody;
            }

            var curhead = (int)HeadPosition;
            if (curhead != HeadPositionLast)
            {
                FSO.HIT.HITVM.Get().PlaySoundEvent(FSO.Client.UI.Model.UISounds.Click);
                int replacePos = (int)DirectionUtils.PosMod(-HeadPositionLast, 18);
                int increment = 1;
                if (curhead < HeadPositionLast) //start adding after last position's compliment position    
                {
                    increment = -1;
                    replacePos = (int)DirectionUtils.PosMod((-HeadPositionLast), 18);
                }
                var total = Math.Abs(curhead - HeadPositionLast);
                for (int i = 0; i < total; i++)
                {
                    if (increment == -1)
                    {
                        SetHead(HeadAvatars[replacePos], HeadPositionLast - 1);
                    }
                    else
                    {
                        SetHead(HeadAvatars[replacePos], HeadPositionLast + 17);
                    }
                    HeadPositionLast += increment;
                    replacePos = ((replacePos - increment) + 18) % 18;

                }
                HeadPositionLast = curhead;
            }
        }

        private void SetBody(VMAvatar body, int i)
        {
            if (ActiveBodies.Count == 0) return; // AUD-17 F-5: empty corpus — PosMod(x,0) is NaN
            i = (int)DirectionUtils.PosMod(i, ActiveBodies.Count);
            var code = CurrentCode[0];
            if (CurrentCode[1] != 'a') code = 'u';
            var oft = new Outfit() { TS1AppearanceID = ActiveBodies[i] + ".apr", TS1TextureID = ActiveBodyTex[i] };
            var hg = ActiveHandgroupTex[i];
            oft.LiteralHandgroup = new HandGroup()
            {
                TS1HandSet = true,
                LightSkin = new HandSet()
                {
                    LeftHand = new Hand()
                    {
                        Idle = new Gesture() { Name = "h" + code + "lo.apr", TexName = "huao" + hg },
                        Pointing = new Gesture() { Name = "h" + code + "lp.apr", TexName = "huap" + hg },
                        Fist = new Gesture() { Name = "h" + code + "lc.apr", TexName = "huac" + hg }
                    },
                    RightHand = new Hand()
                    {
                        Idle = new Gesture() { Name = "h" + code + "ro.apr", TexName = "huao" + hg },
                        Pointing = new Gesture() { Name = "h" + code + "rp.apr", TexName = "huap" + hg },
                        Fist = new Gesture() { Name = "h" + code + "rc.apr", TexName = "huac" + hg }
                    }
                }
            };

            body.BodyOutfit = new FSO.SimAntics.Model.VMOutfitReference(oft);
        }

        private void SetHead(VMAvatar head, int i)
        {
            if (ActiveHeads.Count == 0) return; // AUD-17 F-5: empty corpus — PosMod(x,0) is NaN
            i = (int)DirectionUtils.PosMod(i, ActiveHeads.Count);
            head.HeadOutfit = new FSO.SimAntics.Model.VMOutfitReference(new Outfit() { TS1AppearanceID = ActiveHeads[i] + ".apr", TS1TextureID = ActiveHeadTex[i] });
        }

        private void PopulateReal()
        {
            var child = CurrentCode[1] == 'c';
            //var animSource = HeadAvatars[0].Avatar.Skeleton;

            int i = 0;
            foreach (var head in HeadAvatars)
            {
                SetHead(head, 17-i);
                //head.Avatar.Skeleton = animSource;
                if (child != CurrentChild)
                {
                    head.Avatar.Skeleton = Content.Get().AvatarSkeletons.Get(child ? "child.skel" : "adult.skel");
                    head.Avatar.ReloadSkeleton();
                }
                i++;
            }
            i = 0;
            foreach (var body in BodyAvatars)
            {
                SetBody(body, 17-i);
                //body.Avatar.Skeleton = animSource;
                if (child != CurrentChild)
                {
                    body.Avatar.Skeleton = Content.Get().AvatarSkeletons.Get(child ? "child.skel" : "adult.skel");
                    body.Avatar.ReloadSkeleton();
                }
                i++;
            }
            CurrentChild = child;
        }

        public TS1CASScreen()
        {
            var ui = Content.Get().CustomUI;
            var gd = GameFacade.GraphicsDevice;

            if (Original)
            {
                DesktopCAS = new Simitone.Client.UI.Panels.CAS.UIOriginalDesignChar();
                DesktopFamily = new Simitone.Client.UI.Panels.CAS.UIOriginalDesignFamily();
                DesktopFamilies = new Simitone.Client.UI.Panels.CAS.UIOriginalPickFamily();

                DesktopCAS.OnCollectionChange += ChangeDesktopCollection;
                DesktopCAS.OnCycleHead += (dir) => { HeadPosition += dir; };
                DesktopCAS.OnCycleBody += (dir) => { BodyPosition += dir; };
                DesktopCAS.OnDone += () => Accept(null);
                DesktopCAS.OnCancel += () => GoBack(null);

                DesktopFamily.ModifySim = RequestModifySim;
                DesktopFamily.OnFamilyDone += () => Accept(null);
                DesktopFamily.OnFamilyCancel += () => GoBack(null);

                DesktopFamilies.OnNewFamily += () => { SetMode(UICASMode.FamilyEdit); };
                DesktopFamilies.OnDeleteFamily += DeleteFamily;
                DesktopFamilies.OnCancel += () => GoBack(null);
                DesktopFamilies.MoveInButton.OnButtonClick += (b) => { if (!DesktopFamilies.MoveInButton.Disabled) Accept(null); };

                DesktopCAS.Visible = false;
                DesktopFamily.Visible = false;
                Add(DesktopFamilies);
                Add(DesktopFamily);
                Add(DesktopCAS);
                return;
            }

            CASPanel = new UISimCASPanel();
            CASPanel.OnCollectionChange += CASPanel_OnCollectionChange;
            CASPanel.Position = new Vector2(0, -400);
            CASPanel.OnRandom += CASPanel_OnRandom;
            Add(CASPanel);

            FamilyPanel = new UIFamilyCASPanel(RepresentFamily);
            // AUD-17 F-6: route mobile deletes through the native confirm
            // (RequestModifySim confirms delete, passes everything else through).
            FamilyPanel.ModifySim = RequestModifySim;
            Add(FamilyPanel);

            FamiliesPanel = new UIFamiliesCASPanel();
            FamiliesPanel.OnNewFamily += () => { SetMode(UICASMode.FamilyEdit); };
            FamiliesPanel.OnDeleteFamily += DeleteFamily;
            Add(FamiliesPanel);

            BackButton = new UITwoStateButton(ui.Get("btn_back.png").Get(gd));
            BackButton.Position = new Vector2(25, ScreenHeight - 140);
            Add(BackButton);

            AcceptButton = new UITwoStateButton(ui.Get("btn_accept.png").Get(gd));
            AcceptButton.Position = new Vector2(ScreenWidth-140, ScreenHeight - 140);
            Add(AcceptButton);

            BackButton.OnButtonClick += GoBack;
            AcceptButton.OnButtonClick += Accept;
        }

        private FSO.Client.UI.Controls.UITextBox PendingDesktopNameFocus;
        private InputManager DesktopInput;

        private void ClearDesktopNameFocus()
        {
            PendingDesktopNameFocus = null;
            var focus = DesktopInput?.GetFocus();
            if (focus != null && (focus == DesktopCAS?.NameBox || focus == DesktopFamily?.FamilyNameBox))
                DesktopInput.SetFocus(null);
        }

        internal void UpdateDesktopNameFocus(UpdateState state)
        {
            if (!Original || state.InputManager == null) return;
            DesktopInput = state.InputManager;
            var focus = DesktopInput.GetFocus();
            if ((focus == DesktopCAS.NameBox && (!DesktopCAS.Visible || Mode != UICASMode.SimEdit || ConfirmationPending))
                || (focus == DesktopFamily.FamilyNameBox && (!DesktopFamily.Visible || Mode != UICASMode.FamilyEdit || ConfirmationPending)))
                DesktopInput.SetFocus(null);
            var target = PendingDesktopNameFocus;
            if (target == null || ConfirmationPending || !state.WindowFocused) return;
            if (target == DesktopCAS.NameBox ? !DesktopCAS.Visible : !DesktopFamily.Visible) return;
            // R143 TSSetFocus at 0x2cf53c / 0x2d1f24: once on dialog entry,
            // after the incoming panel becomes visible, before child input.
            PendingDesktopNameFocus = null;
            if (DesktopInput.GetFocus() == null || DesktopInput.GetFocus() == target)
                DesktopInput.SetFocus(target);
        }

        private int EditIndex = -1;

        internal void RequestModifySim(bool delete, int index)
        {
            if (!delete) { ModifySim(false, index); return; }
            if (index < 0 || index >= WIPFamily.Count) return;
            // Native family TSOnCommand @0x2d07dc..0x2d0810 confirms
            // deleting a selected person before mutating the draft.
            ShowConfirmation(GameFacade.Strings.GetString("129", "5"),
                GameFacade.Strings.GetString("129", "6"), () => ModifySim(true, index));
        }

        public void ModifySim(bool delete, int index)
        {
            if (delete && (index < 0 || index >= WIPFamily.Count)) return;
            if (!delete && (index < -1 || index >= WIPFamily.Count)) return;
            // AUD-17 F-6: the Add gate (8-member cap + non-empty family name)
            // was Original-only — the mobile --touch path could add a 9th
            // member or add into a blank family name.
            if (!delete && index == -1
                && (WIPFamily.Count >= 8
                    || (Original ? DesktopFamily.FamilyNameBox.CurrentText.Length == 0
                        : (FamilyPanel != null && FamilyPanel.SecondName.CurrentText.Length == 0)))) return;
            if (index == -1)
            {
                PrepareEdit(index);
                SetMode(UICASMode.SimEdit);
            } else
            {
                if (delete)
                {
                    var fam = RepresentFamily[index];
                    fam.Delete(true, vm.Context);
                    RepresentFamily.RemoveAt(index);
                    WIPFamily.RemoveAt(index);
                    // Native deletion clears selected index @0x2d0860..64.
                    if (Original) DesktopFamily.SelectedMember = -1;

                    foreach (var fam2 in RepresentFamily)
                    {
                        fam2.SetPosition(LotTilePos.OUT_OF_WORLD, Direction.NORTH, vm.Context);
                    }
                    for (int i=0; i<RepresentFamily.Count; i++)
                    {
                        SetFamilyMember(i);
                    }
                    FamilyPanel?.Reset();
                    UpdateFamilySlots();
                } else
                {
                    //prepare sim edit mode with old sim's parameters
                    PrepareEdit(index);
                    SetMode(UICASMode.SimEdit);
                }
            }
        }

        private void PrepareEdit(int i)
        {
            EditIndex = i;
            if (Original)
            {
                // R143: the original screen's fields are the state holders on
                // desktop (engine capacities: name 25, bio 2048, pool 25).
                var sim = (i > -1) ? WIPFamily[i] : null;
                DesktopCAS.NameBox.CurrentText = sim?.Name ?? "";
                DesktopCAS.BioEdit.Text = sim?.Bio ?? "";
                CurrentCode = sim == null ? "ma"
                    : (sim.Gender == 0) ? "ma" : (sim.Gender == 1) ? "fa"
                    : (sim.Gender == 2) ? "mc" : "fc";
                CurrentSkin = sim?.SkinColor ?? "lgt";
                for (int j = 0; j < 5; j++) DesktopCAS.Values[j] = (sim != null) ? sim.Personality[j] / 100 : 0;
                int spent = 0;
                for (int j = 0; j < 5; j++) spent += DesktopCAS.Values[j];
                DesktopCAS.Pool = 25 - spent;

                // Each native character dialog starts with a fresh four-type
                // memory table, even when editing a previously saved draft Sim.
                Array.Clear(DesktopSuitMemory, 0, DesktopSuitMemory.Length);
                LoadDesktopType(CurrentCode);

                BodyPosition = SanifyDesktopSuit(sim != null ? ActiveBodies.IndexOf(sim.Body) : 0, ActiveBodies.Count) - 8;
                HeadPosition = SanifyDesktopSuit(sim != null ? ActiveHeads.IndexOf(sim.Head) : 0, ActiveHeads.Count) - 8;
                DesktopCAS.AType = CurrentCode;
                DesktopCAS.SkinType = CurrentSkin;
                DesktopCAS.UpdateType();
                DesktopCAS.UpdateLeds();
                return;
            }
            if (i > -1)
            {
                var sim = WIPFamily[i];

                //load this family member's traits into the editor
                CASPanel.FirstNameTextBox.CurrentText = sim.Name;
                CASPanel.BioEdit.CurrentText = sim.Bio;
                switch (sim.Gender)
                {
                    case 0: CurrentCode = "ma"; break;
                    case 1: CurrentCode = "fa"; break;
                    case 2: CurrentCode = "mc"; break;
                    case 3: CurrentCode = "fc"; break;
                }
                for (int j = 0; j < 5; j++)
                {
                    CASPanel.Personalities[j].Points = sim.Personality[j] / 100;
                }
                CurrentSkin = sim.SkinColor;

                PopulateSimType(CurrentCode);

                //find index for the sims body and head

                BodyPosition = ActiveBodies.IndexOf(sim.Body) - 8;
                HeadPosition = ActiveHeads.IndexOf(sim.Head) - 8;
            } else
            {
                CASPanel.FirstNameTextBox.CurrentText = "";
                CASPanel.BioEdit.CurrentText = "";
                CurrentCode = "ma";
                CurrentSkin = "lgt";
                for (int j = 0; j < 5; j++)
                {
                    CASPanel.Personalities[j].Points = 0;
                }
                PopulateSimType(CurrentCode);
                BodyPosition = - 8;
                HeadPosition = - 8;
            }
            CASPanel.AType = CurrentCode;
            CASPanel.SkinType = CurrentSkin;
            CASPanel.UpdateTotalPoints();
            CASPanel.UpdateType();
        }

        public UIMobileAlert ConfirmDialog;
        internal bool ConfirmationPending; // internal: ucasflow gate reads the seam/dialog latch state
        internal Action<string, string, Action, Action> ConfirmationPresenter;
        internal Action<FAMI> FamilyDeleteWriter;
        internal Action<string, CASFamilyMember[]> FamilySaveWriter;
        internal Func<List<FAMI>> FamilyCensus;

        private void ShowConfirmation(string title, string message, Action accepted)
        {
            if (ConfirmationPending) return;
            ClearDesktopNameFocus();
            DesktopCAS?.BioEdit.Blur();
            ConfirmationPending = true;
            bool answered = false;
            Action<bool> answer = yes =>
            {
                if (answered) return;
                answered = true;
                ConfirmDialog?.Close();
                ConfirmDialog = null;
                ConfirmationPending = false;
                if (yes) accepted();
            };
            if (ConfirmationPresenter != null)
            {
                ConfirmationPresenter(title, message, () => answer(true), () => answer(false));
                return;
            }
            ConfirmDialog = new UIMobileAlert(new UIAlertOptions
            {
                Title = title,
                Message = message,
                Buttons = UIAlertButton.YesNo(b => answer(true), b => answer(false))
            });
            UIScreen.GlobalShowDialog(ConfirmDialog, true);
        }

        internal void Accept(UIElement button)
        {
            if (ConfirmationPending) return;
            switch (Mode)
            {
                case UICASMode.SimEdit:
                    if ((Original ? DesktopCAS.NameBox.CurrentText : CASPanel.FirstNameTextBox.CurrentText).Length == 0) return;
                    // AUD-17 F-6: the unspent-points confirm was Original-only —
                    // the mobile panel tracks the same pool (Allowed − Total).
                    var unspent = Original ? DesktopCAS.Pool > 0
                        : (CASPanel != null && CASPanel.TotalPoints < CASPanel.AllowedPoints);
                    if (unspent)
                    {
                        // Native TSOnCommand @0x2cd414..0x2cd464 asks before
                        // keeping a Sim with unspent personality points.
                        ShowConfirmation(GameFacade.Strings.GetString("130", "13"),
                            GameFacade.Strings.GetString("130", "14", new[] { Original ? DesktopCAS.NameBox.CurrentText : CASPanel.FirstNameTextBox.CurrentText }),
                            () => { AcceptMember(); SetMode(UICASMode.FamilyEdit); });
                        return;
                    }
                    AcceptMember();
                    break;
                case UICASMode.FamilyEdit:
                    if (WIPFamily.Count == 0) return;
                    if ((Original ? DesktopFamily.FamilyNameBox.CurrentText
                        : FamilyPanel.SecondName.CurrentText).Length == 0) return; // AUD-17 F-1: never save a blank family name
                    ShowConfirmation(GameFacade.Strings.GetString("129", "13"),
                        GameFacade.Strings.GetString("129", "14"),
                        () => { SaveFamily(); SetMode(UICASMode.FamilySelect); });
                    return;
                case UICASMode.FamilySelect:
                    //accept button here is move in. notify the neighbourhood screen that we're moving in now.
                    var selected = SelectedFamily();
                    if (selected == null) return;
                    MoveInFamily = selected.ChunkID;
                    break;
            }
            SetMode((UICASMode)(((int)Mode) - 1));
        }

        internal void GoBack(UIElement button)
        {
            if (ConfirmationPending) return;
            if (Mode == UICASMode.FamilyEdit)
            {
                // Native @0x2d0b48..0x2d0b8c bypasses the warning for an
                // empty draft, but asks before discarding any members.
                if (!Original || WIPFamily.Count > 0)
                {
                    ShowConfirmation(GameFacade.Strings.GetString("129", "7"),
                        GameFacade.Strings.GetString("129", "8"),
                        () => { ClearFamily(); SetMode(UICASMode.FamilySelect); });
                    return;
                }
                ClearFamily();
            }
            SetMode((UICASMode)(((int)Mode) - 1));
        }

        public void SetMode(UICASMode mode)
        {
            bool newFamily = Mode == UICASMode.FamilySelect && mode == UICASMode.FamilyEdit;
            ClearDesktopNameFocus();
            DesktopCAS?.BioEdit.Blur();
            if (mode == UICASMode.ToNeighborhood)
            {
                if (NeighborhoodTransition != null)
                {
                    NeighborhoodTransition(MoveInFamily);
                    return;
                }
                // R208: the engine's done-path runs LoadGame DIRECTLY —
                // cWinDesignFamily::TSOnCommand constructs/loads the next
                // screen with no wipe; the lot load itself shows the canon
                // loading splash (uisplash). The trans_normal stripe wipe was
                // a Simitone invention and is retired.
                Dead = true;
                CleanupLastWorld();
                if (MoveInFamily == null)
                    GameController.EnterGameMode("", false);
                else
                    GameController.EnterGameMode("!"+((NeighTypeFrom == 7)?'m':'n')+MoveInFamily.Value.ToString(), false);
                return;
            } else if (mode == UICASMode.FamilyEdit)
            {
                FamilyPanel?.Reset();
                if (Original) UpdateFamilySlots();
            }

            if (!Original)
            {
                FamiliesPanel.SetSelection(-1);
                if (mode == UICASMode.FamilySelect) AcceptButton.Texture = Content.Get().CustomUI.Get("btn_movein.png").Get(GameFacade.GraphicsDevice);
                else AcceptButton.Texture = Content.Get().CustomUI.Get("btn_accept.png").Get(GameFacade.GraphicsDevice);
            }

            GameFacade.Screens.Tween.To(this, 1f, new Dictionary<string, float> { { "FamilySimInterp", (int)mode-1 } }, TweenQuad.EaseInOut);
            Mode = mode;
            if (Original)
                PendingDesktopNameFocus = mode == UICASMode.SimEdit ? DesktopCAS.NameBox
                    : newFamily ? DesktopFamily.FamilyNameBox : null;
        }

        private void CASPanel_OnRandom()
        {
            var rand = new Random();
            HeadPosition = rand.Next(ActiveHeads.Count);
            BodyPosition = rand.Next(ActiveBodies.Count);
            PopulateReal();
            HeadPositionLast = 0;
            BodyPositionLast = 0;
        }

        private void CASPanel_OnCollectionChange()
        {
            if (CurrentSkin == CASPanel.SkinType && CurrentCode == CASPanel.AType) return;
            CurrentSkin = CASPanel.SkinType;
            if (vm == null) return;
            PopulateSimType(CASPanel.AType);
        }

        public override void Update(UpdateState state)
        {
            UpdateDesktopNameFocus(state);
            base.Update(state);
            ModePositions[3].Y = 11;
            ModeTargets[3].Y = 7.896059f;
            if (Dead) return;
            if (vm == null) InitializeLot();
            vm.Update();
            if (World != null && !Initialized)
            {
                World.State.DisableSmoothRotation = true;
                if (GraphicsModeControl.Mode == GlobalGraphicsMode.Full3D)
                {
                    World.State.SetCameraType(World, FSO.LotView.Utils.Camera.CameraControllerType.FirstPerson, 0);
                    var fp = World.State.Cameras.CameraFirstPerson;
                    Cam = fp.Camera;
                    fp.FixedCam = true;
                }
                SetMode(UICASMode.FamilySelect);
                SetFamilies();
                Initialized = true;
                //FamilySimInterp = FamilySimInterp;
            }

            if (World.State.PreciseZoom != 1) World.State.PreciseZoom = World.State.PreciseZoom;
            switch (Mode)
            {
                case UICASMode.FamilySelect:
                    if (World.State.Level != 2)
                    {
                        World.State.Level = 2;
                        World.State.DrawRoofs = true;
                        vm.Context.Blueprint.Cutaway = new bool[vm.Context.Blueprint.Cutaway.Length];
                        vm.Context.Blueprint.Changes.SetFlag(BlueprintGlobalChanges.WALL_CUT_CHANGED);
                    }
                    break;
                default:
                    if (World.State.Level != 1 && Cam == null)
                    {
                        World.State.Level = 1;
                        World.State.DrawRoofs = false;
                        vm.Context.Blueprint.Cutaway = VMArchitectureTools.GenerateRoomCut(vm.Context.Architecture, World.State.Level, World.State.CutRotation, 
                            new HashSet<uint>(vm.Context.RoomInfo.Where(x => x.Room.IsOutside == false).Select(x => (uint)x.Room.RoomID)));
                        vm.Context.Blueprint.Changes.SetFlag(BlueprintGlobalChanges.WALL_CUT_CHANGED);
                    }
                    break;
            }

            vm.Context.Clock.Minutes = 0;
            vm.Context.Clock.Hours = 12;

            var disableAccept = false;
            switch (Mode)
            {
                case UICASMode.SimEdit:
                    if ((Original ? DesktopCAS.NameBox.CurrentText : CASPanel.FirstNameTextBox.CurrentText).Length == 0) disableAccept = true;
                    break;
                case UICASMode.FamilySelect:
                    if ((Original ? DesktopFamilies.GetSelection() : FamiliesPanel.Selection) == -1) disableAccept = true;
                    break;
                case UICASMode.FamilyEdit:
                    // AUD-17 F-1: the empty-family-NAME guard existed only on
                    // Add — Done accepted a blank last name and permanently
                    // wrote it into the neighborhood FAMs table. Gate Done the
                    // same way (both chrome paths).
                    if (WIPFamily.Count == 0 || (Original ? DesktopFamily.FamilyNameBox.CurrentText
                        : FamilyPanel.SecondName.CurrentText).Length == 0) disableAccept = true;
                    break;
            }

            if (Original)
            {
                DesktopCAS.DoneBtn.Disabled = disableAccept;
                DesktopFamily.DoneBtn.Disabled = disableAccept;
                DesktopFamily.AddBtn.Disabled = WIPFamily.Count >= 8 || DesktopFamily.FamilyNameBox.CurrentText.Length == 0;
            }
            else AcceptButton.Disabled = disableAccept;
            //AcceptButton.ForceState = disableAccept ? 0 : -1;
            //AcceptButton.Opacity = disableAccept ? 0.5f : 1;

            if (Mode == UICASMode.SimEdit && !Original)
                UpdateCarousel(state);

            if (Original && Mode == UICASMode.SimEdit)
            {
                // R143 Vita window: ONE full sim (chosen head + chosen body on a
                // single avatar) stands in the engine's (618,145) 100x220 view
                // rect; the ring carousel is the mobile look and stays hidden.
                if (VitaPreview == null)
                {
                    VitaPreview = BodyAvatars[0];
                    foreach (var body in BodyAvatars) body.VisualPosition = new Vector3(9999, 0, 9999);
                    foreach (var head in HeadAvatars) head.VisualPosition = new Vector3(9999, 0, 9999);
                }
                var chosenBody = (int)DirectionUtils.PosMod(Math.Round(BodyPosition + 8), ActiveBodies.Count);
                var chosenHead = (int)DirectionUtils.PosMod(Math.Round(HeadPosition + 8), ActiveHeads.Count);
                // AUD-17 F-3: SetBody/SetHead ran EVERY frame (~120 outfit
                // allocations/sec + triple appearance rebuild) for a state that
                // only changes on spinner clicks — reassign only on change.
                // AUD-17 F-5: an empty collection (partial/corrupt install)
                // made PosMod(x,0)=NaN -> invalid index; keep the last outfit.
                if (chosenBody != _lastVitaBody && ActiveBodies.Count > 0) { SetBody(VitaPreview, chosenBody); _lastVitaBody = chosenBody; }
                if (chosenHead != _lastVitaHead && ActiveHeads.Count > 0) { SetHead(VitaPreview, chosenHead); _lastVitaHead = chosenHead; }
                // VisualPosition is tile XY/height Z; VitaWorldPos is renderer
                // world XYZ. Invert WorldSpace.GetWorldFromTile's axis/scale map.
                VitaPreview.VisualPosition = new Vector3(VitaWorldPos.X, VitaWorldPos.Z, VitaWorldPos.Y)
                    / WorldSpace.WorldUnitsPerTile;
                VitaPreview.RadianDirection = VitaFacing;
                // The dedicated view renders the same posed avatar, independent
                // of the lot camera and the window's dimensions.
                if (DesktopCAS.VitaSurface != null)
                {
                    // This staging avatar belongs only to the Vita surface;
                    // larger windows must not reveal a second Sim in the lot.
                    VitaPreview.WorldUI.Visible = false;
                    DesktopCAS.VitaSurface.Person = VitaPreview;
                    DesktopCAS.VitaSurface.Visible = true;
                }

                // R212 AnimatePet law: only a person change re-arms the idle
                // cycle (SetPerson semantics); spinner changes are SetOutfit
                // 0x2daf32 and must not restart it.
                var vitaChild = CurrentCode[1] == 'c';
                var vitaMale = CurrentCode[0] == 'm';
                if (VitaIdle == null)
                    VitaIdle = new Simitone.Client.UI.Panels.UIOriginalVitaIdlePlayer(VitaPreview, vitaChild, vitaMale);
                else if (VitaIdle.Avatar != VitaPreview || VitaIdle.Child != vitaChild || VitaIdle.Male != vitaMale)
                    VitaIdle.SetPerson(VitaPreview, vitaChild, vitaMale);
                try { VitaIdle.Update(state, VitaFacing); }
                catch (Exception ve) { Simitone.Client.GameLog.Write("cas-vita-idle EXC " + ve.GetType().Name + " " + ve.Message); VitaIdle = null; }
            }

            if (!Original)
            for (int i=0; i<18; i++)
            {
                var relPos = HeadPosition + i - 9;
                relPos = (float)DirectionUtils.PosMod(relPos, 18);
                if (relPos > 9) relPos += 6;
                var angle = (relPos / 24) * (Math.PI * 2);
                var pos = new Vector3(28.5f + 4.5f*(float)Math.Cos(angle), 21.5f+ 4.5f * (float)Math.Sin(angle), 0);
                HeadAvatars[i].RadianDirection = (float)angle + (float)Math.PI/2;
                HeadAvatars[i].VisualPosition = pos;
            }

            // The desktop preview already positioned BodyAvatars[0] above.
            // Only the mobile screen owns the ring carousel.
            if (!Original)
            for (int i = 0; i < 18; i++)
            {
                var relPos = BodyPosition + i - 9;
                relPos = (float)DirectionUtils.PosMod(relPos, 18);
                if (relPos > 9) relPos += 6;
                var angle = (relPos / 24) * (Math.PI * 2);
                var pos = new Vector3(28.5f + 4.5f * (float)Math.Cos(angle), 21.5f + 4.5f * (float)Math.Sin(angle), 0);
                BodyAvatars[i].RadianDirection = (float)angle + (float)Math.PI / 2;
                BodyAvatars[i].VisualPosition = pos;
            }

            foreach (var fam in RepresentFamily)
            {
                for (int i = 0; i < 16; i++)
                {
                    fam.SetMotiveData((VMMotive)i, 100);
                }
                var q = new List<FSO.SimAntics.Engine.VMQueuedAction>(fam.Thread.Queue);
                foreach (var action in q)
                    fam.Thread.CancelAction(action.UID);
            }
        }

        internal FAMI SelectedFamily()
        {
            var families = Original ? DesktopFamilies.Families : FamiliesPanel.Families;
            var index = Original ? DesktopFamilies.GetSelection() : FamiliesPanel.Selection;
            return index >= 0 && index < families.Count ? families[index] : null;
        }

        public void DeleteFamily()
        {
            var selectedFamily = SelectedFamily();
            if (selectedFamily == null) return;
            var familyName = selectedFamily.ChunkParent?.Get<FAMs>(selectedFamily.ChunkID)?.GetString(0) ?? "";
            ShowConfirmation(GameFacade.Strings.GetString("128", "6"),
                GameFacade.Strings.GetString("128", "7", new[] { familyName }), () =>
                {
                    if (FamilyDeleteWriter != null) FamilyDeleteWriter(selectedFamily);
                    else
                    {
                        var neigh = Content.Get().Neighborhood;
                        var fams = neigh.MainResource.Get<FAMs>(selectedFamily.ChunkID);
                        selectedFamily.ChunkParent.FullRemoveChunk(selectedFamily);
                        if (fams != null) fams.ChunkParent.FullRemoveChunk(fams);
                        neigh.SaveNeighbourhood(true);
                    }
                    if (Original) DesktopFamilies.SetSelection(-1);
                    else FamiliesPanel.SetSelection(-1);
                    SetFamilies();
                });
        }

        public void SetFamilies()
        {
            if (FamilyCensus != null)
            {
                var isolated = FamilyCensus();
                if (Original) DesktopFamilies.UpdateFamilies(isolated, vm);
                else FamiliesPanel.UpdateFamilies(isolated, vm);
                return;
            }
            //get all families that don't have a house from neighbourhood, and populate the list
            //i think house number -1 is townies, so only select 0
            var all = Content.Get().Neighborhood.MainResource.List<FAMI>();

            // Filter: HouseNumber == 0 (not moved into a house)
            // AND ChunkID < 1000 (exclude NPCs/townies which have IDs like 2000, 3000+, 4000, 5000, 6000)
            // AND ChunkID > 0 (exclude "Default Family" which is ChunkID=0 with 0 members)
            // AND FamilyGUIDs.Length > 0 (must have at least one member)
            var families = all.Where(x => x.HouseNumber == 0 && x.ChunkID > 0 && x.ChunkID < 1000 && x.FamilyGUIDs.Length > 0).ToList();

            // Keep diagnostics outside the signed application bundle.
            Console.WriteLine($"[SetFamilies] Families being added to CAS panel ({families.Count} total):");
            foreach (var fam in families)
            {
                Console.WriteLine(
                    $"  - ChunkID={fam.ChunkID}, HouseNumber={fam.HouseNumber}, Unknown={fam.Unknown}, Members={fam.FamilyGUIDs.Length}");
            }

            if (Original) DesktopFamilies.UpdateFamilies(families, vm);
            else FamiliesPanel.UpdateFamilies(families, vm);
        }

        /// <summary>
        /// R143: the original Create-A-Family member slots - 85x105 cells,
        /// portrait at (15,10), name at the slot bottom (engine law §4.3).
        /// </summary>
        // AUD-17 F-7: slot-portrait memo — every refresh used to re-render
        // ALL slots (3 renders + 3 synchronous GPU readbacks per member);
        // now only members whose representative's head outfit changed
        // regenerate. Portraits dispose their own texture on removal, so
        // eviction rides the slot clear.
        private readonly UIOriginalPersonPortrait[] _slotPortraits = new UIOriginalPersonPortrait[8];
        private readonly string[] _slotPortraitKeys = new string[8];

        public void UpdateFamilySlots()
        {
            if (!Original) return;
            DesktopFamily.MemberCount = WIPFamily.Count;
            var font = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadCaption(GameFacade.GraphicsDevice);
            for (int i = 0; i < 8; i++)
            {
                var slot = DesktopFamily.Slots[i];
                var visible = i < WIPFamily.Count;
                var rep = (i < RepresentFamily.Count) ? RepresentFamily[i] : null;
                var key = rep?.HeadOutfit?.OftData?.TS1AppearanceID + ":" + rep?.HeadOutfit?.OftData?.TS1TextureID;
                // memo hit? (visible slot, memo present, key unchanged)
                var memo = visible && _slotPortraits[i] != null && _slotPortraitKeys[i] == key
                    ? _slotPortraits[i] : null;
                if (memo == null) _slotPortraits[i] = null; // stale/hidden — detach below
                foreach (var child in slot.GetChildren().ToList())
                    if (child != memo) slot.Remove(child); // removing a memoed portrait would dispose its texture
                slot.Visible = visible;
                if (!visible || rep == null) continue;
                var data = WIPFamily[i];
                // WIP representatives use a generic object resource: its saved BMP
                // belongs to the template Sim, so render the edited head instead.
                if (_slotPortraits[i] == null)
                {
                    _slotPortraits[i] = UIOriginalPersonPortrait.Create(rep, false);
                    _slotPortraitKeys[i] = key;
                }
                var portrait = _slotPortraits[i];
                portrait.Position = new Vector2(15, 10);
                if (!(portrait.Parent != null && portrait.Parent.GetChildren().Contains(portrait)))
                    slot.Add(portrait);
                if (font != null)
                    slot.Add(new UIOriginalFamilyCaption(data.Name, font));
            }
            DesktopFamily.RefreshSelection();
        }

        public void SetFamilyMember(int index)
        {
            if (RepresentFamily.Count <= index)
            {
                var member = (VMAvatar)vm.Context.CreateObjectInstance(0x7FD96B54, LotTilePos.OUT_OF_WORLD
                    
                    , Direction.EAST, false).BaseObject;

                RepresentFamily.Add(member);
            }

            var fam = RepresentFamily[index];

            fam.SetPosition(LotTilePos.FromBigTile(34, 31, 1) +
                    new LotTilePos((short)((((index + 1) / 2) % 2) * 8), (short)(((index % 2) * 2 - 1) * ((index + 1) / 2) * 10), 0), Direction.EAST, vm.Context, VMPlaceRequestFlags.AllowIntersection);
            var data = WIPFamily[index];
            //set this person's body and head

            fam.Name = data.Name;
            fam.Avatar.Skeleton = Content.Get().AvatarSkeletons.Get((data.Gender>1) ? "child.skel" : "adult.skel").Clone();
            fam.Avatar.BaseSkeleton = fam.Avatar.Skeleton.Clone();
            fam.Avatar.ReloadSkeleton();

            fam.SetPersonData(VMPersonDataVariable.PersonsAge, (short)((data.Gender > 1) ? 12 : 21));
            fam.InitBodyData(vm.Context);

            var oft = new Outfit() { TS1AppearanceID = data.Body + ".apr", TS1TextureID = data.BodyTex };
            var code = (data.Gender > 1) ? "u" : ((data.Gender == 0) ? "m" : "f");
            var hg = data.HandgroupTex;
            oft.LiteralHandgroup = new HandGroup()
            {
                TS1HandSet = true,
                LightSkin = new HandSet()
                {
                    LeftHand = new Hand()
                    {
                        Idle = new Gesture() { Name = "h" + code + "lo.apr", TexName = "huao" + hg },
                        Pointing = new Gesture() { Name = "h" + code + "lp.apr", TexName = "huap" + hg },
                        Fist = new Gesture() { Name = "h" + code + "lc.apr", TexName = "huac" + hg }
                    },
                    RightHand = new Hand()
                    {
                        Idle = new Gesture() { Name = "h" + code + "ro.apr", TexName = "huao" + hg },
                        Pointing = new Gesture() { Name = "h" + code + "rp.apr", TexName = "huap" + hg },
                        Fist = new Gesture() { Name = "h" + code + "rc.apr", TexName = "huac" + hg }
                    }
                }
            };

            fam.BodyOutfit = new FSO.SimAntics.Model.VMOutfitReference(oft);
            fam.HeadOutfit = new FSO.SimAntics.Model.VMOutfitReference(new Outfit() { TS1AppearanceID = data.Head+".apr", TS1TextureID = data.HeadTex });
        }

        private SimTemplateCreateInfo CASToNeighGen(CASFamilyMember x)
        {
            var code = ((x.Gender & 1) == 0) ? "m" : "f";
            code += (x.Gender > 1) ? "c" : "a";
            var ind = x.Body.IndexOf("_");
            var bodyType = x.Body.Substring(ind - 3, 3);
            code += bodyType;
            var info = new SimTemplateCreateInfo(code, x.SkinColor);
            info.Name = x.Name;
            info.Bio = x.Bio;
            // R246 bug fix: SimTemplateCreateInfo.PersonalityPoints is short[6] in the
            // generator's IFF person-data order [Nice, Active, Generous, Playful,
            // Outgoing, Neat] (MakePersonData writes them to pd[2..7], 0..1000 scale).
            // CASFamilyMember.Personality stays short[5] holding the CAS UI values in
            // DesktopCAS display order [Neat, Outgoing, Active, Playful, Nice]
            // (UIOriginalDesignChar.Values; the original CAS UI has only 5 sliders and
            // Generous/pd[4] was 0 in every original created record). The pre-fix
            // straight copy assigned the 5-slot array to the 6-slot field, so
            // MakePersonData's PersonalityPoints[5] read threw IndexOutOfRangeException
            // on the FIRST real family save. Remap at this generator boundary;
            // PrepareEdit still round-trips the 5-slot UI vector untouched.
            var points = new short[6];
            points[0] = x.Personality[4]; // Nice
            points[1] = x.Personality[2]; // Active
            points[2] = 0;                // Generous: no CAS slider in the original UI
            points[3] = x.Personality[3]; // Playful
            points[4] = x.Personality[1]; // Outgoing
            points[5] = x.Personality[0]; // Neat
            info.PersonalityPoints = points;

            info.BodyStringReplace[1] = x.Body + ",BODY=" + x.BodyTex;
            info.BodyStringReplace[2] = x.Head + ",HEAD-HEAD=" + x.HeadTex;

            var hand = (x.Gender > 1) ? "u" : ((x.Gender == 0) ? "m" : "f");
            info.BodyStringReplace[17] = "H" + hand + "LO,HAND=" + "huao" + x.HandgroupTex;
            info.BodyStringReplace[18] = "H" + hand + "RO,HAND=" + "huao" + x.HandgroupTex;
            info.BodyStringReplace[19] = "H" + hand + "LP,HAND=" + "huao" + x.HandgroupTex;
            info.BodyStringReplace[20] = "H" + hand + "RP,HAND=" + "huao" + x.HandgroupTex;
            info.BodyStringReplace[21] = "H" + hand + "LO,HAND=" + "huao" + x.HandgroupTex;
            info.BodyStringReplace[22] = "H" + hand + "RC,HAND=" + "huao" + x.HandgroupTex;
            return info;
        }

        public void ClearFamily()
        {
            var count = WIPFamily.Count;
            if (FamilyPanel != null) FamilyPanel.SecondName.CurrentText = "";
            if (DesktopFamily != null) DesktopFamily.FamilyNameBox.CurrentText = "";
            for (int i = count-1; i >= 0; i--)
                ModifySim(true, i);
        }

        public void SaveFamily()
        {
            var lastName = Original ? DesktopFamily.FamilyNameBox.CurrentText : FamilyPanel.SecondName.CurrentText;
            if (FamilySaveWriter != null) FamilySaveWriter(lastName, WIPFamily.ToArray());
            else SimitoneNeighbourGenerator.CreateFamily(lastName, WIPFamily.Count, WIPFamily.Select(CASToNeighGen).ToArray());
            SetFamilies();
            ClearFamily();
        }

        public void AcceptMember()
        {
            var mem = BuildMember();
            if (EditIndex == -1)
            {
                WIPFamily.Add(mem);
                SetFamilyMember(WIPFamily.Count - 1);
            } else
            {
                WIPFamily[EditIndex] = mem;
                SetFamilyMember(EditIndex);
            }
        }

        public CASFamilyMember BuildMember()
        {
            //build the object out of the contents of various menus
            var i = (int)DirectionUtils.PosMod(Math.Round(BodyPosition+8), ActiveBodies.Count);
            var j = (int)DirectionUtils.PosMod(Math.Round(HeadPosition+8), ActiveHeads.Count);
            string name, bio;
            short[] personality;
            if (Original)
            {
                name = DesktopCAS.NameBox.CurrentText;
                bio = DesktopCAS.BioEdit.Text;
                personality = DesktopCAS.Values.Select(x => (short)(x * 100)).ToArray();
            }
            else
            {
                name = CASPanel.FirstNameTextBox.CurrentText;
                bio = CASPanel.BioEdit.CurrentText;
                personality = CASPanel.Personalities.Select(x => (short)(x.Points * 100)).ToArray();
            }
            var sim = new CASFamilyMember()
            {
                Name = name,
                Bio = bio,
                Body = ActiveBodies[i],
                BodyTex = ActiveBodyTex[i],
                HandgroupTex = ActiveHandgroupTex[i],
                Head = ActiveHeads[j],
                HeadTex = ActiveHeadTex[j],
                Gender = (short)(((CurrentCode[0] == 'm') ? 0 : 1) | ((CurrentCode[1] == 'c') ? 2 : 0)),
                Personality = personality,
                SkinColor = CurrentSkin
            };
            return sim;
        }

        public override void GameResized()
        {
            base.GameResized();
            World?.GameResized();
            if (!Original)
            {
                BackButton.Position = new Vector2(25, ScreenHeight - 140);
                AcceptButton.Position = new Vector2(ScreenWidth - 140, ScreenHeight - 140);
            }
            FamilySimInterp = FamilySimInterp;
        }

        public override void PreDraw(UISpriteBatch batch)
        {
            base.PreDraw(batch);
            vm?.PreDraw();
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            base.Draw(batch);
        }

        public void CleanupLastWorld()
        {
            ClearDesktopNameFocus();
            DesktopCAS?.VitaSurface?.Release();
            if (vm == null) return;

            //clear our cache too, if the setting lets us do that
            TimedReferenceController.Clear();
            TimedReferenceController.Clear();

            vm.Context.Ambience.Kill();
            foreach (var ent in vm.Entities)
            { //stop object sounds
                var threads = ent.SoundThreads;
                for (int i = 0; i < threads.Count; i++)
                {
                    threads[i].Sound.RemoveOwner(ent.ObjectID);
                }
                threads.Clear();
            }
            vm.CloseNet(VMCloseNetReason.LeaveLot);
            GameFacade.Scenes.Remove(World);
            World.Dispose();
            vm.SuppressBHAVChanges();
            vm = null;
            World = null;
            Driver = null;
        }

        public void InitializeLot()
        {
            CleanupLastWorld();
            
            World = new FSO.LotView.World(GameFacade.GraphicsDevice);

            World.Opacity = 1;
            GameFacade.Scenes.Add(World);

            var globalLink = new VMTS1GlobalLinkStub();
            Driver = new VMServerDriver(globalLink);

            vm = new VM(new VMContext(World), Driver, new UIHeadlineRendererProvider());
            vm.ListenBHAVChanges();
            vm.Init();

            using (var file = new BinaryReader(File.OpenRead(Path.Combine(FSOEnvironment.ContentDir, "cas.fsov"))))
            {
                var marshal = new FSO.SimAntics.Marshals.VMMarshal();
                marshal.Deserialize(file);
                marshal.PlatformState = new VMTS1LotState();
                vm.Load(marshal);
                vm.Reset();
            }
            vm.Tick();

            vm.Context.Clock.Hours = 12;
            vm.MyUID = uint.MaxValue;
            var settings = GlobalSettings.Default;
            var myClient = new VMNetClient
            {
                PersistID = uint.MaxValue,
                RemoteIP = "local",
                AvatarState = new VMNetAvatarPersistState()
                {
                    Name = settings.LastUser ?? "",
                    DefaultSuits = new VMAvatarDefaultSuits(settings.DebugGender),
                    BodyOutfit = settings.DebugBody,
                    HeadOutfit = settings.DebugHead,
                    PersistID = uint.MaxValue,
                    SkinTone = (byte)settings.DebugSkin,
                    Gender = (short)(settings.DebugGender ? 1 : 0),
                    Permissions = FSO.SimAntics.Model.TSOPlatform.VMTSOAvatarPermissions.Admin,
                    Budget = 1000000
                }

            };

            var server = (VMServerDriver)Driver;
            server.ConnectClient(myClient);

            HeadAvatars = new VMAvatar[18];
            for (int i=0; i<18; i++)
            {
                HeadAvatars[i] = (VMAvatar)vm.Context.CreateObjectInstance(0x7FD96B54, LotTilePos.OUT_OF_WORLD, Direction.NORTH, true).BaseObject;
            }

            BodyAvatars = new VMAvatar[18];
            for (int i = 0; i < 18; i++)
            {
                BodyAvatars[i] = (VMAvatar)vm.Context.CreateObjectInstance(0x7FD96B54, LotTilePos.OUT_OF_WORLD, Direction.NORTH, true).BaseObject;
            }

            PopulateSimType("ma");
        }
    }

    public class CASFamilyMember
    {
        public string Name;
        public string SkinColor;
        public short Gender; //adult 0,1... child 2,3

        public string Head;
        public string HeadTex;
        public string Body;
        public string BodyTex;
        public string HandgroupTex;

        public short[] Personality = new short[5];
        public string Bio;
        public uint RefGUID; //for family editing. TODO.
        public int ReplaceIndex; //for editing existing sims
    }

    public enum UICASMode : int
    {
        ToNeighborhood = -1,
        FamilySelect = 0,
        FamilyEdit = 1,
        SimEdit = 2,
    }
}
