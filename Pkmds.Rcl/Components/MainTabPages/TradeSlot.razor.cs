namespace Pkmds.Rcl.Components.MainTabPages;

public partial class TradeSlot : RefreshAwareComponent
{
    private LegalityStatus? legalityStatus;

    [Parameter]
    public PKM? Pokemon { get; set; }

    [Parameter]
    public bool IsSelected { get; set; }

    [Parameter]
    public EventCallback OnSlotClick { get; set; }

    // Resolve a species name via the full per-language table (GameInfo.GetStrings),
    // bypassing GameInfo.FilteredSources — the filtered source is pinned to whichever
    // save PKHeX last initialized against, so Gen 5 species render blank when Save A
    // is Gen 1. Always use the app language, not the save's game language.
    private static string GetSpeciesTitle(ushort species)
    {
        var names = GameInfo.GetStrings(GameInfo.CurrentLanguage).specieslist;
        return species < names.Length ? names[species] : "Unknown";
    }

    protected override void OnParametersSet() => ComputeLegality();

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

    private static string StatusIcon(LegalityStatus status) => status switch
    {
        LegalityStatus.Legal => Icons.Material.Filled.CheckCircle,
        LegalityStatus.Fishy => Icons.Material.Filled.Warning,
        _ => Icons.Material.Filled.Cancel
    };

    private static Color StatusColor(LegalityStatus status) => status switch
    {
        LegalityStatus.Legal => Color.Success,
        LegalityStatus.Fishy => Color.Warning,
        _ => Color.Error
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
