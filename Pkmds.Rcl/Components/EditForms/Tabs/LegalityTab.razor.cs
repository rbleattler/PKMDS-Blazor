namespace Pkmds.Rcl.Components.EditForms.Tabs;

using PKHexSeverity = PKHeX.Core.Severity;

public partial class LegalityTab : IDisposable
{
    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    private LegalityAnalysis? Analysis { get; set; }

    private bool IsLegal => Analysis is { } la && la.Results.All(r => r.Valid);

    private bool HasRibbonIssues => Analysis is { } la &&
        la.Results.Any(r => !r.Valid && r.Identifier is CheckIdentifier.Ribbon or CheckIdentifier.RibbonMark);

    private bool HasMoveIssues => Analysis is { } la &&
        la.Results.Any(r => !r.Valid && r.Identifier is CheckIdentifier.CurrentMove);

    private bool HasRelearnMoveIssues => Analysis is { } la &&
        la.Results.Any(r => !r.Valid && r.Identifier is CheckIdentifier.RelearnMove);

    private bool HasBallIssues => Analysis is { } la &&
        la.Results.Any(r => !r.Valid && r.Identifier is CheckIdentifier.Ball);

    private bool HasEncounterIssues => Analysis is { } la &&
        la.Results.Any(r => !r.Valid && r.Identifier is CheckIdentifier.Encounter);

    private bool HasMetLocationIssues => Analysis is { } la &&
        la.Results.Any(r => !r.Valid && r.Identifier is CheckIdentifier.Level or CheckIdentifier.Encounter);

    public void Dispose() =>
        RefreshService.OnAppStateChanged -= Refresh;

    protected override void OnInitialized()
    {
        RefreshService.OnAppStateChanged += Refresh;
        ComputeAnalysis();
    }

    protected override void OnParametersSet() => ComputeAnalysis();

    private void Refresh()
    {
        ComputeAnalysis();
        StateHasChanged();
    }

    private void ComputeAnalysis() =>
        Analysis = Pokemon is { Species: > 0 }
            ? AppService.GetLegalityAnalysis(Pokemon)
            : null;

    private void RemoveInvalidRibbons()
    {
        if (Pokemon is null || Analysis is not { } la)
        {
            return;
        }

        RibbonApplicator.RemoveAllValidRibbons(la);
        RefreshService.Refresh();
        Snackbar.Add("Invalid ribbons removed. Click Save to apply changes.", MudBlazor.Severity.Success);
    }

    private void AddValidRibbons()
    {
        if (Pokemon is null || Analysis is not { } la)
        {
            return;
        }

        RibbonApplicator.SetAllValidRibbons(la);
        RefreshService.Refresh();
        Snackbar.Add("All obtainable ribbons added. Click Save to apply changes.", MudBlazor.Severity.Success);
    }

    private void SuggestMoves()
    {
        if (Pokemon is null)
        {
            return;
        }

        MoveSetApplicator.SetMoveset(Pokemon, random: false);

        // Update Technical Records (Gen 8+ SwSh / SV / ZA) to reflect the new moves,
        // mirroring PKMEditor's SetSuggestedMoves behaviour.
        if (Pokemon is ITechRecord tr)
        {
            tr.ClearRecordFlags();
            var freshLa = AppService.GetLegalityAnalysis(Pokemon);
            tr.SetRecordFlags(Pokemon, TechnicalRecordApplicatorOption.LegalCurrent, freshLa);
        }

        RefreshService.Refresh();
        Snackbar.Add("Moves updated with a legal move set. Click Save to apply changes.", MudBlazor.Severity.Success);
    }

    private void SuggestRelearnMoves()
    {
        if (Pokemon is null || Analysis is not { } la)
        {
            return;
        }

        MoveSetApplicator.SetRelearnMoves(Pokemon, la);
        RefreshService.Refresh();
        Snackbar.Add("Relearn moves updated. Click Save to apply changes.", MudBlazor.Severity.Success);
    }

