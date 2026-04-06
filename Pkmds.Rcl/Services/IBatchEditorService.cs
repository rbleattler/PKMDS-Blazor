namespace Pkmds.Rcl.Services;

public enum BatchEditorScope { Party, Boxes, All }

public sealed record BatchEditorSummary(int Modified, int Skipped);

public interface IBatchEditorService
{
    /// <summary>Dry-run: returns preview of what would change, without mutating save data.</summary>
    Task<IReadOnlyList<BatchEditorPreviewEntry>> PreviewAsync(string script, BatchEditorScope scope);

    /// <summary>Apply: mutates save data and writes back to the SaveFile.</summary>
    Task<BatchEditorSummary> ApplyAsync(string script, BatchEditorScope scope);

    /// <summary>Snapshots current save state for undo.</summary>
    bool CreateSnapshot();

    /// <summary>Restores save state from the most recent snapshot.</summary>
    bool RestoreSnapshot();

    /// <summary>Whether a snapshot is available for undo.</summary>
    bool HasSnapshot { get; }

    /// <summary>Preset management (localStorage).</summary>
    Task<IReadOnlyList<BatchEditorPreset>> GetPresetsAsync();

    Task SavePresetAsync(BatchEditorPreset preset);

    Task DeletePresetAsync(string name);
}
