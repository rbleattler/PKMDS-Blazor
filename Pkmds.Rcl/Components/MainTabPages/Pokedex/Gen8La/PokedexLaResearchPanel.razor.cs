namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen8La;

public partial class PokedexLaResearchPanel : BasePkmdsComponent
{
    private ushort selectedSpecies;
    private DexFilter filter = DexFilter.All;

    // Hisui-ordered (dex index, national species ID) list — built once per save load.
    private List<(ushort HisuiIdx, ushort Species)> allHisuiSpecies = [];

    // Task description strings loaded from PKHeX embedded resources.
    private string[] taskDescriptions = [];
    private string[] timeTaskDescriptions = [];
    private string[] speciesQuests = [];

    private static readonly (PokedexType8a Type, string Label)[] LocalDexTypes =
    [
        (PokedexType8a.Hisui,  "Hisui"),
        (PokedexType8a.Local1, "Fieldlands"),
        (PokedexType8a.Local2, "Mirelands"),
        (PokedexType8a.Local3, "Coastlands"),
        (PokedexType8a.Local4, "Highlands"),
        (PokedexType8a.Local5, "Icelands"),
    ];

    /// <summary>
    /// Incremented by PokedexTab after each bulk operation so the panel
    /// rebuilds its species list on the next render.
    /// </summary>
    [Parameter]
    public int RefreshToken { get; set; }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (AppState.SaveFile is not SAV8LA sav8La)
        {
            return;
        }

        taskDescriptions = Util.GetStringList("tasks8a", "en");
        timeTaskDescriptions = Util.GetStringList("time_tasks8a", "en");
        speciesQuests = Util.GetStringList("species_tasks8a", "en");

        // Build Hisui-ordered list once per save load / token change.
        allHisuiSpecies = [];
        for (ushort s = 1; s <= sav8La.MaxSpeciesID; s++)
        {
            var hisuiIdx = PokedexSave8a.GetDexIndex(PokedexType8a.Hisui, s);
            if (hisuiIdx == 0)
            {
                continue;
            }

            allHisuiSpecies.Add((hisuiIdx, s));
        }

        allHisuiSpecies.Sort(static (a, b) => a.HisuiIdx.CompareTo(b.HisuiIdx));

        // Select first species if none is selected or the selection is now invalid.
        if (selectedSpecies != 0 && PokedexSave8a.GetDexIndex(PokedexType8a.Hisui, selectedSpecies) != 0)
        {
            return;
        }

        if (allHisuiSpecies.Count > 0)
        {
            selectedSpecies = allHisuiSpecies[0].Species;
        }
    }

    // Returns the species list filtered by the current DexFilter.
    // Evaluated per-render (lightweight; list length ~242).
    private IEnumerable<(ushort HisuiIdx, ushort Species)> GetFilteredSpeciesList(PokedexSave8a dex) =>
        filter switch
        {
            DexFilter.Complete => allHisuiSpecies.Where(x => dex.IsComplete(x.Species)),
            DexFilter.Incomplete => allHisuiSpecies.Where(x => !dex.IsComplete(x.Species)),
            _ => allHisuiSpecies,
        };

    private void OnSpeciesSelected(ushort species)
    {
        selectedSpecies = species;
        StateHasChanged();
    }

    private void SetFilter(DexFilter newFilter, PokedexSave8a dex)
    {
        filter = newFilter;

        // If the currently selected species is no longer in the filtered list, pick first.
        var filtered = GetFilteredSpeciesList(dex).ToList();
        if (filtered.All(x => x.Species != selectedSpecies) && filtered.Count > 0)
        {
            selectedSpecies = filtered[0].Species;
        }

        StateHasChanged();
    }

    private void OnTaskValueChanged(PokedexSave8a dex, ushort species, PokedexResearchTask8a task, int value)
    {
        dex.SetResearchTaskProgressByForce(species, task, value);
        dex.UpdateSpecificReportPoke(species);
        StateHasChanged();
    }

    private void CompleteThisSpecies(PokedexSave8a dex, ushort species)
    {
        var dexIndex = PokedexSave8a.GetDexIndex(PokedexType8a.Hisui, species);
        if (dexIndex == 0)
        {
            return;
        }

        foreach (var task in PokedexConstants8a.ResearchTasks[dexIndex - 1])
        {
            if (task.TaskThresholds.Length == 0)
            {
                continue;
            }

            dex.SetResearchTaskProgressByForce(species, task, task.TaskThresholds[^1]);
        }

        dex.UpdateSpecificReportPoke(species);
        StateHasChanged();
    }

    private async Task OpenResearchEditorDialog(ushort species)
    {
        var result = await DialogService.ShowAsync<PokedexLaResearchEditorDialog>(
            string.Empty,
            new DialogParameters<PokedexLaResearchEditorDialog> { { x => x.SpeciesId, species } },
            new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true });

        await result.Result;
        StateHasChanged();
    }

    private async Task OpenFlagsDialog(ushort species)
    {
        var result = await DialogService.ShowAsync<PokedexSpeciesDialog>(
            string.Empty,
            new DialogParameters<PokedexSpeciesDialog> { { x => x.SpeciesId, species } },
            new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true });

        await result.Result;
        StateHasChanged();
    }

    private enum DexFilter
    {
        All,
        Incomplete,
        Complete,
    }
}
