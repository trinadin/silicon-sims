using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.SimAntics.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Screens;
using System;

namespace Simitone.Client.UI.Panels.LiveSubpanels
{
    /// <summary>
    /// R184: the native desktop cWinPersonality row. It is a compact caption
    /// plus one continuous ten-step bar, not a UISkillDisplay pip grid.
    /// </summary>
    public class UIOriginalPersonalityRow : UIContainer
    {
        public const int ROW_W = 97, ROW_H = 16;
        public const int TEXT_W = 51, BAR_X = 54, BAR_W = 40;
        public static readonly Color NativeCaptionColor = new Color(0xC3, 0xCD, 0xCD, 0xFF);

        public UIOriginalText Label;
        public Texture2D BackdropTex;
        public Texture2D BarsTex;
        public int Points;
        public string TierName;
        public string TierDescription;
        public Action Activated;
        public bool DisabledState;
        private bool Hover;
        private bool Down;

        private Vector2 _Size = new Vector2(ROW_W, ROW_H);
        public override Vector2 Size
        {
            get { return _Size; }
            set { _Size = value; }
        }

        public int FillWidth { get { return 4 * Math.Max(0, Math.Min(10, Points)); } }

        public UIOriginalPersonalityRow(string caption, OriginalGlyphFont font, Action activated = null)
        {
            Activated = activated;
            BackdropTex = UIOriginal.EnsureResolved("cpanel\\Backgrounds\\PersBkg.bmp")?.Get(GameFacade.GraphicsDevice);
            BarsTex = UIOriginal.EnsureResolved("cpanel\\HouseSubBars.bmp")?.Get(GameFacade.GraphicsDevice);
            Label = new UIOriginalText(caption ?? "", font)
            {
                Position = Vector2.Zero,
                Size = new Vector2(TEXT_W, ROW_H),
                Color = NativeCaptionColor
            };
            Add(Label);

            // Native hit testing accepts only the common 51x16 caption rect;
            // the bar itself is display-only.
            ListenForMouse(new Rectangle(0, 0, TEXT_W, ROW_H), Mouse);
            FSO.Client.Utils.UIUtils.GiveTooltip(this);
        }

        public Rectangle CaptionHitRect => new Rectangle(0, 0, TEXT_W, ROW_H);

        private void Mouse(UIMouseEventType type, UpdateState state)
        {
            if (DisabledState) return;
            if (type == UIMouseEventType.MouseOver) Hover = true;
            else if (type == UIMouseEventType.MouseOut)
            {
                Hover = false;
                Down = false;
            }
            else if (type == UIMouseEventType.MouseDown)
            {
                Down = true;
                // Native cTSWinBtn sends the parent command on mouse-down.
                Activated?.Invoke();
            }
            else if (type == UIMouseEventType.MouseUp) Down = false;
            ApplyStateColor();
            Invalidate();
        }

        private void ApplyStateColor()
        {
            Label.Color = DisabledState ? new Color(0x40, 0x5D, 0x5F, 0xFF)
                : Down ? new Color(0x00, 0xFF, 0xFF, 0xFF)
                : Hover ? Color.White : NativeCaptionColor;
        }

        public void SetEntry(string caption, string tierName, string tierDescription, int points)
        {
            Label.Text = caption ?? "";
            Points = Math.Max(0, Math.Min(10, points));
            TierName = tierName ?? "";
            TierDescription = tierDescription ?? "";
            Tooltip = string.IsNullOrEmpty(tierName) ? tierDescription
                : string.IsNullOrEmpty(tierDescription) ? tierName
                : tierName + "\n" + tierDescription;
            ApplyStateColor();
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            // PersBkg is 40x14 and is centered one pixel down in the 16px row.
            if (BackdropTex != null)
                DrawLocalTexture(batch, BackdropTex, null, new Vector2(BAR_X, 1), Vector2.One);
            var fill = FillWidth;
            if (BarsTex != null && fill > 0)
                DrawLocalTexture(batch, BarsTex, new Rectangle(0, 0, fill, BarsTex.Height),
                    new Vector2(BAR_X, 0), Vector2.One);
            base.Draw(batch);
        }
    }

