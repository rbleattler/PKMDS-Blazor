# Third-Party Notices

PKMDS-Blazor builds on the work of many open-source projects, community resources, and the Pokémon save-editing community. This file lists the third-party code, assets, and data we depend on or have adapted.

PKMDS-Blazor itself is licensed under [GPL-3.0](LICENSE.md). Third-party components retain their own licenses as noted below.

---

## Embedded code and assets (adapted into this repository)

### PKHeX.Core
- **Project:** [github.com/kwsch/PKHeX](https://github.com/kwsch/PKHeX) by Kaphotics and contributors
- **License:** GPL-3.0
- **Used as:** Primary save-file parsing/editing library (NuGet dependency, not embedded). PKMDS would not exist without this.

### PKHeX Feebas Locator Plugin
- **Project:** [github.com/Bl4ckSh4rk/PKHeXFeebasLocatorPlugin](https://github.com/Bl4ckSh4rk/PKHeXFeebasLocatorPlugin) by Bl4ckSh4rk and contributors
- **License:** GPL-3.0
- **Used as:** Source of the Feebas tile RNG algorithms, tile→pixel coordinate data, and route map images for the in-app Feebas locator (issue #815). Ported files carry per-file attribution headers.
- **Algorithm credits flowed through the plugin:** TuxSH for [RSE](https://www.smogon.com/forums/threads/past-gen-rng-research.61090/page-34#post-3986326) and [DPPt](https://www.smogon.com/forums/threads/past-gen-rng-research.61090/page-36#post-4079097) Feebas RNG research; suloku for the original Feebas Fishing Spot tool; foohyfooh for BDSP support.

### Auto Legality Mod (PKHeX-Plugins)
- **Project:** [github.com/architdate/PKHeX-Plugins](https://github.com/architdate/PKHeX-Plugins) by architdate, kwsch, and contributors
- **License:** GPL-3.0
- **Used as:** Reference implementation and inspiration for the Auto-Legality engine (`Pkmds.Rcl/Services/LegalizationService.cs`).

### pokesprite-spritesheet
- **Project:** [github.com/msikma/pokesprite-spritesheet](https://github.com/msikma/pokesprite-spritesheet) by msikma and contributors
- **License:** see upstream repo
- **Used as:** Bundled Pokémon, item, and ball sprites in `Pkmds.Rcl/wwwroot/sprites/`.

### PokeDings
- **Project:** [github.com/msikma/PokeDings](https://github.com/msikma/PokeDings) by msikma and contributors
- **License:** see upstream repo
- **Used as:** Bundled UI graphics in `Pkmds.Rcl/wwwroot/sprites/`.

---

## External data sources (consumed by build-time tools)

### PokeAPI
- **Project:** [pokeapi.co](https://pokeapi.co/) — [github.com/PokeAPI/pokeapi](https://github.com/PokeAPI/pokeapi)
- **Used by:** `tools/generate-descriptions.cs` (ability/move/item descriptions and metadata via the PokeAPI CSV files); `Pkmds.Core/Utilities/PokeApiSpriteUrls.cs` (sprite URLs).

### Pokémon Showdown
- **Project:** [pokemonshowdown.com](https://pokemonshowdown.com/) — [github.com/smogon/pokemon-showdown](https://github.com/smogon/pokemon-showdown) by Smogon University and contributors
- **License:** MIT (code)
- **Used by:** `tools/generate-descriptions.cs` (Gen 8+ move secondary effects from `data/moves.ts`; Gen 9 item shortDescs from `data/text/items.ts` as PokeAPI fallbacks).

### Bulbapedia
- **Project:** [bulbapedia.bulbagarden.net](https://bulbapedia.bulbagarden.net/)
- **License:** Content licensed under [CC BY-NC-SA 2.5](https://creativecommons.org/licenses/by-nc-sa/2.5/)
- **Used by:** `tools/generate-tm-data.cs` (TM/HM lists scraped from the "List of TMs" article).

### Pokémon Database (pokemondb.net)
- **Project:** [pokemondb.net](https://pokemondb.net/)
- **Used by:** `tools/scrape-pokemondb-descriptions.cs` (last-resort fallback descriptions for items and moves not covered by PokeAPI or Showdown). Cached overrides are persisted in `tools/data/description-overrides.json`.

---

## Trademarks

Pokémon and all related characters, names, and properties are trademarks of Nintendo, Game Freak, and The Pokémon Company. PKMDS-Blazor is a fan-made tool not affiliated with, endorsed, or sponsored by any of those entities. Game data and sprite assets remain the intellectual property of their respective owners; we use them under fan-community conventions to interoperate with legally-owned save files.

---

## Adding a new entry

When porting code or embedding assets from a third-party project:

1. Add an entry to the appropriate section above (project, link, license, what we use it for).
2. Add a per-file attribution header to ported source files in the form:
   ```csharp
   // Adapted from <ProjectName> (<URL>)
   // Copyright (c) <Authors>. Licensed under <License>.
   // Ported to Blazor for PKMDS-Blazor by <name>, <year>.
   ```
3. If the upstream license is copyleft (GPL/LGPL/MPL), confirm PKMDS-Blazor's GPL-3.0 license is compatible before merging.
