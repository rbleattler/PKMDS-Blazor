#!/usr/bin/env bash
# Builds the AOT dylib + Xcode host app + Quick Look extension, signs ad-hoc,
# registers with Launch Services, and runs qlmanage on a fixture.
#
#   ./build-extension.sh                                  # default fixture
#   ./build-extension.sh path/to/file.pk5
#
set -euo pipefail

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
REPO_ROOT="$( cd "$SCRIPT_DIR/../.." && pwd )"
RID="osx-arm64"

FIXTURE="${1:-$REPO_ROOT/TestFiles/Lucario_B06DDFAD.pk5}"

CSPROJ="$SCRIPT_DIR/PkmdsNative/PkmdsNative.csproj"
PUBLISH_DIR="$SCRIPT_DIR/PkmdsNative/bin/Release/net10.0/$RID/publish"
DYLIB_SRC="$PUBLISH_DIR/PkmdsNative.dylib"
DYLIB_DEST="$SCRIPT_DIR/build-resources/PkmdsNative.dylib"

XCODE_DIR="$SCRIPT_DIR/xcode"
PROJECT="$XCODE_DIR/PkmdsQuickLook.xcodeproj"
DERIVED="$SCRIPT_DIR/xcode/build"
APP_PATH="$DERIVED/Build/Products/Release/PkmdsHost.app"

echo "==> dotnet publish ($RID, AOT)"
dotnet publish "$CSPROJ" -c Release -r "$RID" --nologo

[[ -f "$DYLIB_SRC" ]] || { echo "missing $DYLIB_SRC" >&2; exit 1; }

echo "==> stage dylib at $DYLIB_DEST"
cp "$DYLIB_SRC" "$DYLIB_DEST"

echo "==> xcodegen generate"
( cd "$XCODE_DIR" && xcodegen generate --quiet )

echo "==> xcodebuild PkmdsHost (Release, ad-hoc signed)"
xcodebuild \
    -project "$PROJECT" \
    -scheme PkmdsHost \
    -configuration Release \
    -derivedDataPath "$DERIVED" \
    CODE_SIGN_IDENTITY=- \
    CODE_SIGN_STYLE=Manual \
    DEVELOPMENT_TEAM= \
    -quiet \
    build

[[ -d "$APP_PATH" ]] || { echo "missing built app at $APP_PATH" >&2; exit 1; }

echo "==> codesign extension with entitlements + bundle ad-hoc"
ENTITLEMENTS="$XCODE_DIR/PkmdsQuickLook/PkmdsQuickLook.entitlements"
APPEX="$APP_PATH/Contents/PlugIns/PkmdsQuickLook.appex"
codesign --force --sign - --timestamp=none "$APPEX/Contents/Frameworks/PkmdsNative.dylib"
codesign --force --sign - --timestamp=none --options runtime --entitlements "$ENTITLEMENTS" "$APPEX"
codesign --force --sign - --timestamp=none "$APP_PATH"

echo "==> deploy to /Applications and register only that copy"
LSREGISTER=/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister
INSTALLED=/Applications/PkmdsHost.app
osascript -e 'quit app "PkmdsHost"' 2>/dev/null || true
sleep 1
"$LSREGISTER" -u "$APP_PATH" 2>/dev/null || true   # unregister the build-dir copy
"$LSREGISTER" -u "$INSTALLED" 2>/dev/null || true
rm -rf "$INSTALLED"
cp -R "$APP_PATH" "$INSTALLED"
"$LSREGISTER" -f "$INSTALLED"
pluginkit -a "$INSTALLED/Contents/PlugIns/PkmdsQuickLook.appex"
pluginkit -e use -i com.bondcodes.pkmds.host.quicklook
killall pkd quicklookd 2>/dev/null || true
sleep 1
qlmanage -r >/dev/null 2>&1 || true
qlmanage -r cache >/dev/null 2>&1 || true

echo "==> qlmanage -p $FIXTURE"
qlmanage -p "$FIXTURE" 2>&1 | tail -20 || true

echo
echo "Built and installed: $INSTALLED"
echo "Press Space on a .pk5/.sav in Finder to preview."