    /// <summary>Autosized type-4 text button used for the zodiac label.</summary>
    public class UIOriginalZodiacButton : UIContainer
    {
        public UIOriginalText Label;
        public Action Activated;
        private UIMouseEventRef Hit;
        private bool Hover;
        private bool Down;
        private Vector2 _Size;

        public override Vector2 Size
        {
            get { return _Size; }
            set
            {
                _Size = value;
                if (Hit != null)
                {
                    Hit.Region.Width = (int)value.X;
                    Hit.Region.Height = (int)value.Y;
                }
            }
        }

        public UIOriginalZodiacButton(OriginalGlyphFont font, Action activated)
        {
            Activated = activated;
            Label = new UIOriginalText("", font)
            {
                Color = UIOriginalPersonalityRow.NativeCaptionColor
            };
            Add(Label);
            Size = new Vector2(1, font?.LineHeight ?? 1);
            Hit = ListenForMouse(new Rectangle(0, 0, (int)Size.X, (int)Size.Y), Mouse);
            FSO.Client.Utils.UIUtils.GiveTooltip(this);
        }

        public void SetText(string text)
        {
            Label.Text = text ?? "";
            var width = Label.Font?.Measure(Label.Text) ?? 1;
            var height = Label.Font?.LineHeight ?? 1;
            Size = new Vector2(Math.Max(1, width), Math.Max(1, height));
            Label.Size = Size;
            ApplyStateColor();
        }

        private void Mouse(UIMouseEventType type, UpdateState state)
        {
            if (type == UIMouseEventType.MouseOver) Hover = true;
            else if (type == UIMouseEventType.MouseOut)
            {
                Hover = false;
                Down = false;
            }
            else if (type == UIMouseEventType.MouseDown)
            {
                Down = true;
                Activated?.Invoke();
            }
            else if (type == UIMouseEventType.MouseUp) Down = false;
            ApplyStateColor();
            Invalidate();
        }

        private void ApplyStateColor()
        {
            Label.Color = Down ? new Color(0x00, 0xFF, 0xFF, 0xFF)
                : Hover ? Color.White : UIOriginalPersonalityRow.NativeCaptionColor;
        }
    }

    /// <summary>
    /// Native Live Personality panel. Desktop geometry and resources come from
    /// cWinPeople::BuildPersonalityButtons/cWinPersonality (R184). The former
    /// mobile layout remains isolated in its own constructor path.
    /// </summary>
    public class UIPersonalitySubpanel : UISubpanel
    {
        public const int NATIVE_HOST_W = 280;
        public const int NATIVE_HOST_H = 100;
        public const int ROW_X = 105, ROW_Y0 = 8, ROW_PITCH_Y = 18;
        private static readonly int[] TraitStringIndex = { 1, 8, 15, 22, 29 };
        private static readonly VMPersonDataVariable[] TraitData =
        {
            VMPersonDataVariable.NeatPersonality,
            VMPersonDataVariable.OutgoingPersonality,
            VMPersonDataVariable.ActivePersonality,
            VMPersonDataVariable.PlayfulPersonality,
            VMPersonDataVariable.NicePersonality
        };
        private static readonly string[] FallbackNames = { "Neat", "Outgoing", "Active", "Playful", "Nice" };

        public UIOriginalPersonalityRow[] Rows;
        public UIOriginalText HostTitle;
        public UIOriginalText ZodiacText;
        public UIOriginalZodiacButton ZodiacButton;
        public UIOriginalText[] NameTwins;
        public UIOptionAboutPopup CurrentPopup;
        public int PopupTrait = -1;
        public bool PopupIsZodiac;
        public static int TraitNamesTwinned;

        public static readonly int[,] CompatibleSigns =
        {
            { 3, 2 }, { 1, 7 }, { 12, 6 }, { 2, 8 },
            { 9, 4 }, { 11, 9 }, { 6, 4 }, { 12, 5 },
            { 12, 10 }, { 11, 2 }, { 10, 9 }, { 8, 3 }
        };
        public static readonly int[,] IncompatibleSigns =
        {
            { 4, 7 }, { 6, 4 }, { 10, 1 }, { 3, 1 },
            { 10, 3 }, { 5, 2 }, { 12, 8 }, { 7, 11 },
            { 7, 8 }, { 5, 3 }, { 8, 6 }, { 5, 1 }
        };

