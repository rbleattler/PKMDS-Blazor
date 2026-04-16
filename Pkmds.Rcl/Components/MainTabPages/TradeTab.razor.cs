namespace Pkmds.Rcl.Components.MainTabPages;

public partial class TradeTab : RefreshAwareComponent
{
    private bool isLoading;

    // Preserve the slot-B Manic EMU ZIP context across the session so Export Slot B
    // can rebuild the .3ds.sav/.3ds.save ZIP without requiring the user to re-upload.
    private ManicEmuSaveHelper.ManicEmuSaveContext? manicEmuSaveContextB;

    [Inject]
    private IBackupService BackupService { get; set; } = null!;

    [Inject]
    private ISettingsService SettingsService { get; set; } = null!;

    [Inject]
    private IDragDropService DragDropService { get; set; } = null!;

    [Inject]
    private ILogger<TradeTab> Logger { get; set; } = null!;

    private string SlotATitle =>
        $"Slot A — {AppState.SaveFileName ?? "Loaded save"}";

    private string SlotBTitle =>
        $"Slot B — {AppState.SaveFileNameB ?? "Loaded save"}";

    private async Task LoadSecondarySaveAsync()
    {
        // Guard the replace path: if slot B has uncommitted transfers, give the user
        // a chance to back out before the picker opens and we tear down the save.
        if (AppState.SaveFileB is not null && AppState.HasUnsavedChangesB)
        {
            var proceed = await DialogService.ShowMessageBoxAsync(
                "Unsaved changes in Slot B",
                "Slot B has transfers that haven't been exported yet. Replacing it will discard those changes.",
                yesText: "Discard and replace",
                cancelText: "Cancel");
            if (proceed != true)
            {
                return;
            }
        }

        const string message = "Choose a second save file";
        var dialogParameters = new DialogParameters
        {
            { nameof(FileUploadDialog.Message), message }
        };

        var dialog = await DialogService.ShowAsync<FileUploadDialog>(
            "Load Second Save File",
            dialogParameters,
            new DialogOptions { CloseOnEscapeKey = true, BackdropClick = false });

        var result = await dialog.Result;
        if (result is not { Data: IBrowserFile selectedFile })
        {
            return;
        }

        await LoadSecondarySaveFile(selectedFile);
    }

