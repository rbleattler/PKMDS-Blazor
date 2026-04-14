namespace Pkmds.Rcl.Services;

/// <summary>
/// Provides persistent client-side storage for save file backups using the browser's IndexedDB.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Creates a backup of the given save file bytes and metadata.
    /// Returns the auto-assigned backup ID.
    /// </summary>
    Task<long> CreateBackupAsync(byte[] saveBytes, SaveFile saveFile, string? fileName, bool isManicEmu, string source);

    /// <summary>Returns metadata for all stored backups (without raw bytes).</summary>
    Task<IReadOnlyList<BackupEntry>> GetAllMetadataAsync();

    /// <summary>Returns the raw save bytes for the backup with the given <paramref name="id" />, or null if not found.</summary>
    Task<byte[]?> GetBackupBytesAsync(long id);

    /// <summary>Removes the backup with the given <paramref name="id" />.</summary>
    Task DeleteAsync(long id);

    /// <summary>Removes all backups.</summary>
    Task ClearAsync();

    /// <summary>
    /// Deletes the oldest backups so the total count does not exceed <paramref name="maxBackups" />.
    /// </summary>
    Task EnforceRetentionAsync(int maxBackups);
}
