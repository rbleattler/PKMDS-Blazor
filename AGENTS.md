# AGENTS.md

Guidance for AI agents and coding assistants working in this repository.

## Quick commands

Prereqs
- Install the .NET SDK from `global.json`: `dotnet --list-sdks` to verify.
- Install Blazor WASM tooling once: `dotnet workload install wasm-tools`.
- IDE users can open `Pkmds.slnx`. CLI builds target individual projects (e.g., `Pkmds.Web.csproj`).

Local dev (Blazor WASM)
- From repo root (PowerShell): `./watch.ps1`
  - Equivalent from `Pkmds.Web`: `dotnet watch run -c Debug -v n --no-hot-reload`
  - Launch profiles expose: http `http://localhost:5283`, https `https://localhost:7267` (see `Pkmds.Web/Properties/launchSettings.json`).

Restore and build
- Restore: `dotnet restore`
- Build Web (Debug/Release):
  - `dotnet build Pkmds.Web/Pkmds.Web.csproj -c Debug`
  - `dotnet build Pkmds.Web/Pkmds.Web.csproj -c Release`

Format/lint
- Uses `.editorconfig`; Debug builds treat warnings as errors.
- Run: `dotnet format` then `dotnet build -c Debug`.

Tests
- Test project: `Pkmds.Tests` (xUnit + bUnit; coverage via coverlet collector).
- Run all: `dotnet test -c Release`
- Filter: `dotnet test --filter "FullyQualifiedName~Namespace.Class.TestMethod"`

Publish locally (static site)
- `dotnet publish Pkmds.Web/Pkmds.Web.csproj -c Release -o release --nologo`
- `Copy-Item release/wwwroot/index.html release/wwwroot/404.html -Force`
- `New-Item -ItemType File release/wwwroot/.nojekyll -Force | Out-Null`
- Deployable output: `release/wwwroot/`.

## Architecture overview

