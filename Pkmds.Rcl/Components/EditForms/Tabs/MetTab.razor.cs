namespace Pkmds.Rcl.Components.EditForms.Tabs;

public partial class MetTab : IDisposable
{
    protected override void OnInitialized() =>
        AppState.OnAppStateChanged += StateHasChanged;

    public void Dispose() =>
        AppState.OnAppStateChanged -= StateHasChanged;
}