    private async Task LoadSecondarySaveFile(IBrowserFile selectedFile)
    {
        if (selectedFile.Size == 0)
        {
            await DialogService.ShowMessageBoxAsync("Error", "The selected file is empty.");
            return;
        }

        var fileExtension = Path.GetExtension(selectedFile.Name);
        if (fileExtension.Equals(".state", StringComparison.OrdinalIgnoreCase) ||
            fileExtension.Equals(".savestate", StringComparison.OrdinalIgnoreCase))
        {
            await DialogService.ShowMessageBoxAsync("Wrong file type",
                "This looks like an emulator save state, not a save file. " +
                "Please export the actual save file from your emulator instead.");
            return;
        }

        isLoading = true;
        StateHasChanged();

        SaveFile? loadedSave = null;
        ManicEmuSaveHelper.ManicEmuSaveContext? manicContext = null;

        try
        {
            await using var fileStream = selectedFile.OpenReadStream(Constants.MaxFileSize);
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);
            var data = memoryStream.ToArray();

            if (SaveUtil.TryGetSaveFile(data, out var saveFile, selectedFile.Name))
            {
                if (!saveFile.State.Exportable)
                {
                    await DialogService.ShowMessageBoxAsync("Unsupported save file",
                        "This save file cannot be loaded — it may be from an unsupported ROM hack or format.");
                    return;
                }

                loadedSave = saveFile;
            }
            else if (ManicEmuSaveHelper.TryExtractSaveFromZip(data, selectedFile.Name, out saveFile, out var ctx))
            {
                if (!saveFile.State.Exportable)
                {
                    await DialogService.ShowMessageBoxAsync("Unsupported save file",
                        "This save file cannot be loaded — it may be from an unsupported ROM hack or format.");
                    return;
                }

                loadedSave = saveFile;
                manicContext = ctx;
            }
            else
            {
                await DialogService.ShowMessageBoxAsync("Error",
                    "The selected save file is invalid. If this save file came from a ROM hack, it is not supported.");
                return;
            }

            // NOTE: Do NOT call ParseSettings.InitFromSaveFileData(loadedSave) here.
            // That mutates global ParseSettings.ActiveTrainer and would break legality
            // analysis on slot A. Slot B legality is read using the global settings
            // already tuned to slot A's trainer; some handler-state checks may be
            // slightly off for slot B Pokémon but the post-transfer confirmation dialog
            // picks up the important cases.

            AppState.SaveFileB = loadedSave;
            AppState.SaveFileNameB = selectedFile.Name;
            AppState.BoxEditB?.LoadBox(loadedSave.CurrentBox);
            AppState.SelectedBoxNumberB = loadedSave.CurrentBox;
            AppState.SelectedBoxSlotNumberB = null;
            AppState.SelectedPartySlotNumberB = null;
            manicEmuSaveContextB = manicContext;

            // Per-slot on-load backup. Identical contract to the slot-A load path:
            // bytes come from the freshly loaded SaveFile (handles Manic EMU rebuild),
            // metadata is keyed by filename + save metadata + source so slot A and slot B
            // backups coexist in IndexedDB.
            if (SettingsService.Settings.IsAutoBackupEnabled)
            {
                try
                {
                    var rawSave = loadedSave.Write().ToArray();
                    var backupBytes = manicContext is not null
                        ? ManicEmuSaveHelper.RebuildZip(manicContext, rawSave)
                        : rawSave;
                    await BackupService.CreateBackupAsync(
                        backupBytes, loadedSave, selectedFile.Name,
                        isManicEmu: manicContext is not null, source: "auto");
                    await BackupService.EnforceRetentionAsync(SettingsService.Settings.MaxBackupCount);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Auto-backup failed for slot-B {FileName}", selectedFile.Name);
                }
            }

            Snackbar.Add($"Loaded second save: {selectedFile.Name}", Severity.Success);
            RefreshService.Refresh();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading slot-B save file: {FileName}", selectedFile.Name);
            await DialogService.ShowMessageBoxAsync("Error", $"Failed to load save: {ex.Message}");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task UnloadSecondarySaveAsync()
    {
        if (AppState.HasUnsavedChangesB)
        {
            var proceed = await DialogService.ShowMessageBoxAsync(
                "Unsaved changes in Slot B",
                "Slot B has transfers that haven't been exported yet. Closing it will discard those changes.",
                yesText: "Discard and close",
                cancelText: "Cancel");
            if (proceed != true)
            {
                return;
            }
        }

        AppState.SaveFileB = null;
        AppState.SaveFileNameB = null;
        manicEmuSaveContextB = null;
        RefreshService.Refresh();
    }

    private async Task ExportSecondarySaveAsync()
    {
        if (AppState.SaveFileB is not { } saveB)
        {
            return;
        }

        Logger.LogInformation("Exporting slot-B save file");
        var rawSaveBytes = saveB.Write().ToArray();
        var originalName = AppState.SaveFileNameB;

        bool wrote;
        // Same branching as MainLayout.ExportSaveFile — rebuild the Manic EMU ZIP
        // when the slot-B save came from one, otherwise preserve the original name.
        if (manicEmuSaveContextB is not null)
        {
            var (exportName, compoundExt) = ManicEmuSaveHelper.GetExportFileName(originalName);
            var zipBytes = ManicEmuSaveHelper.RebuildZip(manicEmuSaveContextB, rawSaveBytes);
            wrote = await WriteSlotBFileAsync(zipBytes, exportName, compoundExt);
        }
        else if (string.IsNullOrWhiteSpace(originalName))
        {
            wrote = await WriteSlotBFileAsync(rawSaveBytes, "save.sav", ".sav");
        }
        else
        {
            var ext = Path.GetExtension(originalName);
            wrote = await WriteSlotBFileAsync(rawSaveBytes, originalName, ext);
        }

        if (wrote)
        {
            AppState.HasUnsavedChangesB = false;
        }
    }

    // Minimal duplicate of MainLayout's WriteFile: File System Access API when supported,
    // fall back to an anchor click for legacy browsers. Returns whether the file was
    // actually written (false on user cancel or failure) so the caller knows whether
    // to clear the dirty flag.
    private async Task<bool> WriteSlotBFileAsync(byte[] data, string fileName, string fileTypeExtension)
    {
        if (!await FileSystemAccessService.IsSupportedAsync())
        {
            var finalName = string.IsNullOrWhiteSpace(fileName) ? "save.sav" : fileName;
            var base64 = Convert.ToBase64String(data);
            var element = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "eval", "document.createElement('a')");
            await element.InvokeVoidAsync("setAttribute", "href",
                $"data:application/x-pokemon-savedata;base64,{base64}");
            await element.InvokeVoidAsync("setAttribute", "download", finalName);
            await element.InvokeVoidAsync("click");
            return true;
        }

        try
        {
            await JSRuntime.InvokeVoidAsync("showFilePickerAndWrite",
                fileName, data, fileTypeExtension, "Save File");
            return true;
        }
        catch (JSException ex) when (ex.Message.Contains("AbortError", StringComparison.OrdinalIgnoreCase)
                                     || ex.Message.Contains("aborted a request", StringComparison.OrdinalIgnoreCase))
        {
            // User dismissed the picker — not an error.
            return false;
        }
        catch (JSException ex)
        {
            Logger.LogError(ex, "Error exporting slot-B save file: {FileName}", fileName);
            Snackbar.Add("Export failed. Please try again or use a different browser.", Severity.Error);
            return false;
        }
    }

