#!/usr/bin/env bash
# Publishes Pkmds.Web and stages the wwwroot/ bundle into the iOS app's resources.
# Strips PWA-only files that aren't reachable in embed mode (per EMBEDDED_HOST_GUIDE.md §7).
#
# Run after editing anything under Pkmds.Web / Pkmds.Rcl / Pkmds.Core.
# build-and-run.sh invokes this automatically when Resources/PKMDS is missing.
set -euo pipefail

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
REPO_ROOT="$( cd "$SCRIPT_DIR/../.." && pwd )"

PUBLISH_OUT="$REPO_ROOT/release"
PUBLISH_WWWROOT="$PUBLISH_OUT/wwwroot"
DEST="$SCRIPT_DIR/xcode/PkmdsHost/Resources/PKMDS"

PWA_ONLY_FILES=(
    "manifest.webmanifest"
    "service-worker.js"
    "service-worker.published.js"
    "BrowserNotSupported.html"
    "staticwebapp.config.json"
)

echo "==> dotnet publish Pkmds.Web (Release)"
dotnet publish "$REPO_ROOT/Pkmds.Web/Pkmds.Web.csproj" \
    -c Release \
    -o "$PUBLISH_OUT" \
    --nologo

[[ -d "$PUBLISH_WWWROOT" ]] || { echo "missing $PUBLISH_WWWROOT" >&2; exit 1; }

echo "==> stage bundle at $DEST"
rm -rf "$DEST"
mkdir -p "$DEST"
# -a preserves perms + recurses; trailing /. copies contents not the parent dir
cp -a "$PUBLISH_WWWROOT/." "$DEST/"

echo "==> strip PWA-only files"
for f in "${PWA_ONLY_FILES[@]}"; do
    if [[ -f "$DEST/$f" ]]; then
        rm "$DEST/$f"
        echo "    removed $f"
    fi
done

BUNDLE_SIZE=$(du -sh "$DEST" | awk '{print $1}')
FILE_COUNT=$(find "$DEST" -type f | wc -l | tr -d ' ')
echo
echo "Staged $FILE_COUNT files ($BUNDLE_SIZE) at $DEST"
