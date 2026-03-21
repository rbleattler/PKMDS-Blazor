namespace Pkmds.Rcl.Components.Dialogs;

public partial class BoxViewerDialog : RefreshAwareComponent
{
    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    [Inject]
    private IBrowserViewportService BrowserViewportService { get; set; } = default!;

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
        var isXs = await BrowserViewportService.GetCurrentBreakpointAsync() == Breakpoint.Xs;
        await DialogService.ShowAsync<BoxListDialog>(
            "All Boxes",
            new DialogOptions
            {
                MaxWidth = MaxWidth.ExtraExtraLarge,
                FullWidth = true,
                FullScreen = isXs,
                CloseButton = true,
                BackdropClick = true,
                CloseOnEscapeKey = true,
            });
    }

    private void Close() => MudDialog?.Close();
}
