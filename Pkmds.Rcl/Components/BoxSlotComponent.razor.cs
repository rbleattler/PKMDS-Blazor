namespace Pkmds.Rcl.Components;

public partial class BoxSlotComponent : IDisposable
{
    [Parameter, EditorRequired]
    public int BoxNumber { get; set; }

    [Parameter, EditorRequired]
    public int SlotNumber { get; set; }

    [Parameter, EditorRequired]
    public PKM? Pokemon { get; set; }

    private string Style => AppState.SelectedBoxSlotNumber == SlotNumber
        ? "border: 4px solid orange; border-radius: 6px;"
        : string.Empty;

    protected override void OnInitialized() =>
        AppState.OnAppStateChanged += StateHasChanged;

    public void Dispose() =>
        AppState.OnAppStateChanged -= StateHasChanged;

    private void SetSelectedPokemon() =>
        AppState.SetSelectedBoxPokemon(Pokemon, BoxNumber, SlotNumber);
}
