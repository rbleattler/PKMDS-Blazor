namespace Pkmds.Rcl.Components.EditForms.Tabs;

public partial class MetTab : IDisposable
{
    private ItemSummary? ballInfo;
    private EntityContext currentLocationSearchContext = EntityContext.None;

    private GameVersion currentLocationSearchVersion = GameVersion.Any;

    private PKM? lastPokemon;

    private EntityContext originFormat = EntityContext.None;

    /// <summary>
    /// Currently loaded met location group that is populating Met and Egg location comboboxes
    /// </summary>
    private GameVersion origintrack;

    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    [Parameter]
    public LegalityAnalysis? Analysis { get; set; }

    private MetTimeOfDay GetMetTimeOfDay => Pokemon is not (PK2 and ICaughtData2 c2)
        ? MetTimeOfDay.None
        : (MetTimeOfDay)c2.MetTimeOfDay;

    private bool PokemonMetAsEgg => Pokemon is not null && (Pokemon.IsEgg || Pokemon.WasEgg || Pokemon.WasTradedEgg);

    private GameVersion BattleVersionValue
    {
        get => Pokemon is IBattleVersion bv
            ? bv.BattleVersion
            : GameVersion.Any;
        set
        {
            if (Pokemon is IBattleVersion bv)
            {
                bv.BattleVersion = value;
            }
        }
    }

    private byte ObedienceLevelValue
    {
        get => Pokemon is IObedienceLevel ol
            ? ol.ObedienceLevel
            : (byte)0;
        set
        {
            if (Pokemon is IObedienceLevel ol)
            {
                ol.ObedienceLevel = value;
            }
        }
    }

    private string? OriginMarkSpriteFileName => Pokemon is { Format: >= 6 }
        ? ImageHelper.GetOriginMarkSpriteFileName(OriginMarkUtil.GetOriginMark(Pokemon))
        : null;

    private bool ShowGroundTile => Pokemon is IGroundTile && Pokemon.Gen4 && Pokemon.Format < 7;

    private IEnumerable<GameVersion> OriginGameItems =>
        GameInfo.FilteredSources.Games.Select(g => (GameVersion)g.Value);

    private static string GetBattleVersionText(GameVersion version) =>
        version == GameVersion.Any
            ? "None"
            : GameInfo.GetVersionName(version);

    private string? CurrentBallSpriteFileName =>
        Pokemon is { Ball: > 0 } ? ImageHelper.GetBallSpriteFilename(Pokemon.Ball) : null;

    private static IEnumerable<byte> BallItems =>
        GameInfo.FilteredSources.Balls.DistinctBy(b => b.Value).Select(b => (byte)b.Value);

    private static string GetBallText(byte ballValue) =>
        GameInfo.FilteredSources.Balls.FirstOrDefault(b => b.Value == ballValue)?.Text ?? string.Empty;

    private static IEnumerable<GroundTileType> GroundTileItems =>
        GameInfo.FilteredSources.G4GroundTiles.Select(t => (GroundTileType)t.Value);

    private static string GetGroundTileText(GroundTileType tile) =>
        GameInfo.FilteredSources.G4GroundTiles.FirstOrDefault(t => t.Value == (int)tile)?.Text ?? tile.ToString();

    private static IEnumerable<MetTimeOfDay> MetTimeOfDayItems => Enum.GetValues<MetTimeOfDay>();

    public void Dispose() =>
        RefreshService.OnAppStateChanged -= StateHasChanged;

    protected override async Task OnParametersSetAsync()
    {
        if (ReferenceEquals(Pokemon, lastPokemon))
        {
            return;
        }

        lastPokemon = Pokemon;
        await LoadBallInfoAsync();
    }

    private async Task LoadBallInfoAsync()
    {
        if (Pokemon is null || AppState.SaveFile is not { } sav)
        {
            return;
        }

        var ballName = Pokemon.Ball != 0
            ? GameInfo.FilteredSources.Balls.FirstOrDefault(b => b.Value == Pokemon.Ball)?.Text
            : null;
        ballInfo = ballName is not null
            ? await DescriptionService.GetItemInfoAsync(ballName, sav.Version)
            : null;
    }

