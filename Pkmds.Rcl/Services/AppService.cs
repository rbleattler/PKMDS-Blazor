namespace Pkmds.Rcl.Services;

public class AppService(IAppState appState, IRefreshService refreshService) : IAppService
{
    private const string EnglishLang = "en";
    private const string DefaultPkmFileName = "pkm.bin";

    private IAppState AppState { get; } = appState;

    private IRefreshService RefreshService { get; } = refreshService;

    private static string[] NatureStatShortNames => ["Atk", "Def", "Spe", "SpA", "SpD"];

    public PKM? EditFormPokemon
    {
        get;
        set
        {
            field = value?.Clone();

            // Skip stat recalculation when the Pokémon already has persistent party
            // stats that we don't want to overwrite:
            // - HaX mode party Pokémon: user may have hand-edited battle stats
            // - PB7 (Let's Go): all storage is unified (SIZE_PARTY == SIZE_STORED),
            //   so party stats including current HP and status condition persist in
            //   the box and should not be reset
            var hasPersistedPartyStats = field is PB7 { PartyStatsPresent: true }
                || (AppState.IsHaXEnabled && AppState.SelectedPartySlotNumber is not null);
            if (!hasPersistedPartyStats)
            {
                LoadPokemonStats(field);
            }
        }
    }

    public bool IsDrawerOpen
    {
        get;
        set
        {
            field = value;
            RefreshService.Refresh();
        }
    }

    public void ToggleDrawer() => IsDrawerOpen = !IsDrawerOpen;

    public void ClearSelection()
    {
        AppState.SelectedBoxNumber = null;
        AppState.SelectedBoxSlotNumber = null;
        AppState.SelectedPartySlotNumber = null;
        EditFormPokemon = null;
        RefreshService.Refresh();
    }

    public void PinBox(int boxNumber)
    {
        AppState.PinnedBoxNumber = boxNumber;
        AppState.SelectedBoxNumber = null;
        AppState.SelectedBoxSlotNumber = null;
        AppState.SelectedPartySlotNumber = null;
        EditFormPokemon = null;
        RefreshService.Refresh();
    }

    public void UnpinBox()
    {
        AppState.PinnedBoxNumber = null;
        RefreshService.Refresh();
    }

    public string GetPokemonSpeciesName(ushort speciesId) => GetSpeciesComboItem(speciesId).Text;

    public IEnumerable<ComboItem> SearchPokemonNames(string searchString) =>
        AppState.SaveFile is null || searchString is not { Length: > 0 }
            ? []
            : GameInfo.FilteredSources.Species
                .DistinctBy(species => species.Value)
                .Where(species => species.Text.Contains(searchString, StringComparison.OrdinalIgnoreCase))
                .OrderBy(species => species.Text);

    public ComboItem GetSpeciesComboItem(ushort speciesId) => GameInfo.FilteredSources.Species
        .DistinctBy(species => species.Value)
        .FirstOrDefault(species => species.Value == speciesId) ?? new(string.Empty, (int)Species.None);

