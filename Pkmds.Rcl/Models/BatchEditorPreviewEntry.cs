namespace Pkmds.Rcl.Models;

public sealed record BatchEditorPreviewEntry
{
    public required string SpeciesName { get; init; }
    public required string Location { get; init; }
    public required IReadOnlyList<string> Changes { get; init; }
    public bool HasChanges => Changes.Count > 0;
}
