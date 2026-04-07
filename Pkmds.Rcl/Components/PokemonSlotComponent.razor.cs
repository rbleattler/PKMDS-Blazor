namespace Pkmds.Rcl.Components;

public partial class PokemonSlotComponent : IDisposable
{
    // Tracks (species, form, formArg, isShiny, isFemale, spriteStyle) tuples whose high-res sprites have loaded
    // at least once this session. Shared across all instances so switching boxes doesn't re-flash.
    // SpriteStyle is included so switching the setting mid-session never shows stale cached state.
    private static readonly
        HashSet<(ushort Species, byte Form, uint FormArg, bool IsShiny, bool IsFemale, SpriteStyle Style)>
        HighResLoadedSpecies = [];

    private bool highResLoaded;
    private bool isDragOverWithExternalFile;
    private byte lastLoadedForm;
    private uint lastLoadedFormArg;
    private bool lastLoadedIsFemale;
    private bool lastLoadedIsShiny;
    private ushort lastLoadedSpecies;
    private SpriteStyle lastLoadedSpriteStyle;

    private bool? legalityValid;

    [Parameter]
    [EditorRequired]
    public int SlotNumber { get; set; }

    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    [Parameter]
    [EditorRequired]
    public EventCallback OnSlotClick { get; set; }

    [Parameter]
    [EditorRequired]
    public Func<string>? GetClassFunction { get; set; }

    [Parameter]
    public bool IsPartySlot { get; set; }

    [Inject]
    private IDragDropService DragDropService { get; set; } = null!;

    protected virtual int? BoxNumber => null;

    public void Dispose() =>
        RefreshService.OnAppStateChanged -= RefreshLegality;

    /// <summary>Clears the session sprite cache, forcing all high-res sprites to reload.</summary>
    public static void ClearSpriteCache() => HighResLoadedSpecies.Clear();

    private async Task HandleClick() =>
        await OnSlotClick.InvokeAsync();

    private string GetClass() =>
        GetClassFunction?.Invoke() ?? string.Empty;

    protected override void OnInitialized()
    {
        RefreshService.OnAppStateChanged += RefreshLegality;
        ComputeLegalityValid();
    }

    protected override void OnParametersSet()
    {
        ComputeLegalityValid();
        UpdateSpriteState();
    }

    // Updates sprite tracking state from current Pokemon + AppState.SpriteStyle.
    // Called from both OnParametersSet (parent-driven re-render) and RefreshLegality
    // (event-driven re-render) so the two paths stay in sync — particularly important
    // when the sprite style changes mid-session via the settings dialog.
    private void UpdateSpriteState()
    {
        var currentIsShiny = Pokemon?.GetIsShinySafe() ?? false;
        var currentForm = Pokemon?.Form ?? 0;
        var currentFormArg = Pokemon?.GetFormArgument(0) ?? 0;
        var currentIsFemale = Pokemon is not null && ImageHelper.HasFemaleHomeSprite(Pokemon.Species, Pokemon.Gender);
        var currentSpriteStyle = AppState.SpriteStyle;
        if (Pokemon?.Species == lastLoadedSpecies
            && currentForm == lastLoadedForm
            && currentFormArg == lastLoadedFormArg
            && currentIsShiny == lastLoadedIsShiny
            && currentIsFemale == lastLoadedIsFemale
            && currentSpriteStyle == lastLoadedSpriteStyle)
        {
            return;
        }

        lastLoadedSpecies = Pokemon?.Species ?? 0;
        lastLoadedForm = currentForm;
        lastLoadedFormArg = currentFormArg;
        lastLoadedIsShiny = currentIsShiny;
        lastLoadedIsFemale = currentIsFemale;
        lastLoadedSpriteStyle = currentSpriteStyle;
        // If this combo has already loaded high-res in this session, skip the bundled sprite entirely.
        highResLoaded = lastLoadedSpecies > 0
                        && HighResLoadedSpecies.Contains((lastLoadedSpecies, lastLoadedForm, lastLoadedFormArg,
                            lastLoadedIsShiny, lastLoadedIsFemale, lastLoadedSpriteStyle));
    }

