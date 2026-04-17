using PKHexSeverity = PKHeX.Core.Severity;

namespace Pkmds.Rcl.Components.Dialogs;

public partial class ShowdownImportDialog
{
    private string? fetchError;
    private bool importToParty;

    private string inputText = string.Empty;
    private bool isFetching;
    private bool isParsing;
    private List<ParsedEntry> parsedEntries = [];
    private string? pasteInfo;

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    [Inject]
    private HttpClient Http { get; set; } = null!;

    private bool IsUrl => PokepasteTeam.IsURL(inputText, out _);

    private async Task ParseTextAsync()
    {
        if (string.IsNullOrWhiteSpace(inputText))
        {
            return;
        }

        isParsing = true;
        parsedEntries = [];
        StateHasChanged();

        // Yield so the spinner paints before per-set conversion + legality analysis runs.
        // Task.Delay(1) (not Task.Yield) because in Blazor WASM, Yield stays on the same
        // JS macrotask and never lets the browser paint.
        await Task.Delay(1);

        var sets = AppService.ParseShowdownText(inputText);
        var entries = new List<ParsedEntry>(sets.Count);
        foreach (var set in sets)
        {
            var pkm = AppService.ConvertShowdownSetToPkm(set);
            var (status, firstIssue) = AnalyzeLegality(pkm);
            entries.Add(new ParsedEntry(set, pkm, status, firstIssue));

            // Inter-entry yield so the browser can process input between conversions,
            // matching the pattern used by LegalityReportTab's batch legalize sweep.
            await Task.Delay(1);
        }

        parsedEntries = entries;
        isParsing = false;
        StateHasChanged();
    }

    private (LegalityStatus? Status, string FirstIssue) AnalyzeLegality(PKM? pkm)
    {
        if (pkm is null)
        {
            return (null, string.Empty);
        }

        var la = AppService.GetLegalityAnalysis(pkm);
        var status = GetStatus(la);
        var firstIssue = status == LegalityStatus.Legal
            ? string.Empty
            : GetFirstIssue(la);
        return (status, firstIssue);
    }

    // Mirrors LegalityReportTab.GetStatus — keep these in sync.
    private static LegalityStatus GetStatus(LegalityAnalysis la)
    {
        var hasInvalid = la.Results.Any(r => r.Judgement == PKHexSeverity.Invalid)
                         || !MoveResult.AllValid(la.Info.Moves)
                         || !MoveResult.AllValid(la.Info.Relearn);

        if (hasInvalid)
        {
            return LegalityStatus.Illegal;
        }

        var hasFishy = la.Results.Any(r => r.Judgement == PKHexSeverity.Fishy);
        return hasFishy
            ? LegalityStatus.Fishy
            : LegalityStatus.Legal;
    }

    // Mirrors LegalityReportTab.GetFirstIssue — keep these in sync.
    private static string GetFirstIssue(LegalityAnalysis la)
    {
        var ctx = LegalityLocalizationContext.Create(la);

        // Prefer Invalid over Fishy so the more severe issue wins when both are present.
        // CheckResult.Valid is true for Fishy judgements, so match on Judgement directly.
        foreach (var result in la.Results)
        {
            if (result.Judgement == PKHexSeverity.Invalid)
            {
                return ctx.Humanize(in result);
            }
        }

        if (!MoveResult.AllValid(la.Info.Moves))
        {
            return "Invalid move detected.";
        }

        if (!MoveResult.AllValid(la.Info.Relearn))
        {
            return "Invalid relearn move detected.";
        }

        foreach (var result in la.Results)
        {
            if (result.Judgement == PKHexSeverity.Fishy)
            {
                return ctx.Humanize(in result);
            }
        }

        return string.Empty;
    }

    private static Color GetStatusColor(LegalityStatus status) => status switch
    {
        LegalityStatus.Legal => Color.Success,
        LegalityStatus.Fishy => Color.Warning,
        LegalityStatus.Illegal => Color.Error,
        _ => Color.Default
    };

    private static string GetStatusIcon(LegalityStatus status) => status switch
    {
        LegalityStatus.Legal => Icons.Material.Filled.CheckCircle,
        LegalityStatus.Fishy => Icons.Material.Filled.Warning,
        LegalityStatus.Illegal => Icons.Material.Filled.Cancel,
        _ => Icons.Material.Filled.Help
    };

    private static string GetStatusTooltip(ParsedEntry entry) => entry.Status switch
    {
        LegalityStatus.Legal => "Legal",
        _ => string.IsNullOrEmpty(entry.FirstIssue)
            ? GetStatusLabel(entry.Status)
            : entry.FirstIssue
    };

    private static string GetStatusLabel(LegalityStatus? status) => status switch
    {
        LegalityStatus.Legal => "Legal",
        LegalityStatus.Fishy => "Fishy",
        LegalityStatus.Illegal => "Illegal",
        _ => "Unknown"
    };

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
                isFetching = false;
                await ParseTextAsync();
                return;
            }
        }
        catch (Exception ex)
        {
            fetchError = $"Failed to fetch from PokePaste: {ex.Message}";
        }

        isFetching = false;
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

        // Reuse the pre-converted PKMs from parsing — no need to re-run the legalization
        // engine at import time.
        var converted = new List<PKM>(parsedEntries.Count);
        var conversionFailed = 0;
        foreach (var entry in parsedEntries)
        {
            if (entry.Pokemon is { } pkm)
            {
                converted.Add(pkm);
            }
            else
            {
                conversionFailed++;
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

    private sealed record ParsedEntry(
        ShowdownSet Set,
        PKM? Pokemon,
        LegalityStatus? Status,
        string FirstIssue);
}
