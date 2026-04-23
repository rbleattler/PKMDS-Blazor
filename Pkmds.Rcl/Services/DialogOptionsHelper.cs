namespace Pkmds.Rcl.Services;

public sealed class DialogOptionsHelper(IBrowserViewportService browserViewportService)
    : IDialogOptionsHelper
{
    public async Task<DialogOptions> BuildAsync(
        MaxWidth desktopMaxWidth,
        bool fullWidth = true,
        bool closeButton = true,
        bool closeOnEscapeKey = true,
        bool backdropClick = true)
    {
        var isXs = await IsXsBreakpointAsync();

        return new DialogOptions
        {
            MaxWidth = desktopMaxWidth,
            FullWidth = fullWidth,
            FullScreen = isXs,
            CloseButton = closeButton,
            CloseOnEscapeKey = closeOnEscapeKey,
            BackdropClick = backdropClick,
        };
    }

    private async Task<bool> IsXsBreakpointAsync()
    {
        // Guard against the viewport service being unavailable (e.g. during a
        // crash-handler dialog where JS interop may already be broken). In
        // that case, fall back to the desktop layout rather than rethrowing.
        try
        {
            return await browserViewportService.GetCurrentBreakpointAsync() == Breakpoint.Xs;
        }
        catch
        {
            return false;
        }
    }
}
