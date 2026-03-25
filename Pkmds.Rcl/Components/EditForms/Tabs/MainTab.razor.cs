namespace Pkmds.Rcl.Components.EditForms.Tabs;

public partial class MainTab : IDisposable
{
    private static readonly DialogOptions AppearanceDialogOptions = new() { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseButton = true, CloseOnEscapeKey = true };

    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    [Parameter]
    public LegalityAnalysis? Analysis { get; set; }

    private MudSelect<byte>? FormSelect { get; set; }

    private bool IsAlcremie => Pokemon?.Species == (ushort)Species.Alcremie;

    private bool IsVivillon =>
        Pokemon?.Species is (ushort)Species.Scatterbug or (ushort)Species.Spewpa or (ushort)Species.Vivillon;

    private bool IsFurfrou => Pokemon?.Species == (ushort)Species.Furfrou;

    private bool IsPumpkabooOrGourgeist =>
        Pokemon?.Species is (ushort)Species.Pumpkaboo or (ushort)Species.Gourgeist;

    private bool IsMinior => Pokemon?.Species == (ushort)Species.Minior;

    private bool IsFlabebebFamily =>
        Pokemon?.Species is (ushort)Species.Flabébé or (ushort)Species.Floette or (ushort)Species.Florges;

    private bool IsSpinda => Pokemon?.Species == (ushort)Species.Spinda;

    public void Dispose() =>
        RefreshService.OnAppStateChanged -= Refresh;

    /// <summary>
    /// Returns the sprite filename for the form-dropdown preview image.
    /// For Scatterbug and Spewpa, substitutes Vivillon's species so the preview
    /// shows the actual wing pattern rather than an identical caterpillar silhouette.
    /// </summary>
    private string GetFormPreviewSprite() => Pokemon is null
        ? ImageHelper.PokemonFallbackImageFileName
        : IsVivillon
            ? ImageHelper.GetPokemonSpriteFilenameForForm((ushort)Species.Vivillon, Pokemon.Context, Pokemon.Form)
            : ImageHelper.GetPokemonSpriteFilename(Pokemon);

    private CheckResult? GetCheckResult(CheckIdentifier identifier)
    {
        if (Analysis is not { } la)
        {
            return null;
        }

        foreach (var r in la.Results)
        {
            if (r.Identifier == identifier && !r.Valid)
            {
                return r;
            }
        }

        return null;
    }

    private string HumanizeCheckResult(CheckResult? result)
    {
        if (result is not { } r || Analysis is not { } la)
        {
            return string.Empty;
        }

        var ctx = LegalityLocalizationContext.Create(la);
        return ctx.Humanize(in r);
    }

    protected override void OnInitialized() =>
        RefreshService.OnAppStateChanged += Refresh;

    private void Refresh()
    {
        FormSelect?.ForceRender(true);
        StateHasChanged();
    }

    private void OnNatureSet(Nature nature)
    {
        if (Pokemon is null)
        {
            return;
        }

        if (!nature.IsFixed)
        {
            nature = 0; // default valid
        }

        switch (Pokemon.Format)
        {
            case 3 or 4:
                Pokemon.SetPIDNature(nature);
                break;
            default:
                Pokemon.Nature = nature;
                break;
        }

        AppService.LoadPokemonStats(Pokemon);
    }

    private void OnStatNatureSet(Nature statNature)
    {
        if (Pokemon is null)
        {
            return;
        }

        Pokemon.StatNature = statNature;
        AppService.LoadPokemonStats(Pokemon);
    }

    private Task<IEnumerable<ComboItem>> SearchPokemonNames(string searchString, CancellationToken token) =>
        Task.FromResult(AppService.SearchPokemonNames(searchString));

    private Task<IEnumerable<ComboItem>> SearchItemNames(string searchString, CancellationToken token) =>
        Task.FromResult(AppService.SearchItemNames(searchString));

    private int GetAbilitySlotIndex() => Pokemon?.AbilityNumber switch
    {
        2 => 1,
        4 => 2,
        _ => 0
    };

