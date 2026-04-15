namespace Pkmds.Rcl.Components.MainTabPages;

public partial class TradeTab : RefreshAwareComponent
{
    private bool isLoading;

    [Inject]
    private IBackupService BackupService { get; set; } = null!;

    [Inject]
    private ISettingsService SettingsService { get; set; } = null!;

    [Inject]
    private ILogger<TradeTab> Logger { get; set; } = null!;

    private string SlotATitle =>
        $"Slot A — {AppState.SaveFileName ?? "Loaded save"}";

    private string SlotBTitle =>
        $"Slot B — {AppState.SaveFileNameB ?? "Loaded save"}";

    private async Task LoadSecondarySaveAsync()
    {
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
            // analysis on slot A. Slot B legality is read using the global settings already
            // tuned to slot A's trainer; some handler-state checks may be slightly off for
            // slot B Pokémon but Phase 1 is read-only so this is acceptable.

            AppState.SaveFileB = loadedSave;
            AppState.SaveFileNameB = selectedFile.Name;
            AppState.BoxEditB?.LoadBox(loadedSave.CurrentBox);
            AppState.SelectedBoxNumberB = loadedSave.CurrentBox;
            AppState.SelectedBoxSlotNumberB = null;
            AppState.SelectedPartySlotNumberB = null;

            // Per-slot on-load backup. Identical contract to the slot-A load path:
            // bytes come from the freshly loaded SaveFile (handles Manic EMU rebuild),
            // metadata is keyed by filename + save metadata + source so slot A and slot B
            // backups coexist in IndexedDB. EnforceRetentionAsync remains global —
            // Phase 1 only ever produces one slot-B backup so the shared pool is fine.
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

    private void UnloadSecondarySave()
    {
        AppState.SaveFileB = null;
        RefreshService.Refresh();
    }
}
