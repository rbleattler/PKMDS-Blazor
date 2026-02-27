namespace Pkmds.Rcl.Components.EditForms.Tabs;

public partial class RibbonsTab : IDisposable
{
    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    [Parameter]
    public LegalityAnalysis? Analysis { get; set; }

    public void Dispose() =>
        RefreshService.OnAppStateChanged -= StateHasChanged;

    /// <summary>
    /// Returns the worst-severity legality check result for a given ribbon property name,
    /// or null if the ribbon is valid. Ribbon check results store the <see cref="RibbonIndex" />
    /// in <see cref="CheckResult.Argument" />; we match by looking up the property name in
    /// <see cref="RibbonIndex" /> enum values.
    /// </summary>
    private CheckResult? GetRibbonCheckResult(string propertyName)
    {
        if (Analysis is not { } la)
        {
            return null;
        }

        // Map property name → RibbonIndex.  The property name is e.g. "RibbonChampionG3",
        // while RibbonIndex values are e.g. ChampionG3.  Strip the leading "Ribbon" or "Mark" prefix.
        var shortName = propertyName.StartsWith("RibbonMark", StringComparison.Ordinal)
            ? propertyName["Ribbon".Length..] // keep "MarkXxx" form
            : propertyName.StartsWith("Ribbon", StringComparison.Ordinal)
                ? propertyName["Ribbon".Length..] // strip leading "Ribbon"
                : propertyName;

        if (!Enum.TryParse<RibbonIndex>(shortName, ignoreCase: true, out var idx))
        {
            return null;
        }

        CheckResult? worst = null;
        foreach (var r in la.Results)
        {
            if (r.Valid)
            {
                continue;
            }

            if (r.Identifier is not (CheckIdentifier.Ribbon or CheckIdentifier.RibbonMark))
            {
                continue;
            }

            if ((RibbonIndex)r.Argument != idx)
            {
                continue;
            }

            if (worst is null || r.Judgement > worst.Value.Judgement)
            {
                worst = r;
            }
        }

        return worst;
    }

    private IEnumerable<(string DisplayName, CheckResult Result)> GetInvalidRibbonResults()
    {
        if (Analysis is not { } la)
        {
            yield break;
        }

        foreach (var r in la.Results)
        {
            if (r.Valid)
            {
                continue;
            }

            if (r.Identifier is not (CheckIdentifier.Ribbon or CheckIdentifier.RibbonMark))
            {
                continue;
            }

            var propertyName = "Ribbon" + ((RibbonIndex)r.Argument);
            yield return (GetRibbonDisplayName(propertyName), r);
        }
    }

    private string HumanizeRibbonCheckResult(CheckResult result)
    {
        if (Analysis is not { } la)
        {
            return string.Empty;
        }

        var ctx = LegalityLocalizationContext.Create(la);
        return ctx.Humanize(in result, verbose: false);
    }

    protected override void OnInitialized() =>
        RefreshService.OnAppStateChanged += StateHasChanged;

    private static string GetRibbonDisplayName(string propertyName) =>
        RibbonHelper.GetRibbonDisplayName(propertyName);

    private static string GetRibbonSprite(RibbonInfo info) =>
        RibbonHelper.GetRibbonSprite(info);

    private static bool IsMarkEntry(string name) =>
        RibbonHelper.IsMarkEntry(name);

    private List<RibbonInfo> GetAllRibbonInfo() =>
        RibbonHelper.GetAllRibbonInfo(Pokemon);

    private void ToggleRibbon(string propertyName)
    {
        if (Pokemon is null)
        {
            return;
        }

        var current = ReflectUtil.GetValue(Pokemon, propertyName) is bool and true;
        ReflectUtil.SetValue(Pokemon, propertyName, !current);
        StateHasChanged();
    }

    private void SetRibbonCount(string propertyName, int count)
    {
        if (Pokemon is null)
        {
            return;
        }

        // Clamp the incoming count to a valid range before casting to byte.
        var maxAllowed = (int)byte.MaxValue;
        foreach (var ribbon in GetAllRibbonInfo())
        {
            if (ribbon.Name != propertyName)
            {
                continue;
            }

            if (ribbon.MaxCount < maxAllowed)
            {
                maxAllowed = ribbon.MaxCount;
            }

            break;
        }

        var clamped = count;
        if (clamped < 0)
        {
            clamped = 0;
        }

        if (clamped > maxAllowed)
        {
            clamped = maxAllowed;
        }

        ReflectUtil.SetValue(Pokemon, propertyName, (byte)clamped);
        StateHasChanged();
    }

    private void GiveAllRibbons()
    {
        if (Pokemon is null)
        {
            return;
        }

        foreach (var ribbon in GetAllRibbonInfo())
        {
            var value = ribbon.Type is RibbonValueType.Boolean
                ? (object)true
                : (byte)ribbon.MaxCount;
            ReflectUtil.SetValue(Pokemon, ribbon.Name, value);
        }

        StateHasChanged();
    }

    private void ClearAllRibbons()
    {
        if (Pokemon is null)
        {
            return;
        }

        foreach (var ribbon in GetAllRibbonInfo())
        {
            var value = ribbon.Type is RibbonValueType.Boolean
                ? (object)false
                : (byte)0;
            ReflectUtil.SetValue(Pokemon, ribbon.Name, value);
        }

        StateHasChanged();
    }
}
