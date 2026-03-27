namespace Pkmds.Rcl.Models;

/// <summary>Result from a move description lookup, resolved for a specific game version.</summary>
[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
public sealed record MoveSummary(
    string Name,
    string Type,
    string Category,
    int? Power,
    int? Pp,
    int? Accuracy,
    string Description,
    string Target,
    IReadOnlyList<string> Flags,
    int Priority = 0);

/// <summary>Result from an ability description lookup.</summary>
public sealed record AbilitySummary(
    string Name,
    string Description);

/// <summary>Result from an item description lookup.</summary>
public sealed record ItemSummary(
    string Name,
    string Description);
