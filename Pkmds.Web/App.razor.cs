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

    private async Task ShowCrashReportDialog(Exception? exception)
    {
        try
        {
            var parameters = new DialogParameters { { nameof(BugReportDialog.HasSaveFile), AppState.SaveFile is not null }, { nameof(BugReportDialog.AppVersion), AppState.AppVersion ?? string.Empty }, { nameof(BugReportDialog.CapturedException), exception } };
            var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = false, BackdropClick = false };
            var dialog = await DialogService.ShowAsync<BugReportDialog>("Report this crash", parameters, options);
            await dialog.Result;
        }
        catch
        {
            // MudDialogProvider lives inside the ErrorBoundary's child content (via MainLayout).
            // When the boundary fires, the entire child tree is unmounted — the dialog provider
            // is gone and ShowAsync silently fails. Fall back to opening the GitHub new-issue
            // page in a new tab with crash details pre-filled.
            await OpenGitHubIssueAsync(exception);
        }
    }

    private async Task OpenGitHubIssueAsync(Exception? exception)
    {
        var title = Uri.EscapeDataString($"[Crash] {exception?.GetType().Name}: {exception?.Message}");
        var body = Uri.EscapeDataString(
            $"**Version:** {AppState.AppVersion}\n\n" +
            $"**Error:** `{exception?.GetType().Name}: {exception?.Message}`\n\n" +
            $"**Stack trace:**\n```\n{exception?.StackTrace?.Trim()}\n```\n\n" +
            "**Steps to reproduce:**\n*(Please describe what you were doing when this crash occurred)*");
        var url = $"https://github.com/codemonkey85/PKMDS-Blazor/issues/new?title={title}&body={body}&labels=bug";
        await JsRuntime.InvokeVoidAsync("window.open", url, "_blank");
    }
}
