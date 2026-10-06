using System;
using System.Runtime.InteropServices;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// ORIG-02 G-6: anchor the macOS IME/accent candidate window to the caret.
    /// The vendored MonoGame 3.8.4 DesktopGL binding has no
    /// SDL_SetTextInputRect interop, but the SHIPPED libSDL2-2.0.0.dylib
    /// exports it (nm: _SDL_SetTextInputRect @ 0x18a88) — the same raw
    /// DllImport discipline as MacSDLClipboard. Callers pass the caret rect
    /// in WINDOW (point) coordinates.
    /// </summary>
    public static class SDLTextInput
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SDL_Rect
        {
            public int X, Y, W, H;
        }

        [DllImport("libSDL2-2.0.0.dylib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_SetTextInputRect(ref SDL_Rect rect);

        public static void SetCaretRect(int x, int y, int w, int h)
        {
            var r = new SDL_Rect { X = x, Y = y, W = Math.Max(1, w), H = Math.Max(1, h) };
            try { SDL_SetTextInputRect(ref r); }
            catch { /* dylib mismatch: IME falls back to its old corner anchor */ }
        }

        public static void Clear()
        {
            var r = new SDL_Rect { X = 0, Y = 0, W = 0, H = 0 };
            try { SDL_SetTextInputRect(ref r); }
            catch { }
        }
    }
}
