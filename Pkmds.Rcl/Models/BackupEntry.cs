namespace Pkmds.Rcl.Models;

/// <summary>
/// Represents a save file backup entry for display in the backup manager.
/// Does not include the raw save bytes — those are fetched on demand for restore/export.
/// </summary>
public sealed record BackupEntry
{
    public long Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string SaveType { get; init; } = string.Empty;
    public int Generation { get; init; }
    public string GameVersion { get; init; } = string.Empty;
    public string TrainerName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public bool IsManicEmu { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string Source { get; init; } = string.Empty;
}
