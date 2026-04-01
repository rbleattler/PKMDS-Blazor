namespace Pkmds.Rcl.Components.Layout;

public partial class MainLayout : IDisposable
{
    [StringSyntax(StringSyntaxAttribute.Uri)]
    private const string GitHubRepoLink = "https://github.com/codemonkey85/PKMDS-Blazor";

    private const string GitHubTooltip = "Source code on GitHub";

    private IBrowserFile? browserLoadSaveFile;
    private bool isDarkMode;
    private ManicEmuSaveHelper.ManicEmuSaveContext? manicEmuSaveContext;
    private MudThemeProvider? mudThemeProvider;
    private bool systemIsDarkMode;
    private ThemeMode themeMode = ThemeMode.System;

    [Inject]
    private ISettingsService SettingsService { get; set; } = null!;

    private bool IsUpdateAvailable { get; set; }
    private bool IsCheckingForUpdates { get; set; }
    private bool IsUpToDate { get; set; }
    private bool IsUpdateCheckFailed { get; set; }

    public void Dispose()
    {
        RefreshService.OnAppStateChanged -= StateHasChanged;
        RefreshService.OnUpdateAvailable -= ShowUpdateMessage;
    }

    protected override void OnInitialized()
    {
        RefreshService.OnAppStateChanged += StateHasChanged;
        RefreshService.OnUpdateAvailable += ShowUpdateMessage;
    }

    private void ShowUpdateMessage()
    {
        IsUpdateAvailable = true;
        IsUpToDate = false;
        StateHasChanged();
    }

    private async Task CheckForUpdates()
    {
        IsCheckingForUpdates = true;
        IsUpToDate = false;
        IsUpdateCheckFailed = false;
        StateHasChanged();

        var result = await JSRuntime.InvokeAsync<string>("checkForUpdates");

        IsCheckingForUpdates = false;
        switch (result)
        {
            case "none":
                IsUpToDate = true;
                StateHasChanged();
                await Task.Delay(3000);
                IsUpToDate = false;
                break;
            case "error":
            case "no-sw":
                IsUpdateCheckFailed = true;
                StateHasChanged();
                await Task.Delay(4000);
                IsUpdateCheckFailed = false;
                break;
            // "found": JS already dispatched 'updateAvailable' → ShowUpdateMessage() sets IsUpdateAvailable = true
        }

        StateHasChanged();
    }

    private async Task ReloadApp() =>
        await JSRuntime.InvokeVoidAsync("location.reload");

    private bool ComputeIsDarkMode() => themeMode switch
    {
        ThemeMode.Dark => true,
        ThemeMode.Light => false,
        _ => systemIsDarkMode
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || mudThemeProvider is null)
        {
            return;
        }

        systemIsDarkMode = await mudThemeProvider.GetSystemDarkModeAsync();
        await mudThemeProvider.WatchSystemDarkModeAsync(OnSystemPreferenceChanged);

        // Load all persisted settings (theme, PKHaX, verbose logging, trainer defaults)
        await SettingsService.LoadAsync();

        themeMode = SettingsService.Settings.ThemeMode switch
        {
            "dark" => ThemeMode.Dark,
            "light" => ThemeMode.Light,
            _ => ThemeMode.System
        };
        isDarkMode = ComputeIsDarkMode();
        RefreshService.RefreshTheme(isDarkMode);
        RefreshService.Refresh();