    // ── Transfer logic ────────────────────────────────────────────────────────

    private bool CanTransferFromA() =>
        AppState.SaveFile is not null
        && AppState.SaveFileB is not null
        && TryGetSelectedSource(fromA: true, out _, out _, out _, out _);

    private bool CanTransferFromB() =>
        AppState.SaveFile is not null
        && AppState.SaveFileB is not null
        && TryGetSelectedSource(fromA: false, out _, out _, out _, out _);

    private bool TryGetSelectedSource(bool fromA, out SaveFile source, out bool isParty,
        out int? boxNumber, out int slotNumber)
    {
        source = null!;
        isParty = false;
        boxNumber = null;
        slotNumber = -1;

        var sav = fromA ? AppState.SaveFile : AppState.SaveFileB;
        if (sav is null)
        {
            return false;
        }

        var partySel = fromA ? AppState.SelectedPartySlotNumber : AppState.SelectedPartySlotNumberB;
        if (partySel is { } partySlot && partySlot < sav.PartyCount)
        {
            var pkm = sav.GetPartySlotAtIndex(partySlot);
            if (pkm is { Species: > 0 })
            {
                source = sav;
                isParty = true;
                slotNumber = partySlot;
                return true;
            }
        }

        var boxSel = fromA ? AppState.SelectedBoxNumber : AppState.SelectedBoxNumberB;
        var boxSlotSel = fromA ? AppState.SelectedBoxSlotNumber : AppState.SelectedBoxSlotNumberB;
        if (boxSel is { } selBox && boxSlotSel is { } selSlot && sav.BoxCount > 0)
        {
            var pkm = sav.GetBoxSlotAtIndex(selBox, selSlot);
            if (pkm is { Species: > 0 })
            {
                source = sav;
                isParty = false;
                boxNumber = selBox;
                slotNumber = selSlot;
                return true;
            }
        }

        return false;
    }

