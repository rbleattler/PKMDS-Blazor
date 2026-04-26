#!/usr/bin/env bash
# Builds the iOS NativeAOT dylib + Xcode host app + Quick Look extension for the
# iOS Simulator, installs to a booted simulator, and launches the host app.
#
#   ./build-extension.sh                                # default simulator
#   SIM_NAME="iPhone 15 Pro" ./build-extension.sh
#   ./build-extension.sh --device                       # build for ios-arm64 (real device, requires signing)
set -euo pipefail

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
REPO_ROOT="$( cd "$SCRIPT_DIR/../.." && pwd )"

# --device flips the AOT publish target + Xcode destination to a real iOS device.
# Real-device runs require manual signing (Developer ID + provisioning profile)
# that this script does not configure.
TARGET_DEVICE=0
if [[ "${1:-}" == "--device" ]]; then
    TARGET_DEVICE=1
fi

if [[ "$TARGET_DEVICE" -eq 1 ]]; then
    RID="ios-arm64"
    DEST="generic/platform=iOS"
    PRODUCTS_DIR="Release-iphoneos"
else
    RID="iossimulator-arm64"
    DEST=""
    PRODUCTS_DIR="Release-iphonesimulator"
fi

CSPROJ="$SCRIPT_DIR/PkmdsNative/PkmdsNative.csproj"
PUBLISH_DIR="$SCRIPT_DIR/PkmdsNative/bin/Release/net10.0/$RID/publish"
DYLIB_SRC="$PUBLISH_DIR/PkmdsNative.dylib"
DYLIB_DEST="$SCRIPT_DIR/build-resources/PkmdsNative.dylib"

XCODE_DIR="$SCRIPT_DIR/xcode"
PROJECT="$XCODE_DIR/PkmdsQuickLook.xcodeproj"
DERIVED="$XCODE_DIR/build"
APP_PATH="$DERIVED/Build/Products/$PRODUCTS_DIR/PkmdsHost.app"

# Verify the ios workload is installed; the publish below depends on it.
if ! dotnet workload list 2>/dev/null | grep -qE '^ios\b'; then
    cat <<'EOF' >&2
==> error: the 'ios' .NET workload is not installed.
    Install it with:
        sudo dotnet workload install ios
    (Requires a paid Apple Developer account only for real-device runs;
    simulator builds work without one.)
EOF
    exit 1
fi

echo "==> dotnet publish ($RID, AOT)"
dotnet publish "$CSPROJ" -c Release -r "$RID" --nologo

[[ -f "$DYLIB_SRC" ]] || { echo "missing $DYLIB_SRC" >&2; exit 1; }

echo "==> stage dylib at $DYLIB_DEST"
mkdir -p "$(dirname "$DYLIB_DEST")"
cp "$DYLIB_SRC" "$DYLIB_DEST"

echo "==> xcodegen generate"
( cd "$XCODE_DIR" && xcodegen generate --quiet )

if [[ "$TARGET_DEVICE" -eq 1 ]]; then
    echo "==> xcodebuild PkmdsHost (Release, iOS device — signing left to user)"
    xcodebuild \
        -project "$PROJECT" \
        -scheme PkmdsHost \
        -configuration Release \
        -destination "$DEST" \
        -derivedDataPath "$DERIVED" \
        -quiet \
        build
    echo
    echo "Built: $APP_PATH"
    echo "Sign with your Developer ID + provisioning profile, then deploy via Xcode or 'ios-deploy'."
    exit 0
fi

# Pick a simulator (override with SIM_NAME=...)
SIM_NAME="${SIM_NAME:-}"
if [[ -n "$SIM_NAME" ]]; then
    SIM_LINE=$(xcrun simctl list devices available 2>/dev/null \
        | grep -E "^\s+$SIM_NAME \(" \
        | head -1)
else
    SIM_LINE=$(xcrun simctl list devices available 2>/dev/null \
        | grep -E "^\s+iPhone " \
        | head -1)
fi
SIM_UUID=$(echo "$SIM_LINE" | sed -E 's/.*\(([A-F0-9-]+)\).*/\1/')
SIM_NAME=$(echo "$SIM_LINE" | sed -E 's/^\s+(.*) \([A-F0-9-]+\).*/\1/')

if [[ -z "$SIM_UUID" ]]; then
    echo "no iPhone simulator found. Available:" >&2
    xcrun simctl list devices available | grep -E "^\s+iPhone|^\s+iPad" | sed -E 's/^\s+//' >&2
    exit 1
fi

echo "==> using simulator $SIM_NAME ($SIM_UUID)"
xcrun simctl boot "$SIM_UUID" 2>/dev/null || true
open -a Simulator

echo "==> xcodebuild PkmdsHost (Release, iOS Simulator, unsigned)"
xcodebuild \
    -project "$PROJECT" \
    -scheme PkmdsHost \
    -configuration Release \
    -destination "id=$SIM_UUID" \
    -derivedDataPath "$DERIVED" \
    CODE_SIGNING_ALLOWED=NO \
    -quiet \
    build

[[ -d "$APP_PATH" ]] || { echo "missing built app at $APP_PATH" >&2; exit 1; }

echo "==> install + launch"
xcrun simctl uninstall "$SIM_UUID" com.bondcodes.pkmds.host.ios 2>/dev/null || true
xcrun simctl install "$SIM_UUID" "$APP_PATH"
xcrun simctl launch "$SIM_UUID" com.bondcodes.pkmds.host.ios

echo
echo "Launched PkmdsHost on $SIM_NAME."
echo "Drag a .pk*/.sav from Finder onto the Simulator window, then long-press the file in Files.app to preview."
