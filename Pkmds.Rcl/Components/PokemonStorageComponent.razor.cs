namespace Pkmds.Rcl.Components;

public partial class PokemonStorageComponent : RefreshAwareComponent
{
    private void GoToNextBox()
    {
        if (AppState.SaveFile is null || AppState.BoxEdit is null)
        {
            return;
        }

        AppState.BoxEdit.MoveRight();
        AppState.SaveFile.CurrentBox = AppState.BoxEdit.CurrentBox;

        AppState.SelectedBoxNumber = null;
        AppState.SelectedBoxSlotNumber = null;

        RefreshService.RefreshBoxState();
    }

    private void GoToPreviousBox()
    {
        if (AppState.SaveFile is null || AppState.BoxEdit is null)
        {
            return;
        }

        AppState.BoxEdit.MoveLeft();
        AppState.SaveFile.CurrentBox = AppState.BoxEdit.CurrentBox;

        AppState.SelectedBoxNumber = null;
        AppState.SelectedBoxSlotNumber = null;

        RefreshService.RefreshBoxState();
    }

    private void OnBoxChanged(int newBox)
    {
        if (AppState.SaveFile is null || AppState.BoxEdit is null)
        {
            return;
        }

        AppState.BoxEdit.LoadBox(newBox);
        AppState.SaveFile.CurrentBox = AppState.BoxEdit.CurrentBox;

        AppState.SelectedBoxNumber = null;
        AppState.SelectedBoxSlotNumber = null;

        RefreshService.RefreshBoxState();
    }

    private int GetBoxPokemonCount(int boxId)
    {
        if (AppState.SaveFile is not { } saveFile)
            return 0;

        var count = 0;
        for (var slot = 0; slot < saveFile.BoxSlotCount; slot++)
        {
            if (saveFile.GetBoxSlotAtIndex(boxId, slot).Species != 0)
                count++;
        }
        return count;
    }

    private async Task OpenBoxLayoutDialog()
    {
        await DialogService.ShowAsync<BoxLayoutDialog>(
            "Box Layout",
            new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true });
        RefreshService.RefreshBoxState();
    }
}
