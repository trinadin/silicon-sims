using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Utils;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView.Model;
using FSO.Common.Rendering.Framework.Model;
using FSO.SimAntics;
using FSO.SimAntics.Engine.TSOTransaction;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay.Drivers;
using Microsoft.Xna.Framework;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simitone.Client.UI.Panels
{
    public class UIHouseSelectPanel : UIContainer
    {
        public UIDiagonalStripe Diag;
        public UIDiagonalStripe TitleStripe;

        public UILabel StreetTitle;
        public UILabel LotTitle;
        public UILabel LotDescription;
        public UILabel SecondaryText;
        public UIHouseFamilyList FamilyDisplay;

        public UIBigButton EnterLot;
        public UIBigButton More;

        public event Action<int> OnSelected;
        public int HouseID;

        public List<UIBigButton> OptionButtons = new List<UIBigButton>();

        // ROUND-113: original-glyph twins for the lot-query labels (the strings were always
        // original — street names from the neighborhood STR tables, body/secondary from
        // UIText.iff 'NghRollover' (134) / 'MoveInModeStrs' (132); this round makes the
        // GLYPHS original: street + lot titles as UIOriginalText, description + secondary
        // as wrapping UIOriginalParagraphs). Captions mutate after ctor (family/community/
        // price branches), so twins sync every Update and mirror the MoreTween visibility.
        public UIOriginalText StreetTwin;
        public UIOriginalText LotTwin;
        public UIOriginalParagraph DescTwin;
        public UIOriginalParagraph SecondaryTwin;
        public static int LotQueryTwinsMounted = 0;

        private float _MoreTween;
        public float MoreTween
        {
            get
            {
                return _MoreTween;
            }
            set
            {
                var nonOptionButtons = new UIElement[]
                {
                    LotDescription,
                    SecondaryText,
                    EnterLot,
                    More,
                };

                foreach (var btn in nonOptionButtons)
                {
                    btn.Opacity = 1 - value;
                    btn.Visible = btn.Opacity > 0;
                }

                foreach (var btn in OptionButtons)
                {
                    btn.Opacity = value;
                    btn.Visible = btn.Opacity > 0;
                }
                var screen = UIScreen.Current;

                var scale = Math.Max(2 / 3f, Math.Min(1, screen.ScreenWidth / 1704f));
                var space = (96 * scale) - 56;

                if (FamilyDisplay != null)
                    FamilyDisplay.X = (48 + space / 2) - value * screen.ScreenWidth / 2;

                _MoreTween = value;
            }
        }

        public UIHouseSelectPanel(int houseID)
        {
            var screen = GameFacade.Screens.CurrentUIScreen;
            var extra = Math.Max(0, (screen.ScreenHeight - 640) / 128) * 64;
            HouseID = houseID;

            Diag = new UIDiagonalStripe(new Point(screen.ScreenWidth / 2, screen.ScreenHeight + 16), UIDiagonalStripeSide.RIGHT, UIStyle.Current.Bg);
            Diag.Y = -16;
            Diag.ListenForMouse(Diag.GetBounds(), (e, s) => { });
            Add(Diag);

            TitleStripe = new UIDiagonalStripe(new Point(screen.ScreenWidth / 2, 92 + 8 + 32), UIDiagonalStripeSide.RIGHT, UIStyle.Current.Bg);
            TitleStripe.StartOff = 8 + 32;
            TitleStripe.Y = -16 + extra;
            Add(TitleStripe);

            var neigh = Content.Get().Neighborhood;

            var house = neigh.GetHouse(houseID);

            var street = neigh.StreetNames;
            // AUD-17 E-7: missing StreetNames STR chunks used to crash card
            // construction (the production popup guards the same lookup).
            var streetChunk = street.Get<STR>(2001);
            var assignment = streetChunk?.GetString(houseID - 1);

            int streetName;
            if (int.TryParse(assignment, out streetName))
            {
                StreetTitle = new UILabel();
                StreetTitle.Position = new Vector2(30, 94 + extra - 64);
                InitLabel(StreetTitle);
                StreetTitle.CaptionStyle.Color = UIStyle.Current.BtnActive;
                StreetTitle.Caption = (street.Get<STR>(2000)?.GetString(streetName - 1) ?? "%s").Replace("%s", houseID.ToString());
            }

            var nameDesc = neigh.GetHouseNameDesc(houseID);
            var name = nameDesc.Item1;
            if (name == "") name = StreetTitle.Caption;

            LotTitle = new UILabel();
            LotTitle.Position = new Vector2(30, 122 + extra - 64);
            InitLabel(LotTitle);
            LotTitle.CaptionStyle.Size = 37;
            LotTitle.Caption = name;

            var family = neigh.GetFamilyForHouse((short)houseID);

            LotDescription = new UILabel();
            LotDescription.Position = new Vector2(30, 206 + extra);
            InitLabel(LotDescription);
            //LotDescription.CaptionStyle.Size = 15;
            LotDescription.Size = new Vector2(screen.ScreenWidth/2 - 60, screen.ScreenHeight - 415);
            LotDescription.Wrapped = true;
            LotDescription.Alignment = TextAlignment.Top | TextAlignment.Left;

            SecondaryText = new UILabel();
            SecondaryText.Position = new Vector2(30, screen.ScreenHeight - (165 + extra));
            InitLabel(SecondaryText);
            SecondaryText.Size = new Vector2(screen.ScreenWidth / 2 - 60, 29);
            SecondaryText.Wrapped = true;
            SecondaryText.Alignment = TextAlignment.Bottom | TextAlignment.Right;
            SecondaryText.CaptionStyle.Color = UIStyle.Current.SecondaryText;

            var moveInID = (UIScreen.Current as Screens.TS1GameScreen).MoveInFamily;
            var moveIn = (moveInID == null) ? null : Content.Get().Neighborhood.GetFamily((ushort)moveInID.Value);
            var buttonValid = true;

            if (family != null)
            {
                var famUI = new UIHouseFamilyList(family);

                var scale = Math.Max(2 / 3f, Math.Min(1, screen.ScreenWidth / 1704f));
                famUI.ScaleX = famUI.ScaleY = scale;
                var space = (96 * scale) - 56;
                LotDescription.Y += space;
                famUI.Position = new Vector2(48 + space / 2, 152 + extra + space / 2);
                Add(famUI);
                FamilyDisplay = famUI;

                LotDescription.Caption = GameFacade.Strings.GetString("134", "0", new string[] {
                    Content.Get().Neighborhood.MainResource.Get<FAMs>(family.ChunkID)?.GetString(0) ?? "?",
                    "§" + (family.ValueInArch + family.Budget).ToString("##,#0"), //should include lot value eventually
                    family.FamilyFriends.ToString()
                    });

                LotDescription.CaptionStyle.Color = UIStyle.Current.SecondaryText;

                if (moveIn != null)
                {
                    SecondaryText.Caption = GameFacade.Strings.GetString("132", "15"); //house occupied
                    buttonValid = false;
                }
            } else
            {
                LotDescription.Y -= 64;
                LotDescription.Size = new Vector2(LotDescription.Size.X, LotDescription.Size.Y + 65);
                //LotDescription.Caption = new string(Enumerable.Range(1, 255).Select(x => 'a').ToArray());
                LotDescription.Caption = nameDesc.Item2;

                //set up the secondary text
                var zones = neigh.ZoningDictionary;
                // AUD-17 E-4: absent-from-dictionary means RESIDENTIAL (0) on
                // every other surface (TS1GameScreen's zoning default, the lot
                // popup, GetZoningType's decoded not-found law, and the
                // NBR-05 probe's own construction-law comment). The old
                // default of 1 — plus a guessed 81-89 studiotown exception —
                // marked every vacant lot 11-49 "community" on this card and
                // refused move-ins the desktop native gate allows. Lots that
                // genuinely carry community zoning come from the dictionary.
                short result;
                if (!zones.TryGetValue((short)houseID, out result)) result = 0;

                if (result > 0)
                {
                    //zone
                    string str;
                    if (moveIn != null)
                    {
                        str = GameFacade.Strings.GetString("134", "18").Substring(8); //is community, can't move in
                        buttonValid = false;
                    }
                    else
                    {
                        str = GameFacade.Strings.GetString("134", "17").Substring(8); //is community
                    }
                    SecondaryText.Caption = str;
                } else
                {
                    //show price
                    var price = house.Get<SIMI>(1)?.PurchaseValue ?? 0;
                    string str;
                    if (houseID >= 90 && houseID <= 92)
                    {
                        //also requires magicoins
                        var magicoins = neigh.GetMagicoinsForFamily(family);
                        var requiredMC = int.Parse(GameFacade.Strings.GetString("134", (32 + Math.Abs(91 - houseID)).ToString()));

                        if (moveIn != null)
                        {
                            if (magicoins >= requiredMC)
                            {
                                if (moveIn.Budget >= price)
                                {
                                    str = GameFacade.Strings.GetString("134", "31", new string[] { "", "",
                                    "§" + price.ToString("##,#0"),
                                    requiredMC.ToString()
                                    });
                                }
                                else
                                {
                                    //missing simoleons
                                    str = GameFacade.Strings.GetString("134", "38", new string[] { "", "",
                                        "§" + price.ToString("##,#0"),
                                        requiredMC.ToString(),
                                        moveIn.ChunkParent.Get<FAMs>(moveIn.ChunkID)?.GetString(0) ?? "",
                                        "§" +moveIn.Budget.ToString("##,#0")
                                    }).Substring(4);
                                    buttonValid = false;
                                }
                            }
                            else
                            {
                                if (moveIn.Budget >= price)
                                {
                                    //missing magicoins
                                    str = GameFacade.Strings.GetString("134", "36", new string[] { "", "",
                                        "§" + price.ToString("##,#0"),
                                        requiredMC.ToString(),
                                        moveIn.ChunkParent.Get<FAMs>(moveIn.ChunkID)?.GetString(0) ?? "",
                                        magicoins.ToString()
                                    }).Substring(4);
                                }
                                else
                                {
                                    //missing both
                                    str = GameFacade.Strings.GetString("134", "37", new string[] { "", "",
                                        "§" + price.ToString("##,#0"),
                                        requiredMC.ToString(),
                                        moveIn.ChunkParent.Get<FAMs>(moveIn.ChunkID)?.GetString(0) ?? "",
                                        magicoins.ToString()
                                    }).Substring(4);
                                    buttonValid = false;
                                }
                                buttonValid = false;
                            }
                        }
                        else
                        {
                            //suggest move in
                            str = GameFacade.Strings.GetString("134", "29", new string[] { "", "",
                                "§" + price.ToString("##,#0"),
                                requiredMC.ToString()
                            }).Substring(4);
                        }
                    }
                    else
                    {
                        if (moveIn != null)
                        {
                            if (moveIn.Budget >= price)
                            {
                                str = GameFacade.Strings.GetString("134", "5", new string[] { "", "",
                                    "§" + price.ToString("##,#0"),
                                    "§" +(moveIn.Budget - price).ToString("##,#0")
                                }).Substring(4);
                            }
                            else
                            {
                                str = GameFacade.Strings.GetString("134", "4", new string[] { "", "",
                                    "§" + price.ToString("##,#0"),
                                    moveIn.ChunkParent.Get<FAMs>(moveIn.ChunkID)?.GetString(0) ?? "",
                                    "§" +moveIn.Budget.ToString("##,#0")
                                }).Substring(4);
                                buttonValid = false;
                            }
                        }
                        else
                        {
                            //suggest move in
                            str = GameFacade.Strings.GetString("134", "1", new string[] { "", "", "§" + price.ToString("##,#0") }).Substring(4);
                        }
                    }
                        
                    SecondaryText.Caption = str;
                    SecondaryText.CaptionStyle.Size = 15;
                }
            }

            EnterLot = new UIBigButton(false);
            EnterLot.Caption = (moveIn == null)?"Enter Lot":"Move In";
            EnterLot.Width = (moveIn == null)? (screen.ScreenWidth / 2 - 293) : (screen.ScreenWidth/2-60);
            EnterLot.Disabled = !buttonValid;
            EnterLot.Position = new Vector2(30, screen.ScreenHeight - (extra + 125));
            EnterLot.OnButtonClick += (b) => { OnSelected?.Invoke(houseID); Kill(); };
            Add(EnterLot);

            More = new UIBigButton(true);
            More.Caption = "More";
            More.Width = 208;
            More.Position = new Vector2(screen.ScreenWidth / 2 - 238, screen.ScreenHeight - (extra + 125));
            More.OnButtonClick += (btn) => { ShowMore(true); };
            if (moveIn == null) Add(More);

            var optionFunctions = new ButtonClickDelegate[]
            {
                (family==null)?null:(ButtonClickDelegate)((btn) => Evict(family)),
                // NBR-05: the Rezone option runs the same armed rezone cascade
                // as the toolbar tool (STR# 131 law; one code path, two entries).
                // The panel is a Simitone surface (no native counterpart).
                (ButtonClickDelegate)((btn) => ((TS1GameScreen)UIScreen.Current).RezoneLotClickFlow(HouseID)),
                (family==null)?null:(ButtonClickDelegate)((btn) => ExportToDisk(family)),
                (btn) => ShowMore(false)
            };
            var optionNames = new string[]
            {
                (family==null)?"Bulldoze":"Evict",
                "Rezone",
                "Export",
                "Back"
            };

            var bY = extra+140;

            for (int i=0; i<optionFunctions.Length; i++)
            {
                var btn = new UIBigButton(i == optionFunctions.Length-1);
                btn.Caption = optionNames[i];
                btn.Position = new Vector2(screen.ScreenWidth / 4 - btn.Width / 2, bY);
                btn.Disabled = optionFunctions[i] == null;
                btn.OnButtonClick += optionFunctions[i];
                Add(btn);
                bY += 120;
                OptionButtons.Add(btn);
            }

            X = screen.ScreenWidth / -2;
            GameFacade.Screens.Tween.To(this, 0.5f, new Dictionary<string, float>() { { "X", 0f } }, TweenQuad.EaseOut);
            MoreTween = MoreTween;
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            SyncOriginalTwins();
        }

        /// <summary>
        /// ROUND-113: mounts the original-glyph twins for the four lot-query labels and
        /// keeps them synced — the ctor's family/community/price branches mutate the modern
        /// labels' captions AFTER construction, so the twins re-read them each Update and
        /// mirror the MoreTween visibility (which toggles the modern labels' Visible).
        /// Font roles (disclosed): street/lot-body/secondary in the caption table (_10),
        /// the lot NAME in the title table (_14, matching the neighborhood title role);
        /// line height 13px for _10. Modern labels remain as the pre-IFF-mount fallback.
        /// </summary>
        private void SyncOriginalTwins()
        {
            if (StreetTwin == null)
            {
                var cap = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadCaption(FSO.Client.GameFacade.GraphicsDevice);
                var def = Simitone.Client.UI.Controls.OriginalGlyphFont.LoadDefault(FSO.Client.GameFacade.GraphicsDevice);
                if (cap == null || def == null || cap.Atlas == null || def.Atlas == null) return;
                StreetTwin = new UIOriginalText(StreetTitle.Caption, cap) { Color = UIStyle.Current.BtnActive };
                StreetTwin.Position = StreetTitle.Position;
                LotTwin = new UIOriginalText(LotTitle.Caption, def) { Color = Color.White };
                LotTwin.Position = LotTitle.Position;
                DescTwin = new UIOriginalParagraph(cap) { MaxWidth = LotDescription.Size.X, LineHeight = 13f, TextColor = UIStyle.Current.SecondaryText };
                DescTwin.Position = LotDescription.Position;
                SecondaryTwin = new UIOriginalParagraph(cap) { MaxWidth = SecondaryText.Size.X, LineHeight = 13f, RightAlign = true, BottomAnchor = true, TextColor = UIStyle.Current.SecondaryText };
                SecondaryTwin.Position = new Vector2(SecondaryText.Position.X, SecondaryText.Position.Y + SecondaryText.Size.Y);
                Add(StreetTwin);
                Add(LotTwin);
                Add(DescTwin);
                Add(SecondaryTwin);
                StreetTitle.Visible = false;
                LotTitle.Visible = false;
                LotDescription.Visible = false;
                SecondaryText.Visible = false;
                LotQueryTwinsMounted++;
            }
            StreetTwin.Text = StreetTitle.Caption;
            LotTwin.Text = LotTitle.Caption;
            DescTwin.Text = LotDescription.Caption;
            SecondaryTwin.Text = SecondaryText.Caption;
            DescTwin.Visible = LotDescription.Visible;
            SecondaryTwin.Visible = SecondaryText.Visible;
        }

        public void Evict(FAMI family)
        {
            if (family == null) return;
            var familyName = family.ChunkParent.Get<FAMs>(family.ChunkID)?.GetString(0) ?? "selected";
            UIMobileAlert evictDialog = null;
            evictDialog = new UIMobileAlert(new UIAlertOptions()
            {
                Title = GameFacade.Strings.GetString("131", "2"),
                Message = GameFacade.Strings.GetString("131", "3", new string[] {
                    familyName.ToString(),
                    "§" + (family.ValueInArch + family.Budget).ToString("##,#0") }
                ),
                Buttons = UIAlertButton.YesNo(
                    (b) => { evictDialog.Close(); ((TS1GameScreen)UIScreen.Current).EvictLot(family, (short)HouseID); },
                    (b) => { evictDialog.Close(); }
                    )
            });
            UIScreen.GlobalShowDialog(evictDialog, true);
        }

        /// <summary>
        /// UI-21: the lot-query Export button (native option row's Export slot —
        /// decoded-but-unimplemented per the R253 audit, ported here as a disabled
        /// button until now). Serializes the selected bin family to
        /// &lt;UserData&gt;Export/ via the production engine path
        /// (TS1NeighborhoodProvider.ExportFamily through the 247 seam) and shows
        /// the receipt on the existing R142 GenDlg alert. The native confirmation
        /// string is undecoded, so the port uses plain literals (disclosed).
        /// Family-only: the eviction path above is untouched.
        /// </summary>
        public void ExportToDisk(FAMI family)
        {
            if (family == null) return;
            var path = Simitone.Client.Utils.TutorialEngine247.ExportFamily(family.ChunkID);
            UIMobileAlert exportDialog = null;
            exportDialog = new UIMobileAlert(new UIAlertOptions()
            {
                Title = "Export",
                Message = (path != null)
                    ? string.Format("Family exported to {0}", path)
                    : "Couldn't export the family",
                Buttons = UIAlertButton.Ok((b) => exportDialog.Close())
            });
            UIScreen.GlobalShowDialog(exportDialog, true);
        }

        public void ShowMore(bool more)
        {
            GameFacade.Screens.Tween.To(this, 0.5f, new Dictionary<string, float>() { { "MoreTween", (more)?1f:0f } }, TweenQuad.EaseOut);
            /*
            var nonOptionButtons = new UIElement[]
            {
                LotDescription,
                SecondaryText,
                EnterLot,
                More
            };

            foreach (var btn in nonOptionButtons)
            {
                var b = btn as UIButton;
                if (b != null) b.Disabled = more;
            }

            foreach (var btn in OptionButtons)
            {
                btn.Disabled = !more;
            }
            */
        }

        public void Kill()
        {
            EnterLot.Opacity = 0.99f; //force an unpressable state
            More.Opacity = 0.99f;
            var screen = GameFacade.Screens.CurrentUIScreen;
            GameFacade.Screens.Tween.To(this, 0.5f, new Dictionary<string, float>() { { "X", (screen.ScreenWidth / -2) - 32 } }, TweenQuad.EaseIn);
            GameThread.SetTimeout(() => { Parent.Remove(this); }, 500);
        }

        private void InitLabel(UILabel label)
        {
            label.CaptionStyle = label.CaptionStyle.Clone();
            label.CaptionStyle.Color = UIStyle.Current.Text;
            label.CaptionStyle.Size = 19;
            Add(label);
        }
    }

    public class UIHouseFamilyList : UIContainer
    {
        public List<UIAvatarSelectButton> Btns = new List<UIAvatarSelectButton>();
        
        public UIHouseFamilyList(FAMI family)
        {
            PopulateList(family);
        }

        public void PopulateList(FAMI family)
        {
            // AUD-17 E-7: the scratch world leaked if CreateObjectInstance
            // threw (unresolvable member GUID) — try/finally like the popup's
            // guarded portrait loader; a failed member is skipped, matching
            // the CAS family card's AUD-17 F-2 guard.
            var world = new FSO.LotView.World(GameFacade.GraphicsDevice);
            VM vm = null;
            try
            {
                world.Initialize(GameFacade.Scenes);
                var context = new VMContext(world);
                vm = new VM(context, new VMServerDriver(new VMTS1GlobalLinkStub()), new VMNullHeadlineProvider());
                vm.Init();
                var blueprint = new Blueprint(1, 1);

                //world.InitBlueprint(blueprint);
                context.Blueprint = blueprint;
                context.Architecture = new VMArchitecture(1, 1, blueprint, vm.Context);

                int i = 0;
                var baseX = 0;
                foreach (var sim in family.FamilyGUIDs)
                {
                    var grp = vm.Context.CreateObjectInstance(sim, LotTilePos.OUT_OF_WORLD, Direction.NORTH, true);
                    if (grp == null) continue;
                    var fam = grp.BaseObject;
                    var btn = new UIAvatarSelectButton(UIIconCache.GetObject(fam));
                    btn.Opacity = 1f;
                    var id = i;
                    btn.Name = fam.Name;
                    btn.X = baseX + (i++) * 100;
                    btn.Y = 0;
                    btn.DeregisterHandler();
                    Btns.Add(btn);
                    Add(btn);
                    fam.Delete(true, vm.Context);
                }
            }
            finally
            {
                world.Dispose();
            }
        }
    }
}
