using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.Utils;

namespace Simitone.Client
{
    internal static class AutotestClipboard240
    {
        internal static bool Check(out string diagnostics)
        {
            if(!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            { diagnostics="macOS clipboard fixture not applicable"; return true; }
            var failures=new List<string>();
            Action<bool,string> require=(ok,name)=>{if(!ok) failures.Add(name);};
            PasteboardSnapshot snapshot=null;
            try
            {
                // Preserve every item/flavor, including images and attributed
                // text, before SDL temporarily replaces the general pasteboard.
                using(var saved=snapshot=PasteboardSnapshot.Capture())
                {
                    require(ClipboardHandler.Default is MacSDLClipboard,"desktop-handler-installed");
                    var clipboard=ClipboardHandler.Default;
                    clipboard.Set("Sims clipboard \u00c9\u4e2d\ud83d\ude03\nsecond line");
                    require(clipboard.Get()=="Sims clipboard \u00c9\u4e2d\ud83d\ude03\nsecond line","native-utf8-roundtrip");
                    var input=new InputManager();
                    var state=new UpdateState{WindowFocused=true,InputManager=input,FrameTextInput=new List<char>()};
                    Func<StringBuilder,int,int,Keys,Keys,KeyboardInputResult> key=(buffer,start,end,modifier,k)=>{
                        state.KeyboardState=new KeyboardState(modifier,k);
                        state.NewKeys=new List<Keys>{k};
                        return input.ApplyKeyboardInput(buffer,state,start,end,true);
                    };
                    foreach(var modifier in new[]{Keys.LeftControl,Keys.LeftWindows,Keys.RightWindows})
                    {
                        var source=new StringBuilder("Alpha Beta");
                        key(source,0,5,modifier,Keys.C);
                        require(clipboard.Get()=="Alpha",modifier+"-copy");
                        var target=new StringBuilder("!");
                        key(target,0,-1,modifier,Keys.V);
                        require(target.ToString()=="Alpha!",modifier+"-paste");
                        var all=key(source,2,-1,modifier,Keys.A);
                        require(all.SelectionStart==0 && all.SelectionEnd==10,modifier+"-select-all");
                        key(source,0,5,modifier,Keys.X);
                        require(source.ToString()==" Beta" && clipboard.Get()=="Alpha",modifier+"-cut");
                        source=new StringBuilder("Reverse range");
                        key(source,7,0,modifier,Keys.C);
                        require(clipboard.Get()=="Reverse",modifier+"-reverse-copy-to-zero");
                        key(source,7,0,modifier,Keys.X);
                        require(source.ToString()==" range" && clipboard.Get()=="Reverse",modifier+"-reverse-cut-to-zero");
                    }
                    var unrelated=new StringBuilder("one two");
                    var commandBack=key(unrelated,7,-1,Keys.LeftWindows,Keys.Back);
                    require(unrelated.ToString()=="one tw" && !commandBack.CtrlDown,"command-other-keys-unchanged");
                    foreach(var shortcut in new[]{Keys.A,Keys.C,Keys.X,Keys.V})
                    {
                        clipboard.Set("Paste");
                        var text=new StringBuilder("Text");
                        state.FrameTextInput=new List<char>{char.ToLowerInvariant((char)shortcut)};
                        var originalFrame=state.FrameTextInput;
                        var result=key(text,0,4,Keys.LeftWindows,shortcut);
                        require(text.ToString()==(shortcut==Keys.X?"":shortcut==Keys.V?"Paste":"Text"),
                            "paired-command-text-"+shortcut);
                        require(ReferenceEquals(originalFrame,state.FrameTextInput) && originalFrame.Count==1,
                            "paired-frame-preserved-"+shortcut);
                    }
                    clipboard.Set("Paste");
                    state.FrameTextInput=new List<char>{'v','\u00e9','\u4e2d'};
                    var mixed=new StringBuilder();
                    key(mixed,0,-1,Keys.RightWindows,Keys.V);
                    require(mixed.ToString()=="Paste\u00e9\u4e2d","paired-command-preserves-unicode");
                }
            }
            catch(Exception ex) { failures.Add(ex.GetType().Name+":"+ex.Message); }
            diagnostics="SDL UTF8 and Ctrl/Command clipboard shortcuts; nativePasteboardRestore="+
                (snapshot?.Restored==true ? "verified-all-items-types-data" : "unverified")+" "+
                (failures.Count==0?"pass":string.Join(",",failures));
            return failures.Count==0;
        }

        // This test-only snapshot uses AppKit declarations in the installed
        // macOS SDK NSPasteboard.h/NSPasteboardItem.h. All retained objects are
        // released; NSData is cloned into private items before the first write.
        private sealed class PasteboardSnapshot : IDisposable
        {
            private IntPtr Board, Items;
            private bool Mutated;
            internal bool Restored { get; private set; }
            private const string ObjC="/usr/lib/libobjc.A.dylib";
            [DllImport(ObjC)] private static extern IntPtr objc_getClass(string name);
            [DllImport(ObjC)] private static extern IntPtr sel_registerName(string name);
            [DllImport(ObjC,EntryPoint="objc_msgSend")] private static extern IntPtr Send(IntPtr obj,IntPtr selector);
            [DllImport(ObjC,EntryPoint="objc_msgSend")] private static extern IntPtr Send1(IntPtr obj,IntPtr selector,IntPtr arg);
            [DllImport(ObjC,EntryPoint="objc_msgSend")] private static extern byte SendBool2(IntPtr obj,IntPtr selector,IntPtr arg1,IntPtr arg2);
            [DllImport(ObjC,EntryPoint="objc_msgSend")] private static extern byte SendBool1(IntPtr obj,IntPtr selector,IntPtr arg);
            private static IntPtr S(string name)=>sel_registerName(name);
            private static IntPtr New(string name)=>Send(Send(objc_getClass(name),S("alloc")),S("init"));
            internal static PasteboardSnapshot Capture()
            {
                var result=new PasteboardSnapshot();
                try
                {
                    result.Board=Send(objc_getClass("NSPasteboard"),S("generalPasteboard"));
                    if(result.Board==IntPtr.Zero) throw new InvalidOperationException("Pasteboard unavailable; no clipboard mutation attempted");
                    result.Items=New("NSMutableArray");
                    var originals=Send(result.Board,S("pasteboardItems"));
                    long count=Send(originals,S("count")).ToInt64();
                    if(count==0 && Send(Send(result.Board,S("types")),S("count")).ToInt64()>0)
                        throw new InvalidOperationException("Cannot preserve legacy pasteboard items; no clipboard mutation attempted");
                    for(long i=0;i<count;i++)
                    {
                        var original=Send1(originals,S("objectAtIndex:"),new IntPtr(i));
                        var types=Send(original,S("types"));
                        long typeCount=Send(types,S("count")).ToInt64();
                        var clone=New("NSPasteboardItem");
                        try
                        {
                            for(long t=0;t<typeCount;t++)
                            {
                                var type=Send1(types,S("objectAtIndex:"),new IntPtr(t));
                                var data=Send1(original,S("dataForType:"),type);
                                if(data==IntPtr.Zero || SendBool2(clone,S("setData:forType:"),data,type)==0)
                                    throw new InvalidOperationException("Cannot preserve all pasteboard flavors; no clipboard mutation attempted");
                            }
                            Send1(result.Items,S("addObject:"),clone);
                        }
                        finally { Send(clone,S("release")); }
                    }
                    result.Mutated=true;
                    return result;
                }
                catch { result.Dispose(); throw; }
            }
            public void Dispose()
            {
                if(Items==IntPtr.Zero) return;
                try
                {
                    if(Mutated)
                    {
                        Send(Board,S("clearContents"));
                        if(Send(Items,S("count")).ToInt64()>0 && SendBool1(Board,S("writeObjects:"),Items)==0)
                            throw new InvalidOperationException("Could not restore original pasteboard items");
                        var actual=Send(Board,S("pasteboardItems"));
                        long count=Send(Items,S("count")).ToInt64();
                        if(Send(actual,S("count")).ToInt64()!=count)
                            throw new InvalidOperationException("Pasteboard restored item-count mismatch");
                        for(long i=0;i<count;i++)
                        {
                            var expectedItem=Send1(Items,S("objectAtIndex:"),new IntPtr(i));
                            var actualItem=Send1(actual,S("objectAtIndex:"),new IntPtr(i));
                            var types=Send(expectedItem,S("types"));
                            long typeCount=Send(types,S("count")).ToInt64();
                            if(Send(Send(actualItem,S("types")),S("count")).ToInt64()!=typeCount)
                                throw new InvalidOperationException("Pasteboard restored type-count mismatch");
                            for(long t=0;t<typeCount;t++)
                            {
                                var type=Send1(types,S("objectAtIndex:"),new IntPtr(t));
                                var expectedData=Send1(expectedItem,S("dataForType:"),type);
                                var actualData=Send1(actualItem,S("dataForType:"),type);
                                if(actualData==IntPtr.Zero || SendBool1(actualData,S("isEqualToData:"),expectedData)==0)
                                    throw new InvalidOperationException("Pasteboard restored flavor-data mismatch");
                            }
                        }
                        Restored=true;
                    }
                }
                finally { Send(Items,S("release")); Items=IntPtr.Zero; }
            }
        }
    }
}
