namespace Pkmds.Rcl.Components.EditForms.Tabs;

public partial class OtMiscTab : IDisposable
{
    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    /// <summary>
    ///     Cached list of memory combo items, populated when the Pokémon supports memories (Gen 6+).
    /// </summary>
    private List<ComboItem>? CachedMemoryItems { get; set; }

    /// <summary>
    ///     Cached list of memory feeling combo items, populated when the Pokémon supports memories (Gen 6+).
    /// </summary>
    private List<ComboItem>? CachedFeelingItems { get; set; }

    public void Dispose() =>
        RefreshService.OnAppStateChanged -= StateHasChanged;

    protected override void OnInitialized() =>
        RefreshService.OnAppStateChanged += StateHasChanged;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (Pokemon is IMemoryOT or IMemoryHT)
        {
            CachedMemoryItems = AppService.GetMemoryComboItems().ToList();
            CachedFeelingItems = AppService.GetMemoryFeelingComboItems(MemoryGen).ToList();
        }
        else
        {
            CachedMemoryItems = null;
            CachedFeelingItems = null;
        }
    }

    /// <summary>
    ///     Returns the memory generation (6 or 8) used for feeling/argument lookups.
    ///     Gen 6/7 Pokémon use generation 6 memory sets; Gen 8+ use generation 8 memory sets.
    /// </summary>
    private int MemoryGen => Pokemon?.Context switch
    {
        EntityContext.Gen6 or EntityContext.Gen7 => 6,
        _ => 8,
    };

    /// <summary>
    ///     Returns true when the specified memory ID uses a variable argument ({2} placeholder).
    /// </summary>
    private static bool MemoryHasVariable(byte memoryId, IEnumerable<ComboItem> memoryItems)
    {
        foreach (var item in memoryItems)
        {
            if (item.Value == memoryId)
            {
                return item.Text.Contains("{2}", StringComparison.Ordinal);
            }
        }

        return false;
    }

    private void ClearOtMemory()
    {
        if (Pokemon is not IMemoryOT otMemory)
        {
            return;
        }

        otMemory.OriginalTrainerMemory = 0;
        otMemory.OriginalTrainerMemoryIntensity = 0;
        otMemory.OriginalTrainerMemoryFeeling = 0;
        otMemory.OriginalTrainerMemoryVariable = 0;
    }

    private void ClearHtMemory()
    {
        if (Pokemon is not IMemoryHT htMemory)
        {
            return;
        }

        htMemory.HandlingTrainerMemory = 0;
        htMemory.HandlingTrainerMemoryIntensity = 0;
        htMemory.HandlingTrainerMemoryFeeling = 0;
        htMemory.HandlingTrainerMemoryVariable = 0;
    }

    private void FillFromGame()
    {
        if (Pokemon is null || AppState.SaveFile is not { } saveFile)
        {
            return;
        }

        Pokemon.OriginalTrainerName = saveFile.OT;
        Pokemon.OriginalTrainerGender = saveFile.Gender;

        var format = saveFile.GetTrainerIDFormat();
        switch (format)
        {
            case TrainerIDFormat.SixteenBitSingle: // Gen 1-2
                //Pokemon.SetTrainerID16(saveFile.TID);
                break;
            case TrainerIDFormat.SixteenBit: // Gen 3-6
                Pokemon.TID16 = saveFile.TID16;
                Pokemon.SID16 = saveFile.SID16;
                break;
            case TrainerIDFormat.SixDigit: // Gen 7+
                Pokemon.SetTrainerTID7(saveFile.TrainerTID7);
                Pokemon.SetTrainerSID7(saveFile.TrainerSID7);
                break;
        }
    }

    private void OnGenderToggle(Gender newGender) => Pokemon?.OriginalTrainerGender = (byte)newGender;

    private void SetPokemonOTName(string newOTName)
    {
        if (Pokemon is null || AppState.SaveFile is not { } saveFile)
        {
            return;
        }

        if (newOTName is not { Length: > 0 })
        {
            newOTName = saveFile.OT;
        }

        if (newOTName is not { Length: > 0 })
        {
            return;
        }

        Pokemon.OriginalTrainerName = newOTName;

        // For Gen I/II, verify the OT name was set correctly
        // If it becomes empty, the characters were not valid for the Pokémon's language/encoding
        if (Pokemon.Format <= 2 && string.IsNullOrEmpty(Pokemon.OriginalTrainerName) && newOTName.Length > 0)
        {
            // Fallback to save file's OT name if couldn't be encoded
            Pokemon.OriginalTrainerName = saveFile.OT;
        }
    }

    private void SetPokemonEc(uint newEc)
    {
        if (Pokemon is null)
        {
            return;
        }

        Pokemon.EncryptionConstant = newEc;

        AppService.LoadPokemonStats(Pokemon);
        RefreshService.Refresh();
    }

    private void RandomizeEc()
    {
        if (Pokemon is null)
        {
            return;
        }

        CommonEdits.SetRandomEC(Pokemon);
        SetPokemonEc(Pokemon.EncryptionConstant);
    }

    private void SetPokemonEc(string newEcHex)
    {
        if (Pokemon is null || !uint.TryParse(newEcHex, NumberStyles.HexNumber, null, out var parsedEc))
        {
            return;
        }

        Pokemon.EncryptionConstant = parsedEc;

        AppService.LoadPokemonStats(Pokemon);
        RefreshService.Refresh();
    }

    private void SetPokemonHomeTracker(string newPidHex)
    {
        if (Pokemon is not IHomeTrack homeTrack ||
            !uint.TryParse(newPidHex, NumberStyles.HexNumber, null, out var parsedPid))
        {
            return;
        }

        homeTrack.Tracker = parsedPid;
    }
}