    // Gen I/II transparent sprites are 40×40 px — scale up to fill the slot.
    // X/Y and OR/AS sprites are tightly cropped 60×60 px — scale down slightly.
    private string GetHiResSizeClass()
    {
        if (AppState.SpriteStyle != SpriteStyle.Game)
        {
            return string.Empty;
        }

        return AppState.SaveFile?.Version switch
        {
            GameVersion.RD or GameVersion.GN or GameVersion.BU
                or GameVersion.RB or GameVersion.RBY or GameVersion.YW
                or GameVersion.GD or GameVersion.GS or GameVersion.SI or GameVersion.C
                => "pkm-sprite-hires--lg",
            GameVersion.X or GameVersion.Y or GameVersion.OR or GameVersion.AS
                => "pkm-sprite-hires--sm",
            _ => string.Empty
        };
    }

    // ReSharper disable once UnusedMember.Local
    private void OnHighResSpriteLoaded()
    {
        highResLoaded = true;
        if (lastLoadedSpecies > 0)
        {
            HighResLoadedSpecies.Add((lastLoadedSpecies, lastLoadedForm, lastLoadedFormArg, lastLoadedIsShiny,
                lastLoadedIsFemale, lastLoadedSpriteStyle));
        }

        StateHasChanged();
    }

    // ReSharper disable once UnusedMember.Local
    private static void OnHighResSpriteError()
    {
        /* keep showing the bundled sprite — _highResLoaded is already false */
    }

    private void RefreshLegality()
    {
        ComputeLegalityValid();
        UpdateSpriteState();
        StateHasChanged();
    }

    private void ComputeLegalityValid()
    {
        if (Pokemon is not { Species: > 0 } || AppState.IsHaXEnabled)
        {
            legalityValid = null;
            return;
        }

        var la = AppService.GetLegalityAnalysis(Pokemon);
        legalityValid = la.Results.All(r => r.Valid)
                        && MoveResult.AllValid(la.Info.Moves)
                        && MoveResult.AllValid(la.Info.Relearn);
    }

    private string GetPokemonTitle() => Pokemon is { Species: > 0 }
        ? AppService.GetPokemonSpeciesName(Pokemon.Species) ?? "Unknown"
        : "Unknown";

    private string GetItemTitle() => Pokemon is { HeldItem: > 0 }
        ? AppService.GetItemComboItem(Pokemon.HeldItem).Text
        : "Unknown";

    private bool IsAlphaPokemon() => Pokemon switch
    {
        IAlpha alpha => alpha.IsAlpha,
        IAlphaReadOnly alphaReadOnly => alphaReadOnly.IsAlpha,
        _ => false
    };

    /// <returns>
    /// <see langword="true" /> = legal, <see langword="false" /> = illegal/fishy,
    /// <see langword="null" /> = no Pokémon in slot (skip indicator).
    /// </returns>
    private bool? GetLegalityValid() => legalityValid;

    private string? GetStatusOverlaySpriteFileName() =>
        ImageHelper.GetStatusOverlaySpriteFileName(Pokemon);

    private int? GetLetsGoPartySlotNumber()
    {
        // Only show party slot indicators for Let's Go games in the BOX view
        // Don't show indicators in the party view itself
        if (AppState.SaveFile is not SAV7b saveFile || Pokemon is null || Pokemon.Species == 0 || IsPartySlot)
        {
            return null;
        }

        // Use PKHeX's GetBoxSlotFlags to determine if this box slot is a party member
        // In Let's Go, party members are stored in the box and GetBoxSlotFlags returns
        // the party slot index via the IsParty() method
        var flags = saveFile.GetBoxSlotFlags(0, SlotNumber); // Let's Go uses box 0
        var partySlot = flags.IsParty();

        if (partySlot >= 0)
        {
            return partySlot + 1; // Convert 0-based to 1-based for display
        }

        return null;
    }

