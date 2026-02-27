namespace Pkmds.Rcl.Components;

public partial class SaveFileComponent : RefreshAwareComponent
{
    private int activeTabIndex;

    private void JumpToPartyBox() => activeTabIndex = 0;
}