        StateHasChanged();
    }

    private async Task OnSystemPreferenceChanged(bool newValue)
    {
        systemIsDarkMode = newValue;
        if (themeMode == ThemeMode.System)
        {
            isDarkMode = newValue;
            RefreshService.RefreshTheme(isDarkMode);
            var themeStr = newValue
                ? "dark"
                : "light";
            await JSRuntime.InvokeVoidAsync("setAppTheme", themeStr);
            StateHasChanged();
        }
    }

    private async Task OnThemeModeChanged(ThemeMode newMode)
    {
        themeMode = newMode;
        isDarkMode = ComputeIsDarkMode();
        RefreshService.RefreshTheme(isDarkMode);

        var themeStr = themeMode switch
        {
            ThemeMode.Light => "light",
            ThemeMode.Dark => "dark",
            _ => isDarkMode
                ? "dark"
                : "light"
        };
        await JSRuntime.InvokeVoidAsync("setAppTheme", themeStr);

        // Persist the new theme through the settings service (keeps pkmds_theme in sync too)
        await SettingsService.SaveAsync(SettingsService.Settings with
        {
            ThemeMode = themeMode switch
            {
                ThemeMode.Light => "light",
                ThemeMode.Dark => "dark",
                _ => "system"
            }
        });

        StateHasChanged();
    }

    private void DrawerToggle() => AppService.ToggleDrawer();

    private async Task ShowBugReportDialog()
    {
        var parameters = new DialogParameters
        {
            { nameof(BugReportDialog.HasSaveFile), AppState.SaveFile is not null },
            { nameof(BugReportDialog.AppVersion), AppState.AppVersion ?? string.Empty },
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<BugReportDialog>("Report a Bug", parameters, options);
        var result = await dialog.Result;
        if (result is { Data: string issueUrl })
        {
            Snackbar.Add($"Bug report submitted! <a href=\"{issueUrl}\" target=\"_blank\">View issue</a>",
                Severity.Success);
        }
    }

    private async Task ShowSettingsDialog()
    {
        var parameters =
            new DialogParameters { { nameof(AppSettingsDialog.InitialSettings), SettingsService.Settings } };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };

        var dialog = await DialogService.ShowAsync<AppSettingsDialog>("Settings", parameters, options);
        var result = await dialog.Result;

        if (result is { Data: AppSettings updated })
        {
            await SettingsService.SaveAsync(updated);
            RefreshService.Refresh();

            // Re-apply theme from the updated settings
            themeMode = updated.ThemeMode switch
            {
                "light" => ThemeMode.Light,
                "dark" => ThemeMode.Dark,
                _ => ThemeMode.System
            };
            isDarkMode = ComputeIsDarkMode();
            RefreshService.RefreshTheme(isDarkMode);

            var themeStr = isDarkMode
                ? "dark"
                : "light";
            await JSRuntime.InvokeVoidAsync("setAppTheme", themeStr);

            StateHasChanged();
        }
    }

    private async Task ShowLoadSaveFileDialog()
    {
        const string message = "Choose a save file";
        const string manicEmuHint =
            "Tip: If you're using Manic EMU, upload the .3ds.sav or .3ds.save export directly " +
            "for seamless round-trip import support.";

        var dialogParameters = new DialogParameters { { nameof(FileUploadDialog.Message), message }, { nameof(FileUploadDialog.HintText), manicEmuHint } };

        var dialog = await DialogService.ShowAsync<FileUploadDialog>("Load Save File",
            dialogParameters,
            new() { CloseOnEscapeKey = true, BackdropClick = false });

        var result = await dialog.Result;
        if (result is { Data: IBrowserFile selectedFile })
        {
            browserLoadSaveFile = selectedFile;
            await LoadSaveFile(selectedFile);
        }
    }

    private async Task LoadSaveFile(IBrowserFile selectedFile)
    {
        if (browserLoadSaveFile is null)
        {
            Logger.LogWarning("Attempted to load save file but no file was selected");
            await DialogService.ShowMessageBoxAsync("No file selected", "Please select a file to load.");
            return;
        }

        if (selectedFile.Size == 0)
        {
            Logger.LogWarning("Attempted to load empty save file: {FileName}", selectedFile.Name);
            await DialogService.ShowMessageBoxAsync("Error", "The selected file is empty.");
            return;
        }

        var fileExtension = Path.GetExtension(selectedFile.Name);
        if (fileExtension.Equals(".state", StringComparison.OrdinalIgnoreCase) ||
            fileExtension.Equals(".savestate", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("User attempted to load a save state: {FileName}", selectedFile.Name);
            await DialogService.ShowMessageBoxAsync("Wrong file type",
                "This looks like an emulator save state, not a save file. " +
                "Save states are internal emulator snapshots and cannot be edited here. " +
                "Please export the actual save file from your emulator instead (usually a .sav or .dsv file).");
            return;
        }

        Logger.LogInformation("Loading save file: {FileName}", selectedFile.Name);
        AppService.ClearSelection();
        ParseSettings.ClearActiveTrainer();
        AppState.SaveFile = null;
        AppState.ShowProgressIndicator = true;

        var data = Array.Empty<byte>();
        try
        {
            await using var fileStream = browserLoadSaveFile.OpenReadStream(Constants.MaxFileSize);
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);
            data = memoryStream.ToArray();
            Logger.LogDebug("Read {ByteCount} bytes from save file", data.Length);

            // Try to load the file directly as a raw save.
            if (SaveUtil.TryGetSaveFile(data, out var saveFile, selectedFile.Name))
            {
                manicEmuSaveContext = null;
                FinishLoadingSaveFile(saveFile);
            }
            // If that fails, check whether this is a Manic EMU .3ds.sav ZIP archive.
            // Manic EMU packages 3DS saves as a ZIP containing sdmc/… directory paths.
            else if (ManicEmuSaveHelper.TryExtractSaveFromZip(data, selectedFile.Name, out saveFile, out var manicContext))
            {
                manicEmuSaveContext = manicContext;
                Logger.LogInformation("Loaded save from Manic EMU .3ds.sav/.3ds.save archive; entry: {EntryPath}", manicContext.SaveEntryPath);
                FinishLoadingSaveFile(saveFile);
            }
            else
            {
                Logger.LogError("Failed to load save file: {FileName} - Invalid save file format", selectedFile.Name);

                const string message =
                    "The selected save file is invalid. If this save file came from a ROM hack, it is not supported. Otherwise, try saving in-game and re-exporting / re-uploading the save file.";
                await DialogService.ShowMessageBoxAsync("Error", message);
                AppState.ShowProgressIndicator = false;
                return;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading save file: {FileName}", selectedFile.Name);
            await DialogService.ShowMessageBoxAsync("Error", $"{ex.Message}{Environment.NewLine}{ex.StackTrace}");
        }

        if (AppState.SaveFile is null)
        {
            AppState.ShowProgressIndicator = false;
            return;
        }

        AppState.ShowProgressIndicator = false;
        RefreshService.RefreshBoxAndPartyState();
    }

    private void FinishLoadingSaveFile(SaveFile saveFile)
    {
        // Call InitFromSaveFileData to set ParseSettings.ActiveTrainer to the loaded save file.
        // This enables per-Pokémon handler state validation in HistoryVerifier.VerifyHandlerState,
        // matching PKHeX WinForms behaviour and preventing false-positive legality errors on
        // Pokémon whose OT matches the loaded trainer (e.g. BDSP Palkia).
        //
        // InitFromSaveFileData also sets AllowGBCartEra based on SAV1/SAV2.IsVirtualConsole,
        // which gates AllowGBEraEvents (Nintendo Event Mew, GS Ball Celebi, etc.) and
        // AllowGBStadium2. Physical Gen 1/2 saves correctly get AllowGBCartEra = true;
        // VC saves (filename "sav*.dat") get false. Renamed VC saves may be misidentified as
        // physical cartridge saves — that is a PKHeX bug tracked at
        // https://github.com/kwsch/PKHeX/issues/4734 and is not something we work around here,
        // as doing so breaks legitimate GB era events on real physical cartridge saves.
        ParseSettings.InitFromSaveFileData(saveFile);
        AppState.SaveFile = saveFile;
        AppState.BoxEdit?.LoadBox(saveFile.CurrentBox);
        Logger.LogInformation("Successfully loaded save file: {SaveType}, Generation: {Generation}",
            saveFile.GetType().Name, saveFile.Generation);
    }

    private static string EnsureExtension(string fileName, string extension)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "save";
        }

        extension = extension.StartsWith('.')
            ? extension
            : $".{extension}";

        return fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}{extension}";
    }

    private async Task ExportSaveFile()
    {
        if (AppState.SaveFile is null)
        {
            Logger.LogWarning("Attempted to export save file but no save file is loaded");
            return;
        }

        Logger.LogInformation("Exporting save file");
        AppState.ShowProgressIndicator = true;

        var rawSaveBytes = AppState.SaveFile.Write().ToArray();
        var originalName = browserLoadSaveFile?.Name;

        // If the save was loaded from a Manic EMU .3ds.sav ZIP, rebuild the ZIP so the
        // user can import it directly back into Manic EMU without any manual repacking.
        if (manicEmuSaveContext is not null)
        {
            // Determine the export filename and compound extension, preserving the
            // original Manic EMU extension (.3ds.save on iOS, .3ds.sav elsewhere) so
            // the rebuilt ZIP can be imported directly without renaming.
            var (exportName, compoundExt) = ManicEmuSaveHelper.GetExportFileName(originalName);
            Logger.LogDebug("Exporting save as Manic EMU {Extension}: {FileName}", compoundExt, exportName);

            var zipBytes = ManicEmuSaveHelper.RebuildZip(manicEmuSaveContext, rawSaveBytes);
            await WriteFile(zipBytes, exportName, compoundExt, "Save File");
        }
        // Only default to "save.sav" if we have no original filename at all
        else if (string.IsNullOrWhiteSpace(originalName))
        {
            originalName = "save";
            const string fileExtensionFromName = ".sav";
            var finalName = EnsureExtension(originalName, fileExtensionFromName);
            Logger.LogDebug("Exporting save file as: {FileName}", finalName);

            await WriteFile(rawSaveBytes, finalName, fileExtensionFromName, "Save File");
        }
        else
        {
            // Preserve the original filename exactly as it was (with or without extension)
            var fileExtensionFromName = Path.GetExtension(originalName);
            Logger.LogDebug("Exporting save file as: {FileName}", originalName);

            await WriteFile(rawSaveBytes, originalName, fileExtensionFromName, "Save File");
        }

        Logger.LogInformation("Save file exported successfully");
        AppState.ShowProgressIndicator = false;
    }

    private async Task ShowLoadPokemonFileDialog()
    {
        const string title = "Load Pokémon File";
        const string message = "Choose a Pokémon file";

        var dialogParameters = new DialogParameters { { nameof(FileUploadDialog.Message), message } };

        var dialog = await DialogService.ShowAsync<FileUploadDialog>(
            title,
            dialogParameters,
            new() { CloseOnEscapeKey = true, BackdropClick = false });

        var result = await dialog.Result;
        if (result is { Data: IBrowserFile selectedFile })
        {
            await LoadPokemonFile(selectedFile, title);
        }
    }

    private async Task ShowLoadMysteryGiftFileDialog()
    {
        const string title = "Load Mystery Gift file";
        const string message = "Choose a Mystery Gift file";

        var dialogParameters = new DialogParameters { { nameof(FileUploadDialog.Message), message } };

        var dialog = await DialogService.ShowAsync<FileUploadDialog>(
            title,
            dialogParameters,
            new() { CloseOnEscapeKey = true, BackdropClick = false });

        var result = await dialog.Result;
        if (result is { Data: IBrowserFile selectedFile })
        {
            await LoadMysteryGiftFile(selectedFile);
        }
    }

    private async Task LoadPokemonFile(IBrowserFile browserLoadPokemonFile, string title)
    {
        if (AppState.SaveFile is not { } saveFile)
        {
            Logger.LogWarning("Attempted to load Pokémon file but no save file is loaded");
            return;
        }

        if (browserLoadPokemonFile.Size == 0)
        {
            Logger.LogWarning("Attempted to load empty Pokémon file: {FileName}", browserLoadPokemonFile.Name);
            Snackbar.Add("The selected file is empty.", Severity.Error);
            return;
        }

        Logger.LogInformation("Loading Pokémon file: {FileName}", browserLoadPokemonFile.Name);
        AppState.ShowProgressIndicator = true;

        try
        {
            await using var fileStream = browserLoadPokemonFile.OpenReadStream(Constants.MaxFileSize);
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);
            var data = memoryStream.ToArray();
            Logger.LogDebug("Read {ByteCount} bytes from Pokémon file", data.Length);

            if (!FileUtil.TryGetPKM(data, out var pkm, Path.GetExtension(browserLoadPokemonFile.Name), saveFile))
            {
                Logger.LogError("Failed to load Pokémon file: {FileName} - Not a supported format",
                    browserLoadPokemonFile.Name);
                Snackbar.Add("The file is not a supported Pokémon file.", Severity.Error);
                return;
            }

            var pokemon = pkm.Clone();

            if (pkm.GetType() != saveFile.PKMType)
            {
                Logger.LogDebug("Converting Pokémon from {SourceType} to {TargetType}", pkm.GetType().Name,
                    saveFile.PKMType.Name);
                pokemon = EntityConverter.ConvertToType(pkm, saveFile.PKMType, out var c);
                if (!c.IsSuccess || pokemon is null)
                {
                    Logger.LogError("Failed to convert Pokémon: {ConversionResult}",
                        c.GetDisplayString(pkm, saveFile.PKMType));
                    Snackbar.Add(c.GetDisplayString(pkm, saveFile.PKMType), Severity.Error);
                    return;
                }
            }

            saveFile.AdaptToSaveFile(pokemon);

            if (!await EnsureTargetSlotSelectedAsync(saveFile))
            {
                return;
            }

            AppService.EditFormPokemon = pokemon;
            var editedPkm = AppService.EditFormPokemon ?? pokemon;
            AppService.SavePokemon(editedPkm);
            Logger.LogInformation("Pokémon imported successfully via selected slot");

            var la = new LegalityAnalysis(editedPkm);
            if (la.Valid)
            {
                Snackbar.Add($"{title}: {GameInfo.Strings.Species[editedPkm.Species]} imported successfully.", Severity.Success);
            }
            else
            {
                Snackbar.Add(
                    $"{title}: {GameInfo.Strings.Species[editedPkm.Species]} imported, but legality check flagged issues. " +
                    "Review the Pokémon in the editor.",
                    Severity.Warning);
            }

            RefreshService.RequestJumpToPartyBox();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading Pokémon file: {FileName}", browserLoadPokemonFile.Name);
            await DialogService.ShowMessageBoxAsync("Error", $"{ex.Message}{Environment.NewLine}{ex.StackTrace}");
        }
        finally
        {
            AppState.ShowProgressIndicator = false;
        }
    }

    private async Task LoadMysteryGiftFile(IBrowserFile browserLoadMysteryGiftFile)
    {
        if (AppState.SaveFile is not { } saveFile)
        {
            Logger.LogWarning("Attempted to load Mystery Gift file but no save file is loaded");
            return;
        }

        if (browserLoadMysteryGiftFile.Size == 0)
        {
            Logger.LogWarning("Attempted to load empty Mystery Gift file: {FileName}", browserLoadMysteryGiftFile.Name);
            Snackbar.Add("The selected file is empty.", Severity.Error);
            return;
        }

        Logger.LogInformation("Loading Mystery Gift file: {FileName}", browserLoadMysteryGiftFile.Name);
        AppState.ShowProgressIndicator = true;

        try
        {
            await using var fileStream = browserLoadMysteryGiftFile.OpenReadStream(Constants.MaxFileSize);
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);
            var data = memoryStream.ToArray();
            Logger.LogDebug("Read {ByteCount} bytes from Mystery Gift file", data.Length);

            if (!FileUtil.TryGetMysteryGift(data, out var mysteryGift,
                    Path.GetExtension(browserLoadMysteryGiftFile.Name)))
            {
                Logger.LogError("Failed to load Mystery Gift file: {FileName} - Not a supported format",
                    browserLoadMysteryGiftFile.Name);
                Snackbar.Add("The file is not a supported Mystery Gift file.", Severity.Error);
                return;
            }

            // Import the gift card to the mystery gift album when the save supports it.
            await AppService.ImportMysteryGift(data, Path.GetExtension(browserLoadMysteryGiftFile.Name),
                out var albumImportSuccessful, out var albumImportMessage);

            if (albumImportSuccessful)
            {
                Logger.LogInformation("Mystery Gift card imported to album successfully");
                Snackbar.Add("Mystery Gift card added to Wonder Cards album.", Severity.Success);
            }
            else
            {
                Logger.LogWarning("Mystery Gift album import: {Message}", albumImportMessage);
                Snackbar.Add(albumImportMessage, Severity.Warning);
            }

            // If the gift contains a Pokémon and is compatible with this save, generate it and
            // place it in the active slot. Incompatible cards (wrong generation, etc.) must not
            // produce a PKM even if their IsEntity flag is set.
            if (mysteryGift.IsEntity && mysteryGift.IsCardCompatible(saveFile, out _))
            {
                var originalPkm = mysteryGift.ConvertToPKM(saveFile, EncounterCriteria.Unrestricted);
                var pkm = originalPkm;
                if (pkm.GetType() != saveFile.PKMType)
                {
                    pkm = EntityConverter.ConvertToType(pkm, saveFile.PKMType, out var c);
                    if (!c.IsSuccess || pkm is null)
                    {
                        Logger.LogError("Failed to convert Mystery Gift Pokémon: {ConversionResult}",
                            c.GetDisplayString(originalPkm, saveFile.PKMType));
                        Snackbar.Add("Could not convert the gift Pokémon to the save file's format.", Severity.Error);
                        return;
                    }
                }

                saveFile.AdaptToSaveFile(pkm);

                if (!await EnsureTargetSlotSelectedAsync(saveFile))
                {
                    return;
                }

                AppService.EditFormPokemon = pkm;
                var editedPkm = AppService.EditFormPokemon ?? pkm;
                AppService.SavePokemon(editedPkm);
                Logger.LogInformation("Mystery Gift Pokémon placed in slot successfully");

                var la = new LegalityAnalysis(editedPkm);
                var speciesName = GameInfo.Strings.Species[editedPkm.Species];
                if (la.Valid)
                {
                    Snackbar.Add($"{speciesName} received from Mystery Gift.", Severity.Success);
                }
                else
                {
                    Snackbar.Add(
                        $"{speciesName} received from Mystery Gift, but legality check flagged issues. " +
                        "Review the Pokémon in the editor.",
                        Severity.Warning);
                }

                RefreshService.RequestJumpToPartyBox();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading Mystery Gift file: {FileName}", browserLoadMysteryGiftFile.Name);
            await DialogService.ShowMessageBoxAsync("Error", $"{ex.Message}{Environment.NewLine}{ex.StackTrace}");
        }
        finally
        {
            AppState.ShowProgressIndicator = false;
        }
    }

    /// <summary>
    /// Ensures a target box slot is ready for writing. When a slot is already selected and
    /// occupied, prompts the user to overwrite, use the first available slot, or cancel.
    /// Falls back to the first empty box slot automatically when no slot is selected.
    /// </summary>
    /// <returns>
    /// <see langword="true" /> if a slot is ready and the caller should proceed;
    /// <see langword="false" /> if the caller should abort.
    /// </returns>
    private async Task<bool> EnsureTargetSlotSelectedAsync(SaveFile saveFile)
    {
        var slotType = AppService.GetSelectedPokemonSlot(out _, out _, out _);
        var isLetsGoWithSlot = saveFile is SAV7b && AppState.SelectedBoxSlotNumber.HasValue;
        var hasSelectedSlot = slotType != SelectedPokemonType.None || isLetsGoWithSlot;

        if (hasSelectedSlot)
        {
            if (AppService.EditFormPokemon?.Species == 0)
            {
                return true;
            }

            var occupantName = GameInfo.Strings.Species[AppService.EditFormPokemon!.Species];
            var confirmed = await DialogService.ShowMessageBoxAsync(
                "Overwrite Pokémon?",
                $"The selected slot contains {occupantName}. Overwrite it?",
                yesText: "Overwrite",
                noText: "Use First Available Slot",
                cancelText: "Cancel");
            switch (confirmed)
            {
                case null:
                    return false;
                case false when !AppService.TrySelectFirstEmptyBoxSlot():
                    Logger.LogWarning("No available box slots");
                    Snackbar.Add("No empty box slots available. Free up a slot and try again.", Severity.Warning);
                    return false;
            }
        }
        else if (!AppService.TrySelectFirstEmptyBoxSlot())
        {
            Logger.LogWarning("No available box slots");
            Snackbar.Add("No empty box slots available. Free up a slot and try again.", Severity.Warning);
            return false;
        }

        return true;
    }

    private async Task ExportSelectedPokemon()
    {
        if (AppService.EditFormPokemon is null)
        {
            Logger.LogWarning("Attempted to export Pokémon but no Pokémon is selected");
            return;
        }

        var pkm = AppService.EditFormPokemon;
        Logger.LogInformation("Exporting Pokémon: {Species}", pkm.Species);

        AppState.ShowProgressIndicator = true;

        pkm.RefreshChecksum();
        var cleanFileName = AppService.GetCleanFileName(pkm);
        var data = GetPokemonFileData(pkm);
        Logger.LogDebug("Exporting Pokémon as: {FileName}, Size: {Size} bytes", cleanFileName, data.Length);

        await WriteFile(data, cleanFileName, $".{pkm.Extension}", "Pokémon File");
        Logger.LogInformation("Pokémon exported successfully");

        AppState.ShowProgressIndicator = false;
    }

    private static byte[] GetPokemonFileData(PKM? pokemon) =>
        pokemon is null
            ? []
            : pokemon.DecryptedPartyData;

    private async Task WriteFile(byte[] data, string fileName, string fileTypeExtension, string fileTypeDescription)
    {
        Logger.LogDebug("Writing file: {FileName}, Size: {Size} bytes", fileName, data.Length);

        if (!await FileSystemAccessService.IsSupportedAsync())
        {
            Logger.LogDebug("File System Access API not supported, using legacy method");
            await WriteFileOldWay(data, fileName, fileTypeExtension);
            return;
        }

        try
        {
            await JSRuntime.InvokeVoidAsync(
                "showFilePickerAndWrite",
                fileName,
                data,
                fileTypeExtension,
                fileTypeDescription);
            Logger.LogDebug("File written successfully using File System Access API");
        }
        catch (JSException ex) when (ex.Message.Contains("AbortError", StringComparison.OrdinalIgnoreCase) ||
                                     ex.Message.Contains("aborted a request", StringComparison.OrdinalIgnoreCase))
        {
            // User dismissed the file picker — not an error.
            Logger.LogDebug("File save cancelled by user: {FileName}", fileName);
        }
        catch (JSException ex)
        {
            Logger.LogError(ex, "Error writing file using File System Access API: {FileName}", fileName);
            Snackbar.Add("Export failed. Please try again or use a different browser.", Severity.Error);
        }
    }

    private async Task WriteFileOldWay(byte[] data, string fileName, string fileTypeExtension)
    {
        var finalName = EnsureExtension(fileName, fileTypeExtension);

        var base64String = Convert.ToBase64String(data);

        var element = await JSRuntime.InvokeAsync<IJSObjectReference>(
            "eval",
            "document.createElement('a')");

        // You can keep octet-stream or mirror the JS type.
        await element.InvokeVoidAsync(
            "setAttribute",
            "href",
            $"data:application/x-pokemon-savedata;base64,{base64String}");

        await element.InvokeVoidAsync("setAttribute", "download", finalName);

        // No need for target/rel; we're just triggering a download.
        await element.InvokeVoidAsync("click");
    }

    private enum ThemeMode
    {
        Light,
        System,
        Dark
    }
}
