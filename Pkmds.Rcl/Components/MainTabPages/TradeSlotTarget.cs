namespace Pkmds.Rcl.Components.MainTabPages;

/// <summary>
/// Identifies a slot in a Trade tab pane — used as the drop-target payload so
/// <see cref="TradeTab" /> can route cross-save and intra-save transfers based on which
/// save file owns the slot and whether it is a party or box slot.
/// </summary>
/// <param name="OwnerSaveFile">The save file that owns this slot (slot A or slot B).</param>
/// <param name="IsParty">Whether this is a party slot.</param>
/// <param name="BoxNumber">The 0-based box number (null for party slots or Let's Go).</param>
/// <param name="SlotNumber">The 0-based slot index within the box or party.</param>
public readonly record struct TradeSlotTarget(
    SaveFile OwnerSaveFile,
    bool IsParty,
    int? BoxNumber,
    int SlotNumber);
