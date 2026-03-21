namespace Pkmds.Rcl.Components;

public partial class PinnedBoxComponent : RefreshAwareComponent
{
    protected override RefreshEvents SubscribeTo => RefreshEvents.AppState | RefreshEvents.BoxState;

    private static string GetBoxLabel(SaveFile saveFile, int boxNumber)
    {
        var boxName = saveFile is IBoxDetailNameRead boxDetailNameRead
            ? boxDetailNameRead.GetBoxName(boxNumber)
            : string.Empty;
        return $"Box {boxNumber + 1}: {boxName}";
    }

    private void GoToPreviousBox()
    {
        if (AppState is not { SaveFile: { } saveFile, PinnedBoxNumber: { } currentBox })
        {
            return;
        }

        AppState.PinnedBoxNumber = currentBox == 0 ? saveFile.BoxCount - 1 : currentBox - 1;
        StateHasChanged();
    }

    private void GoToNextBox()
    {
        if (AppState is not { SaveFile: { } saveFile, PinnedBoxNumber: { } currentBox })
        {
            return;
        }

        AppState.PinnedBoxNumber = currentBox == saveFile.BoxCount - 1 ? 0 : currentBox + 1;
        StateHasChanged();
    }
}