    private async Task OnBallChanged()
    {
        await LoadBallInfoAsync();
        StateHasChanged();
    }

    private GroundTileType GetGroundTile() => Pokemon is IGroundTile g
        ? g.GroundTile
        : GroundTileType.None;

    private void SetGroundTile(GroundTileType tile)
    {
        if (Pokemon is IGroundTile g)
        {
            g.GroundTile = tile;
        }
    }

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
        RefreshService.OnAppStateChanged += StateHasChanged;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (AppState.SaveFile is not { } saveFile)
        {
            return;
        }

        CheckMetLocationChange(saveFile.Version, saveFile.Context);
    }

    private void CheckMetLocationChange(GameVersion version, EntityContext context)
    {
        if (AppState.SaveFile is not { } saveFile)
        {
            return;
        }

        // Does the list of locations need to be changed to another group?
        var group = GameUtil.GetMetLocationVersionGroup(version);
        if (group is GameVersion.Invalid)
        {
            group = GameUtil.GetMetLocationVersionGroup(saveFile.Version);
            if (group is GameVersion.Invalid || version is GameVersion.Any)
            {
                version = group = context.GetSingleGameVersion();
            }
        }

        if (group != origintrack || context != originFormat)
        {
            currentLocationSearchVersion = version;
            currentLocationSearchContext = context;
        }

        origintrack = group;
        originFormat = context;
    }

    private ComboItem GetMetLocation()
    {
        if (Pokemon is not { } pkm)
        {
            return new("NONE", -1);
        }

        CheckMetLocationChange(pkm.Version, pkm.Context);

        return AppService.GetMetLocationComboItem(Pokemon.MetLocation, currentLocationSearchVersion,
            currentLocationSearchContext);
    }

    private ComboItem GetEggMetLocation()
    {
        if (Pokemon is not { } pkm)
        {
            return new("NONE", -1);
        }

        CheckMetLocationChange(pkm.Version, pkm.Context);
        return AppService.GetMetLocationComboItem(Pokemon.EggLocation, currentLocationSearchVersion,
            currentLocationSearchContext, true);
    }

    private void OriginGameChanged()
    {
        if (Pokemon is not { } pkm)
        {
            return;
        }

        CheckMetLocationChange(pkm.Version, pkm.Context);
    }

    private Task<IEnumerable<ComboItem>> SearchMetLocations(string searchString, CancellationToken token) =>
        Task.FromResult(AppService.SearchMetLocations(searchString, currentLocationSearchVersion,
            currentLocationSearchContext));

    private Task<IEnumerable<ComboItem>> SearchEggMetLocations(string searchString, CancellationToken token) =>
        Task.FromResult(AppService.SearchMetLocations(searchString, currentLocationSearchVersion,
            currentLocationSearchContext, true));

    private void SetMetTimeOfDay(MetTimeOfDay metTimeOfDay)
    {
        if (Pokemon is not (PK2 and ICaughtData2 c2))
        {
            return;
        }

        c2.MetTimeOfDay = (int)metTimeOfDay;
    }

    private void MetAsEggChanged(bool newValue)
    {
        if (Pokemon is null)
        {
            return;
        }

        switch (newValue)
        {
            case false:
                {
                    if (Pokemon.IsEgg)
                    {
                        Pokemon.IsEgg = false;
                    }

                    Pokemon.EggDay = Pokemon.EggMonth = Pokemon.EggYear = 0;
                    Pokemon.EggLocation = 0;
                    break;
                }
            case true:
                {
                    var currentMetDate = Pokemon.MetDate;
                    Pokemon.SetEggMetData(Pokemon.Version, Pokemon.Version);
                    Pokemon.EggMetDate = Pokemon.MetDate = currentMetDate;
                    break;
                }
        }
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private enum MetTimeOfDay
    {
        None,
        Morning,
        Day,
        Night
    }
}
