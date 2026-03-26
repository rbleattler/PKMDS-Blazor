namespace Pkmds.Rcl.Services;

public interface IDescriptionService
{
    /// <summary>
    /// Returns move info (name, type, category, stats, description) for the given PokeAPI move ID,
    /// resolved to the stats and flavor text appropriate for the specified game version.
    /// Returns <c>null</c> if the move ID is not found.
    /// </summary>
    Task<MoveSummary?> GetMoveInfoAsync(int moveId, GameVersion version);

    /// <summary>
    /// Returns ability info (name, description) for the given PokeAPI ability ID.
    /// Returns <c>null</c> if the ability ID is not found.
    /// </summary>
    Task<AbilitySummary?> GetAbilityInfoAsync(int abilityId, GameVersion version);

    /// <summary>
    /// Returns item info (name, description) for the given item name as it appears in PKHeX.
    /// The lookup is case-insensitive. Returns <c>null</c> if the item is not found.
    /// </summary>
    Task<ItemSummary?> GetItemInfoAsync(string itemName, GameVersion version);

    /// <summary>
    /// Returns the move name taught by the TM with the given zero-padded number (e.g. "01", "001")
    /// for the specified game version, or <c>null</c> if not found or the game has no TM data.
    /// </summary>
    Task<string?> GetTmMoveNameAsync(string tmNumber, GameVersion version);

    /// <summary>
    /// Returns the move name taught by the HM with the given key (e.g. "HM01", "HM05")
    /// for the specified game version, or <c>null</c> if not found or the game has no HMs.
    /// </summary>
    Task<string?> GetHmMoveNameAsync(string hmKey, GameVersion version);
}
