/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework;
using FSO.HIT;

using FSO.LotView;
using FSO.SimAntics;
using FSO.SimAntics.Entities;
using FSO.LotView.Components;
using Microsoft.Xna.Framework.Input;
using FSO.LotView.Model;
using FSO.SimAntics.Primitives;
using FSO.SimAntics.NetPlay.Model.Commands;
using FSO.SimAntics.Utils;
using FSO.Common;
using FSO.Client;
using FSO.Content;
using FSO.Client.Debug;
using Simitone.Client.UI.Screens;
using FSO.LotView.RC;
using Simitone.Client.UI.Panels.LotControls;
using FSO.Client.UI.Panels.LotControls;
using FSO.UI.Panels.LotControls;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using FSO.Files.Formats.IFF.Chunks;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// Generates pie menus when the player clicks on objects.
    /// </summary>
    public partial class UILotControl : UIContainer, ILotControl
    {
        private UIMouseEventRef MouseEvt;
        public bool MouseIsOn;
        public I3DRotate Rotate { get { return World.State.Cameras.Camera3D; } }

        private UIPieMenu PieMenu;

        private bool ShowTooltip;
        private bool TipIsError;
        private Texture2D RMBCursor;

        public FSO.SimAntics.VM vm;
        public FSO.LotView.World World { get; set; }
        public VMEntity ActiveEntity { get; set; }
        public int Budget { get
        {
            // NBR-04: on community/downtown lots the visiting family (travel
            // context) pays — fall back to it so InsufficientFunds stays
            // reachable instead of conceding infinite money.
            return vm?.TS1State?.CurrentFamily?.Budget
                ?? FSO.Content.Content.Get().Neighborhood?.GameState?.ActiveFamily?.Budget
                ?? int.MaxValue;
        }
        }
        public uint SelectedSimID
        {
            get
            {
                return (vm == null) ? 0 : vm.MyUID;
            }
        }
        public short ObjectHover;
        public bool InteractionsAvailable;
        public UIInteractionQueue Queue;

        public bool LiveMode = true;
        public bool PanelActive = false;
        public UILotControlTouchHelper Touch;
        public UIArchTouchHelper ArchTouch;

        public int WallsMode = 1;

        /// <summary>
        /// Fired when CustomControl (floor/wallpaper tool) is released via ESC key.
        /// </summary>
        public event Action OnCustomControlReleased;

        private int OldMX;
        private int OldMY;
        private bool FoundMe; //if false and avatar changes, center. Should center on join lot.

        public bool RMBScroll;
        private int RMBScrollX;
        private int RMBScrollY;

        private bool MMBScroll;
        private int MMBScrollX;
        private int MMBScrollY;

        //1 = near, 0.5 = med, 0.25 = far
        //"target" because we rescale the game target to fit this zoom level.
        public float TargetZoom = 1; 

        // NOTE: Blocking dialog system assumes that nothing goes wrong with data transmission (which it shouldn't, because we're using TCP)
        // and that the code actually blocks further dialogs from appearing while waiting for a response.
        // If we are to implement controlling multiple sims, this must be changed.
        private UIMobileDialog BlockingDialog;
        private UINeighborhoodSelectionPanel TS1NeighSelector;
        private ulong LastDialogID;

        private static uint GOTO_GUID = 0x000007C4;
        public VMEntity GotoObject;

        // ==== R244: native dynamic-cutaway state (tools/iff-dump/r244-cutaway-state) ====
        // History = membership model of cCutawaySet: FIFO capacity 3, duplicate
        // insert is a no-op that does NOT refresh recency, the oldest member is
        // evicted. Outside rooms ARE inserted now (native inserts any room id
        // < 0xfffb; the engine's r26 flag tracks outside-ness for the cursor gate).
        private List<uint> CutRooms = new List<uint>();
        public sbyte LastFloor = -1;
        public WorldRotation LastRotation = WorldRotation.TopLeft;
        private bool[] LastCuts; //the standing composition (also the all-true/all-false shortcut arrays)
        private int LastWallMode = -1; //invalidates the shortcut arrays
        private WorldZoom LastCutZoom; //zoom is part of the mask geometry; changes invalidate the mask cache
        private sbyte LastTouchFloor = -1; //touch-path (SoftwareKeyboard) change gate
        private WorldRotation LastTouchRot = WorldRotation.TopLeft;
        private HashSet<uint> LastTouchRooms;
        // R244: per-room mask cache owned here; invalidated on floor/rotation/zoom
        // changes, lot refresh (RefreshCut) and wall/room architecture changes.
        private FSO.LotView.Utils.CutawayMaskCache CutMasks = new FSO.LotView.Utils.CutawayMaskCache();
        private bool CutWallsWired;

        public UIObjectHolder ObjectHolder;
        public UICustomLotControl CustomControl;
        public UIQueryPanel QueryPanel;
        public UIPickupPanel PickupPanel;

        /// <summary>
        /// Creates a new UILotControl instance.
        /// </summary>
        /// <param name="vm">A SimAntics VM instance.</param>
        /// <param name="World">A World instance.</param>
        public UILotControl(FSO.SimAntics.VM vm, FSO.LotView.World World)
        {
            this.vm = vm;
            this.World = World;
            
            MouseEvt = this.ListenForMouse(new Microsoft.Xna.Framework.Rectangle(0, 0,
                GlobalSettings.Default.GraphicsWidth, GlobalSettings.Default.GraphicsHeight), OnMouse);

            Queue = new UIInteractionQueue(ActiveEntity, vm);
            this.Add(Queue);

            ObjectHolder = new UIObjectHolder(vm, World, this);
            Touch = new UILotControlTouchHelper(this);
            Add(Touch);
            ArchTouch = new UIArchTouchHelper(this);
            Add(ArchTouch);
            SetupQuery();


            RMBCursor = GetTexture(0x24B00000001); //exploreanchor.bmp

            vm.OnDialog += vm_OnDialog;
            vm.OnBreakpoint += Vm_OnBreakpoint;
        }

        public void SetupQuery()
        {
            /*UIContainer parent = null;
            if (QueryPanel?.Parent?.Parent != null)
            {
                parent = QueryPanel.Parent;
            }*/

            QueryPanel = new UIQueryPanel(World);
            QueryPanel.X = 0;
            QueryPanel.Y = -114;

            PickupPanel = new UIPickupPanel();
            PickupPanel.OnResponse += (resp) =>
            {
                if (resp) ObjectHolder.SellBack(null);
                else ObjectHolder.Cancel();
            };
        }

        public override void GameResized()
        {
            base.GameResized();
            MouseEvt.Region.Width = GlobalSettings.Default.GraphicsWidth;
            MouseEvt.Region.Height = GlobalSettings.Default.GraphicsHeight;

            //SetupQuery();
        }


        private void Vm_OnBreakpoint(VMEntity entity)
        {
            if (IDEHook.IDE != null) IDEHook.IDE.IDEBreakpointHit(vm, entity);
        }

        public static bool ShowSimanticsExceptions = true;

        internal static int OriginalDialogIconMode(FSO.SimAntics.Primitives.VMDialogOperand operand)
        {
            return operand == null ? -1 : (int)operand.IconMode;
        }

        internal static bool IsOriginalObjectDialog(FSO.SimAntics.Model.VMDialogInfo info)
        {
            return info != null && info.Operand != null && info.DialogID != 0;
        }

        internal static bool UsesAutomaticObjectDialogIcon(FSO.SimAntics.Model.VMDialogInfo info)
        {
            return IsOriginalObjectDialog(info)
                && info.Operand.IconMode == VMDialogIconMode.Automatic;
        }

        internal static bool TryParseOriginalNamedIcon(string value, out string command, out int argument)
        {
            command = null;
            argument = 0;
            if (string.IsNullOrEmpty(value)) return false;

            int split = value.IndexOf(' ');
            if (split < 0)
            {
                command = value.ToLowerInvariant();
                return command == "rel";
            }
            if (split == 0 || split == value.Length - 1) return false;
            command = value.Substring(0, split).ToLowerInvariant();
            return int.TryParse(value.Substring(split + 1), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out argument);
        }

        void vm_OnDialog(FSO.SimAntics.Model.VMDialogInfo info)
        {
            BindTutorialContext();
            if (info != null && ((info.DialogID == LastDialogID && info.DialogID != 0 && info.Block))) return;
            //return if same dialog as before, or not ours
            if ((info == null || info.Block) && BlockingDialog != null)
            {
                //cancel current dialog because it's no longer valid
                if (BlockingDialog is UIMobileAlert oldTutorial) TutorialPresenter?.Forget(oldTutorial);
                UIScreen.RemoveDialog(BlockingDialog);
                LastDialogID = 0;
                BlockingDialog = null;
            }
            if (info == null) return; //return if we're just clearing a dialog.

            if (!ShowSimanticsExceptions && info.Title == "SimAntics Exception!")
                return;

            var options = new UIAlertOptions
            {
                Title = info.Title,
                Message = info.Message,
                Width = 325 + (int)(info.Message.Length / 3.5f),
                Alignment = TextAlignment.Left,
                TextSize = 12
            };

            var b0Event = (info.Block) ? new ButtonClickDelegate(DialogButton0) : null;
            var b1Event = (info.Block) ? new ButtonClickDelegate(DialogButton1) : null;
            var b2Event = (info.Block) ? new ButtonClickDelegate(DialogButton2) : null;

            VMDialogType type = (info.Operand == null) ? VMDialogType.Message : info.Operand.Type;

            switch (type)
            {
                default:
                case VMDialogType.Message:
                    options.Buttons = new UIAlertButton[] { new UIAlertButton(UIAlertButtonType.OK, b0Event, info.Yes) };
                    break;
                case VMDialogType.YesNo:
                    options.Buttons = new UIAlertButton[]
                    {
                        new UIAlertButton(UIAlertButtonType.Yes, b0Event, info.Yes),
                        new UIAlertButton(UIAlertButtonType.No, b1Event, info.No),
                    };
                    break;
                case VMDialogType.YesNoCancel:
                    options.Buttons = new UIAlertButton[]
                    {
                        new UIAlertButton(UIAlertButtonType.Yes, b0Event, info.Yes),
                        new UIAlertButton(UIAlertButtonType.No, b1Event, info.No),
                        new UIAlertButton(UIAlertButtonType.Cancel, b2Event, info.Cancel),
                    };
                    break;
                case VMDialogType.TextEntry:
                    // EXP-08 fix card 1: the original single-line editor on
                    // desktop (the CAS-02 idiom) — the pet pen's adoption
                    // names the pet through this path (TextEntry writes the
                    // StackObject name; make_new_character copies it). Touch
                    // keeps the generic text-entry alert.
                    if (Parent is Simitone.Client.UI.Screens.TS1GameScreen tgsName && tgsName.Desktop
                        && IsOriginalObjectDialog(info))
                    {
                        var nameDlg = new Simitone.Client.UI.Panels.UIOriginalNameEntryDialog(
                            info.Title, info.Message, info.Yes, info.Cancel);
                        var nameCaller = info.Caller;
                        UIScreen.GlobalShowDialog(nameDlg, true);
                        nameDlg.OnResult += (text) =>
                        {
                            // native ObjectDialog: OK is the default command;
                            // cancel answers with code 2 (the engine's
                            // TextEntry case treats empty text as no-rename)
                            vm.SendCommand(new VMNetDialogResponseCmd
                            {
                                ActorUID = nameCaller.PersistID,
                                ResponseCode = (byte)(text == null ? 2 : 0),
                                ResponseText = text ?? ""
                            });
                            BlockingDialog = null;
                        };
                        return;
                    }
                    options.Buttons = new UIAlertButton[] { new UIAlertButton(UIAlertButtonType.OK, b0Event, info.Yes) };
                    options.TextEntry = true;
                    break;
                case VMDialogType.TS1TransformMe:
                {
                    // UI-35 (wave 11): the original form browser for the type-14
                    // TransformMe dialog (ENG-11 decode). Desktop original-UI path
                    // mounts the picker; confirm answers code 0 with the picked
                    // SAnimator outfit enum in ResponseText, cancel answers 2 —
                    // the engine's confirm/cancel law consumes exactly that
                    // (Temp0 = enum on both edges). Touch keeps the OK-alert
                    // stand-in (the confirm shape).
                    if (Parent is Simitone.Client.UI.Screens.TS1GameScreen tgsT
                        && tgsT.Desktop && IsOriginalObjectDialog(info))
                    {
                        var tDlg = new Simitone.Client.UI.Panels.UIOriginalTransformMeDialog(
                            info.Title, info.Message, info.Yes, info.Cancel,
                            info.Caller as FSO.SimAntics.VMAvatar);
                        UIScreen.GlobalShowDialog(tDlg, true);
                        var tCaller = info.Caller;
                        tDlg.OnResult += (formEnum) =>
                        {
                            vm.SendCommand(new VMNetDialogResponseCmd
                            {
                                ActorUID = tCaller.PersistID,
                                ResponseCode = (byte)(formEnum >= 0 ? 0 : 2),
                                ResponseText = tDlg.CurrentEnum.ToString()
                            });
                            BlockingDialog = null;
                        };
                        return;
                    }
                    options.Buttons = new UIAlertButton[] { new UIAlertButton(UIAlertButtonType.OK, b0Event, info.Yes) };
                    break;
                }
                case VMDialogType.NumericEntry:
                    if (!vm.TS1) goto case VMDialogType.TextEntry;
                    else goto case VMDialogType.TS1Neighborhood;
                case VMDialogType.TS1Vacation:
                case VMDialogType.TS1Neighborhood:
                case VMDialogType.TS1StudioTown:
                case VMDialogType.TS1Magictown:
                    TS1NeighSelector = new UINeighborhoodSelectionPanel((ushort)VMDialogPrivateStrings.TypeToNeighID[type]);
                    Parent.Add(TS1NeighSelector);
                    ((TS1GameScreen)Parent).Bg.Visible = true;
                    ((TS1GameScreen)Parent).LotControl.Visible = false;
                    TS1NeighSelector.OnHouseSelect += HouseSelected;
                    return;
                case VMDialogType.TS1PhoneBook:
                {
                    // R192: desktop uses the original CWinPhoneBook law
                    // (kPhoneBookBkg 620x356 + phoneicon + STR# 180 + the
                    // decoded list anchors); touch keeps the mobile alert.
                    Action<int> respond = (res) =>
                    {
                        vm.SendCommand(new VMNetDialogResponseCmd
                        {
                            ActorUID = info.Caller.PersistID,
                            ResponseCode = (byte)((res > 0) ? 1 : 0),
                            ResponseText = res.ToString()
                        });
                        BlockingDialog = null;
                    };
                    if (Parent is Simitone.Client.UI.Screens.TS1GameScreen tgs && tgs.Desktop)
                    {
                        var orig = new Simitone.Client.UI.Panels.UIOriginalPhoneBookDialog(
                            ((VMAvatar)info.Caller).GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.NeighborId), vm);
                        UIScreen.GlobalShowDialog(orig, true);
                        orig.OnResult += (r) => { orig.Close(); respond(r); };
                    }
                    else
                    {
                        var phone = new UICallNeighborAlert(((VMAvatar)info.Caller).GetPersonData(FSO.SimAntics.Model.VMPersonDataVariable.NeighborId), vm);
                        UIScreen.GlobalShowDialog(phone, true);
                        phone.OnResult += (r) => respond(r);
                    }
                    return;
                }

                case VMDialogType.TS1Spellbook:
                case VMDialogType.TS1Cookbook:
                    // CWinMagicBook law (native TryDialog 0xf1c7c/0xf1ce8): the
                    // modal book browser's result is discarded and the primitive
                    // returns TRUE. Desktop mounts the recovered original window;
                    // touch keeps the plain alert below.
                    if (Parent is Simitone.Client.UI.Screens.TS1GameScreen tgsBook && tgsBook.Desktop)
                    {
                        var book = new Simitone.Client.UI.Panels.UIOriginalMagicBookDialog();
                        var bookCaller = info.Caller;
                        UIScreen.GlobalShowDialog(book, true);
                        book.OnResult += (_) =>
                        {
                            vm.SendCommand(new VMNetDialogResponseCmd
                            {
                                ActorUID = bookCaller.PersistID,
                                ResponseCode = 0,
                                ResponseText = ""
                            });
                            BlockingDialog = null;
                        };
                        return;
                    }
                    break; // generic alert path (message + OK)

                case VMDialogType.TS1PetChoice:
                case VMDialogType.TS1Clothes:
                    var ts1categories = new string[] { "b", "f", "s", "l", "w", "h" };
                    var pet = type == VMDialogType.TS1PetChoice;
                    var stackObj = info.Caller.Thread.Stack.Last().StackObject;
                    
                    var skin = new UISelectSkinAlert(pet?null:(info.Caller as VMAvatar), pet?((stackObj as VMAvatar).IsCat?"cat":"dog"):ts1categories[info.Caller.Thread.TempRegisters[0]], vm);
                    BlockingDialog = skin;
                    UIScreen.GlobalShowDialog(skin, true);
                    skin.OnResult += (result) =>
                    {
                        vm.SendCommand(new VMNetDialogResponseCmd
                        {
                            ActorUID = info.Caller.PersistID,
                            ResponseCode = (byte)((result > -1)?1:0),
                            ResponseText = result.ToString()
                        });
                        BlockingDialog = null;
                    };
                    return;
            }

            // TS1's real ObjectDialog primitive always creates
            // cWinPictureDialog, including the common no-icon case. A nonzero
            // DialogID plus an operand distinguishes that path from the debug,
            // missing-object and generic-command messages which share this sink.
            bool originalPictureDialogKind = IsOriginalObjectDialog(info);
            bool tutorialOwner = originalPictureDialogKind && info.Caller != null &&
                info.Caller == vm.Context.TutorialObject && !FSOEnvironment.SoftwareKeyboard;
            bool tutorialGuid = originalPictureDialogKind && info.Caller?.Object.OBJ.GUID == 0xc3249a1d &&
                !FSOEnvironment.SoftwareKeyboard;
            bool tutorialCloseBox = !FSOEnvironment.SoftwareKeyboard && IsNonmodalTutorial(info);
            if (tutorialGuid) options.Title = "";
            // Nonmodal ObjectDialog replaces its primary bottom button with
            // the closebox. Continue and nonmodal are independent flag bits.
            if (tutorialCloseBox) options.Buttons = options.Buttons.Skip(1).ToArray();
            var alert = new UIMobileAlert(options, originalPictureDialogKind,
                tutorialOwner || tutorialCloseBox ? (((byte)info.Operand.Type & 0x80) != 0 ? 1.2 : 2.0) : (double?)null);
            try
            {
                ApplyOriginalDialogIcon(alert, info);
            }
            // Icon generation is decorative. A corrupt/missing thumbnail must
            // never strand a blocking VM primitive without an answerable dialog.
            catch { }

            // SetImage is part of ObjectDialog::SetupDialog. Finish it before
            // mounting/centering the window so the first visible frame does not
            // jump from the no-image geometry to the picture geometry.
            TrackTutorialDialog(alert, info);
            UIScreen.GlobalShowDialog(alert, !IsNonmodalTutorial(info));
            if (tutorialGuid)
            {
                alert.OriginalOpacityMultiplier = 200f / 255f;
                alert.InterpolatedAnimation = alert.InterpolatedAnimation;
            }
            if (tutorialOwner || tutorialCloseBox)
            {
                alert.TutorialEscapeResponse = type == VMDialogType.YesNoCancel ? (byte)2 :
                    type == VMDialogType.YesNo ? (byte)1 : (byte)0;
                alert.TutorialSpacePrimary = type != VMDialogType.YesNo && type != VMDialogType.YesNoCancel;
                alert.TutorialKeyResponse = code => { if (info.Block) DialogResponse(code); else alert.Close(); };
                // ENG-23: ESC on a tutorial lesson dialog runs the true cancel
                // (the owner's "cancel tutorial" tree + the owner kill — the
                // native cDDDSimsView::TSOnKeyDown 0x1b law), not a bare
                // response code the content can treat as a lesson exit.
                alert.TutorialEscapeCancel = TutorialEscapeCancel;
            }
            if (tutorialCloseBox) alert.AddTutorialCloseBox(tutorialOwner,
                () => { if (info.Block) DialogResponse(0); else alert.Close(); });
            if (tutorialOwner && alert.OriginalChrome)
            {
                alert.TutorialOpacity = tutorialGuid ? 200f / 255f : 1;
                TutorialPresenter?.Open(alert);
            }

            if (info.Block)
            {
                BlockingDialog = alert;
                LastDialogID = info.DialogID;
            }
        }

        private void ApplyOriginalDialogIcon(UIMobileAlert alert,
            FSO.SimAntics.Model.VMDialogInfo info)
        {
            if (!IsOriginalObjectDialog(info)) return;
            switch (info.Operand.IconMode)
            {
                case VMDialogIconMode.Automatic:
                    // ParseUIString may have selected a neighbor even when no
                    // live entity exists. Its selector takes precedence over
                    // the signed Stack Object fallback captured by SimAntics.
                    if (info.IconNeighborID >= 0) ApplyNeighborDialogIcon(alert, info.IconNeighborID);
                    else ApplyAutomaticEntityDialogIcon(alert, info.Icon);
                    break;
                case VMDialogIconMode.None:
                    break;
                case VMDialogIconMode.Neighbor:
                    ApplyNeighborDialogIcon(alert, info.IconNeighborID);
                    break;
                case VMDialogIconMode.Indexed:
                    ApplyPrivateDialogBitmap(alert, info.IconResource,
                        5000 + info.Operand.IconNameStringID);
                    break;
                case VMDialogIconMode.Named:
                    ApplyNamedDialogIcon(alert, info);
                    break;
                // The original dispatch has no branches for 5..7.
                default:
                    break;
            }
        }

        internal void ApplyOriginalDialogIconForProbe(UIMobileAlert alert,
            FSO.SimAntics.Model.VMDialogInfo info)
        {
            ApplyOriginalDialogIcon(alert, info);
        }

        private void ApplyAutomaticEntityDialogIcon(UIMobileAlert alert, VMEntity entity)
        {
            if (entity is VMGameObject)
            {
                var objects = entity.MultitileGroup.Objects;
                ObjectComponent[] objComps = new ObjectComponent[objects.Count];
                for (int i = 0; i < objects.Count; i++)
                    objComps[i] = (ObjectComponent)objects[i].WorldUI;
                var thumb = World.GetObjectThumb(objComps,
                    entity.MultitileGroup.GetBasePositions(), GameFacade.GraphicsDevice);
                // Product::DrawIcon's destination is exactly 120x120.
                alert.SetOwnedIcon(thumb, alert.OriginalChrome ? 120 : 256,
                    alert.OriginalChrome ? 120 : 256);
            }
            else if (entity is VMAvatar)
            {
                // PersonFinder returns a 45x45 selector picture. UIIconCache's
                // larger head source is shared, so ownership stays with it.
                var portrait = UIIconCache.GetObject(entity);
                alert.SetIcon(portrait, alert.OriginalChrome ? 45 : 256,
                    alert.OriginalChrome ? 45 : 256);
            }
        }

        private void ApplyNeighborDialogIcon(UIMobileAlert alert, short neighborID)
        {
            if (neighborID < 0) return;
            VMAvatar avatar = null;
            VMMultitileGroup temporary = null;
            try
            {
                avatar = vm.Context.ObjectQueries.Avatars
                    .OfType<VMAvatar>()
                    .FirstOrDefault(x => x.GetPersonData(
                        FSO.SimAntics.Model.VMPersonDataVariable.NeighborId) == neighborID);
                if (avatar == null)
                {
                    var neighbor = Content.Get().Neighborhood.GetNeighborByID(neighborID);
                    if (neighbor == null) return;
                    temporary = vm.Context.CreateObjectInstance(neighbor.GUID,
                        LotTilePos.OUT_OF_WORLD, Direction.NORTH, true);
                    avatar = temporary?.BaseObject as VMAvatar;
                }
                var portrait = avatar == null ? null : UIIconCache.GetObject(avatar);
                alert.SetIcon(portrait, alert.OriginalChrome ? 45 : 256,
                    alert.OriginalChrome ? 45 : 256);
            }
            finally
            {
                temporary?.Delete(vm.Context);
            }
        }

        private static void ApplyPrivateDialogBitmap(UIMobileAlert alert,
            GameIffResource resource, int resourceID)
        {
            if (resourceID < 0 || resourceID > ushort.MaxValue) return;
            var bmp = resource?.Get<BMP>((ushort)resourceID);
            var texture = bmp?.GetTexture(GameFacade.GraphicsDevice);
            if (texture != null)
            {
                // R210: the dialog's private BMP is the object's OWN original
                // data — register for the ui-total provenance walk.
                Simitone.Client.UI.Model.UIArtProvenance.NoteOriginal(texture, "dialog-private-bmp");
                alert.SetOwnedIcon(texture, texture.Width, texture.Height);
            }
        }

        private void ApplyNamedDialogIcon(UIMobileAlert alert,
            FSO.SimAntics.Model.VMDialogInfo info)
        {
            string command;
            int argument;
            if (!TryParseOriginalNamedIcon(info.IconName, out command, out argument)) return;
            switch (command)
            {
                case "gz":
                case "gzi":
                    var reference = UIOriginal.EnsureResolvedByID(argument);
                    var global = reference?.Get(GameFacade.GraphicsDevice);
                    if (global == null) return;
                    if (command == "gz")
                    {
                        var member = UIOriginal.ResourceNameForIDForProbe(argument);
                        if (member == null || global.Width < 4) return;
                        // "gz" alone selects the first horizontal quarter;
                        // the three-letter "gzi" command retains full bounds.
                        global = UIOriginal.Rect(member, 0, 0,
                            Math.Max(1, global.Width / 4), global.Height);
                    }
                    if (global != null)
                        alert.SetIcon(global, global.Width, global.Height);
                    break;
                case "my":
                    ApplyPrivateDialogBitmap(alert, info.IconResource, argument);
                    break;
                case "guid":
                    ApplyGuidDialogIcon(alert, unchecked((uint)argument));
                    break;
                case "rel":
                    ApplyRelationshipDialogIcon(alert);
                    break;
                case "job":
                    ApplyJobDialogIcon(alert, argument);
                    break;
            }
        }

        private void ApplyGuidDialogIcon(UIMobileAlert alert, uint guid)
        {
            VMMultitileGroup temporary = null;
            Texture2D texture = null;
            bool ownsTexture = false;
            try
            {
                temporary = vm.Context.CreateObjectInstance(guid,
                    LotTilePos.OUT_OF_WORLD, Direction.NORTH, true);
                var entity = temporary?.BaseObject;
                if (entity == null) return;
                texture = UIIconCache.GetObject(entity);
                // UIIconCache returns a newly decoded private BMP for ordinary
                // products, but GUID 0x7c4 is the process-wide Go Here CustomUI
                // texture. Never dispose that shared singleton.
                ownsTexture = entity is VMGameObject
                    && entity.Object.OBJ.GUID != 0x000007C4;
                // Product::ComposeBtnImage hands ObjectDialog an explicit 45px
                // rectangle. Product BMPs are two horizontal states: select
                // the first cell before composing, never squeeze both into it.
                if (entity is VMGameObject && texture != null)
                {
                    var source = LiveSubpanels.Catalog.UICatalogItem.ProductIconSource(
                        texture.Width, texture.Height, true, false);
                    var cell = FSO.Common.Utils.TextureUtils.Clip(
                        GameFacade.GraphicsDevice, texture, source);
                    if (cell != null)
                        alert.SetOwnedIcon(cell, alert.OriginalChrome ? 45 : 256,
                            alert.OriginalChrome ? 45 : 256);
                }
                else
                {
                    // Avatar heads are shared by UIIconCache.
                    alert.SetIcon(texture, alert.OriginalChrome ? 45 : 256,
                        alert.OriginalChrome ? 45 : 256);
                }
            }
            finally
            {
                if (ownsTexture) texture?.Dispose();
                temporary?.Delete(vm.Context);
            }
        }

        private void ApplyRelationshipDialogIcon(UIMobileAlert alert)
        {
            // The only shipped `rel` command selects the center fifth of the
            // active character's BMP_2003 relationship strip (200x40, five
            // 40x40 states). This is not a neighbor portrait or SimStub image.
            var selected = ActiveEntity as VMAvatar;
            if (selected == null) return;
            var bitmap = selected.Object.Resource.Get<BMP>(2003);
            Texture2D strip = null;
            try
            {
                strip = bitmap?.GetTexture(GameFacade.GraphicsDevice);
                if (strip == null || strip.Width < 5 || strip.Height < 1) return;
                int frameWidth = strip.Width / 5;
                var center = FSO.Common.Utils.TextureUtils.Clip(GameFacade.GraphicsDevice,
                    strip, new Rectangle(frameWidth * 2, 0, frameWidth, strip.Height));
                if (center != null)
                    alert.SetOwnedIcon(center, center.Width, center.Height);
            }
            finally
            {
                strip?.Dispose();
            }
        }

        private static void ApplyJobDialogIcon(UIMobileAlert alert, int jobID)
        {
            const int rowWidth = 84;
            const int rowHeight = 63;
            const int rowCount = 22;
            if (jobID == -1) jobID = 11;
            if (jobID < 0 || jobID >= rowCount) return;
            var texture = UIOriginal.Rect("cpanel\\Backgrounds\\JobIconMultiPopup.bmp",
                0, jobID * rowHeight, rowWidth, rowHeight);
            if (texture != null)
                alert.SetIcon(texture, texture.Width, texture.Height);
        }

        private void HouseSelected(int house)
        {
            if (ActiveEntity == null || TS1NeighSelector == null) return;
            vm.SendCommand(new VMNetDialogResponseCmd
            {
                ActorUID = ActiveEntity.PersistID,
                ResponseCode = (byte)((house > 0) ? 1 : 0),
                ResponseText = house.ToString()
            });
            Parent.Remove(TS1NeighSelector);
            TS1NeighSelector = null;
        }

        private void DialogButton0(UIElement button) { DialogResponse(0); }
        private void DialogButton1(UIElement button) { DialogResponse(1); }
        private void DialogButton2(UIElement button) { DialogResponse(2); }

        private void DialogResponse(byte code)
        {
            if (BlockingDialog == null || !(BlockingDialog is UIMobileAlert)) return;
            BlockingDialog.Close();
            var ma = (UIMobileAlert)BlockingDialog;
            LastDialogID = 0;
            vm.SendCommand(new VMNetDialogResponseCmd
            {
                ResponseCode = code,
                ResponseText = (ma.ResponseText == null) ? "" : ma.ResponseText
            });
            BlockingDialog = null;
        }

        private void OnMouse(UIMouseEventType type, UpdateState state)
        {
            if (!vm.Ready) return;

            if (type == UIMouseEventType.MouseOver)
            {
                if (QueryPanel.Mode == 1) QueryPanel.SetShown(false);
                MouseIsOn = true;
            }
            else if (type == UIMouseEventType.MouseOut)
            {
                MouseIsOn = false;
                GameFacade.Cursor.SetCursor(CursorType.Normal);
                Tooltip = null;
            }
            else if (type == UIMouseEventType.MouseDown)
            {
                if (!FSOEnvironment.SoftwareKeyboard)
                {
                    if (!LiveMode)
                    {
                        if (CustomControl != null) CustomControl.MouseDown(state);
                        else ObjectHolder.MouseDown(state);
                        return;
                    }
                }
                Touch.MiceDown.Add(state.CurrentMouseID);
            }
            else if (type == UIMouseEventType.MouseUp)
            {
                Touch.MiceDown.Remove(state.CurrentMouseID);
                if (!FSOEnvironment.SoftwareKeyboard)
                {
                    if (!LiveMode)
                    {
                        if (CustomControl != null) CustomControl.MouseUp(state);
                        else ObjectHolder.MouseUp(state);
                        return;
                    }
                }
                state.UIState.TooltipProperties.Show = false;
                state.UIState.TooltipProperties.Opacity = 0;
                ShowTooltip = false;
                TipIsError = false;
            }
        }

        public void SimulateMD(UpdateState state)
        {
            if (CustomControl != null) CustomControl.MouseDown(state);
            else ObjectHolder.MouseDown(state);
        }

        public void SimulateMU(UpdateState state)
        {
            if (CustomControl != null) CustomControl.MouseUp(state);
            else ObjectHolder.MouseUp(state);
        }

        public void AddModifier(UILotControlModifiers mod)
        {
            if (CustomControl != null)
                CustomControl.Modifiers |= mod;
        }

        public void RemoveModifier(UILotControlModifiers mod)
        {
            if (CustomControl != null)
                CustomControl.Modifiers &= ~mod;
        }

        public void ShowPieMenu(Point pt, UpdateState state)
        {
            if (!LiveMode)
            {
                /*
                if (CustomControl != null) CustomControl.MouseDown(state);
                else ObjectHolder.MouseDown(state);
                */
                if (FSOEnvironment.SoftwareKeyboard && ObjectHolder.Holding == null)
                {
                    ObjectHolder.MouseDown(state);
                }
                return;
            }
            if (PieMenu == null && ActiveEntity != null)
            {
                VMEntity obj;
                //get new pie menu, make new pie menu panel for it
                var tilePos = World.EstTileAtPosWithScroll(new Vector2(pt.X, pt.Y) / FSOEnvironment.DPIScaleFactor);

                LotTilePos targetPos = LotTilePos.FromBigTile((short)tilePos.X, (short)tilePos.Y, World.State.Level);
                if (vm.Context.SolidToAvatars(targetPos).Solid) targetPos = LotTilePos.OUT_OF_WORLD;

                GotoObject.SetPosition(targetPos, Direction.NORTH, vm.Context);

                var newHover = World.GetObjectIDAtScreenPos(pt.X,
                    pt.Y,
                    GameFacade.GraphicsDevice);

                ObjectHover = newHover;

                bool objSelected = ObjectHover > 0;
                if (objSelected || (GotoObject.Position != LotTilePos.OUT_OF_WORLD && ObjectHover <= 0))
                {
                    if (objSelected)
                    {
                        obj = vm.GetObjectById(ObjectHover);
                    }
                    else
                    {
                        obj = GotoObject;
                    }
                    if (obj is VMAvatar && state.CtrlDown)
                    {
                        //debug switch to avatar
                        vm.SendCommand(new VMNetChangeControlCmd()
                        {
                            TargetID = obj.ObjectID
                        });
                    }
                    else if (obj != null)
                    {
                        obj = obj.MultitileGroup.GetInteractionGroupLeader(obj);
                        if (obj is VMGameObject && ((VMGameObject)obj).Disabled > 0)
                        {
                            // R198: the five TSO disable branches used STR# 159
                            // ids 16/21/22/24/27 — ids that DO NOT EXIST in the
                            // TS1 table (9 English entries; the flags are only
                            // ever set by TSO netplay commands, dead on this
                            // port). The engine's TS1 answer to a zero-action
                            // object is the R129 reason ladder, so a disabled
                            // object routes there like any other empty pie.
                            var reason = DisabledObjectTooltipText(vm, obj, ActiveEntity);
                            ReasonsShown++;
                            ShowReasonTooltip(state, reason);
                        }
                        else
                        {
                            var menu = obj.GetPieMenu(vm, ActiveEntity, false, true);
                            if (menu.Count != 0)
                            {
                                HITVM.Get().PlaySoundEvent(UISounds.PieMenuAppear);
                                PieMenu = new UIPieMenu(menu, obj, ActiveEntity, this);
                                this.Add(PieMenu);
                                PieMenu.X = state.MouseState.X / FSOEnvironment.DPIScaleFactor;
                                PieMenu.Y = state.MouseState.Y / FSOEnvironment.DPIScaleFactor;
                                PieMenu.UpdateHeadPosition(state.MouseState.X, state.MouseState.Y);
                            }
                            else
                            {
                                // R125: the original's zero-interaction feedback — the
                                // classified STR# 159 reason replaces the dead click
                                var reason = ObjectTooltipReason(vm, obj, ActiveEntity);
                                ReasonsShown++;
                                ShowReasonTooltip(state, reason);
                            }
                        }
                    }

                }
                else
                {
                    ShowErrorTooltip(state, 0, true);
                }
            }
            else
            {
                if (PieMenu != null) PieMenu.RemoveSimScene();
                this.Remove(PieMenu);
                PieMenu = null;
            }
        }

        // ==== R198: the cDefaultTTWindow COLOR LAW (decoded this round,
        // tools/iff-dump/r198/r198-tooltip-law.md) ====
        // The engine owns ONE shared tooltip window (cTSWinMgrW95::
        // GetDefaultTTWindow @0x51c7d4 is the ONLY ctor caller), font face 1
        // size 7, white box. It carries two color slots: the normal slot
        // (cDefaultTTWindow::Init 0x3b9828 stores 0 = the default black pen)
        // and the error slot (0x3b9844-0x3b9860 = the palette lookup
        // (0xFF,0,0) = RED). SetColor @0x3b9770 ({0=normal, 1=error}) has
        // exactly ONE consumer: ProductButton::GetToolTipsWindow @0x20b8d4 —
        // RED when the player cannot afford the catalog product, normal
        // otherwise. The buy catalog additionally overrides its normal slot
        // with RGB(31,124,31) (cWinCatalog::Init 0x26b458, the R145 green).
        // cTool::SetToolTip @0x191c30 (the live-mode path these tooltips ride)
        // never recolors — the default pen is the law here. The error-red is
        // unreachable in this port (the catalog product tooltip was replaced
        // by the R124 description panel); the constant pins the law.
        public static readonly Color TooltipDefaultColor = Color.Black;
        public static readonly Color TooltipErrorColor = new Color(255, 0, 0);

        private void ShowErrorTooltip(UpdateState state, uint id, bool playSound, params string[] args)
        {
            if (playSound) HITVM.Get().PlaySoundEvent(UISounds.Error);
            state.UIState.TooltipProperties.Show = true;
            state.UIState.TooltipProperties.Color = TooltipDefaultColor;
            state.UIState.TooltipProperties.Opacity = 1;
            state.UIState.TooltipProperties.Position = new Vector2(state.MouseState.X,
                state.MouseState.Y);
            state.UIState.Tooltip = GameFacade.Strings.GetString("159", id.ToString(), args);
            state.UIState.TooltipProperties.UpdateDead = false;
            ShowTooltip = true;
            TipIsError = true;
        }

        // ==== R125: the ORIGINAL live-mode object hover tooltips (STR# 159) ====
        // Gate instrumentation.
        public static int CanonStringsLoaded = 0;
        public static int NamesShown = 0;
        public static int ReasonsShown = 0;

        /// The hover text for an entity whose interactions ARE available: its
        /// NAME. VMEntity.ToString already resolves it the original's way
        /// (MultitileGroup.Name -> CTSS[0] -> OBJD label) — the same string the
        /// avatar hover showed before R125, now shown for objects too.
        public static string ObjectHoverName(VMEntity obj)
        {
            NamesShown++;
            return obj.ToString();
        }

        /// R129: the ORIGINAL unavailability reason for a hovered entity, from
        /// UIText.iff STR# 159 'ObjectTTs' — the ENGINE ladder, decoded
        /// instruction-literally from the original PPC binary
        /// (cObjPickerTool::Release 0x1850c4 + the ObjSelector audience
        /// predicates 0x103870-0x104578; tools/iff-dump/r129/). Fixed priority
        /// over audience aggregates of the object's OWN TTAB (the engine's
        /// predicates never merge the global tree), Debug-flagged (0x80)
        /// entries skipped, and every aggregate requires >= 1 live entry.
        /// Engine-derived viewer map: selected person +1550 bit0 = cat,
        /// bit1 = dog; +1536 type in (0,18) = human. [3] is the pet-click
        /// self-hover; [8] is pet-only (pets take no user direction) — a
        /// human viewer's terminal default is [0]. The engine's game-level
        /// pet gate and its debug pet-control flag are constant on Complete
        /// data (disclosed in r129-objpicker-engine.md).
        /// R198: the disabled-object routing target. The TSO flags never fire on
        /// this port (netplay-only setters), and their old STR# 159 ids
        /// (16/21/22/24/27) do not exist in the 9-entry TS1 table — a disabled
        /// object is a zero-action object, and the engine answer is the R129
        /// reason ladder this delegates to (null viewer -> no tooltip, the
        /// engine's own behavior).
        public static int DisabledRouted = 0;
        public static string DisabledObjectTooltipText(VM vm, VMEntity obj, VMEntity viewerEntity)
        {
            DisabledRouted++;
            return ObjectTooltipReason(vm, obj, viewerEntity);
        }

        public static string ObjectTooltipReason(VM vm, VMEntity obj, VMEntity viewerEntity)
        {
            var viewer = viewerEntity as VMAvatar;
            Func<int, string> S = i => { CanonStringsLoaded++; return GameFacade.Strings.GetString("159", i.ToString()); };
            if (viewer == null) return null;                      // engine: no selected person -> no tooltip
            if (obj == viewerEntity && viewer.IsPet) return S(3); // engine modes 29/42: the pet hovering itself

            bool isCat = viewer.IsCat;      // person+1550 bit0
            bool isDog = viewer.IsDog;      // person+1550 bit1
            bool isHuman = !viewer.IsPet;   // r17: a human person is selected

            bool hasTable = obj.TreeTable != null;                // engine: hovered+284 != 0
            bool any = false, allNoChild = true, allNoAdult = true, anyAllowCats = false, anyAllowDogs = false;
            if (hasTable)
            {
                foreach (var e in obj.TreeTable.Interactions)
                {
                    if ((e.Flags & TTABFlags.Debug) > 0) continue; // the engine skips the 0x80 entry flag
                    any = true;
                    if ((e.Flags & TTABFlags.TS1NoChild) == 0) allNoChild = false;
                    if ((e.Flags & TTABFlags.TS1NoAdult) == 0) allNoAdult = false;
                    if ((e.Flags & TTABFlags.TS1AllowCats) > 0) anyAllowCats = true;
                    if ((e.Flags & TTABFlags.TS1AllowDogs) > 0) anyAllowDogs = true;
                }
            }
            // ObjSelector::AdultsOnly / ChildrenOnly / PetsOnly / DogsOnly / CatsOnly / PeopleOnly
            bool adultsOnly = any && allNoChild && !anyAllowCats && !anyAllowDogs;
            bool childrenOnly = any && allNoAdult && !anyAllowCats && !anyAllowDogs;
            bool petsOnly = any && allNoChild && allNoAdult;
            bool dogsOnly = petsOnly && !anyAllowCats;
            bool catsOnly = petsOnly && !anyAllowDogs;
            bool peopleOnly = any && !anyAllowCats && !anyAllowDogs;

            if (hasTable && (isHuman || isCat || isDog) && adultsOnly) return S(1); // rung 1 'adults'
            if (hasTable && !isHuman && childrenOnly) return S(2);                  // rung 2 'kids'
            if (hasTable && !isCat && dogsOnly) return S(4);                        // rung 3 'dogs'
            if (hasTable && !isDog && catsOnly) return S(5);                        // rung 4 'cats'
            if (hasTable && !isCat && !isDog && petsOnly) return S(6);              // rung 5 'pets'
            if (hasTable && (isCat || isDog) && peopleOnly) return S(7);            // rung 6 'people'
            if (isCat || isDog) return S(8);                                        // rung 7 'no user-directed'
            return S(0);                                                            // terminal default 'no actions'
        }

        /// R125: shows an already-classified STR# 159 reason (the shared channel
        /// and error sound of ShowErrorTooltip, with the exact string).
        private void ShowReasonTooltip(UpdateState state, string reason)
        {
            HITVM.Get().PlaySoundEvent(UISounds.Error);
            state.UIState.TooltipProperties.Show = true;
            state.UIState.TooltipProperties.Color = TooltipDefaultColor;
            state.UIState.TooltipProperties.Opacity = 1;
            state.UIState.TooltipProperties.Position = new Vector2(state.MouseState.X,
                state.MouseState.Y);
            state.UIState.Tooltip = reason;
            state.UIState.TooltipProperties.UpdateDead = false;
            ShowTooltip = true;
            TipIsError = true;
        }

        public void ClosePie()
        {
            if (PieMenu != null)
            {
                PieMenu.RemoveSimScene();
                Queue.PieMenuClickPos = PieMenu.Position;
                this.Remove(PieMenu);
                PieMenu = null;
            }
        }

        public override Rectangle GetBounds()
        {
            return new Rectangle(0, 0, GlobalSettings.Default.GraphicsWidth, GlobalSettings.Default.GraphicsHeight);
        }

        public void LiveModeUpdate(UpdateState state, bool scrolled)
        {
            if (MouseIsOn && !RMBScroll && ActiveEntity != null && !FSOEnvironment.SoftwareKeyboard)
            {

                if (state.MouseState.X != OldMX || state.MouseState.Y != OldMY)
                {
                    OldMX = state.MouseState.X;
                    OldMY = state.MouseState.Y;
                    var newHover = World.GetObjectIDAtScreenPos(state.MouseState.X,
                        state.MouseState.Y,
                        GameFacade.GraphicsDevice);

                    if (ObjectHover != newHover)
                    {
                        ObjectHover = newHover;
                        if (ObjectHover > 0)
                        {
                            var obj = vm.GetObjectById(ObjectHover);
                            if (obj != null)
                            {
                                // R125: leader-ize exactly like the pie path so the
                                // availability flag matches what a click would build
                                obj = obj.MultitileGroup.GetInteractionGroupLeader(obj);
                                var menu = obj.GetPieMenu(vm, ActiveEntity, false, true);
                                InteractionsAvailable = (menu.Count > 0);
                            }
                        }
                    }

                    if (!TipIsError) ShowTooltip = false;
                    if (ObjectHover > 0)
                    {
                        var obj = vm.GetObjectById(ObjectHover);
                        if (!TipIsError && obj != null)
                        {
                            // R125: the ORIGINAL hover tooltip — the entity's NAME when
                            // interactions are available, else the unavailability REASON
                            // from UIText.iff STR# 159 'ObjectTTs' (classified from the
                            // object's own TTAB flags — see ObjectTooltipReason).
                            obj = obj.MultitileGroup.GetInteractionGroupLeader(obj);
                            if (obj is VMGameObject && ((VMGameObject)obj).Disabled > 0)
                            {
                                var flags = ((VMGameObject)obj).Disabled;
                                if ((flags & VMGameObjectDisableFlags.ForSale) > 0)
                                {
                                    //for sale (TSO-era residual; TS1 lots never set it)
                                    var guid = obj.MasterDefinition?.GUID ?? obj.Object.OBJ.GUID;
                                    var item = Content.Get().WorldCatalog.GetItemByGUID(guid);

                                    var retailPrice = (int?)(item?.Price) ?? obj.MultitileGroup.Price;
                                    var salePrice = obj.MultitileGroup.SalePrice;
                                    ShowErrorTooltip(state, 22, false, "$" + retailPrice.ToString("##,#0"), "$" + salePrice.ToString("##,#0"));
                                    TipIsError = false;
                                }
                            }
                            else
                            {
                                string tip;
                                if (InteractionsAvailable) tip = ObjectHoverName(obj);
                                else { tip = ObjectTooltipReason(vm, obj, ActiveEntity); ReasonsShown++; }
                                state.UIState.TooltipProperties.Show = true;
                                state.UIState.TooltipProperties.Color = TooltipDefaultColor;
                                state.UIState.TooltipProperties.Opacity = 1;
                                state.UIState.TooltipProperties.Position = new Vector2(state.MouseState.X,
                                    state.MouseState.Y);
                                state.UIState.Tooltip = tip;
                                state.UIState.TooltipProperties.UpdateDead = false;
                                ShowTooltip = true;
                            }
                        }
                    }
                    if (!ShowTooltip)
                    {
                        state.UIState.TooltipProperties.Show = false;
                        state.UIState.TooltipProperties.Opacity = 0;
                    }
                }
            }
            else
            {
                ObjectHover = 0;
            }

            if (!scrolled)
            { //set cursor depending on interaction availability
                CursorType cursor;

                if (PieMenu == null && MouseIsOn)
                {
                    if (ObjectHover == 0)
                    {
                        cursor = CursorType.LiveNothing;
                    }
                    else
                    {
                        if (InteractionsAvailable)
                        {
                            if (vm.GetObjectById(ObjectHover) is VMAvatar) cursor = CursorType.LivePerson;
                            else cursor = CursorType.LiveObjectAvail;
                        }
                        else
                        {
                            cursor = CursorType.LiveObjectUnavail;
                        }
                    }
                }
                else
                {

                    cursor = CursorType.Normal;
                }

                CursorManager.INSTANCE.SetCursor(cursor);
            }

        }

        public void RefreshCut()
        {
            LastFloor = -1;
            LastWallMode = -1;
            LastTouchFloor = -1;
            //R244: a lot refresh rebuilds rooms — every cached mask is stale AND
            //the history's room ids are re-partitioned by the rebuild, so stale
            //ids must not survive (native clears the set on the same shape of
            //event: terrain-rebuild command 0x105, decode.md §1.2B).
            CutRooms.Clear();
            CutMasks.Invalidate();

            if (vm.Context.Blueprint != null && LastCuts != null)
            {
                vm.Context.Blueprint.Cutaway = LastCuts;
                vm.Context.Blueprint.Changes.SetFlag(BlueprintGlobalChanges.WALL_CUT_CHANGED);
            }

            //MouseCutRect = new Rectangle(0,0,0,0);
        }

        public void SetTargetZoom(WorldZoom zoom)
        {
            switch (zoom)
            {
                case WorldZoom.Near:
                    TargetZoom = 1f; break;
                case WorldZoom.Medium:
                    TargetZoom = 0.5f; break;
                case WorldZoom.Far:
                    TargetZoom = 0.25f; break;
            }
            LastZoom = World.State.Zoom;
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            //DrawLocalTexture(batch, World.State.Light.LightMap, new Rectangle(0,0, World.State.Light.LightMap.Width/3, World.State.Light.LightMap.Height/2), new Vector2());
            if (RMBScroll)
            {
                DrawLocalTexture(batch, RMBCursor, new Vector2(RMBScrollX - RMBCursor.Width / 2, RMBScrollY - RMBCursor.Height / 2));
            }
            base.Draw(batch);
        }

        private WorldZoom LastZoom;
        public override void Update(UpdateState state)
        {
            BindTutorialContext();
            TickTutorialPoller(state);
            base.Update(state);

            if (!vm.Ready || vm.Context.Architecture == null) return;

            //handling smooth scaled zoom
            var camType = World.State.Cameras.ActiveType;
            Touch._3D = camType != FSO.LotView.Utils.Camera.CameraControllerType._2D;
            if (World.State.Cameras.ActiveType == FSO.LotView.Utils.Camera.CameraControllerType._3D)
            {
                if (World.BackbufferScale != 1) World.BackbufferScale = 1;
                var s3d = World.State.Cameras.Camera3D;
                if (TargetZoom < -0.25f)
                {
                    TargetZoom -= (TargetZoom - 0.25f) * (1f - (float)Math.Pow(0.975f, 60f / FSOEnvironment.RefreshRate));
                }
                s3d.Zoom3D += ((9.75f - (TargetZoom - 0.25f) * 5.7f) - s3d.Zoom3D) / 10;
            }
            else if (World.State.Cameras.ActiveType == FSO.LotView.Utils.Camera.CameraControllerType._2D)
            {
                if (World.State.Zoom != LastZoom)
                {
                    //zoom has been changed by something else. inherit the value
                    SetTargetZoom(World.State.Zoom);
                    LastZoom = World.State.Zoom;
                }

                float BaseScale;
                WorldZoom targetZoom;
                if (TargetZoom < 0.5f)
                {
                    targetZoom = WorldZoom.Far;
                    BaseScale = 0.25f;
                }
                else if (TargetZoom < 1f)
                {
                    targetZoom = WorldZoom.Medium;
                    BaseScale = 0.5f;
                }
                else
                {
                    targetZoom = WorldZoom.Near;
                    BaseScale = 1f;
                }
                World.BackbufferScale = TargetZoom / BaseScale;
                if (World.State.Zoom != targetZoom) World.State.Zoom = targetZoom;
                WorldConfig.Current.SmoothZoom = false;
            }
            
            if (ActiveEntity == null || ActiveEntity.Dead || ActiveEntity.PersistID != SelectedSimID)
            {
                ActiveEntity = vm.Entities.FirstOrDefault(x => x is VMAvatar && x.PersistID == SelectedSimID); //try and hook onto a sim if we have none selected.
                //if (ActiveEntity == null) ActiveEntity = vm.Entities.FirstOrDefault(x => x is VMAvatar);

                if (!FoundMe && ActiveEntity != null)
                {
                    // Send change control command to ensure sim is properly selected
                    // This sets PersistID, registers in ObjectQueries, sets Global 3, and centers camera
                    vm.SendCommand(new VMNetChangeControlCmd() { TargetID = ActiveEntity.ObjectID });
                    FoundMe = true;
                }

                // Fallback: if no sim with expected PersistID, take control of any available avatar
                if (!FoundMe && ActiveEntity == null && vm.Context.ObjectQueries.Avatars.Count > 0)
                {
                    var fallbackAvatar = vm.Context.ObjectQueries.Avatars.FirstOrDefault();
                    if (fallbackAvatar != null)
                    {
                        // Send change control command to properly select this sim
                        // This sets PersistID, registers in ObjectQueries, sets Global 3, and centers camera
                        vm.SendCommand(new VMNetChangeControlCmd() { TargetID = fallbackAvatar.ObjectID });
                        FoundMe = true;
                    }
                }

                // Fallback for empty lots (no avatars): center on mailbox, any placed object, or lot center
                if (!FoundMe && vm.Context.ObjectQueries.Avatars.Count == 0)
                {
                    // Try to find mailbox first (GUIDs: 0xEF121974 or 0x1D95C9B0)
                    var landmark = vm.Entities.FirstOrDefault(x => x.Object.OBJ.GUID == 0xEF121974 || x.Object.OBJ.GUID == 0x1D95C9B0);

                    // If no mailbox, try to find any object that's placed on the lot (for community lots)
                    if (landmark == null)
                    {
                        landmark = vm.Entities.FirstOrDefault(x => x.Position != LotTilePos.OUT_OF_WORLD && x.Position.Level == 1);
                    }

                    if (landmark != null)
                    {
                        vm.Context.World.State.CenterTile = new Vector2(landmark.VisualPosition.X, landmark.VisualPosition.Y);
                    }
                    else
                    {
                        // Default to lot center
                        var lotSize = vm.Context.Architecture.Width;
                        vm.Context.World.State.CenterTile = new Vector2(lotSize / 2f, lotSize / 2f);
                    }
                    vm.Context.World.State.ScrollAnchor = null;
                    FoundMe = true;
                }

                Queue.QueueOwner = ActiveEntity;
            }

            if (GotoObject == null) GotoObject = vm.Context.CreateObjectInstance(GOTO_GUID, LotTilePos.OUT_OF_WORLD, Direction.NORTH, true).Objects[0];


            // Original TS1 marker: one selected Sim, a shared real-time spin phase,
            // native mood color and native assets. Selection does not accelerate it.
            foreach (VMAvatar avatar in vm.Context.ObjectQueries.Avatars)
            {
                if (avatar.Avatar == null) continue;
                var isActive = (avatar == ActiveEntity);
                avatar.Avatar.TS1PlumbBobVisible = isActive;
                avatar.Avatar.TS1PlumbBobMood = avatar.GetMotiveData(FSO.SimAntics.Model.VMMotive.Mood);
                avatar.Avatar.TS1PlumbBobSeconds = state.Time.TotalGameTime.TotalSeconds;
                avatar.Avatar.HeadObject = null;
                if (!isActive && avatar.GetValue(FSO.SimAntics.Model.VMStackObjectVariable.Category) == 87)
                {
                    avatar.Avatar.HeadObject = Content.Get().RCMeshes.Get("star.fsom");
                    avatar.Avatar.HeadObjectRotation += 3f / FSOEnvironment.RefreshRate;
                }
            }
            /*
            if (ActiveEntity != null && BlockingDialog != null)
            {
                //are we still waiting on a blocking dialog? if not, cancel.
                if (ActiveEntity.Thread != null && (ActiveEntity.Thread.BlockingState == null || !(ActiveEntity.Thread.BlockingState is VMDialogResult)))
                {
                    BlockingDialog.Close();
                    LastDialogID = 0;
                    BlockingDialog = null;
                }
            }*/

            if (Visible)
            {
                if (ShowTooltip) state.UIState.TooltipProperties.UpdateDead = false;

                bool scrolled = false;

                World.State.Cameras.CameraFirstPerson.CaptureMouse = true;

                if (RMBScroll)
                {
                    World.State.ScrollAnchor = null;
                    Vector2 scrollBy = new Vector2();
                    if (state.TouchMode)
                    {
                        scrollBy = new Vector2(RMBScrollX - state.MouseState.X, RMBScrollY - state.MouseState.Y);
                        RMBScrollX = state.MouseState.X;
                        RMBScrollY = state.MouseState.Y;
                        scrollBy /= 128f;
                        scrollBy /= FSOEnvironment.DPIScaleFactor;
                    }
                    else
                    {
                        scrollBy = new Vector2(state.MouseState.X - RMBScrollX, state.MouseState.Y - RMBScrollY);
                        scrollBy *= 0.0005f;

                        var angle = (Math.Atan2(state.MouseState.X - RMBScrollX, (RMBScrollY - state.MouseState.Y) * 2) / Math.PI) * 4;
                        angle += 8;
                        angle %= 8;

                        CursorType type = CursorType.ArrowUp;
                        switch ((int)Math.Round(angle))
                        {
                            case 0: type = CursorType.ArrowUp; break;
                            case 1: type = CursorType.ArrowUpRight; break;
                            case 2: type = CursorType.ArrowRight; break;
                            case 3: type = CursorType.ArrowDownRight; break;
                            case 4: type = CursorType.ArrowDown; break;
                            case 5: type = CursorType.ArrowDownLeft; break;
                            case 6: type = CursorType.ArrowLeft; break;
                            case 7: type = CursorType.ArrowUpLeft; break;
                        }
                        GameFacade.Cursor.SetCursor(type);
                    }
                    World.Scroll(scrollBy * (60f / FSOEnvironment.RefreshRate));
                    scrolled = true;
                }
                if (MouseIsOn)
                {
                    if (state.MouseState.RightButton == ButtonState.Pressed)
                    {
                        if (RMBScroll == false)
                        {
                            RMBScroll = true;
                            RMBScrollX = state.MouseState.X;
                            RMBScrollY = state.MouseState.Y;
                        }
                    }
                    else
                    {
                        if (!scrolled && GlobalSettings.Default.EdgeScroll && !state.TouchMode) scrolled = World.TestScroll(state);
                    }
                }

                if (state.MouseState.RightButton != ButtonState.Pressed)
                {
                    if (RMBScroll)
                    {
                        GameFacade.Cursor.SetCursor(CursorType.Normal);
                        // Check if it was a click (not a drag) on a family member Sim
                        var deltaX = Math.Abs(state.MouseState.X - RMBScrollX);
                        var deltaY = Math.Abs(state.MouseState.Y - RMBScrollY);
                        if (deltaX < 5 && deltaY < 5 && LiveMode && ActiveEntity != null)
                        {
                            // It was a click - check if clicking on a household Sim
                            var clickedObjId = World.GetObjectIDAtScreenPos(RMBScrollX, RMBScrollY, GameFacade.GraphicsDevice);
                            if (clickedObjId > 0)
                            {
                                var clickedObj = vm.GetObjectById(clickedObjId);
                                if (clickedObj is VMAvatar && vm.TS1State.CurrentFamily?.RuntimeSubset.Contains(clickedObj.Object.OBJ.GUID) == true)
                                {
                                    // Switch to this family member
                                    vm.SendCommand(new VMNetChangeControlCmd() { TargetID = clickedObj.ObjectID });
                                    // Update local state immediately so interactions work right away
                                    ActiveEntity = clickedObj;
                                    Queue.QueueOwner = ActiveEntity;
                                    HITVM.Get().PlaySoundEvent(UISounds.Click);
                                }
                            }
                        }
                    }
                    RMBScroll = false;
                }

                // Middle mouse button handling for camera rotation
                if (state.MouseState.MiddleButton == ButtonState.Pressed)
                {
                    if (!MMBScroll)
                    {
                        MMBScroll = true;
                        MMBScrollX = state.MouseState.X;
                        MMBScrollY = state.MouseState.Y;
                    }
                }
                else
                {
                    if (MMBScroll)
                    {
                        // Check if it was a click (not a drag)
                        var deltaX = Math.Abs(state.MouseState.X - MMBScrollX);
                        var deltaY = Math.Abs(state.MouseState.Y - MMBScrollY);
                        if (deltaX < 5 && deltaY < 5)
                        {
                            // It was a click - rotate camera 90 degrees clockwise
                            World.State.Rotation = (WorldRotation)(((int)World.State.Rotation + 1) % 4);
                            HITVM.Get().PlaySoundEvent(UISounds.Click);
                        }
                    }
                    MMBScroll = false;
                }

                if (!LiveMode && PieMenu != null)
                {
                    PieMenu.RemoveSimScene();
                    this.Remove(PieMenu);
                    PieMenu = null;
                }

                if (state.NewKeys.Contains(Keys.F11))
                {
                    var utils = new FSO.SimAntics.Test.CollisionTestUtils();
                    utils.VerifyAllCollision(vm);
                }

                if (state.NewKeys.Contains(Keys.F8))
                {
                    UIMobileAlert alert = null;
                    alert = new UIMobileAlert(new UIAlertOptions()
                    {
                        Title = "Debug Lot Thumbnail",
                        Message = "Arch Value: "+VMArchitectureStats.GetArchValue(vm.Context.Architecture),
                        Buttons = UIAlertButton.Ok((btn) => UIScreen.RemoveDialog(alert))
                    }, true);
                    // The roofless callback is optional. This debug dialog only
                    // displays the normal thumbnail, so requesting and then
                    // abandoning a second decimated texture leaked it.
                    var thumb = World.GetLotThumb(GameFacade.GraphicsDevice, null);
                    thumb = FSO.Common.Utils.TextureUtils.Decimate(thumb, GameFacade.GraphicsDevice, 2, false);
                    // Keep the port-only debug thumbnail inside the same desktop
                    // picture-dialog canvas; touch retains its established size.
                    int debugIconWidth = alert.OriginalChrome ? 120 : thumb.Width;
                    int debugIconHeight = alert.OriginalChrome ? 120 : thumb.Height;
                    alert.SetOwnedIcon(thumb, debugIconWidth, debugIconHeight);
                    UIScreen.GlobalShowDialog(alert, true);
                }
                if (LiveMode) LiveModeUpdate(state, scrolled);
                else if (CustomControl != null)
                {
                    // ESC cancels floor/wallpaper/wall tools
                    if (state.KeyboardState.IsKeyDown(Keys.Escape))
                    {
                        CustomControl.Release();
                        CustomControl = null;
                        OnCustomControlReleased?.Invoke();
                    }
                    else
                    {
                        if (FSOEnvironment.SoftwareKeyboard) CustomControl.MousePosition = new Point(UIScreen.Current.ScreenWidth / 2, UIScreen.Current.ScreenHeight / 2);
                        else
                        {
                            CustomControl.Modifiers = 0;
                            if (state.CtrlDown) CustomControl.Modifiers |= UILotControlModifiers.CTRL;
                            if (state.ShiftDown) CustomControl.Modifiers |= UILotControlModifiers.SHIFT;
                            CustomControl.MousePosition = state.MouseState.Position;
                        }
                        CustomControl.Update(state, scrolled);
                    }
                }
                else ObjectHolder.Update(state, scrolled);

                //set cutaway around mouse
                UpdateCutaway(state);

                if (RMBScrollX == int.MinValue) Dummy(); //cannon fodder for mono AOT compilation: never called but gives these constructors a meaning in life
            }
        }

        private void Dummy()
        {
            CustomControl = new UIWallPlacer(vm, World, this, new List<int>());
            CustomControl = new UIFloorPainter(vm, World, this, new List<int>());
            CustomControl = new UIWallPainter(vm, World, this, new List<int>());
            CustomControl = new UIGrassPaint(vm, World, this, new List<int>());
            CustomControl = new UIRoofer(vm, World, this, new List<int>());
            CustomControl = new UITerrainFlatten(vm, World, this, new List<int>());
            CustomControl = new UITerrainRaiser(vm, World, this, new List<int>());
        }

        private void UpdateCutaway(UpdateState state)
        {
            if (vm.Context.Blueprint == null) return;
            WireCutMaskInvalidation();
            World.State.DynamicCutaway = (WallsMode == 1);

            // WallsMode 0 (walls down: everything cut) and 2/3 (walls up, roof:
            // nothing cut) keep their byte-identical shortcut arrays — assigned
            // once per wall-mode change, never recomposed.
            if (LastWallMode != WallsMode)
            {
                if (WallsMode == 0) //walls down
                {
                    LastCuts = new bool[vm.Context.Architecture.Width * vm.Context.Architecture.Height];
                    vm.Context.Blueprint.Cutaway = LastCuts;
                    vm.Context.Blueprint.Changes.SetFlag(BlueprintGlobalChanges.WALL_CUT_CHANGED);
                    for (int i = 0; i < LastCuts.Length; i++) LastCuts[i] = true;
                }
                else if (WallsMode == 1)
                {
                    // native SetDynamicCutaway clears the room history on any
                    // toggle of the dynamic flag (decode.md §1.2B).
                    CutRooms.Clear();
                    CutMasks.Invalidate();
                }
                else //walls up or roof
                {
                    LastCuts = new bool[vm.Context.Architecture.Width * vm.Context.Architecture.Height];
                    vm.Context.Blueprint.Cutaway = LastCuts;
                    vm.Context.Blueprint.Changes.SetFlag(BlueprintGlobalChanges.WALL_CUT_CHANGED);
                }
                LastWallMode = WallsMode;
            }

            if (WallsMode != 1) return;

            //view changes invalidate the cached per-room masks; a floor change
            //also clears the history (native SetLevel idiom, decode.md §1.2A)
            NoteCutawayViewChange();

            // R244 touch residual: the SoftwareKeyboard path keeps its existing
            // all-indoor-rooms-on-view-floor behavior (GenerateRoomCut mask law)
            // UNCHANGED this round; the native composition below is desktop-only.
            if (FSOEnvironment.SoftwareKeyboard)
            {
                var finalRooms = new HashSet<uint>();
                foreach (var room in vm.Context.RoomInfo)
                {
                    if (!room.Room.IsOutside && room.Room.Floor == World.State.Level-1) finalRooms.Add(room.Room.RoomID);
                }
                if (LastTouchFloor != World.State.Level || LastTouchRot != World.State.CutRotation
                    || LastTouchRooms == null || !finalRooms.SetEquals(LastTouchRooms))
                {
                    LastTouchFloor = World.State.Level;
                    LastTouchRot = World.State.CutRotation;
                    LastTouchRooms = finalRooms;
                    LastCuts = VMArchitectureTools.GenerateRoomCut(vm.Context.Architecture, World.State.Level, World.State.CutRotation, finalRooms);
                    vm.Context.Blueprint.Cutaway = LastCuts;
                    vm.Context.Blueprint.Changes.SetFlag(BlueprintGlobalChanges.WALL_CUT_CHANGED);
                }
                return;
            }

            // suppression = the port's freeze conditions: the RMB camera scroll
            // is active (the native EdgeDetectScroller waiting-for-click state)
            // or the mouse is off the lot control (the native MouseTrack
            // viewport gate). Inserts stop; the compose stays live so the
            // person/standing-history inputs keep driving the mask.
            var mousePhysical = (new Vector2(state.MouseState.X, state.MouseState.Y) / FSOEnvironment.DPIScaleFactor).ToPoint();
            bool suppressed = RMBScroll || !MouseIsOn;
            UpdateCutawayDynamic(mousePhysical, suppressed);
        }

        /// <summary>
        /// R244: the whole desktop dynamic-mode pipeline — view-change
        /// lifecycle, the native MouseTrack(phase 0) history insert plus the
        /// DoDynamicCutaway compose — in one entry point shared with the
        /// uicutaway autotest, so the check drives exactly the production path.
        /// </summary>
        internal void UpdateCutawayDynamic(Point cursorScreen, bool suppressed)
        {
            if (vm?.Context?.Blueprint == null || vm.Context.Architecture == null) return;
            if (WallsMode != 1 || FSOEnvironment.SoftwareKeyboard) return;
            NoteCutawayViewChange();
            if (!suppressed)
            {
                // native MouseTrack: an off-lot cursor runs ResetDynamicCutaway
                // and RETURNS — the reset is the whole update (decode.md §1.1
                // step 5); recomposing the standing history afterwards would
                // immediately defeat the reset
                var bp = vm.Context.Blueprint;
                var tile = World.EstTileAtPosWithScroll(new Vector2(cursorScreen.X, cursorScreen.Y));
                int tx = (int)tile.X, ty = (int)tile.Y;
                if (tx < 1 || ty < 1 || tx > bp.Width - 2 || ty > bp.Height - 2)
                {
                    ResetCutawayMatrix();
                    return;
                }
                DriveCutawayHistory(cursorScreen);
            }
            CommitCutaway(BuildCutawayInputs(cursorScreen, suppressed));
        }

        /// <summary>
        /// R244 view-change lifecycle: any floor/rotation/zoom change
        /// invalidates the cached per-room masks, and a FLOOR change
        /// additionally clears the room history — the native SetLevel/
        /// ScrollToTile idiom (decode.md §1.2A: disable the old floor's
        /// cutaway, clear, reset, swap floor, recompute). A rotation change
        /// alone does NOT clear (native DoCommand 0xe4 keeps the set). Runs
        /// inside the shared production entry point so the uicutaway battery
        /// drives the exact same lifecycle.
        /// </summary>
        internal void NoteCutawayViewChange()
        {
            if (LastFloor == World.State.Level && LastRotation == World.State.CutRotation && LastCutZoom == World.State.Zoom) return;
            bool floorChanged = LastFloor != World.State.Level;
            LastFloor = World.State.Level;
            LastRotation = World.State.CutRotation;
            LastCutZoom = World.State.Zoom;
            CutMasks.Invalidate();
            if (floorChanged) CutRooms.Clear(); //the old floor's room ids must not survive the swap
        }

        private FSO.LotView.Utils.CutawayViewInputs BuildCutawayInputs(Point cursorScreen, bool suppressed)
        {
            var viewport = GameFacade.GraphicsDevice.Viewport;
            // the native main-animation-buffer bounds test: the lot viewport in
            // LOGICAL pixels — the same units as CursorScreenPos
            // (MouseState/DPIScaleFactor) — so the cursor-in-buffer gate is
            // DPI-independent (identical to the physical viewport at DPI 1)
            float dpi = FSOEnvironment.DPIScaleFactor;
            var view = new FSO.LotView.Utils.CutawayViewInputs
            {
                Floor = World.State.Level,
                Rotation = World.State.CutRotation,
                Zoom = World.State.Zoom,
                // effective on-screen sprite scale: the engine only uses it for the
                // cursor Y adjustment and the cache key; the mask algebra cancels it
                PreciseZoom = World.State.PreciseZoom * World.BackbufferScale,
                BufferBounds = new Rectangle(0, 0, (int)(viewport.Width / dpi), (int)(viewport.Height / dpi)),
                CursorScreenPos = cursorScreen,
                CursorSuppressed = suppressed,
                // the port's own mouse->tile projection (the same path the old
                // cursor math used), in BufferBounds pixels
                ScreenToTile = pos => World.EstTileAtPosWithScroll(new Vector2(pos.X, pos.Y)),
                HistoryRooms = CutRooms,
                DynamicEnabled = true,
            };
            AppendPersonInputs(view);
            // UI-25 item 2: native slot-18 AdjustCutawayForTool, cMoveTool leg
            // (0x174100): while an object is held (ObjectHolder.Holding = the
            // native tool+0x24 grab gate; CursorTiles = the picked-object tile
            // list), the dragged footprint is marked into the mask. The phantom
            // tiles track the drag through VisualPosition (their VM Position
            // freezes at pickup; MoveSelected only moves SetVisualPosition);
            // native's +0xf4>>4 is the big tile, and the tool floor tracks the
            // view floor (MoveSelected places at World.State.Level) — the
            // domain the engine helper gates. The PIP builder passes its own
            // inputs WITHOUT this hook: native save-NULLs the tool global
            // around the secondary render (0x1c0e48/0x1c1714).
            var holding = ObjectHolder?.Holding;
            if (holding?.CursorTiles != null)
            {
                view.AdjustCutawayForTool = (corner, mask) =>
                    FSO.LotView.Utils.CutawayMatrix.AdjustForDrag(vm.Context.Blueprint, mask,
                        holding.CursorTiles.Select(t => new Point(
                            (int)Math.Floor(t.VisualPosition.X),
                            (int)Math.Floor(t.VisualPosition.Y))));
            }
            return view;
        }

        /// <summary>
        /// R244: commit only on a real difference (CutawayMatrix.Differs) —
        /// replaces the old recut/notableChange heuristics.
        /// </summary>
        private void CommitCutaway(FSO.LotView.Utils.CutawayViewInputs view)
        {
            var composed = FSO.LotView.Utils.CutawayMatrix.ComposeDynamic(vm.Context.Blueprint, view, CutMasks);
            if (!FSO.LotView.Utils.CutawayMatrix.Differs(composed, vm.Context.Blueprint.Cutaway)) return;
            LastCuts = composed;
            vm.Context.Blueprint.Cutaway = composed;
            vm.Context.Blueprint.Changes.SetFlag(BlueprintGlobalChanges.WALL_CUT_CHANGED);
        }

        /// <summary>
        /// Native DoDynamicCutaway mode-2 (LIVE) person branch inputs: live mode
        /// only; the tracked person first, else the selected person. The port's
        /// honest equivalents are the follow-Sim camera anchor
        /// (World.State.ScrollAnchor — set by the tracking crosshair, cleared by
        /// manual camera input) for the native Animator tracked-object global,
        /// and ActiveEntity (the plumbob sim, vm.MyUID) for
        /// ObjectModule::GetSelectedPerson. Floor gating, the &lt;0xfffb room
        /// validity gate and the outside-person rectangle are the engine's job
        /// (CutawayMatrix.ComposeDynamic); only real on-lot positions pass.
        /// </summary>
        private void AppendPersonInputs(FSO.LotView.Utils.CutawayViewInputs view)
        {
            if (!LiveMode) return; //native GetMode()==2 gate: LIVE mode only
            var person = ResolvePerson();
            if (person == null || person.Position == LotTilePos.OUT_OF_WORLD) return;
            var room = vm.Context.GetRoomAt(LotTilePos.FromBigTile(person.Position.TileX, person.Position.TileY, person.Position.Level));
            view.PersonRoomId = room;
            view.PersonFloor = person.Position.Level;
            view.PersonOutside = vm.Context.RoomInfo[room].Room.IsOutside;
            //native person+0xfc/+0xfd: the avatar's tile — the source of the
            //decoded outside-person k-probe rectangle (decode.md §4)
            view.PersonTile = new Point(person.Position.TileX, person.Position.TileY);
        }

        internal FSO.SimAntics.VMEntity ResolvePerson()
        {
            var anchor = World.State.ScrollAnchor; //tracked: the follow-Sim camera anchor
            if (anchor != null)
            {
                var tracked = vm.Entities.FirstOrDefault(x => x.WorldUI == anchor);
                if (tracked != null) return tracked;
            }
            return ActiveEntity; //selected person
        }

        /// <summary>
        /// R244 MouseTrack(phase 0) equivalent — decode.md §1.1: insert the room
        /// under the cursor AND the room found screen-v "behind" it (probe walk
        /// in half-wall-height pixel steps bounded by half the story pixel
        /// height; out-of-world probes are skipped). An off-lot cursor runs the
        /// ResetDynamicCutaway path instead (matrix cleared, history survives).
        /// Returns true if the history membership changed.
        /// </summary>
        internal bool DriveCutawayHistory(Point cursorScreen)
        {
            var bp = vm.Context.Blueprint;
            var tile = World.EstTileAtPosWithScroll(new Vector2(cursorScreen.X, cursorScreen.Y));
            int tx = (int)tile.X, ty = (int)tile.Y;
            //off-lot = world bounds inset by 1 (native [x0+1..x1-1]x[y0+1..y1-1])
            if (tx < 1 || ty < 1 || tx > bp.Width - 2 || ty > bp.Height - 2)
            {
                ResetCutawayMatrix();
                return false;
            }
            var level = World.State.Level;
            var front = vm.Context.GetRoomAt(LotTilePos.FromBigTile((short)tx, (short)ty, level));
            var behind = front;
            //probe walk: screen-down (toward the camera) in native dv =
            //viewer+0x60/2 = (8 << zoom)/2 = 4 << zoom px steps (docommand.txt
            //0x1d689c pins +0x60 = 8<<zoom; the port zoom bucket 1=Far/2=Medium/
            //3=Near is the native zoom index), while the probed room equals the
            //cursor room, bounded by half the story pixel height
            //((58<<(zoom-1))/2 native); the port renders world pixels at
            //PreciseZoom*BackbufferScale screen px per world px.
            var zoom = (int)World.State.Zoom; //1=Far, 2=Medium, 3=Near (≡ native zoom index)
            float scale = World.State.PreciseZoom * World.BackbufferScale;
            float step = (4 << zoom) * scale;
            float bound = (29 << (zoom - 1)) * scale;
            for (float off = step; off < bound; off += step)
            {
                var probe = World.EstTileAtPosWithScroll(new Vector2(cursorScreen.X, cursorScreen.Y + off));
                int px = (int)probe.X, py = (int)probe.Y;
                if (px < 0 || py < 0 || px >= bp.Width || py >= bp.Height) continue; //out-of-world probe: skipped
                var probed = vm.Context.GetRoomAt(LotTilePos.FromBigTile((short)px, (short)py, level));
                if (probed != front) { behind = probed; break; }
            }
            bool changed = CutHistoryInsert((uint)front);
            if (behind != front) changed |= CutHistoryInsert((uint)behind);
            return changed;
        }

        /// <summary>
        /// cCutawaySet::insert: a duplicate leaves membership untouched (no
        /// recency refresh — skeptic-corrections.md #1); otherwise the room is
        /// appended and the list trimmed to 3 by evicting the oldest member.
        /// Only valid room ids (&lt;0xfffb) are stored.
        /// </summary>
        private bool CutHistoryInsert(uint room)
        {
            if (room >= 0xfffb) return false; //native validity gate
            if (CutRooms.Contains(room)) return false;
            CutRooms.Add(room);
            while (CutRooms.Count > 3) CutRooms.RemoveAt(0);
            return true;
        }

        /// <summary>
        /// ResetDynamicCutaway: clears the standing matrix only — the history
        /// set survives (native callers clear it explicitly).
        /// </summary>
        internal void ResetCutawayMatrix()
        {
            CommitCutaway(new FSO.LotView.Utils.CutawayViewInputs
            {
                Floor = World.State.Level,
                Rotation = World.State.CutRotation,
                Zoom = World.State.Zoom,
                PreciseZoom = World.State.PreciseZoom,
                HistoryRooms = new uint[0], //empty composition; CutRooms intentionally untouched
                DynamicEnabled = false,
            });
        }

        /// <summary>
        /// Wall/room architecture changes rebuild the room partition, so the
        /// cached per-room masks are stale (revision bump) and the history's ids
        /// are re-derived (the port cannot observe native's stable room ids
        /// across edits; the native terrain-rebuild command 0x105 clears the set
        /// the same way).
        /// </summary>
        private void WireCutMaskInvalidation()
        {
            if (CutWallsWired || vm.Context.Architecture == null) return;
            CutWallsWired = true;
            vm.Context.Architecture.WallsChanged += (caller) =>
            {
                CutMasks.Invalidate();
                CutMasks.Revision++;
                CutRooms.Clear();
            };
        }

        /// <summary>
        /// UI-25 item 4: native DrawPictureInPicture's cross-floor clear — the
        /// live hover history is wiped on ENTER (0x1c1068) AND AGAIN on RESTORE
        /// (0x1c161c) of every cross-floor PIP render, so the main mask
        /// recomposes without it (person + cursor only). Same-floor PIP shares
        /// the standing matrix untouched (floor-equal branch 0x1c1050/0x1c1604)
        /// and must NOT clear.
        /// </summary>
        internal void ClearCutHistory() => CutRooms.Clear();

        //uicutaway autotest hooks (the battery drives the production path)
        internal IReadOnlyList<uint> CutawayHistory => CutRooms;
        internal void CutawayHistoryReset() { CutRooms.Clear(); }
        internal FSO.LotView.Utils.CutawayMaskCache CutMaskCache => CutMasks;
    }
}
