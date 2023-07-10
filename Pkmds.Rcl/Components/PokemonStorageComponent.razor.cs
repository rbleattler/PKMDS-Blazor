namespace Pkmds.Rcl.Components;

public partial class PokemonStorageComponent : IDisposable
{
    protected override void OnInitialized() => AppState.OnAppStateChanged += StateHasChanged;

    public void Dispose() => AppState.OnAppStateChanged -= StateHasChanged;

    private void NavigateRight()
    {
        if (AppState.SaveFile is null)
        {
            return;
        }

        if (AppState.SaveFile.CurrentBox == AppState.SaveFile.BoxCount - 1)
        {
            AppState.SaveFile.CurrentBox = 0;
        }
        else
        {
            AppState.SaveFile.CurrentBox++;
        }

        AppState.SelectedBoxNumber = null;
        AppState.SelectedBoxSlotNumber = null;
    }

    private void NavigateLeft()
    {
        if (AppState.SaveFile is null)
        {
            return;
        }

        if (AppState.SaveFile.CurrentBox == 0)
        {
            AppState.SaveFile.CurrentBox = AppState.SaveFile.BoxCount - 1;
        }
        else
        {
            AppState.SaveFile.CurrentBox--;
        }

        AppState.SelectedBoxNumber = null;
        AppState.SelectedBoxSlotNumber = null;
    }
}
