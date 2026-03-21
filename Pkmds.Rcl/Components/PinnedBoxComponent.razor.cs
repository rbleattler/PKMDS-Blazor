namespace Pkmds.Rcl.Components;

public partial class PinnedBoxComponent : RefreshAwareComponent
{
    protected override RefreshEvents SubscribeTo => RefreshEvents.AppState | RefreshEvents.BoxState;

    private void OnBoxChanged(int newBox)
    {
        AppState.PinnedBoxNumber = newBox;
        StateHasChanged();
    }

    private void GoToPreviousBox()
    {
        if (AppState is not { SaveFile: { } saveFile, PinnedBoxNumber: { } currentBox })
        {
            return;
        }

        OnBoxChanged(currentBox == 0 ? saveFile.BoxCount - 1 : currentBox - 1);
    }

    private void GoToNextBox()
    {
        if (AppState is not { SaveFile: { } saveFile, PinnedBoxNumber: { } currentBox })
        {
            return;
        }

        OnBoxChanged(currentBox == saveFile.BoxCount - 1 ? 0 : currentBox + 1);
    }

    private int GetBoxPokemonCount(int boxId)
    {
        if (AppState.SaveFile is not { } saveFile)
        {
            return 0;
        }

        var count = 0;
        for (var slot = 0; slot < saveFile.BoxSlotCount; slot++)
        {
            if (saveFile.GetBoxSlotAtIndex(boxId, slot).Species != 0)
            {
                count++;
            }
        }

        return count;
    }
}
