namespace Pkmds.Rcl.Models;

public sealed record BatchEditorPreset
{
    public required string Name { get; init; }
    public required string Script { get; init; }
    public DateTimeOffset SavedAt { get; init; }
}
