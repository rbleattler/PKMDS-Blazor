namespace Pkmds.Rcl.Services;

public sealed class DialogOptionsHelper : IDialogOptionsHelper
{
    // Narrow-viewport layout is handled purely in CSS (see app.css — the
    // @media (max-width: 959px) block targeting .mud-dialog). That makes the
    // layout responsive to live window resizes, which setting FullScreen at
    // open-time could not — MudBlazor bakes FullScreen into the dialog once
    // and won't re-react to viewport changes without closing and reopening.
    public Task<DialogOptions> BuildAsync(
        MaxWidth desktopMaxWidth,
        bool fullWidth = true,
        bool closeButton = true,
        bool closeOnEscapeKey = true,
        bool backdropClick = true) =>
        Task.FromResult(new DialogOptions
        {
            MaxWidth = desktopMaxWidth,
            FullWidth = fullWidth,
            CloseButton = closeButton,
            CloseOnEscapeKey = closeOnEscapeKey,
            BackdropClick = backdropClick,
        });
}