    private async Task TransferSelectedAsync(bool fromA)
    {
        if (!TryGetSelectedSource(fromA, out var srcSave, out var srcIsParty,
                out var srcBox, out var srcSlot))
        {
            Snackbar.Add("Select a Pokémon in the source pane first.", Severity.Warning);
            return;
        }

        var destSave = fromA ? AppState.SaveFileB : AppState.SaveFile;
        if (destSave is null)
        {
            return;
        }

        // Prefer the user's selected dest slot when one is chosen; otherwise pick the
        // first empty slot (party first, then the currently-selected box, then any box).
        if (!TryGetSelectedDest(fromA: !fromA, destSave, out var destIsParty,
                out var destBox, out var destSlot)
            && !TryFindFirstEmptyDest(destSave, srcIsParty, fromA: !fromA,
                out destIsParty, out destBox, out destSlot))
        {
            Snackbar.Add("No empty slot in the destination save.", Severity.Warning);
            return;
        }

        await ExecuteTransferAsync(srcSave, srcIsParty, srcBox, srcSlot,
            destSave, destIsParty, destBox, destSlot);
    }

    private bool TryGetSelectedDest(bool fromA, SaveFile destSave, out bool isParty,
        out int? boxNumber, out int slotNumber)
    {
        isParty = false;
        boxNumber = null;
        slotNumber = -1;

        var partySel = fromA ? AppState.SelectedPartySlotNumber : AppState.SelectedPartySlotNumberB;
        if (partySel is { } partySlot && partySlot <= destSave.PartyCount && partySlot < 6)
        {
            isParty = true;
            slotNumber = partySlot;
            return true;
        }

        var boxSel = fromA ? AppState.SelectedBoxNumber : AppState.SelectedBoxNumberB;
        var boxSlotSel = fromA ? AppState.SelectedBoxSlotNumber : AppState.SelectedBoxSlotNumberB;
        if (boxSel is { } selBox && boxSlotSel is { } selSlot && destSave.BoxCount > 0)
        {
            boxNumber = selBox;
            slotNumber = selSlot;
            return true;
        }

        return false;
    }

    private static bool TryFindFirstEmptyDest(SaveFile destSave, bool srcIsParty, bool fromA,
        out bool isParty, out int? boxNumber, out int slotNumber)
    {
        _ = fromA;
        isParty = false;
        boxNumber = null;
        slotNumber = -1;

        // Prefer the party if the source came from a party slot and there's room.
        if (srcIsParty && destSave.PartyCount < 6)
        {
            isParty = true;
            slotNumber = destSave.PartyCount;
            return true;
        }

        // Scan boxes for the first empty slot.
        for (var box = 0; box < destSave.BoxCount; box++)
        {
            for (var slot = 0; slot < destSave.BoxSlotCount; slot++)
            {
                if (destSave.GetBoxSlotAtIndex(box, slot) is { Species: 0 })
                {
                    isParty = false;
                    boxNumber = box;
                    slotNumber = slot;
                    return true;
                }
            }
        }

        // Fall back to appending to the party if no box slot is available.
        if (destSave.PartyCount < 6)
        {
            isParty = true;
            slotNumber = destSave.PartyCount;
            return true;
        }

        return false;
    }

    private async Task HandleSlotDropAsync(TradeSlotTarget dest)
    {
        // Snapshot the drag source before ClearDrag runs — dragend fires concurrently.
        var srcPokemon = DragDropService.DraggedPokemon;
        var srcSave = DragDropService.DragSourceSaveFile ?? AppState.SaveFile;
        var srcBox = DragDropService.DragSourceBoxNumber;
        var srcSlot = DragDropService.DragSourceSlotNumber;
        var srcIsParty = DragDropService.IsDragSourceParty;
        DragDropService.ClearDrag();

        if (srcPokemon is null || srcSave is null || srcSlot < 0)
        {
            return;
        }

        await ExecuteTransferAsync(srcSave, srcIsParty, srcBox, srcSlot,
            dest.OwnerSaveFile, dest.IsParty, dest.BoxNumber, dest.SlotNumber);
    }

