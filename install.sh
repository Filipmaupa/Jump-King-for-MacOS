#!/bin/bash
# Builds "Jump King.app" for Apple Silicon Macs from your own Windows copy of Jump King (Steam) and puts
# it in /Applications. Nothing from the game is downloaded or shipped here: the app is made on your Mac
# from the files you point it at, and it still checks with Steam that you own the game.
#
# Needs: Xcode Command Line Tools, Homebrew and  brew install mono dotnet openal-soft
# Usage: ./install.sh <Windows "Jump King" folder> [folder to put the app in, default /Applications]
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
WORK="$HERE/build"
APP="$WORK/Jump King.app"
RES="$APP/Contents/Resources/JumpKing"
BUNDLE_ID=local.jumpking.macport

die() { echo "error: $*" >&2; exit 1; }
step() { echo "==> $*"; }

[ $# -ge 1 ] || die "usage: ./install.sh <Windows \"Jump King\" folder> [destination folder]"
GAME="$(cd "$1" 2>/dev/null && pwd)" || die "no such folder: $1"
DEST="${2:-/Applications}"
[ -f "$GAME/JumpKing.exe" ] && [ -d "$GAME/Content" ] && [ -f "$GAME/SlimDX.dll" ] \
    || die "$GAME doesn't look like the Windows Jump King folder (JumpKing.exe, SlimDX.dll and Content/ expected)"
[ -d "$DEST" ] || die "no such folder: $DEST"

# ---- tools -------------------------------------------------------------------------------------------
[ "$(uname -m)" = arm64 ] || die "this port is for Apple Silicon Macs (arm64)"
xcode-select -p >/dev/null 2>&1 || die "install the Xcode Command Line Tools first: xcode-select --install"
command -v brew >/dev/null || die "install Homebrew first: https://brew.sh"
for f in mono dotnet openal-soft; do
    brew list --versions "$f" >/dev/null || die "missing Homebrew package: brew install mono dotnet openal-soft"
done
MONO_SRC="$(cd "$(brew --prefix mono)" && pwd -P)"
FW="$MONO_SRC/lib/mono/4.5"
export PATH="$MONO_SRC/bin:$PATH"
export DOTNET_ROOT="$(brew --prefix dotnet)/libexec" DOTNET_ROLL_FORWARD=Major
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

# ---- downloads (pinned versions, checked against known hashes) -----------------------------------------
DEPS="$WORK/deps"
mkdir -p "$DEPS"
fetch() {   # fetch <file> <url> <sha256>
    local out="$DEPS/$1"
    if [ ! -f "$out" ] || ! echo "$3  $out" | shasum -a 256 -c -s; then
        step "Downloading $1"
        curl -fsSL -o "$out.part" "$2" || die "download failed: $2"
        echo "$3  $out.part" | shasum -a 256 -c -s || die "unexpected contents: $2"
        mv "$out.part" "$out"
    fi
    if [[ "$out" != *.dmg ]]; then rm -rf "${out%.*}"; mkdir -p "${out%.*}"; unzip -q -o "$out" -d "${out%.*}"; fi
}
fetch monogame.nupkg "https://www.nuget.org/api/v2/package/MonoGame.Framework.DesktopGL/3.7.1.189" \
    dd50168ff1577c60c3b1dada0837779a78db9909f1b9373737a35e6a053905bb
fetch harmony.nupkg "https://www.nuget.org/api/v2/package/Lib.Harmony/2.4.3" \
    f07af11a0d62447e7e1b6e3fcf1e28fc75d9f8d1abf2d6ee79aabcdc4db43e88
fetch steamworks.zip "https://github.com/rlabrecque/Steamworks.NET/releases/download/20.2.0/Steamworks.NET-Standalone_20.2.0.zip" \
    938ac8037bc73c7ba21e9504d8d074a092600d2ec90ce6bf5b1e1988ba4988c0
SW="$DEPS/steamworks/OSX-Linux-x64"
# SDL2 from its official release (Homebrew's sdl2 is now sdl2-compat, a different library on top of SDL3).
fetch sdl2.dmg "https://github.com/libsdl-org/SDL/releases/download/release-2.30.8/SDL2-2.30.8.dmg" \
    d7561119661cdeef9ada6a3e15f88d2a986c4225501ab53adc8486eb7c00bb77
MNT="$DEPS/sdl2-mount"
mkdir -p "$MNT"
hdiutil attach -nobrowse -readonly -noverify -mountpoint "$MNT" "$DEPS/sdl2.dmg" >/dev/null
cp "$MNT/SDL2.framework/Versions/A/SDL2" "$DEPS/libSDL2-2.0.0.dylib"
hdiutil detach "$MNT" >/dev/null

# ---- the port's own pieces -------------------------------------------------------------------------------
OUT="$WORK/out"
rm -rf "$OUT"; mkdir -p "$OUT/tools"

step "Building the patchers"
for p in jkpatch winpatch exitpatch modpatch mgpatch; do
    dotnet build "$HERE/patchers/$p" -c Release -o "$OUT/tools/$p" -v quiet -nologo >"$OUT/tools/$p.log" 2>&1 \
        || { cat "$OUT/tools/$p.log"; die "building $p failed"; }
done

step "Patching MonoGame"
dotnet "$OUT/tools/mgpatch/mgpatch.dll" "$DEPS/monogame/lib/net45/MonoGame.Framework.dll" "$OUT/MonoGame.Framework.dll"
cp "$SW/Steamworks.NET.dll" "$SW/steam_api.bundle/Contents/MacOS/libsteam_api.dylib" "$DEPS/harmony/lib/net48/0Harmony.dll" "$OUT/"

step "Building MacSupport, SteamProbe and the SlimDX stand-in"
csc -nologo -target:library -out:"$OUT/MacSupport.dll" -r:"$OUT/MonoGame.Framework.dll" "$HERE/src/MacSupport/MacSupport.cs"
csc -nologo -out:"$OUT/SteamProbe.exe" -r:"$OUT/Steamworks.NET.dll" "$HERE/src/SteamProbe/SteamProbe.cs"
# JumpKing.exe references SlimDX by strong name, so the stand-in carries the original's public key.
sn -q -e "$GAME/SlimDX.dll" "$OUT/slimdx_pub.snk"
csc -nologo -target:library -delaysign+ -keyfile:"$OUT/slimdx_pub.snk" -out:"$OUT/SlimDX.dll" "$HERE/src/SlimDX/SlimDX.cs"
clang -arch arm64 -dynamiclib -O2 -mmacosx-version-min=11.0 -install_name @rpath/libwin32shim.dylib \
    -o "$OUT/libwin32shim.dylib" "$HERE/src/win32shim/win32shim.c"

step "Patching JumpKing.exe"
cp "$GAME/LanguageJK.dll" "$OUT/"
if monodis --assemblyref "$GAME/JumpKing.exe" | grep "Name=MacSupport" >/dev/null; then
    echo "    already patched for macOS, used as it is"
    cp "$GAME/JumpKing.exe" "$OUT/JumpKing.exe"
else
    cp "$GAME/JumpKing.exe" "$OUT/JumpKing.0.exe"
    dotnet "$OUT/tools/jkpatch/jkpatch.dll" "$OUT/JumpKing.0.exe" "$OUT/JumpKing.1.exe"
    dotnet "$OUT/tools/winpatch/winpatch.dll" "$OUT/JumpKing.1.exe" "$OUT/MacSupport.dll" "$OUT/JumpKing.2.exe"
    dotnet "$OUT/tools/exitpatch/exitpatch.dll" "$OUT/JumpKing.2.exe" "$OUT/MacSupport.dll" "$OUT/JumpKing.3.exe"
    dotnet "$OUT/tools/modpatch/modpatch.dll" "$OUT/JumpKing.3.exe" "$OUT/MacSupport.dll" "$OUT/JumpKing.exe"
fi

# ---- the app -----------------------------------------------------------------------------------------------
step "Assembling the app"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/lib/mono/4.5" "$APP/Contents/etc/mono/4.5" "$RES"

cp -c -R "$GAME/Content" "$RES/Content" 2>/dev/null || cp -R "$GAME/Content" "$RES/Content"
python3 -I "$HERE/tools/make_mask_gl.py" "$GAME/Content/shaders/Mask.xnb" "$RES/Content/shaders/Mask.xnb.gl" >/dev/null
mv "$RES/Content/shaders/Mask.xnb.gl" "$RES/Content/shaders/Mask.xnb"
for f in JumpKing.exe.config LanguageJK.dll.config language.xml en-US _LANGUAGEFILES; do
    if [ -e "$GAME/$f" ]; then cp -R "$GAME/$f" "$RES/"; fi
done
for f in JumpKing.exe LanguageJK.dll MonoGame.Framework.dll Steamworks.NET.dll SlimDX.dll 0Harmony.dll \
         MacSupport.dll SteamProbe.exe libsteam_api.dylib; do
    cp "$OUT/$f" "$RES/"
done
cp "$DEPS/libSDL2-2.0.0.dylib" "$RES/"
cp -L "$(brew --prefix openal-soft)/lib/libopenal.1.dylib" "$RES/"
chmod u+w "$RES"/*.dylib
cat > "$RES/MonoGame.Framework.dll.config" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
	<dllmap dll="SDL2.dll" os="osx" target="libSDL2-2.0.0.dylib"/>
	<dllmap dll="soft_oal.dll" os="osx" target="libopenal.1.dylib" />
</configuration>
EOF
date +%s > "$RES/.build-stamp"

step "Bundling the Mono runtime"
# Mono finds its lib/ and etc/ next to the folder it runs from, so it lives in Contents/MacOS under the
# app's name: macOS then treats the game as this app (name, icon, one Dock tile) instead of "mono".
cp "$MONO_SRC/bin/mono" "$APP/Contents/MacOS/Jump King"
cp -L "$MONO_SRC/lib/libmono-native.dylib" "$MONO_SRC/lib/libMonoPosixHelper.dylib" "$OUT/libwin32shim.dylib" "$APP/Contents/lib/"
bundle_assembly() {   # bundle_assembly <path relative to lib/mono/4.5>
    local rel="$1" src="$FW/$1" dst="$APP/Contents/lib/mono/4.5/$1"
    mkdir -p "$(dirname "$dst")"
    if [ -L "$src" ]; then   # strong-named assemblies live in the GAC; 4.5/ holds symlinks into it
        local link; link="$(readlink "$src")"
        mkdir -p "$(dirname "$(dirname "$dst")/$link")"
        cp -L "$src" "$(dirname "$dst")/$link"
        ln -sfn "$link" "$dst"
    else
        cp "$src" "$dst"
    fi
}
# Everything the game's assemblies reference, plus what Workshop mods commonly use, plus the
# netstandard facades for mods built against .NET Standard.
python3 -I "$HERE/tools/closure.py" "$FW" "$RES" System.Xml.Linq System.Web.Extensions System.Net.Http \
    System.IO.Compression System.IO.Compression.FileSystem Microsoft.CSharp System.ComponentModel.Composition \
    2>/dev/null > "$OUT/assemblies.txt"
for f in "$FW"/Facades/*.dll; do echo "Facades/${f##*/}"; done >> "$OUT/assemblies.txt"
sort -u "$OUT/assemblies.txt" | while read -r rel; do bundle_assembly "$rel"; done
cp "$MONO_SRC/etc/mono/4.5/machine.config" "$MONO_SRC/etc/mono/4.5/settings.map" "$APP/Contents/etc/mono/4.5/"
# Mods that call user32 / winmm / gdi32 (keyboard state, timers) get libwin32shim instead.
python3 -I - "$MONO_SRC/etc/mono/config" "$APP/Contents/etc/mono/config" <<'EOF'
import re, sys
text = open(sys.argv[1]).read()
text = re.sub(r'\s*<dllmap dll="(?:i:)?(?:gdi32|user32|winmm)(?:\.dll)?"[^>]*/>', "", text)
maps = "".join(f'\n\t<dllmap dll="i:{d}" target="$mono_libdir/libwin32shim.dylib" os="osx"/>'
               for d in ("user32.dll", "user32", "winmm.dll", "winmm", "gdi32.dll", "gdi32"))
