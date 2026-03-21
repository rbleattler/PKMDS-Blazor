namespace Pkmds.Rcl.Components.EditForms.Tabs;

public partial class GenSpecificTab : IDisposable
{
    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    public void Dispose() =>
        RefreshService.OnAppStateChanged -= StateHasChanged;

    protected override void OnInitialized() =>
        RefreshService.OnAppStateChanged += StateHasChanged;

    private static bool GetShinyLeafBit(int shinyLeaf, int bit) =>
        (shinyLeaf & (1 << bit)) != 0;

    private static int SetShinyLeafBit(int shinyLeaf, int bit, bool value) =>
        value ? shinyLeaf | (1 << bit) : shinyLeaf & ~(1 << bit);

    private static string GetTeraTypeDisplayName(byte teraTypeId) => teraTypeId == TeraTypeUtil.Stellar
        ? GameInfo.Strings.Types[TeraTypeUtil.StellarTypeDisplayStringIndex]
        : GameInfo.Strings.Types[teraTypeId];
}
