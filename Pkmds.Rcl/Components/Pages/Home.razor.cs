namespace Pkmds.Rcl.Components.Pages;

// ReSharper disable once UnusedType.Global
public partial class Home : IDisposable
{
    public void Dispose() => RefreshService.OnAppStateChanged -= StateHasChanged;

    protected override void OnInitialized() => RefreshService.OnAppStateChanged += StateHasChanged;
}
