namespace Pkmds.Rcl.Components;

public partial class BagItemInfoButton
{
    [Parameter, EditorRequired] public int ItemIndex { get; set; }
    [Parameter, EditorRequired] public string ItemName { get; set; } = string.Empty;
    [Parameter, EditorRequired] public InventoryType PouchType { get; set; }
    [Parameter, EditorRequired] public EntityContext Context { get; set; }
    [Parameter, EditorRequired] public GameVersion Version { get; set; }

    private bool _open;
    private bool _loaded;
    private ItemSummary? _itemInfo;
    private MoveSummary? _moveInfo;
    private ushort? _moveId;
    private byte? _moveType;

    protected override async Task OnInitializedAsync()
    {
        if (ItemIndex != 0 && PouchType == InventoryType.TMHMs)
        {
            _moveId = await TryResolveTMMoveIdAsync();
            if (_moveId.HasValue && AppState.SaveFile is { Context: var ctx })
                _moveType = MoveInfo.GetType(_moveId.Value, ctx);
            StateHasChanged();
        }
    }

    private async Task Toggle()
    {
        _open = !_open;
        if (_open && !_loaded)
            await LoadAsync();
    }

    private void Close() => _open = false;

    private async Task LoadAsync()
    {
        _moveId ??= await TryResolveTMMoveIdAsync();

        if (_moveId.HasValue)
        {
            _moveInfo = await DescriptionService.GetMoveInfoAsync(_moveId.Value, Version);
        }
        else
        {
            _itemInfo = await DescriptionService.GetItemInfoAsync(ItemName, Version);
        }

        _loaded = true;
        StateHasChanged();
    }

    /// <summary>
    /// For items in the TM/HM pouch, looks up the move name from tm-data.json using the
    /// TM number extracted from the item name prefix (e.g. "TM001" → "001"), then resolves
    /// that to a move ID via the game string list. Returns null for non-TM items or when
    /// no TM data is available for the current game.
    /// </summary>
    private async Task<ushort?> TryResolveTMMoveIdAsync()
    {
        if (PouchType != InventoryType.TMHMs)
            return null;

        // Extract the TM/HM/TR prefix and number: "TM001 Hone Claws" → key "001",
        // "TR00 Swords Dance" → key "TR00", "HM01" → key "HM01"
        if (ItemName.Length < 3)
            return null;
        var prefix = ItemName.Split(' ')[0]; // e.g. "TM001", "HM01", "TR00"
        string lookupKey;
        if (prefix.StartsWith("TR", StringComparison.OrdinalIgnoreCase))
        {
            // TR items: keep full prefix in key to distinguish from TM00
            var trNumber = prefix[2..];
            if (!trNumber.All(char.IsDigit)) return null;
            lookupKey = $"TR{trNumber}";
        }
        else if (prefix.StartsWith("HM", StringComparison.OrdinalIgnoreCase))
        {
            // HM items use "HM01"-style keys in hm-data.json to avoid colliding with TM keys.
            var hmNumber = prefix[2..];
            if (!hmNumber.All(char.IsDigit)) return null;
            var hmMoveName = await DescriptionService.GetHMMoveNameAsync($"HM{hmNumber}", Version);
            if (hmMoveName is null)
                return null;
            var movelist = GameInfo.Strings.movelist;
            for (ushort i = 1; i < movelist.Length; i++)
            {
                if (string.Equals(movelist[i], hmMoveName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return null;
        }
        else if (prefix.StartsWith("TM", StringComparison.OrdinalIgnoreCase))
        {
            var tmNumber = prefix[2..];
            if (!tmNumber.All(char.IsDigit)) return null;
            lookupKey = tmNumber;
        }
        else
        {
            return null;
        }

        var moveName = await DescriptionService.GetTMMoveNameAsync(lookupKey, Version);
        // Some game versions (e.g. gen9sv) use 3-digit keys ("001"–"099"); retry with zero-padding.
        if (moveName is null && lookupKey.Length < 3 && lookupKey.All(char.IsDigit))
            moveName = await DescriptionService.GetTMMoveNameAsync(lookupKey.PadLeft(3, '0'), Version);
        if (moveName is null)
            return null;

        var tmMovelist = GameInfo.Strings.movelist;
        for (ushort i = 1; i < tmMovelist.Length; i++)
        {
            if (string.Equals(tmMovelist[i], moveName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return null;
    }
}
