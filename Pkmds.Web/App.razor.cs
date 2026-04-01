using MudBlazor;
using Pkmds.Rcl.Components.Dialogs;

namespace Pkmds.Web;

public partial class App
{
    private ErrorBoundary? errorBoundary;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        await JsRuntime.InvokeVoidAsync("addUpdateListener");
    }

    private async void ShowCrashReportDialog(Exception? exception)
    {
        var parameters = new DialogParameters
        {
            { nameof(BugReportDialog.HasSaveFile), AppState.SaveFile is not null },
            { nameof(BugReportDialog.AppVersion), AppState.AppVersion ?? string.Empty },
            { nameof(BugReportDialog.CapturedException), exception },
        };
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseOnEscapeKey = false,
            BackdropClick = false,
        };
        var dialog = await DialogService.ShowAsync<BugReportDialog>("Report this crash", parameters, options);
        await dialog.Result;
    }
}
