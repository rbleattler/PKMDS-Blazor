namespace Pkmds.Rcl.Components.EditForms.Tabs;

public partial class OtMiscTab : IDisposable
{
    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    private List<ComboItem>? CachedMemoryItems { get; set; }
    private List<ComboItem>? CachedFeelingItems { get; set; }
    private List<ComboItem>? CachedQualityItems { get; set; }

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
            CachedQualityItems = AppService.GetMemoryQualityComboItems().ToList();
        }
        else
        {
            CachedMemoryItems = null;
            CachedFeelingItems = null;
            CachedQualityItems = null;
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

    // Memory ID → MemoryArgType mapping (based on PKHeX.Core memory text format strings)
    private static readonly MemoryArgType[] MemoryArgTypes =
    [
        // 0-9
        MemoryArgType.None, MemoryArgType.SpecificLocation, MemoryArgType.SpecificLocation, MemoryArgType.GeneralLocation,
        MemoryArgType.GeneralLocation, MemoryArgType.Item, MemoryArgType.None, MemoryArgType.Species,
        MemoryArgType.None, MemoryArgType.Item,
        // 10-19
        MemoryArgType.None, MemoryArgType.None, MemoryArgType.Move, MemoryArgType.Species,
        MemoryArgType.Species, MemoryArgType.Item, MemoryArgType.Item, MemoryArgType.Species,
        MemoryArgType.Species, MemoryArgType.SpecificLocation,
        // 20-29
        MemoryArgType.None, MemoryArgType.Species, MemoryArgType.None, MemoryArgType.None,
        MemoryArgType.SpecificLocation, MemoryArgType.Species, MemoryArgType.Item, MemoryArgType.None,
        MemoryArgType.None, MemoryArgType.Species,
        // 30-39
        MemoryArgType.None, MemoryArgType.GeneralLocation, MemoryArgType.GeneralLocation, MemoryArgType.GeneralLocation,
        MemoryArgType.Item, MemoryArgType.Move, MemoryArgType.Move, MemoryArgType.GeneralLocation,
        MemoryArgType.GeneralLocation, MemoryArgType.GeneralLocation,
        // 40-49
        MemoryArgType.Item, MemoryArgType.None, MemoryArgType.GeneralLocation, MemoryArgType.None,
        MemoryArgType.Species, MemoryArgType.Species, MemoryArgType.None, MemoryArgType.None,
        MemoryArgType.Move, MemoryArgType.Move,
        // 50-59
        MemoryArgType.Species, MemoryArgType.Item, MemoryArgType.Item, MemoryArgType.None,
        MemoryArgType.None, MemoryArgType.None, MemoryArgType.None, MemoryArgType.None,
        MemoryArgType.None, MemoryArgType.GeneralLocation,
        // 60-69
        MemoryArgType.Species, MemoryArgType.None, MemoryArgType.None, MemoryArgType.None,
        MemoryArgType.None, MemoryArgType.None, MemoryArgType.None, MemoryArgType.None,
        MemoryArgType.None, MemoryArgType.None,
        // 70-79
        MemoryArgType.GeneralLocation, MemoryArgType.Species, MemoryArgType.Species, MemoryArgType.None,
        MemoryArgType.None, MemoryArgType.Species, MemoryArgType.None, MemoryArgType.None,
        MemoryArgType.None, MemoryArgType.None,
        // 80-89
        MemoryArgType.Move, MemoryArgType.Move, MemoryArgType.Species, MemoryArgType.Species,
        MemoryArgType.Item, MemoryArgType.None, MemoryArgType.GeneralLocation, MemoryArgType.Species,
        MemoryArgType.Item, MemoryArgType.Move,
    ];

    private static MemoryArgType GetMemoryArgType(byte memoryId) =>
        memoryId < MemoryArgTypes.Length ? MemoryArgTypes[memoryId] : MemoryArgType.None;

    private static string GetMemoryArgLabel(MemoryArgType argType) => argType switch
    {
        MemoryArgType.Species => "Pokémon",
        MemoryArgType.Move => "Move",
        MemoryArgType.Item => "Item",
        MemoryArgType.GeneralLocation or MemoryArgType.SpecificLocation => "Location",
        _ => "Variable",
    };

    /// <summary>
    ///     Builds the fully-formatted memory preview string, substituting all placeholders.
    /// </summary>
    private string FormatMemoryText(
        byte memoryId,
        ushort variable,
        byte intensity,
        byte feeling,
        string trainerName,
        IEnumerable<ComboItem>? memoryItems,
        IEnumerable<ComboItem>? qualityItems,
        IEnumerable<ComboItem>? feelingItems)
    {
        if (memoryItems is null || qualityItems is null || feelingItems is null)
        {
            return string.Empty;
        }

        var template = memoryItems!.FirstOrDefault(m => m.Value == memoryId)?.Text;
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        var argType = GetMemoryArgType(memoryId);
        var variableText = argType != MemoryArgType.None
            ? (AppService!.GetMemoryArgumentComboItems(argType, MemoryGen)
                .FirstOrDefault(i => i.Value == variable)?.Text ?? variable.ToString())
            : string.Empty;

        var qualityText = qualityItems!.FirstOrDefault(q => q.Value == intensity)?.Text ?? string.Empty;
        var feelingText = feelingItems!.FirstOrDefault(f => f.Value == feeling)?.Text ?? string.Empty;
        var pokemonName = Pokemon is not null ? AppService!.GetPokemonSpeciesName(Pokemon.Species) ?? Pokemon.Nickname ?? string.Empty : string.Empty;

        return template
            .Replace("{0}", pokemonName)
            .Replace("{1}", trainerName)
            .Replace("{2}", variableText)
            .Replace("{3}", feelingText)
            .Replace("{4}", qualityText);
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
