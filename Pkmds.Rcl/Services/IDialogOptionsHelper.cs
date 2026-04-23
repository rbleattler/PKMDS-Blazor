namespace Pkmds.Rcl.Services;

/// <summary>
/// Builds <see cref="DialogOptions"/> with mobile-aware defaults, promoting
/// dialogs to full-screen on the Xs breakpoint so their content scrolls
/// against the viewport rather than producing nested scrollbars.
/// </summary>
public interface IDialogOptionsHelper
{
    /// <summary>
    /// Builds dialog options sized for the desktop breakpoint, with
    /// <see cref="DialogOptions.FullScreen"/> forced to <c>true</c> when the
    /// current viewport is <see cref="Breakpoint.Xs"/>.
    /// </summary>
    Task<DialogOptions> BuildAsync(
        MaxWidth desktopMaxWidth,
        bool fullWidth = true,
        bool closeButton = true,
        bool closeOnEscapeKey = true,
        bool backdropClick = true);
}
