#!/bin/bash
# packmac.sh - Assemble a native Apple Silicon .app from the self-contained publish output.
set -e
cd "$(dirname "$BASH_SOURCE[0]")"
SUF=""
if [ "$1" = "arm64" ]; then SUF="-arm64"; fi
SRC="publish/osx${SUF}"
APP="dist/The Sims${SUF}.app"  # R156: native name (was Simitone${SUF}.app)
NUGET="$PWD/.nuget-packages"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$SRC"/. "$APP/Contents/MacOS/"
# Resource folders in MacOS are mistaken for nested code by codesign --deep.
# Keep one real copy in Resources and preserve FreeSO's ./Content lookup.
# The SDK-generated nested app duplicates the publish root and is not used.
rm -rf "$APP/Contents/MacOS/Content" "$APP/Contents/MacOS/Simitone.Desktop.app"
cp -R "$SRC/Content" "$APP/Contents/Resources/Content"
ln -s ../Resources/Content "$APP/Contents/MacOS/Content"
# Eto.Forms assemblies are never emitted by MSBuild on macOS; copy explicitly.
cp -n "$NUGET/eto.forms/2.9.0/lib/net6.0/Eto.dll" "$APP/Contents/MacOS/" 2>/dev/null || true
cp -n "$NUGET/eto.platform.mac64/2.9.0/lib/netstandard2.0/Eto.Mac64.dll" "$APP/Contents/MacOS/" 2>/dev/null || true
# Native SDL/OpenAL used by DesktopGL on macOS.
for f in "$NUGET/monogame.library.sdl/2.32.2.1/runtimes/osx/native/libSDL2-2.0.0.dylib" "$NUGET/monogame.library.openal/1.23.1.10/runtimes/osx/native/libopenal.dylib"; do
  [ -f "$f" ] && cp -n "$f" "$APP/Contents/MacOS/" || true
done
# SDL's macOS loader looks for the canonical name libSDL2.dylib; provide a symlink.
ln -sf libSDL2-2.0.0.dylib "$APP/Contents/MacOS/libSDL2.dylib" 2>/dev/null || true
cp Info.plist "$APP/Contents/Info.plist"
# REL-09 F2: ship third-party license notices with the bundle.
cp -n THIRD-PARTY-NOTICES.md "$APP/Contents/Resources/THIRD-PARTY-NOTICES.md" 2>/dev/null || true
# R156: NATIVE icon — the plumbob, generated at pack time (no proprietary bytes committed;
# falls back to the legacy icon only if generation fails).
if python3 tools/make_native_icon.py --out build/NativeIcon.icns 2>/dev/null && [ -f build/NativeIcon.icns ]; then
  cp build/NativeIcon.icns "$APP/Contents/Resources/Icon.icns"
else
  cp -n build/Icon.icns "$APP/Contents/Resources/Icon.icns" 2>/dev/null || true
fi
chmod +x "$APP/Contents/MacOS/TheSims"
codesign --force --deep -s - "$APP"
codesign --verify --deep --strict "$APP"
echo "Built $APP"
du -sh "$APP"
