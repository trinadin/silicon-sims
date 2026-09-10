using System;
using System.Collections.Generic;
using FSO.Client;
using FSO.Common;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Controls;

namespace Simitone.Client
{
    // Expectations recovered from the original MoveWord, MoveCursor and
    // TSOnKeyDown functions, independent of the port's layout implementation.
    internal static class AutotestEditor239
    {
        internal static bool Check(out string diagnostics)
        {
            var failures = new List<string>();
            Action<bool,string> require = (ok,label) => { if (!ok) failures.Add(label); };
            var font = OriginalGlyphFont.LoadDialog(GameFacade.GraphicsDevice);
            var edit = new UIOriginalTextEdit(font,520,font.LineHeight*2);
            var state = new UpdateState { WindowFocused=true, Time=new GameTime(), InputManager=new InputManager() };
            edit.OnFocusChanged(FocusEvent.FocusIn);
            Action<Keys,bool,bool> key = (k,ctrl,shift) => {
                var keys = new List<Keys>{k};
                if(ctrl) keys.Add(Keys.LeftControl);
                if(shift) keys.Add(Keys.LeftShift);
                state.KeyboardState=new KeyboardState(keys.ToArray());
                state.NewKeys.Clear(); state.NewKeys.Add(k); state.FrameTextInput=new List<char>();
                edit.ProcessInput(state);
            };
            edit.Text="alpha  beta gamma";
            edit.SetSelection(2,9); key(Keys.Left,false,false);
            require(edit.SelectionStart==8 && edit.SelectionEnd==-1,"left-from-active-endpoint");
            edit.SetSelection(9,2); key(Keys.Right,false,false);
            require(edit.SelectionStart==3 && edit.SelectionEnd==-1,"right-from-active-endpoint");
            edit.SetSelection(2); key(Keys.Right,true,false);
            require(edit.SelectionStart==7,"word-right-trailing-spaces");
            key(Keys.Left,true,true);
            require(edit.SelectionStart==7 && edit.SelectionEnd==0,"word-left-shift-selection");
            edit.Text="  foo";
            edit.SetSelection(0); key(Keys.Left,true,false);
            require(edit.SelectionStart==2,"native-leading-spaces");
            edit.Text="a\tb c"; edit.SetSelection(0); key(Keys.Right,true,false);
            require(edit.SelectionStart==4,"tab-is-not-word-separator");
            edit.Text="one\ntwo\nthree\nfour\nfive\nsix"; edit.SetSelection(1,2);
            key(Keys.Down,true,false);
            require(edit.FirstVisibleLine==1 && edit.SelectionStart==1 && edit.SelectionEnd==2,"ctrl-down-scroll-only");
            key(Keys.PageDown,true,false);
            require(edit.FirstVisibleLine==3 && edit.SelectionStart==1 && edit.SelectionEnd==2,"ctrl-page-scroll-only");
            key(Keys.Up,true,true);
            require(edit.FirstVisibleLine==3 && edit.SelectionStart==1 && edit.SelectionEnd==2,"ctrl-shift-no-op");
            key(Keys.Up,true,false);
            require(edit.FirstVisibleLine==2 && edit.SelectionEnd==2,"ctrl-up-scroll-only");
            edit.Text="iiiiiiii\nWWWWWWWW"; edit.SetSelection(4); key(Keys.Down,false,false);
            require(edit.SelectionStart==13,"vertical-character-column");
            edit.Text="abcdefgh\nxy\nlast"; edit.SetSelection(6); key(Keys.Down,false,false);
            require(edit.SelectionStart==11,"vertical-hard-line-clamp");
            edit.SetSelection(3); key(Keys.Down,false,false);
            require(edit.SelectionStart==12,"vertical-native-equal-count");
            edit.Text="abcdef\nxy"; edit.SetSelection(6); key(Keys.Down,false,false);
            require(edit.SelectionStart==9,"vertical-final-line-clamp");
            edit.Text="a\nb\nc\nd\ne\nf"; edit.SetSelection(1); key(Keys.PageDown,false,false);
            require(edit.SelectionStart==5 && edit.FirstVisibleLine==2,"page-preserves-viewport-offset");
            key(Keys.PageDown,false,false); key(Keys.PageUp,false,true);
            require(edit.SelectionStart==9 && edit.SelectionEnd==5 && edit.FirstVisibleLine==2,"page-shift-preserves-range-and-offset");
            edit.Text="alpha\nbeta"; edit.SetSelection(1); key(Keys.End,false,false);
            require(edit.SelectionStart==5 && edit.CaretRow==0,"hard-line-end");
            var narrow = new UIOriginalTextEdit(font,font.Measure("word "),font.LineHeight*2);
            narrow.Text="word next";
            narrow.OnFocusChanged(FocusEvent.FocusIn);
            narrow.SetSelection(1);
            state.KeyboardState=new KeyboardState(Keys.End);
            state.NewKeys.Clear(); state.NewKeys.Add(Keys.End); state.FrameTextInput=new List<char>();
            narrow.ProcessInput(state);
            require(narrow.SelectionStart==4 && narrow.CaretRow==0,"soft-row-end");
            require(narrow.HitTestText(new Vector2(1000,2))==4,"soft-row-click-edge");
            narrow.Text="word\nnext";
            require(narrow.HitTestText(new Vector2(1000,2))==4,"hard-row-click-edge");
            narrow.Text="word";
            require(narrow.HitTestText(new Vector2(1000,2))==4,"final-row-click-edge");
            narrow.Blur();
            edit.Text="alpha  beta";
            // Exercise the real mouse handler twice; the selected native word
            // includes its trailing separators. Coordinates are device pixels.
            int mx=(int)Math.Round(font.Measure("al")*FSOEnvironment.DPIScaleFactor);
            int my=(int)Math.Round(2*FSOEnvironment.DPIScaleFactor);
            state.KeyboardState=new KeyboardState();
            state.MouseState=new MouseState(mx,my,0,ButtonState.Pressed,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
            edit.OnMouseEvent(UIMouseEventType.MouseDown,state);
            edit.OnMouseEvent(UIMouseEventType.MouseUp,state);
            edit.OnMouseEvent(UIMouseEventType.MouseDown,state);
            require(edit.SelectionStart==0 && edit.SelectionEnd==7,"double-click-word");
            edit.Blur();
            diagnostics=failures.Count==0 ? "native keyboard/mouse/wrap-edge contracts passed" : string.Join(",",failures);
            return failures.Count==0;
        }
    }
}
