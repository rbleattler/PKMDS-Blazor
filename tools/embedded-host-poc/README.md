# Embedded Host PoC — Browser-based mock host page

Proof-of-concept for issue [#798](https://github.com/codemonkey85/PKMDS-Blazor/issues/798): a standalone HTML page that simulates what a native host (iOS/iPadOS `WKWebView` app) has to build, using only browser APIs. Lets you verify the embedded JS bridge end-to-end without Xcode, a device, or a real `WKWebView`.

See [`EMBEDDED_HOST_GUIDE.md`](../../EMBEDDED_HOST_GUIDE.md) for the full integration contract this PoC exercises.

## What's in here

```
tools/embedded-host-poc/
├── index.html     # Mock host page — rendered in a browser tab
└── mock-host.js   # Parent-page JS (file picker, Done/Cancel, log panel)
```

The companion to this PoC is the `?host=poc` polyfill added to `Pkmds.Web/wwwroot/js/app.js`, which activates only when `?host=poc` is in the URL and routes PKMDS outbound messages via `window.parent.postMessage`.

## How it works

```
┌─────────────────────────────────────────────────────────────┐
│  Browser tab  —  tools/embedded-host-poc/index.html         │
│                                                              │
│  ┌──── Mock native chrome ────────────────────────────────┐ │
│  │  [Cancel]    PKMDS (Mock Host PoC)    [Done]           │ │
│  │  Load save: [Choose .sav…]  status text                │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                              │
│  ┌──── PKMDS iframe (?host=poc) ──────────┐ ┌─ Log panel ┐ │
│  │                                         │ │  ready     │ │
│  │   (Blazor WASM save editor)             │ │  loadSave  │ │
│  │                                         │ │  saveExport│ │
│  └─────────────────────────────────────────┘ └────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

All communication goes through `window.postMessage` — the same cross-origin mechanism a real `WKWebView` uses for `webkit.messageHandlers`. No same-origin requirement, no build step.

## End-to-end flow

1. Page loads → iframe boots PKMDS with `?host=poc`.
2. PKMDS boots, posts `ready` → parent enables the file picker and Cancel/Done buttons.
3. Pick a `.sav` → parent reads it, base64-encodes it, sends `loadSave` to the iframe.
4. PKMDS decodes and renders the save (boxes/party appear).
5. Tap **Done** → parent calls `requestExport()` in the iframe → PKMDS posts `saveExport` with base64 bytes → parent decodes and triggers a browser download.
6. Tap **Cancel** → parent resets state; save is not exported.

## How to run

### Option A — same-origin serve (recommended, relative iframe URL)

Start the PKMDS dev server and open the PoC from the same origin:

```powershell
# From repo root (PowerShell)
./watch.ps1
```

Then copy the PoC files alongside the dev site temporarily, or access them from a server that shares the same origin as `localhost:5283`. The simplest approach:

```powershell
# Copy PoC files into wwwroot so they're served from the same origin
Copy-Item tools/embedded-host-poc/index.html   Pkmds.Web/wwwroot/embedded-poc/index.html -Force
Copy-Item tools/embedded-host-poc/mock-host.js Pkmds.Web/wwwroot/embedded-poc/mock-host.js -Force
```

Then open `http://localhost:5283/embedded-poc/` in your browser.

### Option B — open directly as a local file

The PoC works without any server. Open `index.html` directly in your browser (`File → Open…`). The page defaults to `http://localhost:5283/?host=poc` for the iframe, so the PKMDS dev server must be running at that address.

```powershell
./watch.ps1          # keep running
# then open tools/embedded-host-poc/index.html in your browser
```

To point the iframe at a different PKMDS URL, append `?pkmds=<url>`:

```
file:///path/to/index.html?pkmds=http://localhost:5283
```

## Two different developer tools — why not just use `?host=test`?

| | `?host=test` | Mock host page |
|---|---|---|
| **Lives** | Inside PKMDS | Outside PKMDS |
| **Audience** | PKMDS contributors smoke-testing the bridge | Downstream integrators learning what their app must build |
| **File picker** | PKMDS renders its own picker | Mock host renders the picker (as a native app would) |
| **Done/Cancel chrome** | Not present | Shown (demonstrating the host's responsibility) |
| **Message log** | DevTools console only | Visible UI panel |

## Known PoC limitations

- **No real WKWebView** — this is a browser-on-browser simulation. Cross-origin policy, postMessage timing, and browser file-picker behaviour differ from a native WKWebView host.
- **One-shot session** — no save persistence between page reloads (same as real embed mode).
- **Export is raw bytes** — Manic EMU `.zip` wrapper is not rebuilt (see `EMBEDDED_HOST_GUIDE.md` § 5).
- **Not a polished UI** — developer-facing demo only; production host chrome is your app's responsibility.
