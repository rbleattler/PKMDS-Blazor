namespace Pkmds.Blazor.Components;

public partial class PokemonDetailsComponent : IDisposable
{
    protected override void OnInitialized() => AppState.OnAppStateChanged += StateHasChanged;

    public void Dispose() => AppState.OnAppStateChanged -= StateHasChanged;
}