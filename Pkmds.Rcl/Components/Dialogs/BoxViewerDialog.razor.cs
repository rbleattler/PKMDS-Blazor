namespace Pkmds.Rcl.Components.Dialogs;

public partial class BoxViewerDialog : RefreshAwareComponent
{
    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    [Parameter]
    public int InitialBox { get; set; }

    private int _currentBox;

    protected override RefreshEvents SubscribeTo => RefreshEvents.BoxState;

    protected override void OnInitialized()
    {
        _currentBox = InitialBox;
        base.OnInitialized();
    }

    private void GoToNextBox()
    {
        if (AppState.SaveFile is not { } saveFile)
        {
            return;
        }

        _currentBox = (_currentBox + 1) % saveFile.BoxCount;
    }

    private void GoToPreviousBox()
    {
        if (AppState.SaveFile is not { } saveFile)
        {
            return;
        }

        _currentBox = (_currentBox - 1 + saveFile.BoxCount) % saveFile.BoxCount;
    }

    private async Task OpenBoxList()
    {
        MudDialog?.Close();
        await DialogService.ShowAsync<BoxListDialog>(
            "All Boxes",
            new DialogOptions
            {
                MaxWidth = MaxWidth.ExtraExtraLarge,
                FullWidth = true,
                CloseButton = true,
                BackdropClick = true,
                CloseOnEscapeKey = true,
            });
    }

    private void Close() => MudDialog?.Close();
}
