namespace Pkmds.Rcl.Components.MainTabPages.Pokedex;

public partial class PokedexSpeciesGrid
{
    private bool hasDetailedEditor;
    private List<PokedexGridRow> rows = [];

    private string searchText = string.Empty;
    private DexStatusFilter selectedStatusFilter = DexStatusFilter.All;

    // Tracks the last seen values to avoid a full BuildRows() call on every re-render.
    private SaveFile? _lastSaveFile;
    private int _lastRefreshToken = -1;

    // Incremented by PokedexTab after each bulk operation (Fill / Seen All / Clear).
    // Giving the grid a changing parameter ensures Blazor re-renders the child and
    // calls OnParametersSet, which rebuilds the row list to reflect the new state.
    [Parameter]
    public int RefreshToken { get; set; }

    // Invoked after any per-species Seen/Caught toggle so the parent (PokedexTab)
    // can refresh its summary counts and progress bars without a full grid rebuild.
    [Parameter]
    public EventCallback OnSpeciesChanged { get; set; }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        var saveFile = AppState.SaveFile;
        // Only rebuild from the save file when the save file itself changes or when a
        // bulk operation increments RefreshToken.  Individual Seen/Caught toggles are
        // handled in-place by UpdateRowFromSave so the virtualizer's Items reference
        // changes without a full list rebuild.
        if (!ReferenceEquals(saveFile, _lastSaveFile) || RefreshToken != _lastRefreshToken)
        {
            _lastSaveFile = saveFile;
            _lastRefreshToken = RefreshToken;
            BuildRows();
        }
    }

    // Materializes one PokedexGridRow per species that is registered in this game's
    // Pokédex.  Uses the same per-game filter as PokedexTab.GetDexTotalCount so the
    // grid does not show species that the game never tracks (e.g. non-Galar species
    // in SWSH, non-Hisui species in LA).
    private void BuildRows()
    {
        if (AppState.SaveFile is not { HasPokeDex: true } saveFile)
        {
            rows = [];
            return;
        }

        var speciesNames = GameInfo.Strings.Species;
        var pokedexGridRows = new List<PokedexGridRow>();

        for (ushort i = 1; i <= saveFile.MaxSpeciesID; i++)
        {
            if (!IsSpeciesInDex(saveFile, i))
            {
                continue;
            }

            var name = i < speciesNames.Count
                ? speciesNames[i]
                : i.ToString(CultureInfo.InvariantCulture);

            pokedexGridRows.Add(new PokedexGridRow(i, name, saveFile.GetSeen(i), saveFile.GetCaught(i)));
        }

        rows = pokedexGridRows;
        hasDetailedEditor = saveFile is SAV4 or SAV5 or SAV6XY or SAV6AO or SAV7 or SAV7b
            or SAV8SWSH or SAV8LA or SAV8BS or SAV9SV or SAV9ZA;
        selectedStatusFilter = DexStatusFilter.All;
    }

    // Delegates to the shared PokedexHelpers.IsSpeciesInDex so the grid and the
    // PokedexTab header counts always filter against the same species set.
    private static bool IsSpeciesInDex(SaveFile saveFile, ushort species) =>
        PokedexHelpers.IsSpeciesInDex(saveFile, species);

    // Returns true when the row should be visible given the current search text and
    // status filter.  Name/ID matching runs first; status filter is applied after.
    private bool FilterRow(PokedexGridRow row)
    {
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var search = searchText.Trim();
            if (ushort.TryParse(search, out var id))
            {
                if (row.SpeciesId != id)
                    return false;
            }
            else if (!row.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return selectedStatusFilter switch
        {
            DexStatusFilter.Seen          => row.IsSeen,
            DexStatusFilter.Caught        => row.IsCaught,
            DexStatusFilter.Unseen        => !row.IsSeen,
            DexStatusFilter.SeenNotCaught => row.IsSeen && !row.IsCaught,
            _                             => true,
        };
    }

    private async Task OnSeenChanged(PokedexGridRow row, bool value)
    {
        if (AppState.SaveFile is not { HasPokeDex: true } saveFile)
        {
            return;
        }

        if (saveFile is SAV9SV sv)
        {
            // PKHeX bug: SaveFile.SetSeen is a virtual no-op; SAV9SV never overrides it.
            // Must write through the Zukan API directly for both dex block modes.
            if (sv.Zukan.GetRevision() == 0)
            {
                // Paldea block (pre-DLC saves).
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
                // Kitakami block (post-2.0.1 saves with DLC; stores all species).
                var entry = sv.Zukan.DexKitakami.Get(row.SpeciesId);
                if (value)
                {
                    entry.SetSeenForm(0, true);
                }
                else
                {
                    entry.ClearSeen(0);
                }
            }
        }
        else
        {
            saveFile.SetSeen(row.SpeciesId, value);
        }

        // Re-read the actual stored state to keep the row in sync with the save file.
        UpdateRowFromSave(row, saveFile);

        // Notify the parent so it can refresh its summary counts and progress bars.
        await OnSpeciesChanged.InvokeAsync();
    }

    private async Task OnCaughtChanged(PokedexGridRow row, bool value)
    {
        if (AppState.SaveFile is not { HasPokeDex: true } saveFile)
        {
            return;
        }

        if (saveFile is SAV9SV sv)
        {
            // PKHeX bug: SaveFile.SetCaught is a virtual no-op; SAV9SV never overrides it.
            // Must write through the Zukan API directly for both dex block modes.
            if (sv.Zukan.GetRevision() == 0)
            {
                // Paldea block (pre-DLC saves).
                // PKHeX bug: PokeDexEntry9Paldea.SetCaught(false) calls SetState(2) ("seen")
                // instead of leaving caught unset.
                // Workaround: call SetState() directly.
                var entry = sv.Zukan.DexPaldea.Get(row.SpeciesId);
                entry.SetState(value
                        ? 3u // caught
                        : 2u // seen but not caught
                );
            }
            else
            {
                // Kitakami block (post-2.0.1 saves with DLC; stores all species).
                var entry = sv.Zukan.DexKitakami.Get(row.SpeciesId);
                if (value)
                {
                    // A caught species must also be seen.
                    entry.SetSeenForm(0, true);
                    entry.SetObtainedForm(0, true);
                }
                else
                {
                    // Clear caught but keep seen.
                    entry.SetObtainedForm(0, false);
                }
            }
        }
        else
        {
            saveFile.SetCaught(row.SpeciesId, value);
        }

        // Re-read the actual stored state to keep the row in sync with the save file.
        UpdateRowFromSave(row, saveFile);

        // Notify the parent so it can refresh its summary counts and progress bars.
        await OnSpeciesChanged.InvokeAsync();
    }

    private void UpdateRowFromSave(PokedexGridRow row, SaveFile saveFile)
    {
        var idx = rows.FindIndex(r => r.SpeciesId == row.SpeciesId);
        if (idx < 0)
        {
            return;
        }

        // Build a new list so MudDataGrid's virtualizer detects the Items reference
        // change and re-renders visible rows with the updated Seen/Caught state.
        var newRows = new List<PokedexGridRow>(rows);
        newRows[idx] = row with
        {
            IsSeen = saveFile.GetSeen(row.SpeciesId),
            IsCaught = saveFile.GetCaught(row.SpeciesId),
        };
        rows = newRows;
    }

    private async Task OpenDetails(PokedexGridRow row)
    {
        var result = await DialogService.ShowAsync<PokedexSpeciesDialog>(
            string.Empty,
            new DialogParameters<PokedexSpeciesDialog> { { x => x.SpeciesId, row.SpeciesId } },
            new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true });

        await result.Result;

        // Refresh the row so Seen/Caught columns reflect any changes made in the dialog.
        if (AppState.SaveFile is { HasPokeDex: true } saveFile)
        {
            UpdateRowFromSave(row, saveFile);
        }
    }
}
