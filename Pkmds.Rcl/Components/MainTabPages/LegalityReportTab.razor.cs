using PKHexSeverity = PKHeX.Core.Severity;

namespace Pkmds.Rcl.Components.MainTabPages;

public partial class LegalityReportTab : RefreshAwareComponent
{
    private bool hasRun;
    private bool isScanning;
    private bool isLegalizing;
    private double legalizationPercent;
    private string legalizationStatusText = string.Empty;
    private CancellationTokenSource? legalizeCts;
    private List<LegalityReportEntry> legalityReportEntries = [];
    private LegalityStatus? statusFilter;

    /// <summary>
    /// Callback invoked after a row is clicked to jump to the Party / Box tab.
    /// </summary>
    [Parameter]
    public EventCallback OnJumpToPartyBox { get; set; }

    private int LegalCount => legalityReportEntries.Count(e => e.Status == LegalityStatus.Legal);
    private int FishyCount => legalityReportEntries.Count(e => e.Status == LegalityStatus.Fishy);
    private int IllegalCount => legalityReportEntries.Count(e => e.Status == LegalityStatus.Illegal);

    private bool HasIllegalOrFishy => hasRun && legalityReportEntries.Any(e =>
        e.Status is LegalityStatus.Illegal or LegalityStatus.Fishy);

    private async Task LegalizeAllAsync()
    {
        if (AppState.SaveFile is not { } saveFile)
        {
            return;
        }

        var targets = legalityReportEntries
            .Where(e => e.Status is LegalityStatus.Illegal or LegalityStatus.Fishy)
            .ToList();

        if (targets.Count == 0)
        {
            return;
        }

        legalizeCts?.Dispose();
        legalizeCts = new CancellationTokenSource();
        var ct = legalizeCts.Token;

        isLegalizing = true;
        legalizationPercent = 0;
        legalizationStatusText = $"Legalizing 0/{targets.Count}…";
        StateHasChanged();

        await Task.Yield();

        var successCount = 0;
        var failureCount = 0;
        var cancelled = false;
        var processed = 0;

        for (var i = 0; i < targets.Count; i++)
        {
            if (ct.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            var entry = targets[i];
            legalizationStatusText = $"Legalizing {i + 1}/{targets.Count}: {entry.SpeciesName} ({entry.Location})";
            legalizationPercent = (double)i / targets.Count * 100;
            StateHasChanged();
            // Task.Delay(1) rather than Task.Yield(): in Blazor WASM, Yield stays on the
            // same JS macrotask and the browser never paints. Delay(1) uses setTimeout and
            // actually releases the main thread so the UI can render and process clicks
            // (including the Cancel button).
            try
            {
                await Task.Delay(1, ct);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                break;
            }

            LegalizationOutcome result;
            try
            {
                result = await LegalizationService.LegalizeAsync(entry.Pokemon, saveFile, progress: null, ct);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                break;
            }
            catch
            {
                failureCount++;
                processed++;
                continue;
            }

            processed++;

            if (result.Status != LegalizationStatus.Success)
            {
                failureCount++;
                continue;
            }

            // Pass EntityImportSettings.None to bypass SaveFile.UpdatePKM's "adapt as if
            // traded in" path. The PKM is already legal for the current save; letting
            // SetPKM mutate handler/memory/trainer fields can reintroduce illegalities
            // that our pre-write analysis doesn't see.
            if (entry.IsParty)
            {
                saveFile.SetPartySlotAtIndex(result.Pokemon, entry.SlotNumber, EntityImportSettings.None);
            }
            else
            {
                saveFile.SetBoxSlotAtIndex(result.Pokemon, entry.BoxNumber, entry.SlotNumber, EntityImportSettings.None);
            }

            // Update the report entry in place so the table reflects the newly legal
            // status immediately, and the state is preserved across cancellation without
            // needing a full re-scan.
            var freshLa = AppService.GetLegalityAnalysis(result.Pokemon);
            var newStatus = GetStatus(freshLa);
            var updated = entry with
            {
                Pokemon = result.Pokemon,
                Status = newStatus,
                FirstIssue = newStatus == LegalityStatus.Legal ? string.Empty : GetFirstIssue(freshLa),
            };
            var idx = legalityReportEntries.IndexOf(entry);
            if (idx >= 0)
            {
                legalityReportEntries[idx] = updated;
            }

            successCount++;
        }

        legalizationPercent = 100;
        StateHasChanged();

        if (cancelled)
        {
            Snackbar.Add(
                $"Legalization cancelled. Legalized {successCount}/{targets.Count} Pokémon before cancelling ({processed} processed).",
                Severity.Info);
        }
        else
        {
            Snackbar.Add(
                failureCount == 0
                    ? $"Legalized {successCount}/{targets.Count} Pokémon."
                    : $"Legalized {successCount}/{targets.Count} Pokémon. {failureCount} could not be fixed.",
                failureCount == 0 ? Severity.Success : Severity.Warning);
        }

        isLegalizing = false;
        legalizationStatusText = string.Empty;
        legalizationPercent = 0;
        legalizeCts.Dispose();
        legalizeCts = null;

        RefreshService.Refresh();
    }

    private void CancelLegalizeAll() => legalizeCts?.Cancel();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            legalizeCts?.Cancel();
            legalizeCts?.Dispose();
            legalizeCts = null;
        }

