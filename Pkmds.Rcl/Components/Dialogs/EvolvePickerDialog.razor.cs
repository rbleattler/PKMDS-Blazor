namespace Pkmds.Rcl.Components.Dialogs;

public partial class EvolvePickerDialog
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<EvolutionMethod> Choices { get; set; } = [];

    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    private EvolutionMethod _selected;

    private readonly HashSet<ushort> _failedSprites = [];

    protected override void OnParametersSet()
    {
        // Pre-select the first choice for convenience.
        if (Choices.Count > 0)
        {
            _selected = Choices[0];
        }
    }

    private string GetSpeciesName(ushort species) =>
        species < GameInfo.Strings.specieslist.Length
            ? GameInfo.Strings.specieslist[species]
            : species.ToString();

    private string GetSprite(EvolutionMethod method)
    {
        if (Pokemon is null)
        {
            return ImageHelper.PokemonFallbackImageFileName;
        }

        // If the sprite previously failed, fall back to form 0 (base form always exists).
        if (_failedSprites.Contains(method.Species))
        {
            return ImageHelper.GetPokemonSpriteFilenameForForm(method.Species, Pokemon.Context, 0);
        }

        var destForm = method.GetDestinationForm(Pokemon.Form);
        return ImageHelper.GetPokemonSpriteFilenameForForm(method.Species, Pokemon.Context, destForm);
    }

    private void OnSpriteError(EvolutionMethod method)
    {
        if (_failedSprites.Add(method.Species))
        {
            StateHasChanged();
        }
    }

    private string DescribeMethod(EvolutionMethod method)
    {
        var level = method.Level;
        var arg = method.Argument;

        return method.Method switch
        {
            EvolutionType.LevelUp => GetLevelUpDescription(method),
            EvolutionType.LevelUpNinjask => $"Level {level}",
            EvolutionType.LevelUpFriendship => "Level up (high friendship)",
            EvolutionType.LevelUpFriendshipMorning => "Level up (high friendship, morning)",
            EvolutionType.LevelUpFriendshipNight => "Level up (high friendship, night)",
            EvolutionType.LevelUpATK => $"Level {level} (ATK > DEF)",
            EvolutionType.LevelUpAeqD => $"Level {level} (ATK = DEF)",
            EvolutionType.LevelUpDEF => $"Level {level} (DEF > ATK)",
            EvolutionType.LevelUpBeauty => "Level up (high beauty)",
            EvolutionType.LevelUpECl5 or EvolutionType.LevelUpECgeq5 => "Level up (chance)",
            EvolutionType.LevelUpMale => $"Level {level} (male)",
            EvolutionType.LevelUpFemale => $"Level {level} (female)",
            EvolutionType.LevelUpElectric => "Level up (magnetic field)",
            EvolutionType.LevelUpForest => "Level up (mossy rock)",
            EvolutionType.LevelUpCold => "Level up (icy rock)",
            EvolutionType.LevelUpInverted => "Level up (inverted battle)",
            EvolutionType.LevelUpMorning => $"Level {level} (morning)",
            EvolutionType.LevelUpNight => $"Level {level} (night)",
            EvolutionType.LevelUpDusk => $"Level {level} (dusk)",
            EvolutionType.LevelUpSummit => "Level up (summit)",
            EvolutionType.LevelUpWormhole => "Level up (wormhole)",
            EvolutionType.LevelUpVersion or EvolutionType.LevelUpVersionDay or EvolutionType.LevelUpVersionNight =>
                $"Level {level} (version-specific)",
            EvolutionType.LevelUpHeldItemDay => $"Level up holding {GetItemName(arg)} (day)",
            EvolutionType.LevelUpHeldItemNight => $"Level up holding {GetItemName(arg)} (night)",
            EvolutionType.LevelUpKnowMove or EvolutionType.LevelUpKnowMoveECElse or EvolutionType.LevelUpKnowMoveEC100 =>
                $"Level up knowing {GetMoveName(arg)}",
            EvolutionType.LevelUpWithTeammate => $"Level up with {GetSpeciesName(arg)}",
            EvolutionType.LevelUpAffection50MoveType or EvolutionType.LevelUpMoveType => "Level up (move type condition)",
            EvolutionType.LevelUpWeather => "Level up (weather)",
            EvolutionType.LevelUpFormFemale1 => "Level up (female form)",
            EvolutionType.LevelUpNatureAmped => "Level up (Amped nature)",
            EvolutionType.LevelUpNatureLowKey => "Level up (Low Key nature)",
            EvolutionType.LevelUpUnionCircle => "Level up (Union Circle)",
            EvolutionType.LevelUpInBattleEC100 or EvolutionType.LevelUpInBattleECElse => "Level up (in-battle condition)",
            EvolutionType.LevelUpCollect999 => "Collect 999 Gimmighoul Coins",
            EvolutionType.LevelUpDefeatEquals => $"Level up (defeat Pawniard)",
            EvolutionType.LevelUpUseMoveSpecial => "Level up (use Rage Fist 20 times)",
            EvolutionType.LevelUpRecoilDamageMale or EvolutionType.LevelUpRecoilDamageFemale => "Level up (recoil damage)",
            EvolutionType.LevelUpWalkStepsWith => "Level up (walk 1000 steps)",
            EvolutionType.UseItem => $"Use {GetItemName(arg)}",
            EvolutionType.UseItemMale => $"Use {GetItemName(arg)} (male)",
            EvolutionType.UseItemFemale => $"Use {GetItemName(arg)} (female)",
            EvolutionType.UseItemWormhole => $"Use {GetItemName(arg)} (wormhole)",
            EvolutionType.Trade => GetTradeDescription(method),
            EvolutionType.TradeHeldItem => $"Trade holding {GetItemName(arg)}",
            EvolutionType.TradeShelmetKarrablast => "Trade with Shelmet / Karrablast",
            EvolutionType.CriticalHitsInBattle => "Land 3 critical hits in one battle",
            EvolutionType.HitPointsLostInBattle => "Lose 49+ HP in one battle",
            EvolutionType.Spin => "Spin",
            EvolutionType.TowerOfDarkness => "Train at Tower of Darkness",
            EvolutionType.TowerOfWaters => "Train at Tower of Waters",
            _ => method.Method.ToString()
        };
    }

    private string GetLevelUpDescription(EvolutionMethod method)
    {
        // Gen 2 stores stat-conditioned and friendship+time-of-day evolutions as plain LevelUp
        // since the binary format doesn't encode these conditions.
        // Cross-reference Gen 4 to surface the actual requirement (e.g. Tyrogue's stat branches,
        // Espeon's morning friendship, Umbreon's night friendship).
        if (Pokemon is not null)
        {
            var modernTree = EvolutionTree.GetEvolutionTree(EntityContext.Gen4);
            var modernMethods = modernTree.Forward.GetForward(Pokemon.Species, Pokemon.Form);
            foreach (var m in modernMethods.Span)
            {
                if (m.Species == method.Species)
                {
                    return m.Method switch
                    {
                        EvolutionType.LevelUpFriendship => "Level up (high friendship)",
                        EvolutionType.LevelUpFriendshipMorning => "Level up (high friendship, morning)",
                        EvolutionType.LevelUpFriendshipNight => "Level up (high friendship, night)",
                        EvolutionType.LevelUpATK => $"Level {method.Level} (ATK > DEF)",
                        EvolutionType.LevelUpAeqD => $"Level {method.Level} (ATK = DEF)",
                        EvolutionType.LevelUpDEF => $"Level {method.Level} (DEF > ATK)",
                        _ => method.Level == 0 ? "Level up" : $"Level {method.Level}",
                    };
                }
            }
        }
        return method.Level == 0 ? "Level up" : $"Level {method.Level}";
    }

    private string GetTradeDescription(EvolutionMethod method)
    {
        // Gen 2 stores all trade evolutions as plain Trade (type 5) with Argument=0,
        // because the Gen 2 binary format predates held-item trade tracking.
        // Cross-reference with the Gen 4 tree to surface the actual held item requirement.
        if (method.Argument == 0 && Pokemon is not null)
        {
            var modernTree = EvolutionTree.GetEvolutionTree(EntityContext.Gen4);
            var modernMethods = modernTree.Forward.GetForward(Pokemon.Species, Pokemon.Form);
            foreach (var m in modernMethods.Span)
            {
                if (m.Species == method.Species && m.Method == EvolutionType.TradeHeldItem && m.Argument != 0)
                {
                    var items = GameInfo.Strings.GetItemStrings(EntityContext.Gen4);
                    var itemName = m.Argument < items.Length ? items[m.Argument] : $"item #{m.Argument}";
                    return $"Trade holding {itemName}";
                }
            }
        }
        return "Trade";
    }

    private string GetItemName(ushort itemId)
    {
        // Use the context-specific item list — Gen 1/2/3 have different item ID spaces
        // from the modern unified list (itemlist). Using the wrong list produces wrong names
        // (e.g. "Lemonade" instead of "Fire Stone" for a Gen 1 Eevee).
        var items = Pokemon is not null
            ? GameInfo.Strings.GetItemStrings(Pokemon.Context)
            : GameInfo.Strings.itemlist;
        return itemId < items.Length ? items[itemId] : $"item #{itemId}";
    }

    private string GetMoveName(ushort moveId) =>
        moveId < GameInfo.Strings.movelist.Length
            ? GameInfo.Strings.movelist[moveId]
            : $"move #{moveId}";

    private void Confirm()
    {
        if (_selected == default)
        {
            return;
        }

        MudDialog?.Close(DialogResult.Ok(_selected));
    }

    private void Cancel() => MudDialog?.Close(DialogResult.Cancel());
}
