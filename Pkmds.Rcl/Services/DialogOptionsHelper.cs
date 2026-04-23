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
        var isNarrow = await IsNarrowBreakpointAsync();

        return new DialogOptions
        {
            MaxWidth = desktopMaxWidth,
            FullWidth = fullWidth,
            FullScreen = isNarrow,
            CloseButton = closeButton,
            CloseOnEscapeKey = closeOnEscapeKey,
            BackdropClick = backdropClick,
        };
    }

    private async Task<bool> IsNarrowBreakpointAsync()
    {
        // Promote dialogs to full-screen on Sm-and-down so both phones (Xs, <600px)
        // and narrow iPad / narrow desktop browser windows (Sm, 600–959px) avoid
        // the nested-scrollbar pattern. Guard against the viewport service being
        // unavailable (e.g. during a crash-handler dialog where JS interop may
        // already be broken) by falling back to the desktop layout.
        try
        {
            var breakpoint = await browserViewportService.GetCurrentBreakpointAsync();
            return breakpoint is Breakpoint.Xs or Breakpoint.Sm;
        }
        catch
        {
            return false;
        }
    }
}
