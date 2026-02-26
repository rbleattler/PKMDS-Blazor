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

        var prop = Pokemon.GetType().GetProperty(propertyName);
        if (prop?.PropertyType != typeof(bool))
        {
            return;
        }

        var current = (bool)(prop.GetValue(Pokemon) ?? false);
        prop.SetValue(Pokemon, !current);
        StateHasChanged();
    }

    private void SetRibbonCount(string propertyName, int count)
    {
        if (Pokemon is null)
        {
            return;
        }

        var prop = Pokemon.GetType().GetProperty(propertyName);
        if (prop?.PropertyType != typeof(byte))
        {
            return;
        }

        prop.SetValue(Pokemon, (byte)count);
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
            var prop = Pokemon.GetType().GetProperty(ribbon.Name);
            if (prop?.PropertyType == typeof(bool))
            {
                prop.SetValue(Pokemon, true);
            }
            else if (prop?.PropertyType == typeof(byte))
            {
                prop.SetValue(Pokemon, (byte)ribbon.MaxCount);
            }
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
            var prop = Pokemon.GetType().GetProperty(ribbon.Name);
            if (prop?.PropertyType == typeof(bool))
            {
                prop.SetValue(Pokemon, false);
            }
            else if (prop?.PropertyType == typeof(byte))
            {
                prop.SetValue(Pokemon, (byte)0);
            }
        }

        StateHasChanged();
    }
}