- Single route `/`; all UI lives inside `SaveFileComponent` as a `MudTabs`-based layout.
- `RefreshAwareComponent` base class (in `Pkmds.Rcl`) auto-subscribes to `IRefreshService.OnAppStateChanged` — prefer it over `ComponentBase` for any component that reacts to save-file state changes.
- `GlobalUsings.cs` in `Pkmds.Rcl` imports: `MudBlazor`, `PKHeX.Core`, `Pkmds.Rcl.Components`, `Pkmds.Rcl.Services`, and aliases `Severity = MudBlazor.Severity` (avoids ambiguity with PKHeX's own `Severity`).
- New tabs: add a `<MudTabPanel>` entry in `SaveFileComponent.razor`; put the tab component under `Pkmds.Rcl/Components/MainTabPages/`; use namespace `Pkmds.Rcl.Components.MainTabPages` in the code-behind.
- Tab navigation: bind `@bind-ActivePanelIndex` on `MudTabs` and pass an `EventCallback` parameter to child tabs that need to trigger navigation.

## Project layout and build

- `Directory.Build.props`: sets global `TargetFramework` to `net10.0`, repo metadata, nullable + implicit usings, and `TreatWarningsAsErrors` in Debug.
- `Directory.Packages.props`: central package management (PKHeX.Core, MudBlazor, Serilog, FileSystemAccess, etc.).
- Projects
  - `Pkmds.Core/` (Class Library): PKHeX utilities and extensions. Reusable, UI-independent logic for working with PKHeX.Core (species validation, shiny handling, markings, etc.).
  - `Pkmds.Rcl/` (Razor Class Library): shared UI/components and services. Tailwind integrated via `Tailwind.targets` (input at `Pkmds.Rcl/wwwroot/css/tailwind.input.css`). References `Pkmds.Core`.
  - `Pkmds.Web/` (Blazor WebAssembly): PWA host. Linking and compression enabled; service worker assets generated on publish. JS libs via `libman.json` (crypto-js).
  - `Pkmds.Tests/` (tests): `net10.0`, references RCL and Core.

Runtime wiring (Web)
- Serilog to Browser Console with adjustable level (via `LoggingLevelSwitch`).
- DI: `IAppState`, `IRefreshService`, `IAppService`, drag/drop, logging service, File System Access, MudBlazor, JS interop.
- PKHeX crypto bridged to JS: `RuntimeCryptographyProvider.Aes/Md5` set to Blazor providers at startup.

## CI/CD (GitHub Actions)

- `.github/workflows/buildandtest.yml` (dev branches; PRs to main):
  - Setup .NET (from `global.json`), install wasm tools, restore/build Web (Release), run `dotnet test`.
- `.github/workflows/main.yml` (main):
  - Setup .NET + wasm tools, restore, publish Web to `release/`, copy `index.html` → `404.html`, add `.nojekyll`, replace `%%CACHE_VERSION%%` in `service-worker.published.js`, deploy `release/wwwroot` to `gh-pages`.
- `.github/workflows/codeql.yml`: CodeQL for C# and JS/TS (manual C# build step).

## PKHeX.Core

This app depends heavily on [PKHeX.Core](https://github.com/kwsch/PKHeX). When implementing features, use the PKHeX WinForms app as a reference for how to leverage PKHeX.Core — both for UI/UX patterns and for understanding the correct API usage. The first place you should look when referencing PKHeX is `~/Code/codemonkey85/PKHeX` (macOS) or `C:\Code\codemonkey85\PKHeX` (Windows), which contains the PKHeX WinForms app source code. The `PKHeX.Core` project within that solution is the library we consume here, and the WinForms app is a separate project that references it. The PKHeX WinForms app is a great reference for how to use PKHeX.Core effectively, as it demonstrates real-world usage of the library's APIs in a production application. If you can't find the source there, the PKHeX Wiki and source code are also valuable resources for understanding how to work with PKHeX.Core.

If you encounter bugs or limitations in PKHeX.Core while working on an issue or PR, note them in a code comment at the relevant site and report them on the GitHub issue or PR you are working on.

### PKHeX.Core API notes

- `CheckResult` is a **struct** (value type) — never use `FirstOrDefault()` or other null-returning LINQ on collections of it.
- Spelling: `result.Judgement` (British English), not `Judgment`.
- Human-readable legality messages: `var ctx = LegalityLocalizationContext.Create(la); ctx.Humanize(in result, verbose: false)`.
- Box iteration: `saveFile.BoxCount`, `saveFile.BoxSlotCount`, `saveFile.GetBoxSlotAtIndex(box, slot)`.
- Party iteration: `saveFile.PartyCount`, `saveFile.GetPartySlotAtIndex(i)`.
- `ParseSettings.ActiveTrainer` is internal and set by `InitFromSaveFileData(sav)` — this enables the handler check in `HistoryVerifier.VerifyHandlerState`.
- `ParseSettings.AllowGBCartEra` is set by `InitFromSaveFileData` — `true` for physical Gen 1/2 saves (enables GB era events), `false` for VC saves. Do **not** override it globally to `false`; that breaks legitimate events (e.g. Nintendo Event Mew, GS Ball Celebi) on physical cartridge saves.

## Data generation tools

Static JSON data files consumed by `DescriptionService` are generated from external sources using .NET 10 file-based apps in `tools/`. Run them whenever the upstream data changes.

### `tools/generate-descriptions.cs`

Generates `ability-info.json`, `move-info.json`, and `item-info.json` from PokeAPI CSV data, with optional secondary-effect supplement from Pokémon Showdown.

- **Source**: PokeAPI repo (CSV files under `data/v2/csv/`)
- **Source (optional)**: Pokémon Showdown repo (`data/moves.ts`) — supplements secondary effects (stat changes, status, flinch, drain, multi-hit, crit rate) for Gen 8+ moves that PokeAPI's `move_meta` CSV doesn't cover yet
- **Output**: `Pkmds.Rcl/wwwroot/data/`

```sh
# Without Showdown (PokeAPI data only)
# macOS:
dotnet run tools/generate-descriptions.cs -- --pokeapi ~/Code/codemonkey85/pokeapi
# Windows:
dotnet run tools/generate-descriptions.cs -- --pokeapi C:\Code\codemonkey85\pokeapi

# With Showdown supplement (recommended — fills Gen 8+ move secondary effects)
# macOS:
dotnet run tools/generate-descriptions.cs -- --pokeapi ~/Code/codemonkey85/pokeapi --showdown ~/Code/pokemon-showdown
# Windows:
dotnet run tools/generate-descriptions.cs -- --pokeapi C:\Code\codemonkey85\pokeapi --showdown C:\Code\pokemon-showdown
```

### `tools/generate-tm-data.cs`

Generates `tm-data.json` from the Bulbapedia "List of TMs" page. Also merges Sword/Shield TR data (TR00–TR99) from hardcoded PKHeX.Core move IDs. Requires `move-info.json` to already exist (run `generate-descriptions.cs` first).

- **Source**: Fetched directly from https://bulbapedia.bulbagarden.net/wiki/List_of_TMs (or supply a saved HTML file with `--input`)
- **Output**: `Pkmds.Rcl/wwwroot/data/tm-data.json`

```sh
# Fetch live from Bulbapedia (default)
dotnet run tools/generate-tm-data.cs

# Use a previously saved HTML file
dotnet run tools/generate-tm-data.cs -- --input "path/to/List of TMs - Bulbapedia.html"
```

Both scripts default output to `Pkmds.Rcl/wwwroot/data/` by walking up from the working directory to find the repo root. Pass `--output /path` to override.

## MudBlazor and Razor gotchas

- `ComboItem` (PKHeX) is a **sealed record** (reference type). Using `ComboItem?` in a Razor `@bind-Value` triggers CS8669 in the Razor-generated code — use `int?` with `.Value` for select bindings instead.
- `MudExpansionPanel` in MudBlazor 9 has no `IsInitiallyExpanded` or `IsExpanded` parameters (triggers MUD0002 analyzer error) — omit them; panels start collapsed by default.
- Razor integer literals in attributes must be parenthesised: `Value="@((int?)0)"`, not `Value="@0"`.
- Nullable reference type casts in Razor: `(string?)null` triggers CS8669 — use `default(string)` instead.
- `MudTable` `RowStyleFunc` signature is `Func<T, int, string>` (item + row index), not `Func<T, string>`.

## Local source references

Prefer reading local source over fetching from GitHub or relying solely on docs:

- **PKHeX**: macOS `~/Code/codemonkey85/PKHeX`, Windows `C:\Code\codemonkey85\PKHeX`
- **MudBlazor**: macOS `~/Code/MudBlazor`, Windows `C:\Code\MudBlazor`
- **Pokémon Showdown**: macOS `~/Code/pokemon-showdown`, Windows `C:\Code\pokemon-showdown`
- **PokeAPI**: macOS `~/Code/codemonkey85/pokeapi`, Windows `C:\Code\codemonkey85\pokeapi`

## Workflow

- **Tests**: Do not run `dotnet test` locally — leave it to the CI GitHub Actions workflow (`.github/workflows/buildandtest.yml`). Run only `dotnet format` and `dotnet build -c Debug` to verify changes locally.
- **PR review feedback**: (1) Review all comments and plan the response; (2) reply to each individual comment on the PR explaining what you're doing and why; (3) make code changes, commit, and push; (4) mark all addressed comments as resolved on the PR.

## Known upstream issues

- **MudBlazor bag virtualization** (`BagTab`): `MudDataGrid` `Virtualize="true"` is intentionally disabled due to a MudBlazor bug — `DataGridVirtualizeRow` passes `SpacerElement="div"` inside `<tbody>`, which CSS collapses to 0 px, causing rows to jump on scroll. A fix has been submitted upstream (MudBlazor/MudBlazor#12799). Once it is merged and released: bump the MudBlazor version in `Directory.Packages.props`, then re-enable `Virtualize="true"` on the bag `MudDataGrid`.

## Notes

- Respect the existing code style. Reference `.editorconfig` for formatting rules; Debug builds treat warnings as errors.
- Use `watch.ps1` for a consistent local dev experience.
- If WASM crypto errors occur, ensure `libman restore` has brought down `crypto-js` (or run LibMan in your IDE). CI publishes without requiring LibMan on the runner because the published output contains required assets.
