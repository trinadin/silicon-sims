using System;
using System.Collections.Generic;
using FSO.Client;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Panels.CAS;
using Simitone.Client.UI.Screens;

namespace Simitone.Client
{
    // Original per-state dimensions are independently decoded in R143.
    // Controller fixtures have private IFF chunks/census and injected writers;
    // no InitializeLot, live census mutation, persistence or navigation runs.
    internal static class AutotestCAS239
    {
        private sealed class ButtonProbe : UIOriginalSheetButton
        {
            internal UIMouseEventRef Mouse => ClickHandler;
            internal ButtonProbe(string member, int width) : base(member, width) { }
        }

        internal static bool Check(out string diagnostics)
        {
            var failures = new List<string>();
            Action<bool, string> require = (ok, label) => { if (!ok) failures.Add(label); };
            var oldTween = GameFacade.Screens.Tween;
            var fixtureTween = new UITween();
            TS1CASScreen screen = null;
            int boundsChecked = 0, clicksChecked = 0, deleted = 0, prompts = 0, censusReads = 0;
            try
            {
                // SetMode creates tweens. Keep every fixture target in an
                // unregistered private tween manager, then stop without Complete.
                GameFacade.Screens.Tween = fixtureTween;
                var sheets = new (string Name, int Width, int Height)[]
                {
                    ("DesignCharAdultBtn.bmp",57,100), ("DesignCharChildBtn.bmp",92,90),
                    ("DesignCharFemaleBtn.bmp",57,121), ("DesignCharMaleBtn.bmp",98,111),
                    ("DesignCharDarkBtn.bmp",57,71), ("DesignCharMediumBtn.bmp",46,71),
                    ("DesignCharLightBtn.bmp",55,71), ("DesignCharSkinsLeftBtn.bmp",18,34),
                    ("DesignCharSkinsRightBtn.bmp",18,34), ("AddPersonBtn.bmp",139,103),
                    ("DeletePersonBtn.bmp",149,99), ("EditPersonBtn.bmp",137,99),
                    ("PAFAddFamily.bmp",92,62), ("PAFDeleteFamily.bmp",92,62),
                    ("PAFMoveIn.bmp",164,62)
                };
                foreach (var item in sheets)
                {
                    var button = new ButtonProbe("nbhd\\" + item.Name, item.Width);
                    var expected = new Rectangle(0, 0, item.Width, item.Height);
                    require(button.Mouse.Region == expected && button.GetBounds() == expected
                        && button.Size == new Vector2(item.Width, item.Height), item.Name + ":native-bounds");
                    boundsChecked++;
                    int calls = 0;
                    button.OnButtonClick += b => calls++;
                    var points = new[] { new Point(0,0), new Point(item.Width-1,0),
                        new Point(0,item.Height-1), new Point(item.Width-1,item.Height-1) };
                    foreach (var point in points)
                    {
                        int before = calls;
                        Dispatch(button, point);
                        require(calls == before + 1, item.Name + ":corner-" + point);
                        clicksChecked++;
                    }
                    int edgeCalls = calls;
                    Dispatch(button, new Point(item.Width, item.Height-1));
                    Dispatch(button, new Point(item.Width-1, item.Height));
                    require(calls == edgeCalls, item.Name + ":outside-rejected");
                    button.Disabled = true;
                    button.Selected = true;
                    Dispatch(button, new Point(item.Width-1, item.Height-1));
                    require(calls == edgeCalls, item.Name + ":disabled-no-command");
                }

                screen = new TS1CASScreen();
                require(screen.Original && screen.FamiliesPanel == null, "desktop-only-fixture");
                if (!screen.Original) throw new InvalidOperationException("CAS239 requires desktop mode");
                require(screen.DesktopCAS.NameBox.TextStyle.Size == 10
                    && ReferenceEquals(screen.DesktopCAS.BioEdit.Font, Simitone.Client.UI.Controls.OriginalGlyphFont.LoadCaption(GameFacade.GraphicsDevice))
                    && screen.DesktopFamily.FamilyNameBox.TextStyle.Size == 10, "native-editor-font10");
                screen.DesktopCAS.BioEdit.Text = "one\ntwo\nthree\nfour\nfive\nsix";
                require(screen.DesktopCAS.BioEdit.LineCount == 6
                    && screen.DesktopCAS.BioEdit.Text == "one\ntwo\nthree\nfour\nfive\nsix",
                    "bio-preserves-more-than-four-paragraphs");

                // Exercise the mounted CAS field, not a standalone editor substitute.
                var bio = screen.DesktopCAS.BioEdit;
                var input = new UpdateState { WindowFocused = true, Time = new GameTime(), InputManager = new InputManager() };
                input.InputManager.SetFocus(bio);
                bio.Text = "WW";
                require(bio.HitTestText(new Vector2(5, 0)) == 0
                    && bio.HitTestText(new Vector2(5 + bio.Font.Measure("W"), 0)) == 1,
                    "bio-native-inset-hit-testing");
                int wideCount = 751 / bio.Font.Advance('W');
                bio.Text = new string('W', wideCount + 1);
                require(bio.LineCount == 2, "bio-native-751px-wrap");
                bio.Text = "alpha  beta gamma";
                bio.SetSelection(2, 9);
                input.KeyboardState = new KeyboardState(Keys.Left);
                input.NewKeys.Add(Keys.Left);
                input.FrameTextInput = new List<char>();
                bio.Update(input);
                require(bio.SelectionStart == 8 && bio.SelectionEnd == -1, "bio-native-selection-navigation");
                bio.Text = new string('a', 2046) + "XY";
                bio.SetSelection(2044, 2046);
                input.KeyboardState = new KeyboardState(); input.NewKeys.Clear();
                input.FrameTextInput = new List<char>("12345");
                bio.Update(input);
                require(bio.Text == new string('a', 2044) + "12XY", "bio-capacity-preserves-existing-suffix");
                bio.Text = "keep";
                screen.SetMode(UICASMode.FamilyEdit);
                bio.Update(input);
                require(!bio.IsFocused && input.InputManager.GetFocus() == null && bio.Text == "keep", "bio-mode-transition-blurs");
                input.InputManager.SetFocus(bio); // outgoing panel can be clicked during its tween
                bio.Update(new UpdateState { WindowFocused = true, Time = new GameTime(), InputManager = input.InputManager });
                screen.FamilySimInterp = 0;
                bio.Update(input);
                require(!bio.IsFocused && bio.Text == "keep", "bio-hidden-after-tween-blurs");
                bio.Text = "";

                var census = new List<FAMI> { Family(901,"Fixture Alpha"), Family(902,"Fixture Beta") };
                screen.FamilyCensus = () => { censusReads++; return new List<FAMI>(census); };
                screen.FamilyDeleteWriter = fam => { require(census.Remove(fam), "delete-selected-census-item"); deleted++; };
                screen.FamilySaveWriter = (name,members) => { throw new InvalidOperationException("Unexpected family save"); };
                int transitions = 0;
                int? movedFamily = null;
                screen.NeighborhoodTransition = family => { transitions++; movedFamily = family; };
                Action yes = null, no = null;
                string title = null, message = null;
                screen.ConfirmationPresenter = (t,m,y,n) => { prompts++; title=t; message=m; yes=y; no=n; };
                screen.SetFamilies();
                screen.SetMode(UICASMode.FamilySelect);
                require(screen.vm == null && screen.DesktopFamilies.Cards.Count == 2, "isolated-census-no-vm");
                screen.DeleteFamily();
                screen.Accept(null);
                require(prompts == 0 && deleted == 0 && transitions == 0, "no-selection-no-action");

                screen.DesktopFamilies.SetSelection(1);
                require(screen.SelectedFamily() == census[1], "desktop-selection-resolver");
                input.InputManager.SetFocus(bio);
                bio.Update(new UpdateState { WindowFocused = true, Time = new GameTime(), InputManager = input.InputManager });
                screen.DeleteFamily();
                bio.Update(input);
                require(!bio.IsFocused && input.InputManager.GetFocus() == null && bio.Text == "", "bio-confirmation-blurs");
                require(prompts == 1 && title == GameFacade.Strings.GetString("128","6")
                    && message == GameFacade.Strings.GetString("128","7",new[] { "Fixture Beta" }),
                    "native-delete-family-prompt");
                screen.DeleteFamily(); screen.Accept(null); screen.GoBack(null);
                require(prompts == 1 && deleted == 0 && transitions == 0, "pending-dialog-blocks-other-commands");
                var staleYes = yes;
                no(); staleYes();
                require(deleted == 0 && census.Count == 2 && screen.DesktopFamilies.GetSelection() == 1,
                    "delete-no-and-stale-yes-preserve-family");

                screen.DeleteFamily();
                var accepted = yes;
                accepted(); accepted(); no();
                require(prompts == 2 && deleted == 1 && census.Count == 1
                    && census[0].ChunkID == 901 && censusReads == 2
                    && screen.DesktopFamilies.Cards.Count == 1 && screen.DesktopFamilies.GetSelection() == -1,
                    "delete-yes-once-refreshes-census-clears-selection");
                screen.DesktopFamilies.SetSelection(99);
                require(screen.SelectedFamily() == null && screen.DesktopFamilies.GetSelection() == -1,
                    "stale-family-index-rejected");
                screen.DesktopFamilies.SetSelection(0);
                screen.Accept(null);
                require(transitions == 1 && movedFamily == 901 && deleted == 1, "move-in-selected-family-only");

                screen.SetMode(UICASMode.FamilyEdit);
                screen.DesktopFamily.FamilyNameBox.CurrentText = "Empty Fixture";
                int priorPrompts = prompts;
                screen.GoBack(null);
                require(screen.CurrentMode == UICASMode.FamilySelect && prompts == priorPrompts
                    && screen.DesktopFamily.FamilyNameBox.CurrentText == "", "empty-draft-back-without-prompt");

                // No branch can create/delete a preview avatar in this fixture.
                screen.WIPFamily.Add(new CASFamilyMember { Name = "Draft" });
                screen.RequestModifySim(true,-1);
                screen.RequestModifySim(true,8);
                require(prompts == priorPrompts, "stale-member-delete-rejected");
                screen.RequestModifySim(true,0);
                require(title == GameFacade.Strings.GetString("129","5")
                    && message == GameFacade.Strings.GetString("129","6"), "native-delete-member-prompt");
                no();
                require(screen.WIPFamily.Count == 1, "member-delete-no-preserves-draft");
                screen.WIPFamily.Clear();
                screen.DesktopFamily.MemberCount = 0;
                screen.DesktopFamily.SelectMember(7);
                require(screen.DesktopFamily.SelectedMember == -1 && screen.DesktopFamily.DeleteBtn.Disabled
                    && screen.DesktopFamily.EditBtn.Disabled, "stale-member-selection-cleared");
            }
            catch (Exception e) { failures.Add(e.GetType().Name + ": " + e.Message); }
            finally
            {
                screen?.WIPFamily.Clear();
                fixtureTween.StopAll(false, false);
                GameFacade.Screens.Tween = oldTween;
            }
            require(!fixtureTween.HasQueue, "fixture-tweens-cleaned");
            diagnostics = "nativeBounds=" + boundsChecked + " cornerClicks=" + clicksChecked
                + " prompts=" + prompts + " isolatedDeletes=" + deleted + " censusReads=" + censusReads
                + (failures.Count == 0 ? " PASS" : " FAIL " + string.Join("; ", failures));
            return failures.Count == 0;
        }

        private static void Dispatch(ButtonProbe button, Point point)
        {
            var mouse = new MouseState(point.X, point.Y, 0, ButtonState.Pressed,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            if (!button.HitTestArea(mouse, button.Mouse.Region, false)) return;
            var state = new UpdateState { MouseState = mouse, WindowFocused = true, Time = new GameTime() };
            button.Mouse.Callback(UIMouseEventType.MouseOver, state);
            button.Mouse.Callback(UIMouseEventType.MouseDown, state);
            button.Mouse.Callback(UIMouseEventType.MouseUp, state);
            button.Mouse.Callback(UIMouseEventType.MouseOut, state);
        }

        private static FAMI Family(ushort id, string name)
        {
            var iff = new IffFile();
            var family = new FAMI { ChunkID = id, ChunkProcessed = true, Budget = 20000, FamilyGUIDs = new uint[0] };
            var strings = new FAMs { ChunkID = id, ChunkProcessed = true };
            strings.LanguageSets[0].Strings = new[] { new STRItem(name) { LanguageCode = 1 } };
            iff.AddChunk(family); iff.AddChunk(strings);
            return family;
        }
    }
}
