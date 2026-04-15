namespace Pkmds.Rcl.Components.MainTabPages;

public partial class TradeSlot : RefreshAwareComponent
{
    // Tracks which sprite combos have loaded high-res in this session to avoid re-flashing
    // the bundled fallback when re-rendering. Shared with PokemonSlotComponent's tracking semantics
    // but kept independent so a separate (species, ..., style) can be cached without stepping on it.
    private static readonly
        HashSet<(ushort Species, byte Form, uint FormArg, bool IsShiny, bool IsFemale, SpriteStyle Style)>
        HighResLoadedSpecies = [];

    private bool highResLoaded;
    private byte lastLoadedForm;
    private uint lastLoadedFormArg;
    private bool lastLoadedIsFemale;
    private bool lastLoadedIsShiny;
    private ushort lastLoadedSpecies;
    private SpriteStyle lastLoadedSpriteStyle;

    private LegalityStatus? legalityStatus;

    [Parameter]
    public PKM? Pokemon { get; set; }

    [Parameter]
    public bool IsSelected { get; set; }

    [Parameter]
    public EventCallback OnSlotClick { get; set; }

    // Version of the save file owning this slot — Game-style high-res sprites are version-specific
    // (e.g. RB vs YW Gen 1 art) so each pane passes its own save's Version instead of piggybacking
    // on AppState.SaveFile which always points at Save A.
    [Parameter]
    public GameVersion OwnerVersion { get; set; } = GameVersion.Any;

    // Resolve a species name via the full per-language table (GameInfo.GetStrings),
    // bypassing GameInfo.FilteredSources — the filtered source is pinned to whichever
    // save PKHeX last initialized against, so Gen 5 species render blank when Save A
    // is Gen 1. Always use the app language, not the save's game language.
    private static string GetSpeciesTitle(ushort species)
    {
        var names = GameInfo.GetStrings(GameInfo.CurrentLanguage).specieslist;
        return species < names.Length ? names[species] : "Unknown";
    }

    protected override void OnParametersSet()
    {
        ComputeLegality();
        UpdateSpriteState();
    }

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
        highResLoaded = lastLoadedSpecies > 0
                        && HighResLoadedSpecies.Contains((lastLoadedSpecies, lastLoadedForm, lastLoadedFormArg,
                            lastLoadedIsShiny, lastLoadedIsFemale, lastLoadedSpriteStyle));
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
        /* keep showing the bundled sprite — highResLoaded is already false */
    }

    private async Task HandleClick() => await OnSlotClick.InvokeAsync();

    private bool ShouldShow(LegalityStatus status) => status switch
    {
        LegalityStatus.Legal => AppState.ShowLegalIndicator,
        LegalityStatus.Fishy => AppState.ShowFishyIndicator,
        LegalityStatus.Illegal => AppState.ShowIllegalIndicator,
        _ => false
    };

    private static string StatusTitle(LegalityStatus status) => status switch
    {
        LegalityStatus.Legal => "Legal",
        LegalityStatus.Fishy => "Fishy",
        _ => "Illegal"
    };

    // Use solid glyphs rather than the *Circle / Cancel variants — those are drawn as
    // cutouts and inherit the sprite behind them. The coloured disc comes from the
    // wrapper's CSS class, so the glyph itself just needs to be a solid white symbol.
    private static string StatusIcon(LegalityStatus status) => status switch
    {
        LegalityStatus.Legal => Icons.Material.Filled.Check,
        LegalityStatus.Fishy => Icons.Material.Filled.PriorityHigh,
        _ => Icons.Material.Filled.Close
    };

    private static string StatusClass(LegalityStatus status) => status switch
    {
        LegalityStatus.Legal => "legality-indicator-icon--legal",
        LegalityStatus.Fishy => "legality-indicator-icon--fishy",
        _ => "legality-indicator-icon--illegal"
    };

    private void ComputeLegality()
    {
        if (Pokemon is not { Species: > 0 } || AppState.IsHaXEnabled)
        {
            legalityStatus = null;
            return;
        }

        var la = AppService.GetLegalityAnalysis(Pokemon);
        var hasInvalid = la.Results.Any(r => r.Judgement == PKHeX.Core.Severity.Invalid)
                         || !MoveResult.AllValid(la.Info.Moves)
                         || !MoveResult.AllValid(la.Info.Relearn);
        if (hasInvalid)
        {
            legalityStatus = LegalityStatus.Illegal;
            return;
        }

        var hasFishy = la.Results.Any(r => r.Judgement == PKHeX.Core.Severity.Fishy);
        legalityStatus = hasFishy
            ? LegalityStatus.Fishy
            : LegalityStatus.Legal;
    }
}