    private IReadOnlyList<ComboItem> GetAbilitySlotItems()
    {
        if (Pokemon is null)
        {
            return [];
        }

        var pi = Pokemon.PersonalInfo;
        var names = GameInfo.Strings.Ability;

        static string GetAbilityName(int abilityId, IReadOnlyList<string> names) =>
            abilityId == 0 || (uint)abilityId >= (uint)names.Count
                ? "None"
                : names[abilityId];

        List<ComboItem> items = [];
        for (var i = 0; i < pi.AbilityCount; i++)
        {
            var abilityId = pi.GetAbilityAtIndex(i);
            var suffix = i switch { 0 => "1", 1 => "2", _ => "H" };
            items.Add(new ComboItem($"{GetAbilityName(abilityId, names)} ({suffix})", i));
        }

        return items;
    }

    private void SetAbilitySlot(int slotIndex)
    {
        if (Pokemon is null)
        {
            return;
        }

        Pokemon.SetAbilityIndex(slotIndex);
        AppService.LoadPokemonStats(Pokemon);
        RefreshService.Refresh();
    }

    // ── HaX DEV_Ability helpers (any ability ID, Gen 4+) ─────────────────────

    private ComboItem GetDevAbilityComboItem()
    {
        if (Pokemon is null)
        {
            return new ComboItem("None", 0);
        }

        var names = GameInfo.Strings.Ability;
        var id = Pokemon.Ability;
        var name = id == 0
            ? "None"
            : (uint)id >= (uint)names.Count
                ? $"(Ability #{id})"
                : names[id];
        return new ComboItem(name, id);
    }

    private Task<IEnumerable<ComboItem>> SearchAllAbilities(string searchString, CancellationToken _)
    {
        var names = GameInfo.Strings.Ability;
        IEnumerable<ComboItem> results;
        if (string.IsNullOrWhiteSpace(searchString))
        {
            results = Enumerable.Empty<ComboItem>();
        }
        else
        {
            results = names
                .Select((name, i) => new ComboItem(name, i))
                .Where(item => !string.IsNullOrEmpty(item.Text) &&
                               item.Text.Contains(searchString, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult(results);
    }

    private void SetDevAbility(ComboItem? item)
    {
        if (Pokemon is null || item is null || AppState?.IsHaXEnabled is not true)
        {
            return;
        }

        Pokemon.Ability = (ushort)item.Value;
        RefreshService.Refresh();
    }

    private static IReadOnlyList<ComboItem> GetAbilityNumberItems() =>
    [
        new("Slot 1", 1),
        new("Slot 2", 2),
        new("Slot H", 4)
    ];

    private void SetDevAbilityNumber(int abilityNumber)
    {
        if (Pokemon is null || AppState?.IsHaXEnabled is not true)
        {
            return;
        }

        Pokemon.AbilityNumber = abilityNumber;
        RefreshService.Refresh();
    }

    private void OnShinySet(bool shiny) => Pokemon?.SetIsShinySafe(shiny);

    private void OnGenderToggle(Gender newGender)
    {
        if (Pokemon is not { PersonalInfo.IsDualGender: true } pkm)
        {
            return;
        }

        pkm.SetGender((byte)newGender);
    }

    private void RevertNickname()
    {
        if (Pokemon is null)
        {
            return;
        }

        Pokemon.IsNicknamed = false;
        Pokemon.ClearNickname();
    }

    private void AfterFormChanged()
    {
        if (Pokemon is { Species: (ushort)Species.Indeedee })
        {
            Pokemon.SetGender(Pokemon.Form);
        }

        // For Furfrou, auto-set days remaining to the maximum when switching to a trim form via the
        // dropdown, so the form argument is immediately valid (a 0-day trim reverts to Natural).
        if (Pokemon is { Species: (ushort)Species.Furfrou, Form: not 0 })
        {
            var maxDays = FormArgumentUtil.GetFormArgumentMax(Pokemon.Species, Pokemon.Form, Pokemon.Context);
            Pokemon.ChangeFormArgument(maxDays);
        }

        AppService.LoadPokemonStats(Pokemon);
        RefreshService.Refresh();
    }

    private void SetPokemonPid(uint newPid)
    {
        if (Pokemon is null)
        {
            return;
        }

        Pokemon.PID = newPid;

        AppService.LoadPokemonStats(Pokemon);
        RefreshService.Refresh();
    }

    private async Task OpenAlcremieEditorDialog()
    {
        var parameters = new DialogParameters<AlcremieEditorDialog> { { x => x.Pokemon, Pokemon } };
        var dialog = await DialogService.ShowAsync<AlcremieEditorDialog>("Alcremie Appearance", parameters, AppearanceDialogOptions);
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            AppService.LoadPokemonStats(Pokemon);
            RefreshService.Refresh();
        }
    }

    private async Task OpenVivillonEditorDialog()
    {
        var parameters = new DialogParameters<VivillonEditorDialog> { { x => x.Pokemon, Pokemon } };
        var title = Pokemon?.Species switch
        {
            (ushort)Species.Scatterbug => "Scatterbug Pattern",
            (ushort)Species.Spewpa => "Spewpa Pattern",
            _ => "Vivillon Pattern"
        };
        var dialog = await DialogService.ShowAsync<VivillonEditorDialog>(title, parameters, AppearanceDialogOptions);
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            AppService.LoadPokemonStats(Pokemon);
            RefreshService.Refresh();
        }
    }

    private async Task OpenFurfrouEditorDialog()
    {
        var parameters = new DialogParameters<FurfrouEditorDialog> { { x => x.Pokemon, Pokemon } };
        var dialog = await DialogService.ShowAsync<FurfrouEditorDialog>("Furfrou Trim", parameters, AppearanceDialogOptions);
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            AppService.LoadPokemonStats(Pokemon);
            RefreshService.Refresh();
        }
    }