        // Mobile-only retained controls.
        private UISkillDisplay[] MobileSkills;
        private FSO.Client.UI.Controls.UILabel[] MobileLabels;
        private bool? LastPetCorpus;
        private int LastZodiacCode = int.MinValue;

        public UIPersonalitySubpanel(TS1GameScreen game) : base(game)
        {
            if (game.Desktop)
            {
                InitDesktop();
            }
            else InitMobile();
        }

        private void InitDesktop()
        {
            var gd = GameFacade.GraphicsDevice;
            var titleFont = OriginalGlyphFont.LoadByIndex(11, gd);
            var rowFont = OriginalGlyphFont.LoadByIndex(8, gd);
            var zodiacFont = OriginalGlyphFont.LoadByIndex(12, gd);

            if (titleFont != null)
            {
                HostTitle = new UIOriginalText(OriginalLiveStrings.Trait(0) ?? "Personality", titleFont)
                {
                    Position = new Vector2(5, 0)
                };
                Add(HostTitle);
            }

            Rows = new UIOriginalPersonalityRow[5];
            NameTwins = new UIOriginalText[5];
            for (int i = 0; i < 5; i++)
            {
                var index = i;
                var caption = OriginalLiveStrings.Trait(TraitStringIndex[i]) ?? FallbackNames[i];
                var row = new UIOriginalPersonalityRow(caption, rowFont, () => ToggleTraitPopup(index))
                {
                    Position = new Vector2(ROW_X, ROW_Y0 + i * ROW_PITCH_Y)
                };
                Add(row);
                Rows[i] = row;
                NameTwins[i] = row.Label;
            }
            TraitNamesTwinned++;

            if (zodiacFont != null)
            {
                ZodiacButton = new UIOriginalZodiacButton(zodiacFont, ToggleZodiacPopup)
                {
                    Position = new Vector2(5, 100 - zodiacFont.LineHeight - 5),
                    Tooltip = GameFacade.Strings.GetString("130", "27") ?? "Astrological Sign"
                };
                ZodiacText = ZodiacButton.Label;
                // This quick-tip belongs to UIText.iff DesignCharStrs, not
                // Live.iff's Motives table (the chunk ids collide).
                ZodiacText.Tooltip = ZodiacButton.Tooltip;
                Add(ZodiacButton);
            }
        }

        private void InitMobile()
        {
            MobileSkills = new UISkillDisplay[5];
            MobileLabels = new FSO.Client.UI.Controls.UILabel[5];
            for (int i = 0; i < 5; i++)
            {
                MobileSkills[i] = new UISkillDisplay
                {
                    Position = new Vector2(334 + (i % 3) * 140, 35 + 60 * (i / 3))
                };
                Add(MobileSkills[i]);

                var name = new FSO.Client.UI.Controls.UILabel
                {
                    Caption = OriginalLiveStrings.Trait(TraitStringIndex[i]) ?? FallbackNames[i],
                    Position = new Vector2(332 + (i % 3) * 140, 11 + 60 * (i / 3))
                };
                name.CaptionStyle = name.CaptionStyle.Clone();
                name.CaptionStyle.Color = UIStyle.Current.Text;
                name.CaptionStyle.Size = 15;
                Add(name);
                MobileLabels[i] = name;
            }
        }

        public override void Update(UpdateState state)
        {
            var sel = Game.SelectedAvatar;
            if (sel != null)
            {
                if (Rows != null) UpdateDesktop(sel);
                else UpdateMobile(sel);
            }
            base.Update(state);
        }