open(sys.argv[2], "w").write(text.replace("<configuration>", "<configuration>" + maps, 1))
EOF

step "Making the icon from the game's own"
ICON="$OUT/icon"
mkdir -p "$ICON/JumpKing.iconset"
python3 -I "$HERE/tools/peicon.py" "$GAME/JumpKing.exe" "$ICON" >/dev/null
swiftc -O -o "$OUT/tools/nnscale" "$HERE/tools/nnscale.swift" 2>/dev/null
for s in 16 32 64 128; do sips -s format png "$ICON/g0_${s}x${s}_32.ico" --out "$ICON/$s.png" >/dev/null; done
tail -c +23 "$ICON/g0_256x256_32.ico" > "$ICON/256.png"   # the 256 px image is a PNG after the .ico header
"$OUT/tools/nnscale" "$ICON/256.png" "$ICON/512.png" 512
"$OUT/tools/nnscale" "$ICON/256.png" "$ICON/1024.png" 1024
for s in 16 32 128 256 512; do
    cp "$ICON/$s.png" "$ICON/JumpKing.iconset/icon_${s}x${s}.png"
    cp "$ICON/$((s * 2)).png" "$ICON/JumpKing.iconset/icon_${s}x${s}@2x.png"
done
mkdir -p "$APP/Contents/Resources"
iconutil -c icns "$ICON/JumpKing.iconset" -o "$APP/Contents/Resources/JumpKing.icns"

