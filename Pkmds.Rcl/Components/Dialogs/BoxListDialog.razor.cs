namespace Pkmds.Rcl.Components.Dialogs;

public partial class BoxListDialog : RefreshAwareComponent
{
    private const int BoxBatchSize = 4;

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    protected override RefreshEvents SubscribeTo => RefreshEvents.AppState | RefreshEvents.BoxState;

    private int renderedBoxCount;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var total = AppState.SaveFile?.BoxCount ?? 0;
        if (renderedBoxCount >= total)
        {
            return;
        }

        await Task.Yield();
        renderedBoxCount = Math.Min(renderedBoxCount + BoxBatchSize, total);
        StateHasChanged();
    }

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