        private void UpdateDesktop(FSO.SimAntics.VMAvatar sel)
        {
            var pet = sel.IsPet;
            if (LastPetCorpus != pet)
            {
                LastPetCorpus = pet;
                Invalidate();
            }

            for (int i = 0; i < Rows.Length; i++)
            {
                var entry = TraitStringIndex[i];
                var points = Math.Max(0, Math.Min(10, sel.GetPersonData(TraitData[i]) / 100));
                var tier = points <= 2 ? 0 : points <= 7 ? 1 : 2;
                var caption = pet ? OriginalLiveStrings.PetTrait(entry) : OriginalLiveStrings.Trait(entry);
                var tierName = pet ? OriginalLiveStrings.PetTrait(entry + 1 + tier) : OriginalLiveStrings.Trait(entry + 1 + tier);
                var tierDescription = pet ? OriginalLiveStrings.PetTrait(entry + 4 + tier) : OriginalLiveStrings.Trait(entry + 4 + tier);
                Rows[i].SetEntry(caption ?? FallbackNames[i], tierName, tierDescription, points);
                NameTwins[i] = Rows[i].Label;
            }

            if (CurrentPopup != null && !PopupIsZodiac && PopupTrait >= 0 && PopupTrait < Rows.Length)
            {
                CurrentPopup.SetContent(Rows[PopupTrait].TierName, Rows[PopupTrait].TierDescription);
                PositionPopup();
            }

            if (ZodiacText != null)
            {
                // Gender 0/1 are adults, 2/3 are children. Pet bits are
                // separate, so neither age nor Gender>1 is a species test.
                var adultHuman = !pet && sel.GetPersonData(VMPersonDataVariable.Gender) <= 1;
                ZodiacText.Visible = adultHuman;
                if (ZodiacButton != null) ZodiacButton.Visible = adultHuman;
                if (adultHuman)
                {
                    var code = sel.GetPersonData(VMPersonDataVariable.TS1Zodiac);
                    if (code < 1 || code > 12)
                    {
                        code = ComputeZodiacCode(sel);
                        sel.SetPersonData(VMPersonDataVariable.TS1Zodiac, (short)code);
                    }
                    if (LastZodiacCode != code)
                    {
                        LastZodiacCode = code;
                        ZodiacButton.SetText(Simitone.Client.UI.Panels.CAS.UIOriginalDesignChar.ZodiacName(code - 1));
                    }
                }
                else if (PopupIsZodiac) ClosePopup();
            }
        }

        internal void ToggleTraitPopupForProbe(int index) { ToggleTraitPopup(index); }
        internal void ToggleZodiacPopupForProbe() { ToggleZodiacPopup(); }

        private static Rectangle? PortraitCrop(Texture2D portrait)
        {
            if (portrait == null) return null;
            int width = Math.Min(45, portrait.Width);
            int height = Math.Min(45, portrait.Height);
            return new Rectangle((portrait.Width - width) / 2,
                (portrait.Height - height) / 2, width, height);
        }

        private void ToggleTraitPopup(int index)
        {
            if (index < 0 || index >= (Rows?.Length ?? 0)) return;
            if (CurrentPopup != null && !PopupIsZodiac && PopupTrait == index)
            {
                ClosePopup();
                return;
            }

            Texture2D portrait = null;
            try { portrait = UIIconCache.GetObject(Game.SelectedAvatar); } catch { }
            var row = new UIOriginalOptionsPanel.OptRow
            {
                AboutTitle = Rows[index].TierName,
                AboutBody = Rows[index].TierDescription
            };
            if (CurrentPopup == null)
            {
                CurrentPopup = new UIOptionAboutPopup(null, row, portrait);
                DynamicOverlay.Add(CurrentPopup);
            }
            else CurrentPopup.SetContent(row.AboutTitle, row.AboutBody);
            CurrentPopup.SetArt(portrait, PortraitCrop(portrait), true);
            PopupTrait = index;
            PopupIsZodiac = false;
            PositionPopup();
        }

