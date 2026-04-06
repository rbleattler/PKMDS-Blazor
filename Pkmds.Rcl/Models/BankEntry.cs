namespace Pkmds.Rcl.Models;

public sealed record BankEntry
{
    public long Id { get; init; }
    public required PKM Pokemon { get; init; }
    public required string SpeciesName { get; init; }
    public string? Tag { get; init; }
    public DateTimeOffset AddedAt { get; init; }
}
