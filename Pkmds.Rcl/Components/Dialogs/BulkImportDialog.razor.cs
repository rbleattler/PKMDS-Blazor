namespace Pkmds.Rcl.Components.Dialogs;

public partial class BulkImportDialog : IDisposable
{
    private static readonly string AcceptList = BuildAcceptList();

    private CancellationTokenSource? cts;
    private bool isImporting;
    private bool overwriteExisting;
    private double progressPercent;
    private BulkImportResult? result;
    private IReadOnlyList<IBrowserFile>? selectedFiles;
    private bool sendOverflowToBank = true;
    private string statusText = string.Empty;

    /// <summary>
    /// Files supplied up-front (e.g. by a multi-file drop on the box grid). When set,
    /// the file picker is hidden and these files are imported directly.
    /// </summary>
    [Parameter]
    public IReadOnlyList<(string FileName, byte[] Data)>? PreloadedFiles { get; set; }

    /// <summary>
    /// When <see langword="true" /> (the default), the placement scan fills boxes before
    /// the party. Set to <see langword="false" /> when the import was initiated from a
    /// party slot drop so the party fills first.
    /// </summary>
    [Parameter]
    public bool FillBoxesFirst { get; set; } = true;

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    [Inject]
    private IBankService BankService { get; set; } = null!;

    private bool HasPreloadedFiles => PreloadedFiles is { Count: > 0 };
    private bool HasAnyFiles => HasPreloadedFiles || selectedFiles is { Count: > 0 };

    public void Dispose()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private static string BuildAcceptList() =>
        string.Join(",", EntityFileExtension.GetExtensionsAll().Select(e => "." + e));

    private async Task ImportAsync()
    {
        if (AppState.SaveFile is not { } sav)
        {
            Snackbar.Add("No save file loaded.", Severity.Warning);
            return;
        }

        var sources = await LoadSourcesAsync();
        if (sources.Count == 0)
        {
            Snackbar.Add("No files to import.", Severity.Warning);
            return;
        }

        cts?.Dispose();
        cts = new CancellationTokenSource();
        var ct = cts.Token;

        isImporting = true;
        progressPercent = 0;
        statusText = $"Parsing {sources.Count} file(s)…";
        StateHasChanged();
        await Task.Yield();

        var parseSkipped = 0;
        var parsed = new List<PKM>(sources.Count);

        try
        {
            for (var i = 0; i < sources.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var (fileName, data) = sources[i];
                progressPercent = (double)i / sources.Count * 50;
                statusText = $"Parsing {i + 1}/{sources.Count}: {fileName}";

                var ext = Path.GetExtension(fileName);
                if (!FileUtil.TryGetPKM(data, out var pkm, ext, sav))
                {
                    parseSkipped++;
                    continue;
                }

                var converted = ConvertForSave(pkm, sav, AppState.IsHaXEnabled);
                if (converted is null)
                {
                    parseSkipped++;
                    continue;
                }

                sav.AdaptToSaveFile(converted);
                parsed.Add(converted);

                if ((i + 1) % 8 == 0)
                {
                    StateHasChanged();
                    await Task.Delay(1, ct);
                }
            }

            // Placement phase.
            statusText = $"Placing {parsed.Count} Pokémon…";
            progressPercent = 50;
            StateHasChanged();
            await Task.Yield();

            var (placed, overflow) = PlacePokemon(sav, parsed, overwriteExisting, FillBoxesFirst);
            var illegal = 0;
            foreach (var pk in placed)
            {
                var la = AppService.GetLegalityAnalysis(pk);
                if (!la.Valid)
                {
                    illegal++;
                }
            }

            var overflowToBank = 0;
            if (overflow.Count > 0 && sendOverflowToBank)
            {
                statusText = $"Sending {overflow.Count} Pokémon to Bank…";
                progressPercent = 85;
                StateHasChanged();

                var tid = sav.DisplayTID.ToString(AppService.GetIdFormatString());
                var gameName = SaveFileNameDisplay.FriendlyGameName(sav.Version);
                var sourceSave = $"{sav.OT} ({tid}, {gameName})";
                await BankService.AddRangeAsync(overflow, sourceSave: sourceSave);
                overflowToBank = overflow.Count;
            }

            progressPercent = 100;
            statusText = "Done.";

            result = new BulkImportResult(
                Imported: placed.Count,
                Skipped: parseSkipped,
                Illegal: illegal,
                OverflowRemaining: overflow.Count - overflowToBank,
                OverflowToBank: overflowToBank);

            RefreshService.RefreshBoxAndPartyState();
        }
        catch (OperationCanceledException)
        {
            result = new BulkImportResult(
                Imported: 0, Skipped: 0, Illegal: 0, OverflowRemaining: 0, OverflowToBank: 0)
            { Cancelled = true };
            RefreshService.RefreshBoxAndPartyState();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Import failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            isImporting = false;
            cts?.Dispose();
            cts = null;
            StateHasChanged();
        }
    }

