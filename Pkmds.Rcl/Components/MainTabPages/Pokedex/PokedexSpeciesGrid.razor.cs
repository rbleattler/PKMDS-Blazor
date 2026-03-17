namespace Pkmds.Rcl.Components.MainTabPages.Pokedex;

public partial class PokedexSpeciesGrid
{
    // Incremented by PokedexTab after each bulk operation (Fill / Seen All / Clear).
    // Giving the grid a changing parameter ensures Blazor re-renders the child and
    // calls OnParametersSet, which rebuilds the row list to reflect the new state.
    [Parameter]
    public int RefreshToken { get; set; }

    private string _searchText = string.Empty;
    private List<PokedexGridRow> _rows = [];

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        BuildRows();
    }

    // Materializes one PokedexGridRow per species that is registered in this game's
    // Pokédex.  Uses the same per-game filter as PokedexTab.GetDexTotalCount so the
    // grid does not show species that the game never tracks (e.g. non-Galar species
    // in SWSH, non-Hisui species in LA).
    private void BuildRows()
    {
        if (AppState.SaveFile is not { HasPokeDex: true } saveFile)
        {
            _rows = [];
            return;
        }

        var speciesNames = GameInfo.Strings.Species;
        var rows = new List<PokedexGridRow>();

        for (ushort i = 1; i <= saveFile.MaxSpeciesID; i++)
        {
            if (!IsSpeciesInDex(saveFile, i))
            {
                continue;
            }

            var name = i < speciesNames.Count
                ? speciesNames[i]
                : i.ToString(CultureInfo.InvariantCulture);

            rows.Add(new PokedexGridRow(i, name, saveFile.GetSeen(i), saveFile.GetCaught(i)));
        }

        _rows = rows;
    }

    // Returns true when the species has a dex entry in the given save file.
    // Mirrors the enumeration logic used by PokedexTab.GetDexTotalCount and the
    // corresponding PKHeX WinForms SAV_Pokedex* editors.
    private static bool IsSpeciesInDex(SaveFile saveFile, ushort species) => saveFile switch
    {
        // LGPE: national dex limited to original 151 + Meltan (808) + Melmetal (809).
        SAV7b => species is >= 1 and <= 151 or 808 or 809,

        // SWSH: species must have a Galar / Armor / Crown regional dex index.
        SAV8SWSH swsh => swsh.Zukan.GetEntry(species, out _),

        // LA: only Hisui-native species are tracked.
        SAV8LA => PokedexSave8a.GetDexIndex(PokedexType8a.Hisui, species) != 0,

        // SV: covers all three regional dexes (Paldea / Kitakami / Blueberry).
        SAV9SV sv => sv.Zukan.GetDexIndex(species).Index != 0,

        // ZA: filters by the game's personal table (MaxSpeciesID varies by DLC revision).
        SAV9ZA za => za.Personal.IsSpeciesInGame(species),

        // All other games (Gen 1–7, BDSP): every national species up to MaxSpeciesID.
        _ => true
    };

    // Returns true when the row should be visible given the current search text.
    // Matches against the numeric species ID (exact) or the species name (contains,
    // case-insensitive).
    private bool FilterRow(PokedexGridRow row)
    {
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            return true;
        }

        var search = _searchText.Trim();

        if (ushort.TryParse(search, out var id))
        {
            return row.SpeciesId == id;
        }

        return row.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void OnSeenChanged(PokedexGridRow row, bool value)
    {
        if (AppState.SaveFile is not { HasPokeDex: true } saveFile)
        {
            return;
        }

        if (saveFile is SAV9SV sv && sv.Zukan.GetRevision() == 0)
        {
            // PKHeX bug: PokeDexEntry9Paldea.SetSeen(true) is a no-op when state == 0
            // because it uses Math.Min(state, 2) instead of Math.Max.  SetSeen(false)
            // calls SetState(2) ("seen") instead of 0 ("unknown").
            // Workaround: call SetState() directly.
            var entry = sv.Zukan.DexPaldea.Get(row.SpeciesId);
            if (value)
            {
                // Raise to "seen" (state 2) only if not already seen or caught.
                if (entry.GetState() < 2)
                {
                    entry.SetState(2u);
                }
            }
            else
            {
                // Clear everything — "unknown" (state 0).
                entry.SetState(0u);
            }
        }
        else
        {
            saveFile.SetSeen(row.SpeciesId, value);
        }

        // Re-read the actual stored state to keep the row in sync with the save file.
        UpdateRowFromSave(row, saveFile);
    }

    private void OnCaughtChanged(PokedexGridRow row, bool value)
    {
        if (AppState.SaveFile is not { HasPokeDex: true } saveFile)
        {
            return;
        }

        if (saveFile is SAV9SV sv && sv.Zukan.GetRevision() == 0)
        {
            // PKHeX bug: PokeDexEntry9Paldea.SetCaught(false) calls SetState(2) ("seen")
            // instead of leaving caught unset.
            // Workaround: call SetState() directly.
            var entry = sv.Zukan.DexPaldea.Get(row.SpeciesId);
            entry.SetState(value
                ? 3u  // caught
                : 2u  // seen but not caught
            );
        }
        else
        {
            saveFile.SetCaught(row.SpeciesId, value);
        }

        // Re-read the actual stored state to keep the row in sync with the save file.
        UpdateRowFromSave(row, saveFile);
    }

    private void UpdateRowFromSave(PokedexGridRow row, SaveFile saveFile)
    {
        var idx = _rows.FindIndex(r => r.SpeciesId == row.SpeciesId);
        if (idx >= 0)
        {
            _rows[idx] = row with
            {
                IsSeen = saveFile.GetSeen(row.SpeciesId),
                IsCaught = saveFile.GetCaught(row.SpeciesId)
            };
        }
    }

}
