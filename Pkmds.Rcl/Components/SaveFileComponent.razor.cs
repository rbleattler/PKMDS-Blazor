namespace Pkmds.Rcl.Components;

public partial class SaveFileComponent : RefreshAwareComponent
{
    private int activeTabIndex;

    private void JumpToPartyBox() => activeTabIndex = 0;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        RefreshServiceField!.OnRequestJumpToPartyBox += HandleRequestJumpToPartyBox;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            RefreshServiceField!.OnRequestJumpToPartyBox -= HandleRequestJumpToPartyBox;
        }
    }

    private void HandleRequestJumpToPartyBox()
    {
        JumpToPartyBox();
        _ = InvokeAsync(StateHasChanged);
    }
}