    private async Task ExecuteTransferAsync(
        SaveFile srcSave, bool srcIsParty, int? srcBox, int srcSlot,
        SaveFile destSave, bool destIsParty, int? destBox, int destSlot)
    {
        // Reject in-place moves outright.
        if (ReferenceEquals(srcSave, destSave)
            && srcIsParty == destIsParty
            && srcBox == destBox
            && srcSlot == destSlot)
        {
            return;
        }

        // Let's Go storage has its own set-semantics via the slot-index overload; keep
        // behavior parity with PokemonSlotComponent, which disables drag/drop for SAV7b.
        if (srcSave is SAV7b || destSave is SAV7b)
        {
            Snackbar.Add("Transfers involving Let's Go saves aren't supported yet.", Severity.Warning);
            return;
        }

        var srcPkm = srcIsParty
            ? srcSave.GetPartySlotAtIndex(srcSlot)
            : srcBox.HasValue
                ? srcSave.GetBoxSlotAtIndex(srcBox.Value, srcSlot)
                : null;
        if (srcPkm is not { Species: > 0 })
        {
            return;
        }

        // Intra-save moves: delegate to the same move/swap logic used by the Party/Box tab,
        // but parameterised to the owning save file so slot B also works.
        if (ReferenceEquals(srcSave, destSave))
        {
            if (!MoveWithinSave(srcSave, srcIsParty, srcBox, srcSlot,
                    destIsParty, destBox, destSlot))
            {
                return;
            }

            MarkSlotBDirtyIfInvolved(srcSave, destSave);
            RefreshService.Refresh();
            return;
        }

        // Cross-save transfer.
        // Keep the destination party compact: if the user dropped past PartyCount on an
        // empty slot, clamp to PartyCount so SetPartySlotAtIndex appends rather than
        // leaving a gap (mirrors the intra-save clamp in MoveWithinSave).
        if (destIsParty && destSlot >= destSave.PartyCount)
        {
            destSlot = destSave.PartyCount;
        }

        var destPkmPrev = destIsParty
            ? destSave.GetPartySlotAtIndex(destSlot)
            : destBox.HasValue
                ? destSave.GetBoxSlotAtIndex(destBox.Value, destSlot)
                : null;
        if (destPkmPrev is null)
        {
            return;
        }

        var haxEnabled = AppState.IsHaXEnabled;
        var converted = ConvertForSave(srcPkm.Clone(), destSave, haxEnabled, out var forwardMessage);
        if (converted is null)
        {
            Snackbar.Add(
                string.IsNullOrEmpty(forwardMessage)
                    ? "Could not convert the source Pokémon to the destination's format."
                    : $"Could not transfer: {forwardMessage}",
                Severity.Error);
            return;
        }
        destSave.AdaptToSaveFile(converted, destIsParty);

        // If the destination slot has a Pokémon, we swap by converting it back to the
        // source's format. Otherwise we clear the source slot after the write.
        PKM? convertedBack = null;
        if (destPkmPrev.Species > 0)
        {
            convertedBack = ConvertForSave(destPkmPrev.Clone(), srcSave, haxEnabled, out var reverseMessage);
            if (convertedBack is null)
            {
                Snackbar.Add(
                    string.IsNullOrEmpty(reverseMessage)
                        ? "Could not convert the destination Pokémon back to the source's format for a swap."
                        : $"Swap not possible: {reverseMessage}",
                    Severity.Error);
                return;
            }
            srcSave.AdaptToSaveFile(convertedBack, srcIsParty);
        }

        // Apply writes before running legality — LegalityAnalysis re-runs below so we
        // catch any adapt-on-write surprises introduced by AdaptToSaveFile.
        WriteSlot(destSave, destIsParty, destBox, destSlot, converted);
        if (convertedBack is not null)
        {
            WriteSlot(srcSave, srcIsParty, srcBox, srcSlot, convertedBack);
        }
        else
        {
            ClearSlot(srcSave, srcIsParty, srcBox, srcSlot);
        }

        // Re-run legality on the destination-side result. In HaX mode we trust the user
        // and skip the confirmation; otherwise prompt before committing to the write.
        if (!AppState.IsHaXEnabled)
        {
            var proceed = await ConfirmLegalityAsync(converted, destSave);
            if (!proceed)
            {
                // Revert: restore the original destination and source PKMs.
                WriteSlot(destSave, destIsParty, destBox, destSlot, destPkmPrev);
                WriteSlot(srcSave, srcIsParty, srcBox, srcSlot, srcPkm);
                RefreshService.Refresh();
                return;
            }
        }

        if (!string.IsNullOrEmpty(forwardMessage))
        {
            Snackbar.Add($"Warning: {forwardMessage}", Severity.Warning);
        }

        MarkSlotBDirtyIfInvolved(srcSave, destSave);
        RefreshService.Refresh();
    }

