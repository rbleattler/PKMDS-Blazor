#!/usr/bin/env bash
# Generates the Xcode project, builds for the iOS Simulator, installs + launches.
# Refreshes the PKMDS bundle on first run (or when --refresh is passed).
#
#   ./build-and-run.sh                 # build + run on default simulator
#   ./build-and-run.sh --refresh       # re-publish PKMDS first
#   SIM_NAME="iPhone 15" ./build-and-run.sh
set -euo pipefail

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
XCODE_DIR="$SCRIPT_DIR/xcode"
PROJECT="$XCODE_DIR/PkmdsHost.xcodeproj"
DERIVED="$XCODE_DIR/build"
BUNDLE_DIR="$XCODE_DIR/PkmdsHost/Resources/PKMDS"

APP_NAME="PkmdsHost"
BUNDLE_ID="com.bondcodes.pkmds.host.ios"
# Default: first available iPhone simulator. Override with SIM_NAME="iPhone 15 Pro".
SIM_NAME="${SIM_NAME:-}"

if [[ "${1:-}" == "--refresh" ]] || [[ ! -d "$BUNDLE_DIR" ]] || [[ -z "$(ls -A "$BUNDLE_DIR" 2>/dev/null)" ]]; then
    echo "==> refresh PKMDS bundle"
    "$SCRIPT_DIR/refresh-pkmds.sh"
fi

echo "==> xcodegen generate"
( cd "$XCODE_DIR" && xcodegen generate --quiet )

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

echo "==> xcodebuild (Debug, simulator)"
xcodebuild \
    -project "$PROJECT" \
    -scheme "$APP_NAME" \
    -configuration Debug \
    -destination "id=$SIM_UUID" \
    -derivedDataPath "$DERIVED" \
    CODE_SIGNING_ALLOWED=NO \
    -quiet \
    build

APP_PATH="$DERIVED/Build/Products/Debug-iphonesimulator/$APP_NAME.app"
[[ -d "$APP_PATH" ]] || { echo "missing built app at $APP_PATH" >&2; exit 1; }

echo "==> install + launch"
xcrun simctl install "$SIM_UUID" "$APP_PATH"
xcrun simctl launch "$SIM_UUID" "$BUNDLE_ID"

echo
echo "Launched $APP_NAME on $SIM_NAME."
echo "Tap 'Pick a save…' and choose a .sav from the simulator's Files app."
echo "(In the simulator: Files → Browse → On My iPhone — drag a .sav into Finder's Simulator window to add one.)"
