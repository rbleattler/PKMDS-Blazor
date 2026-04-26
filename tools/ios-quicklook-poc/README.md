# iOS / iPadOS Quick Look POC

Proof-of-concept for issue [#796](https://github.com/codemonkey85/PKMDS-Blazor/issues/796): a native iOS / iPadOS Quick Look extension that previews PKHeX-compatible files (`.pk1`–`.pk9`, `.pa8`, `.pb7`, `.pb8`, `.sav`) by calling PKHeX.Core through a NativeAOT-compiled dylib.

This is the iOS companion to [`tools/macos-quicklook-poc/`](../macos-quicklook-poc/) (issue [#549](https://github.com/codemonkey85/PKMDS-Blazor/issues/549) / PR [#795](https://github.com/codemonkey85/PKMDS-Blazor/pull/795)). The C# layer (`PkmdsNative/Exports.cs`, `PkmdsNative/HtmlRenderer.cs`) and the rendered HTML are reused **verbatim** from the macOS POC — only the platform shell changes.

## What's in here

```
tools/ios-quicklook-poc/
├── PkmdsNative/                 # C# NativeAOT library — verbatim from macos-quicklook-poc
│   ├── Exports.cs               # C-exported pkmds_* entry points (unchanged)
│   ├── HtmlRenderer.cs          # Self-contained HTML preview (unchanged)
│   └── PkmdsNative.csproj       # RIDs: iossimulator-arm64;ios-arm64
├── xcode/
│   ├── project.yml              # xcodegen spec — iOS host app + .appex extension
│   ├── PkmdsHost/
│   │   └── PkmdsHostApp.swift   # Minimal SwiftUI placeholder
│   └── PkmdsQuickLook/
│       ├── PreviewViewController.swift   # QLPreviewingController + WKWebView (UIKit)
│       └── PkmdsQuickLook.entitlements   # Network client (no sandbox keys — extension is sandboxed by default on iOS)
├── build-extension.sh           # dotnet publish → xcodegen → xcodebuild → install on simulator
└── README.md
```

## Prerequisites

- **Xcode** (full install, not just Command Line Tools) with the **iOS Simulator runtime** installed.
- **.NET SDK** matching `global.json` (net10.0).
- **`ios` .NET workload** — `sudo dotnet workload install ios` (one-time).
- **xcodegen** — `brew install xcodegen`.
- **(Real device only)** Apple Developer account + provisioning profile — see "Signing on iOS" below.

## Build & run

### iOS Simulator (no signing required)

```sh
./build-extension.sh
# Optional: pick a specific simulator
SIM_NAME="iPhone 15 Pro" ./build-extension.sh
```

The script publishes the AOT dylib for `iossimulator-arm64`, regenerates the Xcode project, builds for the simulator, installs the host app, and launches it. Drag a `.pk*` or `.sav` from Finder onto the Simulator window — it lands in **Files → On My iPhone**, where Quick Look should show the styled HTML preview.

### Real device (`ios-arm64`)

```sh
./build-extension.sh --device
```

Builds the dylib for `ios-arm64` and the host app for a generic iOS device — but **does not sign or deploy**. You'll need to open the Xcode project (`xcode/PkmdsQuickLook.xcodeproj`), set your Team / signing identity, and run from Xcode against a connected device. See "Signing on iOS" below for why this isn't automated.

## What's the same as the macOS POC

Verbatim, no changes:
- `PkmdsNative/Exports.cs` — same C-exported `pkmds_*` entry points
- `PkmdsNative/HtmlRenderer.cs` — same HTML output (`color-scheme: light dark`, `-apple-system` font stack, etc.)
- `xcode/PkmdsQuickLook/PreviewViewController.swift` — same `WKWebView` rendering path; only the AppKit/UIKit shell differs (see below)

## What's different from the macOS POC

These are the iOS-specific deltas that the next reader of this code is most likely to trip on.

### 1. UIKit shell instead of AppKit

```swift
// macOS                                        // iOS
import Cocoa                                    import UIKit
import Quartz                                   import QuickLook
final class PreviewViewController:              final class PreviewViewController:
    NSViewController, QLPreviewingController        UIViewController, QLPreviewingController
```

`QLPreviewingController` is the same protocol — it just lives in different parent frameworks. iOS uses `QuickLook.framework`; macOS uses `QuickLookUI.framework` from `Quartz`.

### 2. Transparent WebView background

`NSWindow`-hosted `WKWebView` exposes a private `drawsBackground` flag we toggle via KVC on macOS. UIKit doesn't have that — we set `webView.isOpaque = false`, `webView.backgroundColor = .clear`, and clear the inner scroll view explicitly so the system's preview chrome shows through.

### 3. Deployment target: iOS 16.0

`QLPreviewingController` exists since iOS 11, but iOS 16 is the floor for `WKWebView` color-scheme behaviour we rely on (`UIScreen.traitCollection.userInterfaceStyle` propagation through `WKWebView`). Modern enough that it covers the realistic install base; bump down if a specific requirement surfaces.

### 4. Entitlements differ

iOS extensions are **sandboxed by default** — there's no per-extension `app-sandbox` toggle, and the `com.apple.security.cs.disable-library-validation` macOS escape hatch **does not exist on iOS**. This is the single biggest practical difference between the two POCs.

The iOS entitlements file declares only `com.apple.security.network.client` (so the WebView can fetch HOME sprites from PokeAPI). File access goes through the extension's read-only handoff from the host process — no `files.user-selected.read-only` key needed.

### 5. UTI declarations live in the host app

Same custom UTIs (`com.bondcodes.pkmds.pkm-file`, `com.bondcodes.pkmds.save-file`), same conformance to `public.data`, same filename-extension list. The iOS-specific bits in `project.yml` are `UILaunchScreen`, `UISupportedInterfaceOrientations`, and `TARGETED_DEVICE_FAMILY: "1,2"` (iPhone + iPad).

### 6. Different dylib install path

macOS embeds the dylib at `Contents/Frameworks/PkmdsNative.dylib` and the extension at `Contents/PlugIns/PkmdsQuickLook.appex/Contents/Frameworks/...`. iOS extensions are at `<App>.app/PlugIns/PkmdsQuickLook.appex/Frameworks/...` (no nested `Contents/`), so the rpath is different:

```
LD_RUNPATH_SEARCH_PATHS = "@executable_path/Frameworks @executable_path/../../Frameworks"
```

The first entry resolves the dylib when embedded directly in the extension; the second walks up to the host app's `Frameworks/` directory in case it ends up shared there.

### 7. No `qlmanage` equivalent

macOS has `qlmanage -p <file>` to drive a Quick Look preview from the CLI. iOS has nothing equivalent — verification is "drag a file into the simulator and long-press it." The Web Inspector (Safari → Develop → Simulator → \<extension\>) is your friend for inspecting the rendered HTML inside the extension's `WKWebView`.

## Signing on iOS

The macOS POC's "ad-hoc sign + `disable-library-validation`" trick **does not work on iOS**. App Store extensions require:

- The host app and the extension signed with the same Team ID.
- Any embedded dynamic library (the AOT dylib here) signed with the same Team ID.
- A real provisioning profile (free Apple ID profiles work for personal-device install but not extensions in some cases — verify with your Apple ID).

This means real-device verification of this POC requires an Apple Developer account from the start. Simulator-only validation is enough to confirm the extension loads, the dylib resolves, and the HTML renders — but does not exercise the signing path.

## The known unknowns

These are documented risks from issue [#796](https://github.com/codemonkey85/PKMDS-Blazor/issues/796) that this POC is positioned to verify but cannot finish verifying without the full signing pipeline.

### 1. Extension memory cap (~120 MB)

iOS preview/share extensions are killed when they exceed roughly 120 MB resident. The macOS dylib is 17 MB; runtime PKHeX parsing adds 30-40 MB peak. Should fit comfortably, but **needs verification with the largest realistic fixtures** — full Box dumps, Gen 8/9 saves with thousands of stored Pokémon. The simulator does not enforce the same cap as the device, so verification has to happen on hardware (Xcode's Memory Report gauge during a `qlmanage`-equivalent preview).

### 2. NativeAOT for `ios-arm64` in .NET 10

Officially supported but newer than the macOS path. Expect the same trim warnings as macOS (`IL2070` on `EntityBlank.GetBlank` and `ReflectUtil.GetAllProperties`, plus three "always throw" notes on `SaveBlock3*.PrintMembers`). Track with:

```sh
dotnet publish PkmdsNative/PkmdsNative.csproj -c Release -r ios-arm64 -v normal 2>&1 \
    | grep -E 'warning IL|warning AOT'
```

### 3. dylib install validation

Same class of failure as macOS gotcha #5 (library validation rejects mismatched signatures), but on iOS there's no escape hatch entitlement. The dylib **must** be signed with the same Team ID as the extension, full stop. Verify after a signed build with:

```sh
codesign -dv <App>.app/PlugIns/PkmdsQuickLook.appex/Frameworks/PkmdsNative.dylib
```

The "Authority=" line should match the host app and extension.

### 4. Quick Look extension activation

iOS won't activate an extension until the host app has been launched at least once after install. The build script launches the host app automatically; if the extension isn't picked up, kill and relaunch the host app, then reboot the simulator (`xcrun simctl shutdown booted; xcrun simctl boot <UUID>`) and try again.

### 5. Web Inspector access

For the simulator: Safari → Develop → Simulator → "PkmdsQuickLook" appears once an actual preview is in flight. If it's missing, the extension isn't running — check the simulator's diagnostic logs:

```sh
xcrun simctl spawn booted log stream --level debug \
    --predicate 'process == "QuickLookUIService" OR process == "PkmdsQuickLook"'
```

## Out of scope for this POC

- App Store / TestFlight distribution — covered by "Macros" below.
- iCloud / sync — Quick Look is read-only by definition.
- Bundling sample fixtures — keeps the POC honest about the security-scoped URL handoff.
- Wiring up the iOS Files-app share extension (separate `NSExtensionPointIdentifier`) — out of scope; this is preview only.

## Macros for next session

If this POC graduates to a real `tools/ios-quicklook/` (no `-poc` suffix):

1. Set up Apple Developer ID + provisioning profile + automatic signing.
2. Verify the 120 MB memory ceiling against full Box-dump fixtures on a real device.
3. Set up TestFlight distribution.
4. Mirror the macOS "promote `ImageHelper` from `Pkmds.Rcl` into `Pkmds.Core`" cleanup so both platforms share the sprite-URL helper.
5. Decide whether the iOS host app should also expose a usable UI (vs. the current placeholder); the macOS host has the same question open.
6. Investigate string-table trimming on the AOT dylib if size matters more on iOS than it did on macOS.
