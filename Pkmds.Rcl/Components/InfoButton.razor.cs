namespace Pkmds.Rcl.Components;

public partial class InfoButton
{
    private bool open;

    /// <summary>
    /// The title shown in the popover's header row alongside the close
    /// button. Keeps the left side of the header from looking empty and
    /// gives the popover a clear subject line without duplicating it in
    /// the body.
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>The content to display inside the info popover.</summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment ChildContent { get; set; } = null!;

    private void Toggle() => open = !open;
    private void Close() => open = false;
}
