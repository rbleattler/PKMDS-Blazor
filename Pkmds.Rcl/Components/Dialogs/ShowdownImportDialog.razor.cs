namespace Pkmds.Rcl.Components.Dialogs;

public partial class ShowdownImportDialog
{
    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    [Inject]
    private HttpClient Http { get; set; } = null!;

    private string inputText = string.Empty;
    private bool isFetching;
    private string? fetchError;
    private string? pasteInfo;
    private List<ShowdownSet> parsedSets = [];
    private bool importToParty;

    private bool IsUrl => PokepasteTeam.IsURL(inputText, out _);

    private void ParseText()
    {
        parsedSets = [.. AppService.ParseShowdownText(inputText)];
    }

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

        return parts.Count > 0 ? string.Join(" | ", parts) : null;
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
        var localization = BattleTemplateParseErrorLocalization.Get(GameLanguage.DefaultLanguage);
        return string.Join("\n", set.InvalidLines.Select(e => e.Humanize(localization)));
    }

    private async Task ImportAsync()
    {
        if (AppState.SaveFile is null)
        {
            Snackbar.Add("No save file loaded.", Severity.Warning);
            return;
        }

        var imported = 0;
        var failed = 0;

        foreach (var set in parsedSets)
        {
            var pkm = AppService.ConvertShowdownSetToPkm(set);
            if (pkm is null)
            {
                failed++;
                continue;
            }

            var placed = importToParty
                ? AppService.TryPlacePokemonInPartySlot(pkm)
                : AppService.TryPlacePokemonInFirstAvailableSlot(pkm);

            if (placed)
            {
                imported++;
            }
            else
            {
                failed++;
            }
        }

        if (imported > 0)
        {
            // Placement methods already fire individual refresh events;
            // fire both here to cover all views regardless of target.
            RefreshService.RefreshBoxState();
            RefreshService.RefreshPartyState();
        }

        var severity = failed == 0 ? Severity.Success : Severity.Warning;
        var message = failed == 0
            ? $"Imported {imported} Pokémon."
            : $"Imported {imported} Pokémon. {failed} could not be placed (no empty slots available).";

        Snackbar.Add(message, severity);
        MudDialog?.Close();
    }

    private void Cancel() => MudDialog?.Close(DialogResult.Cancel());
}
