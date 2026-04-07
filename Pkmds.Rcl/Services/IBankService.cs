namespace Pkmds.Rcl.Services;

/// <summary>
/// Provides persistent client-side storage for PKM files using the browser's IndexedDB.
/// </summary>
public interface IBankService
{
    /// <summary>Returns all stored Pokémon entries.</summary>
    Task<IReadOnlyList<BankEntry>> GetAllAsync();

    /// <summary>Adds a single Pokémon to the bank.</summary>
    Task AddAsync(PKM pkm, string? tag = null, string? sourceSave = null);

    /// <summary>Adds multiple Pokémon to the bank.</summary>
    Task AddRangeAsync(IEnumerable<PKM> pokemon, string? tag = null, string? sourceSave = null);

    /// <summary>Removes the entry with the given <paramref name="id"/> from the bank.</summary>
    Task DeleteAsync(long id);

    /// <summary>Removes all entries from the bank.</summary>
    Task ClearAsync();

    /// <summary>Exports all bank entries as a JSON byte array suitable for download.</summary>
    Task<byte[]> ExportAsync();

    /// <summary>
    /// Imports entries from a previously-exported JSON byte array.
    /// Existing entries are preserved; imported entries receive new IDs.
    /// </summary>
    Task ImportAsync(byte[] data);

    /// <summary>
    /// Returns <see langword="true"/> if the bank already contains an entry whose
    /// decrypted box data matches <paramref name="pkm"/>.
    /// </summary>
    Task<bool> IsDuplicateAsync(PKM pkm);

    /// <summary>
    /// Splits <paramref name="candidates"/> into Pokémon not already in the bank
    /// (<c>Unique</c>) and those that match an existing entry by decrypted box data
    /// (<c>Duplicates</c>), using a single bank read — O(N+M) instead of O(N×M).
    /// </summary>
    Task<(IReadOnlyList<PKM> Unique, IReadOnlyList<PKM> Duplicates)> PartitionDuplicatesAsync(
        IEnumerable<PKM> candidates);
}