    private async Task<List<(string FileName, byte[] Data)>> LoadSourcesAsync()
    {
        if (HasPreloadedFiles)
        {
            return PreloadedFiles!.ToList();
        }

        if (selectedFiles is null)
        {
            return [];
        }

        var list = new List<(string, byte[])>(selectedFiles.Count);
        foreach (var file in selectedFiles)
        {
            try
            {
                await using var stream = file.OpenReadStream(Constants.MaxFileSize);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                list.Add((file.Name, ms.ToArray()));
            }
            catch
            {
                // Treat unreadable files as skipped; let the parse phase report them.
                list.Add((file.Name, []));
            }
        }

        return list;
    }

    private static PKM? ConvertForSave(PKM pkm, SaveFile sav, bool isHaXEnabled)
    {
        var clone = pkm.Clone();
        if (clone.GetType() == sav.PKMType)
        {
            return clone;
        }

        var converted = EntityConverter.ConvertToType(pkm, sav.PKMType, out var result);

        if ((!result.IsSuccess || converted is null) && isHaXEnabled)
        {
            var previous = EntityConverter.AllowIncompatibleConversion;
            EntityConverter.AllowIncompatibleConversion = EntityCompatibilitySetting.AllowIncompatibleAll;
            try
            {
                converted = EntityConverter.ConvertToType(pkm, sav.PKMType, out result);
            }
            finally
            {
                EntityConverter.AllowIncompatibleConversion = previous;
            }
        }

        return result.IsSuccess ? converted : null;
    }

    private static (List<PKM> Placed, List<PKM> Overflow) PlacePokemon(
        SaveFile sav, IReadOnlyList<PKM> candidates, bool overwrite, bool fillBoxesFirst)
    {
        var placed = new List<PKM>(candidates.Count);
        var overflow = new List<PKM>();

        foreach (var pkm in candidates)
        {
            if (TryPlaceSequential(sav, pkm, overwrite, fillBoxesFirst))
            {
                placed.Add(pkm);
            }
            else
            {
                overflow.Add(pkm);
            }
        }

        return (placed, overflow);
    }

    private static bool TryPlaceSequential(SaveFile sav, PKM pkm, bool overwrite, bool fillBoxesFirst)
    {
        // Party is never overwritten — `sav.PartyCount < 6` means there's a grow slot,
        // which matches IAppService.TryPlacePokemonInFirstAvailableSlot. Overwrite only
        // applies to boxes.
        if (fillBoxesFirst)
        {
            return TryFillBoxes(sav, pkm, overwrite) || TryFillParty(sav, pkm);
        }

        return TryFillParty(sav, pkm) || TryFillBoxes(sav, pkm, overwrite);
    }

    private static bool TryFillParty(SaveFile sav, PKM pkm)
    {
        if (sav.PartyCount >= 6)
        {
            return false;
        }

        sav.SetPartySlotAtIndex(pkm, sav.PartyCount);
        return true;
    }

    private static bool TryFillBoxes(SaveFile sav, PKM pkm, bool overwrite)
    {
        for (var box = 0; box < sav.BoxCount; box++)
        {
            for (var slot = 0; slot < sav.BoxSlotCount; slot++)
            {
                var existing = sav.GetBoxSlotAtIndex(box, slot);
                if (existing.Species != 0)
                {
                    if (!overwrite || !IsSlotEligibleForOverwrite(sav, box, slot))
                    {
                        continue;
                    }
                }

                sav.SetBoxSlotAtIndex(pkm, box, slot);
                return true;
            }
        }

        return false;
    }

    // Avoid stomping on locked battle-team slots (Gen 5/6). PKHeX's GetBoxSlotFlags
    // returns a Locked flag for these; treat them as "skip" even in overwrite mode.
    private static bool IsSlotEligibleForOverwrite(SaveFile sav, int box, int slot)
    {
        var flags = sav.GetBoxSlotFlags(box, slot);
        return !flags.HasFlag(StorageSlotSource.Locked);
    }

    private void CancelImport() => cts?.Cancel();

    private Severity GetResultSeverity() => result switch
    {
        null => Severity.Info,
        { Cancelled: true } => Severity.Info,
        { Skipped: 0, Illegal: 0, OverflowRemaining: 0 } => Severity.Success,
        _ => Severity.Warning
    };

    private string GetResultMessage()
    {
        if (result is null)
        {
            return string.Empty;
        }

        if (result.Cancelled)
        {
            return "Import cancelled.";
        }

        var parts = new List<string> { $"Imported {result.Imported}" };
        if (result.Skipped > 0)
        {
            parts.Add($"skipped {result.Skipped} (incompatible)");
        }

        if (result.Illegal > 0)
        {
            parts.Add($"illegal: {result.Illegal}");
        }

        if (result.OverflowToBank > 0)
        {
            parts.Add($"sent {result.OverflowToBank} to Bank");
        }

        if (result.OverflowRemaining > 0)
        {
            parts.Add($"{result.OverflowRemaining} didn't fit (slots full)");
        }

        return string.Join(", ", parts) + ".";
    }

    private void Close() => MudDialog?.Close(result is null
        ? DialogResult.Cancel()
        : DialogResult.Ok(result));

    private sealed record BulkImportResult(
        int Imported,
        int Skipped,
        int Illegal,
        int OverflowRemaining,
        int OverflowToBank)
    {
        public bool Cancelled { get; init; }
    }
}
