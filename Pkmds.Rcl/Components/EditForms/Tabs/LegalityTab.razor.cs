namespace Pkmds.Rcl.Components.EditForms.Tabs;

using PKHexSeverity = PKHeX.Core.Severity;

public partial class LegalityTab : IDisposable
{
    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    private LegalityAnalysis? Analysis { get; set; }

    private bool IsLegal => Analysis is { } la && la.Results.All(r => r.Valid);

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
