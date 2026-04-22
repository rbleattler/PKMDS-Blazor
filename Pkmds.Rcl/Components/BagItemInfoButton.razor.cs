namespace Pkmds.Rcl.Components;

public partial class BagItemInfoButton
{
    private ItemSummary? itemInfo;
    private bool loaded;
    private ushort? moveId;
    private MoveSummary? moveInfo;
    private byte? moveType;

    private bool open;

    [Parameter]
    [EditorRequired]
    public int ItemIndex { get; set; }

    [Parameter]
    [EditorRequired]
    public string ItemName { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public InventoryType PouchType { get; set; }

    [Parameter]
    [EditorRequired]
    public EntityContext Context { get; set; }

    [Parameter]
    [EditorRequired]
    public GameVersion Version { get; set; }

    protected override async Task OnInitializedAsync()
    {
        // Pouch-type check removed: Gen 1/2 saves use a single bag with no TMHMs pouch,
        // yet TM items there still need the move-lookup path. TryResolveTmMoveIdAsync
        // guards internally by item-name prefix.
        if (ItemIndex != 0)
        {
            moveId = await TryResolveTmMoveIdAsync();
            if (moveId.HasValue && AppState.SaveFile is { Context: var ctx })
            {
                moveType = MoveInfo.GetType(moveId.Value, ctx);
            }

            StateHasChanged();
        }
    }

    private async Task Toggle()
    {
        open = !open;
        if (open && !loaded)
        {
            await LoadAsync();
        }
    }

    private void Close() => open = false;

    private async Task LoadAsync()
    {
        moveId ??= await TryResolveTmMoveIdAsync();

        if (moveId.HasValue)
        {
            moveInfo = await DescriptionService.GetMoveInfoAsync(moveId.Value, Version);
        }
        else
        {
            // BagTab enriches TM/HM/TR names to "TM41 (Softboiled)" for display. Strip that
            // suffix before the item-info lookup so we hit the underlying "tm41" JSON key.
            var lookupName = ItemName;
            var parenIdx = lookupName.IndexOf(" (", StringComparison.Ordinal);
            if (parenIdx > 0)
            {
                lookupName = lookupName[..parenIdx];
            }

            itemInfo = await DescriptionService.GetItemInfoAsync(lookupName, Version);
        }

        loaded = true;
        StateHasChanged();
    }

    /// <summary>
    /// For items in the TM/HM pouch, looks up the move name from tm-data.json using the
    /// TM number extracted from the item name prefix (e.g. "TM001" → "001"), then resolves
    /// that to a move ID via the game string list. Returns null for non-TM items or when
    /// no TM data is available for the current game.
    /// </summary>
    private async Task<ushort?> TryResolveTmMoveIdAsync()
    {
        // Guard by item-name prefix rather than pouch type — Gen 1/2 have no TMHMs pouch,
        // but their single-bag items named "TM41" etc. are still TMs.
        // Extract the TM/HM/TR prefix and number: "TM001 Hone Claws" → key "001",
        // "TR00 Swords Dance" → key "TR00", "HM01" → key "HM01"
        if (ItemName.Length < 3)
        {
            return null;
        }

        var prefix = ItemName.Split(' ')[0]; // e.g. "TM001", "HM01", "TR00"
        string lookupKey;
        if (prefix.StartsWith("TR", StringComparison.OrdinalIgnoreCase))
        {
            // TR items: keep full prefix in key to distinguish from TM00
            var trNumber = prefix[2..];
            if (!trNumber.All(char.IsDigit))
            {
                return null;
            }

            lookupKey = $"TR{trNumber}";
        }
        else if (prefix.StartsWith("HM", StringComparison.OrdinalIgnoreCase))
        {
            // HM items use "HM01"-style keys in hm-data.json to avoid colliding with TM keys.
            var hmNumber = prefix[2..];
            if (!hmNumber.All(char.IsDigit))
            {
                return null;
            }

            var hmMoveName = await DescriptionService.GetHmMoveNameAsync($"HM{hmNumber}", Version);
            if (hmMoveName is null)
            {
                return null;
            }

            var hmId = GameInfoUtilities.FindMoveIdByName(hmMoveName);
            return hmId > 0 ? hmId : null;
        }
        else if (prefix.StartsWith("TM", StringComparison.OrdinalIgnoreCase))
        {
            var tmNumber = prefix[2..];
            if (!tmNumber.All(char.IsDigit))
            {
                return null;
            }

            lookupKey = tmNumber;
        }
        else
        {
            return null;
        }

        var moveName = await DescriptionService.GetTmMoveNameAsync(lookupKey, Version);
        // Some game versions (e.g. gen9sv) use 3-digit keys ("001"–"099"); retry with zero-padding.
        if (moveName is null && lookupKey.Length < 3 && lookupKey.All(char.IsDigit))
        {
            moveName = await DescriptionService.GetTmMoveNameAsync(lookupKey.PadLeft(3, '0'), Version);
        }

        if (moveName is null)
        {
            return null;
        }

        // tm-data.json is Bulbapedia-sourced and sometimes differs in hyphenation/spacing
        // from PKHeX's canonical move names (e.g. "Softboiled" vs "Soft-Boiled",
        // "ThunderPunch" vs "Thunder Punch"). FindMoveIdByName handles the mismatch.
        var tmId = GameInfoUtilities.FindMoveIdByName(moveName);
        return tmId > 0 ? tmId : null;
    }
}