    private async Task OpenPumpkabooSizeDialog()
    {
        var parameters = new DialogParameters<PumpkabooSizeDialog> { { x => x.Pokemon, Pokemon } };
        var title = Pokemon?.Species == (ushort)Species.Gourgeist
            ? "Gourgeist Size"
            : "Pumpkaboo Size";
        var dialog = await DialogService.ShowAsync<PumpkabooSizeDialog>(title, parameters, AppearanceDialogOptions);
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            AppService.LoadPokemonStats(Pokemon);
            RefreshService.Refresh();
        }
    }

    private async Task OpenMiniorColorDialog()
    {
        var parameters = new DialogParameters<MiniorColorDialog> { { x => x.Pokemon, Pokemon } };
        var dialog = await DialogService.ShowAsync<MiniorColorDialog>("Minior Form", parameters, AppearanceDialogOptions);
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            AppService.LoadPokemonStats(Pokemon);
            RefreshService.Refresh();
        }
    }

    private async Task OpenFlowerColorDialog()
    {
        var parameters = new DialogParameters<FlowerColorDialog> { { x => x.Pokemon, Pokemon } };
        var title = Pokemon?.Species switch
        {
            (ushort)Species.Floette => "Floette Flower Color",
            (ushort)Species.Florges => "Florges Flower Color",
            _ => "Flabébé Flower Color"
        };
        var dialog = await DialogService.ShowAsync<FlowerColorDialog>(title, parameters, AppearanceDialogOptions);
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            AppService.LoadPokemonStats(Pokemon);
            RefreshService.Refresh();
        }
    }

    private async Task OpenPidEcDialog()
    {
        var parameters = new DialogParameters<PidEcDialog> { { x => x.Pokemon, Pokemon } };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true, CloseOnEscapeKey = true };

        await DialogService.ShowAsync<PidEcDialog>("PID / EC Generator", parameters, options);
    }

    private async Task OpenSpindaPatternDialog()
    {
        var parameters = new DialogParameters<SpindaPatternDialog> { { x => x.Pokemon, Pokemon } };
        var dialog = await DialogService.ShowAsync<SpindaPatternDialog>("Spinda Spot Pattern", parameters, AppearanceDialogOptions);
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            AppService.LoadPokemonStats(Pokemon);
            RefreshService.Refresh();
        }
    }

    private Task OpenTrashBytesEditor(StringSource field) =>
        OpenTrashBytesEditorAsync(Pokemon, field);

    private void SetPokemonPid(string newPidHex)
    {
        if (Pokemon is null || !uint.TryParse(newPidHex, NumberStyles.HexNumber, null, out var parsedPid))
        {
            return;
        }

        Pokemon.PID = parsedPid;

        AppService.LoadPokemonStats(Pokemon);
        RefreshService.Refresh();
    }

    // ReSharper disable once InconsistentNaming
    private double GetEXPToLevelUp()
    {
        if (Pokemon is not { CurrentLevel: var level and < 100, EXP: var exp, PersonalInfo.EXPGrowth: var growth })
        {
            return 0;
        }

        var table = Experience.GetTable(growth);
        var next = Experience.GetEXP(++level, table);
        return next - exp;
    }

    private void SetPokemonNickname(string newNickname)
    {
        if (Pokemon is not { Species: var species, Language: var language, Format: var format })
        {
            return;
        }

        var defaultName = SpeciesName.GetSpeciesNameGeneration(species, language, format);

        if (newNickname is not { Length: > 0 })
        {
            newNickname = defaultName;
        }

        if (newNickname is not { Length: > 0 })
        {
            return;
        }

        Pokemon.IsNicknamed = !string.Equals(newNickname, defaultName, StringComparison.Ordinal);
        Pokemon.Nickname = newNickname;

        // For Gen I/II, verify the nickname was set correctly
        // If it becomes empty, the characters were not valid for the Pokémon's language/encoding
        if (Pokemon.Format > 2 || !string.IsNullOrEmpty(Pokemon.Nickname))
        {
            return;
        }

        // Fallback to default name if nickname couldn't be encoded
        Pokemon.Nickname = defaultName;
        Pokemon.IsNicknamed = false;
    }

    private bool CanEvolve =>
        Pokemon is { IsEgg: false } &&
        AppService.GetDirectEvolutions(Pokemon).Count > 0;

    private async Task EvolveAsync()
    {
        if (Pokemon is null)
        {
            return;
        }

        var choices = AppService.GetDirectEvolutions(Pokemon);
        if (choices.Count == 0)
        {
            return;
        }

        EvolutionMethod chosen;
        if (choices.Count == 1)
        {
            chosen = choices[0];
        }
        else
        {
            var parameters = new DialogParameters<EvolvePickerDialog>
            {
                { x => x.Choices, choices },
                { x => x.Pokemon, Pokemon },
            };
            var dialog = await DialogService.ShowAsync<EvolvePickerDialog>("Choose Evolution", parameters,
                new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true, CloseOnEscapeKey = true });
            var result = await dialog.Result;
            if (result is null or { Canceled: true })
            {
                return;
            }

            chosen = (EvolutionMethod)result.Data!;
        }

        // Capture Nincada snapshot before applying (Shedinja side-effect).
        var isNincada = Pokemon.Species == (ushort)Species.Nincada && chosen.Species == (ushort)Species.Ninjask;
        var nincadaSnapshot = isNincada ? Pokemon.Clone() : null;

        ApplyEvolution(chosen);

        if (isNincada && nincadaSnapshot is not null)
        {
            await OfferShedinjaAsync(nincadaSnapshot);
        }
    }

    private static byte? GetRequiredGender(EvolutionType method) => method switch
    {
        EvolutionType.LevelUpMale or EvolutionType.UseItemMale => 0,
        EvolutionType.LevelUpFemale or EvolutionType.UseItemFemale => 1,
        _ => null,
    };

    private void ApplyEvolution(EvolutionMethod method)
    {
        if (Pokemon is null)
        {
            return;
        }

        var destForm = method.GetDestinationForm(Pokemon.Form);

        // Wurmple: match EC/PID to the chosen branch so legality is satisfied.
        if (Pokemon.Species == (ushort)Species.Wurmple)
        {
            var evoGroup = WurmpleUtil.GetWurmpleEvoGroup(method.Species);
            if (Pokemon.Format >= 6)
            {
                // Gen 6+: EC is an independent field — set it to match the branch.
                Pokemon.EncryptionConstant = WurmpleUtil.GetWurmpleEncryptionConstant(evoGroup);
            }
            else
            {
                // Gen 3–5: EC getter returns PID; the EC setter is a no-op, so we must set PID.
                // Note: changing PID may introduce legality flags (gender/nature/ability correlation),
                // which is acceptable in a save editor context.
                uint pid;
                var rnd = Util.Rand;
                do pid = rnd.Rand32();
                while (evoGroup != WurmpleUtil.GetWurmpleEvoVal(pid));
                Pokemon.PID = pid;
            }
        }

        // Gender-locked evolutions (e.g. Kirlia→Gallade requires male, Combee→Vespiquen requires female).
        // For Gen 3–5, gender is derived from PID, so we must regenerate a PID that satisfies both
        // the required gender and preserves the existing nature/ability correlation where possible.
        var requiredGender = GetRequiredGender(method.Method);
        if (requiredGender is { } targetGender && Pokemon.Gender != targetGender)
        {
            // SetPIDGender re-rolls PID (preserving nature/ability/non-shiny) for Gen ≤ 5,
            // and also updates EC when the PKM originated in Gen 3–5 but is stored in Gen 6+.
            Pokemon.SetPIDGender(targetGender);
            Pokemon.Gender = targetGender;
        }

        // Bump level to the minimum required for this evolution.
        if (method.Level > 0 && Pokemon.CurrentLevel < method.Level)
        {
            Pokemon.CurrentLevel = method.Level;
        }

        // For level-up evolutions the legality check requires current level > met level
        // (specifically: current level ≥ met level + 1).
        // Set directly to MetLevel + 1 so this holds even if CurrentLevel is well below MetLevel.
        // Level 100 Pokémon are exempt — PKHeX allows level-up evolutions at max level.
        if (method.Method.IsLevelUpRequired
            && Pokemon.CurrentLevel <= Pokemon.MetLevel
            && Pokemon.CurrentLevel < Experience.MaxLevel)
        {
            Pokemon.CurrentLevel = (byte)Math.Min(Experience.MaxLevel, Pokemon.MetLevel + 1);
        }

        // Capture before changing species: Gen 3 computes IsNicknamed from Nickname vs. species name,
        // so reading it after the species change gives the wrong answer.
        var wasNicknamed = Pokemon.IsNicknamed;

        Pokemon.Species = method.Species;
        Pokemon.Form = destForm;
        Pokemon.Gender = Pokemon.GetSaneGender();

        if (!wasNicknamed)
        {
            Pokemon.ClearNickname();
        }

        AppService.LoadPokemonStats(Pokemon);
        RefreshService.Refresh();
    }

    private void SetSpecies(ushort species)
    {
        if (Pokemon is null)
        {
            return;
        }

        Pokemon.Species = species;

        // AbilityNumber 0 is invalid for every encounter. SetAbilityIndex sets
        // both the Ability ID and AbilityNumber together, which is required —
        // setting AbilityNumber alone leaves Ability at 0 (AbilityUnexpected).
        if (Pokemon.AbilityNumber is not (1 or 2 or 4))
        {
            Pokemon.SetAbilityIndex(0);
        }

        // Ensure gender is valid for the new species.
        Pokemon.Gender = Pokemon.GetSaneGender();

        // Keep the nickname in sync when it is not manually set.
        if (!Pokemon.IsNicknamed)
        {
            Pokemon.ClearNickname();
        }

        // EC = 0 causes a legality error (PIDEqualsEC when PID is also 0).
        // Generate a random EC on first species assignment so the Pokémon is
        // not flagged before the user has had a chance to fix anything.
        if (Pokemon.EncryptionConstant == 0)
        {
            Pokemon.SetRandomEC();
        }

        AppService.LoadPokemonStats(Pokemon);
        RefreshService.Refresh();
    }

    /// <summary>
    /// When Nincada evolves into Ninjask, offers to generate a Shedinja and place it in
    /// the first available party or box slot, mirroring the in-game mechanic.
    /// </summary>
    private async Task OfferShedinjaAsync(PKM nincadaSnapshot)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "Generate Shedinja?",
            "Nincada has evolved into Ninjask. In the games, a Shedinja also appears in the next available slot. Would you like to generate one?",
            yesText: "Generate Shedinja",
            noText: "Skip");

        if (confirmed is not true)
        {
            return;
        }

        var shedinja = nincadaSnapshot;
        shedinja.Species = (ushort)Species.Shedinja;

        // If the Nincada wasn't nicknamed, update the cached nickname to "Shedinja".
        if (!shedinja.IsNicknamed)
        {
            shedinja.ClearNickname();
        }

        AppService.LoadPokemonStats(shedinja);

        if (!AppService.TryPlacePokemonInFirstAvailableSlot(shedinja))
        {
            Snackbar.Add("No empty slot available for Shedinja.", Severity.Warning);
            return;
        }

        Snackbar.Add("Shedinja placed in the first available slot.", Severity.Success);
    }
}
