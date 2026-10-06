using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Panels.CAS;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Simitone.Client
{
    /// <summary>Actual CAS callbacks with synthetic catalogs and no VM, renderer,
    /// neighborhood writer, live focus manager or screen transition.</summary>
    internal static class AutotestCAS240
    {
        private sealed class MemoryClipboard : ClipboardHandler
        {
            private string Value = "";
            public override string Get() => Value;
            public override void Set(string text) { Value = text; }
        }
        private sealed class NameProbe : UIOriginalFamilyNameBox
        {
            internal void Select(int start, int end = -1) { SelectionStart = start; SelectionEnd = end; }
            internal int Start => SelectionStart;
            internal int End => SelectionEnd;
        }

        internal static bool Check(out string diagnostics)
        {
            var failures = new List<string>();
            Action<bool,string> require = (ok,label) => { if (!ok) failures.Add(label); };
            var oldTween = GameFacade.Screens.Tween;
            var tween = new UITween();
            var previousUpdate = GameFacade.LastUpdateState;
            var clipboard = ClipboardHandler.Default;
            var input = new UpdateState { Time = new GameTime(), WindowFocused = true,
                InputManager = new InputManager(), FrameTextInput = new List<char>() };
            TS1CASScreen screen = null;
            int catalogLoads = 0, buttonClicks = 0;
            try
            {
                ClipboardHandler.Default = new MemoryClipboard();
                GameFacade.Screens.Tween = tween;
                GameFacade.LastUpdateState = input;
                screen = new TS1CASScreen();
                require(screen.Original, "desktop-fixture");
                screen.DesktopTypeLoaderForTest = type =>
                {
                    catalogLoads++;
                    int heads = screen.CurrentSkin == "med" ? 2 : 6;
                    int bodies = screen.CurrentSkin == "med" ? 3 : 7;
                    screen.ActiveHeads = Enumerable.Range(0,heads).Select(i=>type+"-head-"+i).ToList();
                    screen.ActiveBodies = Enumerable.Range(0,bodies).Select(i=>type+"-body-"+i).ToList();
                    screen.ActiveHeadTex = screen.ActiveHeads.Select(x=>x+"-"+screen.CurrentSkin).ToList();
                    screen.ActiveBodyTex = screen.ActiveBodies.Select(x=>x+"-"+screen.CurrentSkin).ToList();
                    screen.ActiveHandgroupTex = screen.ActiveBodies.Select(x=>screen.CurrentSkin).ToList();
                };
                screen.FamilySaveWriter = (name,members) => { throw new InvalidOperationException("Unexpected persistence"); };
                screen.NeighborhoodTransition = family => { throw new InvalidOperationException("Unexpected navigation"); };
                screen.DesktopFamily.FamilyNameBox.CurrentText = "Fixture";
                screen.RequestModifySim(false,-1);
                var controls = screen.DesktopCAS;
                var mouse = typeof(UIButton).GetMethod("OnMouseEvent",BindingFlags.Instance|BindingFlags.NonPublic);
                Action<UIButton> press = button =>
                {
                    mouse.Invoke(button,new object[]{UIMouseEventType.MouseDown,input});
                    mouse.Invoke(button,new object[]{UIMouseEventType.MouseUp,input});
                    buttonClicks++;
                };
                Action<int,int> advance = (head,body) =>
                {
                    for(int i=0;i<head;i++) press(controls.HeadNextBtn);
                    for(int i=0;i<body;i++) press(controls.BodyNextBtn);
                };
                Action<string,int,int,string> selected = (type,head,body,label) =>
                {
                    var member = screen.BuildMember();
                    require(screen.CurrentCode == type && member.Head == type+"-head-"+head
                        && member.Body == type+"-body-"+body,label);
                };
                selected("ma",0,0,"new-male-adult-zero"); advance(2,3);
                press(controls.FemaleBtn); selected("fa",0,0,"new-female-adult-zero"); advance(1,2);
                press(controls.ChildBtn); selected("fc",0,0,"new-female-child-zero"); advance(3,4);
                press(controls.MaleBtn); selected("mc",0,0,"new-male-child-zero"); advance(4,5);
                press(controls.AdultBtn); selected("ma",2,3,"male-adult-restored");
                press(controls.FemaleBtn); selected("fa",1,2,"female-adult-restored");
                press(controls.ChildBtn); selected("fc",3,4,"female-child-restored");
                press(controls.MaleBtn); selected("mc",4,5,"male-child-restored");
                press(controls.AdultBtn); press(controls.MediumBtn);
                selected("ma",1,2,"skin-clamps-native-indices");
                press(controls.LightBtn); selected("ma",1,2,"skin-has-no-separate-memory");
                screen.RequestModifySim(false,-1);
                selected("ma",0,0,"new-session-resets-current");
                press(controls.FemaleBtn); selected("fa",0,0,"new-session-resets-other-type");
                screen.WIPFamily.Add(new CASFamilyMember { Gender=1, SkinColor="lgt", Head="fa-head-4", Body="fa-body-5",
                    Personality=new short[5], Name="Existing", Bio="" });
                screen.RequestModifySim(false,0);
                selected("fa",4,5,"existing-sim-loads-own-outfits");
                press(controls.MaleBtn); selected("ma",0,0,"existing-session-other-type-zero");
                press(controls.FemaleBtn); selected("fa",4,5,"existing-sim-choice-restored");
                screen.WIPFamily[0].Head="missing"; screen.WIPFamily[0].Body="missing";
                screen.RequestModifySim(false,0); selected("fa",0,0,"unavailable-suit-clamps-first");
                require(TS1CASScreen.SanifyDesktopSuit(2,0)==-1 && TS1CASScreen.SanifyDesktopSuit(-1,4)==0
                    && TS1CASScreen.SanifyDesktopSuit(7,4)==3,"sanify-empty-negative-overflow");

                // Native dialog-entry focus, with the production textboxes and
                // controller lifecycle; no click or SetFocus before typing.
                // The preceding catalog-only member has no world portrait.
                screen.WIPFamily.Clear();
                screen.SetMode(UICASMode.FamilySelect);
                screen.FamilySimInterp = -1;
                screen.DesktopFamily.FamilyNameBox.CurrentText = "";
                screen.SetMode(UICASMode.FamilyEdit);
                screen.UpdateDesktopNameFocus(input);
                require(input.InputManager.GetFocus() == null, "family-hidden-does-not-focus");
                screen.FamilySimInterp = 0;
                screen.UpdateDesktopNameFocus(input);
                require(input.InputManager.GetFocus() == screen.DesktopFamily.FamilyNameBox, "family-entry-focus");
                input.FrameTextInput = "Family".ToList();
                screen.DesktopFamily.FamilyNameBox.Update(input);
                require(screen.DesktopFamily.FamilyNameBox.CurrentText == "Family", "family-types-without-click");
                input.FrameTextInput.Clear();
                screen.RequestModifySim(false, -1);
                screen.UpdateDesktopNameFocus(input);
                require(input.InputManager.GetFocus() == null, "outgoing-family-focus-cleared");
                screen.FamilySimInterp = 1;
                screen.UpdateDesktopNameFocus(input);
                require(input.InputManager.GetFocus() == screen.DesktopCAS.NameBox, "person-entry-focus");
                input.FrameTextInput = "Sim".ToList();
                screen.DesktopCAS.NameBox.Update(input);
                require(screen.DesktopCAS.NameBox.CurrentText == "Sim", "person-types-without-click");
                input.FrameTextInput.Clear();
                input.InputManager.SetFocus(screen.DesktopCAS.BioEdit);
                screen.DesktopCAS.BioEdit.Update(input);
                screen.UpdateDesktopNameFocus(input);
                require(input.InputManager.GetFocus() == screen.DesktopCAS.BioEdit, "entry-focus-does-not-steal-bio");
                screen.SetMode(UICASMode.FamilyEdit);
                require(input.InputManager.GetFocus() == null, "outgoing-person-focus-cleared");
                screen.FamilySimInterp = 0;
                screen.RequestModifySim(false, -1);
                screen.DesktopCAS.NameBox.CurrentText = "Pending";
                Action dismissFocusConfirmation = null;
                screen.ConfirmationPresenter = (title, message, yes, no) => dismissFocusConfirmation = no;
                screen.Accept(null);
                screen.FamilySimInterp = 1;
                screen.UpdateDesktopNameFocus(input);
                require(screen.ConfirmationPending && input.InputManager.GetFocus() == null,
                    "modal-cancels-pending-name-focus");
                dismissFocusConfirmation?.Invoke();
                screen.UpdateDesktopNameFocus(input);
                require(input.InputManager.GetFocus() == null, "dismissed-modal-does-not-rearm-entry-focus");
                screen.ConfirmationPresenter = null;
                screen.SetMode(UICASMode.FamilySelect);
                screen.FamilySimInterp = -1;

                require(screen.DesktopFamily.FamilyNameBox is UIOriginalFamilyNameBox,"production-family-field-filter");
                var field = new NameProbe(); field.SetSize(255,25); field.MaxChars=100;
                input.InputManager.SetFocus(field);
                field.CurrentText="Keep"; field.Select(0,4);
                string forbidden="\\/|*?:<>\"'%()&;@!#,.";
                input.FrameTextInput=forbidden.ToList(); var originalInput=input.FrameTextInput;
                field.Update(input);
                require(field.CurrentText=="Keep" && field.Start==0 && field.End==4,"rejected-typing-preserves-selection");
                require(ReferenceEquals(input.FrameTextInput,originalInput),"typed-input-restored-for-siblings");
                field.Select(4); input.FrameTextInput=" A-Z_+09É".ToList(); field.Update(input);
                require(field.CurrentText=="Keep A-Z_+09É","accepted-typing-spaces-dash-underscore-unicode");
                field.CurrentText=""; field.Select(0); input.FrameTextInput="A:B/C.D".ToList(); field.Update(input);
                require(field.CurrentText=="ABCD","typed-forbidden-characters-rejected");
                ClipboardHandler.Default.Set("A:B/C.D"); field.CurrentText=""; field.Select(0);
                input.KeyboardState=new KeyboardState(Keys.LeftControl,Keys.V); input.NewKeys=new List<Keys>{Keys.V};
                input.FrameTextInput=new List<char>(); field.Update(input);
                require(field.CurrentText=="A:B/C.D","native-paste-bypasses-character-filter");
                field.CurrentText=""; field.Select(0); input.KeyboardState=new KeyboardState(Keys.OemPeriod);
                input.NewKeys=new List<Keys>{Keys.OemPeriod}; input.FrameTextInput=null; var originalKeys=input.NewKeys;
                field.Update(input);
                require(field.CurrentText=="" && ReferenceEquals(originalKeys,input.NewKeys),"legacy-key-path-rejects-and-restores");
                field.CurrentText="O'Name";
                require(field.CurrentText=="O'Name","programmatic-text-remains-unchanged");
            }
            catch(Exception e) { failures.Add(e.GetType().Name+": "+e.Message+" @"+string.Join(" | ",e.StackTrace.Split('\n').Take(8).Select(l=>l.Trim()))); }
            finally
            {
                input.InputManager.SetFocus(null);
                screen?.WIPFamily.Clear();
                tween.StopAll(false,false);
                GameFacade.Screens.Tween=oldTween;
                GameFacade.LastUpdateState=previousUpdate;
                ClipboardHandler.Default = clipboard;
            }
            diagnostics="catalogLoads="+catalogLoads+" buttonClicks="+buttonClicks
                +(failures.Count==0?" PASS":" FAIL "+string.Join("; ",failures));
            return failures.Count==0;
        }
    }
}
