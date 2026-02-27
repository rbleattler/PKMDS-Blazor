namespace Pkmds.Rcl.Components.EditForms.Tabs;

public partial class ContestStatsTab : IDisposable
{
    private const byte ContestStatMax = byte.MaxValue;

    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    public void Dispose() =>
        RefreshService.OnAppStateChanged -= StateHasChanged;

    protected override void OnInitialized() =>
        RefreshService.OnAppStateChanged += StateHasChanged;

    private static void MaxAllStats(IContestStats contestStats)
    {
        contestStats.ContestCool = ContestStatMax;
        contestStats.ContestBeauty = ContestStatMax;
        contestStats.ContestCute = ContestStatMax;
        contestStats.ContestSmart = ContestStatMax;
        contestStats.ContestTough = ContestStatMax;
        contestStats.ContestSheen = ContestStatMax;
    }

    private static void ClearAllStats(IContestStats contestStats)
    {
        contestStats.ContestCool = 0;
        contestStats.ContestBeauty = 0;
        contestStats.ContestCute = 0;
        contestStats.ContestSmart = 0;
        contestStats.ContestTough = 0;
        contestStats.ContestSheen = 0;
    }
}
