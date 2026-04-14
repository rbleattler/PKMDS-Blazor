namespace Pkmds.Rcl.Components.Dialogs;

public partial class ShowdownImportDialog
{
    private string? fetchError;
    private bool importToParty;

    private string inputText = string.Empty;
    private bool isFetching;
    private List<ShowdownSet> parsedSets = [];
    private string? pasteInfo;

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    [Inject]
    private HttpClient Http { get; set; } = null!;

    private bool IsUrl => PokepasteTeam.IsURL(inputText, out _);

    private void ParseText() => parsedSets = [.. AppService.ParseShowdownText(inputText)];

    private async Task FetchUrlAsync()
    {
        if (!PokepasteTeam.IsURL(inputText, out var rawUrl))
        {
            return;
        }

        isFetching = true;
        fetchError = null;
        pasteInfo = null;

        // Convert the /raw URL (returned by PokepasteTeam.IsURL) to the /json endpoint,
        // which also sets Access-Control-Allow-Origin: * and provides title/author/notes.
        var jsonUrl = rawUrl.EndsWith("/raw", StringComparison.OrdinalIgnoreCase)
            ? string.Concat(rawUrl.AsSpan(0, rawUrl.Length - 4), "/json")
            : rawUrl + "/json";

        try
        {
            var json = await Http.GetStringAsync(jsonUrl);
            var response = JsonSerializer.Deserialize<PokePasteResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (response is null || string.IsNullOrWhiteSpace(response.Paste))
            {
                fetchError = "Received an empty paste from PokePaste.";
            }
            else
            {
                inputText = response.Paste;
                pasteInfo = BuildPasteInfo(response);
                ParseText();
            }
        }
        catch (Exception ex)
        {
            fetchError = $"Failed to fetch from PokePaste: {ex.Message}";
        }
        finally
        {
            isFetching = false;
        }
    }

    private static string? BuildPasteInfo(PokePasteResponse response)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(response.Title))
        {
            parts.Add($"Title: {response.Title}");
        }

        if (!string.IsNullOrWhiteSpace(response.Author))
        {
            parts.Add($"Author: {response.Author}");
        }

        return parts.Count > 0
            ? string.Join(" | ", parts)
            : null;
    }

    private string GetSetSummary(ShowdownSet set)
    {
        var strings = GameInfo.Strings;
        var speciesName = strings.Species.Count > set.Species
            ? strings.Species[set.Species]
            : set.Species.ToString();

        var parts = new List<string>(3);

        if (!string.IsNullOrEmpty(set.Nickname) && !set.Nickname.Equals(speciesName, StringComparison.Ordinal))
        {
            parts.Add($"{set.Nickname} ({speciesName})");
        }
        else
        {
            parts.Add(speciesName);
        }

        parts.Add($"Lv.{set.Level}");

        if (set.HeldItem > 0 && set.HeldItem < strings.Item.Count)
        {
            parts.Add($"@ {strings.Item[set.HeldItem]}");
        }

        return string.Join(" ", parts);
    }

    private static string GetWarnings(ShowdownSet set)
    {
        var localization = BattleTemplateParseErrorLocalization.Get();
        return string.Join("\n", set.InvalidLines.Select(e => e.Humanize(localization)));
    }

    private async Task ImportAsync()
    {
        if (AppState.SaveFile is not { } sav)
        {
            Snackbar.Add("No save file loaded.", Severity.Warning);
            return;
        }

        // Convert all sets up front so we know exactly what we have.
        var converted = new List<PKM>(parsedSets.Count);
        var conversionFailed = 0;
        foreach (var set in parsedSets)
        {
            var pkm = AppService.ConvertShowdownSetToPkm(set);
            if (pkm is null)
            {
                conversionFailed++;
            }
            else
            {
                converted.Add(pkm);
            }
        }

        if (importToParty)
        {
            await ImportToPartyAsync(sav, converted, conversionFailed);
        }
        else
        {
            ImportToBox(converted, conversionFailed);
        }
    }

    private async Task ImportToPartyAsync(SaveFile sav, List<PKM> converted, int conversionFailed)
    {
        var emptySlots = 6 - sav.PartyCount;

        if (converted.Count <= emptySlots)
        {
            // Enough empty slots — just fill them.
            var placed = 0;
            foreach (var pkm in converted)
            {
                if (AppService.TryPlacePokemonInPartySlot(pkm))
                {
                    placed++;
                }
            }

            ReportResult(placed, conversionFailed + (converted.Count - placed));
            MudDialog?.Close();
            return;
        }

        // Party doesn't have enough room — ask to overwrite.
        var names = string.Join(", ", converted
            .Take(6)
            .Select(p => AppService.GetPokemonSpeciesName(p.Species) ?? p.Species.ToString()));

        var confirm = await DialogService.ShowMessageBoxAsync(
            "Overwrite Party?",
            $"The party doesn't have enough empty slots for all {converted.Count} Pokémon. " +
            $"Replace the entire party with: {names}?",
            yesText: "Overwrite",
            cancelText: "Cancel");

        if (confirm != true)
        {
            return;
        }

        var written = AppService.OverwriteParty(converted);
        var skipped = Math.Max(0, converted.Count - written);
        ReportResult(written, conversionFailed + skipped);
        MudDialog?.Close();
    }

    private void ImportToBox(List<PKM> converted, int conversionFailed)
    {
        var placed = 0;
        foreach (var pkm in converted)
        {
            if (AppService.TryPlacePokemonInFirstAvailableSlot(pkm))
            {
                placed++;
            }
        }

        RefreshService.RefreshBoxState();
        ReportResult(placed, conversionFailed + (converted.Count - placed));
        MudDialog?.Close();
    }

    private void ReportResult(int imported, int failed)
    {
        if (imported == 0 && failed == 0)
        {
            return;
        }

        var severity = failed == 0
            ? Severity.Success
            : Severity.Warning;
        var message = failed == 0
            ? $"Imported {imported} Pokémon."
            : $"Imported {imported} Pokémon. {failed} could not be placed.";

        Snackbar.Add(message, severity);
    }

    private void Cancel() => MudDialog?.Close(DialogResult.Cancel());
}