        public static string CompatibilityLine(int zodiacCode, bool compatible)
        {
            int row = Math.Max(1, Math.Min(12, zodiacCode)) - 1;
            var table = compatible ? CompatibleSigns : IncompatibleSigns;
            var prefix = GameFacade.Strings.GetString("154", compatible ? "6" : "7");
            if (string.IsNullOrEmpty(prefix) || prefix.StartsWith("154:"))
                prefix = compatible ? "Most compatible with: " : "Least compatible with: ";
            return prefix
                + Simitone.Client.UI.Panels.CAS.UIOriginalDesignChar.ZodiacName(table[row, 0] - 1)
                + ", "
                + Simitone.Client.UI.Panels.CAS.UIOriginalDesignChar.ZodiacName(table[row, 1] - 1);
        }

        private void ToggleZodiacPopup()
        {
            if (ZodiacButton == null || !ZodiacButton.Visible || Game.SelectedAvatar == null) return;
            if (CurrentPopup != null && PopupIsZodiac)
            {
                ClosePopup();
                return;
            }
            int code = Math.Max(1, Math.Min(12,
                (int)Game.SelectedAvatar.GetPersonData(VMPersonDataVariable.TS1Zodiac)));
            int titleIndex = 2 * (code - 1);
            var title = GameFacade.Strings.GetString("165", titleIndex.ToString());
            var body = GameFacade.Strings.GetString("165", (titleIndex + 1).ToString())
                + "\n" + CompatibilityLine(code, true)
                + "\n" + CompatibilityLine(code, false);
            var row = new UIOriginalOptionsPanel.OptRow { AboutTitle = title, AboutBody = body };
            if (CurrentPopup == null)
            {
                CurrentPopup = new UIOptionAboutPopup(null, row, null);
                DynamicOverlay.Add(CurrentPopup);
            }
            else CurrentPopup.SetContent(title, body);
            CurrentPopup.SetArt(null, null, false);
            PopupTrait = -1;
            PopupIsZodiac = true;
            PositionPopup();
        }

        private void PositionPopup()
        {
            if (CurrentPopup == null) return;
            CurrentPopup.Position = new Vector2(Size.X - CurrentPopup.Size.X, -CurrentPopup.Size.Y);
            CurrentPopup.CanonPos = new Point((int)CurrentPopup.X, (int)CurrentPopup.Y);
        }

        public void ClosePopup()
        {
            if (CurrentPopup != null)
            {
                DynamicOverlay.Remove(CurrentPopup);
                CurrentPopup = null;
            }
            PopupTrait = -1;
            PopupIsZodiac = false;
        }

        public override void GameResized()
        {
            base.GameResized();
            PositionPopup();
        }

        public override void Kill()
        {
            ClosePopup();
            base.Kill();
        }

        private static short ComputeZodiacCode(FSO.SimAntics.VMAvatar sel)
        {
            var values = new float[5];
            for (int i = 0; i < 5; i++) values[i] = sel.GetPersonData(TraitData[i]) / 100f;
            int best = 11;
            float bestDistance = float.MaxValue;
            var archetypes = Simitone.Client.UI.Panels.CAS.UIOriginalDesignChar.ZodiacArchetypes;
            for (int sign = 0; sign < archetypes.Length; sign++)
            {
                float distance = 0;
                for (int trait = 0; trait < values.Length; trait++)
                {
                    var delta = values[trait] - archetypes[sign][trait];
                    distance += delta * delta;
                }
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = sign;
                }
            }
            return (short)(best + 1);
        }

        private void UpdateMobile(FSO.SimAntics.VMAvatar sel)
        {
            if (NameTwins == null)
            {
                var font = OriginalGlyphFont.LoadCaption(GameFacade.GraphicsDevice);
                if (font != null && OriginalLiveStrings.Trait(1) != null)
                {
                    NameTwins = new UIOriginalText[5];
                    for (int i = 0; i < 5; i++)
                    {
                        NameTwins[i] = new UIOriginalText(MobileLabels[i].Caption, font)
                        {
                            Color = UIStyle.Current.Text,
                            Position = MobileLabels[i].Position
                        };
                        Add(NameTwins[i]);
                        MobileLabels[i].Visible = false;
                    }
                    TraitNamesTwinned++;
                }
            }
            for (int i = 0; i < 5; i++) MobileSkills[i].Value = sel.GetPersonData(TraitData[i]) / 100;
        }
    }
}