    private void MarkSlotBDirtyIfInvolved(SaveFile srcSave, SaveFile destSave)
    {
        if (AppState.SaveFileB is { } slotB
            && (ReferenceEquals(srcSave, slotB) || ReferenceEquals(destSave, slotB)))
        {
            AppState.HasUnsavedChangesB = true;
        }
    }

    private static PKM? ConvertForSave(PKM pkm, SaveFile destSave, bool haxEnabled, out string message)
    {
        message = string.Empty;
        var destType = destSave.PKMType;
        if (pkm.GetType() == destType)
        {
            return pkm;
        }

        var converted = EntityConverter.ConvertToType(pkm, destType, out var result);
        if (converted is not null && result.IsSuccess)
        {
            return converted;
        }

        // HaX fallback mirrors PokemonSlotComponent / PokemonBankTab — retry with
        // AllowIncompatibleAll so DLC-gated species (e.g. Bibarel into pre-Teal-Mask
        // Violet) can still transfer. The original message becomes a warning.
        if (haxEnabled)
        {
            var previous = EntityConverter.AllowIncompatibleConversion;
            EntityConverter.AllowIncompatibleConversion = EntityCompatibilitySetting.AllowIncompatibleAll;
            try
            {
                converted = EntityConverter.ConvertToType(pkm, destType, out result);
            }
            finally
            {
                EntityConverter.AllowIncompatibleConversion = previous;
            }

            if (converted is not null && result.IsSuccess)
            {
                message = result.GetDisplayString(pkm, destType);
                return converted;
            }
        }

        message = result.GetDisplayString(pkm, destType);
        return null;
    }

    private static void WriteSlot(SaveFile save, bool isParty, int? boxNumber, int slotNumber, PKM pkm)
    {
        if (isParty)
        {
            save.SetPartySlotAtIndex(pkm, slotNumber);
        }
        else if (boxNumber.HasValue)
        {
            save.SetBoxSlotAtIndex(pkm, boxNumber.Value, slotNumber);
        }
    }

    private static void ClearSlot(SaveFile save, bool isParty, int? boxNumber, int slotNumber)
    {
        if (isParty)
        {
            // DeletePartySlot keeps the party compact, matching AppService.MovePokemon.
            save.DeletePartySlot(slotNumber);
        }
        else if (boxNumber.HasValue)
        {
            save.SetBoxSlotAtIndex(save.BlankPKM, boxNumber.Value, slotNumber);
        }
    }

