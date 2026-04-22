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
        (shinyLeaf & 1 << bit) != 0;

    private static int SetShinyLeafBit(int shinyLeaf, int bit, bool value) =>
        value
            ? shinyLeaf | 1 << bit
            : shinyLeaf & ~(1 << bit);

    private static string GetTeraTypeDisplayName(byte teraTypeId) => teraTypeId == TeraTypeUtil.Stellar
        ? GameInfo.Strings.Types[TeraTypeUtil.StellarTypeDisplayStringIndex]
        : GameInfo.Strings.Types[teraTypeId];

    private static IEnumerable<MoveType> TeraTypeOriginalItems =>
        Enum.GetValues<MoveType>()
            .Where(m => m != MoveType.Any)
            .Concat([(MoveType)TeraTypeUtil.Stellar]);

    private static IEnumerable<MoveType> TeraTypeOverrideItems =>
        Enumerable.Repeat((MoveType)TeraTypeUtil.OverrideNone, 1)
            .Concat(TeraTypeOriginalItems);

    private static string GetTeraTypeText(MoveType teraType) =>
        (byte)teraType == TeraTypeUtil.OverrideNone
            ? Constants.EmptyIndex
            : GetTeraTypeDisplayName((byte)teraType);

    private static readonly IReadOnlyList<(StatusCondition Value, string Text)> StatusConditionItems =
    [
        (StatusCondition.None, "Healthy"),
        (StatusCondition.Sleep1, "Sleeping (1 turn)"),
        (StatusCondition.Sleep2, "Sleeping (2 turns)"),
        (StatusCondition.Sleep3, "Sleeping (3 turns)"),
        (StatusCondition.Sleep4, "Sleeping (4 turns)"),
        (StatusCondition.Sleep5, "Sleeping (5 turns)"),
        (StatusCondition.Sleep6, "Sleeping (6 turns)"),
        (StatusCondition.Sleep7, "Sleeping (7 turns)"),
        (StatusCondition.Poison, "Poisoned"),
        (StatusCondition.Burn, "Burned"),
        (StatusCondition.Freeze, "Frozen"),
        (StatusCondition.Paralysis, "Paralyzed"),
        (StatusCondition.PoisonBad, "Badly Poisoned")
    ];

    private static IEnumerable<StatusCondition> StatusConditionValues =>
        StatusConditionItems.Select(i => i.Value);

    private static string GetStatusConditionText(StatusCondition value)
    {
        foreach (var (v, t) in StatusConditionItems)
        {
            if (v == value)
            {
                return t;
            }
        }

        return value.ToString();
    }
}
