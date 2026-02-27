namespace Pkmds.Rcl.Components.EditForms.Tabs;

public partial class RibbonsTab : IDisposable
{
    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    public void Dispose() =>
        RefreshService.OnAppStateChanged -= StateHasChanged;

    protected override void OnInitialized() =>
        RefreshService.OnAppStateChanged += StateHasChanged;

    internal static string GetRibbonDisplayName(string propertyName) =>
        RibbonHelper.GetRibbonDisplayName(propertyName);

    internal static string GetRibbonSprite(RibbonInfo info) =>
        RibbonHelper.GetRibbonSprite(info);

    internal static bool IsMarkEntry(string name) =>
        RibbonHelper.IsMarkEntry(name);

    internal List<RibbonInfo> GetAllRibbonInfo() =>
        RibbonHelper.GetAllRibbonInfo(Pokemon);

    private void ToggleRibbon(string propertyName)
    {
        if (Pokemon is null)
        {
            return;
        }

        var current = ReflectUtil.GetValue(Pokemon, propertyName) is bool value && value;
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
            if (ribbon.Name == propertyName)
            {
                if (ribbon.MaxCount < maxAllowed)
                {
                    maxAllowed = ribbon.MaxCount;
                }

                break;
            }
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