    private void SuggestBall()
    {
        if (Pokemon is null || Analysis is not { } la)
        {
            return;
        }

        BallApplicator.ApplyBallLegalByColor(Pokemon, la, PersonalColorUtil.GetColor(Pokemon));
        RefreshService.Refresh();
        Snackbar.Add("Ball updated to a legal option. Click Save to apply changes.", MudBlazor.Severity.Success);
    }

    private void SuggestMetLocation()
    {
        if (Pokemon is null)
        {
            return;
        }

        var encounter = EncounterSuggestion.GetSuggestedMetInfo(Pokemon);
        if (encounter is null)
        {
            Snackbar.Add("No met location suggestion is available for this Pokémon.", MudBlazor.Severity.Warning);
            return;
        }

        Pokemon.MetLocation = encounter.Location;
        var metLevel = encounter.GetSuggestedMetLevel(Pokemon);
        Pokemon.MetLevel = metLevel;

        // A Pokémon's current level must be at least its met level.
        // For freshly-created Pokémon the level defaults to 1, so raise it now.
        if (Pokemon.CurrentLevel < metLevel)
        {
            Pokemon.CurrentLevel = metLevel;
            AppService.LoadPokemonStats(Pokemon);
        }

        RefreshService.Refresh();
        Snackbar.Add("Met location and level updated. Click Save to apply changes.", MudBlazor.Severity.Success);
    }

    private static Color GetSeverityColor(PKHexSeverity severity) => severity switch
    {
        PKHexSeverity.Valid => Color.Success,
        PKHexSeverity.Fishy => Color.Warning,
        _ => Color.Error,
    };

    private static string GetSeverityIcon(PKHexSeverity severity) => severity switch
    {
        PKHexSeverity.Valid => Icons.Material.Filled.CheckCircle,
        PKHexSeverity.Fishy => Icons.Material.Filled.Warning,
        _ => Icons.Material.Filled.Cancel,
    };

    private static string GetIdentifierLabel(CheckIdentifier id) => id switch
    {
        CheckIdentifier.CurrentMove => "Move",
        CheckIdentifier.RelearnMove => "Relearn Move",
        CheckIdentifier.Encounter => "Encounter",
        CheckIdentifier.Shiny => "Shiny",
        CheckIdentifier.EC => "Encryption Constant",
        CheckIdentifier.PID => "PID",
        CheckIdentifier.Gender => "Gender",
        CheckIdentifier.EVs => "EVs",
        CheckIdentifier.Language => "Language",
        CheckIdentifier.Nickname => "Nickname",
        CheckIdentifier.Trainer => "Trainer",
        CheckIdentifier.IVs => "IVs",
        CheckIdentifier.Level => "Level",
        CheckIdentifier.Ball => "Ball",
        CheckIdentifier.Memory => "Memory",
        CheckIdentifier.Geography => "Geo Locations",
        CheckIdentifier.Form => "Form",
        CheckIdentifier.Egg => "Egg",
        CheckIdentifier.Misc => "Misc",
        CheckIdentifier.Fateful => "Fateful Encounter",
        CheckIdentifier.Ribbon => "Ribbon",
        CheckIdentifier.Training => "Training",
        CheckIdentifier.Ability => "Ability",
        CheckIdentifier.Evolution => "Evolution",
        CheckIdentifier.Nature => "Nature",
        CheckIdentifier.GameOrigin => "Game Origin",
        CheckIdentifier.HeldItem => "Held Item",
        CheckIdentifier.RibbonMark => "Ribbon/Mark",
        CheckIdentifier.GVs => "GVs",
        CheckIdentifier.Marking => "Marking",
        CheckIdentifier.AVs => "AVs",
        CheckIdentifier.TrashBytes => "Trash Bytes",
        CheckIdentifier.SlotType => "Slot Type",
        CheckIdentifier.Handler => "Handler",
        _ => id.ToString(),
    };

    private string HumanizeResult(CheckResult result)
    {
        if (Analysis is not { } la)
        {
            return result.Result.ToString();
        }

        var ctx = LegalityLocalizationContext.Create(la, "en");
        return ctx.Humanize(in result, verbose: false);
    }
}