    private bool IsDraggable()
    {
        // Don't allow dragging if no Pokémon
        if (Pokemon is not { Species: > 0 })
        {
            return false;
        }

        // Drag and drop is not supported for Let's Go games
        if (AppState.SaveFile is SAV7b)
        {
            return false;
        }

        // Don't allow dragging the last battle-ready Pokémon in the party
        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (IsPartySlot && IsLastBattleReadyPokemon())
        {
            return false;
        }

        return true;
    }

    private void HandleDragStart(DragEventArgs e)
    {
        if (Pokemon is not { Species: > 0 })
        {
            return;
        }

        DragDropService.StartDrag(Pokemon, BoxNumber, SlotNumber, IsPartySlot);
        e.DataTransfer.EffectAllowed = "copyMove";

        // Set drag-out data so the PKM file can be dragged to the OS desktop (Chrome/Edge only).
        // Uses IJSInProcessRuntime for a synchronous call — required because dataTransfer can only
        // be modified during the synchronous dragstart event handler.
        Pokemon.RefreshChecksum();
        var filename = AppService.GetCleanFileName(Pokemon);
        var base64 = Convert.ToBase64String(Pokemon.DecryptedPartyData);
        if (JSRuntime is IJSInProcessRuntime inProcessRuntime)
        {
            inProcessRuntime.Invoke<bool>("setDragDownloadData", filename, base64);
        }
    }

    private void HandleDragEnd(DragEventArgs e) => DragDropService.ClearDrag();

    private bool IsLastBattleReadyPokemon()
    {
        if (!IsPartySlot || AppState.SaveFile is not { } saveFile)
        {
            return false;
        }

        var battleReadyCount = GetBattleReadyCount(saveFile);

        // This is the last battle-ready Pokémon if there's only 1 and this is it
        return battleReadyCount == 1 && Pokemon is { Species: > 0, IsEgg: false };
    }

    private static int GetBattleReadyCount(SaveFile saveFile)
    {
        // Count battle-ready Pokémon in party (non-Eggs with Species > 0)
        var count = 0;
        for (var i = 0; i < saveFile.PartyCount; i++)
        {
            var partyMon = saveFile.GetPartySlotAtIndex(i);
            if (partyMon is { Species: > 0, IsEgg: false })
            {
                count++;
            }
        }

        return count;
    }

