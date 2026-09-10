using System;
using System.Runtime.InteropServices;
using FSO.Common.Rendering.Framework.IO;

namespace Simitone.Client.Utils
{
    // DesktopGL already owns SDL and its video subsystem. This handler neither
    // starts/stops SDL nor owns a native library handle.
    public sealed class MacSDLClipboard : ClipboardHandler
    {
        private const string SDL = "libSDL2-2.0.0.dylib";
        [DllImport(SDL,CallingConvention=CallingConvention.Cdecl)]
        private static extern IntPtr SDL_GetClipboardText();
        [DllImport(SDL,CallingConvention=CallingConvention.Cdecl)]
        private static extern int SDL_SetClipboardText([MarshalAs(UnmanagedType.LPUTF8Str)] string text);
        [DllImport(SDL,CallingConvention=CallingConvention.Cdecl)]
        private static extern void SDL_free(IntPtr pointer);
        [DllImport(SDL,CallingConvention=CallingConvention.Cdecl)]
        private static extern IntPtr SDL_GetError();

        public static void Install()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) ClipboardHandler.Default = new MacSDLClipboard();
        }
        public override string Get()
        {
            IntPtr pointer = SDL_GetClipboardText();
            if(pointer==IntPtr.Zero) throw new InvalidOperationException("SDL clipboard read failed: "+Marshal.PtrToStringUTF8(SDL_GetError()));
            try { return Marshal.PtrToStringUTF8(pointer) ?? ""; }
            finally { SDL_free(pointer); }
        }
        public override void Set(string text)
        {
            if(SDL_SetClipboardText(text ?? "")!=0)
                throw new InvalidOperationException("SDL clipboard write failed: "+Marshal.PtrToStringUTF8(SDL_GetError()));
        }
    }
}
