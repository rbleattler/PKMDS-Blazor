namespace Pkmds.Rcl.Components;

public partial class InfoButton
{
    private bool open;
    private ElementReference anchor;

    /// <summary>The content to display inside the info popover.</summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment ChildContent { get; set; } = null!;

    private void Toggle() => open = !open;
    private void Close() => open = false;
}
