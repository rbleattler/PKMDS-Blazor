namespace Pkmds.Rcl.Components.Dialogs;

public partial class BoxListDialog : RefreshAwareComponent
{
    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    protected override RefreshEvents SubscribeTo => RefreshEvents.AppState | RefreshEvents.BoxState;

    private void SwapWithNext(int boxIndex, int boxCount)
    {
        var nextBox = (boxIndex + 1) % boxCount;
        if (!AppService.SwapBoxes(boxIndex, nextBox))
        {
            Snackbar.Add("Locked or team slots prevent swapping these boxes.", Severity.Warning);
        }
    }

    private void Close() => MudDialog?.Close();
}