    private async Task<bool> ConfirmLegalityAsync(PKM pkm, SaveFile destSave)
    {
        // Adapt-aware analysis: the converted PKM is already placed in the dest save, so
        // a plain LegalityAnalysis reflects what the user will see if we commit.
        var la = new LegalityAnalysis(pkm);
        var invalid = la.Results.Any(r => r.Judgement == PKHeX.Core.Severity.Invalid)
                      || !MoveResult.AllValid(la.Info.Moves)
                      || !MoveResult.AllValid(la.Info.Relearn);
        var fishy = !invalid && la.Results.Any(r => r.Judgement == PKHeX.Core.Severity.Fishy);
        if (!invalid && !fishy)
        {
            return true;
        }

        // LegalityLocalizationContext is a ref struct, so we can't use LINQ over it —
        // materialise the humanized strings via a plain loop.
        var ctx = LegalityLocalizationContext.Create(la);
        var messages = new List<string>(3);
        foreach (var r in la.Results)
        {
            if (r.Judgement is not (PKHeX.Core.Severity.Invalid or PKHeX.Core.Severity.Fishy))
            {
                continue;
            }
            var humanized = ctx.Humanize(in r, verbose: false);
            if (!string.IsNullOrWhiteSpace(humanized))
            {
                messages.Add(humanized);
            }
            if (messages.Count >= 3)
            {
                break;
            }
        }
        _ = destSave;

        var severity = invalid ? "illegal" : "fishy";
        var body = messages.Count > 0
            ? $"The converted Pokémon is {severity}:\n\n• {string.Join("\n• ", messages)}\n\nProceed with the transfer?"
            : $"The converted Pokémon is {severity}. Proceed with the transfer?";
        var result = await DialogService.ShowMessageBoxAsync(
            invalid ? "Illegal after conversion" : "Fishy after conversion",
            body,
            yesText: "Transfer anyway",
            cancelText: "Cancel");
        return result == true;
    }

    // ── Intra-save move (both slot A→A and slot B→B) ──────────────────────────

    // Deliberately duplicates the branching in AppService.MovePokemon so slot B has the
    // same move/swap + Gen 1/2 compacting semantics without taking a dependency on
    // AppState.SaveFile. Returns false when the move is a no-op (e.g. trying to move
    // the last battle-ready party member out).
    private static bool MoveWithinSave(SaveFile save,
        bool srcIsParty, int? srcBox, int srcSlot,
        bool destIsParty, int? destBox, int destSlot)
    {
        var source = srcIsParty
            ? save.GetPartySlotAtIndex(srcSlot)
            : srcBox.HasValue ? save.GetBoxSlotAtIndex(srcBox.Value, srcSlot) : null;
        if (source is null)
        {
            return false;
        }

        var dest = destIsParty
            ? save.GetPartySlotAtIndex(destSlot)
            : destBox.HasValue ? save.GetBoxSlotAtIndex(destBox.Value, destSlot) : null;
        if (dest is null)
        {
            return false;
        }

        var isSwap = source.Species > 0 && dest.Species > 0;

        // Party safety: don't let the last non-Egg party member get moved out.
        if (srcIsParty && !destIsParty && !isSwap)
        {
            var battleReady = 0;
            for (var i = 0; i < save.PartyCount; i++)
            {
                if (i == srcSlot)
                {
                    continue;
                }
                var partyMon = save.GetPartySlotAtIndex(i);
                if (partyMon is { Species: > 0, IsEgg: false })
                {
                    battleReady++;
                }
            }
            if (battleReady == 0)
            {
                return false;
            }
        }

        if (isSwap)
        {
            WriteSlot(save, srcIsParty, srcBox, srcSlot, dest);
            WriteSlot(save, destIsParty, destBox, destSlot, source);
            return true;
        }

        if (srcIsParty && !destIsParty)
        {
            if (destBox.HasValue)
            {
                save.SetBoxSlotAtIndex(source, destBox.Value, destSlot);
            }
            save.DeletePartySlot(srcSlot);
            return true;
        }

        if (!srcIsParty && destIsParty)
        {
            if (srcBox.HasValue)
            {
                save.SetBoxSlotAtIndex(save.BlankPKM, srcBox.Value, srcSlot);
            }
            var target = destSlot >= save.PartyCount ? save.PartyCount : destSlot;
            save.SetPartySlotAtIndex(source, target);
            return true;
        }

        WriteSlot(save, destIsParty, destBox, destSlot, source);
        ClearSlot(save, srcIsParty, srcBox, srcSlot);
        return true;
    }
}