cp "$HERE/launcher/JumpKing.sh" "$APP/Contents/MacOS/JumpKing"
chmod +x "$APP/Contents/MacOS/JumpKing"
cat > "$APP/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleDisplayName</key>
	<string>Jump King</string>
	<key>CFBundleExecutable</key>
	<string>JumpKing</string>
	<key>CFBundleIconFile</key>
	<string>JumpKing</string>
	<key>CFBundleIdentifier</key>
	<string>$BUNDLE_ID</string>
	<key>CFBundleName</key>
	<string>Jump King</string>
	<key>CFBundlePackageType</key>
	<string>APPL</string>
	<key>CFBundleShortVersionString</key>
	<string>2025.05.30</string>
	<key>CFBundleVersion</key>
	<string>1</string>
	<key>LSApplicationCategoryType</key>
	<string>public.app-category.games</string>
	<key>LSMinimumSystemVersion</key>
	<string>12.0</string>
	<key>NSHighResolutionCapable</key>
	<true/>
</dict>
</plist>
EOF

# ---- install --------------------------------------------------------------------------------------------
TARGET="$DEST/Jump King.app"
if [ -e "$TARGET" ]; then
    # Only ever replace an app this script made (saves and settings live outside it, in Application Support).
    [ "$(defaults read "$TARGET/Contents/Info" CFBundleIdentifier 2>/dev/null)" = "$BUNDLE_ID" ] \
        || die "$TARGET exists and wasn't made by this script; move it away first"
    pgrep -f "Jump King.app/Contents/MacOS/Jump King" >/dev/null && die "quit Jump King first"
    step "Replacing $TARGET"
    rm -rf "$TARGET"
else
    step "Installing to $TARGET"
fi
mv "$APP" "$TARGET"
/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -f "$TARGET" || true
rm -rf "$OUT"
echo "Done. Start Steam, sign in to the account that owns Jump King, then open Jump King from $DEST."