    public IEnumerable<ComboItem> SearchItemNames(string searchString) =>
        AppState.SaveFile is null || searchString is not { Length: > 0 }
            ? []
            : GameInfo.FilteredSources.Items
                .DistinctBy(item => item.Value)
                .Where(item => item.Text.Contains(searchString, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Text);

    public ComboItem GetItemComboItem(int itemId) => GameInfo.FilteredSources.Items
        .DistinctBy(item => item.Value)
        .FirstOrDefault(item => item.Value == itemId) ?? new ComboItem(string.Empty, itemId);

    public string GetStatModifierString(Nature nature)
    {
        var (up, down) = nature.GetNatureModification();
        return up == down
            ? "(neutral)"
            : $"({NatureStatShortNames[up]} ↑, {NatureStatShortNames[down]} ↓)";
    }

    public void LoadPokemonStats(PKM? pokemon)
    {
        if (AppState.SaveFile is null || pokemon is null)
        {
            return;
        }

        var pt = AppState.SaveFile.Personal;
        var pi = pt.GetFormEntry(pokemon.Species, pokemon.Form);
        Span<ushort> stats = stackalloc ushort[6];
        pokemon.LoadStats(pi, stats);

        // Preserve current HP for party Pokémon — SetStats overwrites
        // Stat_HPCurrent with Stat_HPMax, losing any user-set value.
        var previousHp = pokemon.PartyStatsPresent ? pokemon.Stat_HPCurrent : -1;
        pokemon.SetStats(stats);
        if (previousHp >= 0)
        {
            pokemon.Stat_HPCurrent = Math.Min(previousHp, pokemon.Stat_HPMax);
        }
    }

    public IEnumerable<ComboItem> SearchMetLocations(string searchString, GameVersion gameVersion,
        EntityContext entityContext, bool isEggLocation = false) =>
        AppState.SaveFile is null || searchString is not { Length: > 0 }
            ? []
            : GameInfo.GetLocationList(gameVersion, entityContext, isEggLocation)
                .DistinctBy(l => l.Value)
                .Where(metLocation => metLocation.Text.Contains(searchString, StringComparison.OrdinalIgnoreCase))
                .OrderBy(metLocation => metLocation.Text);

    public ComboItem GetMetLocationComboItem(ushort metLocationId, GameVersion gameVersion, EntityContext entityContext,
        bool isEggLocation = false) => AppState.SaveFile is null
        ? null!
        : GameInfo.GetLocationList(gameVersion, entityContext, isEggLocation)
            .DistinctBy(l => l.Value)
            .FirstOrDefault(metLocation => metLocation.Value == metLocationId) ?? null!;

    public IEnumerable<ComboItem> SearchMoves(string searchString) =>
        AppState.SaveFile is null || searchString is not { Length: > 0 }
            ? []
            : GameInfo.FilteredSources.Moves
                .DistinctBy(move => move.Value)
                .Where(move => move.Text.Contains(searchString, StringComparison.OrdinalIgnoreCase))
                .OrderBy(move => move.Text);

    public IEnumerable<ComboItem> GetMoves() => AppState.SaveFile is null
        ? []
        : GameInfo.FilteredSources.Moves
            .DistinctBy(move => move.Value)
            .OrderBy(move => move.Text);

    public ComboItem GetMoveComboItem(int moveId) => GameInfo.FilteredSources.Moves
        .DistinctBy(move => move.Value)
        .FirstOrDefault(metLocation => metLocation.Value == moveId) ?? null!;

    public void SavePokemon(PKM? pokemon)
    {
        if (AppState.SaveFile is null || pokemon is null)
        {
            return;
        }

        var selectedPokemonType = GetSelectedPokemonSlot(out var partySlot, out var boxNumber, out var boxSlot);
        switch (selectedPokemonType)
        {
            case SelectedPokemonType.Party:
                AppState.SaveFile.SetPartySlotAtIndex(pokemon, partySlot);

                // Let's Go games store Pokémon in a unified storage system
                // Changes to party affect box display, so refresh both
                if (AppState.SaveFile is SAV7b)
                {
                    RefreshService.RefreshBoxAndPartyState();
                }
                else
                {
                    RefreshService.RefreshPartyState();
                }

                break;
            case SelectedPokemonType.Box:
                AppState.SaveFile.SetBoxSlotAtIndex(pokemon, boxNumber, boxSlot);
                RefreshService.RefreshBoxState();
                break;
            case SelectedPokemonType.None when AppState.SaveFile is SAV7b:
                // Let's Go unified storage: save to box slot without box number
                AppState.SaveFile.SetBoxSlotAtIndex(pokemon, boxSlot);
                RefreshService.RefreshBoxAndPartyState();
                break;
        }
    }

    public string GetCleanFileName(PKM pkm) => pkm.Context switch
    {
        EntityContext.SplitInvalid or EntityContext.MaxInvalid => DefaultPkmFileName,
        EntityContext.Gen1 or EntityContext.Gen2 => pkm switch
        {
            PK1 pk1 => $"{GameInfo.GetStrings(EnglishLang).Species[pk1.Species]}_{pk1.DV16}.{pk1.Extension}",
            PK2 pk2 => $"{GameInfo.GetStrings(EnglishLang).Species[pk2.Species]}_{pk2.DV16}.{pk2.Extension}",
            _ => DefaultPkmFileName
        },
        _ => $"{GameInfo.GetStrings(EnglishLang).Species[pkm.Species]}_{pkm.PID:X}.{pkm.Extension}"
    };

    public void SetSelectedLetsGoPokemon(PKM? pkm, int slotNumber)
    {
        AppState.SelectedPartySlotNumber = null;

        AppState.SelectedBoxSlotNumber = slotNumber;
        EditFormPokemon = pkm;

        HandleNullOrEmptyPokemon();
        RefreshService.Refresh();
    }

    public void SetSelectedBoxPokemon(PKM? pkm, int boxNumber, int slotNumber)
    {
        AppState.PinnedBoxNumber = null;
        AppState.SelectedPartySlotNumber = null;

        AppState.SelectedBoxNumber = boxNumber;
        AppState.SelectedBoxSlotNumber = slotNumber;
        EditFormPokemon = pkm;

        if (AppState is { SaveFile: { } saveFile, BoxEdit: { } boxEdit })
        {
            boxEdit.LoadBox(boxNumber);
            saveFile.CurrentBox = boxEdit.CurrentBox;
        }

        HandleNullOrEmptyPokemon();
        RefreshService.Refresh();
    }

    public void SetSelectedPartyPokemon(PKM? pkm, int slotNumber)
    {
        AppState.PinnedBoxNumber = null;
        AppState.SelectedBoxNumber = null;
        AppState.SelectedBoxSlotNumber = null;

        AppState.SelectedPartySlotNumber = slotNumber;
        EditFormPokemon = pkm;

        HandleNullOrEmptyPokemon();
        RefreshService.Refresh();
    }

    public void DeletePokemon(int partySlotNumber)
    {
        if (AppState is not { SaveFile: { } saveFile })
        {
            return;
        }

        // Validate party requirements: must keep at least one non-Egg battle-ready Pokémon
        var battleReadyCount = 0;
        for (var i = 0; i < saveFile.PartyCount; i++)
        {
            if (i == partySlotNumber)
            {
                continue; // Skip the one being deleted
            }

            var partyMon = saveFile.GetPartySlotAtIndex(i);
            if (partyMon is { Species: > 0, IsEgg: false })
            {
                battleReadyCount++;
            }
        }

        // Prevent deletion if it would leave no battle-ready Pokémon
        if (battleReadyCount == 0)
        {
            // Cannot delete the last battle-ready Pokémon - silently prevent
            return;
        }

        saveFile.DeletePartySlot(partySlotNumber);

        AppState.SelectedPartySlotNumber = null;

        RefreshService.RefreshPartyState();
    }

    public void DeletePokemon(int boxNumber, int boxSlotNumber)
    {
        if (AppState is not { SaveFile: { } saveFile })
        {
            return;
        }

        saveFile.SetBoxSlotAtIndex(saveFile.BlankPKM, boxNumber, boxSlotNumber);

        AppState.SelectedBoxNumber = null;
        AppState.SelectedBoxSlotNumber = null;

        RefreshService.RefreshBoxState();
    }

    public string ExportPokemonAsShowdown(PKM? pkm) => pkm is null
        ? string.Empty
        : ShowdownParsing.GetShowdownText(pkm);

    public string ExportPartyAsShowdown()
    {
        if (AppState.SaveFile is not { PartyCount: var partyCount } saveFile || partyCount == 0)
        {
            return string.Empty;
        }

        var sbShowdown = new StringBuilder();

        for (var slot = 0; slot < partyCount; slot++)
        {
            var pkm = saveFile.GetPartySlotAtIndex(slot);

            sbShowdown
                .AppendLine(ShowdownParsing.GetShowdownText(pkm))
                .AppendLine();
        }

        return sbShowdown.ToString().Trim();
    }

    public string ExportBoxAsShowdown(int boxNumber)
    {
        if (AppState.SaveFile is not { } sav)
        {
            return string.Empty;
        }

        var sbShowdown = new StringBuilder();

        for (var slot = 0; slot < sav.BoxSlotCount; slot++)
        {
            var pkm = sav.GetBoxSlotAtIndex(boxNumber, slot);
            if (pkm.Species == 0)
            {
                continue;
            }

            sbShowdown
                .AppendLine(ShowdownParsing.GetShowdownText(pkm))
                .AppendLine();
        }

        return sbShowdown.ToString().TrimEnd();
    }

    public IReadOnlyList<ShowdownSet> ParseShowdownText(string text) =>
        [.. ShowdownParsing.GetShowdownSets(text).Where(s => s.Species != 0)];

    public PKM? ConvertShowdownSetToPkm(ShowdownSet set)
    {
        if (AppState.SaveFile is not { } sav || set.Species == 0)
        {
            return null;
        }

        var blank = sav.BlankPKM;
        var destType = blank.GetType();

        // Use EncounterMovesetGenerator to find a legal encounter that supports all of the
        // requested moves. This is the same pattern used by the Living Dex generator and the
        // popular Auto Legality Modifier (ALM) plugin: starting from a valid encounter means
        // the resulting PKM has a correct met location, origin version, and HOME-transfer
        // chain automatically — resolving "Unable to match an encounter from origin game"
        // for species like Basculegion that can only reach SV via HOME from PLA.
        var probe = blank.Clone();
        probe.Species = set.Species;
        probe.Form = set.Form;
        probe.CurrentLevel = set.Level;

        // For Toxtricity, the form is nature-locked: Amped (form 0) or Low-Key (form 1) is
        // determined by the Pokémon's nature. Override the probe form so the generator searches
        // the correct form's encounter pool, ensuring form+nature compatibility.
        if (probe.Species == (int)Species.Toxtricity && set.Nature != Nature.Random)
            probe.Form = (byte)ToxtricityUtil.GetAmpLowKeyResult(set.Nature);

        EncounterMovesetGenerator.OptimizeCriteria(probe, sav);

        // Search ALL game versions (context=0 = no generation filter) sorted newest-first
        // (higher GameVersion enum value = newer game, e.g. SL=50, VL=51 vs X=24, Y=25)
        // so native Gen9 encounters are found before older-gen egg encounters. PLA(47) still
        // comes before SWSH(44,45) ensuring Hisuian-only Pokémon get their PLA encounter.
        var versions = GameUtil.GetVersionsWithinRange(probe)
            .OrderByDescending(v => (int)v)
            .ToArray();

        // Skip egg and mystery-gift encounters: the generator yields them first (Egg=1, Mystery=2
        // have the lowest EncounterTypeGroup values = highest priority) but they're unsuitable for
        // competitive Pokémon imports: eggs produce complex relearn requirements, mystery gifts set
        // FatefulEncounter + event marks that the Pokémon hasn't actually received.
        // Fall back to a gift encounter before an egg if no wild/static/trade is found.
        PKM pkm;
        IEncounterable? foundEnc = null;
        IEncounterable? eggFallback = null;
        IEncounterable? giftFallback = null;
        foreach (var enc in EncounterMovesetGenerator.GenerateEncounters(probe, set.Moves.AsMemory(), versions))
        {
            if (enc is IEncounterEgg) { eggFallback ??= enc; continue; }
            if (enc is MysteryGift) { giftFallback ??= enc; continue; }
            foundEnc = enc;
            break;
        }
        foundEnc ??= giftFallback ?? eggFallback;

        if (foundEnc is not null)
        {
            var generated = foundEnc.ConvertToPKM(sav);
            pkm = EntityConverter.ConvertToType(generated, destType, out _) ?? blank.Clone();
        }
        else
        {
            // No valid encounter found — fall back to a blank template. The import will
            // succeed but may still have legality issues (e.g. unrecognised species).
            pkm = blank.Clone();
        }

        // Overlay the user-specified Showdown details (moves, EVs/IVs, nature, ability,
        // nickname, shiny, tera type, etc.) on top of the legally-generated base.
        pkm.ApplySetDetails(set);

        // Clamp MetLevel BEFORE suggesting relearn moves. ApplySetDetails sets
        // CurrentLevel = set.Level (e.g. 50 for competition format). If the encounter's met
        // level (e.g. 62 for a Noivern wild encounter) exceeds the requested level, clamp it
        // first so that GetSuggestedRelearnMovesFromEncounter can correctly flag moves that are
        // above the (now-lower) met level as needing relearn slots (e.g. Boomburst on Lv.50).
        if (foundEnc is not null && pkm.MetLevel > pkm.CurrentLevel)
            pkm.MetLevel = pkm.CurrentLevel;

        // Clamp ObedienceLevel alongside MetLevel so Gen9 Pokémon don't fail the
        // "obedience level exceeds current level" check after the MetLevel is lowered.
        if (pkm is IObedienceLevel obLevel && obLevel.ObedienceLevel > pkm.CurrentLevel)
            obLevel.ObedienceLevel = pkm.CurrentLevel;

        // Suggest relearn moves now that MetLevel is finalised. With mystery-gift encounters
        // skipped, the remaining encounter types (wild/static/trade/egg) produce reliable
        // suggestions and we no longer need a "clear if still invalid" safety net.
        if (foundEnc is not null && !pkm.FatefulEncounter)
        {
            Span<ushort> suggestedRelearn = stackalloc ushort[4];
            var la = new LegalityAnalysis(pkm);
            la.GetSuggestedRelearnMovesFromEncounter(suggestedRelearn, foundEnc);
            pkm.SetRelearnMoves(suggestedRelearn);
        }

        ApplyPostImportFixes(pkm, sav, foundEnc);
        return pkm;
    }

    private static void ApplyPostImportFixes(PKM pkm, SaveFile sav, IEncounterable? enc = null)
    {
        // Clear held items not available in this save file's game to prevent null refs
        // when rendering the slot component and to avoid legality errors.
        if (pkm.HeldItem != 0 && !sav.HeldItems.Contains((ushort)pkm.HeldItem))
        {
            pkm.HeldItem = 0;
        }

        // Fill in the language so legality checks that key off it (e.g. nickname language)
        // don't fail with "unknown language".
        if (pkm.Language <= 0)
        {
            pkm.Language = sav.Language > 0 ? sav.Language : (int)LanguageID.English;
        }

        // Only suggest a met location when we have no valid encounter (blank fallback path).
        // When enc is not null, ConvertToPKM already set the correct met location and level,
        // and MetLevel was already clamped to CurrentLevel in ConvertShowdownSetToPkm.
        if (enc is null)
        {
            var suggestedMet = EncounterSuggestion.GetSuggestedMetInfo(pkm);
            if (suggestedMet is { Location: not 0 })
            {
                pkm.MetLocation = suggestedMet.Location;
                var metLevel = suggestedMet.GetSuggestedMetLevel(pkm);
                pkm.MetLevel = metLevel;
                if (pkm.CurrentLevel < metLevel)
                    pkm.CurrentLevel = metLevel;
            }
        }

        // Toxtricity's form is determined by nature (Amped vs Low-Key). After ApplySetDetails
        // applies the user's requested nature, ensure the form matches that nature.
        if (pkm.Species == (int)Species.Toxtricity)
            pkm.Form = (byte)ToxtricityUtil.GetAmpLowKeyResult(pkm.Nature);

        // ApplySetDetails already handles relearn moves when the encounter is parsed;
        // we intentionally preserve the moves from the Showdown set rather than calling
        // SetMoveset(), because with an invalid encounter MoveResult.AllValid returns false
        // even for legitimately learnable moves, and SetMoveset()'s random fallback can
        // replace valid user-specified moves with moves the species cannot even learn.

        // If the nickname buffer is empty (which LoadString checks directly against the raw
        // trash bytes, not the IsNicknamed flag), clear it so the verifier sees the species
        // name instead of an empty slot.
        if (string.IsNullOrEmpty(pkm.Nickname))
        {
            pkm.SetDefaultNickname();
        }

        // Showdown format has no height/weight data; use average (0x80) to avoid
        // "improbable height/weight" legality warnings.
        if (pkm is IScaledSize ss && ss.HeightScalar == 0 && ss.WeightScalar == 0)
        {
            ss.HeightScalar = 0x80;
            ss.WeightScalar = 0x80;
        }

        // Fix FormArgument for Pokémon that require a non-zero evolution argument
        // (e.g. Basculegion ≥ 294, Runerigus ≥ 49, Wyrdeer ≥ 20, Annihilape ≥ 20, etc.).
        // When the encounter species differs from the PKM species, the Pokémon was generated
        // from a pre-evolution encounter and GetFormArgumentMinEvolution returns the minimum.
        if (pkm is IFormArgument fa && enc is not null && enc.Species != pkm.Species)
        {
            var minArg = GetFormArgumentMinEvolution(pkm.Species, enc.Species);
            if (fa.FormArgument < minArg)
                fa.FormArgument = minArg;
        }

        // Set a non-zero HOME tracker so the "tracker missing" legality error is suppressed.
        // Also keep Scale == HeightScalar: when HasTracker is true, MiscScaleVerifier requires
        // HeightScalar == Scale for non-PLA encounters.
        // Note: PKHeX comments that fake trackers are not ideal, but for import purposes
        // this avoids the unavoidable "HOME Transfer Tracker is missing" error.
        if (pkm is IHomeTrack { HasTracker: false } ht)
        {
            ht.Tracker = 1;
            if (pkm is IScaledSize3 ss3 && pkm is IScaledSize ss2)
                ss3.Scale = ss2.HeightScalar;
        }

        pkm.RefreshChecksum();
    }

    // Mirrors IFormArgument.GetFormArgumentMinEvolution from the PKHeX.Core source.
    // Inlined here because that method uses C#14 extension syntax unavailable in the consumed package.
    private static uint GetFormArgumentMinEvolution(ushort currentSpecies, ushort originalSpecies) => originalSpecies switch
    {
        (int)Species.Yamask when currentSpecies == (int)Species.Runerigus => 49u,
        (int)Species.Qwilfish when currentSpecies == (int)Species.Overqwil => 20u,
        (int)Species.Stantler when currentSpecies == (int)Species.Wyrdeer => 20u,
        (int)Species.Basculin when currentSpecies == (int)Species.Basculegion => 294u,
        (int)Species.Mankey or (int)Species.Primeape when currentSpecies == (int)Species.Annihilape => 20u,
        (int)Species.Pawniard or (int)Species.Bisharp when currentSpecies == (int)Species.Kingambit => 3u,
        (int)Species.Farfetchd when currentSpecies == (int)Species.Sirfetchd => 3u,
        (int)Species.Gimmighoul when currentSpecies == (int)Species.Gholdengo => 999u,
        _ => 0u,
    };

    public bool TryPlacePokemonInPartySlot(PKM pkm)
    {
        if (AppState.SaveFile is not { } sav || sav.PartyCount >= 6)
        {
            return false;
        }

        sav.SetPartySlotAtIndex(pkm, sav.PartyCount);
        RefreshService.RefreshPartyState();
        return true;
    }

    public int OverwriteParty(IReadOnlyList<PKM> pokemon)
    {
        if (AppState.SaveFile is not { } sav || pokemon.Count == 0)
        {
            return 0;
        }

        var list = pokemon.Take(6).ToList();
        sav.PartyData = list;
        RefreshService.RefreshPartyState();
        return list.Count;
    }

    public string GetIdFormatString(bool isSid = false)
    {
        if (AppState.SaveFile is not { } saveFile)
        {
            return string.Empty;
        }

        var format = saveFile.GetTrainerIDFormat();
        return (format, isSid) switch
        {
            (TrainerIDFormat.SixteenBit, false) => TrainerIDExtensions.TID16,
            (TrainerIDFormat.SixteenBit, true) => TrainerIDExtensions.SID16,
            (TrainerIDFormat.SixDigit, false) => TrainerIDExtensions.TID7,
            (TrainerIDFormat.SixDigit, true) => TrainerIDExtensions.SID7,
            _ => "D"
        };
    }

    public SelectedPokemonType GetSelectedPokemonSlot(out int partySlot, out int boxNumber, out int boxSlot)
    {
        const int defaultValue = -1;

        partySlot = AppState.SelectedPartySlotNumber ?? defaultValue;
        boxNumber = AppState.SelectedBoxNumber ?? defaultValue;
        boxSlot = AppState.SelectedBoxSlotNumber ?? defaultValue;

        return (partySlot, boxNumber, boxSlot) switch
        {
            (not defaultValue, defaultValue, defaultValue) => SelectedPokemonType.Party,
            (defaultValue, not defaultValue, not defaultValue) => SelectedPokemonType.Box,
            _ => SelectedPokemonType.None
        };
    }

    public bool TrySelectFirstEmptyBoxSlot()
    {
        if (AppState.SaveFile is not { } sav)
        {
            return false;
        }

        for (var box = 0; box < sav.BoxCount; box++)
        {
            for (var slot = 0; slot < sav.BoxSlotCount; slot++)
            {
                if (sav.GetBoxSlotAtIndex(box, slot).Species != 0)
                {
                    continue;
                }

                if (sav is SAV7b)
                {
                    // Let's Go uses a flat index across unified storage.
                    SetSelectedLetsGoPokemon(sav.BlankPKM, box * sav.BoxSlotCount + slot);
                }
                else
                {
                    SetSelectedBoxPokemon(sav.BlankPKM, box, slot);
                }

                return true;
            }
        }

        return false;
    }

    public Task ImportMysteryGift(DataMysteryGift gift, out bool isSuccessful, out string resultsMessage)
    {
        try
        {
            if (AppState.SaveFile is not { } saveFile)
            {
                isSuccessful = false;
                resultsMessage = "No save file loaded.";
                return Task.CompletedTask;
            }

            if (!gift.IsCardCompatible(saveFile, out var msg))
            {
                isSuccessful = false;
                resultsMessage = msg;
                return Task.CompletedTask;
            }

            var cards = GetMysteryGiftProvider(saveFile);
            var album = LoadMysteryGifts(saveFile, cards);
            var flags = cards as IMysteryGiftFlags;
            var index = 0;

            var lastUnfilled = GetLastUnfilledByType(gift, album);
            if (lastUnfilled > -1)
            {
                index = lastUnfilled;
            }

            if (gift is PCD { IsLockCapsule: true })
            {
                index = 11;
            }

            var other = album[index];
            if (gift is PCD { CanConvertToPGT: true } pcd && other is PGT)
            {
                gift = pcd.Gift;
            }
            else if (gift.Type != other.Type)
            {
                isSuccessful = false;
                resultsMessage = $"{gift.Type} != {other.Type}";
                return Task.CompletedTask;
            }
            else if (gift is PCD g && g is { IsLockCapsule: true } != (index == 11))
            {
                isSuccessful = false;
                resultsMessage = $"{GameInfo.Strings.Item[533]} slot not valid.";
                return Task.CompletedTask;
            }

            album[index] = gift.Clone();

            List<string> receivedFlags = [];

            SetCardId(gift.CardID, flags, receivedFlags);
            SaveReceivedFlags(flags, receivedFlags);
            SaveReceivedCards(saveFile, cards, album);

            isSuccessful = true;
            resultsMessage = "The Mystery Gift has been successfully imported.";
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            resultsMessage = ex.Message;
            isSuccessful = false;
            return Task.CompletedTask;
        }

        static int GetLastUnfilledByType(DataMysteryGift gift, DataMysteryGift[] album)
        {
            for (var i = 0; i < album.Length; i++)
            {
                var exist = album[i];
                if (!exist.IsEmpty)
                {
                    continue;
                }

                if (exist.Type != gift.Type)
                {
                    continue;
                }

                return i;
            }

            return -1;
        }

        static DataMysteryGift[] LoadMysteryGifts(SaveFile saveFile, IMysteryGiftStorage cards)
        {
            var count = cards.GiftCountMax;
            var size = saveFile is SAV4HGSS
                ? count + 1
                : count;
            var result = new DataMysteryGift[size];
            for (var i = 0; i < count; i++)
            {
                result[i] = cards.GetMysteryGift(i);
            }

            if (saveFile is SAV4HGSS s4)
            {
                result[^1] = s4.LockCapsuleSlot;
            }

            return result;
        }

        static IMysteryGiftStorage GetMysteryGiftProvider(SaveFile saveFile) =>
            saveFile is IMysteryGiftStorageProvider provider
                ? provider.MysteryGiftStorage
                : throw new Exception(
                    $"{SaveFileNameDisplay.FriendlyGameName(saveFile.Version)} does not support Mystery Gifts.");

        static void SetCardId(int cardId, IMysteryGiftFlags? flags, List<string> receivedFlags)
        {
            if (flags is null || (uint)cardId >= flags.MysteryGiftReceivedFlagMax)
            {
                return;
            }

            var card = cardId.ToString("0000");
            if (!receivedFlags.Contains(card))
            {
                receivedFlags.Add(card);
            }
        }

        static void SaveReceivedFlags(IMysteryGiftFlags? flags, List<string> receivedFlags)
        {
            if (flags is null)
            {
                return; // nothing to save
            }

            var count = flags.MysteryGiftReceivedFlagMax;
            for (var i = 1; i < count; i++)
            {
                if (flags.GetMysteryGiftReceivedFlag(i))
                {
                    receivedFlags.Add(i.ToString("0000"));
                }
            }

            // Store the list of set flag indexes back to the bitflag array.
            flags.ClearReceivedFlags();
            foreach (var o in receivedFlags)
            {
                if (!int.TryParse(o, out var index))
                {
                    continue;
                }

                flags.SetMysteryGiftReceivedFlag(index, true);
            }
        }

        static void SaveReceivedCards(SaveFile saveFile, IMysteryGiftStorage cards, DataMysteryGift[] album)
        {
            if (cards is MysteryBlock4 s4)
            {
                // Replace the line causing the error with the following code
                s4.IsDeliveryManActive = album.Any(g => !g.IsEmpty);
                MysteryBlock4.UpdateSlotPGT(album, saveFile is SAV4HGSS);
                if (saveFile is SAV4HGSS hgss)
                {
                    hgss.LockCapsuleSlot = (PCD)album[^1];
                }
            }

            var count = cards.GiftCountMax;
            for (var i = 0; i < count; i++)
            {
                cards.SetMysteryGift(i, album[i]);
            }

            if (cards is MysteryBlock5 s5)
            {
                s5.EndAccess(); // need to encrypt the at-rest data with the seed.
            }
        }
    }

    public Task ImportMysteryGift(byte[] data, string fileExtension, out bool isSuccessful, out string resultsMessage)
    {
        var gift = MysteryGift.GetMysteryGift(data, fileExtension);
        if (gift is not null)
        {
            return ImportMysteryGift(gift, out isSuccessful, out resultsMessage);
        }

        isSuccessful = false;
        resultsMessage = "The Mystery Gift could not be imported.";
        return Task.CompletedTask;
    }

    public void MovePokemon(int? sourceBoxNumber, int sourceSlotNumber, bool isSourceParty,
        int? destBoxNumber, int destSlotNumber, bool isDestParty)
    {
        if (AppState.SaveFile is not { } saveFile)
        {
            return;
        }

        // Validate slot numbers are non-negative
        if (sourceSlotNumber < 0 || destSlotNumber < 0)
        {
            return;
        }

        // Validate party slot bounds
        if (isSourceParty && sourceSlotNumber >= 6)
        {
            return;
        }

        if (isDestParty && destSlotNumber >= 6)
        {
            return;
        }

        // Validate box slot bounds
        if (!isSourceParty && sourceBoxNumber.HasValue && sourceSlotNumber >= saveFile.BoxSlotCount)
        {
            return;
        }

        if (!isDestParty && destBoxNumber.HasValue && destSlotNumber >= saveFile.BoxSlotCount)
        {
            return;
        }

        // Get source Pokémon
        var sourcePokemon = isSourceParty
            ? saveFile.GetPartySlotAtIndex(sourceSlotNumber)
            : sourceBoxNumber.HasValue
                ? saveFile.GetBoxSlotAtIndex(sourceBoxNumber.Value, sourceSlotNumber)
                : saveFile.GetBoxSlotAtIndex(sourceSlotNumber);

        // Get destination Pokémon
        var destPokemon = isDestParty
            ? saveFile.GetPartySlotAtIndex(destSlotNumber)
            : destBoxNumber.HasValue
                ? saveFile.GetBoxSlotAtIndex(destBoxNumber.Value, destSlotNumber)
                : saveFile.GetBoxSlotAtIndex(destSlotNumber);

        // Determine if this is a swap or a move
        var isSwap = sourcePokemon.Species > 0 && destPokemon.Species > 0;

        // Validate party requirements: must keep at least one non-Egg battle-ready Pokémon
        if (isSourceParty && !isDestParty && !isSwap)
        {
            // Moving from party to box (not a swap)
            // Count remaining battle-ready Pokémon after this move
            var battleReadyCount = 0;
            for (var i = 0; i < saveFile.PartyCount; i++)
            {
                if (i == sourceSlotNumber)
                {
                    continue; // Skip the one being moved
                }

                var partyMon = saveFile.GetPartySlotAtIndex(i);
                if (partyMon is { Species: > 0, IsEgg: false })
                {
                    battleReadyCount++;
                }
            }

            // Prevent move if it would leave no battle-ready Pokémon
            if (battleReadyCount == 0)
            {
                // Cannot move the last battle-ready Pokémon - silently prevent
                return;
            }
        }

        switch (isSourceParty)
        {
            // Special handling when moving from party: PKHeX.Core auto-compacts party
            // We need to use DeletePartySlot instead of SetPartySlotAtIndex for proper compacting
            case true when !isDestParty && !isSwap:
                {
                    // Moving FROM party TO box (non-swap case)
                    // Set the box slot first, then delete from party
                    // PKHeX will automatically compact the party
                    if (destBoxNumber.HasValue)
                    {
                        saveFile.SetBoxSlotAtIndex(sourcePokemon, destBoxNumber.Value, destSlotNumber);

                        // Gen 1 and Gen 2 boxes should be compacted like party (they were lists, not grids)
                        if (saveFile.Context is EntityContext.Gen1 or EntityContext.Gen2)
                        {
                            CompactBox(saveFile, destBoxNumber.Value);
                        }
                    }
                    else // LetsGo storage
                    {
                        saveFile.SetBoxSlotAtIndex(sourcePokemon, destSlotNumber);
                    }

                    // Delete from party using DeletePartySlot (properly compacts the party)
                    saveFile.DeletePartySlot(sourceSlotNumber);
                    break;
                }
            case false when isDestParty && !isSwap:
                {
                    // Moving FROM box TO party (non-swap case)
                    // Delete from box first
                    if (sourceBoxNumber.HasValue)
                    {
                        saveFile.SetBoxSlotAtIndex(saveFile.BlankPKM, sourceBoxNumber.Value, sourceSlotNumber);

                        // Gen 1 and Gen 2 boxes should be compacted like party (they were lists, not grids)
                        if (saveFile.Context is EntityContext.Gen1 or EntityContext.Gen2)
                        {
                            CompactBox(saveFile, sourceBoxNumber.Value);
                        }
                    }
                    else // LetsGo storage
                    {
                        saveFile.SetBoxSlotAtIndex(saveFile.BlankPKM, sourceSlotNumber);
                    }

                    // Add to party at the first available empty slot (or the specified slot if within PartyCount)
                    // PKHeX.Core's party is kept compact, so we should add at PartyCount position
                    // unless the user explicitly dropped on an occupied slot (which would be a swap)
                    // or on an empty slot within the current party range
                    var targetSlot = destSlotNumber;
                    if (destSlotNumber >= saveFile.PartyCount)
                    {
                        // User dropped beyond current party - add at end of party (PartyCount position)
                        targetSlot = saveFile.PartyCount;
                    }

                    saveFile.SetPartySlotAtIndex(sourcePokemon, targetSlot);
                    break;
                }
            default:
                {
                    if (isSwap)
                    {
                        // Swap: exchange the two Pokémon
                        // For swaps, we can set both at once since we're not creating/deleting slots
                        if (isSourceParty)
                        {
                            saveFile.SetPartySlotAtIndex(destPokemon, sourceSlotNumber);
                        }
                        else if (sourceBoxNumber.HasValue)
                        {
                            saveFile.SetBoxSlotAtIndex(destPokemon, sourceBoxNumber.Value, sourceSlotNumber);
                        }
                        else // LetsGo storage
                        {
                            saveFile.SetBoxSlotAtIndex(destPokemon, sourceSlotNumber);
                        }

                        if (isDestParty)
                        {
                            saveFile.SetPartySlotAtIndex(sourcePokemon, destSlotNumber);
                        }
                        else if (destBoxNumber.HasValue)
                        {
                            saveFile.SetBoxSlotAtIndex(sourcePokemon, destBoxNumber.Value, destSlotNumber);
                        }
                        else // LetsGo storage
                        {
                            saveFile.SetBoxSlotAtIndex(sourcePokemon, destSlotNumber);
                        }
                    }
                    else
                    {
                        // General case: move between boxes or within same storage
                        // Set destination first
                        if (isDestParty)
                        {
                            saveFile.SetPartySlotAtIndex(sourcePokemon, destSlotNumber);
                        }
                        else if (destBoxNumber.HasValue)
                        {
                            saveFile.SetBoxSlotAtIndex(sourcePokemon, destBoxNumber.Value, destSlotNumber);
                        }
                        else // LetsGo storage
                        {
                            saveFile.SetBoxSlotAtIndex(sourcePokemon, destSlotNumber);
                        }

                        // Then blank out source
                        if (isSourceParty)
                        {
                            // Use DeletePartySlot for proper party compacting
                            saveFile.DeletePartySlot(sourceSlotNumber);
                        }
                        else if (sourceBoxNumber.HasValue)
                        {
                            saveFile.SetBoxSlotAtIndex(saveFile.BlankPKM, sourceBoxNumber.Value, sourceSlotNumber);

                            // Gen 1 and Gen 2 boxes should be compacted like party (they were lists, not grids)
                            if (saveFile.Context is EntityContext.Gen1 or EntityContext.Gen2)
                            {
                                CompactBox(saveFile, sourceBoxNumber.Value);
                            }
                        }
                        else // LetsGo storage
                        {
                            saveFile.SetBoxSlotAtIndex(saveFile.BlankPKM, sourceSlotNumber);
                        }

                        // For Gen 1/2: If we just moved within the same box or moved into a box, compact the destination box too
                        if (saveFile.Context is EntityContext.Gen1 or EntityContext.Gen2 && !isDestParty &&
                            destBoxNumber.HasValue)
                        {
                            // Check if destination box differs from source box, or if we're moving within same box
                            // Either way, compact the destination box to ensure proper list format
                            if (!isSourceParty)
                            {
                                CompactBox(saveFile, destBoxNumber.Value);
                            }
                        }
                    }

                    break;
                }
        }

        // Refresh the UI based on what changed
        if (isSourceParty || isDestParty)
        {
            if (saveFile is SAV7b)
            {
                RefreshService.RefreshBoxAndPartyState();
            }
            else
            {
                RefreshService.RefreshPartyState();
                if (!isSourceParty || !isDestParty)
                {
                    RefreshService.RefreshBoxState();
                }
            }
        }
        else
        {
            RefreshService.RefreshBoxState();
        }
    }

    public IEnumerable<ComboItem> GetMemoryComboItems() =>
        new MemoryStrings(GameInfo.Strings).Memory;

    public IEnumerable<ComboItem> GetMemoryFeelingComboItems(int memoryGen)
    {
        var feelings = new MemoryStrings(GameInfo.Strings).GetMemoryFeelings(memoryGen).ToArray();
        return feelings.Select((f, i) => new ComboItem(f, i));
    }

    public IEnumerable<ComboItem> GetMemoryQualityComboItems()
    {
        var qualities = new MemoryStrings(GameInfo.Strings).GetMemoryQualities().ToArray();
        return qualities.Select((q, i) => new ComboItem(q, i));
    }

    public IEnumerable<ComboItem> GetMemoryArgumentComboItems(MemoryArgType argType, int memoryGen) =>
        new MemoryStrings(GameInfo.Strings).GetArgumentStrings(argType, memoryGen);

    public IEnumerable<ComboItem> GetLanguageComboItems(int generation, EntityContext context) =>
        GameInfo.LanguageDataSource((byte)generation, context);

    public IEnumerable<ComboItem> GetGeoCountryComboItems()
    {
        var items = new List<ComboItem> { new("—", 0) };
        for (var i = 1; i <= 255; i++)
        {
            var name = GeoLocation.GetCountryName("en", (byte)i);
            if (!string.IsNullOrWhiteSpace(name) && name != "INVALID")
            {
                items.Add(new ComboItem(name, i));
            }
        }

        return items;
    }

    public IEnumerable<ComboItem> GetGeoRegionComboItems(byte country)
    {
        if (country == 0)
        {
            return [new ComboItem("—", 0)];
        }

        var items = new List<ComboItem> { new("—", 0) };
        for (var regionId = 1; regionId <= 255; regionId++)
        {
            var name = GeoLocation.GetRegionName("en", country, (byte)regionId);
            if (!string.IsNullOrWhiteSpace(name) && name != "INVALID")
            {
                items.Add(new ComboItem(name, regionId));
            }
        }

        return items;
    }

    public IReadOnlyList<ComboItem> GetConsoleRegionComboItems() =>
        GameInfo.FilteredSources.ConsoleRegions;

    public LegalityAnalysis GetLegalityAnalysis(PKM pkm) => new(pkm);

    public IEnumerable<AdvancedSearchResult> SearchPokemon(AdvancedSearchFilter filter)
    {
        if (AppState.SaveFile is not { } sav)
        {
            yield break;
        }

        // Party slots
        for (var i = 0; i < sav.PartyCount; i++)
        {
            var pkm = sav.GetPartySlotAtIndex(i);
            if (pkm is not { Species: > 0 })
            {
                continue;
            }

            if (Matches(pkm, filter))
            {
                yield return BuildSearchResult(pkm, true, 0, i);
            }
        }

        // Box slots
        for (var box = 0; box < sav.BoxCount; box++)
        {
            for (var slot = 0; slot < sav.BoxSlotCount; slot++)
            {
                var pkm = sav.GetBoxSlotAtIndex(box, slot);
                if (pkm is not { Species: > 0 })
                {
                    continue;
                }

                if (Matches(pkm, filter))
                {
                    yield return BuildSearchResult(pkm, false, box, slot);
                }
            }
        }
    }

    // ── Encounter Database ────────────────────────────────────────────────

    public IEnumerable<EncounterSearchResult> SearchEncounters(EncounterSearchFilter filter)
    {
        if (AppState.SaveFile is not { } sav)
        {
            yield break;
        }

        if (filter.Species is not { } species)
        {
            yield break;
        }

        var blankPkm = sav.BlankPKM.Clone();
        blankPkm.Species = species;
        if (filter.Form is { } form)
        {
            blankPkm.Form = form;
        }

        // When Version is null, pass an empty array so GenerateEncounters searches all
        // versions compatible with the PKM's format internally.
        var versions = filter.Version is { } v
            ? new[] { v }
            : [];

        var encounters =
            EncounterMovesetGenerator.GenerateEncounters(blankPkm, sav, ReadOnlyMemory<ushort>.Empty, versions);

        // Deduplicate by reference: WC8/MysteryGift objects are classes and the generator can
        // return the same instance multiple times (once per compatible game version, e.g. SW and SH).
        var seen = new HashSet<IEncounterable>(ReferenceEqualityComparer.Instance);

        foreach (var enc in encounters)
        {
            if (!seen.Add(enc))
            {
                continue;
            }

            // Level range filter — skip if the encounter's range doesn't overlap the requested range.
            if (filter.LevelMin is { } lmin && enc.LevelMax < lmin)
            {
                continue;
            }

            if (filter.LevelMax is { } lmax && enc.LevelMin > lmax)
            {
                continue;
            }

            // Shiny lock filter.
            if (filter.IsShinyLocked is { } shinyLocked)
            {
                var isLocked = enc.Shiny == Shiny.Never;
                if (isLocked != shinyLocked)
                {
                    continue;
                }
            }

            // Encounter type group filter.
            if (filter.EncounterGroup is { } group && GetEncounterTypeGroup(enc) != group)
            {
                continue;
            }

            yield return BuildEncounterResult(enc);
        }
    }

    public PKM? GeneratePokemonFromEncounter(IEncounterable encounter)
    {
        if (AppState.SaveFile is not { } sav)
        {
            return null;
        }

        // Always return the generated PKM — do not gate on LegalityAnalysis here.
        // Some encounter types (e.g. HOME Mystery Gifts) may not pass a strict legality
        // check even when generated correctly; the caller can run a legality check separately
        // and surface the result to the user.
        var pkm = encounter.ConvertToPKM(sav);

        // Some cross-game encounters (e.g. a Legends: Arceus static returning PA8 when the
        // loaded save is BDSP, which requires PB8) produce a PKM in the wrong format.
        // Attempt a format conversion via EntityConverter; return null if none is possible
        // so the caller can surface a meaningful error instead of crashing.
        var expectedType = sav.BlankPKM.GetType();
        if (pkm.GetType() != expectedType)
        {
            pkm = EntityConverter.ConvertToType(pkm, expectedType, out _);
        }

        // PKHeX's string encoding can silently produce an empty OT name when the save's
        // character encoding doesn't round-trip cleanly (e.g. Japanese Gen 3 saves where
        // StringConverter3 stops encoding at the first unrecognised character).
        // For non-trade encounters the OT should always be the player's own trainer name,
        // so fall back to the save file directly if the generated PKM has no OT.
        if (pkm is null || !string.IsNullOrEmpty(pkm.OriginalTrainerName)
                        || encounter is IFixedTrainer
                        || string.IsNullOrEmpty(sav.OT))
        {
            return pkm;
        }

        pkm.OriginalTrainerName = sav.OT;
        pkm.OriginalTrainerGender = sav.Gender;

        return pkm;
    }

    public bool SwapBoxes(int boxA, int boxB)
    {
        if (AppState.SaveFile is not { } sav)
        {
            return false;
        }

        var success = sav.SwapBox(boxA, boxB);
        if (success)
        {
            RefreshService.RefreshBoxState();
        }

        return success;
    }

    public IReadOnlyList<EvolutionMethod> GetDirectEvolutions(PKM pkm)
    {
        var tree = EvolutionTree.GetEvolutionTree(pkm.Context);
        var methods = tree.Forward.GetForward(pkm.Species, pkm.Form);
        return
        [
            .. methods.Span
                .ToArray()
                .Where(m => m.Species != 0 && m.Method != EvolutionType.LevelUpShedinja)
                .OrderBy(m => m.Species)
        ];
    }

    public bool TryPlacePokemonInFirstAvailableSlot(PKM pkm)
    {
        if (AppState.SaveFile is not { } sav)
        {
            return false;
        }

        // Prefer an open party slot over a box slot.
        if (sav.PartyCount < 6)
        {
            sav.SetPartySlotAtIndex(pkm, sav.PartyCount);
            RefreshService.RefreshPartyState();
            return true;
        }

        // Scan boxes in order for the first empty slot (Species == 0).
        for (var box = 0; box < sav.BoxCount; box++)
        {
            for (var slot = 0; slot < sav.BoxSlotCount; slot++)
            {
                if (sav.GetBoxSlotAtIndex(box, slot).Species != 0)
                {
                    continue;
                }

                sav.SetBoxSlotAtIndex(pkm, box, slot);
                RefreshService.RefreshBoxState();
                return true;
            }
        }

        return false;
    }

    private AdvancedSearchResult BuildSearchResult(PKM pkm, bool isParty, int box, int slot)
    {
        var speciesName = GetPokemonSpeciesName(pkm.Species);
        var location = isParty
            ? $"Party {slot + 1}"
            : $"Box {box + 1}, Slot {slot + 1}";

        return new AdvancedSearchResult
        {
            Pokemon = pkm,
            SpeciesName = speciesName,
            Location = location,
            IsParty = isParty,
            BoxNumber = box,
            SlotNumber = slot
        };
    }

    /// <summary>
    /// Returns <see langword="true" /> when <paramref name="pkm" /> satisfies every
    /// non-null/non-empty criterion in <paramref name="f" />.
    /// Cheap equality checks run first; expensive legality analysis runs last.
    /// </summary>
    private static bool Matches(PKM pkm, AdvancedSearchFilter f)
    {
        // ── Basic ─────────────────────────────────────────────────────────

        if (f.Species.HasValue && pkm.Species != f.Species.Value)
        {
            return false;
        }

        if (f.Form.HasValue && pkm.Form != f.Form.Value)
        {
            return false;
        }

        if (f.IsShiny.HasValue && pkm.IsShiny != f.IsShiny.Value)
        {
            return false;
        }

        if (f.IsEgg.HasValue && pkm.IsEgg != f.IsEgg.Value)
        {
            return false;
        }

        if (f.Gender.HasValue)
        {
            // Issue spec uses -1 for genderless; PKHeX uses 2.
            var filterGender = f.Gender.Value == -1
                ? 2
                : f.Gender.Value;
            if (pkm.Gender != filterGender)
            {
                return false;
            }
        }

        if (f.Nature.HasValue && (byte)pkm.Nature != f.Nature.Value)
        {
            return false;
        }

        if (f.Ability.HasValue && pkm.Ability != f.Ability.Value)
        {
            return false;
        }

        if (f.HeldItem.HasValue && pkm.HeldItem != f.HeldItem.Value)
        {
            return false;
        }

        if (f.Ball.HasValue && pkm.Ball != f.Ball.Value)
        {
            return false;
        }

        if (f.OriginGame.HasValue && pkm.Version != f.OriginGame.Value)
        {
            return false;
        }

        // ── Language ──────────────────────────────────────────────────────

        if (f.LanguageId.HasValue && pkm.Language != f.LanguageId.Value)
        {
            return false;
        }

        // ── Level ─────────────────────────────────────────────────────────

        if (f.LevelMin.HasValue && pkm.CurrentLevel < f.LevelMin.Value)
        {
            return false;
        }

        if (f.LevelMax.HasValue && pkm.CurrentLevel > f.LevelMax.Value)
        {
            return false;
        }

        // ── IVs ───────────────────────────────────────────────────────────

        if (f.HpIvMin.HasValue && pkm.IV_HP < f.HpIvMin.Value)
        {
            return false;
        }

        if (f.AtkIvMin.HasValue && pkm.IV_ATK < f.AtkIvMin.Value)
        {
            return false;
        }

        if (f.DefIvMin.HasValue && pkm.IV_DEF < f.DefIvMin.Value)
        {
            return false;
        }

        if (f.SpaIvMin.HasValue && pkm.IV_SPA < f.SpaIvMin.Value)
        {
            return false;
        }

        if (f.SpdIvMin.HasValue && pkm.IV_SPD < f.SpdIvMin.Value)
        {
            return false;
        }

        if (f.SpeIvMin.HasValue && pkm.IV_SPE < f.SpeIvMin.Value)
        {
            return false;
        }

        // ── EVs ───────────────────────────────────────────────────────────

        if (f.HpEvMin.HasValue && pkm.EV_HP < f.HpEvMin.Value)
        {
            return false;
        }

        if (f.AtkEvMin.HasValue && pkm.EV_ATK < f.AtkEvMin.Value)
        {
            return false;
        }

        if (f.DefEvMin.HasValue && pkm.EV_DEF < f.DefEvMin.Value)
        {
            return false;
        }

        if (f.SpaEvMin.HasValue && pkm.EV_SPA < f.SpaEvMin.Value)
        {
            return false;
        }

        if (f.SpdEvMin.HasValue && pkm.EV_SPD < f.SpdEvMin.Value)
        {
            return false;
        }

        if (f.SpeEvMin.HasValue && pkm.EV_SPE < f.SpeEvMin.Value)
        {
            return false;
        }

        // ── Trainer ───────────────────────────────────────────────────────

        if (f.OriginalTrainerName is { Length: > 0 } otName
            && !pkm.OriginalTrainerName.Contains(otName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (f.TrainerId.HasValue && pkm.TID16 != (ushort)f.TrainerId.Value)
        {
            return false;
        }

        // ── Moves ─────────────────────────────────────────────────────────

        if (f.AnyMoves.Count > 0)
        {
            var m1 = pkm.Move1;
            var m2 = pkm.Move2;
            var m3 = pkm.Move3;
            var m4 = pkm.Move4;
            var anyMatch = f.AnyMoves.Any(m => m == m1 || m == m2 || m == m3 || m == m4);
            if (!anyMatch)
            {
                return false;
            }
        }

        if (f.AllMoves.Count > 0)
        {
            var m1 = pkm.Move1;
            var m2 = pkm.Move2;
            var m3 = pkm.Move3;
            var m4 = pkm.Move4;
            var allMatch = f.AllMoves.All(m => m == m1 || m == m2 || m == m3 || m == m4);
            if (!allMatch)
            {
                return false;
            }
        }

        // ── Hidden Power type ─────────────────────────────────────────────

        if (f.HiddenPowerType.HasValue)
        {
            Span<int> ivs = [pkm.IV_HP, pkm.IV_ATK, pkm.IV_DEF, pkm.IV_SPA, pkm.IV_SPD, pkm.IV_SPE];
            if (HiddenPower.GetType(ivs, pkm.Context) != f.HiddenPowerType.Value)
            {
                return false;
            }
        }

        // ── Ribbons / Marks (reflection — runs before legality) ───────────

        if (f.RequiredRibbons.Count > 0)
        {
            var pkmType = pkm.GetType();
            if (f.RequiredRibbons
                .Select(ribbonName => pkmType.GetProperty(ribbonName))
                .Any(prop => prop?.GetValue(pkm) is not true))
            {
                return false;
            }
        }

        // ── Legality (expensive — evaluated last) ─────────────────────────

        if (!f.IsLegal.HasValue)
        {
            return true;
        }

        var isLegal = new LegalityAnalysis(pkm).Valid;
        return isLegal == f.IsLegal.Value;
    }

    private void HandleNullOrEmptyPokemon()
    {
        if (AppState.SaveFile is not { } saveFile)
        {
            return;
        }

        // Empty box slots in PKHeX return a non-null PKM with Species=0, not null.
        // Apply the template for both cases so new Pokémon start with the correct
        // OT/ID/Language/Version/MetDate/Ball — matching EntityTemplates.TemplateFields.
        if (EditFormPokemon is not (null or { Species: 0 }))
        {
            return;
        }

        // Apply trainer data and other defaults to the blank PKM so new Pokémon
        // start with correct OT/ID/Language/Version/MetDate/Ball — matching what
        // PKHeX WinForms does via EntityTemplates.TemplateFields.
        // Species is reset to 0 afterward so the user is prompted to pick one.
        var blank = saveFile.BlankPKM.Clone();
        EntityTemplates.TemplateFields(blank, saveFile);
        blank.Species = 0;
        blank.ClearNickname(); // Remove the template species name set by TemplateFields
        EditFormPokemon = blank;
    }

    /// <summary>
    /// Compacts a box by shifting all Pokémon left to fill gaps (for Gen 1 and Gen 2 games).
    /// In these generations, boxes were lists, not grids, so they should have no gaps.
    /// </summary>
    private static void CompactBox(SaveFile saveFile, int boxNumber)
    {
        var boxSlotCount = saveFile.BoxSlotCount;
        var compacted = new PKM[boxSlotCount];
        var writeIndex = 0;

        // Collect all non-blank Pokémon
        for (var i = 0; i < boxSlotCount; i++)
        {
            var pkm = saveFile.GetBoxSlotAtIndex(boxNumber, i);
            if (pkm.Species > 0)
            {
                compacted[writeIndex++] = pkm;
            }
        }

        // Fill remaining slots with blank Pokémon
        for (var i = writeIndex; i < boxSlotCount; i++)
        {
            compacted[i] = saveFile.BlankPKM;
        }

        // Write the compacted box back
        for (var i = 0; i < boxSlotCount; i++)
        {
            saveFile.SetBoxSlotAtIndex(compacted[i], boxNumber, i);
        }
    }

    private EncounterSearchResult BuildEncounterResult(IEncounterable enc)
    {
        var speciesName = GetPokemonSpeciesName(enc.Species);

        var gameName = GameInfo.FilteredSources.Games
                           .FirstOrDefault(g => g.Value == (int)enc.Version)?.Text
                       ?? enc.Version.ToString();

        var levelRange = enc.LevelMin == enc.LevelMax
            ? $"Lv. {enc.LevelMin}"
            : $"Lv. {enc.LevelMin}–{enc.LevelMax}";

        var location = GetEncounterLocationName(enc);

        return new EncounterSearchResult
        {
            Encounter = enc,
            SpeciesName = speciesName,
            GameName = gameName,
            EncounterTypeName = GetEncounterTypeName(enc),
            LevelRange = levelRange,
            IsShinyLocked = enc.Shiny == Shiny.Never,
            IsMysteryGift = enc is MysteryGift,
            Location = location
        };
    }

    /// <summary>
    /// Returns the human-readable location name for an encounter, or <see langword="null" />
    /// when no location is associated (e.g., location ID is 0).
    /// </summary>
    private static string? GetEncounterLocationName(IEncounterable enc)
    {
        var locationId = enc.Location != 0
            ? enc.Location
            : enc.EggLocation;
        if (locationId == 0)
        {
            return null;
        }

        var isEgg = locationId != enc.Location;
        return GameInfo.GetLocationName(isEgg, locationId, enc.Generation, enc.Generation, enc.Version);
    }

    /// <summary>
    /// Classifies an <see cref="IEncounterable" /> into one of the five
    /// <see cref="EncounterTypeGroup" /> buckets.
    /// </summary>
    private static EncounterTypeGroup GetEncounterTypeGroup(IEncounterable enc)
    {
        if (enc.IsEgg)
        {
            return EncounterTypeGroup.Egg;
        }

        if (enc is MysteryGift)
        {
            return EncounterTypeGroup.Mystery;
        }

        var name = enc.Name;
        if (name.Contains("Wild", StringComparison.OrdinalIgnoreCase))
        {
            return EncounterTypeGroup.Slot;
        }

        return name.Contains("Trade", StringComparison.OrdinalIgnoreCase)
            ? EncounterTypeGroup.Trade
            : EncounterTypeGroup.Static;
    }

    private static string GetEncounterTypeName(IEncounterable enc) => GetEncounterTypeGroup(enc) switch
    {
        EncounterTypeGroup.Egg => "Egg",
        EncounterTypeGroup.Mystery => "Mystery Gift",
        EncounterTypeGroup.Slot => "Wild",
        EncounterTypeGroup.Trade => "Trade",
        EncounterTypeGroup.Static => "Static",
        _ => "Unknown"
    };
}