        base.Dispose(disposing);
    }

    private async Task RunScanAsync()
    {
        if (AppState.SaveFile is not { } saveFile)
        {
            return;
        }

        isScanning = true;
        hasRun = false;
        legalityReportEntries = [];
        statusFilter = null;
        StateHasChanged();

        // Yield once so the spinner renders before the CPU-bound sweep begins.
        await Task.Yield();

        var entries = new List<LegalityReportEntry>();

        // --- Party slots ---
        for (var i = 0; i < saveFile.PartyCount; i++)
        {
            var pkm = saveFile.GetPartySlotAtIndex(i);
            if (pkm is not { Species: > 0 })
            {
                continue;
            }

            var la = AppService.GetLegalityAnalysis(pkm);
            entries.Add(BuildEntry(pkm, la, true, 0, i));
        }

        // --- Box slots (yield after every box to keep the UI responsive) ---
        for (var box = 0; box < saveFile.BoxCount; box++)
        {
            for (var slot = 0; slot < saveFile.BoxSlotCount; slot++)
            {
                var pkm = saveFile.GetBoxSlotAtIndex(box, slot);
                if (pkm is not { Species: > 0 })
                {
                    continue;
                }

                var la = AppService.GetLegalityAnalysis(pkm);
                entries.Add(BuildEntry(pkm, la, false, box, slot));
            }

            // Yield after every box so the progress spinner stays animated.
            await Task.Yield();
        }

        legalityReportEntries = entries;
        isScanning = false;
        hasRun = true;
        StateHasChanged();
    }

    private LegalityReportEntry BuildEntry(PKM pkm, LegalityAnalysis la, bool isParty, int box, int slot)
    {
        var speciesName = AppService.GetPokemonSpeciesName(pkm.Species) ??
                          pkm.Species.ToString(CultureInfo.InvariantCulture);
        var location = isParty
            ? $"Party {slot + 1}"
            : $"Box {box + 1}, Slot {slot + 1}";

        return new LegalityReportEntry
        {
            Pokemon = pkm,
            SpeciesName = speciesName,
            Location = location,
            Status = GetStatus(la),
            FirstIssue = GetFirstIssue(la),
            IsParty = isParty,
            BoxNumber = box,
            SlotNumber = slot
        };
    }

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

    private static string GetFirstIssue(LegalityAnalysis la)
    {
        var ctx = LegalityLocalizationContext.Create(la);

        foreach (var result in la.Results)
        {
            if (!result.Valid)
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

        return string.Empty;
    }

    private void SetFilter(LegalityStatus? filter) => statusFilter = filter;

    private async Task OnRowClickAsync(TableRowClickEventArgs<LegalityReportEntry> args)
    {
        if (args.Item is not { } entry)
        {
            return;
        }

        if (entry.IsParty)
        {
            AppService.SetSelectedPartyPokemon(entry.Pokemon, entry.SlotNumber);
        }
        else if (AppState.SaveFile is SAV7b lgsave)
        {
            // Let's Go renders all boxes as a single flat scrollable list.
            // Convert box+slot to the flat index (0..999) that SetSelectedLetsGoPokemon expects.
            var flatSlot = entry.BoxNumber * lgsave.BoxSlotCount + entry.SlotNumber;
            AppService.SetSelectedLetsGoPokemon(entry.Pokemon, flatSlot);
        }
        else
        {
            AppService.SetSelectedBoxPokemon(entry.Pokemon, entry.BoxNumber, entry.SlotNumber);
        }

        await OnJumpToPartyBox.InvokeAsync();
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

    private static string GetStatusLabel(LegalityStatus status) => status switch
    {
        LegalityStatus.Legal => "Legal",
        LegalityStatus.Fishy => "Fishy",
        LegalityStatus.Illegal => "Illegal",
        _ => "Unknown"
    };

    private bool TableFilterFunction(LegalityReportEntry legalityReportEntry) =>
        statusFilter is null || legalityReportEntry.Status == statusFilter;
}