    private void HandleDragEnter(DragEventArgs e)
    {
        // Show visual feedback when an external file is dragged over the slot
        if (DragDropService.IsDragging ||
            !e.DataTransfer.Types.Any(t => t.Equals("Files", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        isDragOverWithExternalFile = true;
        StateHasChanged();
    }

    private void HandleDragLeave(DragEventArgs e)
    {
        if (!isDragOverWithExternalFile)
        {
            return;
        }

        isDragOverWithExternalFile = false;
        StateHasChanged();
    }

    private async Task HandleDrop(DragEventArgs e)
    {
        isDragOverWithExternalFile = false;

        // Check for internal drag first - this takes priority over file drops
        if (DragDropService.IsDragging)
        {
            // Don't drop onto the same slot
            if (DragDropService.IsDragSourceParty == IsPartySlot &&
                DragDropService.DragSourceBoxNumber == BoxNumber &&
                DragDropService.DragSourceSlotNumber == SlotNumber)
            {
                DragDropService.ClearDrag();
                StateHasChanged();
                return;
            }

            // Drag and drop is not supported for Let's Go games
            if (AppState.SaveFile is SAV7b)
            {
                DragDropService.ClearDrag();
                StateHasChanged();
                return;
            }

            // Move the Pokémon
            AppService.MovePokemon(
                DragDropService.DragSourceBoxNumber,
                DragDropService.DragSourceSlotNumber,
                DragDropService.IsDragSourceParty,
                BoxNumber,
                SlotNumber,
                IsPartySlot
            );

            // Clear the selection so the editor panel closes after the move
            AppService.ClearSelection();

            DragDropService.ClearDrag();
            StateHasChanged();
            return;
        }

        // Check if this is a file drop from external source
        // Only process if we're not in an internal drag operation
        if (e.DataTransfer.Files.Length > 0)
        {
            await HandleFileDropAsync(e.DataTransfer.Files);
        }
    }

    private async Task HandleFileDropAsync(string[] fileNames)
    {
        if (AppState.SaveFile is not { } saveFile)
        {
            Snackbar.Add("No save file loaded. Please load a save file first.", Severity.Warning);
            return;
        }

        if (fileNames.Length == 0)
        {
            return;
        }

        var fileName = fileNames[0];

        try
        {
            AppState.ShowProgressIndicator = true;

            // Use JavaScript interop to read the file
            var fileDataBase64 = await JSRuntime.InvokeAsync<string>("readDroppedFile", 0);
            if (string.IsNullOrEmpty(fileDataBase64))
            {
                Snackbar.Add("Failed to read the dropped file.", Severity.Error);
                return;
            }

            var data = Convert.FromBase64String(fileDataBase64);

            // Try to parse as Pokemon file
            if (!FileUtil.TryGetPKM(data, out var pkm, Path.GetExtension(fileName), saveFile))
            {
                Snackbar.Add($"The file '{fileName}' is not a supported Pokémon file.", Severity.Error);
                return;
            }

            var pokemon = pkm.Clone();

            // Convert if needed
            if (pkm.GetType() != saveFile.PKMType)
            {
                pokemon = EntityConverter.ConvertToType(pkm, saveFile.PKMType, out var c);

                // In HaX mode, retry with AllowIncompatibleAll when the normal route fails.
                // This mirrors PKHeX WinForms HaX behaviour and handles cases such as a
                // species that exists only in a DLC region of the target game (e.g. Bibarel
                // dropping into a Violet save before the Teal Mask DLC is detected).
                if ((!c.IsSuccess || pokemon is null) && AppState.IsHaXEnabled)
                {
                    var previous = EntityConverter.AllowIncompatibleConversion;
                    EntityConverter.AllowIncompatibleConversion = EntityCompatibilitySetting.AllowIncompatibleAll;
                    try
                    {
                        pokemon = EntityConverter.ConvertToType(pkm, saveFile.PKMType, out c);
                    }
                    finally
                    {
                        EntityConverter.AllowIncompatibleConversion = previous;
                    }

                    if (c.IsSuccess && pokemon is not null)
                    {
                        Snackbar.Add(
                            $"Warning: {c.GetDisplayString(pkm, saveFile.PKMType)}",
                            Severity.Warning);
                    }
                }

                if (!c.IsSuccess || pokemon is null)
                {
                    Snackbar.Add($"Failed to convert Pokémon: {c.GetDisplayString(pkm, saveFile.PKMType)}",
                        Severity.Error);
                    return;
                }
            }

            saveFile.AdaptToSaveFile(pokemon);

            // Place the Pokemon in the dropped slot
            if (IsPartySlot)
            {
                saveFile.SetPartySlotAtIndex(pokemon, SlotNumber);
                RefreshService.RefreshPartyState();
            }
            else if (BoxNumber.HasValue)
            {
                saveFile.SetBoxSlotAtIndex(pokemon, BoxNumber.Value, SlotNumber);
                RefreshService.RefreshBoxState();
            }
            else // LetsGo storage
            {
                saveFile.SetBoxSlotAtIndex(pokemon, SlotNumber);
                RefreshService.RefreshBoxState();
            }

            Snackbar.Add(
                $"Successfully imported {AppService.GetPokemonSpeciesName(pokemon.Species) ?? "Pokémon"} from {fileName}",
                Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error importing Pokémon: {ex.Message}", Severity.Error);
            // TODO: Add proper logging with ILogger when available
            await Console.Error.WriteLineAsync($"Error in HandleFileDropAsync: {ex}");
        }
        finally
        {
            AppState.ShowProgressIndicator = false;
        }
    }

    private string GetDragClass()
    {
        if (isDragOverWithExternalFile)
        {
            return "slot-file-drop";
        }

        if (!DragDropService.IsDragging)
        {
            return string.Empty;
        }

        // Check if this is the source slot
        if (DragDropService.IsDragSourceParty == IsPartySlot &&
            DragDropService.DragSourceBoxNumber == BoxNumber &&
            DragDropService.DragSourceSlotNumber == SlotNumber)
        {
            return "slot-dragging";
        }

        return string.Empty;
    }
}
