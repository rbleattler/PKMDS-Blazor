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

    [Parameter]
    public SaveFile? OwnerSave { get; set; }

    // Resolve a species name using the owner save's language, bypassing
    // GameInfo.FilteredSources (which is pinned to whichever save PKHeX last
    // initialized against — can't name Gen 5 species when Save A is Gen 1).
    internal string GetSpeciesTitle(ushort species)
    {
        if (OwnerSave is null)
        {
            return AppService.GetPokemonSpeciesName(species) ?? "Unknown";
        }

        var lang = OwnerSave.Language >= 0
            ? GameLanguage.LanguageCode(OwnerSave.Language)
            : GameInfo.CurrentLanguage;
        var names = GameInfo.GetStrings(lang).specieslist;
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
